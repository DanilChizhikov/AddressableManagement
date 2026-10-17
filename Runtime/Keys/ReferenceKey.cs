using System;
using UnityEngine;
using UnityEngine.AddressableAssets;
using Object = UnityEngine.Object;

namespace DTech.AddressableManagement
{
	/// <summary>
	/// Key of an asset picked in the inspector by its GUID, optionally a sub-asset such as a sprite inside a texture.
	/// </summary>
	[Serializable]
	public class ReferenceKey<TAsset> : AssetKeyT<TAsset>
		where TAsset : Object
	{
		/// <summary>
		/// Whether the key holds a well-formed asset GUID.
		/// </summary>
		public override bool IsValid => _reference != null && _reference.RuntimeKeyIsValid();

		/// <summary>
		/// The asset GUID, followed by [sub-object name] for a sub-asset.
		/// </summary>
		public override string RuntimeKey => _reference == null ? string.Empty : _reference.RuntimeKey.ToString();

		[SerializeField] private AssetReferenceT<TAsset> _reference;

		/// <summary>
		/// Creates an empty key.
		/// </summary>
		public ReferenceKey() : this(string.Empty, string.Empty)
		{
		}

		/// <summary>
		/// Creates a key for the main asset with the given GUID.
		/// </summary>
		/// <param name="guid">The asset GUID.</param>
		public ReferenceKey(string guid) : this(guid, string.Empty)
		{
		}

		/// <summary>
		/// Creates a key for a sub-asset of the asset with the given GUID.
		/// </summary>
		/// <param name="guid">The asset GUID.</param>
		/// <param name="subObjectName">The sub-asset name, empty for the main asset.</param>
		public ReferenceKey(string guid, string subObjectName)
		{
			_reference = new AssetReferenceT<TAsset>(guid ?? string.Empty)
			{
				SubObjectName = subObjectName ?? string.Empty,
			};
		}
	}
}