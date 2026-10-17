using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DTech.AddressableManagement.Internal.Downloads;
using DTech.AddressableManagement.Internal.Gateway;
using UnityEngine.ResourceManagement.ResourceLocations;

namespace DTech.AddressableManagement.Tests.Editor
{
	internal sealed class FakeAddressablesGateway : AddressablesGatewayBase
	{
		public override bool IsInternetReachable => _isInternetReachable;
		public override bool IsEditor => _isEditor;
		public bool VersionChanged { get; set; }
		public Task InitializeBarrier { get; set; } = Task.CompletedTask;
		public Func<int, Exception> InitializeFailure { get; set; } = _ => null;
		public Func<string, bool> ProbeResult { get; set; } = _ => true;
		public Task CatalogCheckBarrier { get; set; } = Task.CompletedTask;
		public Func<int, IReadOnlyList<string>> CatalogCheck { get; set; } = _ => Array.Empty<string>();
		public Func<string, string> InstalledRewriter { get; private set; }
		public Func<ContentScope, long> DownloadSize { get; set; } = _ => 0L;
		public Func<ContentScope, ContentOperation<bool>> DownloadOperation { get; set; } =
			_ => FakeContentOperation<bool>.Succeeded(true);
		public IList<IResourceLocation> RemoteContent { get; set; } = new List<IResourceLocation>();
		public Func<string, Type, IList<IResourceLocation>> Locate { get; set; } = LocateByKey;
		public Func<IResourceLocation, object> LoadResult { get; set; } = _ => null;
		public bool CompleteLoadsImmediately { get; set; } = true;
		public List<IResourceLocation> LoadedLocations { get; } = new List<IResourceLocation>();
		public List<IFakeOperation> LoadOperations { get; } = new List<IFakeOperation>();
		public List<Type> LocatedTypes { get; } = new List<Type>();
		public List<string> ProbedUrls { get; } = new List<string>();
		public List<ContentScope> DownloadedScopes { get; } = new List<ContentScope>();
		public List<string> UpdatedCatalogIds { get; } = new List<string>();
		public int InitializeCalls { get; private set; }
		public int CatalogCheckCalls { get; private set; }
		public int CleanBundleCacheCalls { get; private set; }
		public int VersionResetCalls { get; private set; }
		public int DownloadSizeCalls { get; private set; }
		public int RemoteContentCollections { get; private set; }
		public int LocateCalls { get; private set; }

		private bool _isInternetReachable = true;
		private bool _isEditor;

		public void SetIsInternetReachable(bool isInternetReachable) => _isInternetReachable = isInternetReachable;
		public void SetIsEditor(bool isEditor) => _isEditor = isEditor;
        
		public override async Task InitializeAsync(CancellationToken token)
		{
			int call = InitializeCalls++;
			await InitializeBarrier;
			Exception failure = InitializeFailure(call);
			if (failure != null)
			{
				throw failure;
			}
		}

		public override Task WaitForCacheReadyAsync(CancellationToken token)
		{
			return Task.CompletedTask;
		}

		public override bool ResetCatalogCacheIfVersionChanged()
		{
			VersionResetCalls++;
			return VersionChanged;
		}

		public override IDisposable InstallRequestRewriter(Func<string, string> rewrite)
		{
			InstalledRewriter = rewrite;
			return new Uninstaller(this);
		}

		public override Task<bool> ProbeAsync(string url, TimeSpan timeout, CancellationToken token)
		{
			ProbedUrls.Add(url);
			return Task.FromResult(ProbeResult(url));
		}

		public override async Task<IReadOnlyList<string>> CheckForCatalogUpdatesAsync(CancellationToken token)
		{
			int call = CatalogCheckCalls++;
			await CatalogCheckBarrier;
			return CatalogCheck(call);
		}

		public override Task UpdateCatalogsAsync(IReadOnlyList<string> catalogIds, CancellationToken token)
		{
			UpdatedCatalogIds.AddRange(catalogIds);
			return Task.CompletedTask;
		}

		public override Task CleanBundleCacheAsync(CancellationToken token)
		{
			CleanBundleCacheCalls++;
			return Task.CompletedTask;
		}
		
		public override Task<long> GetDownloadSizeAsync(ContentScope scope, CancellationToken token)
		{
			DownloadSizeCalls++;
			try
			{
				return Task.FromResult(DownloadSize(scope));
			}
			catch (Exception exception)
			{
				return Task.FromException<long>(exception);
			}
		}

		public override ContentOperation<bool> Download(ContentScope scope)
		{
			DownloadedScopes.Add(scope);
			return DownloadOperation(scope);
		}

		public override IList<IResourceLocation> CollectRemoteContentLocations()
		{
			RemoteContentCollections++;
			return RemoteContent;
		}

		public override Task<IList<IResourceLocation>> LocateAsync(string runtimeKey, Type type, CancellationToken token)
		{
			LocateCalls++;
			LocatedTypes.Add(type);
			return Task.FromResult(Locate(runtimeKey, type));
		}

		public override ContentOperation<TAsset> LoadAsset<TAsset>(IResourceLocation location)
		{
			LoadedLocations.Add(location);
			var operation = new FakeContentOperation<TAsset>();
			LoadOperations.Add(operation);
			if (CompleteLoadsImmediately)
			{
				operation.Settle(LoadResult(location));
			}

			return operation;
		}

		public override async Task NextFrameAsync(CancellationToken token)
		{
			token.ThrowIfCancellationRequested();
			await Task.Yield();
		}

		public override async Task DelayAsync(TimeSpan delay, CancellationToken token)
		{
			token.ThrowIfCancellationRequested();
			await Task.Yield();
		}
		
		private static IList<IResourceLocation> LocateByKey(string runtimeKey, Type type)
		{
			return new List<IResourceLocation>
			{
				new ResourceLocationBase(runtimeKey, "Assets/" + runtimeKey, "FakeProvider", type),
			};
		}

		private sealed class Uninstaller : IDisposable
		{
			private readonly FakeAddressablesGateway _gateway;

			public Uninstaller(FakeAddressablesGateway gateway)
			{
				_gateway = gateway;
			}

			public void Dispose()
			{
				_gateway.InstalledRewriter = null;
			}
		}
	}
}