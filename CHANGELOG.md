# Changelog

## [1.0.0] - 2026-10-17

A rebuild of the package around one `IAssetService` without a singleton, data-only keys and CDN routing.

### Breaking
- Package renamed to `com.dtech.addressable-management`, namespace `DTech.AddressableManagement`. Requires Unity 6000.0 and Addressables 2.9.1.
- String and `AssetReference` parameters replaced by data-only keys: `ReferenceKey<T>`, `AddressKey<T>`, `ComponentKey<T>`, `LabelKey`, `SceneKey`.
- `IAssetService` reworked: `LoadAsset<T>(AssetKey<T>)` returns a handler per call, `UnloadAsset(AssetKey)` replaces `UnloadAsset(Object)`, downloads take keys and report progress in bytes.

### Added
- One Addressables load per asset with the package's own reference count; GUID, address and label keys of an asset share it, and the Addressables handle is released exactly once after the last handler.
- `LoadAssets<T>(LabelKey)`, `LoadScene(SceneKey, SceneLoadOptions)` with deferred activation, lifetime tokens for handlers.
- Asynchronous download sizes and downloads with byte progress, `RetryPolicy`, verification of every attempt, `SuspendDownloads()` and downloads of all remote content.
- CDN routing through `ContentDeliverySettings`: ordered content roots (404 moves on), ordered mirrors (network errors, timeouts, 5xx and corrupted data move on), optional mirror probe, `{Platform}` and Addressables runtime tokens, runtime replacement through `IContentRoute.Apply`.
- Single-flight `InitializeAsync` and `UpdateCatalogsAsync`; every operation waits for initialization and running catalog updates.
- `OnFailed` with `ContentFailure` (stage, kind, key, exception); failures are classified from the web request result, not from exception text.
- Optional reset of cached remote catalogs after an application version change and optional bundle cache cleanup after catalog updates.
- Editor: CDN roots from Addressables profiles, key drawer, Analyze rule and menu item for local assets depending on remote ones, shader rebinding for bundles in the Editor, update window with release notes.
- Leak report on `Dispose` and creation and release call sites for released handlers in the Editor and development builds.

### Fixed
- Addressables was called from thread pool threads.
- Download progress divided the total by the downloaded amount.
- The download size was measured key by key over every key of every locator; it is now one query.
- A cached handler was shared by all callers, so one `Release` unloaded the asset for everyone, and a request with another type returned null.

### Removed
- `IAssetResponse`, the `Infrastructure` assembly, string key overloads and synchronous size checks.