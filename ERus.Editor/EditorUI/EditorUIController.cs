using System.Collections.Generic;
using System.Linq;
using Silk.NET.Input;
using Silk.NET.OpenGL;
using Silk.NET.OpenGL.Extensions.ImGui;
using Silk.NET.Windowing;
using ImGuiNET;
using System;
using System.Numerics;
using ERus.Editor.EditorUI.Managers;

namespace ERus.Editor.EditorUI;

public class EditorUIController
{
    private ImGuiController _imGuiController;
    private ERus.Engine.Core.Engine _engine;
    
    private EditorWindowManager _windowManager;
    private EditorToolbar _toolbar;

    // Layout
    private bool _layoutApplied = false;
    private bool _requestLoadUnityLayout = false;

    // --- Multi-Seleção ---
    public HashSet<ERus.Engine.ECS.Entity> SelectedEntities { get; } = new();

    public ERus.Engine.ECS.Entity? SelectedEntity
    {
        get => SelectedEntities.Count > 0 ? SelectedEntities.First() : null;
        set
        {
            SelectedEntities.Clear();
            if (value.HasValue)
                SelectedEntities.Add(value.Value);
        }
    }

    public void ToggleSelection(ERus.Engine.ECS.Entity entity)
    {
        if (!SelectedEntities.Remove(entity))
            SelectedEntities.Add(entity);
    }

    public void Select(ERus.Engine.ECS.Entity entity, bool additive)
    {
        if (additive)
        {
            SelectedEntities.Add(entity);
        }
        else
        {
            SelectedEntities.Clear();
            SelectedEntities.Add(entity);
        }
    }

    public void ClearSelection()
    {
        SelectedEntities.Clear();
    }

    // --- Undo/Redo ---
    public UndoSystem UndoSystem { get; } = new UndoSystem(100);

    public EditorUIController(ERus.Engine.Core.Engine engine)
    {
        _engine = engine;
        _windowManager = new EditorWindowManager(this, _engine);
        _toolbar = new EditorToolbar(this, _engine);
    }

    public void Initialize(IWindow window, IInputContext input, GL gl)
    {
        _imGuiController = new ImGuiController(gl, window, input);

        var io = ImGui.GetIO();
        io.ConfigFlags |= ImGuiConfigFlags.DockingEnable; 
        io.ConfigFlags |= ImGuiConfigFlags.ViewportsEnable; 
        
        LoadFonts(io);
        ApplyDarkTheme();
    }

    private unsafe void LoadFonts(ImGuiIOPtr io)
    {
        io.Fonts.AddFontDefault();
        string fontPath = System.IO.Path.Combine("Assets", "Fonts", "fa-solid-900.ttf");
        if (System.IO.File.Exists(fontPath))
        {
            ImFontConfig* config = ImGuiNative.ImFontConfig_ImFontConfig();
            config->MergeMode = 1;
            config->PixelSnapH = 1;
            config->GlyphMinAdvanceX = 14.0f;
            config->GlyphMaxAdvanceX = 14.0f;

            ushort* ranges = stackalloc ushort[] { 0xe000, 0xf8ff, 0 };
            io.Fonts.AddFontFromFileTTF(fontPath, 14.0f, config, (IntPtr)ranges);
            
            ImGuiNative.ImFontConfig_destroy(config);
        }
    }

    private void ApplyDarkTheme()
    {
        ImGui.StyleColorsDark();

        var style = ImGui.GetStyle();
        style.WindowRounding = 6.0f;
        style.ChildRounding = 4.0f;
        style.FrameRounding = 4.0f;
        style.PopupRounding = 4.0f;
        style.ScrollbarRounding = 4.0f;
        style.GrabRounding = 4.0f;
        style.TabRounding = 4.0f;

        style.WindowBorderSize = 0.0f;
        style.FrameBorderSize = 0.0f;
        style.PopupBorderSize = 1.0f;

        style.Colors[(int)ImGuiCol.WindowBg] = new Vector4(0.12f, 0.12f, 0.12f, 1.00f);
        style.Colors[(int)ImGuiCol.Header] = new Vector4(0.20f, 0.20f, 0.20f, 1.00f);
        style.Colors[(int)ImGuiCol.HeaderHovered] = new Vector4(0.30f, 0.30f, 0.30f, 1.00f);
        style.Colors[(int)ImGuiCol.HeaderActive] = new Vector4(0.25f, 0.25f, 0.25f, 1.00f);
        style.Colors[(int)ImGuiCol.Button] = new Vector4(0.20f, 0.20f, 0.20f, 1.00f);
        style.Colors[(int)ImGuiCol.ButtonHovered] = new Vector4(0.30f, 0.30f, 0.30f, 1.00f);
        style.Colors[(int)ImGuiCol.ButtonActive] = new Vector4(0.25f, 0.25f, 0.25f, 1.00f);
        style.Colors[(int)ImGuiCol.FrameBg] = new Vector4(0.16f, 0.16f, 0.16f, 1.00f);
        style.Colors[(int)ImGuiCol.FrameBgHovered] = new Vector4(0.24f, 0.24f, 0.24f, 1.00f);
        style.Colors[(int)ImGuiCol.FrameBgActive] = new Vector4(0.20f, 0.20f, 0.20f, 1.00f);
        style.Colors[(int)ImGuiCol.Tab] = new Vector4(0.12f, 0.12f, 0.12f, 1.00f);
        style.Colors[(int)ImGuiCol.TabHovered] = new Vector4(0.24f, 0.24f, 0.24f, 1.00f);
        style.Colors[(int)ImGuiCol.TabActive] = new Vector4(0.20f, 0.20f, 0.20f, 1.00f);
        style.Colors[(int)ImGuiCol.TitleBg] = new Vector4(0.10f, 0.10f, 0.10f, 1.00f);
        style.Colors[(int)ImGuiCol.TitleBgActive] = new Vector4(0.15f, 0.15f, 0.15f, 1.00f);
        style.Colors[(int)ImGuiCol.MenuBarBg] = new Vector4(0.10f, 0.10f, 0.10f, 1.00f);
    }

    public void Update(double deltaTime)
    {
        _imGuiController.Update((float)deltaTime);
    }

    public void Render()
    {
        var io = ImGui.GetIO();

        if (!_layoutApplied)
        {
            _layoutApplied = true;
            ApplyDefaultLayoutIfNeeded();
        }

        if (_requestLoadUnityLayout)
        {
            _requestLoadUnityLayout = false;
            LoadUnityLayout();
        }

        // --- Atalhos Globais ---
        HandleShortcuts(io);

        _toolbar.Draw();

        uint dockspaceId = ImGui.GetID("EditorDockSpace");
        ImGui.DockSpaceOverViewport(dockspaceId, ImGui.GetMainViewport(), ImGuiDockNodeFlags.None);

        _windowManager.DrawWindows();

        _imGuiController.Render();
    }

    private void HandleShortcuts(ImGuiIOPtr io)
    {
        if (io.WantTextInput) return;

        bool ctrl = io.KeyCtrl;
        bool shift = io.KeyShift;

        // Undo / Redo
        if (ctrl && !shift && ImGui.IsKeyPressed(ImGuiKey.Z))
            UndoSystem.Undo();
        else if (ctrl && shift && ImGui.IsKeyPressed(ImGuiKey.Z))
            UndoSystem.Redo();

        // Delete Entidades
        if (ImGui.IsKeyPressed(ImGuiKey.Delete) && SelectedEntity != null)
        {
            var ecs = _engine.GetModule<ERus.Engine.Modules.ECSModule>();
            var netModule = _engine.GetModule<ERus.Engine.Modules.NetworkModule>();

            foreach (var entity in SelectedEntities.ToList())
            {
                if (netModule != null && ecs.ActiveScene.Registry.HasComponent<ERus.Engine.ECS.NetworkIdentityComponent>(entity))
                {
                    var netId = ecs.ActiveScene.Registry.GetComponent<ERus.Engine.ECS.NetworkIdentityComponent>(entity).NetworkId;
                    netModule.Replication?.SendDestroy(netId);
                }

                ecs.ActiveScene.Registry.DestroyEntity(entity);
            }

            ClearSelection();
        }
    }

    public void RequestLoadUnityLayout()
    {
        _requestLoadUnityLayout = true;
    }

    private void ApplyDefaultLayoutIfNeeded()
    {
        string iniPath = System.IO.Path.Combine(System.Environment.CurrentDirectory, "imgui.ini");
        
        bool needsLayout = false;
        
        if (!System.IO.File.Exists(iniPath))
        {
            needsLayout = true;
        }
        else
        {
            string content = System.IO.File.ReadAllText(iniPath);
            string[] requiredDockedWindows = { "Inspector", "Project", "Console" };
            foreach (var windowName in requiredDockedWindows)
            {
                int windowIdx = content.IndexOf($"[Window][{windowName}]");
                if (windowIdx < 0)
                {
                    needsLayout = true;
                    break;
                }
                int nextSection = content.IndexOf("[Window]", windowIdx + 1);
                int dockingSection = content.IndexOf("[Docking]", windowIdx + 1);
                int sectionEnd = content.Length;
                if (nextSection > 0) sectionEnd = Math.Min(sectionEnd, nextSection);
                if (dockingSection > 0) sectionEnd = Math.Min(sectionEnd, dockingSection);
                
                string windowSection = content.Substring(windowIdx, sectionEnd - windowIdx);
                if (!windowSection.Contains("DockId"))
                {
                    needsLayout = true;
                    break;
                }
            }
        }

        if (needsLayout)
        {
            LoadUnityLayout();
        }
    }

    private void LoadUnityLayout()
    {
        string iniPath = System.IO.Path.Combine(System.Environment.CurrentDirectory, "imgui.ini");
        
        var viewport = ImGui.GetMainViewport();
        int viewportW = (int)viewport.Size.X;
        int viewportH = (int)viewport.Size.Y;
        
        int menuBarH = 37; 
        int contentH = viewportH - menuBarH;
        int hierarchyW = (int)(viewportW * 0.18f); 
        int inspectorW = (int)(viewportW * 0.22f); 
        int centerW = viewportW - hierarchyW - inspectorW;
        int sceneH = (int)(contentH * 0.65f); 
        int bottomH = contentH - sceneH; 

        uint dockspaceId = ImGui.GetID("EditorDockSpace");
        string dockspaceHex = $"0x{dockspaceId:X8}";
        
        string iniContent = $@"[Window][WindowOverViewport_11111111]
Pos=0,{menuBarH}
Size={viewportW},{contentH}
Collapsed=0

[Window][Debug##Default]
Pos=60,60
Size=400,400
Collapsed=0

[Window][Hierarchy]
Pos=0,{menuBarH}
Size={hierarchyW},{contentH}
Collapsed=0
DockId=0x00000001,0

[Window][Inspector]
Pos={hierarchyW + centerW},{menuBarH}
Size={inspectorW},{contentH}
Collapsed=0
DockId=0x00000002,0

[Window][Scene]
Pos={hierarchyW},{menuBarH}
Size={centerW},{sceneH}
Collapsed=0
DockId=0x00000003,0

[Window][Game]
Pos={hierarchyW},{menuBarH}
Size={centerW},{sceneH}
Collapsed=0
DockId=0x00000003,1

[Window][Project]
Pos={hierarchyW},{menuBarH + sceneH}
Size={centerW},{bottomH}
Collapsed=0
DockId=0x00000004,0

[Window][Console]
Pos={hierarchyW},{menuBarH + sceneH}
Size={centerW},{bottomH}
Collapsed=0
DockId=0x00000004,1

[Docking][Data]
DockSpace   ID={dockspaceHex} Pos=0,{menuBarH} Size={viewportW},{contentH} Split=X
  DockNode  ID=0x00000001 Parent={dockspaceHex} SizeRef={hierarchyW},{contentH} Selected=0x29EABFBD
  DockNode  ID=0x00000005 Parent={dockspaceHex} SizeRef={viewportW - hierarchyW},{contentH} Split=X
    DockNode  ID=0x00000006 Parent=0x00000005 SizeRef={centerW},{contentH} Split=Y
      DockNode  ID=0x00000003 Parent=0x00000006 SizeRef={centerW},{sceneH} Selected=0xE192E354
      DockNode  ID=0x00000004 Parent=0x00000006 SizeRef={centerW},{bottomH} Selected=0x65CC51DC
    DockNode  ID=0x00000002 Parent=0x00000005 SizeRef={inspectorW},{contentH} Selected=0xE7039252
";
        System.IO.File.WriteAllText(iniPath, iniContent);
        ImGui.LoadIniSettingsFromDisk(iniPath);
    }

    public void Dispose()
    {
        _imGuiController?.Dispose();
    }
}



