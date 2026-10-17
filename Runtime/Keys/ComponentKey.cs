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
		public override bool IsValid { get; }
		public override string RuntimeKey { get; }
	}
}