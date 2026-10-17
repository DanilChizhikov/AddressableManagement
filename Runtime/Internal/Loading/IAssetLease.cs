using System;

namespace DTech.AddressableManagement.Internal.Loading
{
    internal interface IAssetLease
    {
        bool IsReleased { get; }

        void Attach(AssetEntry entry);
        void Fail(FailureKind kind, Exception exception);
        void OnEntryCompleted(AssetEntry entry);
        void OnEntryReleased();
    }
}
