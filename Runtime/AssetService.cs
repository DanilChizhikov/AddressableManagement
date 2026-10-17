using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DTech.AddressableManagement.Internal.Delivery;
using DTech.AddressableManagement.Internal.Diagnostics;
using DTech.AddressableManagement.Internal.Downloads;
using DTech.AddressableManagement.Internal.Gateway;
using DTech.AddressableManagement.Internal.Loading;
using DTech.AddressableManagement.Internal.Scenes;
using UnityEngine;

namespace DTech.AddressableManagement
{
    /// <summary>
    /// Default IAssetService over Unity Addressables. Build one per application from the composition root and
    /// dispose it on shutdown.
    /// </summary>
    public sealed class AssetService : IAssetService
    {
        /// <inheritdoc/>
        public event Action<ContentFailure> OnFailed;

        private readonly CancellationTokenSource _lifetime = new();
        private readonly AssetLog _log;
        private readonly ContentRoute _route;
        private readonly ContentContext _context;
        private readonly ContentBootstrapper _bootstrapper;
        private readonly CatalogUpdater _catalogs;
        private readonly DownloadStateCache _downloadCache = new();
        private readonly DownloadSuspension _downloadSuspension = new();
        private readonly DownloadCoordinator _downloads;
        private readonly HandlerTracker _handlers = new();
        private readonly AssetRegistry _assets;
        private readonly LoadPolicy _loadPolicy;

        /// <inheritdoc/>
        public bool IsInitialized => _bootstrapper.IsInitialized;

        /// <inheritdoc/>
        public CatalogState CatalogState => _catalogs.State;

        /// <inheritdoc/>
        public IContentRoute Route => _route;

        /// <inheritdoc/>
        public bool IsDownloadSuspended => _downloadSuspension.IsSuspended;

        /// <inheritdoc/>
        public bool EnableLogs
        {
            get => _log.Enabled;
            set => _log.Enabled = value;
        }

        private bool _isDisposed;

        /// <summary>
        /// Creates the service.
        /// </summary>
        /// <param name="settings">How the service behaves; read once.</param>
        public AssetService(AssetServiceSettings settings) : this(settings, null)
        {
        }

        internal AssetService(AssetServiceSettings settings, AddressablesGatewayBase gateway)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            _log = new AssetLog
            {
                Enabled = settings.EnableLogs,
            };

            _route = new ContentRoute(settings.Delivery, _log);
            gateway ??= new AddressablesGateway(_log);
            _context = new ContentContext(gateway, _route, _log, RaiseFailed, _lifetime.Token);

            _bootstrapper = new ContentBootstrapper(_context, settings.ResetCatalogCacheOnVersionChange);
            _catalogs = new CatalogUpdater(_context, _bootstrapper, settings.CleanBundleCacheAfterCatalogUpdate);
            _catalogs.OnCatalogsChanged += _downloadCache.Clear;
            _downloads = new DownloadCoordinator(_context, _bootstrapper, _catalogs, _downloadCache, _downloadSuspension);
            _loadPolicy = new LoadPolicy(settings.LoadRetry, settings.RebindShadersInEditor);
            _assets = new AssetRegistry(_context, _bootstrapper, _catalogs, _loadPolicy);
        }

        /// <inheritdoc/>
        public Task InitializeAsync(CancellationToken token = default)
        {
            ThrowIfDisposed();
            return _bootstrapper.EnsureInitializedAsync(token);
        }

        /// <inheritdoc/>
        public Task<CatalogState> UpdateCatalogsAsync(CancellationToken token = default)
        {
            ThrowIfDisposed();
            return _catalogs.UpdateAsync(token);
        }

        /// <inheritdoc/>
        public Task<long> GetDownloadSizeAsync(AssetKey key, CancellationToken token = default)
        {
            ThrowIfDisposed();
            DownloadTarget downloadTarget = DownloadTarget.ForKeys(new[] { RequireValid(key, nameof(key)).RuntimeKey });
            return _downloads.GetSizeAsync(downloadTarget, token);
        }

        /// <inheritdoc/>
        public Task<long> GetDownloadSizeAsync(IReadOnlyList<AssetKey> keys, CancellationToken token = default)
        {
            ThrowIfDisposed();
            var downloadTarget = DownloadTarget.ForKeys(RuntimeKeys(keys));
            return _downloads.GetSizeAsync(downloadTarget, token);
        }

        /// <inheritdoc/>
        public Task<long> GetRemoteContentSizeAsync(CancellationToken token = default)
        {
            ThrowIfDisposed();
            return _downloads.GetSizeAsync(DownloadTarget.RemoteContent, token);
        }

        /// <inheritdoc/>
        public IAssetDownloadResponse DownloadAssets(IReadOnlyList<AssetKey> keys, DownloadOptions options, CancellationToken token = default)
        {
            ThrowIfDisposed();
            var downloadTarget = DownloadTarget.ForKeys(RuntimeKeys(keys));
            return _downloads.Start(downloadTarget, options, token);
        }

        /// <inheritdoc/>
        public IAssetDownloadResponse DownloadRemoteContent(DownloadOptions options, CancellationToken token = default)
        {
            ThrowIfDisposed();
            return _downloads.Start(DownloadTarget.RemoteContent, options, token);
        }

        /// <inheritdoc/>
        public IDisposable SuspendDownloads()
        {
            ThrowIfDisposed();
            return _downloadSuspension.Suspend();
        }

        /// <inheritdoc/>
        public IAssetHandler<TAsset> LoadAsset<TAsset>(AssetKeyT<TAsset> key, CancellationToken lifetime = default)
            where TAsset : UnityEngine.Object
        {
            ThrowIfDisposed();
            if (key == null)
            {
                throw new ArgumentNullException(nameof(key));
            }

            var handler = new AssetHandler<TAsset>(key, _context, _handlers);
            handler.AttachCancellationToken(lifetime);
            if (handler.IsReleased)
            {
                return handler;
            }

            if (!key.IsValid)
            {
                var error = new ContentDeliveryException(FailureKind.InvalidKey, "The key does not point at any content.");
                RaiseFailed(new ContentFailure(ContentStage.Load, FailureKind.InvalidKey, key.RuntimeKey, error));
                handler.Fail(FailureKind.InvalidKey, error);
                return handler;
            }

            _assets.Load(handler);
            return handler;
        }

        /// <inheritdoc/>
        public IAssetHandler<IReadOnlyList<TAsset>> LoadAssets<TAsset>(LabelKey key,
            CancellationToken lifetime = default)
            where TAsset : UnityEngine.Object
        {
            ThrowIfDisposed();
            if (key == null)
            {
                throw new ArgumentNullException(nameof(key));
            }

            var handler = new AssetBatchHandler<TAsset>(key, _context, _handlers);
            handler.AttachCancellationToken(lifetime);
            if (handler.IsReleased)
            {
                return handler;
            }

            if (!key.IsValid)
            {
                var error = new ContentDeliveryException(FailureKind.InvalidKey, "The label is empty.");
                RaiseFailed(new ContentFailure(ContentStage.Load, FailureKind.InvalidKey, key.RuntimeKey, error));
                handler.Fail(error);
                return handler;
            }

            handler.Start(_assets);
            return handler;
        }

        /// <inheritdoc/>
        public ISceneHandler LoadScene(SceneKey key, SceneLoadOptions options, CancellationToken lifetime = default)
        {
            ThrowIfDisposed();
            if (key == null)
            {
                throw new ArgumentNullException(nameof(key));
            }

            var handler = new SceneHandler(key, options, _context, _handlers, _loadPolicy);
            handler.AttachCancellationToken(lifetime);
            if (handler.IsReleased)
            {
                return handler;
            }

            if (!key.IsValid)
            {
                var error = new ContentDeliveryException(FailureKind.InvalidKey, "The key does not point at any scene.");
                RaiseFailed(new ContentFailure(ContentStage.Scene, FailureKind.InvalidKey, key.RuntimeKey, error));
                handler.Fail(FailureKind.InvalidKey, error);
                return handler;
            }

            handler.Start(_bootstrapper, _catalogs);
            return handler;
        }

        /// <inheritdoc/>
        public void UnloadAsset(AssetKey key)
        {
            ThrowIfDisposed();
            if (key == null)
            {
                throw new ArgumentNullException(nameof(key));
            }

            _handlers.ReleaseAll(key.RuntimeKey);
        }

        /// <summary>
        /// Cancels running work, releases everything the service holds and stops rewriting Addressables requests.
        /// </summary>
        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;
            _lifetime.Cancel();
            _handlers.ReleaseLeaked(_log);
            _assets.Dispose();
            _bootstrapper.Dispose();
            OnFailed = null;
        }
        
        private static TKey RequireValid<TKey>(TKey key, string parameterName) where TKey : AssetKey
        {
            if (key == null)
            {
                throw new ArgumentNullException(parameterName);
            }

            if (!key.IsValid)
            {
                throw new ArgumentException($"The key '{key}' does not point at any content.", parameterName);
            }

            return key;
        }

        private static string[] RuntimeKeys(IReadOnlyList<AssetKey> keys)
        {
            if (keys == null)
            {
                throw new ArgumentNullException(nameof(keys));
            }

            var runtimeKeys = new string[keys.Count];
            for (int i = 0; i < keys.Count; i++)
            {
                runtimeKeys[i] = RequireValid(keys[i], nameof(keys)).RuntimeKey;
            }

            return runtimeKeys;
        }

        private void ThrowIfDisposed()
        {
            if (_isDisposed)
            {
                throw new ObjectDisposedException(nameof(AssetService));
            }
        }

        private void RaiseFailed(ContentFailure failure)
        {
            _log.ErrorOnce($"{failure.Stage}|{failure.Kind}|{failure.Key}", failure.ToString());
            try
            {
                OnFailed?.Invoke(failure);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }
    }
}