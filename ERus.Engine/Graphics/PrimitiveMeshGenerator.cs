using System;
using System.Collections.Generic;
using System.Numerics;

namespace ERus.Engine.Graphics;

public class MeshData
{
    public float[] Vertices { get; set; } = Array.Empty<float>();
    public uint[] Indices { get; set; } = Array.Empty<uint>();
    public float BoundingRadius { get; set; } = 0f;

    public void CalculateBoundingRadius()
    {
        float maxSq = 0f;
        for (int i = 0; i < Vertices.Length; i += 6)
        {
            float x = Vertices[i];
            float y = Vertices[i+1];
            float z = Vertices[i+2];
            float sq = x*x + y*y + z*z;
            if (sq > maxSq) maxSq = sq;
        }
        BoundingRadius = System.MathF.Sqrt(maxSq);
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
        // 8 vertices, 36 indices
        var verts = new List<float>();
        Vector3[] positions = {
            new Vector3(-0.5f, -0.5f, -0.5f), new Vector3( 0.5f, -0.5f, -0.5f),
            new Vector3( 0.5f,  0.5f, -0.5f), new Vector3(-0.5f,  0.5f, -0.5f),
            new Vector3(-0.5f, -0.5f,  0.5f), new Vector3( 0.5f, -0.5f,  0.5f),
            new Vector3( 0.5f,  0.5f,  0.5f), new Vector3(-0.5f,  0.5f,  0.5f)
        };
        Vector3[] colors = {
            new Vector3(1,0,0), new Vector3(0,1,0), new Vector3(0,0,1), new Vector3(1,1,0),
            new Vector3(1,0,1), new Vector3(0,1,1), new Vector3(1,1,1), new Vector3(0.5f,0.5f,0.5f)
        };
        for (int i = 0; i < 8; i++) {
            verts.Add(positions[i].X); verts.Add(positions[i].Y); verts.Add(positions[i].Z);
            verts.Add(colors[i].X); verts.Add(colors[i].Y); verts.Add(colors[i].Z);
        }

        uint[] indices = {
            // Front
            0, 1, 2, 2, 3, 0,
            // Right
            1, 5, 6, 6, 2, 1,
            // Back
            5, 4, 7, 7, 6, 5,
            // Left
            4, 0, 3, 3, 7, 4,
            // Top
            3, 2, 6, 6, 7, 3,
            // Bottom
            4, 5, 1, 1, 0, 4
        };

        var mesh = new MeshData { Vertices = verts.ToArray(), Indices = indices };
        mesh.CalculateBoundingRadius();
        return mesh;
    }

    public static MeshData GeneratePlane()
    {
        var verts = new List<float>();
        // Plane is on XZ axis, Y=0
        Vector3[] pos = {
            new Vector3(-0.5f, 0, -0.5f), new Vector3( 0.5f, 0, -0.5f),
            new Vector3( 0.5f, 0,  0.5f), new Vector3(-0.5f, 0,  0.5f)
        };
        for (int i = 0; i < 4; i++) {
            verts.Add(pos[i].X); verts.Add(pos[i].Y); verts.Add(pos[i].Z);
            verts.Add(1); verts.Add(1); verts.Add(1); // Color white
        }
        uint[] indices = { 0, 3, 2, 2, 1, 0 }; // Top face
        var mesh = new MeshData { Vertices = verts.ToArray(), Indices = indices };
        mesh.CalculateBoundingRadius();
        return mesh;
    }

    public static MeshData GenerateQuad()
    {
        var verts = new List<float>();
        // Quad is on XY axis, Z=0 (facing forward)
        Vector3[] pos = {
            new Vector3(-0.5f, -0.5f, 0), new Vector3( 0.5f, -0.5f, 0),
            new Vector3( 0.5f,  0.5f, 0), new Vector3(-0.5f,  0.5f, 0)
        };
        for (int i = 0; i < 4; i++) {
            verts.Add(pos[i].X); verts.Add(pos[i].Y); verts.Add(pos[i].Z);
            verts.Add(1); verts.Add(1); verts.Add(1);
        }
        uint[] indices = { 0, 1, 2, 2, 3, 0 };
        var mesh = new MeshData { Vertices = verts.ToArray(), Indices = indices };
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

                verts.Add(x); verts.Add(y); verts.Add(z);
                verts.Add(1f); verts.Add(1f); verts.Add(1f); // Color
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

        // Vertices for sides
        for (int i = 0; i <= CylinderSegments; i++)
        {
            float theta = ((float)i / CylinderSegments) * MathF.PI * 2f;
            float x = radius * MathF.Cos(theta);
            float z = radius * MathF.Sin(theta);

            // Bottom
            verts.Add(x); verts.Add(-halfHeight); verts.Add(z);
            verts.Add(0.8f); verts.Add(0.8f); verts.Add(0.8f);
            
            // Top
            verts.Add(x); verts.Add(halfHeight); verts.Add(z);
            verts.Add(0.8f); verts.Add(0.8f); verts.Add(0.8f);
        }

        int topCenterIdx = verts.Count / 6;
        verts.Add(0); verts.Add(halfHeight); verts.Add(0);
        verts.Add(1); verts.Add(1); verts.Add(1);

        int bottomCenterIdx = verts.Count / 6;
        verts.Add(0); verts.Add(-halfHeight); verts.Add(0);
        verts.Add(1); verts.Add(1); verts.Add(1);

        for (int i = 0; i < CylinderSegments; i++)
        {
            uint b1 = (uint)(i * 2);
            uint t1 = (uint)(i * 2 + 1);
            uint b2 = (uint)(i * 2 + 2);
            uint t2 = (uint)(i * 2 + 3);

            // Side
            indices.Add(b1); indices.Add(t1); indices.Add(b2);
            indices.Add(t1); indices.Add(t2); indices.Add(b2);

            // Top Cap
            indices.Add((uint)topCenterIdx); indices.Add(t2); indices.Add(t1);
            
            // Bottom Cap
            indices.Add((uint)bottomCenterIdx); indices.Add(b1); indices.Add(b2);
        }

        var mesh = new MeshData { Vertices = verts.ToArray(), Indices = indices.ToArray() };
        mesh.CalculateBoundingRadius();
        return mesh;
    }

    public static MeshData GenerateCapsule(float radius = 0.5f, float height = 2.0f)
    {
        var verts = new List<float>();
        var indices = new List<uint>();
        
        // Height is total height including hemispheres. 
        // Cylinder part height = height - 2*radius
        float cylinderHeight = System.Math.Max(0, height - 2 * radius);
        float halfCylHeight = cylinderHeight / 2f;
        
        int halfRings = CapsuleRings / 2;

        // Top hemisphere
        for (int r = 0; r <= halfRings; r++)
        {
            float phi = ((float)r / halfRings) * (MathF.PI / 2f);
            float yOffset = halfCylHeight;
            
            for (int s = 0; s <= CapsuleSegments; s++)
            {
                float theta = ((float)s / CapsuleSegments) * MathF.PI * 2f;
                float x = radius * MathF.Sin(phi) * MathF.Cos(theta);
                float y = radius * MathF.Cos(phi) + yOffset;
                float z = radius * MathF.Sin(phi) * MathF.Sin(theta);

                verts.Add(x); verts.Add(y); verts.Add(z);
                verts.Add(0.9f); verts.Add(0.9f); verts.Add(0.9f);
            }
        }

        // Bottom hemisphere
        for (int r = halfRings; r <= CapsuleRings; r++)
        {
            float phi = ((float)r / CapsuleRings) * MathF.PI;
            float yOffset = -halfCylHeight;
            
            for (int s = 0; s <= CapsuleSegments; s++)
            {
                float theta = ((float)s / CapsuleSegments) * MathF.PI * 2f;
                float x = radius * MathF.Sin(phi) * MathF.Cos(theta);
                float y = radius * MathF.Cos(phi) + yOffset;
                float z = radius * MathF.Sin(phi) * MathF.Sin(theta);

                verts.Add(x); verts.Add(y); verts.Add(z);
                verts.Add(0.9f); verts.Add(0.9f); verts.Add(0.9f);
            }
        }

        // Indices
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
