using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DTech.AddressableManagement.Internal.Diagnostics;
using DTech.AddressableManagement.Internal.Downloads;
using DTech.AddressableManagement.Internal.Threading;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.AddressableAssets.ResourceLocators;
using UnityEngine.Networking;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceLocations;

namespace DTech.AddressableManagement.Internal.Gateway
{
    internal sealed class AddressablesGateway : AddressablesGatewayBase
	{
        private readonly AssetLog _log;

        public override bool IsInternetReachable => Application.internetReachability != NetworkReachability.NotReachable;
        public override bool IsEditor => Application.isEditor;

        public AddressablesGateway(AssetLog log)
        {
            _log = log;
        }
        
        public override Task InitializeAsync(CancellationToken token)
        {
            AsyncOperationHandle<IResourceLocator> handle = Addressables.InitializeAsync(false);
            return AwaitOwnedAsync(handle, token);
        }

        public override async Task WaitForCacheReadyAsync(CancellationToken token)
        {
            while (!Caching.ready)
            {
                await Awaitable.NextFrameAsync(token);
            }
        }

        public override bool ResetCatalogCacheIfVersionChanged()
        {
            return CatalogCacheVersionGuard.ResetIfVersionChanged();
        }

        public override IDisposable InstallRequestRewriter(Func<string, string> rewrite)
        {
            return RequestRewriterHook.Install(rewrite, _log);
        }

        public override async Task<bool> ProbeAsync(string url, TimeSpan timeout, CancellationToken token)
        {
            using UnityWebRequest request = UnityWebRequest.Get(url);
            request.timeout = Math.Max(1, (int)Math.Ceiling(timeout.TotalSeconds));
            UnityWebRequestAsyncOperation operation = request.SendWebRequest();
            while (!operation.isDone)
            {
                await Awaitable.NextFrameAsync(token);
            }

            return request.result == UnityWebRequest.Result.Success;
        }

        public override async Task<IReadOnlyList<string>> CheckForCatalogUpdatesAsync(CancellationToken token)
        {
            AsyncOperationHandle<List<string>> handle = Addressables.CheckForCatalogUpdates(false);
            List<string> catalogIds = await AwaitOwnedAsync(handle, token);
            return catalogIds ?? (IReadOnlyList<string>)Array.Empty<string>();
        }

        public override Task UpdateCatalogsAsync(IReadOnlyList<string> catalogIds, CancellationToken token)
        {
            AsyncOperationHandle<List<IResourceLocator>> handle = Addressables.UpdateCatalogs(catalogIds, false);
            return AwaitOwnedAsync(handle, token);
        }

        public override Task CleanBundleCacheAsync(CancellationToken token)
        {
            return AwaitOwnedAsync(Addressables.CleanBundleCache(), token);
        }

        public override Task<long> GetDownloadSizeAsync(ContentScope scope, CancellationToken token)
        {
            AsyncOperationHandle<long> handle = scope.Locations != null
                ? Addressables.GetDownloadSizeAsync((IEnumerable)new object[] { scope.Locations })
                : Addressables.GetDownloadSizeAsync((IEnumerable)scope.Keys);
            return AwaitOwnedAsync(handle, token);
        }

        public override ContentOperation<bool> Download(ContentScope scope)
        {
            AsyncOperationHandle handle = scope.Locations != null
                ? Addressables.DownloadDependenciesAsync(scope.Locations, false)
                : Addressables.DownloadDependenciesAsync((IEnumerable)scope.Keys, Addressables.MergeMode.Union, false);
            return new AddressablesOperation<bool>(handle, Succeeded);
        }

        public override IList<IResourceLocation> CollectRemoteContentLocations()
        {
            var seen = new HashSet<IResourceLocation>();
            var locations = new List<IResourceLocation>();
            foreach (IResourceLocator locator in Addressables.ResourceLocators)
            {
                foreach (IResourceLocation location in locator.AllLocations)
                {
                    if (location.HasDependencies && seen.Add(location))
                    {
                        locations.Add(location);
                    }
                }
            }

            return locations;
        }

        public override async Task NextFrameAsync(CancellationToken token)
        {
            await Awaitable.NextFrameAsync(token);
        }

        public override Task DelayAsync(TimeSpan delay, CancellationToken token)
        {
            return delay > TimeSpan.Zero ? Task.Delay(delay, token) : NextFrameAsync(token);
        }
        
        private static bool Succeeded(AsyncOperationHandle handle)
        {
            return true;
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