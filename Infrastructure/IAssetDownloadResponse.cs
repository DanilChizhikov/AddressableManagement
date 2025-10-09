namespace MbsCore.AddressableManagement.Infrastructure
{
    public interface IAssetDownloadResponse
    {
        float Progress { get; }
        float DownloadMegabytes { get; }
        float DownloadedMegabytes { get; }
        bool IsDone { get; }
    }
}