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

    private EditorLayoutManager _layoutManager;
    private EditorInputHandler _inputHandler;

    // Layout
    private bool _layoutApplied = false;
    private bool _requestLoadUnityLayout = false;

    // --- Undo/Redo ---
    public UndoSystem UndoSystem { get; } = new UndoSystem(100);

    public EditorUIController(ERus.Engine.Core.Engine engine)
    {
        _engine = engine;
        _windowManager = new EditorWindowManager(this, _engine);
        _toolbar = new EditorToolbar(this, _engine);
        _layoutManager = new EditorLayoutManager();
        _inputHandler = new EditorInputHandler(_engine, UndoSystem);
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
            _layoutManager.ApplyDefaultLayoutIfNeeded();
        }

        if (_requestLoadUnityLayout)
        {
            _requestLoadUnityLayout = false;
            _layoutManager.LoadUnityLayout();
        }

        // --- Atalhos Globais ---
        _inputHandler.HandleShortcuts(io);

        _toolbar.Draw();

        uint dockspaceId = ImGui.GetID("EditorDockSpace");
        ImGui.DockSpaceOverViewport(dockspaceId, ImGui.GetMainViewport(), ImGuiDockNodeFlags.None);

        _windowManager.DrawWindows();

        _imGuiController.Render();
    }

    public void RequestLoadUnityLayout()
    {
        _requestLoadUnityLayout = true;
    }

    public void Dispose()
    {
        _imGuiController?.Dispose();
    }
}



