using System;
using Silk.NET.Input;
using Silk.NET.OpenGL;
using Silk.NET.OpenGL.Extensions.ImGui;
using Silk.NET.Windowing;
using Silk.NET.Maths;

namespace ERus.Hub;

class Program
{
    private static IWindow _window = null!;
    private static GL _gl = null!;
    private static ImGuiController _imGuiController = null!;
    private static IInputContext _inputContext = null!;
    private static HubUI _hubUI = null!;

    static void Main(string[] args)
    {
        var options = WindowOptions.Default;
        options.Size = new Vector2D<int>(800, 600);
        options.Title = "ERus Hub";

        _window = Window.Create(options);

        _window.Load += OnLoad;
        _window.Render += OnRender;
        _window.Update += OnUpdate;
        _window.Closing += OnClose;
        _window.Resize += OnResize;

        _window.Run();
        _window.Dispose();
    }

    private static void OnLoad()
    {
        _gl = _window.CreateOpenGL();
        _inputContext = _window.CreateInput();
        _imGuiController = new ImGuiController(_gl, _window, _inputContext);
        
        _hubUI = new HubUI();
        
        _gl.ClearColor(0.1f, 0.11f, 0.13f, 1.0f);
    }

    private static void OnUpdate(double deltaTime)
    {
        _imGuiController.Update((float)deltaTime);
    }

    private static void OnRender(double deltaTime)
    {
        _gl.Clear(ClearBufferMask.ColorBufferBit);

        _hubUI.Draw();

        _imGuiController.Render();
    }

    private static void OnResize(Vector2D<int> size)
    {
        _gl.Viewport(size);
    }

    private static void OnClose()
    {
        _imGuiController?.Dispose();
        _inputContext?.Dispose();
        _gl?.Dispose();
    }
}
