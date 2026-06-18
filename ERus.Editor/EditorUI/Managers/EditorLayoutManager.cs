using System;
using Silk.NET.Windowing;
using ImGuiNET;

namespace ERus.Editor.EditorUI.Managers;

public class EditorLayoutManager
{
    public void ApplyDefaultLayoutIfNeeded()
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

    public void LoadUnityLayout()
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
}
