using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DTech.AddressableManagement.Internal.Delivery;
using DTech.AddressableManagement.Internal.Gateway;
using UnityEngine.ResourceManagement.ResourceLocations;

namespace DTech.AddressableManagement.Internal.Downloads
{
    internal sealed class DownloadCoordinator
    {
        private readonly ContentContext _context;
        private readonly ContentBootstrapper _bootstrapper;
        private readonly CatalogUpdater _catalogs;
        private readonly DownloadStateCache _cache;
        private readonly DownloadSuspension _suspension;

        public DownloadCoordinator(ContentContext context, ContentBootstrapper bootstrapper, CatalogUpdater catalogs,
            DownloadStateCache cache, DownloadSuspension suspension)
        {
            _context = context;
            _bootstrapper = bootstrapper;
            _catalogs = catalogs;
            _cache = cache;
            _suspension = suspension;
        }

        public async Task<long> GetSizeAsync(DownloadTarget target, CancellationToken token)
        {
            using (CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(token, _context.Lifetime))
            {
                await PrepareAsync(false, linked.Token);
                ContentScope scope = Resolve(target);
                if (scope.IsEmpty)
                {
                    return 0L;
                }

                long size = await _context.Gateway.GetDownloadSizeAsync(scope, linked.Token);
                if (size <= 0L)
                {
                    _cache.MarkCached(scope);
                    return 0L;
                }

                return size;
            }
        }

        public IAssetDownloadResponse Start(DownloadTarget target, DownloadOptions options, CancellationToken token)
        {
            var response = new AssetDownloadResponse(token, _context.Lifetime);
            _ = RunAsync(response, target, options);
            return response;
        }

        private async Task RunAsync(AssetDownloadResponse response, DownloadTarget target, DownloadOptions options)
        {
            CancellationToken token = response.Token;
            try
            {
                await PrepareAsync(options.UpdateCatalogsFirst, token);
                ContentScope scope = Resolve(target);
                long size = scope.IsEmpty ? 0L : await _context.Gateway.GetDownloadSizeAsync(scope, token);
                if (size <= 0L)
                {
                    _cache.MarkCached(scope);
                    response.Complete(new DownloadResult(DownloadOutcome.AlreadyCached, FailureKind.None, null));
                    return;
                }

                response.SetTotalBytes(size);
                await DownloadWithRetriesAsync(response, target, scope,
                    options.Retry);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                response.Complete(new DownloadResult(DownloadOutcome.Cancelled, FailureKind.None, null));
            }
            catch (Exception exception)
            {
                Fail(response, target, FailureClassifier.Classify(exception),
                    exception);
            }
        }

        private async Task DownloadWithRetriesAsync(AssetDownloadResponse response, DownloadTarget target,
            ContentScope scope, RetryPolicy retry)
        {
            CancellationToken token = response.Token;
            int attempt = 1;
            while (true)
            {
                response.SetAttempt(attempt);
                while (_suspension.IsSuspended)
                {
                    response.SetState(DownloadState.Suspended);
                    await _suspension.WaitResumedAsync(token);
                }

                int generation = _context.Route.Generation;
                (FailureKind kind, Exception error) = await AttemptAsync(response, scope);
                if (kind == FailureKind.None)
                {
                    _cache.MarkCached(scope);
                    response.Complete(new DownloadResult(DownloadOutcome.Downloaded, FailureKind.None, null));
                    return;
                }

                if (_context.Gateway.IsInternetReachable &&
                    _context.Route.TryAdvance(kind, generation))
                {
                    await _context.Gateway.NextFrameAsync(token);
                    continue;
                }

                if (!FailureClassifier.IsTransient(kind) ||
                    !retry.AllowsAttempt(attempt + 1))
                {
                    Fail(response, target, kind,
                        error);

                    return;
                }

                attempt++;
                response.SetState(DownloadState.Retrying);
                _context.Log.Warning(
                    $"Download of {target.Description} failed ({kind}); attempt {attempt} follows in {retry.Delay.TotalSeconds:0.##} s.");

                await _context.Gateway.DelayAsync(retry.Delay, token);
            }
        }

        private async Task<(FailureKind, Exception)> AttemptAsync(AssetDownloadResponse response, ContentScope scope)
        {
            CancellationToken token = response.Token;
            if (!_context.Gateway.IsInternetReachable)
            {
                return (FailureKind.Network, new ContentDeliveryException(FailureKind.Network, "The internet is not reachable."));
            }

            response.SetState(DownloadState.Downloading);
            ContentOperation<bool> operation = _context.Gateway.Download(scope);
            try
            {
                while (!operation.Completion.IsCompleted)
                {
                    response.SetDownloadedBytes(operation.DownloadedBytes);
                    await _context.Gateway.NextFrameAsync(token);
                }

                await operation.Completion;
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                return (FailureClassifier.Classify(exception), exception);
            }
            finally
            {
                operation.Release();
            }

            long missing = await _context.Gateway.GetDownloadSizeAsync(scope, token);
            if (missing > 0L)
            {
                return (FailureKind.Unknown, new ContentDeliveryException(FailureKind.Unknown,
                    $"{missing} bytes are still missing after the download finished."));
            }

            return (FailureKind.None, null);
        }

        private async Task PrepareAsync(bool updateCatalogs, CancellationToken token)
        {
            await _bootstrapper.EnsureInitializedAsync(token);
            if (updateCatalogs)
            {
                await _catalogs.UpdateAsync(token);
                return;
            }

            await _catalogs.WaitIdleAsync(token);
        }

        private ContentScope Resolve(DownloadTarget target)
        {
            if (target.IsRemoteContent)
            {
                if (_cache.IsRemoteContentCached)
                {
                    return ContentScope.Empty;
                }

                IList<IResourceLocation> locations = _cache.GetRemoteContent(_context.Gateway.CollectRemoteContentLocations);
                return locations.Count == 0 ? ContentScope.Empty : ContentScope.ForLocations(locations);
            }

            var pending = new List<string>(target.Keys.Count);
            foreach (string key in target.Keys)
            {
                if (!_cache.IsCached(key) &&
                    !pending.Contains(key))
                {
                    pending.Add(key);
                }
            }

            return pending.Count == 0 ? ContentScope.Empty : ContentScope.ForKeys(pending);
        }

        private void Fail(AssetDownloadResponse response, DownloadTarget target, FailureKind kind, Exception exception)
        {
            var contentFailure = new ContentFailure(ContentStage.Download, kind, target.Description, exception);
            var downloadResult = new DownloadResult(DownloadOutcome.Failed, kind, exception);
            
            _context.Report(contentFailure);
            response.Complete(downloadResult);
        }
    }
}