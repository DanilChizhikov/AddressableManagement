using System.Threading;
using System.Threading.Tasks;

namespace DTech.AddressableManagement
{
    public interface IAssetHandler
    {
        AssetKey Key { get; }
        float Progress { get; }
        bool IsDone { get; }
        bool IsReleased { get; }

        void AttachCancellationToken(CancellationToken token);
        Task WaitForCompletionAsync(CancellationToken token = default);
        void Release();
    }
    
    public interface IAssetHandler<TResult> : IAssetHandler
    {
        TResult Result { get; }
    }
}