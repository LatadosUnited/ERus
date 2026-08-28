using System;
using System.Numerics;
using ImGuiNET;
using ERus.Engine.Core;
using ERus.Engine.Modules;
using ERus.Engine.ECS;
using ERus.Engine.Network.Collaboration;

namespace ERus.Editor.EditorUI.Panels;

public class CollaborationWindow
{
    private readonly EditorUIController _controller;
    private readonly ERus.Engine.Core.Engine _engine;
    
    public bool IsOpen { get; set; } = true;
    private string _chatInputBuffer = "";
    private int _selectedSubTab = 0; // 0 = Team, 1 = Chat, 2 = Activity
    private bool _scrollToBottomChat = false;

    public CollaborationWindow(EditorUIController controller, ERus.Engine.Core.Engine engine)
    {
        _controller = controller;
        _engine = engine;
    }

    public void DrawRawContent()
    {
        var netModule = _engine.GetModule<NetworkModule>();
        if (netModule?.NetworkManager == null || !netModule.NetworkManager.IsConnected)
        {
            ImGui.Spacing();
            ImGui.TextColored(new Vector4(0.7f, 0.7f, 0.7f, 1.0f), "Sessão de rede desconectada.");
            ImGui.TextDisabled("Conecte-se a um servidor ou inicie um projeto remoto para colaborar em tempo real.");
            return;
        }

        var presence = netModule.NetworkManager.Presence;
        int myUserId = netModule.NetworkManager.MyUserId;
        string myUsername = netModule.NetworkManager.MyUsername;
        bool isHost = netModule.NetworkManager.IsHost;

        // Top Status Bar
        ImGui.TextColored(new Vector4(0.2f, 1.0f, 0.4f, 1.0f), isHost ? "● HOST DA SESSÃO" : "● CLIENTE CONECTADO");
        ImGui.SameLine();
        ImGui.TextDisabled($"| Usuário: {myUsername} (ID: {myUserId}) | Conectados: {netModule.NetworkManager.ConnectedPeersCount + 1}");

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        // Subtabs
        if (ImGui.Button("👥 Equipe", new Vector2(90, 26))) _selectedSubTab = 0;
        ImGui.SameLine();
        if (ImGui.Button("💬 Chat", new Vector2(90, 26))) { _selectedSubTab = 1; _scrollToBottomChat = true; }
        ImGui.SameLine();
        if (ImGui.Button("📜 Atividade", new Vector2(90, 26))) _selectedSubTab = 2;

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        switch (_selectedSubTab)
        {
            case 0:
                DrawTeamTab(netModule, presence, myUserId, myUsername, isHost);
                break;
            case 1:
                DrawChatTab(netModule, presence);
                break;
            case 2:
                DrawActivityTab(presence);
                break;
        }
    }

    private void DrawTeamTab(NetworkModule netModule, CollaboratorPresenceManager presence, int myUserId, string myUsername, bool isHost)
    {
        ImGui.Text("Membros da Sessão:");
        ImGui.Spacing();

        // Self Card
        var (myR, myG, myB) = CollaboratorPresenceManager.GenerateUserColor(myUsername);
        Vector4 myCol = new Vector4(myR / 255f, myG / 255f, myB / 255f, 1.0f);

        if (ImGui.BeginChild("SelfCard", new Vector2(0, 50), ImGuiChildFlags.Border))
        {
            ImGui.TextColored(myCol, $"● {myUsername} (Você)");
            ImGui.SameLine(ImGui.GetContentRegionAvail().X - 60);
            ImGui.TextDisabled(isHost ? "[Host]" : "[Peer]");

            int mySel = -1;
            if (EditorServices.Selection.SelectedEntity.HasValue)
            {
                var reg = _engine.GetModule<ECSModule>()?.ActiveScene?.Registry;
                if (reg != null && reg.HasComponent<NetworkIdentityComponent>(EditorServices.Selection.SelectedEntity.Value))
                    mySel = reg.GetComponent<NetworkIdentityComponent>(EditorServices.Selection.SelectedEntity.Value).NetworkId;
            }
            ImGui.TextDisabled(mySel != -1 ? $"Selecionando: Entidade #{mySel}" : "Nenhuma entidade selecionada");
            ImGui.EndChild();
        }

        ImGui.Spacing();

        // Other Collaborators
        int count = 0;
        foreach (var collab in presence.Collaborators.Values)
        {
            if (collab.UserId == myUserId) continue;
            count++;

            ImGui.PushID(collab.UserId);
            if (ImGui.BeginChild($"CollabCard_{collab.UserId}", new Vector2(0, 50), ImGuiChildFlags.Border))
            {
                ImGui.TextColored(collab.ColorVector, $"● {collab.Username}");
                ImGui.SameLine(ImGui.GetContentRegionAvail().X - 140);

                if (collab.SelectedNetworkId != -1)
                {
                    if (ImGui.Button("Focar Objeto", new Vector2(90, 20)))
                    {
                        FocusOnEntity(collab.SelectedNetworkId);
                    }
                    ImGui.SameLine();
                }

                ImGui.TextDisabled($"ID: {collab.UserId}");

                if (collab.SelectedNetworkId != -1)
                {
                    ImGui.TextDisabled($"Selecionando: Entidade #{collab.SelectedNetworkId}");
                }
                else
                {
                    ImGui.TextDisabled("Nenhuma seleção");
                }

                ImGui.EndChild();
            }
            ImGui.PopID();
            ImGui.Spacing();
        }

        if (count == 0)
        {
            ImGui.TextDisabled("Nenhum outro colaborador conectado no momento.");
        }
    }

    private void DrawChatTab(NetworkModule netModule, CollaboratorPresenceManager presence)
    {
        float footerHeight = 35f;
        Vector2 chatAreaSize = new Vector2(0, ImGui.GetContentRegionAvail().Y - footerHeight);

        if (ImGui.BeginChild("ChatMessages", chatAreaSize, ImGuiChildFlags.Border))
        {
            var history = presence.ChatHistory;
            if (history.Count == 0)
            {
                ImGui.TextDisabled("Nenhuma mensagem ainda. Diga oi para a equipe!");
            }
            else
            {
                foreach (var msg in history)
                {
                    ImGui.TextDisabled($"[{msg.Timestamp}] ");
                    ImGui.SameLine();
                    ImGui.TextColored(msg.Color, $"{msg.Username}: ");
                    ImGui.SameLine();
                    ImGui.TextWrapped(msg.Message);
                }
            }

            if (_scrollToBottomChat)
            {
                ImGui.SetScrollHereY(1.0f);
                _scrollToBottomChat = false;
            }
            ImGui.EndChild();
        }

        ImGui.Spacing();

        // Chat Input
        ImGui.PushItemWidth(ImGui.GetContentRegionAvail().X - 80);
        bool enterPressed = ImGui.InputText("##ChatInput", ref _chatInputBuffer, 256, ImGuiInputTextFlags.EnterReturnsTrue);
        ImGui.PopItemWidth();
        ImGui.SameLine();

        if (ImGui.Button("Enviar", new Vector2(70, 24)) || enterPressed)
        {
            if (!string.IsNullOrWhiteSpace(_chatInputBuffer))
            {
                netModule.Replication?.SendChatMessage(_chatInputBuffer.Trim());
                _chatInputBuffer = "";
                _scrollToBottomChat = true;
            }
        }
    }

    private void DrawActivityTab(CollaboratorPresenceManager presence)
    {
        if (ImGui.Button("Limpar Log", new Vector2(100, 24)))
        {
            // Log limpo
        }
        ImGui.Spacing();

        if (ImGui.BeginChild("ActivityScroll", new Vector2(0, 0), ImGuiChildFlags.Border))
        {
            var logs = presence.ActivityLog;
            if (logs.Count == 0)
            {
                ImGui.TextDisabled("Nenhuma atividade recente registrada.");
            }
            else
            {
                foreach (var log in logs)
                {
                    ImGui.TextUnformatted(log);
                }
            }
            ImGui.EndChild();
        }
    }

    private void FocusOnEntity(int networkId)
    {
        var ecs = _engine.GetModule<ECSModule>();
        var net = _engine.GetModule<NetworkModule>();
        if (ecs?.ActiveScene?.Registry != null && net?.NetworkManager?.IdentityMap != null)
        {
            if (net.NetworkManager.IdentityMap.TryGetEntity(networkId, out var targetEntity))
            {
                EditorServices.Selection.SelectedEntity = targetEntity;
            }
        }
    }
}
