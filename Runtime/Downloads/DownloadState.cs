namespace DTech.AddressableManagement
{
	/// <summary>
	/// Where a download is in its lifetime.
	/// </summary>
	public enum DownloadState : byte
	{
		/// <summary>
		/// Waiting for initialization, a running catalog update or the size query.
		/// </summary>
		Queued = 0,

		/// <summary>
		/// Waiting until every download suspension is disposed.
		/// </summary>
		Suspended = 1,

		/// <summary>
		/// Bundles are being downloaded.
		/// </summary>
		Downloading = 2,

		/// <summary>
		/// The last attempt failed; waiting for the next one.
		/// </summary>
		Retrying = 3,

		/// <summary>
		/// Finished; the result is available.
		/// </summary>
		Completed = 4,
	}
}