using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DTech.AddressableManagement.Internal.Diagnostics;
using DTech.AddressableManagement.Internal.Threading;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.AddressableAssets.ResourceLocators;
using UnityEngine.Networking;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace DTech.AddressableManagement.Internal.Gateway
{
    internal sealed class AddressablesGateway
	{
        private readonly AssetLog _log;

        public bool IsInternetReachable => Application.internetReachability != NetworkReachability.NotReachable;
        public bool IsEditor => Application.isEditor;

        public AddressablesGateway(AssetLog log)
        {
            _log = log;
        }

        public async Task WaitForCacheReadyAsync(CancellationToken token)
        {
            while (!Caching.ready)
            {
                await Awaitable.NextFrameAsync(token);
            }
        }

        public bool ResetCatalogCacheIfVersionChanged()
        {
            return CatalogCacheVersionGuard.ResetIfVersionChanged();
        }

        public IDisposable InstallRequestRewriter(Func<string, string> rewrite)
        {
            return RequestRewriterHook.Install(rewrite, _log);
        }

        public async Task<bool> ProbeAsync(string url, TimeSpan timeout, CancellationToken token)
        {
            using (UnityWebRequest request = UnityWebRequest.Get(url))
            {
                request.timeout = Math.Max(1, (int)Math.Ceiling(timeout.TotalSeconds));
                UnityWebRequestAsyncOperation operation = request.SendWebRequest();
                while (!operation.isDone)
                {
                    await Awaitable.NextFrameAsync(token);
                }

                return request.result == UnityWebRequest.Result.Success;
            }
        }

        public Task InitializeAsync(CancellationToken token)
        {
            AsyncOperationHandle<IResourceLocator> handle = Addressables.InitializeAsync(false);
            return AwaitOwnedAsync(handle, token);
        }

        public async Task<IReadOnlyList<string>> CheckForCatalogUpdatesAsync(CancellationToken token)
        {
            List<string> catalogIds = await AwaitOwnedAsync(Addressables.CheckForCatalogUpdates(false), token);
            return catalogIds ?? (IReadOnlyList<string>)Array.Empty<string>();
        }

        public Task UpdateCatalogsAsync(IReadOnlyList<string> catalogIds, CancellationToken token)
        {
            return AwaitOwnedAsync(Addressables.UpdateCatalogs(catalogIds, false), token);
        }

        public Task CleanBundleCacheAsync(CancellationToken token)
        {
            return AwaitOwnedAsync(Addressables.CleanBundleCache(), token);
        }

        public async Task NextFrameAsync(CancellationToken token)
        {
            await Awaitable.NextFrameAsync(token);
        }

        public Task DelayAsync(TimeSpan delay, CancellationToken token)
        {
            return delay > TimeSpan.Zero ? Task.Delay(delay, token) : NextFrameAsync(token);
        }

        private static async Task<TResult> AwaitOwnedAsync<TResult>(AsyncOperationHandle<TResult> handle,
                CancellationToken token)
        {
            var operation = new AddressablesOperation<TResult>(handle);
            try
            {
                return await TaskCancellation.WaitAsync(operation.Completion, token);
            }
            finally
            {
                operation.Release();
            }
        }
	}
}