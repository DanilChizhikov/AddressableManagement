# Addressable Management
![](https://img.shields.io/badge/unity-6000.0+-000.svg)
![Unity Tests](https://github.com/DanilChizhikov/AddressableManagement/actions/workflows/tests.yml/badge.svg?branch=master)

## Description
A service layer over Unity Addressables. Keys are plain serializable data, one `IAssetService` built by your
composition root loads assets and scenes, measures and downloads bundles, and routes every remote request through
ordered CDN mirrors and content roots with automatic fallback. There is no singleton and no synchronous API.

## Table of Contents
- [Getting Started](#getting-started)
    - [Install via UPM (using Git URL)](#install-via-upm-using-git-url)
- [Features](#features)
- [Basic Usage](#basic-usage)
    - [Creating the Service](#creating-the-service)
    - [Keys](#keys)
    - [Initialization and Catalogs](#initialization-and-catalogs)
    - [Loading Assets](#loading-assets)
    - [Loading Scenes](#loading-scenes)
    - [Download Sizes and Downloads](#download-sizes-and-downloads)
    - [CDN Routing](#cdn-routing)
    - [Failures](#failures)
    - [Editor Tools](#editor-tools)
- [Settings Reference](#settings-reference)
- [Notes](#notes)
- [Migrating from 0.0.x](#migrating-from-00x)
- [License](#license)

## Getting Started
Prerequisites:
- [GIT](https://git-scm.com/downloads)
- [Unity](https://unity.com/releases/editor/archive) 6000.0+
- [Addressables](https://docs.unity3d.com/Packages/com.unity.addressables@2.9/manual/index.html) 2.9.1+

### Install via UPM (using Git URL)
1. Open the manifest.json file in your project's Packages folder.
2. Add the following line to the dependencies section:
   ```json
   "com.dtech.addressable-management": "https://github.com/DanilChizhikov/AddressableManagement.git",
   ```
3. Unity will automatically import the package.

The package uses `v*.*.*` release tags, so you can pin a version, for example
`https://github.com/DanilChizhikov/AddressableManagement.git#v1.0.0`.

## Features
- 🔑 **Data-only keys**: `ReferenceKey<T>`, `AddressKey<T>`, `ComponentKey<T>`, `LabelKey` and `SceneKey` hold data and nothing else
- 🧩 **One Addressables load per asset**: every caller gets its own handler, the package keeps the reference count and releases the Addressables handle exactly once, after the last handler
- 🎬 **Scenes**: single or additive loads, deferred activation, unload through the handler
- 📦 **Downloads**: asynchronous size queries, downloads with byte progress, retries, suspension and verification
- 🌐 **CDN routing**: ordered content roots (404 moves on) and mirrors (network errors, timeouts and 5xx move on), optional mirror probe, runtime replacement from remote configuration
- 🧯 **Typed failures**: one `OnFailed` event with the stage, the kind and the exception, after retries and fallbacks are exhausted
- 🛠 **Editor tools**: CDN roots from Addressables profiles, a dependency check between local and remote groups, shader rebinding for bundles in the Editor

## Basic Usage

### Creating the Service
Build one service per application in your composition root, keep it for the whole session and dispose it on shutdown.
The package never creates a service by itself.

```csharp
using DTech.AddressableManagement;
using UnityEngine;

public sealed class GameBootstrap : MonoBehaviour
{
    [SerializeField] private AssetServiceSettings _assetSettings = new AssetServiceSettings();

    private AssetService _assets;

    private async void Start()
    {
        _assets = new AssetService(_assetSettings);
        _assets.OnFailed += failure => Debug.LogWarning(failure);

        await _assets.InitializeAsync(destroyCancellationToken);
        await _assets.UpdateCatalogsAsync(destroyCancellationToken);

        // Hand IAssetService to the rest of the game, for example through your DI container.
    }

    private void OnDestroy()
    {
        _assets?.Dispose();
    }
}
```

`AssetServiceSettings` is a serializable class: keep it in a bootstrap component, a ScriptableObject or a DI installer.
Call the service from the main thread only.

### Keys
| Key | Points at | Inspector |
|---|---|---|
| `ReferenceKey<T>` | An asset or sub-asset (for example a sprite inside a texture) by GUID | Addressables asset picker |
| `AddressKey<T>` | An asset by address | Text field |
| `ComponentKey<T>` | A prefab that carries the component `T`; loads return the component | Picker that only offers such prefabs |
| `LabelKey` | Every asset with an Addressables label | Label dropdown |
| `SceneKey` | An addressable scene | Picker that only offers scenes |

```csharp
[SerializeField] private ReferenceKey<Sprite> _icon;
[SerializeField] private ComponentKey<EnemyView> _enemy;
[SerializeField] private LabelKey _levelPack;
[SerializeField] private SceneKey _arena;

var bossKey = new AddressKey<GameObject>("Enemies/Boss");
```

Keys only hold data. Every operation goes through `IAssetService`.

### Initialization and Catalogs
`InitializeAsync` initializes Addressables and the CDN route. `UpdateCatalogsAsync` checks for newer remote catalogs,
applies them and returns a `CatalogState`. Concurrent calls of either method share one operation, and the token you
pass cancels only your wait. Every other operation waits for initialization and for a running catalog update by itself.

Turn on **Only update catalogs manually** in the Addressables settings, so catalog updates go through the service and
its CDN fallback instead of running inside Addressables initialization.

### Loading Assets
```csharp
IAssetHandler<GameObject> handler = _assets.LoadAsset(new AddressKey<GameObject>("Enemies/Boss"), destroyCancellationToken);
GameObject prefab = await handler.WaitForCompletionAsync();
if (handler.IsFailed)
{
    Debug.LogWarning(handler.Exception);
    return;
}

Instantiate(prefab);

// Later, when the prefab is no longer needed:
handler.Release();
```

- Every `LoadAsset` call returns its own handler. Handlers of the same asset share one Addressables load, even when
  they come from different keys (GUID and address).
- The asset is unloaded after its last handler is released. Release a handler once you are done; `Release` is safe to
  call more than once.
- The optional `lifetime` token (for example `destroyCancellationToken`) releases the handler when it is cancelled.
  `AttachCancellationToken` adds more tokens later.
- `Result` throws `ObjectDisposedException` after `Release`. In the Editor and development builds the message shows
  where the handler was created and released.
- A key that points at nothing gives a failed handler instead of an exception.
- `ComponentKey<T>` and component types in general load the prefab and return its component, or fail with
  `FailureKind.TypeMismatch`.
- `LoadAssets<T>(LabelKey)` loads every asset of a label into one handler with an `IReadOnlyList<T>` result.
- `UnloadAsset(key)` releases every handler of that key and unloads the content.
- `Dispose` releases handlers that were never released and logs a warning that lists them.

### Loading Scenes
```csharp
ISceneHandler arena = _assets.LoadScene(_arena, new SceneLoadOptions(LoadSceneMode.Additive, deferActivation: true, priority: 100));
await arena.WaitForCompletionAsync();
await arena.ActivateAsync();

// Later:
await arena.UnloadAsync();
```

`default(SceneLoadOptions)` replaces the open scenes and activates the new one at once. `Release` unloads the scene;
a scene loaded with deferred activation is activated first, because Unity cannot unload a scene that never activated.

### Download Sizes and Downloads
```csharp
var pack = new AssetKey[] { _levelPack, _arena };
long bytes = await _assets.GetDownloadSizeAsync(pack);
if (bytes > 0)
{
    IAssetDownloadResponse download = _assets.DownloadAssets(pack, new DownloadOptions(RetryPolicy.Default, updateCatalogsFirst: false));
    download.OnStateChanged += response => Debug.Log($"{response.State}: {response.Progress:P0}");
    DownloadResult result = await download.WaitForCompletionAsync();
}
```

- Size queries are asynchronous only. Keys measured at zero bytes are remembered until catalogs change.
- `GetRemoteContentSizeAsync` and `DownloadRemoteContent` cover all content of the loaded catalogs.
- A download reports `State`, `Progress`, `TotalBytes`, `DownloadedBytes` and `Attempt`, and ends with a
  `DownloadResult` (`Downloaded`, `AlreadyCached`, `Failed` or `Cancelled`).
- After an attempt the size is measured again; missing bytes count as a failed attempt.
- `RetryPolicy` sets the number of attempts and the real-time delay between them. `RetryPolicy.Unlimited(delay)` keeps
  trying, for example to wait out a lost connection. Moving to another CDN root or mirror does not use an attempt.
  Failures that cannot recover (unknown key, 4xx) are not retried.
- `SuspendDownloads()` returns a scope; downloads that have not started an attempt wait until every scope is disposed.
- Size queries and downloads throw `ArgumentException` for keys that point at nothing.
- Any progress or consent UI is yours to build on top of `IAssetDownloadResponse`.

### CDN Routing
`ContentDeliverySettings` rewrites every http(s) request Addressables sends:

```
request:  https://origin.example.com/game/Android/catalog_1.0.hash
roots:    https://cdn.example.com/game/{Platform}/live, https://cdn.example.com/game/{Platform}/previous
mirrors:  https://cdn.example.com, https://cdn-backup.example.net

result:   https://cdn.example.com/game/Android/live/catalog_1.0.hash
```

- The file name is appended to the active content root, then the origin is replaced by the active mirror. Both lists
  are optional: without roots the path from the catalog stays, without mirrors the origin stays.
- A 404 moves to the next content root. A connection failure, timeout, 5xx or corrupted data moves to the next mirror.
  Nothing moves while the device is offline.
- The route stays where it moved for the session. `Route.Reset()` goes back to the first entries; `Route.Apply(settings)`
  replaces them, for example with values from remote configuration. `Route.OnChanged` reports every move.
- With `ProbePath` set and two or more mirrors, initialization requests `<mirror>/<ProbePath>` from each mirror until
  one answers with 2xx.
- Entries may use `{Platform}` (the Addressables platform folder, such as `Android` or `iOS`) and Addressables runtime
  tokens such as `{UnityEngine.Application.version}`.
- Content roots assume that the remote catalog and all bundles sit in one folder, as Addressables builds them. Catalogs
  loaded from other folders should rely on mirrors only.
- The service rewrites requests through `Addressables.WebRequestOverride`, so the operation cache keys of bundles stay
  the same and moving to another mirror never loads an already loaded bundle twice.

### Failures
`OnFailed` reports failures that retries and fallbacks could not recover from. `ContentFailure` carries the `Stage`
(`Initialization`, `CatalogUpdate`, `Download`, `Load`, `Scene`), the `Kind` (`NotFound`, `ClientError`, `ServerError`,
`Network`, `Corrupted`, `InvalidKey`, `TypeMismatch`, `Unknown`), the key and the exception. Each distinct failure is
also logged once as an error. Moves along the CDN route are logged as warnings and do not raise `OnFailed`.

### Editor Tools
- **Add Root From Addressables Profile** under `ContentDeliverySettings` in the inspector takes a profile's
  `Remote.LoadPath`, turns `[BuildTarget]` and the application version into runtime tokens and adds it as a content root.
- **Analyze → Check Local Assets Depending On Remote Assets (DTech)** and **Tools → DTech → Addressable Management →
  Check Local To Remote Dependencies** list local assets that depend on assets in remote groups and therefore cannot
  load offline.
- With `RebindShadersInEditor` on, materials loaded from bundles in the Editor get the Editor's own shaders, so content
  built for a mobile target does not render pink in Use Existing Build mode. Project assets are never touched.
- **Update window**: for a package installed from git or a registry, the Editor checks for a newer version once per
  session and shows the release notes of every version after the installed one. **Skip Version** hides the window until
  a newer release, **Skip Always** hides it for good, **Tools → DTech → Addressable Management → Check Update** opens it
  on demand.

## Settings Reference
| Setting | Default | Meaning |
|---|---|---|
| `Delivery` | empty | CDN content roots, mirrors and probe |
| `LoadRetry` | 3 attempts, 3 s | How asset and scene loads retry when their bundles fail to download |
| `ResetCatalogCacheOnVersionChange` | off | Deletes cached remote catalogs once after `Application.version` changes; cached bundles stay |
| `CleanBundleCacheAfterCatalogUpdate` | off | Removes cached bundles no loaded catalog references after an update; skipped in the Editor |
| `RebindShadersInEditor` | on | Rebinds shaders of materials loaded from bundles in the Editor |
| `EnableLogs` | off | Logs routine progress such as rewritten requests |

## Notes
- Build one service per application. The request rewriter is global to Addressables; a second service logs an error.
  Requests Addressables sends before `InitializeAsync` runs are not rewritten.
- Call the service from the main thread. Continuations never run inside the Addressables update callback.
- Synchronous loading (`WaitForCompletion`) is not offered on purpose.

## Migrating from 0.0.x
- Package `com.danilchizhikov.addressablemanagement` became `com.dtech.addressable-management`; the namespace is
  `DTech.AddressableManagement`.
- String keys and `AssetReference` parameters are replaced by the key types above.
- `GetDownloadSizeAsync` and `DownloadAssets` take keys; use `GetRemoteContentSizeAsync` and `DownloadRemoteContent`
  for everything. Download progress is reported in bytes.
- `UnloadAsset(Object)` is gone: release the handler, or call `UnloadAsset(key)`.

## License
MIT