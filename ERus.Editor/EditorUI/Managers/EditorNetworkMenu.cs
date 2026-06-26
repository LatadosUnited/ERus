using ImGuiNET;
using System;
using System.Numerics;

namespace ERus.Editor.EditorUI.Managers;

public class EditorNetworkMenu
{
    private ERus.Engine.Core.Engine _engine;

    private string _netIp = "127.0.0.1";
    private string _netPort = "9050";
    private string _netStatus = "Offline";

    public EditorNetworkMenu(ERus.Engine.Core.Engine engine)
    {
        _engine = engine;
    }

    public void Draw()
    {
        if (ImGui.BeginMenu("Network"))
        {
            ImGui.TextColored(new Vector4(0.5f, 0.8f, 1.0f, 1.0f), $"Status: {_netStatus}");
            ImGui.Separator();
            
            ImGui.InputText("IP", ref _netIp, 32);
            ImGui.InputText("Port", ref _netPort, 6);
            
            ImGui.Separator();

            var networkModule = _engine.GetModule<ERus.Engine.Modules.NetworkModule>();
            if (networkModule != null)
            {
                if (ImGui.Button("Connect"))
                {
                    if (int.TryParse(_netPort, out int port))
                    {
                        networkModule.StartClient(_netIp, port);
                        _netStatus = $"Conectado a {_netIp}:{port}";
                    }
                }
                ImGui.Separator();
                if (ImGui.Button("Disconnect"))
                {
                    networkModule.Disconnect();
                    _netStatus = "Offline";
                }
            }
            ImGui.EndMenu();
        }
    }
}


