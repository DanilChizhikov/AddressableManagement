using System;
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
		public override bool IsValid { get; }
		public override string RuntimeKey { get; }
	}
}