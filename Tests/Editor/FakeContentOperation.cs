using System;
using System.Threading.Tasks;
using DTech.AddressableManagement.Internal.Gateway;

namespace DTech.AddressableManagement.Tests.Editor
{
	internal sealed class FakeContentOperation<TResult> : ContentOperation<TResult>
	{
		private readonly TaskCompletionSource<TResult> _completion =
			new TaskCompletionSource<TResult>(TaskCreationOptions.RunContinuationsAsynchronously);

		public override Task<TResult> Completion => _completion.Task;
		public override float Progress => _progress;
		public override long DownloadedBytes => _downloadedBytes;
		public override long TotalBytes => _totalBytes;
		public int ReleaseCalls { get; private set; }
		
		private float _progress;
		private long _downloadedBytes;
		private long _totalBytes;

		public FakeContentOperation<TResult> SetProgress(float progress)
		{
			_progress = progress;
			return this;
		}

		public FakeContentOperation<TResult> SetDownloadedBytes(long downloadedBytes)
		{
			_downloadedBytes = downloadedBytes;
			return this;
		}

		public FakeContentOperation<TResult> SetTotalBytes(long totalBytes)
		{
			_totalBytes = totalBytes;
			return this;
		}

		public static FakeContentOperation<TResult> Succeeded(TResult result)
		{
			var operation = new FakeContentOperation<TResult>();
			operation.Succeed(result);
			return operation;
		}

		public static FakeContentOperation<TResult> Failed(Exception exception)
		{
			var operation = new FakeContentOperation<TResult>();
			operation.Fail(exception);
			return operation;
		}

		public void Succeed(TResult result)
		{
			_progress = 1f;
			_completion.TrySetResult(result);
		}

		public void Fail(Exception exception)
		{
			_completion.TrySetException(exception);
		}

		public override void Release()
		{
			ReleaseCalls++;
		}
	}
}