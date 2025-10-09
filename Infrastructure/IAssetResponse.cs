using UnityEngine;

namespace MbsCore.AddressableManagement.Infrastructure
{
    public interface IAssetResponse
    {
        float Progress { get; }
        bool IsDone { get; }
    }
    
    public interface IAssetResponse<TResult> : IAssetResponse where TResult : Object
    {
        TResult Result { get; }
    }
}