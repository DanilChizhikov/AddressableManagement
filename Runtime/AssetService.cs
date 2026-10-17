using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DTech.AddressableManagement.Internal.Delivery;
using DTech.AddressableManagement.Internal.Diagnostics;
using DTech.AddressableManagement.Internal.Downloads;
using DTech.AddressableManagement.Internal.Gateway;
using UnityEngine;

namespace DTech.AddressableManagement
{
    /// <summary>
    /// Default IAssetService over Unity Addressables. Build one per application from the composition root and
    /// dispose it on shutdown.
    /// </summary>
    public sealed class AssetService : IAssetService
    {
        /// <summary>
        /// Raised for content failures that retries and CDN fallbacks could not recover from.
        /// </summary>
        public event Action<ContentFailure> OnFailed;

        private readonly CancellationTokenSource _lifetime = new CancellationTokenSource();
        private readonly AssetLog _log;
        private readonly ContentRoute _route;
        private readonly ContentContext _context;
        private readonly ContentBootstrapper _bootstrapper;
        private readonly CatalogUpdater _catalogs;
        private readonly DownloadStateCache _downloadCache = new DownloadStateCache();
        private readonly DownloadSuspension _downloadSuspension = new DownloadSuspension();
        private readonly DownloadCoordinator _downloads;

        /// <summary>
        /// Whether Addressables finished initializing.
        /// </summary>
        public bool IsInitialized => _bootstrapper.IsInitialized;

        /// <summary>
        /// Outcome of the latest catalog update.
        /// </summary>
        public CatalogState CatalogState => _catalogs.State;

        /// <summary>
        /// The CDN route remote requests currently take.
        /// </summary>
        public IContentRoute Route => _route;

        /// <summary>
        /// Whether at least one download suspension is active.
        /// </summary>
        public bool IsDownloadSuspended => _downloadSuspension.IsSuspended;

        /// <summary>
        /// Logs routine progress. Warnings and errors are always logged.
        /// </summary>
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
        }

        /// <summary>
        /// Initializes Addressables and the CDN route. Concurrent calls share one initialization; a failed one is
        /// retried by the next call.
        /// </summary>
        /// <param name="token">Cancels the wait only, not the shared initialization.</param>
        /// <returns>A task that completes when Addressables is ready.</returns>
        public Task InitializeAsync(CancellationToken token = default)
        {
            ThrowIfDisposed();
            return _bootstrapper.EnsureInitializedAsync(token);
        }

        /// <summary>
        /// Checks for newer remote catalogs and applies them, moving along the CDN route on failures. Concurrent
        /// calls share one update.
        /// </summary>
        /// <param name="token">Cancels the wait only, not the shared update.</param>
        /// <returns>The outcome of the update.</returns>
        public Task<CatalogState> UpdateCatalogsAsync(CancellationToken token = default)
        {
            ThrowIfDisposed();
            return _catalogs.UpdateAsync(token);
        }

        /// <summary>
        /// Measures how many bytes the bundles behind the key still need to download.
        /// </summary>
        /// <param name="key">Any asset, label or scene key.</param>
        /// <param name="token">Cancels the query.</param>
        /// <returns>The number of bytes; 0 when everything is cached or local.</returns>
        public Task<long> GetDownloadSizeAsync(AssetKey key, CancellationToken token = default)
        {
            ThrowIfDisposed();
            DownloadTarget downloadTarget = DownloadTarget.ForKeys(new[] { RequireValid(key, nameof(key)).RuntimeKey });
            return _downloads.GetSizeAsync(downloadTarget, token);
        }

        /// <summary>
        /// Measures how many bytes the bundles behind the keys still need to download. Shared bundles count once.
        /// </summary>
        /// <param name="keys">Any asset, label or scene keys.</param>
        /// <param name="token">Cancels the query.</param>
        /// <returns>The number of bytes; 0 when everything is cached or local.</returns>
        public Task<long> GetDownloadSizeAsync(IReadOnlyList<AssetKey> keys, CancellationToken token = default)
        {
            ThrowIfDisposed();
            var downloadTarget = DownloadTarget.ForKeys(RuntimeKeys(keys));
            return _downloads.GetSizeAsync(downloadTarget, token);
        }

        /// <summary>
        /// Measures how many bytes all content of the loaded catalogs still needs to download.
        /// </summary>
        /// <param name="token">Cancels the query.</param>
        /// <returns>The number of bytes; 0 when everything is cached or local.</returns>
        public Task<long> GetRemoteContentSizeAsync(CancellationToken token = default)
        {
            ThrowIfDisposed();
            return _downloads.GetSizeAsync(DownloadTarget.RemoteContent, token);
        }

        /// <summary>
        /// Downloads the bundles behind the keys without loading them.
        /// </summary>
        /// <param name="keys">Any asset, label or scene keys.</param>
        /// <param name="options">Retries and whether catalogs are updated first.</param>
        /// <param name="token">Cancels the download.</param>
        /// <returns>The running download.</returns>
        public IAssetDownloadResponse DownloadAssets(IReadOnlyList<AssetKey> keys, DownloadOptions options, CancellationToken token = default)
        {
            ThrowIfDisposed();
            var downloadTarget = DownloadTarget.ForKeys(RuntimeKeys(keys));
            return _downloads.Start(downloadTarget, options, token);
        }

        /// <summary>
        /// Downloads the bundles of all content of the loaded catalogs without loading them.
        /// </summary>
        /// <param name="options">Retries and whether catalogs are updated first.</param>
        /// <param name="token">Cancels the download.</param>
        /// <returns>The running download.</returns>
        public IAssetDownloadResponse DownloadRemoteContent(DownloadOptions options, CancellationToken token = default)
        {
            ThrowIfDisposed();
            return _downloads.Start(DownloadTarget.RemoteContent, options, token);
        }

        /// <summary>
        /// Holds back downloads that have not started an attempt yet until the returned scope is disposed.
        /// Running attempts and loads are not affected.
        /// </summary>
        /// <returns>The suspension scope; dispose it to resume.</returns>
        public IDisposable SuspendDownloads()
        {
            ThrowIfDisposed();
            return _downloadSuspension.Suspend();
        }

        /// <summary>
        /// Loads an asset.
        /// </summary>
        /// <param name="key">The asset key.</param>
        /// <param name="lifetime">Releases the handler when cancelled.</param>
        /// <returns>The handler of the load.</returns>
        public IAssetHandler<TAsset> LoadAsset<TAsset>(AssetKeyT<TAsset> key, CancellationToken lifetime = default)
            where TAsset : UnityEngine.Object =>
            throw new NotImplementedException();

        /// <summary>
        /// Loads every asset of the label that loads as TAsset.
        /// </summary>
        /// <param name="key">The label key.</param>
        /// <param name="lifetime">Releases the handler when cancelled.</param>
        /// <returns>The handler of the batch.</returns>
        public IAssetHandler<IReadOnlyList<TAsset>> LoadAssets<TAsset>(LabelKey key,
            CancellationToken lifetime = default)
            where TAsset : UnityEngine.Object =>
            throw new NotImplementedException();

        /// <summary>
        /// Loads an addressable scene.
        /// </summary>
        /// <param name="key">The scene key.</param>
        /// <param name="options">Load mode, deferred activation and priority.</param>
        /// <param name="lifetime">Unloads the scene when cancelled.</param>
        /// <returns>The handler of the scene.</returns>
        public ISceneHandler LoadScene(SceneKey key, SceneLoadOptions options, CancellationToken lifetime = default) =>
            throw new NotImplementedException();

        /// <summary>
        /// Releases every handler loaded with the key and unloads the content.
        /// </summary>
        /// <param name="key">The key to unload.</param>
        public void UnloadAsset(AssetKey key) => throw new NotImplementedException();

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