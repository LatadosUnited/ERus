---
name: erus-imgui-window
description: Use this skill when you need to create a new Editor Window, Panel, or Tool for the ERus Editor using ImGuiNET.
---

# ERus ImGui Editor Window

The ERus Editor is built using `ImGuiNET`. To add a new panel or window, follow these steps:

## 1. Create the Window Class
Create a class that inherits from `EditorWindow`.
- Location: `ERus.Editor/EditorUI/Panels/`

```csharp
using ImGuiNET;

namespace ERus.Editor.EditorUI.Panels;

public class MyCustomToolWindow : EditorWindow
{
    private readonly EditorUIController _controller;

    public MyCustomToolWindow(EditorUIController controller) : base("My Custom Tool")
    {
        _controller = controller;
    }

    protected override void DrawContent()
    {
        ImGui.Text("Hello World!");
        if (ImGui.Button("Click Me"))
        {
            // Do something
        }
    }
}
```

## 2. Register the Window in the UI Controller
You must add the window to the `EditorUIController` so it gets drawn during the render loop.
- Location: `ERus.Editor/EditorUI/EditorUIController.cs`

Inside the `EditorUIController` constructor:
```csharp
public MyCustomToolWindow MyTool { get; }

public EditorUIController(Engine.Core.Engine engine, IWindow window)
{
    // ...
    MyTool = new MyCustomToolWindow(this);
    // Add to the _windows list so it's managed automatically:
    _windows.Add(MyTool);
}
```

## 3. Add to the Menu Bar (Optional)
If users need to toggle this window on/off, add it to `EditorToolbar.cs`.
- Location: `ERus.Editor/EditorUI/Managers/EditorToolbar.cs`

```csharp
// Inside Draw() -> BeginMainMenuBar() -> BeginMenu("Window")
if (ImGui.MenuItem("My Custom Tool"))
{
    _controller.MyTool.IsOpen = true;
}
```
