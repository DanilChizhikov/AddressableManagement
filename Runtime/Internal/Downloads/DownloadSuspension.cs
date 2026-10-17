using System;
using System.Threading;
using System.Threading.Tasks;
using DTech.AddressableManagement.Internal.Threading;

namespace DTech.AddressableManagement.Internal.Downloads
{
	internal sealed class DownloadSuspension
	{
		public bool IsSuspended => _count > 0;

		private int _count;
		private TaskCompletionSource<bool> _resumed;

		public IDisposable Suspend()
		{
			_count++;
			return new Scope(this);
		}

		public async Task WaitResumedAsync(CancellationToken token)
		{
			while (IsSuspended)
			{
				_resumed ??= new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
				await TaskCancellation.WaitAsync(_resumed.Task, token);
			}
		}

		private void Resume()
		{
			_count--;
			if (_count > 0 ||
				_resumed == null)
			{
				return;
			}

			TaskCompletionSource<bool> resumed = _resumed;
			_resumed = null;
			resumed.TrySetResult(true);
		}

		private sealed class Scope : IDisposable
		{
			private DownloadSuspension _owner;

			public Scope(DownloadSuspension owner)
			{
				_owner = owner;
			}

			public void Dispose()
			{
				DownloadSuspension owner = _owner;
				_owner = null;
				owner?.Resume();
			}
		}
	}
}