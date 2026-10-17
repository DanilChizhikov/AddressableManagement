using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DTech.AddressableManagement.Internal.Downloads;
using UnityEngine.ResourceManagement.ResourceLocations;
using Object = UnityEngine.Object;

namespace DTech.AddressableManagement.Internal.Gateway
{
	internal abstract class AddressablesGatewayBase
	{
		public abstract bool IsInternetReachable { get; }
		public abstract bool IsEditor { get; }

		public abstract Task InitializeAsync(CancellationToken token);
		public abstract Task WaitForCacheReadyAsync(CancellationToken token);
		public abstract bool ResetCatalogCacheIfVersionChanged();
		public abstract IDisposable InstallRequestRewriter(Func<string, string> rewrite);
		public abstract Task<bool> ProbeAsync(string url, TimeSpan timeout, CancellationToken token);
		public abstract Task<IReadOnlyList<string>> CheckForCatalogUpdatesAsync(CancellationToken token);
		public abstract Task UpdateCatalogsAsync(IReadOnlyList<string> catalogIds, CancellationToken token);
		public abstract Task CleanBundleCacheAsync(CancellationToken token);
		public abstract Task<long> GetDownloadSizeAsync(ContentScope scope, CancellationToken token);
		public abstract ContentOperation<bool> Download(ContentScope scope);
		public abstract IList<IResourceLocation> CollectRemoteContentLocations();
		public abstract Task<IList<IResourceLocation>> LocateAsync(string runtimeKey, Type type, CancellationToken token);
		public abstract ContentOperation<TAsset> LoadAsset<TAsset>(IResourceLocation location) where TAsset : Object;
		public abstract Task NextFrameAsync(CancellationToken token);
		public abstract Task DelayAsync(TimeSpan delay, CancellationToken token);
	}
}