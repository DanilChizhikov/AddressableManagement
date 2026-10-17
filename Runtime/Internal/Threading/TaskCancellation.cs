using System;
using System.Threading;
using System.Threading.Tasks;

namespace DTech.AddressableManagement.Internal.Threading
{
	internal static class TaskCancellation
	{
		public static async Task WaitAsync(Task task, CancellationToken token)
		{
			if (!token.CanBeCanceled || task.IsCompleted)
			{
				await task;
				return;
			}

			token.ThrowIfCancellationRequested();
			var cancelled = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
			using (token.Register(SetCancelled, cancelled))
			{
				if (await Task.WhenAny(task, cancelled.Task) != task)
				{
					throw new OperationCanceledException(token);
				}
			}

			await task;
		}

		public static async Task<TResult> WaitAsync<TResult>(Task<TResult> task, CancellationToken token)
		{
			await WaitAsync((Task)task, token);
			return await task;
		}

		private static void SetCancelled(object state)
		{
			((TaskCompletionSource<bool>)state).TrySetResult(true);
		}
	}
}