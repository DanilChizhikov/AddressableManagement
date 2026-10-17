using System;
using UnityEngine;

namespace DTech.AddressableManagement
{
	/// <summary>
	/// Key of an addressable scene. The inspector only offers scene assets.
	/// </summary>
	[Serializable]
	public sealed class SceneKey : AssetKey
	{
		/// <summary>
		/// Whether the key holds a well-formed asset GUID.
		/// </summary>
		public override bool IsValid => _reference != null && _reference.RuntimeKeyIsValid();

		/// <summary>
		/// The scene GUID.
		/// </summary>
		public override string RuntimeKey => _reference == null ? string.Empty : _reference.RuntimeKey.ToString();

		[SerializeField] private SceneReference _reference;

		/// <summary>
		/// Creates an empty key.
		/// </summary>
		public SceneKey() : this(string.Empty)
		{
		}

		/// <summary>
		/// Creates a key for the scene with the given GUID.
		/// </summary>
		/// <param name="guid">The scene asset GUID.</param>
		public SceneKey(string guid)
		{
			_reference = new SceneReference(guid ?? string.Empty);
		}
	}
}