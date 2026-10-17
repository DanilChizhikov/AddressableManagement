using System;

namespace DTech.AddressableManagement
{
	/// <summary>
	/// Key of an addressable scene. The inspector only offers scene assets.
	/// </summary>
	[Serializable]
	public sealed class SceneKey : AssetKey
	{
		public override bool IsValid { get; }
		public override string RuntimeKey { get; }
	}
}