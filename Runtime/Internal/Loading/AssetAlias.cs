using System;

namespace DTech.AddressableManagement.Internal.Loading
{
    internal readonly struct AssetAlias : IEquatable<AssetAlias>
    {
        public string RuntimeKey { get; }
        public Type LoadType { get; }

        public AssetAlias(string runtimeKey, Type loadType)
        {
            RuntimeKey = runtimeKey ?? string.Empty;
            LoadType = loadType;
        }

        public bool Equals(AssetAlias other)
        {
            return string.Equals(RuntimeKey, other.RuntimeKey, StringComparison.Ordinal) && LoadType == other.LoadType;
        }

        public override bool Equals(object obj)
        {
            return obj is AssetAlias other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(RuntimeKey, LoadType);
        }
    }
}
