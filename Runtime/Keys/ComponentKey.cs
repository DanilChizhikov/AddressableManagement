using System;
using UnityEngine;

namespace DTech.AddressableManagement
{
	/// <summary>
	/// Key of a prefab that carries TComponent. The inspector only offers prefabs with that component, and a load
	/// returns the component of the loaded prefab.
	/// </summary>
	[Serializable]
	public class ComponentKey<TComponent> : AssetKeyT<TComponent>
		where TComponent : Component
	{
		/// <summary>
		/// Whether the key holds a well-formed asset GUID.
		/// </summary>
		public override bool IsValid => _reference != null && _reference.RuntimeKeyIsValid();

		/// <summary>
		/// The prefab GUID.
		/// </summary>
		public override string RuntimeKey => _reference == null ? string.Empty : _reference.RuntimeKey.ToString();

		[SerializeField] private ComponentPrefabReference<TComponent> _reference;

		/// <summary>
		/// Creates an empty key.
		/// </summary>
		public ComponentKey() : this(string.Empty)
		{
		}

		/// <summary>
		/// Creates a key for the prefab with the given GUID.
		/// </summary>
		/// <param name="guid">The prefab GUID.</param>
		public ComponentKey(string guid)
		{
			_reference = new ComponentPrefabReference<TComponent>(guid ?? string.Empty);
		}
	}
}