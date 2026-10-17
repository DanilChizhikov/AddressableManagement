namespace DTech.AddressableManagement
{
	/// <summary>
	/// How a download ended.
	/// </summary>
	public enum DownloadOutcome : byte
	{
		/// <summary>
		/// The download has not finished.
		/// </summary>
		None = 0,

		/// <summary>
		/// The bundles were downloaded and verified.
		/// </summary>
		Downloaded = 1,

		/// <summary>
		/// Nothing had to be downloaded.
		/// </summary>
		AlreadyCached = 2,

		/// <summary>
		/// Every allowed attempt failed.
		/// </summary>
		Failed = 3,

		/// <summary>
		/// The download was cancelled.
		/// </summary>
		Cancelled = 4,
	}
}