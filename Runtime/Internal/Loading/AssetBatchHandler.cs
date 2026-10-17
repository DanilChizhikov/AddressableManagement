using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DTech.AddressableManagement.Internal.Delivery;
using DTech.AddressableManagement.Internal.Diagnostics;
using DTech.AddressableManagement.Internal.Threading;
using UnityEngine.ResourceManagement.ResourceLocations;
using Object = UnityEngine.Object;

namespace DTech.AddressableManagement.Internal.Loading
{
    internal sealed class AssetBatchHandler<TAsset> : IAssetHandler<IReadOnlyList<TAsset>>, ITrackedHandler
            where TAsset : Object
    {
        private readonly TaskCompletionSource<bool> _completion = new (TaskCreationOptions.RunContinuationsAsynchronously);

        private readonly List<AssetHandler<TAsset>> _children = new ();
        private readonly ContentContext _context;
        private readonly HandlerTracker _tracker;

        public AssetKey Key { get; }
        public string RuntimeKey => Key.RuntimeKey;
        public string CreationTrace { get; }
        public bool IsDone { get; private set; }
        public bool IsFailed { get; private set; }
        public bool IsReleased { get; private set; }
        public Exception Exception { get; private set; }

        public float Progress
        {
            get
            {
                if (IsDone)
                {
                    return 1f;
                }

                if (_children.Count == 0)
                {
                    return 0f;
                }

                float sum = 0f;
                foreach (AssetHandler<TAsset> child in _children)
                {
                    sum += child.Progress;
                }

                return sum / _children.Count;
            }
        }

        public IReadOnlyList<TAsset> Result
        {
            get
            {
                if (IsReleased)
                {
                    throw new ObjectDisposedException(nameof(IAssetHandler),
                            $"The handler of '{Key}' was released.{HandlerTrace.Describe(CreationTrace, _releaseTrace)}");
                }

                return _result;
            }
        }

        private IReadOnlyList<TAsset> _result;
        private string _releaseTrace;
        private List<CancellationTokenRegistration> _registrations;

        public AssetBatchHandler(LabelKey key, ContentContext context, HandlerTracker tracker)
        {
            Key = key;
            _context = context;
            _tracker = tracker;
            CreationTrace = HandlerTrace.Capture();
            _tracker.Track(this);
        }

        public void Start(AssetRegistry registry)
        {
            if (AssetConversion.LoadsAsGameObject(typeof(TAsset)))
            {
                _ = LoadAsync<UnityEngine.GameObject>(registry);
                return;
            }

            _ = LoadAsync<TAsset>(registry);
        }

        public void AttachCancellationToken(CancellationToken token)
        {
            if (!token.CanBeCanceled || IsReleased)
            {
                return;
            }

            if (token.IsCancellationRequested)
            {
                Release();
                return;
            }

            _registrations ??= new List<CancellationTokenRegistration>();
            _registrations.Add(token.Register(ReleaseFromToken, this, true));
        }

        public async Task<IReadOnlyList<TAsset>> WaitForCompletionAsync(CancellationToken token = default)
        {
            await TaskCancellation.WaitAsync(_completion.Task, token);
            return IsFailed || IsReleased ? null : _result;
        }

        Task IAssetHandler.WaitForCompletionAsync(CancellationToken token)
        {
            return WaitForCompletionAsync(token);
        }

        public void Release()
        {
            if (IsReleased)
            {
                return;
            }

            IsReleased = true;
            _releaseTrace = HandlerTrace.Capture();
            _result = null;
            _tracker.Untrack(this);
            DisposeRegistrations();
            ReleaseChildren();
            _completion.TrySetCanceled();
        }

        public void Fail(Exception exception)
        {
            if (IsReleased || IsDone)
            {
                return;
            }

            ReleaseChildren();
            _tracker.Untrack(this);
            IsDone = true;
            IsFailed = true;
            Exception = exception;
            _completion.TrySetResult(true);
        }

        private static void ReleaseFromToken(object state)
        {
            ((AssetBatchHandler<TAsset>)state).Release();
        }

        private async Task LoadAsync<TLoad>(AssetRegistry registry) where TLoad : Object
        {
            IList<IResourceLocation> locations;
            try
            {
                locations = await registry.LocateAsync<TLoad>(Key.RuntimeKey);
            }
            catch (OperationCanceledException) when (_context.Lifetime.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                FailureKind kind = FailureClassifier.Classify(exception);
                _context.Report(new ContentFailure(ContentStage.Load, kind, Key.RuntimeKey, exception));
                Fail(exception);
                return;
            }

            if (IsReleased)
            {
                return;
            }

            if (locations.Count == 0)
            {
                _context.Log.Warning($"Label '{Key}' has no assets that load as {typeof(TAsset).Name}.");
                Complete(Array.Empty<TAsset>());
                return;
            }

            foreach (IResourceLocation location in locations)
            {
                var child = new AssetHandler<TAsset>(Key, _context, null);
                _children.Add(child);
                child.Attach(registry.Acquire<TLoad>(location));
            }

            AssetHandler<TAsset>[] children = _children.ToArray();
            var results = new TAsset[children.Length];
            for (int i = 0; i < children.Length; i++)
            {
                AssetHandler<TAsset> child = children[i];
                try
                {
                    results[i] = await child.WaitForCompletionAsync();
                }
                catch (OperationCanceledException)
                {
                    return;
                }

                if (IsReleased)
                {
                    return;
                }

                if (child.IsFailed)
                {
                    Fail(child.Exception);
                    return;
                }
            }

            Complete(results);
        }

        private void Complete(IReadOnlyList<TAsset> result)
        {
            if (IsReleased || IsDone)
            {
                return;
            }

            _result = result;
            IsDone = true;
            _completion.TrySetResult(true);
        }

        private void ReleaseChildren()
        {
            foreach (AssetHandler<TAsset> child in _children)
            {
                child.Release();
            }

            _children.Clear();
        }

        private void DisposeRegistrations()
        {
            if (_registrations == null)
            {
                return;
            }

            foreach (CancellationTokenRegistration registration in _registrations)
            {
                registration.Dispose();
            }

            _registrations = null;
        }
    }
}
