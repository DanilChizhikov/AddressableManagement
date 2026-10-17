namespace DTech.AddressableManagement
{
	/// <summary>
	/// The step of content delivery that failed.
	/// </summary>
	public enum ContentStage : byte
	{
		/// <summary>
		/// Initialization of Addressables and of the CDN route.
		/// </summary>
		Initialization = 0,

		/// <summary>
		/// Checking for and applying catalog updates.
		/// </summary>
		CatalogUpdate = 1,

		/// <summary>
		/// Downloading bundles.
		/// </summary>
		Download = 2,

		/// <summary>
		/// Loading an asset.
		/// </summary>
		Load = 3,

		/// <summary>
		/// Loading, activating or unloading a scene.
		/// </summary>
		Scene = 4,
	}
}