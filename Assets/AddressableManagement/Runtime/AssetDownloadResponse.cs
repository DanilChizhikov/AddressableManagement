using MbsCore.AddressableManagement.Infrastructure;

namespace MbsCore.AddressableManagement.Runtime
{
    internal sealed class AssetDownloadResponse : IAssetDownloadResponse
    {
        private const float Megabyte = 1024f;

        public float Progress => DownloadMegabytes > 0 ? DownloadMegabytes / DownloadedMegabytes : 0f;
        public float DownloadMegabytes { get; }
        public float DownloadedMegabytes { get; private set; }
        public bool IsDone { get; set; }

        public AssetDownloadResponse(long downloadSize)
        {
            DownloadMegabytes = BytesToMegabytes(downloadSize);
            DownloadedMegabytes = 0f;
            IsDone = false;
        }

        public static IAssetDownloadResponse GetEmpty() => new AssetDownloadResponse(0);

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