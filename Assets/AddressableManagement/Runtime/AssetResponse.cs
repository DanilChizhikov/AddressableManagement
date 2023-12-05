using MbsCore.AddressableManagement.Infrastructure;
using UnityEngine;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace MbsCore.AddressableManagement.Runtime
{
    internal sealed class AssetResponse<TResult> : IAssetResponse<TResult> where TResult : Object
    {
        public string Id { get; }
        public AsyncOperationHandle<TResult> OperationHandle { get; }
        public float Progress => HasHandler ? OperationHandle.PercentComplete : 0f;
        public bool IsDone => HasHandler && OperationHandle.IsDone;
        public TResult Result => HasHandler ? OperationHandle.Result : null;
        public bool HasHandler { get; set; }

        public AssetResponse(string id, AsyncOperationHandle<TResult> operationHandle)
        {
            Id = id;
            OperationHandle = operationHandle;
            HasHandler = true;
        }
    }
}