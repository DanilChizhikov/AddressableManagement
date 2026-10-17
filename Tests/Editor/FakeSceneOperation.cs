using System;
using System.Threading;
using System.Threading.Tasks;
using DTech.AddressableManagement.Internal.Scenes;
using UnityEngine.SceneManagement;

namespace DTech.AddressableManagement.Tests.Editor
{
	internal sealed class FakeSceneOperation : SceneOperation
	{
		private readonly TaskCompletionSource<bool> _completion = new (TaskCreationOptions.RunContinuationsAsynchronously);

		public override Task Completion => _completion.Task;
		public override float Progress => _progress;
		public override Scene Scene => default;
		public override bool IsActivated => _isActivated;
		public int ActivateCalls { get; private set; }
		public int UnloadCalls { get; private set; }
		
		private float _progress;
		private bool _isActivated;

		public FakeSceneOperation(bool activateOnLoad)
		{
			_isActivated = activateOnLoad;
		}

		public void SetProgress(float progress) => _progress = progress;

		public void Succeed()
		{
			_completion.TrySetResult(true);
		}

		public void Fail(Exception exception)
		{
			_completion.TrySetException(exception);
		}

		public override Task ActivateAsync(CancellationToken token)
		{
			ActivateCalls++;
			_isActivated = true;
			return Task.CompletedTask;
		}

		public override Task UnloadAsync()
		{
			UnloadCalls++;
			return Task.CompletedTask;
		}
	}
}