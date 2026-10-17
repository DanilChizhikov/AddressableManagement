using System;
using System.Threading.Tasks;
using DTech.AddressableManagement.Internal.Delivery;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace DTech.AddressableManagement.Internal.Gateway
{
	internal sealed class AddressablesOperation<TResult> : ContentOperation<TResult>
	{
		private readonly TaskCompletionSource<TResult> _completion =
			new(TaskCreationOptions.RunContinuationsAsynchronously);

		private readonly AsyncOperationHandle _handle;
		private readonly Func<AsyncOperationHandle, TResult> _readResult;

		public override Task<TResult> Completion => _completion.Task;
		public override float Progress => IsHandleUsable ? _handle.PercentComplete : Completion.IsCompleted ? 1f : 0f;
		public override long DownloadedBytes => IsHandleUsable ? _handle.GetDownloadStatus().DownloadedBytes : 0L;
		public override long TotalBytes => IsHandleUsable ? _handle.GetDownloadStatus().TotalBytes : 0L;

		private bool _isReleased;

		private bool IsHandleUsable => !_isReleased && _handle.IsValid();

		public AddressablesOperation(AsyncOperationHandle<TResult> handle) : this(handle, ReadTypedResult)
		{
		}

		public AddressablesOperation(AsyncOperationHandle handle, Func<AsyncOperationHandle, TResult> readResult)
		{
			_handle = handle;
			_readResult = readResult;
			if (!handle.IsValid())
			{
				_completion.TrySetException(new ContentDeliveryException(FailureKind.Unknown,
					"Addressables returned an invalid operation handle."));
				return;
			}

			_handle.Completed += OnCompleted;
		}

		public override void Release()
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
		
		private static TResult ReadTypedResult(AsyncOperationHandle handle)
		{
			return (TResult)handle.Result;
		}

		private static void ReleaseWhenDone(AsyncOperationHandle handle)
		{
			Addressables.Release(handle);
		}

		private void OnCompleted(AsyncOperationHandle handle)
		{
			if (handle.Status != AsyncOperationStatus.Succeeded)
			{
				var deliveryException = new ContentDeliveryException(FailureKind.Unknown, $"Addressables operation '{handle.DebugName}' failed.");
				_completion.TrySetException(handle.OperationException ?? deliveryException);
				return;
			}

			try
			{
				_completion.TrySetResult(_readResult(handle));
			}
			catch (Exception exception)
			{
				_completion.TrySetException(exception);
			}
		}
	}
}