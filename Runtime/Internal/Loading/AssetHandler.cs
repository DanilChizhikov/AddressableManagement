using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DTech.AddressableManagement.Internal.Delivery;
using DTech.AddressableManagement.Internal.Diagnostics;
using DTech.AddressableManagement.Internal.Threading;
using Object = UnityEngine.Object;

namespace DTech.AddressableManagement.Internal.Loading
{
    internal sealed class AssetHandler<TAsset> : IAssetHandler<TAsset>, IAssetLease, ITrackedHandler where TAsset : Object
    {
        private readonly TaskCompletionSource<bool> _completion = new (TaskCreationOptions.RunContinuationsAsynchronously);

        private readonly ContentContext _context;
        private readonly HandlerTracker _tracker;

        public AssetKey Key { get; }
        public string RuntimeKey => Key.RuntimeKey;
        public string CreationTrace { get; }
        public float Progress => IsDone ? 1f : _entry?.Progress ?? 0f;
        public bool IsDone { get; private set; }
        public bool IsFailed { get; private set; }
        public bool IsReleased { get; private set; }
        public Exception Exception { get; private set; }

        public TAsset Result
        {
            get
            {
                ThrowIfReleased();
                return _result;
            }
        }

        private AssetEntry _entry;
        private TAsset _result;
        private string _releaseTrace;
        private List<CancellationTokenRegistration> _registrations;

        public AssetHandler(AssetKey key, ContentContext context, HandlerTracker tracker)
        {
            Key = key;
            _context = context;
            _tracker = tracker;
            CreationTrace = HandlerTrace.Capture();
            _tracker?.Track(this);
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

        public async Task<TAsset> WaitForCompletionAsync(CancellationToken token = default)
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

            MarkReleased();
            AssetEntry entry = _entry;
            _entry = null;
            entry?.Release(this);
        }

        public void Attach(AssetEntry entry)
        {
            if (IsReleased || IsDone)
            {
                return;
            }

            _entry = entry;
            entry.Retain(this);
        }

        public void Fail(FailureKind kind, Exception exception)
        {
            if (IsReleased || IsDone)
            {
                return;
            }

            AssetEntry entry = _entry;
            _entry = null;
            entry?.Release(this);
            IsDone = true;
            IsFailed = true;
            Exception = exception;
            _completion.TrySetResult(true);
        }

        public void OnEntryCompleted(AssetEntry entry)
        {
            if (IsReleased || IsDone)
            {
                return;
            }

            if (entry.Failure != null)
            {
                _entry = null;
                Fail(entry.FailureKind, entry.Failure);
                return;
            }

            if (!AssetConversion.TryConvert(entry.Asset, out TAsset result))
            {
                string loaded = entry.Asset == null ? "nothing" : $"a {entry.Asset.GetType().Name}";
                var mismatch = new ContentDeliveryException(FailureKind.TypeMismatch,
                        $"'{Key}' loaded {loaded}, not a {typeof(TAsset).Name}.");
                _context.Report(new ContentFailure(ContentStage.Load, FailureKind.TypeMismatch, Key.RuntimeKey, mismatch));
                Fail(FailureKind.TypeMismatch, mismatch);
                return;
            }

            _result = result;
            IsDone = true;
            _completion.TrySetResult(true);
        }

        public void OnEntryReleased()
        {
            if (IsReleased)
            {
                return;
            }

            _entry = null;
            MarkReleased();
        }

        private static void ReleaseFromToken(object state)
        {
            ((AssetHandler<TAsset>)state).Release();
        }

        private void MarkReleased()
        {
            IsReleased = true;
            _releaseTrace = HandlerTrace.Capture();
            _result = null;
            _tracker?.Untrack(this);
            if (_registrations != null)
            {
                foreach (CancellationTokenRegistration registration in _registrations)
                {
                    registration.Dispose();
                }

                _registrations = null;
            }

            _completion.TrySetCanceled();
        }

        private void ThrowIfReleased()
        {
            if (IsReleased)
            {
                throw new ObjectDisposedException(nameof(IAssetHandler),
                        $"The handler of '{Key}' was released.{HandlerTrace.Describe(CreationTrace, _releaseTrace)}");
            }
        }
    }
}
