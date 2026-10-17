using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DTech.AddressableManagement.Internal.Delivery;
using DTech.AddressableManagement.Internal.Gateway;
using DTech.AddressableManagement.Internal.Threading;
using UnityEngine.ResourceManagement.ResourceLocations;
using Object = UnityEngine.Object;

namespace DTech.AddressableManagement.Internal.Loading
{
    internal abstract class AssetEntry
    {
        private readonly List<IAssetLease> _leases = new List<IAssetLease>();
        private readonly List<AssetAlias> _aliases = new List<AssetAlias>();
        private readonly Action<AssetEntry> _onDisposed;

        public AssetIdentity Identity { get; }
        public IReadOnlyList<AssetAlias> Aliases => _aliases;
        public int LeaseCount => _leases.Count;
        public bool IsStarted { get; private set; }
        public bool IsCompleted { get; private set; }
        public Object Asset { get; private set; }
        public FailureKind FailureKind { get; private set; }
        public Exception Failure { get; private set; }
        public abstract float Progress { get; }

        private bool _isDisposed;

        protected AssetEntry(AssetIdentity identity, Action<AssetEntry> onDisposed)
        {
            Identity = identity;
            _onDisposed = onDisposed;
        }

        public void AddAlias(AssetAlias alias)
        {
            if (!_aliases.Contains(alias))
            {
                _aliases.Add(alias);
            }
        }

        public void Retain(IAssetLease lease)
        {
            if (_isDisposed)
            {
                if (Failure != null)
                {
                    lease.OnEntryCompleted(this);
                    return;
                }

                lease.OnEntryReleased();
                return;
            }

            _leases.Add(lease);
            if (IsCompleted)
            {
                lease.OnEntryCompleted(this);
                return;
            }

            if (IsStarted)
            {
                return;
            }

            IsStarted = true;
            StartLoad();
        }

        public void Release(IAssetLease lease)
        {
            if (!_leases.Remove(lease) ||
                _leases.Count > 0)
            {
                return;
            }

            Dispose();
        }

        public void ForceRelease()
        {
            IAssetLease[] leases = _leases.ToArray();
            _leases.Clear();
            foreach (IAssetLease lease in leases)
            {
                lease.OnEntryReleased();
            }

            Dispose();
        }

        public string Describe()
        {
            return _aliases.Count > 0 ? _aliases[0].RuntimeKey : Identity.InternalId;
        }

        protected abstract void StartLoad();
        protected abstract void ReleaseOperation();

        protected void Complete(Object asset)
        {
            if (_isDisposed)
            {
                return;
            }

            IsCompleted = true;
            Asset = asset;
            foreach (IAssetLease lease in _leases.ToArray())
            {
                lease.OnEntryCompleted(this);
            }
        }

        protected void Fail(FailureKind kind, Exception exception)
        {
            if (_isDisposed)
            {
                return;
            }

            IsCompleted = true;
            FailureKind = kind;
            Failure = exception;
            IAssetLease[] leases = _leases.ToArray();
            _leases.Clear();
            Dispose();
            foreach (IAssetLease lease in leases)
            {
                lease.OnEntryCompleted(this);
            }
        }

        private void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;
            _onDisposed(this);
            ReleaseOperation();
        }
    }

    internal sealed class AssetEntry<TLoad> : AssetEntry
        where TLoad : Object
    {
        private readonly ContentContext _context;
        private readonly IResourceLocation _location;
        private readonly LoadPolicy _policy;
        private readonly CancellationTokenSource _cancellation;

        public override float Progress => IsCompleted ? 1f : _operation?.Progress ?? 0f;

        private ContentOperation<TLoad> _operation;

        public AssetEntry(AssetIdentity identity, IResourceLocation location, ContentContext context,
            LoadPolicy policy,
            Action<AssetEntry> onDisposed) : base(identity, onDisposed)
        {
            _location = location;
            _context = context;
            _policy = policy;
            _cancellation = CancellationTokenSource.CreateLinkedTokenSource(context.Lifetime);
        }

        protected override void StartLoad()
        {
            _ = LoadAsync();
        }

        protected override void ReleaseOperation()
        {
            _cancellation.Cancel();
            _cancellation.Dispose();
            _operation?.Release();
            _operation = null;
        }

        private async Task LoadAsync()
        {
            CancellationToken token = _cancellation.Token;
            int attempt = 1;
            try
            {
                while (true)
                {
                    int generation = _context.Route.Generation;
                    ContentOperation<TLoad> operation = _context.Gateway.LoadAsset<TLoad>(_location);
                    _operation = operation;
                    try
                    {
                        TLoad asset = await TaskCancellation.WaitAsync(operation.Completion, token);
                        if (_policy.RebindShadersInEditor)
                        {
                            EditorShaderRebinder.Rebind(asset);
                        }

                        Complete(asset);
                        return;
                    }
                    catch (Exception exception) when (!token.IsCancellationRequested)
                    {
                        operation.Release();
                        _operation = null;
                        FailureKind kind = FailureClassifier.Classify(exception);
                        if (_context.Gateway.IsInternetReachable &&
                            _context.Route.TryAdvance(kind, generation))
                        {
                            await _context.Gateway.NextFrameAsync(token);
                            continue;
                        }

                        if (FailureClassifier.IsTransient(kind) &&
                            _policy.Retry.AllowsAttempt(attempt + 1))
                        {
                            attempt++;
                            _context.Log.Warning($"Load of '{Describe()}' failed ({kind}); attempt {attempt} follows.");
                            await _context.Gateway.DelayAsync(_policy.Retry.Delay, token);
                            continue;
                        }

                        _context.Report(new ContentFailure(ContentStage.Load, kind, Describe(),
                            exception));

                        Fail(kind, exception);
                        return;
                    }
                }
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
            }
        }
    }
}