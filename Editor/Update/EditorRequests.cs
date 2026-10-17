using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.PackageManager.Requests;
using UnityEngine.Networking;

namespace DTech.AddressableManagement.Editor.Update
{
	internal static class EditorRequests
	{
		public static Task SendAsync(UnityWebRequest request, CancellationToken cancellationToken)
		{
			UnityWebRequestAsyncOperation operation = request.SendWebRequest();
			return Poll.Start(() => operation.isDone, request.Abort, cancellationToken);
		}

		public static Task WaitAsync(Request request, CancellationToken cancellationToken)
		{
			return Poll.Start(() => request.IsCompleted, null, cancellationToken);
		}

		private sealed class Poll
		{
			private readonly Func<bool> _isDone;
			private readonly Action _abort;
			private readonly CancellationToken _cancellationToken;
			private readonly TaskCompletionSource<bool> _completion = new();

			private Poll(Func<bool> isDone, Action abort, CancellationToken cancellationToken)
			{
				_isDone = isDone;
				_abort = abort;
				_cancellationToken = cancellationToken;
			}

			public static Task Start(Func<bool> isDone, Action abort, CancellationToken cancellationToken)
			{
				var poll = new Poll(isDone, abort, cancellationToken);
				EditorApplication.update += poll.Execute;
				return poll._completion.Task;
			}

			private void Execute()
			{
				if (_cancellationToken.IsCancellationRequested)
				{
					EditorApplication.update -= Execute;
					_abort?.Invoke();
					_completion.TrySetCanceled(_cancellationToken);
					return;
				}

				if (!_isDone())
				{
					return;
				}

				EditorApplication.update -= Execute;
				_completion.TrySetResult(true);
			}
		}
	}
}
