using System;
using System.Linq;
using System.Numerics;
using ImGuiNET;
using ERus.Engine.Core;
using ERus.Engine.Modules;
using ERus.Engine.ECS;

namespace ERus.Editor.EditorUI.Panels;

public enum GizmoMode
{
    Translate,
    Rotate,
    Scale
}

public class SceneViewWindow : EditorWindow
{
    private readonly EditorUIController _controller;
    private readonly ERus.Engine.Core.Engine _engine;
    private readonly EditorCamera _camera = new EditorCamera();
    private readonly GizmoInteraction _gizmo = new GizmoInteraction();
    
    private ERus.Engine.Graphics.GLFramebuffer _sceneFramebuffer;
    private ERus.Engine.Graphics.SceneRenderer _sceneRenderer;

    private GizmoMode _currentMode = GizmoMode.Translate;
    private bool _isLocalSpace = false;

    public SceneViewWindow(EditorUIController controller, ERus.Engine.Core.Engine engine) : base("Scene") 
    { 
        _controller = controller;
        _engine = engine;
    }

    protected override void DrawContent()
    {
        var io = ImGui.GetIO();
        bool isHovered = ImGui.IsWindowHovered();
        bool rightMouseDown = ImGui.IsMouseDown(ImGuiMouseButton.Right);

        // --- Keyboard Shortcuts (só quando NÃO está controlando a câmera) ---
        if (isHovered && !io.WantTextInput && !rightMouseDown)
        {
            if (ImGui.IsKeyPressed(ImGuiKey.W)) _currentMode = GizmoMode.Translate;
            if (ImGui.IsKeyPressed(ImGuiKey.E)) _currentMode = GizmoMode.Rotate;
            if (ImGui.IsKeyPressed(ImGuiKey.R)) _currentMode = GizmoMode.Scale;
        }

        // --- Toolbar ---
        DrawToolbar(ImGui.GetWindowPos());

        // --- Viewport ---
        var size = ImGui.GetContentRegionAvail();
        if (size.X <= 0 || size.Y <= 0) return;

        if (_sceneFramebuffer == null)
        {
            _sceneFramebuffer = new ERus.Engine.Graphics.GLFramebuffer(_engine.Gl, (int)size.X, (int)size.Y);
            _sceneRenderer = new ERus.Engine.Graphics.SceneRenderer(_engine.Gl);
        }

        if (_sceneFramebuffer.Width != (int)size.X || _sceneFramebuffer.Height != (int)size.Y)
        {
            _sceneFramebuffer.Invalidate((int)size.X, (int)size.Y);
        }

        _camera.Update(io.DeltaTime, isHovered && !ImGui.IsMouseDown(ImGuiMouseButton.Left));

        var ecsModule = _engine.GetModule<ECSModule>();
        var netModule = _engine.GetModule<NetworkModule>();
        var registry = ecsModule?.ActiveScene.Registry;

        if (ecsModule != null && registry != null)
        {
            _sceneFramebuffer.Bind();
            _engine.Gl.ClearColor(0.2f, 0.2f, 0.2f, 1.0f);
            _engine.Gl.Clear((uint)Silk.NET.OpenGL.ClearBufferMask.ColorBufferBit | (uint)Silk.NET.OpenGL.ClearBufferMask.DepthBufferBit);

            float sceneAspect = size.X / size.Y;
            if (sceneAspect == 0) sceneAspect = 1.0f;

            bool isLockedByOther = false;
            if (_controller.SelectedEntity.HasValue)
            {
                var entity = _controller.SelectedEntity.Value;
                if (registry.HasComponent<NetworkIdentityComponent>(entity))
                {
                    var netComp = registry.GetComponent<NetworkIdentityComponent>(entity);
                    if (netComp.LockUserId != -1 && netComp.LockUserId != netModule?.NetworkManager?.MyUserId)
                        isLockedByOther = true;
                }
            }

            _sceneRenderer.Draw(registry, _camera.GetViewMatrix(), sceneAspect, _controller.SelectedEntity, isLockedByOther, true);
            
            _sceneFramebuffer.Unbind(_engine.CurrentSize);
        }

        var textureId = _sceneFramebuffer.TextureId;
        if (textureId == 0) { ImGui.Text("Inicializando OpenGL..."); return; }

        var cursorPos = ImGui.GetCursorScreenPos();
        ImGui.Image((IntPtr)textureId, size, new Vector2(0, 1), new Vector2(1, 0));

        var drawList = ImGui.GetWindowDrawList();

        if (registry == null) return;

        var proj = Matrix4x4.CreatePerspectiveFieldOfView(MathF.PI / 4f, size.X / size.Y, 0.1f, 100f);
        var view = _camera.GetViewMatrix();
        var viewProj = view * proj;

        // --- Raycasting ---
        var mousePos = io.MousePos - cursorPos;
        Vector3 rayOrigin = Vector3.Zero;
        Vector3 rayDir = Vector3.UnitZ;
        bool isMouseInView = mousePos.X >= 0 && mousePos.Y >= 0 && mousePos.X < size.X && mousePos.Y < size.Y;

        if (isMouseInView)
        {
            (rayOrigin, rayDir) = GizmoMath.ScreenToRay(mousePos, size, proj, view);
        }

        bool interactedWithGizmo = false;
        bool snapping = io.KeyCtrl; // Ctrl segura = snapping ativo

        // --- GIZMO (entidade selecionada) ---
        if (_controller.SelectedEntity.HasValue && !rightMouseDown)
        {
            var entity = _controller.SelectedEntity.Value;

            if (registry.HasComponent<TransformComponent>(entity))
            {
                // Verificar lock de rede
                bool isLockedByOther = false;
                int netId = -1;

                if (registry.HasComponent<NetworkIdentityComponent>(entity))
                {
                    var netComp = registry.GetComponent<NetworkIdentityComponent>(entity);
                    netId = netComp.NetworkId;
                    if (netComp.LockUserId != -1 && netComp.LockUserId != netModule?.NetworkManager?.MyUserId)
                        isLockedByOther = true;
                }
                ref var t = ref registry.GetComponent<TransformComponent>(entity);
                var ePos = new Vector3(t.Position.X, t.Position.Y, t.Position.Z);
                var eRot = new Vector3(t.Rotation.X, t.Rotation.Y, t.Rotation.Z);
                var eScale = new Vector3(t.Scale.X, t.Scale.Y, t.Scale.Z);

                var axes = GizmoMath.ComputeAxes(eRot, _isLocalSpace, _currentMode);
                float distToCam = Vector3.Distance(rayOrigin, ePos);
                float gizmoSize = distToCam * 0.15f;
                float clickFatness = distToCam * 0.05f;

                // 1. HOVER
                if (isMouseInView && !isLockedByOther)
                {
                    _gizmo.UpdateHover(rayOrigin, rayDir, ePos, axes, gizmoSize, clickFatness, _currentMode);
                }

                // 2. RENDER
                switch (_currentMode)
                {
                    case GizmoMode.Translate:
                        GizmoRenderer.DrawTranslateGizmo(drawList, axes, ePos, gizmoSize, viewProj, cursorPos, size, _gizmo.ActiveAxis, _gizmo.HoveredAxis);
                        break;
                    case GizmoMode.Rotate:
                        GizmoRenderer.DrawRotateGizmo(drawList, axes, ePos, gizmoSize, viewProj, cursorPos, size, rayOrigin, _gizmo.ActiveAxis, _gizmo.HoveredAxis);
                        break;
                    case GizmoMode.Scale:
                        GizmoRenderer.DrawScaleGizmo(drawList, axes, ePos, gizmoSize, viewProj, cursorPos, size, _gizmo.ActiveAxis, _gizmo.HoveredAxis);
                        break;
                }

                GizmoRenderer.DrawSnapIndicator(drawList, cursorPos, size, snapping);

                // 3. INTERACTION
                if (isMouseInView && !isLockedByOther)
                {
                    // Begin drag
                    if (ImGui.IsMouseClicked(ImGuiMouseButton.Left) && _gizmo.HoveredAxis != -1)
                    {
                        _gizmo.BeginDrag(ePos, eRot, eScale, axes, rayOrigin, rayDir, _currentMode, t);
                        interactedWithGizmo = true;

                        if (netId != -1) netModule?.Replication?.RequestLock(netId);
                    }

                    // Update drag
                    if (ImGui.IsMouseDragging(ImGuiMouseButton.Left) && _gizmo.IsActive)
                    {
                        interactedWithGizmo = true;

                        if (_gizmo.UpdateDrag(rayOrigin, rayDir, ref t, axes, _currentMode, snapping))
                        {
                            // Replicar pela rede em tempo real
                            if (netModule != null && netId != -1)
                                netModule.Replication?.SendTransform(netId, t.Position, t.Rotation, t.Scale);
                        }
                    }

                    // End drag
                    if (ImGui.IsMouseReleased(ImGuiMouseButton.Left) && _gizmo.IsActive)
                    {
                        var (oldPos, oldRot, oldScale, newPos, newRot, newScale) = _gizmo.EndDrag(t);

                        // Registrar no UndoSystem
                        var cmd = new TransformCommand(
                            entity, registry, netModule,
                            oldPos, oldRot, oldScale,
                            newPos, newRot, newScale,
                            $"{_currentMode} Entity {entity.Id}");
                        _controller.UndoSystem.Record(cmd);

                        // Unlock na rede
                        if (netId != -1)
                            netModule?.Replication?.SendUnlock(netId);
                    }
                }
            }
        }

        // --- ENTITY SELECTION RAYCAST (OBB) ---
        if (isMouseInView && !interactedWithGizmo && !_gizmo.IsActive
            && ImGui.IsMouseClicked(ImGuiMouseButton.Left) && _gizmo.HoveredAxis == -1)
        {
            PerformEntityPicking(registry, rayOrigin, rayDir, io.KeyCtrl);
        }
    }

    private void DrawToolbar(Vector2 windowPos)
    {
        ImGui.SetNextWindowPos(new Vector2(windowPos.X + 10, windowPos.Y + 30));
        ImGui.SetNextWindowBgAlpha(0.7f);
        
        ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, 8.0f);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(8, 8));

        ImGui.Begin("##SceneToolbarOverlay", ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoDocking | ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoFocusOnAppearing | ImGuiWindowFlags.NoNav | ImGuiWindowFlags.NoMove);

        // Gizmo mode toggle buttons (Translate, Rotate, Scale)
        bool isTr = _currentMode == GizmoMode.Translate;
        bool isRo = _currentMode == GizmoMode.Rotate;
        bool isSc = _currentMode == GizmoMode.Scale;

        if (isTr) ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.2f, 0.4f, 0.8f, 1.0f));
        if (ImGui.Button("W")) _currentMode = GizmoMode.Translate;
        if (isTr) ImGui.PopStyleColor();

        ImGui.SameLine();
        if (isRo) ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.8f, 0.4f, 0.2f, 1.0f));
        if (ImGui.Button("E")) _currentMode = GizmoMode.Rotate;
        if (isRo) ImGui.PopStyleColor();

        ImGui.SameLine();
        if (isSc) ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.2f, 0.8f, 0.4f, 1.0f));
        if (ImGui.Button("R")) _currentMode = GizmoMode.Scale;
        if (isSc) ImGui.PopStyleColor();

        ImGui.SameLine();
        ImGui.Text("|");
        ImGui.SameLine();

        ImGui.Checkbox("Local", ref _isLocalSpace);

        ImGui.SameLine();
        ImGui.Text("| Snap:");
        ImGui.SameLine();
        ImGui.Checkbox("##SnapEnabled", ref _gizmo.SnapSettings.Enabled);

        if (_gizmo.SnapSettings.Enabled)
        {
            ImGui.SameLine();
            ImGui.SetNextItemWidth(60);
            ImGui.DragFloat("Pos", ref _gizmo.SnapSettings.TranslateSnap, 0.1f, 0.1f, 10f);
            ImGui.SameLine();
            ImGui.SetNextItemWidth(60);
            ImGui.DragFloat("Rot°", ref _gizmo.SnapSettings.RotateSnap, 1f, 1f, 90f);
            ImGui.SameLine();
            ImGui.SetNextItemWidth(60);
            ImGui.DragFloat("Sca", ref _gizmo.SnapSettings.ScaleSnap, 0.01f, 0.01f, 1f);
        }

        ImGui.End();
        
        ImGui.PopStyleVar(2);
    }

    /// <summary>
    /// Picking de entidades via OBB (considera rotação).
    /// Suporta multi-seleção com Ctrl+Click.
    /// </summary>
    private void PerformEntityPicking(Registry registry, Vector3 rayOrigin, Vector3 rayDir, bool additive)
    {
        float closestDist = float.MaxValue;
        Entity? bestHit = null;

        var entities = registry.GetLivingEntities();
        foreach (var e in entities)
        {
            if (!registry.HasComponent<TransformComponent>(e) || !registry.HasComponent<MeshComponent>(e))
                continue;

            ref var t = ref registry.GetComponent<TransformComponent>(e);
            var center = new Vector3(t.Position.X, t.Position.Y, t.Position.Z);
            var halfExtents = new Vector3(t.Scale.X, t.Scale.Y, t.Scale.Z) * 0.5f;
            var rotMatrix = GizmoMath.BuildRotationMatrix(
                new Vector3(t.Rotation.X, t.Rotation.Y, t.Rotation.Z));

            var (hit, dist) = GizmoMath.RayOBBIntersection(rayOrigin, rayDir, center, halfExtents, rotMatrix);

            if (hit && dist < closestDist && dist > 0)
            {
                closestDist = dist;
                bestHit = e;
            }
        }

        if (bestHit.HasValue)
        {
            if (additive)
                _controller.ToggleSelection(bestHit.Value);
            else
                _controller.SelectedEntity = bestHit.Value;
        }
        else if (!additive)
        {
            _controller.ClearSelection();
        }
    }
}



