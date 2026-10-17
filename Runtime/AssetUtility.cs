using System;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using Object = UnityEngine.Object;

namespace DTech.AddressableManagement
{
    internal sealed class AssetUtility
    {
        public static AssetHandler<TResult> LoadAssetAsync<TResult>(string key) where TResult : Object
        {
            throw new NotImplementedException();
        }
    }
}