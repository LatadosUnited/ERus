using System;
using System.Linq;
using System.Runtime.InteropServices;
using ImGuiNET;
using ERus.Engine.Core;
using ERus.Engine.Modules;
using ERus.Engine.ECS;

namespace ERus.Editor.EditorUI.Panels;

public class HierarchyWindow : EditorWindow
{
    private readonly EditorUIController _controller;
    private readonly ERus.Engine.Core.Engine _engine;

    private Entity? _lastClickedEntity = null;

    public HierarchyWindow(EditorUIController controller, ERus.Engine.Core.Engine engine) : base("Hierarchy") 
    {
        _controller = controller;
        _engine = engine;
    }

    protected override void DrawContent()
    {
        var ecsModule = _engine.GetModule<ECSModule>();
        if (ecsModule == null)
        {
            ImGui.Text("ECS Module Offline");
            return;
        }

        var registry = ecsModule.ActiveScene.Registry;
        var entities = registry.GetLivingEntities();
        var io = ImGui.GetIO();

        if (EditorServices.Selection.SelectedEntities.Count > 1)
        {
            ImGui.TextColored(
                new System.Numerics.Vector4(0.5f, 0.8f, 1.0f, 1.0f),
                $"{EditorServices.Selection.SelectedEntities.Count} entidades selecionadas");
            ImGui.Separator();
        }

        // Scroll area para a lista de entidades
        if (ImGui.BeginChild("HierarchyScrollArea", new System.Numerics.Vector2(0, 0), ImGuiChildFlags.None, ImGuiWindowFlags.NoBackground))
        {
            // Encontrar nós raiz (sem parente)
            foreach (var entity in entities)
            {
                bool isRoot = true;
                if (registry.HasComponent<RelationshipComponent>(entity))
                {
                    isRoot = registry.GetComponent<RelationshipComponent>(entity).Parent == null;
                }

                if (isRoot)
                {
                    DrawEntityNode(entity, registry, io);
                }
            }

            // Área vazia para drop (un-parent) e clique para desselecionar
            float remainingH = ImGui.GetContentRegionAvail().Y;
            if (remainingH > 0)
            {
                ImGui.Dummy(new System.Numerics.Vector2(ImGui.GetContentRegionAvail().X, remainingH));
            }
            else
            {
                // Garante pelo menos um mínimo para drop target
                ImGui.Dummy(new System.Numerics.Vector2(ImGui.GetContentRegionAvail().X, 20));
            }

            // Clique na área vazia desseleção
            if (ImGui.IsItemClicked() && !io.KeyCtrl && !io.KeyShift)
            {
                EditorServices.Selection.ClearSelection();
            }

            // Drop target na área vazia para remover parente ou dropar Prefab
            if (ImGui.BeginDragDropTarget())
            {
                unsafe
                {
                    ImGuiPayloadPtr payload = ImGui.AcceptDragDropPayload("ENTITY");
                    if (payload.NativePtr != null)
                    {
                        int id = *(int*)payload.Data;
                        var droppedEntity = entities.FirstOrDefault(e => e.Id == id);
                        ERus.Engine.ECS.RelationshipSystem.SetParent(droppedEntity, null, registry);
                    }

                    ImGuiPayloadPtr assetPayload = ImGui.AcceptDragDropPayload("ASSET_PATH");
                    if (assetPayload.NativePtr != null)
                    {
                        string sourceFile = ERus.Editor.EditorUI.Managers.DragDropState.DraggedPayload;
                        if (System.IO.Path.GetExtension(sourceFile).Equals(".prefab", StringComparison.OrdinalIgnoreCase))
                        {
                            ERus.Engine.ECS.SceneSerializer.LoadPrefab(sourceFile, ecsModule.ActiveScene, null);
                        }
                    }
                }
                ImGui.EndDragDropTarget();
            }

            // Context Menu no escopo do Child (para clicar em área vazia da hierarquia)
            if (ImGui.BeginPopupContextWindow("HierarchyContextMenu", ImGuiPopupFlags.MouseButtonRight | ImGuiPopupFlags.NoOpenOverItems))
            {
                if (ImGui.MenuItem($"{FontAwesome.File} Empty Entity"))
                {
                    var newEntity = registry.CreateEntity();
                    registry.AddComponent(newEntity, new TransformComponent());
                    registry.AddComponent(newEntity, new TagComponent { Name = "Empty Entity" });
                    
                    var netModule = _engine.GetModule<NetworkModule>();
                    if (netModule != null && netModule.NetworkManager?.IsHost == true)
                    {
                        int netId = (netModule.NetworkManager?.IdentityMap.AssignNetworkId(registry, newEntity) ?? -1);
                        netModule.Replication?.SendSpawn(netId, "Empty Entity", 0);
                    }
                }

                if (ImGui.BeginMenu($"{FontAwesome.Cube} 3D Object"))
                {
                    if (ImGui.MenuItem("Cube"))
                    {
                        var newEntity = registry.CreateEntity();
                        registry.AddComponent(newEntity, new TransformComponent());
                        registry.AddComponent(newEntity, new TagComponent { Name = "Cube" });
                        registry.AddComponent(newEntity, new MeshComponent { Type = PrimitiveMeshType.Cube });
                        
                        var netModule = _engine.GetModule<NetworkModule>();
                        if (netModule != null && netModule.NetworkManager?.IsHost == true)
                        {
                            int netId = (netModule.NetworkManager?.IdentityMap.AssignNetworkId(registry, newEntity) ?? -1);
                            netModule.Replication?.SendSpawn(netId, "Cube", (int)PrimitiveMeshType.Cube);
                        }
                    }
                    ImGui.EndMenu();
                }

                if (ImGui.MenuItem($"{FontAwesome.Camera} Camera"))
                {
                    var newEntity = registry.CreateEntity();
                    registry.AddComponent(newEntity, new TransformComponent());
                    registry.AddComponent(newEntity, new TagComponent { Name = "Camera" });
                    registry.AddComponent(newEntity, new CameraComponent());
                    
                    var netModule = _engine.GetModule<NetworkModule>();
                    if (netModule != null && netModule.NetworkManager?.IsHost == true)
                    {
                        int netId = (netModule.NetworkManager?.IdentityMap.AssignNetworkId(registry, newEntity) ?? -1);
                        netModule.Replication?.SendSpawn(netId, "Camera", -1);
                    }
                }

                ImGui.EndPopup();
            }
        }
        ImGui.EndChild();
    }

    private void DrawEntityNode(Entity entity, Registry registry, ImGuiIOPtr io)
    {
        string name = $"Entity {entity.Id}";
        string icon = FontAwesome.CubeSolid;

        if (registry.HasComponent<TagComponent>(entity))
            name = registry.GetComponent<TagComponent>(entity).Name;

        if (registry.HasComponent<CameraComponent>(entity))
            icon = FontAwesome.Camera;
        else if (registry.HasComponent<MeshComponent>(entity))
            icon = FontAwesome.Cube;

        bool isSelected = EditorServices.Selection.SelectedEntities.Contains(entity);
        bool hasChildren = false;

        if (registry.HasComponent<RelationshipComponent>(entity))
        {
            hasChildren = registry.GetComponent<RelationshipComponent>(entity).FirstChild != null;
        }

        ImGuiTreeNodeFlags flags = ImGuiTreeNodeFlags.OpenOnArrow | ImGuiTreeNodeFlags.SpanAvailWidth | ImGuiTreeNodeFlags.FramePadding;
        if (isSelected) flags |= ImGuiTreeNodeFlags.Selected;
        if (!hasChildren) flags |= ImGuiTreeNodeFlags.Leaf;

        // Usa entity.Id + 1 para evitar IntPtr.Zero (que ocorre quando Id == 0)
        // IntPtr.Zero faz o ImGui ignorar o ID e usar hash interno, causando conflitos
        bool opened = ImGui.TreeNodeEx((IntPtr)(entity.Id + 1), flags, $"{icon} {name}");

        // Seleção ao clicar
        if (ImGui.IsItemClicked() && !ImGui.IsItemToggledOpen())
        {
            if (io.KeyCtrl)
                EditorServices.Selection.ToggleSelection(entity);
            else if (io.KeyShift && _lastClickedEntity.HasValue)
                EditorServices.Selection.ToggleSelection(entity);
            else
                EditorServices.Selection.SelectedEntity = entity;

            _lastClickedEntity = entity;
        }

        // Context menu por entidade (botão direito)
        if (ImGui.BeginPopupContextItem())
        {
            if (ImGui.MenuItem($"{FontAwesome.Copy} Duplicate"))
            {
                // TODO: Implementar duplicação
            }
            if (ImGui.MenuItem($"{FontAwesome.Trash} Delete"))
            {
                var netModule = _engine.GetModule<NetworkModule>();
                if (netModule != null && registry.HasComponent<NetworkIdentityComponent>(entity))
                {
                    var netId = registry.GetComponent<NetworkIdentityComponent>(entity).NetworkId;
                    netModule.Replication?.SendDestroy(netId);
                }
                registry.DestroyEntity(entity);
                EditorServices.Selection.SelectedEntities.Remove(entity);
            }
            ImGui.EndPopup();
        }

        unsafe
        {
            // Drag
            if (ImGui.BeginDragDropSource())
            {
                int id = entity.Id;
                ImGui.SetDragDropPayload("ENTITY", (IntPtr)(&id), sizeof(int));
                ImGui.Text($"{icon} {name}");
                ImGui.EndDragDropSource();
            }

            // Drop
            if (ImGui.BeginDragDropTarget())
            {
                ImGuiPayloadPtr payload = ImGui.AcceptDragDropPayload("ENTITY");
                if (payload.NativePtr != null)
                {
                    int droppedId = *(int*)payload.Data;
                    var droppedEntity = registry.GetLivingEntities().FirstOrDefault(e => e.Id == droppedId);
                    if (droppedEntity.Id != entity.Id)
                    {
                        ERus.Engine.ECS.RelationshipSystem.SetParent(droppedEntity, entity, registry);
                    }
                }

                ImGuiPayloadPtr assetPayload = ImGui.AcceptDragDropPayload("ASSET_PATH");
                if (assetPayload.NativePtr != null)
                {
                    string sourceFile = ERus.Editor.EditorUI.Managers.DragDropState.DraggedPayload;
                    if (System.IO.Path.GetExtension(sourceFile).Equals(".prefab", StringComparison.OrdinalIgnoreCase))
                    {
                        var ecsModule = _engine.GetModule<ECSModule>();
                        ERus.Engine.ECS.SceneSerializer.LoadPrefab(sourceFile, ecsModule.ActiveScene, entity);
                    }
                }
                ImGui.EndDragDropTarget();
            }
        }

        if (opened)
        {
            if (hasChildren)
            {
                var rel = registry.GetComponent<RelationshipComponent>(entity);
                Entity? currentChild = rel.FirstChild;
                while (currentChild != null)
                {
                    DrawEntityNode(currentChild.Value, registry, io);
                    if (registry.HasComponent<RelationshipComponent>(currentChild.Value))
                        currentChild = registry.GetComponent<RelationshipComponent>(currentChild.Value).NextSibling;
                    else
                        currentChild = null;
                }
            }
            ImGui.TreePop();
        }
    }
}
