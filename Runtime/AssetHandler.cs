using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace DTech.AddressableManagement
{
    internal sealed class AssetHandler<TResult> : IAssetHandler<TResult> where TResult : Object
    {
        private readonly AsyncOperationHandle<TResult> _operationHandle;
        
        public AssetKey Key { get; }
        public float Progress => _operationHandle.PercentComplete;
        public bool IsDone => !IsReleased && _operationHandle.IsDone;
        public bool IsReleased { get; private set; }
        public TResult Result => _operationHandle.Result;

        public AssetHandler(AssetKey key, AsyncOperationHandle<TResult> operationHandle)
        {
            Key = key;
            _operationHandle = operationHandle;
        }
        
        public void AttachCancellationToken(CancellationToken token)
        {
            token.Register(Release);
        }

        public async Task WaitForCompletionAsync(CancellationToken token = default)
        {
            while (!_operationHandle.IsDone)
            {
                if (token.IsCancellationRequested || _operationHandle.Status == AsyncOperationStatus.Failed)
                {
                    break;
                }
                
                await Task.Yield();
            }
        }

        public void Release()
        {
            if (IsReleased)
            {
                return;
            }
            
            _operationHandle.Release();
            IsReleased = true;
        }
    }
}