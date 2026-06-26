using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Silk.NET.Maths;
using ERus.Engine.Scripting;

namespace ERus.Engine.ECS;

// ============================================================================
// NOVO FORMATO DE SERIALIZAÇÃO (V2) - Baseado em componentes genéricos
// ============================================================================

/// <summary>
/// Representação serializada de uma entidade com componentes genéricos.
/// Em vez de propriedades flat (HasBoxCollider, BoxSizeX...), cada componente 
/// é serializado como um JsonElement dentro do dicionário Components.
/// </summary>
public class SerializedEntityV2
{
    public int ParentIndex { get; set; } = -1;
    public Dictionary<string, JsonElement> Components { get; set; } = new();
}

public class SceneDataV2
{
    public int Version { get; set; } = 2;
    public List<SerializedEntityV2> Entities { get; set; } = new();
}

// ============================================================================
// FORMATO LEGADO (V1) - Mantido APENAS para leitura retrocompatível
// ============================================================================

public class SerializedScript
{
    public string ScriptType { get; set; } = "";
    public Dictionary<string, string> ScriptFields { get; set; } = new Dictionary<string, string>();
}

public class SerializedEntity
{
    public int ParentIndex { get; set; } = -1;
    public int NetworkId { get; set; } = -1;
    public string Tag { get; set; } = "Entity";
    public float PosX { get; set; } public float PosY { get; set; } public float PosZ { get; set; }
    public float RotX { get; set; } public float RotY { get; set; } public float RotZ { get; set; }
    public float ScaX { get; set; } = 1; public float ScaY { get; set; } = 1; public float ScaZ { get; set; } = 1;
    public int MeshType { get; set; } = -1;
    public string? AssetPath { get; set; }
    public Guid AssetGuid { get; set; }
    public bool HasCamera { get; set; } = false;
    public float CamFov { get; set; } = 45f; public bool CamIsPrimary { get; set; } = true;
    public float CamNear { get; set; } = 0.1f; public float CamFar { get; set; } = 1000f;
    public List<SerializedScript> Scripts { get; set; } = new List<SerializedScript>();
    public bool HasRigidBody { get; set; } = false;
    public float Mass { get; set; } = 1.0f; public bool IsKinematic { get; set; } = false;
    public bool UseGravity { get; set; } = true; public int Constraints { get; set; } = 0;
    public bool HasBoxCollider { get; set; } = false;
    public float BoxSizeX { get; set; } = 1; public float BoxSizeY { get; set; } = 1; public float BoxSizeZ { get; set; } = 1;
    public float BoxCenterX { get; set; } = 0; public float BoxCenterY { get; set; } = 0; public float BoxCenterZ { get; set; } = 0;
    public bool BoxIsTrigger { get; set; } = false;
    public bool HasSphereCollider { get; set; } = false;
    public float SphereRadius { get; set; } = 0.5f;
    public float SphereCenterX { get; set; } = 0; public float SphereCenterY { get; set; } = 0; public float SphereCenterZ { get; set; } = 0;
    public bool SphereIsTrigger { get; set; } = false;
    public bool HasCapsuleCollider { get; set; } = false;
    public float CapsuleRadius { get; set; } = 0.5f; public float CapsuleHeight { get; set; } = 1.0f;
    public float CapsuleCenterX { get; set; } = 0; public float CapsuleCenterY { get; set; } = 0; public float CapsuleCenterZ { get; set; } = 0;
    public bool CapsuleIsTrigger { get; set; } = false;
    public bool HasCylinderCollider { get; set; } = false;
    public float CylinderRadius { get; set; } = 0.5f; public float CylinderHeight { get; set; } = 1.0f;
    public float CylinderCenterX { get; set; } = 0; public float CylinderCenterY { get; set; } = 0; public float CylinderCenterZ { get; set; } = 0;
    public bool CylinderIsTrigger { get; set; } = false;
    public bool HasMeshCollider { get; set; } = false;
    public Guid MeshColliderAssetGuid { get; set; } = Guid.Empty;
    public bool MeshColliderIsConvex { get; set; } = true;
    public float MeshColliderCenterX { get; set; } = 0; public float MeshColliderCenterY { get; set; } = 0; public float MeshColliderCenterZ { get; set; } = 0;
    public bool MeshColliderIsTrigger { get; set; } = false;
}

public class SceneData
{
    public List<SerializedEntity> Entities { get; set; } = new List<SerializedEntity>();
}

// ============================================================================
// SERIALIZER
// ============================================================================

public static class SceneSerializer
{
    // Tipos que são gerenciados pelo sistema (não devem ser serializados diretamente pelo loop genérico)
    private static readonly HashSet<Type> _managedTypes = new()
    {
        typeof(RelationshipComponent),  // Parent/Child é resolvido por índice
    };

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new Vector3DFloatConverter() }
    };

    // Cache dos membros serializáveis por tipo de componente (excluindo [NonSerializedComponent])
    private static readonly Dictionary<Type, List<MemberInfo>> _serializableMembersCache = new();

    private static List<MemberInfo> GetSerializableMembers(Type type)
    {
        if (_serializableMembersCache.TryGetValue(type, out var cached))
            return cached;

        var members = new List<MemberInfo>();

        // Propriedades públicas de instância
        foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (prop.GetCustomAttribute<NonSerializedComponentAttribute>() != null) continue;
            if (!prop.CanRead || !prop.CanWrite) continue;
            members.Add(prop);
        }

        // Campos públicos de instância
        foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Instance))
        {
            if (field.GetCustomAttribute<NonSerializedComponentAttribute>() != null) continue;
            members.Add(field);
        }

        _serializableMembersCache[type] = members;
        return members;
    }

    // ========================================================================
    // SERIALIZE (V2 - Genérico)
    // ========================================================================

    private static SerializedEntityV2 SerializeSingleEntity(Entity entity, Registry registry)
    {
        var sEntity = new SerializedEntityV2();

        foreach (var componentType in registry.GetRegisteredComponentTypes())
        {
            if (_managedTypes.Contains(componentType)) continue;
            if (!registry.HasComponentByType(entity, componentType)) continue;

            var component = registry.GetComponentBoxed(entity, componentType);
            var componentData = SerializeComponent(component, componentType);
            sEntity.Components[componentType.Name] = JsonSerializer.SerializeToElement(componentData, _jsonOptions);
        }

        return sEntity;
    }

    private static Dictionary<string, object?> SerializeComponent(object component, Type componentType)
    {
        var data = new Dictionary<string, object?>();
        var members = GetSerializableMembers(componentType);

        foreach (var member in members)
        {
            object? value = member switch
            {
                PropertyInfo prop => prop.GetValue(component),
                FieldInfo field => field.GetValue(component),
                _ => null
            };
            data[member.Name] = value;
        }

        return data;
    }

    // ========================================================================
    // DESERIALIZE (V2 - Genérico)
    // ========================================================================

    // Mapeamento: nome do tipo → Type real (cache)
    private static Dictionary<string, Type>? _componentTypeMap;

    private static Dictionary<string, Type> GetComponentTypeMap()
    {
        if (_componentTypeMap != null) return _componentTypeMap;

        _componentTypeMap = new Dictionary<string, Type>();
        foreach (var type in Assembly.GetAssembly(typeof(IComponent))!.GetTypes())
        {
            if (type.IsValueType && typeof(IComponent).IsAssignableFrom(type) && type != typeof(IComponent))
            {
                _componentTypeMap[type.Name] = type;
            }
        }
        return _componentTypeMap;
    }

    private static void DeserializeSingleEntity(Entity entity, SerializedEntityV2 sEntity, Registry registry)
    {
        var typeMap = GetComponentTypeMap();

        foreach (var kvp in sEntity.Components)
        {
            if (!typeMap.TryGetValue(kvp.Key, out var componentType)) continue;
            if (_managedTypes.Contains(componentType)) continue;

            var component = DeserializeComponent(kvp.Value, componentType);
            if (component != null)
            {
                registry.AddComponentBoxed(entity, componentType, component);
            }
        }
    }

    private static object? DeserializeComponent(JsonElement element, Type componentType)
    {
        // Cria instância default do struct
        var component = Activator.CreateInstance(componentType)!;
        var members = GetSerializableMembers(componentType);

        foreach (var member in members)
        {
            string memberName = member.Name;
            if (!element.TryGetProperty(memberName, out var jsonValue)) continue;

            Type memberType = member switch
            {
                PropertyInfo prop => prop.PropertyType,
                FieldInfo field => field.FieldType,
                _ => typeof(object)
            };

            try
            {
                var value = JsonSerializer.Deserialize(jsonValue.GetRawText(), memberType, _jsonOptions);
                switch (member)
                {
                    case PropertyInfo prop: prop.SetValue(component, value); break;
                    case FieldInfo field: field.SetValue(component, value); break;
                }
            }
            catch (Exception)
            {
                // Se um campo falhar na deserialização, ignora e usa o valor padrão
            }
        }

        return component;
    }

    // ========================================================================
    // PUBLIC API (Save/Load/Undo Snapshot) — Sempre salva V2, lê V1 ou V2
    // ========================================================================

    public static string SerializeEntityToJson(Entity entity, Registry registry)
    {
        var sEntity = SerializeSingleEntity(entity, registry);
        return JsonSerializer.Serialize(sEntity, _jsonOptions);
    }

    public static void DeserializeEntityFromJson(string json, Entity entity, Registry registry)
    {
        var sEntity = JsonSerializer.Deserialize<SerializedEntityV2>(json, _jsonOptions);
        if (sEntity != null)
        {
            // Clear existing non-managed components to do a clean overwrite
            foreach (var type in registry.GetRegisteredComponentTypes())
            {
                if (_managedTypes.Contains(type)) continue;
                if (registry.HasComponentByType(entity, type))
                    registry.RemoveComponentByType(entity, type);
            }
            DeserializeSingleEntity(entity, sEntity, registry);
        }
    }

    public static void SaveScene(string filepath, Scene scene)
    {
        var registry = scene.Registry;
        var sceneData = new SceneDataV2();
        var entities = new List<Entity>(registry.GetLivingEntities());
        
        var entityToIndex = new Dictionary<int, int>();
        for (int i = 0; i < entities.Count; i++)
            entityToIndex[entities[i].Id] = i;

        foreach (var entity in entities)
        {
            var sEntity = SerializeSingleEntity(entity, registry);
            
            if (registry.HasComponent<RelationshipComponent>(entity))
            {
                var rel = registry.GetComponent<RelationshipComponent>(entity);
                if (rel.Parent.HasValue && entityToIndex.TryGetValue(rel.Parent.Value.Id, out int parentIndex))
                    sEntity.ParentIndex = parentIndex;
            }

            sceneData.Entities.Add(sEntity);
        }

        string json = JsonSerializer.Serialize(sceneData, _jsonOptions);
        File.WriteAllText(filepath, json);
        ConsoleLog.Log($"Cena salva em {filepath}");
    }

    public static void LoadScene(string filepath, Scene scene)
    {
        var registry = scene.Registry;
        if (!File.Exists(filepath)) return;

        string json = File.ReadAllText(filepath);

        // Detectar formato: V2 tem campo "Version", V1 não tem
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("Version", out _))
            {
                LoadSceneV2(json, scene);
            }
            else
            {
                LoadSceneV1Legacy(json, scene);
            }
        }
        catch (Exception ex)
        {
            ConsoleLog.Error($"Erro ao ler cena {filepath}: {ex.Message}");
        }
    }

    private static void LoadSceneV2(string json, Scene scene)
    {
        var registry = scene.Registry;
        var sceneData = JsonSerializer.Deserialize<SceneDataV2>(json, _jsonOptions);
        if (sceneData == null) return;

        registry.Clear();

        var createdEntities = new List<Entity>();
        foreach (var sEntity in sceneData.Entities)
        {
            var entity = registry.CreateEntity();
            DeserializeSingleEntity(entity, sEntity, registry);
            createdEntities.Add(entity);
        }

        // Restaurar hierarquias
        for (int i = 0; i < sceneData.Entities.Count; i++)
        {
            var sEntity = sceneData.Entities[i];
            if (sEntity.ParentIndex >= 0 && sEntity.ParentIndex < createdEntities.Count)
            {
                RelationshipSystem.SetParent(createdEntities[i], createdEntities[sEntity.ParentIndex], registry);
            }
        }

        ConsoleLog.Log($"Cena carregada ({sceneData.Entities.Count} entidades) [formato V2]");
    }

    // ========================================================================
    // PREFABS
    // ========================================================================

    public static void SavePrefab(string filepath, Scene scene, Entity rootEntity)
    {
        var registry = scene.Registry;
        var sceneData = new SceneDataV2();
        
        var entitiesToSave = new List<Entity>();
        void CollectRecursive(Entity e)
        {
            entitiesToSave.Add(e);
            if (registry.HasComponent<RelationshipComponent>(e))
            {
                var rel = registry.GetComponent<RelationshipComponent>(e);
                var child = rel.FirstChild;
                while (child.HasValue)
                {
                    CollectRecursive(child.Value);
                    if (registry.HasComponent<RelationshipComponent>(child.Value))
                        child = registry.GetComponent<RelationshipComponent>(child.Value).NextSibling;
                    else
                        child = null;
                }
            }
        }
        CollectRecursive(rootEntity);

        var entityToIndex = new Dictionary<int, int>();
        for (int i = 0; i < entitiesToSave.Count; i++)
            entityToIndex[entitiesToSave[i].Id] = i;

        foreach (var entity in entitiesToSave)
        {
            var sEntity = SerializeSingleEntity(entity, registry);
            
            if (registry.HasComponent<RelationshipComponent>(entity))
            {
                var rel = registry.GetComponent<RelationshipComponent>(entity);
                if (rel.Parent.HasValue && entityToIndex.TryGetValue(rel.Parent.Value.Id, out int parentIndex))
                    sEntity.ParentIndex = parentIndex;
            }

            sceneData.Entities.Add(sEntity);
        }

        string json = JsonSerializer.Serialize(sceneData, _jsonOptions);
        File.WriteAllText(filepath, json);
        ConsoleLog.Log($"Prefab salvo em {filepath}");
    }

    public static Entity? LoadPrefab(string filepath, Scene scene, Entity? parent = null)
    {
        var registry = scene.Registry;
        if (!File.Exists(filepath)) return null;

        string json = File.ReadAllText(filepath);
        
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("Version", out _))
            {
                return LoadPrefabV2(json, scene, parent);
            }
            else
            {
                return LoadPrefabV1Legacy(json, scene, parent);
            }
        }
        catch
        {
            return null;
        }
    }

    private static Entity? LoadPrefabV2(string json, Scene scene, Entity? parent)
    {
        var registry = scene.Registry;
        var sceneData = JsonSerializer.Deserialize<SceneDataV2>(json, _jsonOptions);
        if (sceneData == null || sceneData.Entities.Count == 0) return null;

        var createdEntities = new List<Entity>();
        foreach (var sEntity in sceneData.Entities)
        {
            var entity = registry.CreateEntity();
            DeserializeSingleEntity(entity, sEntity, registry);
            createdEntities.Add(entity);
        }

        for (int i = 0; i < sceneData.Entities.Count; i++)
        {
            var sEntity = sceneData.Entities[i];
            if (sEntity.ParentIndex >= 0 && sEntity.ParentIndex < createdEntities.Count)
            {
                RelationshipSystem.SetParent(createdEntities[i], createdEntities[sEntity.ParentIndex], registry);
            }
            else if (i == 0 && parent.HasValue)
            {
                RelationshipSystem.SetParent(createdEntities[i], parent.Value, registry);
            }
        }

        ConsoleLog.Log($"Prefab carregado ({sceneData.Entities.Count} entidades) [formato V2]");
        return createdEntities[0];
    }

    // ========================================================================
    // LEGACY V1 READERS (Retrocompatibilidade)
    // ========================================================================

    private static void LoadSceneV1Legacy(string json, Scene scene)
    {
        var registry = scene.Registry;
        SceneData? sceneData = null;
        try { sceneData = JsonSerializer.Deserialize<SceneData>(json); }
        catch (Exception ex) { ConsoleLog.Error($"Erro ao ler cena (formato legado): {ex.Message}"); return; }
        
        if (sceneData == null) return;

        registry.Clear();

        var createdEntities = new List<Entity>();
        foreach (var sEntity in sceneData.Entities)
        {
            var entity = registry.CreateEntity();
            DeserializeSingleEntityV1Legacy(entity, sEntity, registry);
            createdEntities.Add(entity);
        }

        for (int i = 0; i < sceneData.Entities.Count; i++)
        {
            var sEntity = sceneData.Entities[i];
            if (sEntity.ParentIndex >= 0 && sEntity.ParentIndex < createdEntities.Count)
            {
                RelationshipSystem.SetParent(createdEntities[i], createdEntities[sEntity.ParentIndex], registry);
            }
        }

        ConsoleLog.Log($"Cena carregada ({sceneData.Entities.Count} entidades) [formato legado V1 → será salva em V2]");
    }

    private static Entity? LoadPrefabV1Legacy(string json, Scene scene, Entity? parent)
    {
        var registry = scene.Registry;
        SceneData? sceneData;
        try { sceneData = JsonSerializer.Deserialize<SceneData>(json); }
        catch { return null; }

        if (sceneData == null || sceneData.Entities.Count == 0) return null;

        var createdEntities = new List<Entity>();
        foreach (var sEntity in sceneData.Entities)
        {
            var entity = registry.CreateEntity();
            DeserializeSingleEntityV1Legacy(entity, sEntity, registry);
            createdEntities.Add(entity);
        }

        for (int i = 0; i < sceneData.Entities.Count; i++)
        {
            var sEntity = sceneData.Entities[i];
            if (sEntity.ParentIndex >= 0 && sEntity.ParentIndex < createdEntities.Count)
            {
                RelationshipSystem.SetParent(createdEntities[i], createdEntities[sEntity.ParentIndex], registry);
            }
            else if (i == 0 && parent.HasValue)
            {
                RelationshipSystem.SetParent(createdEntities[i], parent.Value, registry);
            }
        }

        ConsoleLog.Log($"Prefab carregado ({sceneData.Entities.Count} entidades) [formato legado V1]");
        return createdEntities[0];
    }

    private static void DeserializeSingleEntityV1Legacy(Entity entity, SerializedEntity sEntity, Registry registry)
    {
        if (sEntity.NetworkId != -1)
            registry.AddComponent(entity, new NetworkIdentityComponent { NetworkId = sEntity.NetworkId, LockUserId = -1 });

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
                    scriptData.FieldValues[kvp.Key] = kvp.Value;
                scriptComp.Scripts.Add(scriptData);
            }
            if (scriptComp.Scripts.Count > 0)
                registry.AddComponent(entity, scriptComp);
        }

        if (sEntity.HasRigidBody)
        {
            registry.AddComponent(entity, new RigidBodyComponent
            {
                Mass = sEntity.Mass,
                IsKinematic = sEntity.IsKinematic,
                UseGravity = sEntity.UseGravity,
                Constraints = (RigidbodyConstraints)sEntity.Constraints
            });
        }

        if (sEntity.HasBoxCollider)
            registry.AddComponent(entity, new BoxColliderComponent { Size = new Vector3D<float>(sEntity.BoxSizeX, sEntity.BoxSizeY, sEntity.BoxSizeZ), Center = new Vector3D<float>(sEntity.BoxCenterX, sEntity.BoxCenterY, sEntity.BoxCenterZ), IsTrigger = sEntity.BoxIsTrigger });
        if (sEntity.HasSphereCollider)
            registry.AddComponent(entity, new SphereColliderComponent { Radius = sEntity.SphereRadius, Center = new Vector3D<float>(sEntity.SphereCenterX, sEntity.SphereCenterY, sEntity.SphereCenterZ), IsTrigger = sEntity.SphereIsTrigger });
        if (sEntity.HasCapsuleCollider)
            registry.AddComponent(entity, new CapsuleColliderComponent { Radius = sEntity.CapsuleRadius, Height = sEntity.CapsuleHeight, Center = new Vector3D<float>(sEntity.CapsuleCenterX, sEntity.CapsuleCenterY, sEntity.CapsuleCenterZ), IsTrigger = sEntity.CapsuleIsTrigger });
        if (sEntity.HasCylinderCollider)
            registry.AddComponent(entity, new CylinderColliderComponent { Radius = sEntity.CylinderRadius, Height = sEntity.CylinderHeight, Center = new Vector3D<float>(sEntity.CylinderCenterX, sEntity.CylinderCenterY, sEntity.CylinderCenterZ), IsTrigger = sEntity.CylinderIsTrigger });
        if (sEntity.HasMeshCollider)
            registry.AddComponent(entity, new MeshColliderComponent { AssetGuid = sEntity.MeshColliderAssetGuid, IsConvex = sEntity.MeshColliderIsConvex, Center = new Vector3D<float>(sEntity.MeshColliderCenterX, sEntity.MeshColliderCenterY, sEntity.MeshColliderCenterZ), IsTrigger = sEntity.MeshColliderIsTrigger });
    }
}

// ============================================================================
// JSON CONVERTER para Vector3D<float> (Silk.NET)
// ============================================================================

public class Vector3DFloatConverter : JsonConverter<Vector3D<float>>
{
    public override Vector3D<float> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.StartObject)
        {
            float x = 0, y = 0, z = 0;
            while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
            {
                if (reader.TokenType == JsonTokenType.PropertyName)
                {
                    string prop = reader.GetString()!;
                    reader.Read();
                    switch (prop)
                    {
                        case "X": x = reader.GetSingle(); break;
                        case "Y": y = reader.GetSingle(); break;
                        case "Z": z = reader.GetSingle(); break;
                    }
                }
            }
            return new Vector3D<float>(x, y, z);
        }
        throw new JsonException("Expected StartObject for Vector3D<float>");
    }

    public override void Write(Utf8JsonWriter writer, Vector3D<float> value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteNumber("X", value.X);
        writer.WriteNumber("Y", value.Y);
        writer.WriteNumber("Z", value.Z);
        writer.WriteEndObject();
    }
}
