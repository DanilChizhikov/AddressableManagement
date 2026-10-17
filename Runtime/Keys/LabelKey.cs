using System;

namespace DTech.AddressableManagement
{
	/// <summary>
	/// Key of every asset that carries an Addressables label: batch loads, download sizes and downloads of a pack.
	/// </summary>
	[Serializable]
	public sealed class LabelKey : AssetKey
	{
		public override bool IsValid { get; }
		public override string RuntimeKey { get; }
	}
}