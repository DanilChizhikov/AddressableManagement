using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

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
		public abstract Task NextFrameAsync(CancellationToken token);
		public abstract Task DelayAsync(TimeSpan delay, CancellationToken token);
	}
}