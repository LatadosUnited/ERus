using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Silk.NET.Maths;

namespace ERus.Engine.ECS;

public class SerializedScript
{
    public string ScriptType { get; set; } = "";
    public Dictionary<string, string> ScriptFields { get; set; } = new Dictionary<string, string>();
}

public class SerializedEntity
{
    public int NetworkId { get; set; } = -1;
    public string Tag { get; set; } = "Entity";
    
    // Transform
    public float PosX { get; set; }
    public float PosY { get; set; }
    public float PosZ { get; set; }
    public float RotX { get; set; }
    public float RotY { get; set; }
    public float RotZ { get; set; }
    public float ScaX { get; set; } = 1;
    public float ScaY { get; set; } = 1;
    public float ScaZ { get; set; } = 1;

    // Mesh
    public int MeshType { get; set; } = -1;
    public string? AssetPath { get; set; } // Obsoleto: Mantido para retrocompatibilidade
    public Guid AssetGuid { get; set; }

    // Camera
    public bool HasCamera { get; set; } = false;
    public float CamFov { get; set; } = 45f;
    public bool CamIsPrimary { get; set; } = true;
    public float CamNear { get; set; } = 0.1f;
    public float CamFar { get; set; } = 1000f;

    // Scripts
    public List<SerializedScript> Scripts { get; set; } = new List<SerializedScript>();
}

public class SceneData
{
    public List<SerializedEntity> Entities { get; set; } = new List<SerializedEntity>();
}

public static class SceneSerializer
{
    public static void SaveScene(string filepath, Scene scene)
    {
        var registry = scene.Registry;
        var sceneData = new SceneData();

        foreach (var entity in registry.GetLivingEntities())
        {
            var sEntity = new SerializedEntity();
            
            if (registry.HasComponent<NetworkIdentityComponent>(entity))
            {
                sEntity.NetworkId = registry.GetComponent<NetworkIdentityComponent>(entity).NetworkId;
            }

            if (registry.HasComponent<TagComponent>(entity))
            {
                sEntity.Tag = registry.GetComponent<TagComponent>(entity).Name;
            }

            if (registry.HasComponent<TransformComponent>(entity))
            {
                var t = registry.GetComponent<TransformComponent>(entity);
                sEntity.PosX = t.Position.X; sEntity.PosY = t.Position.Y; sEntity.PosZ = t.Position.Z;
                sEntity.RotX = t.Rotation.X; sEntity.RotY = t.Rotation.Y; sEntity.RotZ = t.Rotation.Z;
                sEntity.ScaX = t.Scale.X;    sEntity.ScaY = t.Scale.Y;    sEntity.ScaZ = t.Scale.Z;
            }

            if (registry.HasComponent<MeshComponent>(entity))
            {
                var mesh = registry.GetComponent<MeshComponent>(entity);
                sEntity.MeshType = (int)mesh.Type;
                sEntity.AssetGuid = mesh.AssetGuid;
            }

            if (registry.HasComponent<CameraComponent>(entity))
            {
                var cam = registry.GetComponent<CameraComponent>(entity);
                sEntity.HasCamera = true;
                sEntity.CamFov = cam.FieldOfView;
                sEntity.CamIsPrimary = cam.IsPrimary;
                sEntity.CamNear = cam.NearClip;
                sEntity.CamFar = cam.FarClip;
            }

            if (registry.HasComponent<ScriptComponent>(entity))
            {
                var scriptComp = registry.GetComponent<ScriptComponent>(entity);
                foreach (var scriptData in scriptComp.Scripts)
                {
                    var serializedScript = new SerializedScript { ScriptType = scriptData.ScriptTypeName ?? "" };
                    foreach(var kvp in scriptData.FieldValues)
                    {
                        serializedScript.ScriptFields[kvp.Key] = kvp.Value;
                    }
                    sEntity.Scripts.Add(serializedScript);
                }
            }

            sceneData.Entities.Add(sEntity);
        }

        var options = new JsonSerializerOptions { WriteIndented = true };
        string json = JsonSerializer.Serialize(sceneData, options);
        File.WriteAllText(filepath, json);
        Console.WriteLine($"[SceneSerializer] Cena salva em {filepath}");
    }

    public static void LoadScene(string filepath, Scene scene)
    {
        var registry = scene.Registry;
        if (!File.Exists(filepath)) return;

        string json = File.ReadAllText(filepath);
        SceneData? sceneData = null;
        try
        {
            sceneData = JsonSerializer.Deserialize<SceneData>(json);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SceneSerializer] Erro ao ler cena {filepath}: {ex.Message}");
            return;
        }
        
        if (sceneData == null) return;

        registry.Clear();

        foreach (var sEntity in sceneData.Entities)
        {
            var entity = registry.CreateEntity();

            if (sEntity.NetworkId != -1)
            {
                registry.AddComponent(entity, new NetworkIdentityComponent { NetworkId = sEntity.NetworkId, LockUserId = -1 });
            }

            registry.AddComponent(entity, new TagComponent { Name = sEntity.Tag });

            var t = new TransformComponent
            {
                Position = new Vector3D<float>(sEntity.PosX, sEntity.PosY, sEntity.PosZ),
                Rotation = new Vector3D<float>(sEntity.RotX, sEntity.RotY, sEntity.RotZ),
                Scale = new Vector3D<float>(sEntity.ScaX, sEntity.ScaY, sEntity.ScaZ)
            };
            registry.AddComponent(entity, t);

            if (sEntity.MeshType != -1 || !string.IsNullOrEmpty(sEntity.AssetPath) || sEntity.AssetGuid != Guid.Empty)
            {
                Guid guidToUse = sEntity.AssetGuid;
                if (guidToUse == Guid.Empty && !string.IsNullOrEmpty(sEntity.AssetPath))
                {
                    // Retrocompatibilidade: Converte AssetPath para Guid
                    var foundGuid = ERus.Engine.Core.Engine.Instance.AssetDatabase.GetGuidByPath(sEntity.AssetPath);
                    if (foundGuid.HasValue) guidToUse = foundGuid.Value;
                }

                registry.AddComponent(entity, new MeshComponent { 
                    Type = sEntity.MeshType != -1 ? (PrimitiveMeshType)sEntity.MeshType : PrimitiveMeshType.None,
                    AssetGuid = guidToUse
                });
            }

            if (sEntity.HasCamera)
            {
                registry.AddComponent(entity, new CameraComponent 
                { 
                    FieldOfView = sEntity.CamFov,
                    IsPrimary = sEntity.CamIsPrimary,
                    NearClip = sEntity.CamNear,
                    FarClip = sEntity.CamFar
                });
            }

            if (sEntity.Scripts != null && sEntity.Scripts.Count > 0)
            {
                var scriptComp = new ScriptComponent();
                foreach (var sScript in sEntity.Scripts)
                {
                    if (string.IsNullOrEmpty(sScript.ScriptType)) continue;

                    var scriptData = new ScriptData { ScriptTypeName = sScript.ScriptType };
                    foreach(var kvp in sScript.ScriptFields)
                    {
                        scriptData.FieldValues[kvp.Key] = kvp.Value;
                    }
                    scriptComp.Scripts.Add(scriptData);
                }
                if (scriptComp.Scripts.Count > 0)
                {
                    registry.AddComponent(entity, scriptComp);
                }
            }
        }

        Console.WriteLine($"[SceneSerializer] Cena carregada de {filepath} ({sceneData.Entities.Count} entidades)");
    }
}
