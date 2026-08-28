using System;
using System.Collections.Generic;
using System.Numerics;

namespace ERus.Engine.Graphics;

public class MeshData
{
    // Formato por vértice: PosX, PosY, PosZ, NormX, NormY, NormZ, TexU, TexV (8 floats)
    public float[] Vertices { get; set; } = Array.Empty<float>();
    public uint[] Indices { get; set; } = Array.Empty<uint>();
    public float BoundingRadius { get; set; } = 0f;

    public void CalculateBoundingRadius()
    {
        float maxSq = 0f;
        for (int i = 0; i < Vertices.Length; i += 8)
        {
            float x = Vertices[i];
            float y = Vertices[i + 1];
            float z = Vertices[i + 2];
            float sq = x * x + y * y + z * z;
            if (sq > maxSq) maxSq = sq;
        }
        BoundingRadius = MathF.Sqrt(maxSq);
    }
}

public static class PrimitiveMeshGenerator
{
    public const int SphereSegments = 32;
    public const int SphereRings = 16;
    public const int CylinderSegments = 32;
    public const int CapsuleSegments = 32;
    public const int CapsuleRings = 16;

    public static MeshData GenerateCube()
    {
        // 24 vértices (4 por face para normais e UVs independentes)
        var verts = new List<float>();
        var indices = new List<uint>();

        void AddFace(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, Vector3 normal)
        {
            uint baseIdx = (uint)(verts.Count / 8);

            // Vértice 0 (Bottom-Left)
            verts.Add(p0.X); verts.Add(p0.Y); verts.Add(p0.Z);
            verts.Add(normal.X); verts.Add(normal.Y); verts.Add(normal.Z);
            verts.Add(0f); verts.Add(0f);

            // Vértice 1 (Bottom-Right)
            verts.Add(p1.X); verts.Add(p1.Y); verts.Add(p1.Z);
            verts.Add(normal.X); verts.Add(normal.Y); verts.Add(normal.Z);
            verts.Add(1f); verts.Add(0f);

            // Vértice 2 (Top-Right)
            verts.Add(p2.X); verts.Add(p2.Y); verts.Add(p2.Z);
            verts.Add(normal.X); verts.Add(normal.Y); verts.Add(normal.Z);
            verts.Add(1f); verts.Add(1f);

            // Vértice 3 (Top-Left)
            verts.Add(p3.X); verts.Add(p3.Y); verts.Add(p3.Z);
            verts.Add(normal.X); verts.Add(normal.Y); verts.Add(normal.Z);
            verts.Add(0f); verts.Add(1f);

            indices.Add(baseIdx);
            indices.Add(baseIdx + 1);
            indices.Add(baseIdx + 2);
            indices.Add(baseIdx + 2);
            indices.Add(baseIdx + 3);
            indices.Add(baseIdx);
        }

        // Front (+Z)
        AddFace(new Vector3(-0.5f, -0.5f, 0.5f), new Vector3(0.5f, -0.5f, 0.5f), new Vector3(0.5f, 0.5f, 0.5f), new Vector3(-0.5f, 0.5f, 0.5f), Vector3.UnitZ);
        // Back (-Z)
        AddFace(new Vector3(0.5f, -0.5f, -0.5f), new Vector3(-0.5f, -0.5f, -0.5f), new Vector3(-0.5f, 0.5f, -0.5f), new Vector3(0.5f, 0.5f, -0.5f), -Vector3.UnitZ);
        // Top (+Y)
        AddFace(new Vector3(-0.5f, 0.5f, 0.5f), new Vector3(0.5f, 0.5f, 0.5f), new Vector3(0.5f, 0.5f, -0.5f), new Vector3(-0.5f, 0.5f, -0.5f), Vector3.UnitY);
        // Bottom (-Y)
        AddFace(new Vector3(-0.5f, -0.5f, -0.5f), new Vector3(0.5f, -0.5f, -0.5f), new Vector3(0.5f, -0.5f, 0.5f), new Vector3(-0.5f, -0.5f, 0.5f), -Vector3.UnitY);
        // Right (+X)
        AddFace(new Vector3(0.5f, -0.5f, 0.5f), new Vector3(0.5f, -0.5f, -0.5f), new Vector3(0.5f, 0.5f, -0.5f), new Vector3(0.5f, 0.5f, 0.5f), Vector3.UnitX);
        // Left (-X)
        AddFace(new Vector3(-0.5f, -0.5f, -0.5f), new Vector3(-0.5f, -0.5f, 0.5f), new Vector3(-0.5f, 0.5f, 0.5f), new Vector3(-0.5f, 0.5f, -0.5f), -Vector3.UnitX);

        var mesh = new MeshData { Vertices = verts.ToArray(), Indices = indices.ToArray() };
        mesh.CalculateBoundingRadius();
        return mesh;
    }

    public static MeshData GeneratePlane()
    {
        // Plane no plano XZ, Y=0, apontando para cima (+Y)
        float[] verts = new float[]
        {
            // Pos                   // Normal           // UV
            -0.5f, 0.0f,  0.5f,     0.0f, 1.0f, 0.0f,   0.0f, 0.0f,
             0.5f, 0.0f,  0.5f,     0.0f, 1.0f, 0.0f,   1.0f, 0.0f,
             0.5f, 0.0f, -0.5f,     0.0f, 1.0f, 0.0f,   1.0f, 1.0f,
            -0.5f, 0.0f, -0.5f,     0.0f, 1.0f, 0.0f,   0.0f, 1.0f
        };

        uint[] indices = new uint[] { 0, 1, 2, 2, 3, 0 };

        var mesh = new MeshData { Vertices = verts, Indices = indices };
        mesh.CalculateBoundingRadius();
        return mesh;
    }

    public static MeshData GenerateQuad()
    {
        // Quad no plano XY, Z=0, apontando para frente (+Z) - Perfeito para Sprites 2D e UI
        float[] verts = new float[]
        {
            // Pos                   // Normal           // UV
            -0.5f, -0.5f, 0.0f,     0.0f, 0.0f, 1.0f,   0.0f, 0.0f,
             0.5f, -0.5f, 0.0f,     0.0f, 0.0f, 1.0f,   1.0f, 0.0f,
             0.5f,  0.5f, 0.0f,     0.0f, 0.0f, 1.0f,   1.0f, 1.0f,
            -0.5f,  0.5f, 0.0f,     0.0f, 0.0f, 1.0f,   0.0f, 1.0f
        };

        uint[] indices = new uint[] { 0, 1, 2, 2, 3, 0 };

        var mesh = new MeshData { Vertices = verts, Indices = indices };
        mesh.CalculateBoundingRadius();
        return mesh;
    }

    public static MeshData GenerateSphere(float radius = 0.5f)
    {
        var verts = new List<float>();
        var indices = new List<uint>();

        for (int r = 0; r <= SphereRings; r++)
        {
            float v = (float)r / SphereRings;
            float phi = v * MathF.PI;

            for (int s = 0; s <= SphereSegments; s++)
            {
                float u = (float)s / SphereSegments;
                float theta = u * MathF.PI * 2f;

                float x = radius * MathF.Sin(phi) * MathF.Cos(theta);
                float y = radius * MathF.Cos(phi);
                float z = radius * MathF.Sin(phi) * MathF.Sin(theta);

                Vector3 norm = Vector3.Normalize(new Vector3(x, y, z));

                verts.Add(x); verts.Add(y); verts.Add(z);
                verts.Add(norm.X); verts.Add(norm.Y); verts.Add(norm.Z);
                verts.Add(u); verts.Add(1.0f - v);
            }
        }

        for (int r = 0; r < SphereRings; r++)
        {
            for (int s = 0; s < SphereSegments; s++)
            {
                uint first = (uint)((r * (SphereSegments + 1)) + s);
                uint second = (uint)(first + SphereSegments + 1);

                indices.Add(first);
                indices.Add(second);
                indices.Add(first + 1);

                indices.Add(second);
                indices.Add(second + 1);
                indices.Add(first + 1);
            }
        }

        var mesh = new MeshData { Vertices = verts.ToArray(), Indices = indices.ToArray() };
        mesh.CalculateBoundingRadius();
        return mesh;
    }

    public static MeshData GenerateCylinder(float radius = 0.5f, float height = 1.0f)
    {
        var verts = new List<float>();
        var indices = new List<uint>();

        float halfHeight = height / 2f;

        // Vértices da lateral
        for (int i = 0; i <= CylinderSegments; i++)
        {
            float u = (float)i / CylinderSegments;
            float theta = u * MathF.PI * 2f;
            float x = radius * MathF.Cos(theta);
            float z = radius * MathF.Sin(theta);
            Vector3 norm = Vector3.Normalize(new Vector3(x, 0, z));

            // Bottom vertex
            verts.Add(x); verts.Add(-halfHeight); verts.Add(z);
            verts.Add(norm.X); verts.Add(norm.Y); verts.Add(norm.Z);
            verts.Add(u); verts.Add(0f);

            // Top vertex
            verts.Add(x); verts.Add(halfHeight); verts.Add(z);
            verts.Add(norm.X); verts.Add(norm.Y); verts.Add(norm.Z);
            verts.Add(u); verts.Add(1f);
        }

        for (int i = 0; i < CylinderSegments; i++)
        {
            uint b1 = (uint)(i * 2);
            uint t1 = (uint)(i * 2 + 1);
            uint b2 = (uint)(i * 2 + 2);
            uint t2 = (uint)(i * 2 + 3);

            indices.Add(b1); indices.Add(t1); indices.Add(b2);
            indices.Add(t1); indices.Add(t2); indices.Add(b2);
        }

        // Top Cap
        uint topCenterIdx = (uint)(verts.Count / 8);
        verts.Add(0); verts.Add(halfHeight); verts.Add(0);
        verts.Add(0); verts.Add(1); verts.Add(0);
        verts.Add(0.5f); verts.Add(0.5f);

        uint topRingStart = (uint)(verts.Count / 8);
        for (int i = 0; i <= CylinderSegments; i++)
        {
            float u = (float)i / CylinderSegments;
            float theta = u * MathF.PI * 2f;
            float x = radius * MathF.Cos(theta);
            float z = radius * MathF.Sin(theta);

            verts.Add(x); verts.Add(halfHeight); verts.Add(z);
            verts.Add(0); verts.Add(1); verts.Add(0);
            verts.Add(0.5f + 0.5f * MathF.Cos(theta));
            verts.Add(0.5f + 0.5f * MathF.Sin(theta));
        }

        for (int i = 0; i < CylinderSegments; i++)
        {
            indices.Add(topCenterIdx);
            indices.Add((uint)(topRingStart + i + 1));
            indices.Add((uint)(topRingStart + i));
        }

        // Bottom Cap
        uint bottomCenterIdx = (uint)(verts.Count / 8);
        verts.Add(0); verts.Add(-halfHeight); verts.Add(0);
        verts.Add(0); verts.Add(-1); verts.Add(0);
        verts.Add(0.5f); verts.Add(0.5f);

        uint bottomRingStart = (uint)(verts.Count / 8);
        for (int i = 0; i <= CylinderSegments; i++)
        {
            float u = (float)i / CylinderSegments;
            float theta = u * MathF.PI * 2f;
            float x = radius * MathF.Cos(theta);
            float z = radius * MathF.Sin(theta);

            verts.Add(x); verts.Add(-halfHeight); verts.Add(z);
            verts.Add(0); verts.Add(-1); verts.Add(0);
            verts.Add(0.5f + 0.5f * MathF.Cos(theta));
            verts.Add(0.5f - 0.5f * MathF.Sin(theta));
        }

        for (int i = 0; i < CylinderSegments; i++)
        {
            indices.Add(bottomCenterIdx);
            indices.Add((uint)(bottomRingStart + i));
            indices.Add((uint)(bottomRingStart + i + 1));
        }

        var mesh = new MeshData { Vertices = verts.ToArray(), Indices = indices.ToArray() };
        mesh.CalculateBoundingRadius();
        return mesh;
    }

    public static MeshData GenerateCapsule(float radius = 0.5f, float height = 2.0f)
    {
        var verts = new List<float>();
        var indices = new List<uint>();

        float cylinderHeight = System.Math.Max(0, height - 2 * radius);
        float halfCylHeight = cylinderHeight / 2f;
        int halfRings = CapsuleRings / 2;

        // Top hemisphere
        for (int r = 0; r <= halfRings; r++)
        {
            float v = (float)r / halfRings;
            float phi = v * (MathF.PI / 2f);
            float yOffset = halfCylHeight;

            for (int s = 0; s <= CapsuleSegments; s++)
            {
                float u = (float)s / CapsuleSegments;
                float theta = u * MathF.PI * 2f;
                float x = radius * MathF.Sin(phi) * MathF.Cos(theta);
                float y = radius * MathF.Cos(phi) + yOffset;
                float z = radius * MathF.Sin(phi) * MathF.Sin(theta);

                Vector3 norm = Vector3.Normalize(new Vector3(x, radius * MathF.Cos(phi), z));

                verts.Add(x); verts.Add(y); verts.Add(z);
                verts.Add(norm.X); verts.Add(norm.Y); verts.Add(norm.Z);
                verts.Add(u); verts.Add(1.0f - (v * 0.5f));
            }
        }

        // Bottom hemisphere
        for (int r = halfRings; r <= CapsuleRings; r++)
        {
            float v = (float)r / CapsuleRings;
            float phi = v * MathF.PI;
            float yOffset = -halfCylHeight;

            for (int s = 0; s <= CapsuleSegments; s++)
            {
                float u = (float)s / CapsuleSegments;
                float theta = u * MathF.PI * 2f;
                float x = radius * MathF.Sin(phi) * MathF.Cos(theta);
                float y = radius * MathF.Cos(phi) + yOffset;
                float z = radius * MathF.Sin(phi) * MathF.Sin(theta);

                Vector3 norm = Vector3.Normalize(new Vector3(x, radius * MathF.Cos(phi), z));

                verts.Add(x); verts.Add(y); verts.Add(z);
                verts.Add(norm.X); verts.Add(norm.Y); verts.Add(norm.Z);
                verts.Add(u); verts.Add(1.0f - v);
            }
        }

        int totalRings = halfRings + (CapsuleRings - halfRings + 1);
        for (int r = 0; r < totalRings - 1; r++)
        {
            for (int s = 0; s < CapsuleSegments; s++)
            {
                uint first = (uint)((r * (CapsuleSegments + 1)) + s);
                uint second = (uint)(first + CapsuleSegments + 1);

                indices.Add(first);
                indices.Add(second);
                indices.Add(first + 1);

                indices.Add(second);
                indices.Add(second + 1);
                indices.Add(first + 1);
            }
        }

        var mesh = new MeshData { Vertices = verts.ToArray(), Indices = indices.ToArray() };
        mesh.CalculateBoundingRadius();
        return mesh;
    }
}
