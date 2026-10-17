using System.Threading.Tasks;
using DTech.AddressableManagement.Internal.Delivery;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace DTech.AddressableManagement.Internal.Gateway
{
	internal sealed class AddressablesOperation<TResult>
	{
		private readonly TaskCompletionSource<TResult> _completion =
			new(TaskCreationOptions.RunContinuationsAsynchronously);

		private readonly AsyncOperationHandle<TResult> _handle;

		public Task<TResult> Completion => _completion.Task;
		public float Progress => IsHandleUsable ? _handle.PercentComplete : Completion.IsCompleted ? 1f : 0f;
		public long DownloadedBytes => IsHandleUsable ? _handle.GetDownloadStatus().DownloadedBytes : 0L;
		public long TotalBytes => IsHandleUsable ? _handle.GetDownloadStatus().TotalBytes : 0L;

		private bool _isReleased;

		private bool IsHandleUsable => !_isReleased && _handle.IsValid();

		public AddressablesOperation(AsyncOperationHandle<TResult> handle)
		{
			_handle = handle;
			if (!handle.IsValid())
			{
				_completion.TrySetException(new ContentDeliveryException(FailureKind.Unknown,
					"Addressables returned an invalid operation handle."));
				return;
			}

			_handle.Completed += OnCompleted;
		}

		public void Release()
		{
			if (_isReleased)
			{
				return;
			}

			_isReleased = true;
			if (!_handle.IsValid())
			{
				return;
			}

			if (_handle.IsDone)
			{
				Addressables.Release(_handle);
				return;
			}

			_handle.Completed += ReleaseWhenDone;
		}

		private static void ReleaseWhenDone(AsyncOperationHandle<TResult> handle)
		{
			Addressables.Release(handle);
		}

		private void OnCompleted(AsyncOperationHandle<TResult> handle)
		{
			if (handle.Status == AsyncOperationStatus.Succeeded)
			{
				_completion.TrySetResult(handle.Result);
				return;
			}

			_completion.TrySetException(handle.OperationException ??
				new ContentDeliveryException(FailureKind.Unknown, $"Addressables operation '{handle.DebugName}' failed."));
		}
	}
}