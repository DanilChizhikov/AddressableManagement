using System;
using System.Threading;
using System.Threading.Tasks;
using DTech.AddressableManagement.Internal.Threading;

namespace DTech.AddressableManagement.Internal.Delivery
{
	internal sealed class ContentBootstrapper : IDisposable
	{
		private readonly ContentContext _context;
		private readonly bool _resetCatalogCacheOnVersionChange;
		private readonly SingleFlight<bool> _initialization = new SingleFlight<bool>();

		public bool IsInitialized { get; private set; }

		private IDisposable _requestRewriter;

		public ContentBootstrapper(ContentContext context, bool resetCatalogCacheOnVersionChange)
		{
			_context = context;
			_resetCatalogCacheOnVersionChange = resetCatalogCacheOnVersionChange;
		}

		public Task EnsureInitializedAsync(CancellationToken token)
		{
			return IsInitialized ? Task.CompletedTask : _initialization.RunAsync(InitializeAsync, token);
		}

		public void Dispose()
		{
			_requestRewriter?.Dispose();
			_requestRewriter = null;
		}

		private async Task<bool> InitializeAsync()
		{
			CancellationToken lifetime = _context.Lifetime;
			await _context.Gateway.WaitForCacheReadyAsync(lifetime);
			if (_resetCatalogCacheOnVersionChange)
			{
				ResetCatalogCache();
			}

			_requestRewriter ??= _context.Gateway.InstallRequestRewriter(_context.Route.Rewrite);
			await ProbeMirrorsAsync(lifetime);
			try
			{
				await _context.Gateway.InitializeAsync(lifetime);
			}
			catch (Exception exception) when (!(exception is OperationCanceledException))
			{
				_context.Report(new ContentFailure(ContentStage.Initialization, FailureClassifier.Classify(exception),
					string.Empty, exception));

				throw;
			}

			IsInitialized = true;
			_context.Log.Info("Addressables initialized.");
			return true;
		}

		private void ResetCatalogCache()
		{
			try
			{
				if (_context.Gateway.ResetCatalogCacheIfVersionChanged())
				{
					_context.Log.Info("Cached remote catalogs were deleted after the application version changed.");
				}
			}
			catch (Exception exception)
			{
				_context.Log.Warning($"Could not reset the catalog cache: {exception.Message}");
			}
		}

		private async Task ProbeMirrorsAsync(CancellationToken token)
		{
			ContentRoute route = _context.Route;
			if (route.ProbePath.Length == 0 ||
				route.MirrorCount < 2 ||
				!_context.Gateway.IsInternetReachable)
			{
				return;
			}

			for (int i = 0; i < route.MirrorCount; i++)
			{
				string url = route.ActiveMirror + "/" + route.ProbePath.TrimStart('/');
				if (await _context.Gateway.ProbeAsync(url, route.ProbeTimeout, token))
				{
					return;
				}

				_context.Log.Warning($"Mirror probe failed: {url}");
				if (!route.TryAdvance(FailureKind.Network, route.Generation))
				{
					break;
				}
			}

			route.Reset();
			var deliveryException = new ContentDeliveryException(FailureKind.Network, "No mirror answered the probe.");
			var contentFailure = new ContentFailure(ContentStage.Initialization, FailureKind.Network, route.ProbePath, deliveryException);
			_context.Report(contentFailure);
		}
	}
}