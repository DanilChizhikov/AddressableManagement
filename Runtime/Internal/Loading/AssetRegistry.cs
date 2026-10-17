using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DTech.AddressableManagement.Internal.Delivery;
using UnityEngine;
using UnityEngine.ResourceManagement.ResourceLocations;
using Object = UnityEngine.Object;

namespace DTech.AddressableManagement.Internal.Loading
{
    internal sealed class AssetRegistry : IDisposable
    {
        private readonly ContentContext _context;
        private readonly ContentBootstrapper _bootstrapper;
        private readonly CatalogUpdater _catalogs;
        private readonly LoadPolicy _policy;
        private readonly Dictionary<AssetIdentity, AssetEntry> _entries = new Dictionary<AssetIdentity, AssetEntry>();
        private readonly Dictionary<AssetAlias, AssetEntry> _aliases = new Dictionary<AssetAlias, AssetEntry>();
        private readonly Dictionary<AssetAlias, Task<AssetEntry>> _resolving = new Dictionary<AssetAlias, Task<AssetEntry>>();

        public int EntryCount => _entries.Count;

        public AssetRegistry(ContentContext context, ContentBootstrapper bootstrapper, CatalogUpdater catalogs,
            LoadPolicy policy)
        {
            _context = context;
            _bootstrapper = bootstrapper;
            _catalogs = catalogs;
            _policy = policy;
        }

        public void Load<TAsset>(AssetHandler<TAsset> handler)
            where TAsset : Object
        {
            if (AssetConversion.LoadsAsGameObject(typeof(TAsset)))
            {
                _ = AcquireAsync<GameObject>(handler, handler.RuntimeKey);
                return;
            }

            _ = AcquireAsync<TAsset>(handler, handler.RuntimeKey);
        }

        public AssetEntry Acquire<TLoad>(IResourceLocation location)
            where TLoad : Object
        {
            var identity = new AssetIdentity(location.InternalId, typeof(TLoad));
            if (!_entries.TryGetValue(identity, out AssetEntry entry))
            {
                entry = new AssetEntry<TLoad>(identity, location, _context,
                    _policy, OnEntryDisposed);

                _entries.Add(identity, entry);
            }

            return entry;
        }

        public async Task<IList<IResourceLocation>> LocateAsync<TLoad>(string runtimeKey)
            where TLoad : Object
        {
            CancellationToken lifetime = _context.Lifetime;
            await _bootstrapper.EnsureInitializedAsync(lifetime);
            await _catalogs.WaitIdleAsync(lifetime);
            return await _context.Gateway.LocateAsync(runtimeKey, typeof(TLoad), lifetime);
        }

        public void Dispose()
        {
            var entries = new List<AssetEntry>(_entries.Values);
            foreach (AssetEntry entry in entries)
            {
                entry.ForceRelease();
            }

            _entries.Clear();
            _aliases.Clear();
        }

        private async Task AcquireAsync<TLoad>(IAssetLease lease, string runtimeKey)
            where TLoad : Object
        {
            AssetEntry entry;
            try
            {
                entry = await ResolveAsync<TLoad>(runtimeKey);
            }
            catch (OperationCanceledException) when (_context.Lifetime.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                lease.Fail(FailureClassifier.Classify(exception), exception);
                return;
            }

            if (lease.IsReleased)
            {
                ForgetIfUnused(entry);
                return;
            }

            lease.Attach(entry);
        }

        private Task<AssetEntry> ResolveAsync<TLoad>(string runtimeKey)
            where TLoad : Object
        {
            var alias = new AssetAlias(runtimeKey, typeof(TLoad));
            if (_aliases.TryGetValue(alias, out AssetEntry known))
            {
                return Task.FromResult(known);
            }

            if (_resolving.TryGetValue(alias, out Task<AssetEntry> resolving))
            {
                return resolving;
            }

            resolving = ResolveAliasAsync<TLoad>(alias);
            if (!resolving.IsCompleted)
            {
                _resolving[alias] = resolving;
            }

            return resolving;
        }

        private async Task<AssetEntry> ResolveAliasAsync<TLoad>(AssetAlias alias)
            where TLoad : Object
        {
            try
            {
                IList<IResourceLocation> locations = await LocateAsync<TLoad>(alias.RuntimeKey);
                if (locations.Count == 0)
                {
                    throw new ContentDeliveryException(FailureKind.NotFound,
                        $"Addressables has no {typeof(TLoad).Name} for the key '{alias.RuntimeKey}'.");
                }

                AssetEntry entry = Acquire<TLoad>(locations[0]);
                entry.AddAlias(alias);
                _aliases[alias] = entry;
                return entry;
            }
            catch (Exception exception) when (!(exception is OperationCanceledException))
            {
                _context.Report(new ContentFailure(ContentStage.Load, FailureClassifier.Classify(exception),
                    alias.RuntimeKey, exception));

                throw;
            }
            finally
            {
                _resolving.Remove(alias);
            }
        }

        private void ForgetIfUnused(AssetEntry entry)
        {
            if (entry.LeaseCount == 0 &&
                !entry.IsStarted)
            {
                OnEntryDisposed(entry);
            }
        }

        private void OnEntryDisposed(AssetEntry entry)
        {
            if (_entries.TryGetValue(entry.Identity, out AssetEntry current) &&
                current == entry)
            {
                _entries.Remove(entry.Identity);
            }

            foreach (AssetAlias alias in entry.Aliases)
            {
                if (_aliases.TryGetValue(alias, out current) &&
                    current == entry)
                {
                    _aliases.Remove(alias);
                }
            }
        }
    }
}