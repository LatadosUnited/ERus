using ImGuiNET;
using System;
using System.Numerics;

namespace ERus.Editor.EditorUI.Managers;

public class EditorNetworkMenu
{
    private ERus.Engine.Core.Engine _engine;

    private string _netIp = "127.0.0.1";
    private string _netPort = "9050";
    private string _netSessionToken = "";
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
            ImGui.InputText("Session Token", ref _netSessionToken, 64, ImGuiInputTextFlags.Password);

            if (string.IsNullOrWhiteSpace(_netSessionToken))
            {
                ImGui.TextColored(new Vector4(1.0f, 0.7f, 0.2f, 1.0f), "Sem token: sessão aberta a qualquer cliente.");
            }

            ImGui.Separator();

            var networkModule = _engine.GetModule<ERus.Engine.Modules.NetworkModule>();
            if (networkModule != null)
            {
                if (ImGui.Button("Host"))
                {
                    if (int.TryParse(_netPort, out int port))
                    {
                        networkModule.StartHost(port, _netSessionToken);
                        _netStatus = $"Hospedando na porta {port}";
                    }
                }
                ImGui.SameLine();
                if (ImGui.Button("Connect"))
                {
                    if (int.TryParse(_netPort, out int port))
                    {
                        networkModule.StartClient(_netIp, port, _netSessionToken);
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


