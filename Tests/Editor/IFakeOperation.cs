namespace DTech.AddressableManagement.Tests.Editor
{
	internal interface IFakeOperation
	{
		int ReleaseCalls { get; }

		void Settle(object result);
	}
}