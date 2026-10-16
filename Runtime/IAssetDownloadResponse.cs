namespace DTech.AddressableManagement
{
    public interface IAssetDownloadResponse
    {
        float Progress { get; }
        float DownloadMegabytes { get; }
        float DownloadedMegabytes { get; }
        bool IsDone { get; }
    }
}