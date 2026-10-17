using System;

namespace DTech.AddressableManagement.Internal.Delivery
{
	internal sealed class ContentDeliveryException : Exception
	{
		public FailureKind Kind { get; }

		public ContentDeliveryException(FailureKind kind, string message) : base(message)
		{
			Kind = kind;
		}

		public ContentDeliveryException(FailureKind kind, string message, Exception innerException)
			: base(message, innerException)
		{
			Kind = kind;
		}
	}
}