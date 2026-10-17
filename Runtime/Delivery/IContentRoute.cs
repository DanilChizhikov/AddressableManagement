using System;

namespace DTech.AddressableManagement
{
	/// <summary>
	/// The CDN route remote requests currently take. The route moves forward on failures and stays there for the
	/// session; Reset and Apply move it back.
	/// </summary>
	public interface IContentRoute
	{
		/// <summary>
		/// Raised when the active content root or mirror changes.
		/// </summary>
		event Action<IContentRoute> OnChanged;

		/// <summary>
		/// The content root requests go to, or null when no roots are configured.
		/// </summary>
		string ActiveRoot { get; }

		/// <summary>
		/// The origin requests go to, or null when no mirrors are configured.
		/// </summary>
		string ActiveMirror { get; }

		/// <summary>
		/// Replaces the roots and mirrors, for example with values from remote configuration, and starts from the
		/// first of each. Requests already sent are not affected.
		/// </summary>
		/// <param name="settings">The new delivery settings.</param>
		void Apply(ContentDeliverySettings settings);

		/// <summary>
		/// Goes back to the first content root and the first mirror.
		/// </summary>
		void Reset();
	}
}