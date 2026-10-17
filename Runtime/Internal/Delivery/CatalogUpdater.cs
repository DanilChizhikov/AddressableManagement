using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DTech.AddressableManagement.Internal.Threading;

namespace DTech.AddressableManagement.Internal.Delivery
{
    internal sealed class CatalogUpdater
    {
        public event Action OnCatalogsChanged;

        private readonly ContentContext _context;
        private readonly ContentBootstrapper _bootstrapper;
        private readonly bool _cleanBundleCache;
        private readonly SingleFlight<CatalogState> _update = new SingleFlight<CatalogState>();

        public CatalogState State { get; private set; }

        public CatalogUpdater(ContentContext context, ContentBootstrapper bootstrapper, bool cleanBundleCache)
        {
            _context = context;
            _bootstrapper = bootstrapper;
            _cleanBundleCache = cleanBundleCache;
        }

        public Task<CatalogState> UpdateAsync(CancellationToken token)
        {
            return _update.RunAsync(RunAsync, token);
        }

        public Task WaitIdleAsync(CancellationToken token)
        {
            return _update.WaitIdleAsync(token);
        }

        private async Task<CatalogState> RunAsync()
        {
            CancellationToken lifetime = _context.Lifetime;
            State = CatalogState.Updating;
            try
            {
                await _bootstrapper.EnsureInitializedAsync(lifetime);
                State = await UpdateAlongRouteAsync(lifetime);
            }
            catch (Exception)
            {
                State = CatalogState.Failed;
                throw;
            }

            return State;
        }

        private async Task<CatalogState> UpdateAlongRouteAsync(CancellationToken token)
        {
            if (!_context.Gateway.IsInternetReachable)
            {
                _context.Report(new ContentFailure(ContentStage.CatalogUpdate, FailureKind.Network, string.Empty,
                    new ContentDeliveryException(FailureKind.Network, "The internet is not reachable.")));

                return CatalogState.Failed;
            }

            while (true)
            {
                int generation = _context.Route.Generation;
                try
                {
                    IReadOnlyList<string> catalogIds = await _context.Gateway.CheckForCatalogUpdatesAsync(token);
                    if (catalogIds.Count == 0)
                    {
                        _context.Log.Info("Catalogs are up to date.");
                        return CatalogState.UpToDate;
                    }

                    await _context.Gateway.UpdateCatalogsAsync(catalogIds, token);
                    _context.Log.Info($"Updated {catalogIds.Count} catalog(s).");
                    OnCatalogsChanged?.Invoke();
                    await CleanBundleCacheAsync(token);
                    return CatalogState.Updated;
                }
                catch (Exception exception) when (!(exception is OperationCanceledException))
                {
                    FailureKind kind = FailureClassifier.Classify(exception);
                    if (_context.Gateway.IsInternetReachable &&
                        _context.Route.TryAdvance(kind, generation))
                    {
                        await _context.Gateway.NextFrameAsync(token);
                        continue;
                    }

                    _context.Report(new ContentFailure(ContentStage.CatalogUpdate, kind, string.Empty,
                        exception));

                    return CatalogState.Failed;
                }
            }
        }

        private async Task CleanBundleCacheAsync(CancellationToken token)
        {
            if (!_cleanBundleCache ||
                _context.Gateway.IsEditor)
            {
                return;
            }

            try
            {
                await _context.Gateway.CleanBundleCacheAsync(token);
                _context.Log.Info("Cleaned bundles no loaded catalog references.");
            }
            catch (Exception exception) when (!(exception is OperationCanceledException))
            {
                _context.Log.Warning($"Could not clean the bundle cache: {exception.Message}");
            }
        }
    }
}