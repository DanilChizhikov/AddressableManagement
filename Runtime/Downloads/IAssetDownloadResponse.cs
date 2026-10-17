using System;
using System.Threading;
using System.Threading.Tasks;

namespace DTech.AddressableManagement
{
	/// <summary>
	/// A running download of bundles: state, byte progress and the result. Build any progress or consent UI on top of it.
	/// </summary>
	public interface IAssetDownloadResponse
	{
		/// <summary>
		/// Raised when State changes, including the final change to Completed.
		/// </summary>
		event Action<IAssetDownloadResponse> OnStateChanged;

		/// <summary>
		/// Where the download is in its lifetime.
		/// </summary>
		DownloadState State { get; }

		/// <summary>
		/// Downloaded share of TotalBytes, from 0 to 1.
		/// </summary>
		float Progress { get; }

		/// <summary>
		/// Bytes to download, known once the size query finished; 0 before that and when nothing is needed.
		/// </summary>
		long TotalBytes { get; }

		/// <summary>
		/// Bytes downloaded so far.
		/// </summary>
		long DownloadedBytes { get; }

		/// <summary>
		/// The number of the current attempt, starting from 1.
		/// </summary>
		int Attempt { get; }

		/// <summary>
		/// Whether the download finished.
		/// </summary>
		bool IsDone { get; }

		/// <summary>
		/// How the download ended; meaningful once IsDone is set.
		/// </summary>
		DownloadResult Result { get; }

		/// <summary>
		/// Waits until the download finishes. Failures and cancellation of the download are reported in the result.
		/// </summary>
		/// <param name="token">Cancels the wait only, not the download.</param>
		/// <returns>The result.</returns>
		Task<DownloadResult> WaitForCompletionAsync(CancellationToken token = default);

		/// <summary>
		/// Cancels the download. The result becomes Cancelled unless the download already finished.
		/// </summary>
		void Cancel();
	}
}