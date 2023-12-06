using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace MbsCore.AddressableManagement.Infrastructure
{
    public interface IAssetService
    {
        Task<long> GetDownloadSizeAsync();
        IAssetDownloadResponse DownloadAssets();
        IAssetResponse<TResult> LoadAsset<TResult>(AssetReference reference) where TResult : Object;
        IAssetResponse<TResult> LoadAsset<TResult>(string key) where TResult : Object;
        void UnloadAsset<T>(T asset) where T : Object;
    }
}
