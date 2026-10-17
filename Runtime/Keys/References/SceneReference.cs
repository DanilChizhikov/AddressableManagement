using System;
using UnityEngine.AddressableAssets;
using Object = UnityEngine.Object;

namespace DTech.AddressableManagement
{
	[Serializable]
	internal sealed class SceneReference : AssetReference
	{
		public SceneReference(string guid) : base(guid)
		{
		}

		public override bool ValidateAsset(Object obj)
		{
			#if UNITY_EDITOR
			return obj is UnityEditor.SceneAsset;
			#else
            return false;
			#endif
		}

		public override bool ValidateAsset(string path)
		{
			#if UNITY_EDITOR
			return UnityEditor.AssetDatabase.GetMainAssetTypeAtPath(path) == typeof(UnityEditor.SceneAsset);
			#else
            return false;
			#endif
		}
	}
}