using System;
using System.IO;
using ERus.Engine.Assets;
using Xunit;

namespace ERus.Tests;

public class AssetDatabaseTests : IDisposable
{
    private readonly string _testDir;

    public AssetDatabaseTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "ERus_AssetDatabaseTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testDir);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testDir))
            {
                Directory.Delete(_testDir, true);
            }
        }
        catch
        {
            // Best effort cleanup
        }
    }

    [Fact]
    public void AssetDatabase_Scan_GeneratesMetaFilesWithValidGuidAndHash()
    {
        var db = new AssetDatabase(_testDir);
        string sampleAsset = Path.Combine(_testDir, "test_texture.png");
        File.WriteAllBytes(sampleAsset, new byte[] { 1, 2, 3, 4, 5 });

        db.Scan();

        string metaPath = sampleAsset + ".meta";
        Assert.True(File.Exists(metaPath), "O arquivo .meta deveria ter sido gerado.");

        var guid = db.GetGuidByPath(sampleAsset);
        Assert.NotNull(guid);
        Assert.NotEqual(Guid.Empty, guid.Value);

        string? retrievedPath = db.GetPathByGuid(guid.Value);
        Assert.NotNull(retrievedPath);
        Assert.Equal(Path.GetFullPath(sampleAsset), Path.GetFullPath(retrievedPath));
    }

    [Fact]
    public void AssetDatabase_PreservesExistingGuid_OnSubsequentScan()
    {
        var db = new AssetDatabase(_testDir);
        string sampleAsset = Path.Combine(_testDir, "model.obj");
        File.WriteAllText(sampleAsset, "v 0 0 0\nv 1 1 1");

        db.Scan();
        var initialGuid = db.GetGuidByPath(sampleAsset);
        Assert.NotNull(initialGuid);

        // Segundo scan
        db.Scan();
        var secondaryGuid = db.GetGuidByPath(sampleAsset);
        Assert.Equal(initialGuid, secondaryGuid);
    }

    [Fact]
    public void AssetDatabase_UpdatesHash_WhenFileIsModified()
    {
        var db = new AssetDatabase(_testDir);
        string sampleAsset = Path.Combine(_testDir, "data.json");
        File.WriteAllText(sampleAsset, "{\"version\": 1}");

        db.Scan();
        var guid = db.GetGuidByPath(sampleAsset);
        Assert.NotNull(guid);

        // Modifica arquivo com novo timestamp e conteúdo
        File.WriteAllText(sampleAsset, "{\"version\": 2, \"updated\": true}");
        File.SetLastWriteTimeUtc(sampleAsset, DateTime.UtcNow.AddSeconds(5));

        db.Scan();
        var newGuid = db.GetGuidByPath(sampleAsset);
        Assert.Equal(guid, newGuid); // GUID deve permanecer o mesmo
    }

    [Fact]
    public void AssetDatabase_GetPathByHash_ResolvesCorrectFile()
    {
        var db = new AssetDatabase(_testDir);
        string sampleAsset = Path.Combine(_testDir, "sprite.png");
        byte[] content = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
        File.WriteAllBytes(sampleAsset, content);

        db.Scan();

        var guid = db.GetGuidByPath(sampleAsset);
        Assert.NotNull(guid);

        string metaContent = File.ReadAllText(sampleAsset + ".meta");
        var meta = System.Text.Json.JsonSerializer.Deserialize<AssetMeta>(metaContent);
        Assert.NotNull(meta);
        Assert.False(string.IsNullOrEmpty(meta.Hash));

        string? resolvedPath = db.GetPathByHash(meta.Hash);
        Assert.NotNull(resolvedPath);
        Assert.Equal(Path.GetFullPath(sampleAsset), Path.GetFullPath(resolvedPath));
    }
}
