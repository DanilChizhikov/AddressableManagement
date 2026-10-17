using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DTech.AddressableManagement
{
    /// <summary>
    /// Entry point to Addressables content: initialization, catalog updates, download sizes, downloads, asset and
    /// scene loads. Build one per application from the composition root and call it from the main thread. Every
    /// operation waits for initialization and for a running catalog update by itself.
    /// </summary>
    public interface IAssetService : IDisposable
    {
        /// <summary>
        /// Raised for content failures that retries and CDN fallbacks could not recover from.
        /// </summary>
        event Action<ContentFailure> OnFailed;

        /// <summary>
        /// Whether Addressables finished initializing.
        /// </summary>
        bool IsInitialized { get; }

        /// <summary>
        /// Outcome of the latest catalog update.
        /// </summary>
        CatalogState CatalogState { get; }

        /// <summary>
        /// The CDN route remote requests currently take.
        /// </summary>
        IContentRoute Route { get; }

        /// <summary>
        /// Whether at least one download suspension is active.
        /// </summary>
        bool IsDownloadSuspended { get; }

        /// <summary>
        /// Logs routine progress. Warnings and errors are always logged.
        /// </summary>
        bool EnableLogs { get; set; }

        /// <summary>
        /// Initializes Addressables and the CDN route. Concurrent calls share one initialization; a failed one is
        /// retried by the next call.
        /// </summary>
        /// <param name="token">Cancels the wait only, not the shared initialization.</param>
        /// <returns>A task that completes when Addressables is ready.</returns>
        Task InitializeAsync(CancellationToken token = default);

        /// <summary>
        /// Checks for newer remote catalogs and applies them, moving along the CDN route on failures. Concurrent
        /// calls share one update.
        /// </summary>
        /// <param name="token">Cancels the wait only, not the shared update.</param>
        /// <returns>The outcome of the update.</returns>
        Task<CatalogState> UpdateCatalogsAsync(CancellationToken token = default);

        /// <summary>
		/// Measures how many bytes the bundles behind the key still need to download. Throws ArgumentException for a
		/// key that points at nothing.
        /// </summary>
        /// <param name="key">Any asset, label or scene key.</param>
        /// <param name="token">Cancels the query.</param>
        /// <returns>The number of bytes; 0 when everything is cached or local.</returns>
        Task<long> GetDownloadSizeAsync(AssetKey key, CancellationToken token = default);

        /// <summary>
        /// Measures how many bytes the bundles behind the keys still need to download. Shared bundles count once.
        /// </summary>
        /// <param name="keys">Any asset, label or scene keys.</param>
        /// <param name="token">Cancels the query.</param>
        /// <returns>The number of bytes; 0 when everything is cached or local.</returns>
        Task<long> GetDownloadSizeAsync(IReadOnlyList<AssetKey> keys, CancellationToken token = default);

        /// <summary>
        /// Measures how many bytes all content of the loaded catalogs still needs to download.
		/// Throws ArgumentException for a key that points at nothing.
        /// </summary>
        /// <param name="token">Cancels the query.</param>
        /// <returns>The number of bytes; 0 when everything is cached or local.</returns>
        Task<long> GetRemoteContentSizeAsync(CancellationToken token = default);

        /// <summary>
		/// Downloads the bundles behind the keys without loading them. Throws ArgumentException for a key that points
		/// at nothing.
        /// </summary>
        /// <param name="keys">Any asset, label or scene keys.</param>
        /// <param name="options">Retries and whether catalogs are updated first.</param>
        /// <param name="token">Cancels the download.</param>
        /// <returns>The running download.</returns>
        IAssetDownloadResponse DownloadAssets(IReadOnlyList<AssetKey> keys, DownloadOptions options,
                CancellationToken token = default);

        /// <summary>
        /// Downloads the bundles of all content of the loaded catalogs without loading them.
        /// </summary>
        /// <param name="options">Retries and whether catalogs are updated first.</param>
        /// <param name="token">Cancels the download.</param>
        /// <returns>The running download.</returns>
        IAssetDownloadResponse DownloadRemoteContent(DownloadOptions options, CancellationToken token = default);

        /// <summary>
        /// Holds back downloads that have not started an attempt yet until the returned scope is disposed.
        /// Running attempts and loads are not affected.
        /// </summary>
        /// <returns>The suspension scope; dispose it to resume.</returns>
        IDisposable SuspendDownloads();

        /// <summary>
        /// Loads an asset. Each call returns its own handler; one Addressables load serves every handler of the same
		/// asset, and the asset is unloaded once all of them are released. A key that points at nothing gives a failed
		/// handler.
        /// </summary>
        /// <param name="key">The asset key.</param>
        /// <param name="lifetime">Releases the handler when cancelled.</param>
        /// <returns>The handler of the load.</returns>
        IAssetHandler<TAsset> LoadAsset<TAsset>(AssetKeyT<TAsset> key, CancellationToken lifetime = default)
                where TAsset : UnityEngine.Object;

        /// <summary>
		/// Loads every asset of the label that loads as TAsset. A label without such assets gives an empty list; an
		/// empty label gives a failed handler.
        /// </summary>
        /// <param name="key">The label key.</param>
        /// <param name="lifetime">Releases the handler when cancelled.</param>
        /// <returns>The handler of the batch; its result lists the loaded assets.</returns>
        IAssetHandler<IReadOnlyList<TAsset>> LoadAssets<TAsset>(LabelKey key, CancellationToken lifetime = default)
                where TAsset : UnityEngine.Object;

        /// <summary>
        /// Loads an addressable scene. A key that points at nothing gives a failed handler.
        /// </summary>
        /// <param name="key">The scene key.</param>
        /// <param name="options">Load mode, deferred activation and priority.</param>
        /// <param name="lifetime">Unloads the scene when cancelled.</param>
        /// <returns>The handler of the scene.</returns>
        ISceneHandler LoadScene(SceneKey key, SceneLoadOptions options, CancellationToken lifetime = default);

        /// <summary>
        /// Releases every handler loaded with the key, whatever its type, and unloads the content.
        /// Loads still resolving when this is called are not affected.
        /// </summary>
        /// <param name="key">The key to unload.</param>
        void UnloadAsset(AssetKey key);
    }
}