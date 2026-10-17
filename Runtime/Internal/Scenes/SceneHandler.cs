using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DTech.AddressableManagement.Internal.Delivery;
using DTech.AddressableManagement.Internal.Diagnostics;
using DTech.AddressableManagement.Internal.Loading;
using DTech.AddressableManagement.Internal.Threading;
using UnityEngine.SceneManagement;

namespace DTech.AddressableManagement.Internal.Scenes
{
    internal sealed class SceneHandler : ISceneHandler, ITrackedHandler
    {
        private readonly TaskCompletionSource<bool> _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);

        private readonly SceneLoadOptions _options;
        private readonly ContentContext _context;
        private readonly HandlerTracker _tracker;
        private readonly LoadPolicy _policy;
        private readonly CancellationTokenSource _cancellation;

        public AssetKey Key { get; }
        public string RuntimeKey => Key.RuntimeKey;
        public string CreationTrace { get; }
        public float Progress => IsDone ? 1f : _operation?.Progress ?? 0f;
        public bool IsDone { get; private set; }
        public bool IsFailed { get; private set; }
        public bool IsReleased { get; private set; }
        public bool IsActivated => _operation != null && _operation.IsActivated && IsDone && !IsFailed;
        public Exception Exception { get; private set; }

        public Scene Result
        {
            get
            {
                if (IsReleased)
                {
                    throw new ObjectDisposedException(nameof(ISceneHandler),
                        $"The scene handler of '{Key}' was released.{HandlerTrace.Describe(CreationTrace, _releaseTrace)}");
                }

                return IsDone && !IsFailed ? _operation.Scene : default;
            }
        }

        private SceneOperation _operation;
        private Task _unload;
        private string _releaseTrace;
        private List<CancellationTokenRegistration> _registrations;

        public SceneHandler(SceneKey key, SceneLoadOptions options, ContentContext context,
            HandlerTracker tracker,
            LoadPolicy policy)
        {
            Key = key;
            _options = options;
            _context = context;
            _tracker = tracker;
            _policy = policy;
            _cancellation = CancellationTokenSource.CreateLinkedTokenSource(context.Lifetime);
            CreationTrace = HandlerTrace.Capture();
            _tracker.Track(this);
        }

        public void Start(ContentBootstrapper bootstrapper, CatalogUpdater catalogs)
        {
            _ = LoadAsync(bootstrapper, catalogs);
        }

        public void AttachCancellationToken(CancellationToken token)
        {
            if (!token.CanBeCanceled || IsReleased)
            {
                return;
            }

            if (token.IsCancellationRequested)
            {
                Release();
                return;
            }

            _registrations ??= new List<CancellationTokenRegistration>();
            _registrations.Add(token.Register(ReleaseFromToken, this, true));
        }

        public async Task<Scene> WaitForCompletionAsync(CancellationToken token = default)
        {
            await TaskCancellation.WaitAsync(_completion.Task, token);
            return IsFailed || IsReleased ? default : _operation.Scene;
        }

        Task IAssetHandler.WaitForCompletionAsync(CancellationToken token)
        {
            return WaitForCompletionAsync(token);
        }

        public async Task ActivateAsync(CancellationToken token = default)
        {
            await WaitForCompletionAsync(token);
            if (IsFailed || IsReleased)
            {
                throw new InvalidOperationException($"The scene '{Key}' is not loaded, so it cannot be activated.");
            }

            if (_operation.IsActivated)
            {
                return;
            }

            await _operation.ActivateAsync(token);
            RebindShaders();
        }

        public Task UnloadAsync(CancellationToken token = default)
        {
            if (!IsReleased)
            {
                MarkReleased();
                _unload = UnloadOperationAsync(_operation);
            }

            return _unload == null ? Task.CompletedTask : TaskCancellation.WaitAsync(_unload, token);
        }

        public void Release()
        {
            _ = UnloadAsync();
        }

        public void Fail(FailureKind kind, Exception exception)
        {
            if (IsReleased || IsDone)
            {
                return;
            }

            _tracker.Untrack(this);
            IsDone = true;
            IsFailed = true;
            Exception = exception;
            _completion.TrySetResult(true);
        }

        private static void ReleaseFromToken(object state)
        {
            ((SceneHandler)state).Release();
        }

        private async Task LoadAsync(ContentBootstrapper bootstrapper, CatalogUpdater catalogs)
        {
            CancellationToken token = _cancellation.Token;
            int attempt = 1;
            try
            {
                await bootstrapper.EnsureInitializedAsync(token);
                await catalogs.WaitIdleAsync(token);
                while (true)
                {
                    int generation = _context.Route.Generation;
                    SceneOperation operation = _context.Gateway.LoadScene(Key.RuntimeKey, _options);
                    _operation = operation;
                    try
                    {
                        await TaskCancellation.WaitAsync(operation.Completion, token);
                        break;
                    }
                    catch (Exception exception) when (!token.IsCancellationRequested)
                    {
                        _operation = null;
                        _ = UnloadOperationAsync(operation);
                        FailureKind kind = FailureClassifier.Classify(exception);
                        if (_context.Gateway.IsInternetReachable &&
                            _context.Route.TryAdvance(kind, generation))
                        {
                            await _context.Gateway.NextFrameAsync(token);
                            continue;
                        }

                        if (FailureClassifier.IsTransient(kind) &&
                            _policy.Retry.AllowsAttempt(attempt + 1))
                        {
                            attempt++;
                            await _context.Gateway.DelayAsync(_policy.Retry.Delay, token);
                            continue;
                        }

                        _context.Report(new ContentFailure(ContentStage.Scene, kind, Key.RuntimeKey,
                            exception));

                        Fail(kind, exception);
                        return;
                    }
                }

                IsDone = true;
                if (_operation.IsActivated)
                {
                    RebindShaders();
                }

                _completion.TrySetResult(true);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
            }
            catch (Exception exception)
            {
                FailureKind kind = FailureClassifier.Classify(exception);
                _context.Report(new ContentFailure(ContentStage.Scene, kind, Key.RuntimeKey,
                    exception));

                Fail(kind, exception);
            }
        }

        private async Task UnloadOperationAsync(SceneOperation operation)
        {
            if (operation == null)
            {
                return;
            }

            try
            {
                await operation.UnloadAsync();
            }
            catch (Exception exception)
            {
                _context.Report(new ContentFailure(ContentStage.Scene, FailureClassifier.Classify(exception),
                    Key.RuntimeKey, exception));
            }
        }

        private void MarkReleased()
        {
            IsReleased = true;
            _releaseTrace = HandlerTrace.Capture();
            _tracker.Untrack(this);
            _cancellation.Cancel();
            _cancellation.Dispose();
            if (_registrations != null)
            {
                foreach (CancellationTokenRegistration registration in _registrations)
                {
                    registration.Dispose();
                }

                _registrations = null;
            }

            _completion.TrySetCanceled();
        }

        private void RebindShaders()
        {
            if (_policy.RebindShadersInEditor &&
                _operation != null)
            {
                EditorShaderRebinder.Rebind(_operation.Scene);
            }
        }
    }
}