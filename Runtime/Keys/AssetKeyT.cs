using System;

namespace DTech.AddressableManagement
{
	/// <summary>
	/// Key of a single asset that loads as TAsset. A component type loads the prefab that carries it.
	/// </summary>
	[Serializable]
	public abstract class AssetKeyT<TAsset> : AssetKey
	{
		private protected AssetKeyT()
		{
		}
	}
}