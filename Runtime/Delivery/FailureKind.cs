namespace DTech.AddressableManagement
{
	/// <summary>
	/// Why content could not be delivered.
	/// </summary>
	public enum FailureKind
	{
		/// <summary>
		/// No failure.
		/// </summary>
		None = 0,

		/// <summary>
		/// The server answered 404 or 410, or the key has no location of the requested type.
		/// </summary>
		NotFound = 1,

		/// <summary>
		/// The server rejected the request with another 4xx code.
		/// </summary>
		ClientError = 2,

		/// <summary>
		/// The server failed with a 5xx code.
		/// </summary>
		ServerError = 3,

		/// <summary>
		/// The device is offline, the connection failed or timed out, or the server asked to slow down.
		/// </summary>
		Network = 4,

		/// <summary>
		/// The received data could not be processed, for example a bundle failed its CRC check.
		/// </summary>
		Corrupted = 5,

		/// <summary>
		/// Addressables does not know the key.
		/// </summary>
		InvalidKey = 6,

		/// <summary>
		/// The asset loaded but is not of the requested type, for example a prefab without the requested component.
		/// </summary>
		TypeMismatch = 7,

		/// <summary>
		/// Any other failure.
		/// </summary>
		Unknown = 8,
	}
}