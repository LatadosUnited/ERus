using Silk.NET.OpenGL;
using ERus.Engine.Core;
using ERus.Engine.Graphics;
using System.Numerics;

namespace ERus.Engine.Modules;

/// <summary>
/// Módulo responsável pela renderização primária e limpeza do buffer de vídeo.
/// Deve ser sempre o primeiro módulo da Engine a rodar no Render() para garantir a tela limpa.
/// </summary>
public class GraphicsModule : IEngineModule
{
    private GL _gl;
    private GLFramebuffer _gameFramebuffer;
    private SceneRenderer _sceneRenderer;
    private Core.Engine _engine;

    /// <summary>
    /// ID da textura gerada pelo Framebuffer do GameView.
    /// </summary>
    public uint GameTextureId => _gameFramebuffer?.TextureId ?? 0;

    /// <summary>
    /// Tamanho atual do painel do GameView, modificado pela interface.
    /// </summary>
    public Vector2 GameViewSize { get; set; } = new Vector2(800, 600);

    /// <summary>
    /// Guarda a referência do OpenGL injetada pela Engine.
    /// Define a cor de fundo padrão e inicializa os renderizadores.
    /// </summary>
    public void Initialize(Core.Engine engine)
    {
        _engine = engine;
        _gl = engine.Gl;
        
        ERus.Engine.Assets.AssetManager.Initialize(_gl);

        // Define a cor de fundo padrão (azul escuro) da tela inteira (embaixo de tudo)
        _gl.ClearColor(0.1f, 0.15f, 0.2f, 1.0f);

        _gameFramebuffer = new GLFramebuffer(_gl, (int)GameViewSize.X, (int)GameViewSize.Y);
        _sceneRenderer = new SceneRenderer(_gl);
    }

    public void Update(double deltaTime)
    {
        // A camada gráfica crua não possui lógica no Update (Física ou Inputs)
    }

    /// <summary>
    /// Limpa os buffers da tela nativa e depois desenha a cena 3D dentro do Framebuffer Offscreen.
    /// </summary>
    public void Render(double deltaTime)
    {
        // 1. Limpa a tela nativa principal (onde o ImGui será desenhado depois)
        _gl.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
        _gl.ClearColor(0.1f, 0.15f, 0.2f, 1.0f);
        _gl.Clear((uint)ClearBufferMask.ColorBufferBit | (uint)ClearBufferMask.DepthBufferBit);

        // Verifica redimensionamento do GameView
        if (_gameFramebuffer.Width != (int)GameViewSize.X || _gameFramebuffer.Height != (int)GameViewSize.Y)
        {
            _gameFramebuffer.Invalidate((int)GameViewSize.X, (int)GameViewSize.Y);
        }

        var ecsModule = _engine.GetModule<ECSModule>();
        if (ecsModule == null) return;

        // --- RENDER GAME VIEW ---
        _gameFramebuffer.Bind();
        _gl.ClearColor(0.0f, 0.0f, 0.0f, 1.0f);
        _gl.Clear((uint)ClearBufferMask.ColorBufferBit | (uint)ClearBufferMask.DepthBufferBit);

        // Buscar Câmera Primária
        ERus.Engine.ECS.Entity? primaryCamEntity = null;
        foreach (var entity in ecsModule.ActiveScene.Registry.View<ERus.Engine.ECS.CameraComponent>())
        {
            var cam = ecsModule.ActiveScene.Registry.GetComponent<ERus.Engine.ECS.CameraComponent>(entity);
            if (cam.IsPrimary)
            {
                primaryCamEntity = entity;
                break;
            }
        }

        if (primaryCamEntity.HasValue && ecsModule.ActiveScene.Registry.HasComponent<ERus.Engine.ECS.TransformComponent>(primaryCamEntity.Value))
        {
            ref var t = ref ecsModule.ActiveScene.Registry.GetComponent<ERus.Engine.ECS.TransformComponent>(primaryCamEntity.Value);
            var camComp = ecsModule.ActiveScene.Registry.GetComponent<ERus.Engine.ECS.CameraComponent>(primaryCamEntity.Value);
            
            float gameAspect = (float)GameViewSize.X / (float)GameViewSize.Y;
            if (gameAspect == 0) gameAspect = 1.0f;

            // Converter Transform para View Matrix
            float degToRad = System.MathF.PI / 180f;
            var rot = Matrix4x4.CreateRotationX(t.Rotation.X * degToRad)
                    * Matrix4x4.CreateRotationY(t.Rotation.Y * degToRad)
                    * Matrix4x4.CreateRotationZ(t.Rotation.Z * degToRad);

            Vector3 forward = Vector3.Transform(-Vector3.UnitZ, rot);
            Vector3 up = Vector3.Transform(Vector3.UnitY, rot);
            Vector3 pos = new Vector3(t.Position.X, t.Position.Y, t.Position.Z);

            Matrix4x4 gameViewMatrix = Matrix4x4.CreateLookAt(pos, pos + forward, up);

            _sceneRenderer.Draw(ecsModule.ActiveScene.Registry, gameViewMatrix, gameAspect, null, false, false, camComp.FieldOfView, camComp.NearClip, camComp.FarClip);
        }

        _gameFramebuffer.Unbind(_engine.CurrentSize);
    }

    public void Dispose()
    {
        _gameFramebuffer?.Dispose();
        _sceneRenderer?.Dispose();
    }
}
