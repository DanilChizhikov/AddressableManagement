namespace DTech.AddressableManagement
{
	/// <summary>
	/// How a download runs. The default value makes a single attempt against the catalogs already loaded.
	/// </summary>
	public readonly struct DownloadOptions
	{
		/// <summary>
		/// Default retries, no catalog update first.
		/// </summary>
		public static DownloadOptions Default => new (RetryPolicy.Default, false);

		/// <summary>
		/// How failed attempts are retried.
		/// </summary>
		public RetryPolicy Retry { get; }

		/// <summary>
		/// Whether catalogs are updated before the download size is measured.
		/// </summary>
		public bool UpdateCatalogsFirst { get; }

		/// <summary>
		/// Creates download options.
		/// </summary>
		/// <param name="retry">How failed attempts are retried.</param>
		/// <param name="updateCatalogsFirst">Whether catalogs are updated before the download.</param>
		public DownloadOptions(RetryPolicy retry, bool updateCatalogsFirst)
		{
			Retry = retry;
			UpdateCatalogsFirst = updateCatalogsFirst;
		}
	}
}