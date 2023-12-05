using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MbsCore.AddressableManagement.Infrastructure;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using Object = UnityEngine.Object;

namespace MbsCore.AddressableManagement.Runtime
{
    public sealed class AssetService : IAssetService, IDisposable
    {
        private const float MaxPercentComplete = 1f;
        private const int MillisecondsDelay = 1000;
        
        private readonly Dictionary<string, CancellationTokenSource> _loadAssetTokenMap;
        private readonly Dictionary<string, IAssetResponse> _responsesMap;
        private readonly Dictionary<Object, string> _loadedAssetMap;

        public AssetService()
        {
            _loadAssetTokenMap = new Dictionary<string, CancellationTokenSource>();
            _responsesMap = new Dictionary<string, IAssetResponse>();
            _loadedAssetMap = new Dictionary<Object, string>();
        }
        
        public IAssetResponse<TResult> LoadAsset<TResult>(AssetReference reference) where TResult : Object
        {
            if (!reference.RuntimeKeyIsValid())
            {
                throw new ArgumentException($"{nameof(AssetReference)} runtime kye is invalid!");
            }

            if (_responsesMap.TryGetValue(reference.AssetGUID, out IAssetResponse cachedResponse))
            {
                return cachedResponse as IAssetResponse<TResult>;
            }

            return LoadAsset<TResult>(reference.RuntimeKey.ToString());
        }

        public IAssetResponse<TResult> LoadAsset<TResult>(string key) where TResult : Object
        {
            if (_responsesMap.TryGetValue(key, out IAssetResponse cachedResponse))
            {
                return cachedResponse as IAssetResponse<TResult>;
            }
            
            AssetResponse<TResult> response = AssetUtility.LoadAssetAsync<TResult>(key);
            var tokenSource = new CancellationTokenSource();
            _loadAssetTokenMap.Add(key, tokenSource);
            Task.Factory.StartNew(() => LoadAssetAsync(response, tokenSource.Token));
            _responsesMap.Add(key, response);
            return response;
        }

        public void UnloadAsset<T>(T asset) where T : Object
        {
            if (!_loadedAssetMap.TryGetValue(asset, out string id))
            {
                return;
            }
            
            if(!_responsesMap.TryGetValue(id, out IAssetResponse cachedResponse) ||
               cachedResponse is not AssetResponse<T> response)
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

        private void ReleaseResponse<T>(ref AssetResponse<T> response) where T : Object
        {
            Object asset = response.OperationHandle.Result;
            if (!Addressables.ReleaseInstance(response.OperationHandle))
            {
                Object.Destroy(asset);
            }

            response.HasHandler = false;
            response = null;
        }

        private async Task LoadAssetAsync<TResult>(AssetResponse<TResult> response, CancellationToken token)
                where TResult : Object
        {
            AsyncOperationHandle<TResult> operationHandle = response.OperationHandle;
            while (!Mathf.Approximately(operationHandle.PercentComplete, MaxPercentComplete))
            {
                await Task.Delay(MillisecondsDelay);
            }

            await WaitUntil(() => operationHandle.IsDone);
            if (token.IsCancellationRequested)
            {
                response.HasHandler = false;
                Addressables.Release(operationHandle);
            }
            else
            {
                _loadedAssetMap.Add(operationHandle.Result, response.Id);
            }

            CancellationTokenSource tokenSource = _loadAssetTokenMap[response.Id];
            _loadAssetTokenMap.Remove(response.Id);
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