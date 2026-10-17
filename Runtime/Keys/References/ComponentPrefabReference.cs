using System;
using UnityEngine;
using UnityEngine.AddressableAssets;
using Object = UnityEngine.Object;

namespace DTech.AddressableManagement
{
	[Serializable]
	internal sealed class ComponentPrefabReference<TComponent> : AssetReferenceGameObject where TComponent : Component
	{
		public ComponentPrefabReference(string guid) : base(guid)
		{
		}

		public override bool ValidateAsset(Object obj)
		{
			return obj is GameObject gameObject && gameObject.TryGetComponent(out TComponent _);
		}

		public override bool ValidateAsset(string path)
		{
			#if UNITY_EDITOR
			var gameObject = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
			return gameObject != null && gameObject.TryGetComponent(out TComponent _);
			#else
            return false;
			#endif
		}
	}
}