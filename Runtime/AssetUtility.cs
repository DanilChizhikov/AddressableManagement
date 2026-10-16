using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace DTech.AddressableManagement
{
    internal sealed class AssetUtility
    {
        public static AssetHandler<TResult> LoadAssetAsync<TResult>(string key) where TResult : Object
        {
            AsyncOperationHandle<TResult> handle = Addressables.LoadAssetAsync<TResult>(key);
            return new AssetHandler<TResult>(key, handle);
        }
    }
}