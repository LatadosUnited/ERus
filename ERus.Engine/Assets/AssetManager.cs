using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using Silk.NET.Assimp;
using Silk.NET.OpenGL;
using StbImageSharp;

namespace ERus.Engine.Assets;

public class AssetManager
{
    private static AssetManager? _instance;
    private GL _gl;
    private Silk.NET.Assimp.Assimp _assimp;
    
    private Dictionary<string, Model> _models = new();
    private Dictionary<string, Texture> _textures = new();

    public static void Initialize(GL gl)
    {
        if (_instance == null)
        {
            _instance = new AssetManager(gl);
        }
    }

    public static AssetManager Get()
    {
        if (_instance == null)
            throw new Exception("AssetManager não foi inicializado.");
        return _instance;
    }

    private AssetManager(GL gl)
    {
        _gl = gl;
        _assimp = Silk.NET.Assimp.Assimp.GetApi();
    }

    public Model? LoadModel(string path)
    {
        if (string.IsNullOrEmpty(path)) return null;

        string fullPath = Path.GetFullPath(path);

        if (_models.ContainsKey(fullPath))
            return _models[fullPath];

        if (!System.IO.File.Exists(fullPath))
        {
            Scripting.ConsoleLog.Error($"[AssetManager] Model file not found: {fullPath}");
            return null;
        }

        Scripting.ConsoleLog.Log($"[AssetManager] Carregando modelo: {fullPath}");

        Model model = new Model(fullPath);

        unsafe
        {
            var pScene = _assimp.ImportFile(fullPath, (uint)(PostProcessSteps.Triangulate | PostProcessSteps.FlipUVs | PostProcessSteps.CalculateTangentSpace));
            
            if (pScene == null || pScene->MRootNode == null || (pScene->MFlags & (uint)SceneFlags.Incomplete) != 0)
            {
                Scripting.ConsoleLog.Error($"[AssetManager] Erro no Assimp ao carregar {fullPath}");
                return null;
            }

            string directory = Path.GetDirectoryName(fullPath) ?? "";
            ProcessNode(pScene->MRootNode, pScene, model, directory);
        }

        _models[fullPath] = model;
        return model;
    }

    private unsafe void ProcessNode(Node* node, Scene* scene, Model model, string directory)
    {
        for (uint i = 0; i < node->MNumMeshes; i++)
        {
            var mesh = scene->MMeshes[node->MMeshes[i]];
            model.AddMesh(ProcessMesh(mesh, scene, directory));
        }

        for (uint i = 0; i < node->MNumChildren; i++)
        {
            ProcessNode(node->MChildren[i], scene, model, directory);
        }
    }

    private unsafe Mesh ProcessMesh(Silk.NET.Assimp.Mesh* mesh, Scene* scene, string directory)
    {
        var vertices = new List<Vertex>();
        var indices = new List<uint>();
        var textures = new List<Texture>();

        // Process vertices
        for (uint i = 0; i < mesh->MNumVertices; i++)
        {
            Vertex vertex = new Vertex();
            
            vertex.Position = new Vector3(mesh->MVertices[i].X, mesh->MVertices[i].Y, mesh->MVertices[i].Z);
            
            if (mesh->MNormals != null)
                vertex.Normal = new Vector3(mesh->MNormals[i].X, mesh->MNormals[i].Y, mesh->MNormals[i].Z);
            
            if (mesh->MTextureCoords[0] != null)
            {
                vertex.TexCoords = new Vector2(mesh->MTextureCoords[0][i].X, mesh->MTextureCoords[0][i].Y);
                if (mesh->MTangents != null)
                    vertex.Tangent = new Vector3(mesh->MTangents[i].X, mesh->MTangents[i].Y, mesh->MTangents[i].Z);
                if (mesh->MBitangents != null)
                    vertex.Bitangent = new Vector3(mesh->MBitangents[i].X, mesh->MBitangents[i].Y, mesh->MBitangents[i].Z);
            }
            else
            {
                vertex.TexCoords = new Vector2(0.0f, 0.0f);
            }

            vertices.Add(vertex);
        }

        // Process indices
        for (uint i = 0; i < mesh->MNumFaces; i++)
        {
            Face face = mesh->MFaces[i];
            for (uint j = 0; j < face.MNumIndices; j++)
            {
                indices.Add(face.MIndices[j]);
            }
        }

        // Process material
        if (mesh->MMaterialIndex >= 0)
        {
            var material = scene->MMaterials[mesh->MMaterialIndex];
            
            var diffuseMaps = LoadMaterialTextures(material, Silk.NET.Assimp.TextureType.Diffuse, "texture_diffuse", directory);
            textures.AddRange(diffuseMaps);
            
            var specularMaps = LoadMaterialTextures(material, Silk.NET.Assimp.TextureType.Specular, "texture_specular", directory);
            textures.AddRange(specularMaps);
            
            var normalMaps = LoadMaterialTextures(material, Silk.NET.Assimp.TextureType.Normals, "texture_normal", directory);
            textures.AddRange(normalMaps);
            
            var heightMaps = LoadMaterialTextures(material, Silk.NET.Assimp.TextureType.Height, "texture_height", directory);
            textures.AddRange(heightMaps);
        }

        return new Mesh(_gl, vertices, indices, textures);
    }

    private unsafe List<Texture> LoadMaterialTextures(Material* mat, Silk.NET.Assimp.TextureType type, string typeName, string directory)
    {
        var textures = new List<Texture>();
        uint textureCount = _assimp.GetMaterialTextureCount(mat, type);
        
        for (uint i = 0; i < textureCount; i++)
        {
            AssimpString path;
            _assimp.GetMaterialTexture(mat, type, i, &path, null, null, null, null, null, null);
            
            string fileName = path.AsString;
            string fullPath = Path.Combine(directory, fileName);
            
            if (_textures.ContainsKey(fullPath))
            {
                textures.Add(_textures[fullPath]);
            }
            else
            {
                Texture? texture = LoadTextureFromFile(fullPath, typeName);
                if (texture != null)
                {
                    textures.Add(texture);
                    _textures[fullPath] = texture;
                }
            }
        }
        return textures;
    }

    private unsafe Texture? LoadTextureFromFile(string path, string typeName)
    {
        if (!System.IO.File.Exists(path))
        {
            Scripting.ConsoleLog.Warn($"[AssetManager] Texture not found: {path}");
            return null;
        }

        uint textureId = _gl.GenTexture();
        
        StbImage.stbi_set_flip_vertically_on_load(1);
        ImageResult image = ImageResult.FromMemory(System.IO.File.ReadAllBytes(path), ColorComponents.RedGreenBlueAlpha);

        _gl.BindTexture(TextureTarget.Texture2D, textureId);
        
        fixed (byte* ptr = image.Data)
        {
            _gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.Rgba, (uint)image.Width, (uint)image.Height, 0, PixelFormat.Rgba, PixelType.UnsignedByte, ptr);
            _gl.GenerateMipmap(TextureTarget.Texture2D);
        }

        _gl.TexParameterI(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)Silk.NET.OpenGL.TextureWrapMode.Repeat);
        _gl.TexParameterI(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)Silk.NET.OpenGL.TextureWrapMode.Repeat);
        _gl.TexParameterI(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.LinearMipmapLinear);
        _gl.TexParameterI(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);

        return new Texture(_gl, textureId, typeName, path);
    }
}
