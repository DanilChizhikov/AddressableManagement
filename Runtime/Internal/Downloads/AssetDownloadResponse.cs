using System;
using System.Threading;
using System.Threading.Tasks;
using DTech.AddressableManagement.Internal.Threading;
using UnityEngine;

namespace DTech.AddressableManagement.Internal.Downloads
{
    internal sealed class AssetDownloadResponse : IAssetDownloadResponse
    {
        public event Action<IAssetDownloadResponse> OnStateChanged;

        private readonly TaskCompletionSource<DownloadResult> _completion =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        private readonly CancellationTokenSource _cancellation;

        public DownloadState State { get; private set; }
        public long TotalBytes { get; private set; }
        public long DownloadedBytes { get; private set; }
        public int Attempt { get; private set; }
        public DownloadResult Result { get; private set; }
        public bool IsDone => State == DownloadState.Completed;
        public CancellationToken Token { get; }

        public float Progress
        {
            get
            {
                if (TotalBytes > 0L)
                {
                    return Mathf.Clamp01((float)((double)DownloadedBytes / TotalBytes));
                }

                return IsDone && Result.IsSuccess ? 1f : 0f;
            }
        }

        public AssetDownloadResponse(CancellationToken token, CancellationToken lifetime)
        {
            _cancellation = CancellationTokenSource.CreateLinkedTokenSource(token, lifetime);
            Token = _cancellation.Token;
        }

        public Task<DownloadResult> WaitForCompletionAsync(CancellationToken token = default)
        {
            return TaskCancellation.WaitAsync(_completion.Task, token);
        }

        public void Cancel()
        {
            if (!IsDone)
            {
                _cancellation.Cancel();
            }
        }

        public void SetState(DownloadState state)
        {
            if (IsDone || State == state)
            {
                return;
            }

            State = state;
            RaiseStateChanged();
        }

        public void SetAttempt(int attempt)
        {
            Attempt = attempt;
        }

        public void SetTotalBytes(long bytes)
        {
            TotalBytes = Math.Max(0L, bytes);
        }

        public void SetDownloadedBytes(long bytes)
        {
            DownloadedBytes = Math.Max(0L, Math.Min(bytes, TotalBytes));
        }

        public void Complete(DownloadResult result)
        {
            if (IsDone)
            {
                return;
            }

            Result = result;
            if (result.IsSuccess)
            {
                DownloadedBytes = TotalBytes;
            }

            State = DownloadState.Completed;
            _cancellation.Dispose();
            RaiseStateChanged();
            _completion.TrySetResult(result);
        }

        private void RaiseStateChanged()
        {
            try
            {
                OnStateChanged?.Invoke(this);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }
    }
}