using UnityEngine;

namespace DTech.AddressableManagement
{
    /// <summary>
    /// How an AssetService behaves. Read once when the service is built; later changes do not reach it.
    /// </summary>
    [CreateAssetMenu(menuName = "DTech/Addressables/Asset Service Settings", fileName = "AssetServiceSettings")]
    public sealed class AssetServiceSettings : ScriptableObject
    {
        /// <summary>
        /// Where remote content is requested from. Never null.
        /// </summary>
        public ContentDeliverySettings Delivery
        {
            get => _delivery ??= new ContentDeliverySettings();
            set => _delivery = value;
        }

        /// <summary>
        /// How asset and scene loads retry when the bundles behind them fail to download.
        /// </summary>
        public RetryPolicy LoadRetry
        {
            get => _loadRetry;
            set => _loadRetry = value;
        }

        /// <summary>
        /// Deletes the cached remote catalogs once after Application.version changes, so a new build starts from
        /// the catalog it ships with. Cached bundles are kept.
        /// </summary>
        public bool ResetCatalogCacheOnVersionChange
        {
            get => _resetCatalogCacheOnVersionChange;
            set => _resetCatalogCacheOnVersionChange = value;
        }

        /// <summary>
        /// Removes cached bundles that no loaded catalog references after catalogs were updated. Skipped in the Editor.
        /// </summary>
        public bool CleanBundleCacheAfterCatalogUpdate
        {
            get => _cleanBundleCacheAfterCatalogUpdate;
            set => _cleanBundleCacheAfterCatalogUpdate = value;
        }

        /// <summary>
        /// In the Editor, swaps shaders of materials loaded from bundles for the Editor's own shaders, so content built
        /// for a mobile target does not render pink. Has no effect in players.
        /// </summary>
        public bool RebindShadersInEditor
        {
            get => _rebindShadersInEditor;
            set => _rebindShadersInEditor = value;
        }

        /// <summary>
        /// Logs routine progress such as rewritten requests and catalog checks. Warnings and errors are always logged.
        /// </summary>
        public bool EnableLogs
        {
            get => _enableLogs;
            set => _enableLogs = value;
        }

        [SerializeField] private ContentDeliverySettings _delivery = new ContentDeliverySettings();

        [Tooltip("How asset and scene loads retry when the bundles behind them fail to download.")]
        [SerializeField] private RetryPolicy _loadRetry = RetryPolicy.Default;

        [Tooltip("Delete cached remote catalogs once after Application.version changes. Cached bundles are kept.")]
        [SerializeField] private bool _resetCatalogCacheOnVersionChange;

        [Tooltip("Remove cached bundles no loaded catalog references after catalogs were updated. Skipped in the Editor.")]
        [SerializeField] private bool _cleanBundleCacheAfterCatalogUpdate;

        [Tooltip("In the Editor, swap shaders of materials loaded from bundles for the Editor's own shaders.")]
        [SerializeField] private bool _rebindShadersInEditor = true;

        [Tooltip("Log routine progress. Warnings and errors are always logged.")]
        [SerializeField] private bool _enableLogs;
    }
}