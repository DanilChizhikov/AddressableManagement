using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine.AddressableAssets;
using Object = UnityEngine.Object;

namespace DTech.AddressableManagement
{
    public sealed class AssetService : IAssetService, IDisposable
    {
        private const float MaxPercentComplete = 1f;
        private const int MillisecondsDelay = 1000;
        
        private readonly Dictionary<string, CancellationTokenSource> _loadAssetTokenMap;
        private readonly Dictionary<string, IAssetHandler> _responsesMap;
        private readonly Dictionary<Object, string> _loadedAssetMap;
        private readonly AssetDownloadManager _downloadManager;

        public AssetService()
        {
            _loadAssetTokenMap = new Dictionary<string, CancellationTokenSource>();
            _responsesMap = new Dictionary<string, IAssetHandler>();
            _loadedAssetMap = new Dictionary<Object, string>();
            _downloadManager = new AssetDownloadManager();
        }
        
        public Task<long> GetDownloadSizeAsync(CancellationToken token = default)
        {
            throw new NotImplementedException();
        }

        public IAssetDownloadResponse DownloadAssets(bool updateCatalogs, CancellationToken token = default)
        {
            throw new NotImplementedException();
        }

        public IAssetHandler<TResult> LoadAsset<TResult>(AssetKey key)
        {
            throw new NotImplementedException();
        }

        public void UnloadAsset(AssetKey key)
        {
            throw new NotImplementedException();
        }

        public IAssetDownloadResponse DownloadAssets() => _downloadManager.DownloadAssets();

        public IAssetHandler<TResult> LoadAsset<TResult>(AssetReference reference) where TResult : Object
        {
            throw new NotImplementedException();
        }

        public void UnloadAsset<T>(T asset) where T : Object
        {
        }
        
        public void Dispose()
        {
        }
    }
}