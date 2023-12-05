using UnityEngine;
using UnityEngine.AddressableAssets;

namespace MbsCore.AddressableManagement.Infrastructure
{
    public interface IAssetService
    {
        IAssetResponse<TResult> LoadAsset<TResult>(AssetReference reference) where TResult : Object;
        IAssetResponse<TResult> LoadAsset<TResult>(string key) where TResult : Object;
        void UnloadAsset<T>(T asset) where T : Object;
    }
}
