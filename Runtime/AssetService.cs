using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
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
        
        public async Task<long> GetDownloadSizeAsync(CancellationToken token = default)
        {
            
        }

        public IAssetDownloadResponse DownloadAssets(bool updateCatalogs, CancellationToken token = default)
        {
            
        }

        public IAssetHandler<TResult> LoadAsset<TResult>(AssetKey key)
        {
            
        }

        public void UnloadAsset(AssetKey key)
        {
            
        }

        public async Task<long> GetDownloadSizeAsync()
        {
            await _downloadManager.InitializeAsync();
            return _downloadManager.DownloadSize;
        }

        public IAssetDownloadResponse DownloadAssets() => _downloadManager.DownloadAssets();

        public IAssetHandler<TResult> LoadAsset<TResult>(AssetReference reference) where TResult : Object
        {
            if (!reference.RuntimeKeyIsValid())
            {
                throw new ArgumentException($"{nameof(AssetReference)} runtime kye is invalid!");
            }

            if (_responsesMap.TryGetValue(reference.AssetGUID, out IAssetHandler cachedResponse))
            {
                return cachedResponse as IAssetHandler<TResult>;
            }

            return LoadAsset<TResult>(reference.RuntimeKey.ToString());
        }

        public IAssetHandler<TResult> LoadAsset<TResult>(string key) where TResult : Object
        {
            if (_responsesMap.TryGetValue(key, out IAssetHandler cachedResponse))
            {
                return cachedResponse as IAssetHandler<TResult>;
            }
            
            AssetHandler<TResult> handler = AssetUtility.LoadAssetAsync<TResult>(key);
            var tokenSource = new CancellationTokenSource();
            _loadAssetTokenMap.Add(key, tokenSource);
            Task.Factory.StartNew(() => LoadAssetAsync(handler, tokenSource.Token));
            _responsesMap.Add(key, handler);
            return handler;
        }

        public void UnloadAsset<T>(T asset) where T : Object
        {
            if (!_loadedAssetMap.TryGetValue(asset, out string id))
            {
                return;
            }
            
            if(!_responsesMap.TryGetValue(id, out IAssetHandler cachedResponse) ||
               cachedResponse is not AssetHandler<T> response)
            {
                return;
            }

            _loadedAssetMap.Remove(asset);
            _responsesMap.Remove(id);
            ReleaseResponse(ref response);
        }
        
        public void Dispose()
        {
            var tokenSources = new HashSet<CancellationTokenSource>(_loadAssetTokenMap.Values);
            foreach (var tokenSource in tokenSources)
            {
                tokenSource.Cancel();
            }
            
            _loadAssetTokenMap.Clear();
            var assets = new HashSet<Object>(_loadedAssetMap.Keys);
            foreach (var asset in assets)
            {
                UnloadAsset(asset);
            }
            
            _loadedAssetMap.Clear();
            _responsesMap.Clear();
        }

        private void ReleaseResponse<T>(ref AssetHandler<T> handler) where T : Object
        {
            Object asset = handler.OperationHandle.Result;
            if (!Addressables.ReleaseInstance(handler.OperationHandle))
            {
                Object.Destroy(asset);
            }

            handler.HasHandler = false;
            handler = null;
        }

        private async Task LoadAssetAsync<TResult>(AssetHandler<TResult> handler, CancellationToken token)
                where TResult : Object
        {
            AsyncOperationHandle<TResult> operationHandle = handler.OperationHandle;
            while (!Mathf.Approximately(operationHandle.PercentComplete, MaxPercentComplete))
            {
                await Task.Delay(MillisecondsDelay);
            }

            await WaitUntil(() => operationHandle.IsDone);
            if (token.IsCancellationRequested)
            {
                handler.HasHandler = false;
                Addressables.Release(operationHandle);
            }
            else
            {
                _loadedAssetMap.Add(operationHandle.Result, handler.Key);
            }

            CancellationTokenSource tokenSource = _loadAssetTokenMap[handler.Key];
            _loadAssetTokenMap.Remove(handler.Key);
            tokenSource.Dispose();
        }

        private async Task WaitUntil(Func<bool> predicate, CancellationToken token = new CancellationToken())
        {
            while (!predicate.Invoke())
            {
                await Task.Delay(MillisecondsDelay, token);
            }
        }
    }
}