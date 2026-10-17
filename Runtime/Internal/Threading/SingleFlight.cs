using System;
using System.Threading;
using System.Threading.Tasks;

namespace DTech.AddressableManagement.Internal.Threading
{
	internal sealed class SingleFlight<TResult>
	{
		public bool IsRunning => _running != null && !_running.IsCompleted;

		private Task<TResult> _running;

		public Task<TResult> RunAsync(Func<Task<TResult>> operation, CancellationToken token)
		{
			if (_running == null || _running.IsCompleted)
			{
				_running = operation();
			}

			return TaskCancellation.WaitAsync(_running, token);
		}

		public async Task WaitIdleAsync(CancellationToken token)
		{
			Task<TResult> running = _running;
			if (running == null || running.IsCompleted)
			{
				return;
			}

			try
			{
				await TaskCancellation.WaitAsync(running, token);
			}
			catch (Exception) when (!token.IsCancellationRequested)
			{
			}
		}
	}
}