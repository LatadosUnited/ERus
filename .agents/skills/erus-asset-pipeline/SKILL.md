---
name: erus-asset-pipeline
description: Use this skill when you need to load new asset types from disk or synchronize new types of files over the ERus Network via TCP.
---

# ERus Asset Pipeline

The ERus engine loads local assets from disk, but in a multiplayer session, it must also synchronize these assets (3D models, textures, scenes) via TCP.

## 1. Asset Sync Architecture
- **AssetSyncManager**: Responsible for orchestrating asset downloads.
- **AssetTcpServer**: The Host runs this to serve files to clients.
- **AssetTcpClient**: The Client uses this to request and download missing files.

## 2. Announcing New Assets
When a Client attempts to load an asset (for example, `LoadScene("MyScene.scene")` or `MeshComponent.SourceFile = "model.obj"`), the client MUST first check if the asset exists locally.
If it doesn't exist, the client asks the host for it.
However, to optimize the process, the Engine usually announces assets when they are instantiated.

```csharp
// Example of Announcing an Asset to connected peers:
var netModule = engine.GetModule<NetworkModule>();
if (netModule != null)
{
    // Tells peers we are about to use this asset, giving them time to download it
    _ = netModule.NetworkManager.AssetSync.AnnounceAssetAsync(absolutePath);
}
```

## 3. Dealing with Pending Assets (Placeholders)
Downloading assets takes time. During this time, the Engine should render a Placeholder (like a transparent cube or a loading icon).

```csharp
if (!File.Exists(absolutePath))
{
    // File doesn't exist! We need to download it.
    // 1. Assign a Placeholder visually.
    // 2. Request the download.
    var netModule = _engine.GetModule<NetworkModule>();
    _ = netModule.NetworkManager.AssetSync.RequestAssetAsync(absolutePath, (downloadedPath) => 
    {
        // Callback when download finishes!
        // Swap the placeholder for the real asset here.
    });
}
```

## 4. Paths
Always use normalized paths or relative paths inside the `Assets/` folder to prevent path mismatch between Windows/Linux or different users' machines.
