using System;
using System.Collections.Concurrent;
using ImGuiNET;
using System.Numerics;
using ERus.Engine.ECS;
using ERus.Engine.Modules;
using ERus.Engine.Network.Replication;
using ERus.Editor.EditorUI.Managers;

namespace ERus.Editor.EditorUI.Inspector;

/// <summary>
/// Contexto de uma unica passada de desenho do Inspector.
/// Carrega a entidade selecionada e concentra os helpers de ImGui, assets,
/// rede e undo que os <see cref="IComponentDrawer"/> compartilham.
/// </summary>
public sealed class InspectorContext
{
    public ERus.Engine.Core.Engine Engine { get; }
    public Registry Registry { get; }
    public Entity Entity { get; }

    private readonly InspectorUndoTracker _undoTracker;
    private readonly ConcurrentQueue<Action> _mainThreadActions;

    public InspectorContext(
        ERus.Engine.Core.Engine engine,
        Registry registry,
        Entity entity,
        InspectorUndoTracker undoTracker,
        ConcurrentQueue<Action> mainThreadActions)
    {
        Engine = engine;
        Registry = registry;
        Entity = entity;
        _undoTracker = undoTracker;
        _mainThreadActions = mainThreadActions;
    }

    // --- Rede ---------------------------------------------------------------

    public NetworkModule? Network => Engine.GetModule<NetworkModule>();

    public EntityReplicationSystem? Replication => Network?.Replication;

    public bool TryGetNetworkId(out int networkId) => TryGetNetworkId(Entity, out networkId);

    public bool TryGetNetworkId(Entity entity, out int networkId)
    {
        networkId = -1;
        if (!Registry.HasComponentByType(entity, typeof(NetworkIdentityComponent))) return false;
        networkId = Registry.GetComponent<NetworkIdentityComponent>(entity).NetworkId;
        return true;
    }

    /// <summary>
    /// Verifica se a entidade esta travada por outro usuario na sessao colaborativa.
    /// </summary>
    public bool IsLockedByOtherUser()
    {
        if (!Registry.HasComponentByType(Entity, typeof(NetworkIdentityComponent))) return false;

        var netId = Registry.GetComponent<NetworkIdentityComponent>(Entity);
        if (netId.LockUserId == -1) return false;

        var manager = Network?.NetworkManager;
        if (manager == null) return false;

        return netId.LockUserId != manager.MyUserId;
    }

    /// <summary>
    /// Publica o asset na rede e executa o callback na main thread quando o hash estiver pronto.
    /// </summary>
    public void AnnounceAsset(string assetPath, Action<string> onHashReady)
    {
        var netModule = Network;
        if (netModule == null) return;

        _ = netModule.NetworkManager?.AssetSync?.AnnounceAssetAsync(assetPath, hash =>
        {
            _mainThreadActions.Enqueue(() => onHashReady(hash));
        });
    }

    public void EnqueueMainThread(Action action) => _mainThreadActions.Enqueue(action);

    // --- Undo ---------------------------------------------------------------

    /// <summary>
    /// Registra um comando de undo para o widget desenhado imediatamente antes desta chamada.
    /// </summary>
    public void TrackUndo(string propertyName) => _undoTracker.Track(propertyName, Registry, Entity);

    // --- Assets -------------------------------------------------------------

    public string GetAssetPath(Guid guid, string emptyLabel = "(Nenhum)", string missingLabel = "(Desconhecido / Falta)")
    {
        if (guid == Guid.Empty) return emptyLabel;
        return Engine.AssetDatabase.GetPathByGuid(guid) ?? missingLabel;
    }

    public Guid? ResolveAssetGuid(string path) => Engine.AssetDatabase.GetGuidByPath(path);

    /// <summary>
    /// Aceita um drop de asset sobre o ultimo item desenhado, filtrando por extensao.
    /// </summary>
    public static bool TryAcceptAssetDrop(string[] extensions, out string droppedPath)
    {
        droppedPath = string.Empty;
        if (!ImGui.BeginDragDropTarget()) return false;

        bool accepted = false;
        var payload = ImGui.AcceptDragDropPayload("ASSET_PATH");
        unsafe
        {
            if (payload.NativePtr != null)
            {
                string dropped = DragDropState.DraggedPayload;
                foreach (var ext in extensions)
                {
                    if (dropped.EndsWith(ext, StringComparison.OrdinalIgnoreCase))
                    {
                        droppedPath = dropped;
                        accepted = true;
                        break;
                    }
                }
            }
        }
        ImGui.EndDragDropTarget();
        return accepted;
    }

    // --- Layout de propriedades --------------------------------------------

    public static bool BeginPropertyTable(string id, float labelWidth = 100.0f)
    {
        if (!ImGui.BeginTable(id, 2, ImGuiTableFlags.BordersInnerV | ImGuiTableFlags.SizingStretchProp))
            return false;

        ImGui.TableSetupColumn("Property", ImGuiTableColumnFlags.WidthFixed, labelWidth);
        ImGui.TableSetupColumn("Value", ImGuiTableColumnFlags.WidthStretch);
        return true;
    }

    /// <summary>
    /// Abre a linha de uma propriedade. O chamador deve fazer PopItemWidth apos o widget.
    /// </summary>
    public static void PropertyLabel(string label)
    {
        ImGui.TableNextRow();
        ImGui.TableNextColumn();
        ImGui.TextUnformatted(label);
        ImGui.TableNextColumn();
        ImGui.PushItemWidth(-1);
    }

    /// <summary>
    /// DragFloat3 sobre um Vector3D do Silk, convertendo de/para System.Numerics.
    /// Usa o padrao valor/out porque os componentes expoem propriedades, nao campos —
    /// nao da para passar <c>ref</c> para elas.
    /// </summary>
    public static bool DragVector3(string id, Silk.NET.Maths.Vector3D<float> value,
        out Silk.NET.Maths.Vector3D<float> result, float speed = 0.1f)
    {
        var tmp = new Vector3(value.X, value.Y, value.Z);
        if (!ImGui.DragFloat3(id, ref tmp, speed))
        {
            result = value;
            return false;
        }
        result = new Silk.NET.Maths.Vector3D<float>(tmp.X, tmp.Y, tmp.Z);
        return true;
    }

    // --- Botoes -------------------------------------------------------------

    /// <summary>Botao vermelho-escuro usado para remover um componente.</summary>
    public static bool RemoveComponentButton(string label)
    {
        ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.7f, 0.3f, 0.2f, 1.0f));
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.8f, 0.4f, 0.3f, 1.0f));
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(0.9f, 0.5f, 0.4f, 1.0f));
        bool pressed = ImGui.Button(label, new Vector2(-1, 22));
        ImGui.PopStyleColor(3);
        return pressed;
    }

    /// <summary>Botao vermelho usado para acoes destrutivas sobre a entidade.</summary>
    public static bool DestructiveButton(string label)
    {
        ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.8f, 0.2f, 0.2f, 1.0f));
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.9f, 0.3f, 0.3f, 1.0f));
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(1.0f, 0.4f, 0.4f, 1.0f));
        bool pressed = ImGui.Button(label, new Vector2(-1, 30));
        ImGui.PopStyleColor(3);
        return pressed;
    }
}
