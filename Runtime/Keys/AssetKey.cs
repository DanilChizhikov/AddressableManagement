using System;
using UnityEngine.AddressableAssets;

namespace DTech.AddressableManagement
{
	/// <summary>
	/// Serializable reference to Addressables content. A key only holds data: loading, downloading and
	/// size queries go through IAssetService.
	/// </summary>
	[Serializable]
	public abstract class AssetKey : IEquatable<AssetKey>
	{
		/// <summary>
		/// Whether the key points at something: a well-formed asset GUID, an address or a label.
		/// </summary>
		public abstract bool IsValid { get; }
		
		/// <summary>
		/// The key Addressables resolves: an asset GUID with an optional [sub-object] suffix, an address or a label.
		/// Never null.
		/// </summary>
		public abstract string RuntimeKey { get; }
		
		private protected AssetKey()
		{
		}
		
		/// <summary>
		/// Whether both keys are of the same kind and resolve the same runtime key.
		/// </summary>
		/// <param name="other">The key to compare with.</param>
		/// <returns>True when both keys address the same content the same way.</returns>
		public bool Equals(AssetKey other)
		{
			if (ReferenceEquals(this, other))
			{
				return true;
			}

			return other != null && other.GetType() == GetType() &&
				string.Equals(other.RuntimeKey, RuntimeKey, StringComparison.Ordinal);
		}
		
		/// <summary>
		/// Whether the object is a key of the same kind with the same runtime key.
		/// </summary>
		/// <param name="obj">The object to compare with.</param>
		/// <returns>True when the object addresses the same content the same way.</returns>
		public sealed override bool Equals(object obj) => Equals(obj as AssetKey);

		/// <summary>
		/// Hash of the key kind and the runtime key.
		/// </summary>
		/// <returns>The hash code.</returns>
		public sealed override int GetHashCode() => HashCode.Combine(GetType(), RuntimeKey);

		/// <summary>
		/// The runtime key.
		/// </summary>
		/// <returns>The runtime key, never null.</returns>
		public sealed override string ToString() => RuntimeKey;
	}
}