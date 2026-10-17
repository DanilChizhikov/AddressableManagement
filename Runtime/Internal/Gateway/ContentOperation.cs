using System.Threading.Tasks;

namespace DTech.AddressableManagement.Internal.Gateway
{
	internal abstract class ContentOperation<TResult>
	{
		public abstract Task<TResult> Completion { get; }
		public abstract float Progress { get; }
		public abstract long DownloadedBytes { get; }
		public abstract long TotalBytes { get; }

		public abstract void Release();
	}
}