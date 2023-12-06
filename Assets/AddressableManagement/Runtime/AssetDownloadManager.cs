using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MbsCore.AddressableManagement.Infrastructure;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.AddressableAssets.ResourceLocators;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace MbsCore.AddressableManagement.Runtime
{
    internal sealed class AssetDownloadManager : IDisposable
    {
        private const float PercentComplete = 1f;
        private const int DownloadDelay = 1000;
        
        private readonly List<object> _resourceKeys;

        private AssetDownloadResponse _response;
        private CancellationTokenSource _downloadTokenSource;
        
        public bool IsInitialized { get; private set; }
        public long DownloadSize { get; private set; }

        public AssetDownloadManager()
        {
            _resourceKeys = new List<object>();
            IsInitialized = Application.isEditor;
            DownloadSize = 0;
        }

        public async Task InitializeAsync()
        {
            if (IsInitialized)
            {
                return;
            }

            IResourceLocator resourceLocator = await Addressables.InitializeAsync().Task;
            _resourceKeys.Clear();
            foreach (var key in resourceLocator.Keys)
            {
                if (_resourceKeys.Contains(key))
                {
                    continue;
                }

                AsyncOperationHandle<long> handle = Addressables.GetDownloadSizeAsync(key);
                long downloadSize = await handle.Task;
                if (downloadSize <= 0)
                {
                    continue;
                }
                
                _resourceKeys.Add(key);
                Addressables.Release(handle);
            }

            await GetDownloadSizeAsync(_resourceKeys);
            IsInitialized = true;
        }

        public IAssetDownloadResponse DownloadAssets()
        {
            if (!IsInitialized)
            {
                return AssetDownloadResponse.GetEmpty();
            }

            if (_response == null)
            {
                _response = new AssetDownloadResponse(DownloadSize);
                _downloadTokenSource = new CancellationTokenSource();
                Task.Factory.StartNew(() => DownloadCycleAsync(_downloadTokenSource.Token));
            }

            return _response;
        }
        
        public void Dispose()
        {
            IsInitialized = false;
            _downloadTokenSource?.Cancel();
            _downloadTokenSource?.Dispose();
            DownloadSize = 0;
            _resourceKeys.Clear();
        }

        private async Task GetDownloadSizeAsync(IEnumerable<object> keys)
        {
            AsyncOperationHandle<long> sizeHandler = Addressables.GetDownloadSizeAsync(keys);
            DownloadSize = await sizeHandler.Task;
            Addressables.Release(sizeHandler);
        }

        private async Task DownloadCycleAsync(CancellationToken token)
        {
            AsyncOperationHandle handle = Addressables.DownloadDependenciesAsync(_resourceKeys, Addressables.MergeMode.Union);
            long downloadSize = DownloadSize;
            while (!Mathf.Approximately(handle.PercentComplete, PercentComplete))
            {
                if (token.IsCancellationRequested)
                {
                    Addressables.Release(handle);
                    break;
                }
                
                DownloadStatus status = handle.GetDownloadStatus();
                _response.SetDownloadedBytes(status.DownloadedBytes);
                DownloadSize = downloadSize - status.DownloadedBytes;
                await Task.Delay(DownloadDelay);
            }

            await handle.Task;
            Addressables.Release(handle);
            _response.IsDone = Mathf.Approximately(_response.DownloadMegabytes, _response.DownloadedMegabytes);
        }
    }
}