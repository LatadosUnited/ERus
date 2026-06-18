using Silk.NET.OpenGL;
using System;
using System.Numerics;
using ERus.Engine.ECS;

namespace ERus.Engine.Graphics;

public class SceneRenderer : IDisposable
{
    private readonly GL _gl;
    private uint _vao;
    private uint _vbo;
    
    private uint _gridVao;
    private uint _gridVbo;
    private int _gridVertexCount;
    
    private uint _gizmoVao;
    private uint _gizmoVbo;
    private int _gizmoVertexCount;
    
    private uint _cameraVao;
    private uint _cameraVbo;
    private int _cameraVertexCount;

    private uint _shaderProgram;

    private int _modelLoc;
    private int _viewLoc;
    private int _projLoc;
    private int _tintLoc;

    private uint _assetShaderProgram;
    private int _assetModelLoc;
    private int _assetViewLoc;
    private int _assetProjLoc;
    private int _assetTintLoc;

    private readonly string _vertexShaderSource = @"
        #version 330 core
        layout (location = 0) in vec3 aPosition;
        layout (location = 1) in vec3 aColor;

        out vec3 vertexColor;

        uniform mat4 uModel;
        uniform mat4 uView;
        uniform mat4 uProjection;

        void main()
        {
            gl_Position = uProjection * uView * uModel * vec4(aPosition, 1.0);
            vertexColor = aColor;
        }";

    private readonly string _fragmentShaderSource = @"
        #version 330 core
        in vec3 vertexColor;
        out vec4 FragColor;

        uniform vec3 uColorTint;

        void main()
        {
            FragColor = vec4(vertexColor * uColorTint, 1.0);
        }";

    private readonly string _assetVertexShaderSource = @"
        #version 330 core
        layout (location = 0) in vec3 aPosition;
        layout (location = 1) in vec3 aNormal;
        layout (location = 2) in vec2 aTexCoords;

        out vec2 TexCoords;
        out vec3 Normal;
        out vec3 FragPos;

        uniform mat4 uModel;
        uniform mat4 uView;
        uniform mat4 uProjection;

        void main()
        {
            gl_Position = uProjection * uView * uModel * vec4(aPosition, 1.0);
            TexCoords = aTexCoords;
            Normal = mat3(transpose(inverse(uModel))) * aNormal;
            FragPos = vec3(uModel * vec4(aPosition, 1.0));
        }
        ";

    private readonly string _assetFragmentShaderSource = @"
        #version 330 core
        in vec2 TexCoords;
        in vec3 Normal;
        in vec3 FragPos;

        out vec4 FragColor;

        uniform sampler2D texture_diffuse1;
        uniform vec3 uColorTint;

        void main()
        {
            vec4 texColor = texture(texture_diffuse1, TexCoords);
            // Se o alpha for 0, provavelmente não há textura ligada, então usamos cinza
            if (texColor.a < 0.1) texColor = vec4(0.8, 0.8, 0.8, 1.0);

            vec3 norm = normalize(Normal);
            vec3 lightDir = normalize(vec3(0.5, 1.0, 0.5));
            
            float diff = max(dot(norm, lightDir), 0.3); // 0.3 é a luz ambiente
            vec3 diffuse = diff * texColor.rgb;
            
            FragColor = vec4(diffuse * uColorTint, texColor.a);
        }
        ";

    public SceneRenderer(GL gl)
    {
        _gl = gl;
        Initialize();
    }

    private unsafe void Initialize()
    {
        // 1. Compilar Shaders
        uint vertexShader = _gl.CreateShader(ShaderType.VertexShader);
        _gl.ShaderSource(vertexShader, _vertexShaderSource);
        _gl.CompileShader(vertexShader);

        uint fragmentShader = _gl.CreateShader(ShaderType.FragmentShader);
        _gl.ShaderSource(fragmentShader, _fragmentShaderSource);
        _gl.CompileShader(fragmentShader);

        _shaderProgram = _gl.CreateProgram();
        _gl.AttachShader(_shaderProgram, vertexShader);
        _gl.AttachShader(_shaderProgram, fragmentShader);
        _gl.LinkProgram(_shaderProgram);

        _gl.DeleteShader(vertexShader);
        _gl.DeleteShader(fragmentShader);

        // Compilar Asset Shader
        uint aVertexShader = _gl.CreateShader(ShaderType.VertexShader);
        _gl.ShaderSource(aVertexShader, _assetVertexShaderSource);
        _gl.CompileShader(aVertexShader);

        uint aFragmentShader = _gl.CreateShader(ShaderType.FragmentShader);
        _gl.ShaderSource(aFragmentShader, _assetFragmentShaderSource);
        _gl.CompileShader(aFragmentShader);

        _assetShaderProgram = _gl.CreateProgram();
        _gl.AttachShader(_assetShaderProgram, aVertexShader);
        _gl.AttachShader(_assetShaderProgram, aFragmentShader);
        _gl.LinkProgram(_assetShaderProgram);

        _gl.DeleteShader(aVertexShader);
        _gl.DeleteShader(aFragmentShader);

        // 2. Localizar Uniforms
        _modelLoc = _gl.GetUniformLocation(_shaderProgram, "uModel");
        _viewLoc = _gl.GetUniformLocation(_shaderProgram, "uView");
        _projLoc = _gl.GetUniformLocation(_shaderProgram, "uProjection");
        _tintLoc = _gl.GetUniformLocation(_shaderProgram, "uColorTint");

        _assetModelLoc = _gl.GetUniformLocation(_assetShaderProgram, "uModel");
        _assetViewLoc = _gl.GetUniformLocation(_assetShaderProgram, "uView");
        _assetProjLoc = _gl.GetUniformLocation(_assetShaderProgram, "uProjection");
        _assetTintLoc = _gl.GetUniformLocation(_assetShaderProgram, "uColorTint");

        // 3. Criar VBO e VAO
        float[] vertices =
        {
            // Posições           // Cores
            -0.5f, -0.5f, -0.5f,  1.0f, 0.0f, 0.0f,
             0.5f, -0.5f, -0.5f,  1.0f, 0.0f, 0.0f,
             0.5f,  0.5f, -0.5f,  1.0f, 0.0f, 0.0f,
             0.5f,  0.5f, -0.5f,  1.0f, 0.0f, 0.0f,
            -0.5f,  0.5f, -0.5f,  1.0f, 0.0f, 0.0f,
            -0.5f, -0.5f, -0.5f,  1.0f, 0.0f, 0.0f,

            -0.5f, -0.5f,  0.5f,  0.0f, 1.0f, 0.0f,
             0.5f, -0.5f,  0.5f,  0.0f, 1.0f, 0.0f,
             0.5f,  0.5f,  0.5f,  0.0f, 1.0f, 0.0f,
             0.5f,  0.5f,  0.5f,  0.0f, 1.0f, 0.0f,
            -0.5f,  0.5f,  0.5f,  0.0f, 1.0f, 0.0f,
            -0.5f, -0.5f,  0.5f,  0.0f, 1.0f, 0.0f,

            -0.5f,  0.5f,  0.5f,  0.0f, 0.0f, 1.0f,
            -0.5f,  0.5f, -0.5f,  0.0f, 0.0f, 1.0f,
            -0.5f, -0.5f, -0.5f,  0.0f, 0.0f, 1.0f,
            -0.5f, -0.5f, -0.5f,  0.0f, 0.0f, 1.0f,
            -0.5f, -0.5f,  0.5f,  0.0f, 0.0f, 1.0f,
            -0.5f,  0.5f,  0.5f,  0.0f, 0.0f, 1.0f,

             0.5f,  0.5f,  0.5f,  1.0f, 1.0f, 0.0f,
             0.5f,  0.5f, -0.5f,  1.0f, 1.0f, 0.0f,
             0.5f, -0.5f, -0.5f,  1.0f, 1.0f, 0.0f,
             0.5f, -0.5f, -0.5f,  1.0f, 1.0f, 0.0f,
             0.5f, -0.5f,  0.5f,  1.0f, 1.0f, 0.0f,
             0.5f,  0.5f,  0.5f,  1.0f, 1.0f, 0.0f,

            -0.5f, -0.5f, -0.5f,  0.0f, 1.0f, 1.0f,
             0.5f, -0.5f, -0.5f,  0.0f, 1.0f, 1.0f,
             0.5f, -0.5f,  0.5f,  0.0f, 1.0f, 1.0f,
             0.5f, -0.5f,  0.5f,  0.0f, 1.0f, 1.0f,
            -0.5f, -0.5f,  0.5f,  0.0f, 1.0f, 1.0f,
            -0.5f, -0.5f, -0.5f,  0.0f, 1.0f, 1.0f,

            -0.5f,  0.5f, -0.5f,  1.0f, 0.0f, 1.0f,
             0.5f,  0.5f, -0.5f,  1.0f, 0.0f, 1.0f,
             0.5f,  0.5f,  0.5f,  1.0f, 0.0f, 1.0f,
             0.5f,  0.5f,  0.5f,  1.0f, 0.0f, 1.0f,
            -0.5f,  0.5f,  0.5f,  1.0f, 0.0f, 1.0f,
            -0.5f,  0.5f, -0.5f,  1.0f, 0.0f, 1.0f
        };

        _vao = _gl.GenVertexArray();
        _vbo = _gl.GenBuffer();

        _gl.BindVertexArray(_vao);

        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo);
        fixed (float* buf = vertices)
        {
            _gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(vertices.Length * sizeof(float)), buf, BufferUsageARB.StaticDraw);
        }

        // Atributo de Posição
        _gl.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 6 * sizeof(float), (void*)0);
        _gl.EnableVertexAttribArray(0);

        // Atributo de Cor
        _gl.VertexAttribPointer(1, 3, VertexAttribPointerType.Float, false, 6 * sizeof(float), (void*)(3 * sizeof(float)));
        _gl.EnableVertexAttribArray(1);

        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, 0);
        _gl.BindVertexArray(0);

        // 4. Criar VBO e VAO para a Grade (Grid)
        // Tamanho 1000 gera uma grade de 2000x2000 unidades, o que cobre toda a cena na prática
        int gridSize = 1000;
        float step = 1.0f;
        var gridVertices = new System.Collections.Generic.List<float>();

        for (int i = -gridSize; i <= gridSize; i++)
        {
            // Linha ao longo de Z
            float zR = (i == 0) ? 0.0f : 0.4f;
            float zG = (i == 0) ? 0.0f : 0.4f;
            float zB = (i == 0) ? 1.0f : 0.4f;

            gridVertices.Add(i * step); gridVertices.Add(0f); gridVertices.Add(-gridSize * step);
            gridVertices.Add(zR); gridVertices.Add(zG); gridVertices.Add(zB);
            gridVertices.Add(i * step); gridVertices.Add(0f); gridVertices.Add(gridSize * step);
            gridVertices.Add(zR); gridVertices.Add(zG); gridVertices.Add(zB);

            // Linha ao longo de X
            float xR = (i == 0) ? 1.0f : 0.4f;
            float xG = (i == 0) ? 0.0f : 0.4f;
            float xB = (i == 0) ? 0.0f : 0.4f;

            gridVertices.Add(-gridSize * step); gridVertices.Add(0f); gridVertices.Add(i * step);
            gridVertices.Add(xR); gridVertices.Add(xG); gridVertices.Add(xB);
            gridVertices.Add(gridSize * step); gridVertices.Add(0f); gridVertices.Add(i * step);
            gridVertices.Add(xR); gridVertices.Add(xG); gridVertices.Add(xB);
        }

        _gridVertexCount = gridVertices.Count / 6;

        _gridVao = _gl.GenVertexArray();
        _gridVbo = _gl.GenBuffer();

        _gl.BindVertexArray(_gridVao);
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _gridVbo);

        var gridArr = gridVertices.ToArray();
        fixed (float* buf = gridArr)
        {
            _gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(gridArr.Length * sizeof(float)), buf, BufferUsageARB.StaticDraw);
        }

        _gl.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 6 * sizeof(float), (void*)0);
        _gl.EnableVertexAttribArray(0);
        _gl.VertexAttribPointer(1, 3, VertexAttribPointerType.Float, false, 6 * sizeof(float), (void*)(3 * sizeof(float)));
        _gl.EnableVertexAttribArray(1);

        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, 0);
        _gl.BindVertexArray(0);

        // 5. Criar VBO e VAO para o Gizmo (Setas Sólidas)
        var gizmoVerts = new System.Collections.Generic.List<float>();

        // Y Axis (Verde)
        GenerateArrow(gizmoVerts, Matrix4x4.Identity, new Vector3(0.0f, 1.0f, 0.0f));
        // X Axis (Vermelho) -> Rotaciona -90º no eixo Z
        GenerateArrow(gizmoVerts, Matrix4x4.CreateRotationZ(-MathF.PI / 2f), new Vector3(1.0f, 0.0f, 0.0f));
        // Z Axis (Azul) -> Rotaciona 90º no eixo X
        GenerateArrow(gizmoVerts, Matrix4x4.CreateRotationX(MathF.PI / 2f), new Vector3(0.0f, 0.0f, 1.0f));

        _gizmoVertexCount = gizmoVerts.Count / 6;

        _gizmoVao = _gl.GenVertexArray();
        _gizmoVbo = _gl.GenBuffer();

        _gl.BindVertexArray(_gizmoVao);
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _gizmoVbo);
        
        var gizmoArr = gizmoVerts.ToArray();
        fixed (float* buf = gizmoArr)
        {
            _gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(gizmoArr.Length * sizeof(float)), buf, BufferUsageARB.StaticDraw);
        }
        _gl.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 6 * sizeof(float), (void*)0);
        _gl.EnableVertexAttribArray(0);
        _gl.VertexAttribPointer(1, 3, VertexAttribPointerType.Float, false, 6 * sizeof(float), (void*)(3 * sizeof(float)));
        _gl.EnableVertexAttribArray(1);

        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, 0);
        _gl.BindVertexArray(0);

        // 6. Criar VBO e VAO para a Câmera (Wireframe)
        var camVerts = new System.Collections.Generic.List<float>();
        float cb = 0.2f;
        Vector3[] body = {
            new Vector3(-cb, -cb, cb), new Vector3(cb, -cb, cb),
            new Vector3(cb, -cb, cb), new Vector3(cb, cb, cb),
            new Vector3(cb, cb, cb), new Vector3(-cb, cb, cb),
            new Vector3(-cb, cb, cb), new Vector3(-cb, -cb, cb),
            new Vector3(-cb, -cb, -cb), new Vector3(cb, -cb, -cb),
            new Vector3(cb, -cb, -cb), new Vector3(cb, cb, -cb),
            new Vector3(cb, cb, -cb), new Vector3(-cb, cb, -cb),
            new Vector3(-cb, cb, -cb), new Vector3(-cb, -cb, -cb),
            new Vector3(-cb, -cb, cb), new Vector3(-cb, -cb, -cb),
            new Vector3(cb, -cb, cb), new Vector3(cb, -cb, -cb),
            new Vector3(cb, cb, cb), new Vector3(cb, cb, -cb),
            new Vector3(-cb, cb, cb), new Vector3(-cb, cb, -cb)
        };
        float fl = 0.5f; float fw = 0.3f;
        Vector3[] frustum = {
            new Vector3(0, 0, -cb), new Vector3(-fw, -fw, -fl),
            new Vector3(0, 0, -cb), new Vector3(fw, -fw, -fl),
            new Vector3(0, 0, -cb), new Vector3(fw, fw, -fl),
            new Vector3(0, 0, -cb), new Vector3(-fw, fw, -fl),
            new Vector3(-fw, -fw, -fl), new Vector3(fw, -fw, -fl),
            new Vector3(fw, -fw, -fl), new Vector3(fw, fw, -fl),
            new Vector3(fw, fw, -fl), new Vector3(-fw, fw, -fl),
            new Vector3(-fw, fw, -fl), new Vector3(-fw, -fw, -fl)
        };
        void AddCamLine(Vector3 p1, Vector3 p2) {
            camVerts.Add(p1.X); camVerts.Add(p1.Y); camVerts.Add(p1.Z); camVerts.Add(1f); camVerts.Add(1f); camVerts.Add(1f);
            camVerts.Add(p2.X); camVerts.Add(p2.Y); camVerts.Add(p2.Z); camVerts.Add(1f); camVerts.Add(1f); camVerts.Add(1f);
        }
        for (int i=0; i<body.Length; i+=2) AddCamLine(body[i], body[i+1]);
        for (int i=0; i<frustum.Length; i+=2) AddCamLine(frustum[i], frustum[i+1]);

        _cameraVertexCount = camVerts.Count / 6;
        _cameraVao = _gl.GenVertexArray();
        _cameraVbo = _gl.GenBuffer();

        _gl.BindVertexArray(_cameraVao);
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _cameraVbo);
        var camArr = camVerts.ToArray();
        fixed (float* buf = camArr) {
            _gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(camArr.Length * sizeof(float)), buf, BufferUsageARB.StaticDraw);
        }
        _gl.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 6 * sizeof(float), (void*)0);
        _gl.EnableVertexAttribArray(0);
        _gl.VertexAttribPointer(1, 3, VertexAttribPointerType.Float, false, 6 * sizeof(float), (void*)(3 * sizeof(float)));
        _gl.EnableVertexAttribArray(1);

        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, 0);
        _gl.BindVertexArray(0);
    }

    private void GenerateArrow(System.Collections.Generic.List<float> buffer, Matrix4x4 transform, Vector3 baseColor)
    {
        // === Haste (Shaft) - Cubo alongado ===
        float w = 0.05f; float h = 2.5f;
        Vector3[] sV = {
            // Front face
            new Vector3(-w, 0, w), new Vector3(w, 0, w), new Vector3(w, h, w),
            new Vector3(w, h, w), new Vector3(-w, h, w), new Vector3(-w, 0, w),
            // Right face
            new Vector3(w, 0, w), new Vector3(w, 0, -w), new Vector3(w, h, -w),
            new Vector3(w, h, -w), new Vector3(w, h, w), new Vector3(w, 0, w),
            // Back face
            new Vector3(w, 0, -w), new Vector3(-w, 0, -w), new Vector3(-w, h, -w),
            new Vector3(-w, h, -w), new Vector3(w, h, -w), new Vector3(w, 0, -w),
            // Left face
            new Vector3(-w, 0, -w), new Vector3(-w, 0, w), new Vector3(-w, h, w),
            new Vector3(-w, h, w), new Vector3(-w, h, -w), new Vector3(-w, 0, -w)
        };
        Vector3[] sN = {
            Vector3.UnitZ, Vector3.UnitZ, Vector3.UnitZ, Vector3.UnitZ, Vector3.UnitZ, Vector3.UnitZ,
            Vector3.UnitX, Vector3.UnitX, Vector3.UnitX, Vector3.UnitX, Vector3.UnitX, Vector3.UnitX,
            -Vector3.UnitZ, -Vector3.UnitZ, -Vector3.UnitZ, -Vector3.UnitZ, -Vector3.UnitZ, -Vector3.UnitZ,
            -Vector3.UnitX, -Vector3.UnitX, -Vector3.UnitX, -Vector3.UnitX, -Vector3.UnitX, -Vector3.UnitX
        };

        // === Ponta (Tip) - Pirâmide ===
        float pw = 0.15f; float pb = 2.5f; float ph = 3.0f;
        Vector3 tip = new Vector3(0, ph, 0);
        Vector3[] pV = {
            // Front
            new Vector3(-pw, pb, pw), new Vector3(pw, pb, pw), tip,
            // Right
            new Vector3(pw, pb, pw), new Vector3(pw, pb, -pw), tip,
            // Back
            new Vector3(pw, pb, -pw), new Vector3(-pw, pb, -pw), tip,
            // Left
            new Vector3(-pw, pb, -pw), new Vector3(-pw, pb, pw), tip,
            // Base (bottom of pyramid to close the mesh visually)
            new Vector3(-pw, pb, -pw), new Vector3(pw, pb, pw), new Vector3(-pw, pb, pw),
            new Vector3(-pw, pb, -pw), new Vector3(pw, pb, -pw), new Vector3(pw, pb, pw)
        };
        
        Vector3 nFront = Vector3.Normalize(new Vector3(0, pw, ph - pb));
        Vector3 nRight = Vector3.Normalize(new Vector3(ph - pb, pw, 0));
        Vector3 nBack = Vector3.Normalize(new Vector3(0, pw, -(ph - pb)));
        Vector3 nLeft = Vector3.Normalize(new Vector3(-(ph - pb), pw, 0));
        Vector3 nBottom = -Vector3.UnitY;

        Vector3[] pN = {
            nFront, nFront, nFront,
            nRight, nRight, nRight,
            nBack, nBack, nBack,
            nLeft, nLeft, nLeft,
            nBottom, nBottom, nBottom,
            nBottom, nBottom, nBottom
        };

        Vector3 lightDir = Vector3.Normalize(new Vector3(1.0f, 1.5f, 0.5f));

        void AddGeometry(Vector3[] verts, Vector3[] norms)
        {
            for (int i = 0; i < verts.Length; i++)
            {
                var pos = Vector3.Transform(verts[i], transform);
                var norm = Vector3.Normalize(Vector3.TransformNormal(norms[i], transform));

                // Fake Lighting calculation
                float intensity = Vector3.Dot(norm, lightDir);
                // Clamp de [0, 1] e mapeia para luz ambiente [0.5, 1.0]
                intensity = 0.5f + Math.Max(0, intensity) * 0.5f;
                var col = baseColor * intensity;

                buffer.Add(pos.X); buffer.Add(pos.Y); buffer.Add(pos.Z);
                buffer.Add(col.X); buffer.Add(col.Y); buffer.Add(col.Z);
            }
        }

        AddGeometry(sV, sN);
        AddGeometry(pV, pN);
    }

    public unsafe void Draw(Registry registry, Matrix4x4 viewMatrix, float aspectRatio, Entity? selectedEntity = null, bool isLocked = false, bool drawGrid = true, float fov = 45f, float nearClip = 0.1f, float farClip = 100f)
    {
        _gl.Enable(EnableCap.DepthTest); // Liga teste de profundidade 3D
        _gl.UseProgram(_shaderProgram);

        // Reseta o Tint
        _gl.Uniform3(_tintLoc, 1.0f, 1.0f, 1.0f);

        // View Matrix vem da Câmera Injetada
        Matrix4x4 view = viewMatrix;
        // Projection Matrix
        float fovRad = fov * (MathF.PI / 180f);
        Matrix4x4 proj = Matrix4x4.CreatePerspectiveFieldOfView(fovRad, aspectRatio, nearClip, farClip);

        _gl.UniformMatrix4(_viewLoc, 1, false, (float*)&view);
        _gl.UniformMatrix4(_projLoc, 1, false, (float*)&proj);

        // --- Renderizar a Grade (Grid) ---
        if (drawGrid)
        {
            var identity = Matrix4x4.Identity;
            _gl.UniformMatrix4(_modelLoc, 1, false, (float*)&identity);
            _gl.BindVertexArray(_gridVao);
            _gl.DrawArrays(PrimitiveType.Lines, 0, (uint)_gridVertexCount);
        }

        // --- Renderizar os Objetos 3D ---
        _gl.BindVertexArray(_vao);

        // Intera sobre os objetos físicos no mundo
        foreach (var entity in registry.View<TransformComponent, MeshComponent>())
        {
            ref var transform = ref registry.GetComponent<TransformComponent>(entity);
            ref var mesh = ref registry.GetComponent<MeshComponent>(entity);

            var scale = Matrix4x4.CreateScale(transform.Scale.X, transform.Scale.Y, transform.Scale.Z);
            
            float degToRad = MathF.PI / 180f;
            var rot = Matrix4x4.CreateRotationX(transform.Rotation.X * degToRad)
                    * Matrix4x4.CreateRotationY(transform.Rotation.Y * degToRad)
                    * Matrix4x4.CreateRotationZ(transform.Rotation.Z * degToRad);
            
            var translation = Matrix4x4.CreateTranslation(transform.Position.X, transform.Position.Y, transform.Position.Z);
            var modelMatrix = scale * rot * translation;

            if (mesh.AssetGuid != System.Guid.Empty)
            {
                // Usar Asset Shader
                _gl.UseProgram(_assetShaderProgram);
                _gl.Uniform3(_assetTintLoc, 1.0f, 1.0f, 1.0f);
                _gl.UniformMatrix4(_assetViewLoc, 1, false, (float*)&view);
                _gl.UniformMatrix4(_assetProjLoc, 1, false, (float*)&proj);
                _gl.UniformMatrix4(_assetModelLoc, 1, false, (float*)&modelMatrix);

                var assetManager = ERus.Engine.Assets.AssetManager.Get();
                string? path = ERus.Engine.Core.Engine.Instance.AssetDatabase.GetPathByGuid(mesh.AssetGuid);
                if (!string.IsNullOrEmpty(path))
                {
                    var model = assetManager.LoadModel(path);
                    if (model != null)
                    {
                        model.Draw(_assetShaderProgram);
                    }
                }

                // Restaurar Basic Shader para os próximos objetos
                _gl.UseProgram(_shaderProgram);
                _gl.Uniform3(_tintLoc, 1.0f, 1.0f, 1.0f);
                _gl.UniformMatrix4(_viewLoc, 1, false, (float*)&view);
                _gl.UniformMatrix4(_projLoc, 1, false, (float*)&proj);
                
                // Precisamos re-bindar o VAO padrão porque o model.Draw() desbindou
                _gl.BindVertexArray(_vao);
            }
            else if (mesh.Type == PrimitiveMeshType.Cube)
            {
                _gl.UniformMatrix4(_modelLoc, 1, false, (float*)&modelMatrix);
                _gl.DrawArrays(PrimitiveType.Triangles, 0, 36);
            }
        }

        // --- Renderizar Ícones de Câmera ---
        if (drawGrid) // drawGrid == true indica que estamos na SceneView
        {
            _gl.BindVertexArray(_cameraVao);
            foreach (var entity in registry.View<TransformComponent, CameraComponent>())
            {
                ref var transform = ref registry.GetComponent<TransformComponent>(entity);

                float degToRad = MathF.PI / 180f;
                var rot = Matrix4x4.CreateRotationX(transform.Rotation.X * degToRad)
                        * Matrix4x4.CreateRotationY(transform.Rotation.Y * degToRad)
                        * Matrix4x4.CreateRotationZ(transform.Rotation.Z * degToRad);
                
                var translation = Matrix4x4.CreateTranslation(transform.Position.X, transform.Position.Y, transform.Position.Z);
                var model = rot * translation;

                _gl.UniformMatrix4(_modelLoc, 1, false, (float*)&model);
                _gl.DrawArrays(PrimitiveType.Lines, 0, (uint)_cameraVertexCount);
            }
        }

        // O Gizmo 3D antigo foi removido daqui.
        // A renderização do Gizmo agora é totalmente feita via ImGui DrawList
        // na classe GizmoRenderer (chamada pela SceneViewWindow), o que
        // evita o bug de "duas setas" na tela e suporta os 3 modos corretamente.

        _gl.BindVertexArray(0);
        _gl.Disable(EnableCap.DepthTest); // Desliga no final para não afetar o ImGui
    }

    public void Dispose()
    {
        if (_vao != 0) _gl.DeleteVertexArray(_vao);
        if (_vbo != 0) _gl.DeleteBuffer(_vbo);
        if (_gridVao != 0) _gl.DeleteVertexArray(_gridVao);
        if (_gridVbo != 0) _gl.DeleteBuffer(_gridVbo);
        if (_gizmoVao != 0) _gl.DeleteVertexArray(_gizmoVao);
        if (_gizmoVbo != 0) _gl.DeleteBuffer(_gizmoVbo);
        if (_cameraVao != 0) _gl.DeleteVertexArray(_cameraVao);
        if (_cameraVbo != 0) _gl.DeleteBuffer(_cameraVbo);
        if (_shaderProgram != 0) _gl.DeleteProgram(_shaderProgram);
    }
}
