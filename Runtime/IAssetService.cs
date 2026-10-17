using System.Threading;
using System.Threading.Tasks;

namespace DTech.AddressableManagement
{
    public interface IAssetService
    {
        Task<long> GetDownloadSizeAsync(CancellationToken token = default);
        IAssetDownloadResponse DownloadAssets(bool updateCatalogs, CancellationToken token = default);
        IAssetHandler<TResult> LoadAsset<TResult>(AssetKey key);
        void UnloadAsset(AssetKey key);
    }
}