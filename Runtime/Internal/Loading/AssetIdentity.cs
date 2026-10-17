using System;

namespace DTech.AddressableManagement.Internal.Loading
{
    internal readonly struct AssetIdentity : IEquatable<AssetIdentity>
    {
        public string InternalId { get; }
        public Type LoadType { get; }

        public AssetIdentity(string internalId, Type loadType)
        {
            InternalId = internalId ?? string.Empty;
            LoadType = loadType;
        }

        public bool Equals(AssetIdentity other)
        {
            return string.Equals(InternalId, other.InternalId, StringComparison.Ordinal) && LoadType == other.LoadType;
        }

        public override bool Equals(object obj)
        {
            return obj is AssetIdentity other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(InternalId, LoadType);
        }
    }
}
