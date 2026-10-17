using System;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DTech.AddressableManagement
{
	/// <summary>
	/// Key of an asset by its Addressables address, for keys built in code or received from a server.
	/// </summary>
	[Serializable]
	public class AddressKey<TAsset> : AssetKeyT<TAsset>
		where TAsset : Object
	{
		/// <summary>
		/// Whether the address is not empty.
		/// </summary>
		public override bool IsValid => !string.IsNullOrEmpty(_address);

		/// <summary>
		/// The address.
		/// </summary>
		public override string RuntimeKey => _address ?? string.Empty;
		
		[SerializeField] private string _address;
		
		/// <summary>
		/// Creates an empty key.
		/// </summary>
		public AddressKey() : this(string.Empty)
		{
		}
		
		/// <summary>
		/// Creates a key for the given address.
		/// </summary>
		/// <param name="address">The Addressables address.</param>
		public AddressKey(string address)
		{
			_address = address ?? string.Empty;
		}
	}
}