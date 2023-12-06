using MbsCore.AddressableManagement.Infrastructure;
using UnityEngine;

namespace MbsCore.AddressableManagement.Runtime
{
    internal sealed class AssetDownloadResponse : IAssetDownloadResponse
    {
        private const float Megabyte = 1024f;
        private const float MaxProgress = 1f;

        public float Progress => DownloadMegabytes > 0f ? Mathf.Clamp01(DownloadMegabytes / DownloadedMegabytes) : MaxProgress;
        public float DownloadMegabytes { get; }
        public float DownloadedMegabytes { get; private set; }
        public bool IsDone { get; private set; }

        public AssetDownloadResponse(long downloadSize)
        {
            DownloadMegabytes = BytesToMegabytes(downloadSize);
            DownloadedMegabytes = 0f;
            CheckDoneStatus();
        }

        public static IAssetDownloadResponse GetEmpty() => new AssetDownloadResponse(0);

        public void CheckDoneStatus()
        {
            IsDone = Mathf.Approximately(Progress, MaxProgress);
        }

        public void SetDownloadedBytes(long value)
        {
            DownloadedMegabytes = BytesToMegabytes(value);
        }
        
        private float BytesToMegabytes(long bytes)
        {
            float kilobytes = bytes / Megabyte;
            return kilobytes / Megabyte;
        }
    }
}