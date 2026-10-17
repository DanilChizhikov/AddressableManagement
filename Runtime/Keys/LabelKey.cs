using System;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace DTech.AddressableManagement
{
	/// <summary>
	/// Key of every asset that carries an Addressables label: batch loads, download sizes and downloads of a pack.
	/// </summary>
	[Serializable]
	public sealed class LabelKey : AssetKey
	{
		/// <summary>
		/// Whether the label is not empty.
		/// </summary>
		public override bool IsValid => !string.IsNullOrEmpty(RuntimeKey);

		/// <summary>
		/// The label.
		/// </summary>
		public override string RuntimeKey => _label?.labelString ?? string.Empty;

		[SerializeField] private AssetLabelReference _label;

		/// <summary>
		/// Creates an empty key.
		/// </summary>
		public LabelKey() : this(string.Empty)
		{
		}

		/// <summary>
		/// Creates a key for the given label.
		/// </summary>
		/// <param name="label">The Addressables label.</param>
		public LabelKey(string label)
		{
			_label = new AssetLabelReference
			{
				labelString = label ?? string.Empty,
			};
		}
	}
}