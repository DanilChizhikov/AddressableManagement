# AddressableManagement
![](https://img.shields.io/badge/unity-2022.3+-000.svg)

## Description
This package shows a possible implementation of working with Addressables Unity.

## Table of Contents
- [Getting Started](#Getting-Started)
    - [Install manually (using .unitypackage)](#Install-manually-(using-.unitypackage))
    - [Install via UPM (using Git URL)](#Install-via-UPM-(using-Git-URL))
- [Basic Usage](#Basic-Usage)
    - [Runtime Code](#Runtime-Code)
    - [Remote Assets](#Remote-Assets)
- [License](#License)

## Getting Started
Prerequisites:
- [GIT](https://git-scm.com/downloads)
- [Unity](https://unity.com/releases/editor/archive) 2022.3+
- [Addressables](https://docs.unity3d.com/Manual/com.unity.addressables.html) 1.21.19+

### Install manually (using .unitypackage)
1. Download the .unitypackage from [releases](https://github.com/DanilChizhikov/Localization/releases/) page.
2. Open AddressableManagement.x.x.x.unitypackage

### Install via UPM (using Git URL)
1. Navigate to your project's Packages folder and open the manifest.json file.
2. Add this line below the "dependencies": { line
    - ```json title="Packages/manifest.json"
      "com.danilchizhikov.addressablemanagement": "https://github.com/DanilChizhikov/AddressableManagement.git?path=Assets/AddressableManagement#0.0.2",
      ```
UPM should now install the package.

## Basic Usage

### Runtime Code
First, you need to initialize the AssetService, this can be done using different methods.
Here we will show the easiest way, which is not the method that we recommend using!
```csharp
public class AssetServiceBootstrap : MonoBehaviour
{
    private static IAssetService _service;

    public static IAssetService Service => _service;

    private void Awake()
    {
        if (_service != null)
        {
            Destroy(gameObject);
            return;
        }

        _service = new AssetService();
    }
}
```

IAssetService allows you to upload assets via their keys or when using AssetRefernce.

For Example:
```csharp
public sealed class ExampleUsage : MonoBehaviour
{
    [SerializeField] private AssetReference _reference = default;
    [SerializeField] private string _assetKey = string.Empty;
    
    private IAssetService _assetService;

    private GameObject _keyOrigin;
    private GameObject _referenceOrigin;

    [Inject]
    public void Construct(IAssetService assetService)
    {
        _assetService = assetService;
    }

    public async UniTask LoadAssetByKeyAsync()
    {
        IAssetResponse<GameObject> response = _assetService.LoadAsset<GameObject>(_assetKey);
        await UniTask.WaitUntil(() => response.IsDone);
        _keyOrigin = response.Result;
    }
    
    public async UniTask LoadAssetByReferenceAsync()
    {
        IAssetResponse<GameObject> response = _assetService.LoadAsset<GameObject>(_reference);
        await UniTask.WaitUntil(() => response.IsDone);
        _referenceOrigin = response.Result;
    }

    private void OnDestroy()
    {
        if (_keyOrigin != null)
        {
            _assetService.UnloadAsset(_keyOrigin);
        }

        if (_referenceOrigin != null)
        {
            _assetService.UnloadAsset(_referenceOrigin);
        }
    }
}
```

IAssetResponse:
```csharp
using UnityEngine;

namespace MbsCore.AddressableManagement.Infrastructure
{
    public interface IAssetResponse
    {
        float Progress { get; }
        bool IsDone { get; }
    }
    
    public interface IAssetResponse<TResult> : IAssetResponse where TResult : Object
    {
        TResult Result { get; }
    }
}
```

### Remote Assets

To download assets remotely, you can use the following methods.
```csharp
namespace MbsCore.AddressableManagement.Infrastructure
{
    public interface IAssetService
    {
        //Returns the size of the bytes required for downloading.
        Task<long> GetDownloadSizeAsync();
        //Starts downloading assets and returns a user-friendly interface for tracking progress.
        IAssetDownloadResponse DownloadAssets();
    }
}
```

IAssetDownloadResponse:
```csharp
namespace MbsCore.AddressableManagement.Infrastructure
{
    public interface IAssetDownloadResponse
    {
        float Progress { get; }
        float DownloadMegabytes { get; }
        float DownloadedMegabytes { get; }
        bool IsDone { get; }
    }
}
```

## License

MIT