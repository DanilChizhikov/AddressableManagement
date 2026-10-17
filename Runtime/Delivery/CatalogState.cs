namespace DTech.AddressableManagement
{
	/// <summary>
	/// Outcome of the latest catalog update.
	/// </summary>
	public enum CatalogState : byte
	{
		/// <summary>
		/// No update was requested yet.
		/// </summary>
		NotChecked = 0,

		/// <summary>
		/// An update is running.
		/// </summary>
		Updating = 1,

		/// <summary>
		/// The catalogs were already the latest.
		/// </summary>
		UpToDate = 2,

		/// <summary>
		/// Newer catalogs were downloaded and applied.
		/// </summary>
		Updated = 3,

		/// <summary>
		/// The update failed; the catalogs loaded at initialization stay in use.
		/// </summary>
		Failed = 4,
	}
}