using System;
using UnityEngine.AddressableAssets;

namespace DTech.AddressableManagement
{
	[Serializable]
	public abstract class AssetKey
	{
		public bool IsValid => Reference != null && Reference.RuntimeKeyIsValid();
		
		protected abstract AssetReference Reference { get; }

		public sealed override string ToString() => Reference == null ? string.Empty : Reference.RuntimeKey.ToString();
	}
}