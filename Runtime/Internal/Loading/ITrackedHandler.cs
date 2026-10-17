namespace DTech.AddressableManagement.Internal.Loading
{
    internal interface ITrackedHandler
    {
        string RuntimeKey { get; }
        string CreationTrace { get; }

        void Release();
    }
}
