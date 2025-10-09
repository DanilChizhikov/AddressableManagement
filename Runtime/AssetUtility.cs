using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace MbsCore.AddressableManagement.Runtime
{
    internal sealed class AssetUtility
    {
        public static AssetResponse<TResult> LoadAssetAsync<TResult>(string key) where TResult : Object
        {
            AsyncOperationHandle<TResult> handle = Addressables.LoadAssetAsync<TResult>(key);
            return new AssetResponse<TResult>(key, handle);
        }
    }
}