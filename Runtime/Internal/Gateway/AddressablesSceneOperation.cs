using System.Threading;
using System.Threading.Tasks;
using DTech.AddressableManagement.Internal.Delivery;
using DTech.AddressableManagement.Internal.Scenes;
using DTech.AddressableManagement.Internal.Threading;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.SceneManagement;

namespace DTech.AddressableManagement.Internal.Gateway
{
	internal sealed class AddressablesSceneOperation : SceneOperation
	{
        private readonly TaskCompletionSource<bool> _completion = new (TaskCreationOptions.RunContinuationsAsynchronously);

        private readonly AsyncOperationHandle<SceneInstance> _handle;

        public override Task Completion => _completion.Task;
        public override float Progress => _isUnloading || !_handle.IsValid() ? (_isLoaded ? 1f : 0f) : _handle.PercentComplete;
        public override Scene Scene => _isLoaded && IsActivated ? _instance.Scene : default;
        public override bool IsActivated => _isActivated;

        private SceneInstance _instance;
        private bool _isLoaded;
        private bool _isUnloading;
        private bool _isActivated;

        public AddressablesSceneOperation(AsyncOperationHandle<SceneInstance> handle, bool activateOnLoad)
        {
            _handle = handle;
            _isActivated = activateOnLoad;
            if (!handle.IsValid())
            {
                _completion.TrySetException(new ContentDeliveryException(FailureKind.Unknown,
                        "Addressables returned an invalid scene operation handle."));
                return;
            }

            _handle.Completed += OnCompleted;
        }

        public override async Task ActivateAsync(CancellationToken token)
        {
            if (IsActivated || !_isLoaded)
            {
                return;
            }

            await TaskCancellation.WaitAsync(AwaitAsync(_instance.ActivateAsync()), token);
            _isActivated = true;
        }

        public override async Task UnloadAsync()
        {
            if (_isUnloading)
            {
                return;
            }

            _isUnloading = true;
            if (!_handle.IsValid())
            {
                return;
            }

            if (!_handle.IsDone)
            {
                await _handle.Task;
            }

            if (!_handle.IsValid())
            {
                return;
            }

            if (_handle.Status != AsyncOperationStatus.Succeeded)
            {
                Addressables.Release(_handle);
                return;
            }

            if (!IsActivated)
            {
                AsyncOperation asyncOperation = _instance.ActivateAsync();
                await AwaitAsync(asyncOperation);
                _isActivated = true;
            }

            await Addressables.UnloadSceneAsync(_handle, true).Task;
        }

        private static Task AwaitAsync(AsyncOperation operation)
        {
            if (operation.isDone)
            {
                return Task.CompletedTask;
            }

            var done = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            operation.completed += _ => done.TrySetResult(true);
            return done.Task;
        }

        private void OnCompleted(AsyncOperationHandle<SceneInstance> handle)
        {
            if (handle.Status != AsyncOperationStatus.Succeeded)
            {
                _completion.TrySetException(handle.OperationException ??
                        new ContentDeliveryException(FailureKind.Unknown, $"Scene operation '{handle.DebugName}' failed."));
                return;
            }

            _instance = handle.Result;
            _isLoaded = true;
            _completion.TrySetResult(true);
        }
	}
}