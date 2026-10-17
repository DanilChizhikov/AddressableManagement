using System;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DTech.AddressableManagement.Internal.Loading
{
    internal static class AssetConversion
    {
        public static bool LoadsAsGameObject(Type assetType)
        {
            return typeof(Component).IsAssignableFrom(assetType);
        }

        public static bool TryConvert<TAsset>(Object asset, out TAsset result) where TAsset : Object
        {
            if (asset is TAsset direct)
            {
                result = direct;
                return true;
            }

            if (asset is GameObject gameObject && LoadsAsGameObject(typeof(TAsset)) &&
                gameObject.TryGetComponent(typeof(TAsset), out Component component))
            {
                result = (TAsset)(Object)component;
                return true;
            }

            result = null;
            return false;
        }
    }
}
