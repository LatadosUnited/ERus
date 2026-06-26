using ERus.Engine.Core;
using Silk.NET.Maths;
using Jitter2.Dynamics;
using Jitter2.Collision.Shapes;
using Jitter2.LinearMath;
using System.Collections.Generic;

namespace ERus.Engine.ECS;

/// <summary>
/// Responsável por criar as Shapes de colisão do Jitter2 a partir dos componentes 
/// de collider de uma entidade (e seus filhos recursivamente).
/// Separado do PhysicsSystem para manter responsabilidade única.
/// </summary>
public static class PhysicsShapeFactory
{
    /// <summary>
    /// Adiciona todas as shapes de colisão encontradas na entidade (e filhos recursivos) ao RigidBody.
    /// </summary>
    public static void AddShapesFromEntity(Entity entity, RigidBody body, System.Numerics.Matrix4x4 parentInverseMatrix, Registry registry, Core.Engine engine)
    {
        var globalTransform = GetGlobalTransform(entity, registry);
        
        // Relativo ao RigidBody raiz
        System.Numerics.Matrix4x4 relativeTransform = globalTransform * parentInverseMatrix;
        System.Numerics.Matrix4x4.Decompose(relativeTransform, out System.Numerics.Vector3 scale, out System.Numerics.Quaternion rotation, out System.Numerics.Vector3 translation);

        // Converter structs de math
        JMatrix rotMatrix = JMatrix.CreateFromQuaternion(new JQuaternion(rotation.X, rotation.Y, rotation.Z, rotation.W));

        void ApplyShape(RigidBodyShape baseShape, Vector3D<float> center)
        {
            System.Numerics.Vector3 c = new System.Numerics.Vector3(center.X * scale.X, center.Y * scale.Y, center.Z * scale.Z);
            System.Numerics.Vector3 finalPos = translation + System.Numerics.Vector3.Transform(c, rotation);
            JVector jFinalPos = new JVector(finalPos.X, finalPos.Y, finalPos.Z);

            if (jFinalPos.LengthSquared() > 0.0001f || rotation != System.Numerics.Quaternion.Identity)
            {
                var tShape = new TransformedShape(baseShape, jFinalPos, rotMatrix);
                body.AddShape(tShape);
            }
            else
            {
                body.AddShape(baseShape);
            }
        }

        // --- Box ---
        if (registry.HasComponent<BoxColliderComponent>(entity))
        {
            var coll = registry.GetComponent<BoxColliderComponent>(entity);
            ApplyShape(new BoxShape(coll.Size.X * scale.X, coll.Size.Y * scale.Y, coll.Size.Z * scale.Z), coll.Center);
        }
        
        // --- Sphere ---
        if (registry.HasComponent<SphereColliderComponent>(entity))
        {
            var coll = registry.GetComponent<SphereColliderComponent>(entity);
            float maxScale = System.Math.Max(scale.X, System.Math.Max(scale.Y, scale.Z));
            ApplyShape(new SphereShape(coll.Radius * maxScale), coll.Center);
        }

        // --- Capsule ---
        if (registry.HasComponent<CapsuleColliderComponent>(entity))
        {
            var coll = registry.GetComponent<CapsuleColliderComponent>(entity);
            float maxScaleXZ = System.Math.Max(scale.X, scale.Z);
            ApplyShape(new CapsuleShape(coll.Radius * maxScaleXZ, coll.Height * scale.Y), coll.Center);
        }

        // --- Cylinder ---
        if (registry.HasComponent<CylinderColliderComponent>(entity))
        {
            var coll = registry.GetComponent<CylinderColliderComponent>(entity);
            float maxScaleXZ = System.Math.Max(scale.X, scale.Z);
            ApplyShape(new CylinderShape(coll.Radius * maxScaleXZ, coll.Height * scale.Y), coll.Center);
        }

        // --- Mesh ---
        if (registry.HasComponent<MeshColliderComponent>(entity))
        {
            var coll = registry.GetComponent<MeshColliderComponent>(entity);
            if (coll.AssetGuid != System.Guid.Empty)
            {
                var am = ERus.Engine.Assets.AssetManager.Get();
                string path = engine.AssetDatabase.GetPathByGuid(coll.AssetGuid) ?? "";
                var modelData = am.LoadModel(path);
                if (modelData != null && modelData.Meshes.Count > 0)
                {
                    List<JVector> vertices = new List<JVector>();
                    List<JTriangle> triangles = new List<JTriangle>();
                    
                    List<int> indices = new List<int>();
                    int indexOffset = 0;
                    foreach(var mesh in modelData.Meshes)
                    {
                        for(int i=0; i<mesh.Vertices.Count; i++)
                        {
                            var pos = mesh.Vertices[i].Position;
                            vertices.Add(new JVector(pos.X * scale.X, pos.Y * scale.Y, pos.Z * scale.Z));
                        }
                        for(int i=0; i<mesh.Indices.Count; i++)
                        {
                            indices.Add((int)mesh.Indices[i] + indexOffset);
                        }
                        indexOffset = vertices.Count;
                    }

                    if (coll.IsConvex)
                    {
                        var hull = new PointCloudShape(System.Runtime.InteropServices.CollectionsMarshal.AsSpan(vertices));
                        ApplyShape(hull, coll.Center);
                    }
                    else
                    {
                        var tm = new TriangleMesh(vertices.ToArray(), indices.ToArray(), false);
                        foreach(var tShape in TriangleShape.CreateAllShapes(tm))
                        {
                            ApplyShape(tShape, coll.Center);
                        }
                    }
                }
            }
        }

        // Recursar para filhos
        if (registry.HasComponent<RelationshipComponent>(entity))
        {
            var rel = registry.GetComponent<RelationshipComponent>(entity);
            var child = rel.FirstChild;
            while (child.HasValue)
            {
                AddShapesFromEntity(child.Value, body, parentInverseMatrix, registry, engine);
                if (registry.HasComponent<RelationshipComponent>(child.Value))
                    child = registry.GetComponent<RelationshipComponent>(child.Value).NextSibling;
                else
                    child = null;
            }
        }
    }

    /// <summary>
    /// Calcula a matrix de transformação global (local * parent * grandparent...) de uma entidade.
    /// </summary>
    public static System.Numerics.Matrix4x4 GetGlobalTransform(Entity entity, Registry registry)
    {
        if (!registry.HasComponent<TransformComponent>(entity))
            return System.Numerics.Matrix4x4.Identity;

        ref var t = ref registry.GetComponent<TransformComponent>(entity);
        float degToRad = System.MathF.PI / 180f;
        var localMatrix = System.Numerics.Matrix4x4.CreateScale(t.Scale.X, t.Scale.Y, t.Scale.Z) *
                          System.Numerics.Matrix4x4.CreateRotationX(t.Rotation.X * degToRad) *
                          System.Numerics.Matrix4x4.CreateRotationY(t.Rotation.Y * degToRad) *
                          System.Numerics.Matrix4x4.CreateRotationZ(t.Rotation.Z * degToRad) *
                          System.Numerics.Matrix4x4.CreateTranslation(t.Position.X, t.Position.Y, t.Position.Z);

        if (registry.HasComponent<RelationshipComponent>(entity))
        {
            var parent = registry.GetComponent<RelationshipComponent>(entity).Parent;
            if (parent.HasValue)
            {
                var parentMatrix = GetGlobalTransform(parent.Value, registry);
                return localMatrix * parentMatrix;
            }
        }
        return localMatrix;
    }
}
