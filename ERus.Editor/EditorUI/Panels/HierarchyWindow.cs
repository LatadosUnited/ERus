using System;
using System.Linq;
using System.Runtime.InteropServices;
using ImGuiNET;
using ERus.Engine.Core;
using ERus.Engine.Modules;
using ERus.Engine.ECS;
using ERus.Engine.Scripting;

namespace ERus.Editor.EditorUI.Panels;

public class HierarchyWindow : EditorWindow
{
    private readonly EditorUIController _controller;
    private readonly ERus.Engine.Core.Engine _engine;

    private Entity? _lastClickedEntity = null;
    private int? _renamingEntityId = null;
    private string _renamingBuffer = "";
    private string _renamingOriginalName = "";
    private bool _focusRenameInput = false;

    private void StartRenaming(Entity entity, string currentName)
    {
        _renamingEntityId = entity.Id;
        _renamingBuffer = currentName;
        _renamingOriginalName = currentName;
        _focusRenameInput = true;
    }

    private void ApplyRename(Entity entity, Registry registry)
    {
        if (_renamingEntityId != entity.Id) return;

        string newName = _renamingBuffer.Trim();
        if (string.IsNullOrWhiteSpace(newName))
        {
            newName = _renamingOriginalName;
        }

        if (registry.HasComponent<TagComponent>(entity))
        {
            ref var tag = ref registry.GetComponent<TagComponent>(entity);
            if (tag.Name != newName)
            {
                tag.Name = newName;
                var netModule = _engine.GetModule<NetworkModule>();
                if (netModule != null && registry.HasComponent<NetworkIdentityComponent>(entity))
                {
                    var netId = registry.GetComponent<NetworkIdentityComponent>(entity).NetworkId;
                    netModule.Replication?.SendRename(netId, newName);
                }
            }
        }
        else
        {
            registry.AddComponent(entity, new TagComponent { Name = newName });
            var netModule = _engine.GetModule<NetworkModule>();
            if (netModule != null && registry.HasComponent<NetworkIdentityComponent>(entity))
            {
                var netId = registry.GetComponent<NetworkIdentityComponent>(entity).NetworkId;
                netModule.Replication?.SendRename(netId, newName);
            }
        }

        _renamingEntityId = null;
    }

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

        if (ImGui.IsWindowFocused(ImGuiFocusedFlags.RootAndChildWindows) && ImGui.IsKeyPressed(ImGuiKey.F2))
        {
            if (EditorServices.Selection.SelectedEntity.HasValue)
            {
                var selEntity = EditorServices.Selection.SelectedEntity.Value;
                string currentName = $"Entity {selEntity.Id}";
                if (registry.HasComponent<TagComponent>(selEntity))
                    currentName = registry.GetComponent<TagComponent>(selEntity).Name;
                
                StartRenaming(selEntity, currentName);
            }
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
                        string beforeJson = ERus.Engine.ECS.SceneSerializer.SerializeEntityToJson(droppedEntity, registry);
                        ERus.Engine.ECS.RelationshipSystem.SetParent(droppedEntity, null, registry);
                        string afterJson = ERus.Engine.ECS.SceneSerializer.SerializeEntityToJson(droppedEntity, registry);
                        _controller.UndoSystem.Record(new ERus.Editor.EditorUI.Commands.EntityEditCommand(droppedEntity, registry, beforeJson, afterJson, "Unparent"));
                    }

                    ImGuiPayloadPtr assetPayload = ImGui.AcceptDragDropPayload("ASSET_PATH");
                    if (assetPayload.NativePtr != null)
                    {
                        string sourceFile = ERus.Editor.EditorUI.Managers.DragDropState.DraggedPayload;
                        string ext = System.IO.Path.GetExtension(sourceFile).ToLowerInvariant();
                        if (ext == ".prefab")
                        {
                            ERus.Engine.ECS.SceneSerializer.LoadPrefab(sourceFile, ecsModule.ActiveScene, null);
                        }
                        else if (ext == ".obj" || ext == ".fbx" || ext == ".gltf" || ext == ".glb")
                        {
                            TryInstantiate3DModel(sourceFile, registry, null);
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
                    _controller.UndoSystem.Record(new ERus.Editor.EditorUI.Commands.EntityLifecycleCommand(newEntity, registry, ERus.Editor.EditorUI.Commands.LifecycleAction.Create));
                }

                if (ImGui.BeginMenu($"{FontAwesome.Cube} 3D Object"))
                {
                    var shapes = new[] { 
                        PrimitiveMeshType.Cube, PrimitiveMeshType.Sphere, 
                        PrimitiveMeshType.Capsule, PrimitiveMeshType.Cylinder, 
                        PrimitiveMeshType.Plane, PrimitiveMeshType.Quad 
                    };

                    foreach (var shape in shapes)
                    {
                        if (ImGui.MenuItem(shape.ToString()))
                        {
                            var newEntity = registry.CreateEntity();
                            registry.AddComponent(newEntity, new TransformComponent());
                            registry.AddComponent(newEntity, new TagComponent { Name = shape.ToString() });
                            registry.AddComponent(newEntity, new MeshComponent { Type = shape });

                            // Add a default collider
                            if (shape == PrimitiveMeshType.Cube) { registry.AddComponent(newEntity, new BoxColliderComponent { Size = new Silk.NET.Maths.Vector3D<float>(1, 1, 1) }); }
                            else if (shape == PrimitiveMeshType.Sphere) { registry.AddComponent(newEntity, new SphereColliderComponent { Radius = 0.5f }); }
                            else if (shape == PrimitiveMeshType.Capsule) { registry.AddComponent(newEntity, new CapsuleColliderComponent { Radius = 0.5f, Height = 1.0f }); }
                            else if (shape == PrimitiveMeshType.Cylinder) { registry.AddComponent(newEntity, new CylinderColliderComponent { Radius = 0.5f, Height = 1.0f }); }
                            else if (shape == PrimitiveMeshType.Plane) { registry.AddComponent(newEntity, new BoxColliderComponent { Size = new Silk.NET.Maths.Vector3D<float>(10, 0.1f, 10) }); } 
                            else if (shape == PrimitiveMeshType.Quad) { registry.AddComponent(newEntity, new BoxColliderComponent { Size = new Silk.NET.Maths.Vector3D<float>(1, 1, 0.1f) }); }
                            
                            var netModule = _engine.GetModule<NetworkModule>();
                            if (netModule != null && netModule.NetworkManager?.IsHost == true)
                            {
                                int netId = (netModule.NetworkManager?.IdentityMap.AssignNetworkId(registry, newEntity) ?? -1);
                                netModule.Replication?.SendSpawn(netId, shape.ToString(), (int)shape);
                            }
                            _controller.UndoSystem.Record(new ERus.Editor.EditorUI.Commands.EntityLifecycleCommand(newEntity, registry, ERus.Editor.EditorUI.Commands.LifecycleAction.Create));
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
                    _controller.UndoSystem.Record(new ERus.Editor.EditorUI.Commands.EntityLifecycleCommand(newEntity, registry, ERus.Editor.EditorUI.Commands.LifecycleAction.Create));
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

        bool isNetworked = false;
        bool isLocked = false;
        if (registry.HasComponent<NetworkIdentityComponent>(entity))
        {
            var netId = registry.GetComponent<NetworkIdentityComponent>(entity);
            isNetworked = true;
            isLocked = netId.LockUserId != -1;
        }

        string netIcon = isNetworked ? (isLocked ? FontAwesome.Link : FontAwesome.NetworkWired) : "";
        string displayName = string.IsNullOrEmpty(netIcon) ? $"{icon} {name}" : $"{icon} {name}  {netIcon}";

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

        bool isRenaming = _renamingEntityId == entity.Id;

        ImGuiTreeNodeFlags flags = ImGuiTreeNodeFlags.OpenOnArrow | ImGuiTreeNodeFlags.SpanAvailWidth | ImGuiTreeNodeFlags.FramePadding;
        if (isSelected) flags |= ImGuiTreeNodeFlags.Selected;
        if (!hasChildren) flags |= ImGuiTreeNodeFlags.Leaf;

        if (isRenaming)
        {
            flags &= ~ImGuiTreeNodeFlags.SpanAvailWidth; // Permitir SameLine
            flags |= ImGuiTreeNodeFlags.AllowOverlap;
        }

        // Usa entity.Id + 1 para evitar IntPtr.Zero (que ocorre quando Id == 0)
        // IntPtr.Zero faz o ImGui ignorar o ID e usar hash interno, causando conflitos
        bool opened;
        if (isRenaming)
        {
            opened = ImGui.TreeNodeEx((IntPtr)(entity.Id + 1), flags, $"{icon} ###Node_{entity.Id}");
            ImGui.SameLine();
            
            if (_focusRenameInput)
            {
                ImGui.SetKeyboardFocusHere();
                _focusRenameInput = false;
            }
            
            ImGui.PushItemWidth(-1);
            if (ImGui.InputText($"##Rename_{entity.Id}", ref _renamingBuffer, 256, ImGuiInputTextFlags.EnterReturnsTrue | ImGuiInputTextFlags.AutoSelectAll))
            {
                ApplyRename(entity, registry);
            }
            ImGui.PopItemWidth();

            if (ImGui.IsItemDeactivated())
            {
                if (ImGui.IsKeyPressed(ImGuiKey.Escape))
                    _renamingEntityId = null; // Cancela
                else
                    ApplyRename(entity, registry); // Aplica on blur
            }
        }
        else
        {
            if (isLocked) ImGui.PushStyleColor(ImGuiCol.Text, new System.Numerics.Vector4(0.6f, 0.6f, 0.6f, 1.0f));
            opened = ImGui.TreeNodeEx((IntPtr)(entity.Id + 1), flags, displayName);
            if (isLocked) ImGui.PopStyleColor();
        }

        // Seleção ao clicar
        if (!isRenaming && ImGui.IsItemClicked() && !ImGui.IsItemToggledOpen())
        {
            if (io.KeyCtrl)
                EditorServices.Selection.ToggleSelection(entity);
            else if (io.KeyShift && _lastClickedEntity.HasValue)
                EditorServices.Selection.ToggleSelection(entity);
            else
                EditorServices.Selection.SelectedEntity = entity;

            _lastClickedEntity = entity;
        }

        // Duplo clique para renomear
        if (!isRenaming && ImGui.IsItemHovered() && ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left))
        {
            StartRenaming(entity, name);
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
                _controller.UndoSystem.Record(new ERus.Editor.EditorUI.Commands.EntityLifecycleCommand(entity, registry, ERus.Editor.EditorUI.Commands.LifecycleAction.Destroy));
                registry.DestroyEntity(entity);
                EditorServices.Selection.SelectedEntities.Remove(entity);
            }
            
            if (isNetworked)
            {
                ImGui.Separator();
                if (!isLocked)
                {
                    if (ImGui.MenuItem($"{FontAwesome.Link} Take Control"))
                    {
                        ref var netId = ref registry.GetComponent<NetworkIdentityComponent>(entity);
                        var netModule = _engine.GetModule<NetworkModule>();
                        int myPeerId = netModule?.NetworkManager?.MyUserId ?? 0;
                        netId.LockUserId = myPeerId; // Locally lock immediately (predictive)
                        // TODO: Enviar LockEntityPacket
                        ConsoleLog.Log($"[Rede] Took control of {name}");
                    }
                }
                else
                {
                    if (ImGui.MenuItem($"{FontAwesome.LinkSlash} Release Control"))
                    {
                        ref var netId = ref registry.GetComponent<NetworkIdentityComponent>(entity);
                        netId.LockUserId = -1; // Locally unlock
                        // TODO: Enviar UnlockEntityPacket
                        ConsoleLog.Log($"[Rede] Released control of {name}");
                    }
                }
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
                        string beforeJson = ERus.Engine.ECS.SceneSerializer.SerializeEntityToJson(droppedEntity, registry);
                        ERus.Engine.ECS.RelationshipSystem.SetParent(droppedEntity, entity, registry);
                        string afterJson = ERus.Engine.ECS.SceneSerializer.SerializeEntityToJson(droppedEntity, registry);
                        _controller.UndoSystem.Record(new ERus.Editor.EditorUI.Commands.EntityEditCommand(droppedEntity, registry, beforeJson, afterJson, "Parent"));
                    }
                }

                ImGuiPayloadPtr assetPayload = ImGui.AcceptDragDropPayload("ASSET_PATH");
                if (assetPayload.NativePtr != null)
                {
                    string sourceFile = ERus.Editor.EditorUI.Managers.DragDropState.DraggedPayload;
                    string ext = System.IO.Path.GetExtension(sourceFile).ToLowerInvariant();
                    if (ext == ".prefab")
                    {
                        var ecsModule = _engine.GetModule<ECSModule>();
                        ERus.Engine.ECS.SceneSerializer.LoadPrefab(sourceFile, ecsModule.ActiveScene, entity);
                    }
                    else if (ext == ".obj" || ext == ".fbx" || ext == ".gltf" || ext == ".glb")
                    {
                        TryInstantiate3DModel(sourceFile, registry, entity);
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

    private void TryInstantiate3DModel(string sourceFile, Registry registry, Entity? parent)
    {
        var guid = _engine.AssetDatabase.GetGuidByPath(sourceFile);
        if (guid == null)
        {
            _engine.AssetDatabase.ProcessFile(sourceFile);
            guid = _engine.AssetDatabase.GetGuidByPath(sourceFile);
        }

        if (guid != null)
        {
            var newEntity = registry.CreateEntity();
            registry.AddComponent(newEntity, new TransformComponent());
            registry.AddComponent(newEntity, new TagComponent { Name = System.IO.Path.GetFileNameWithoutExtension(sourceFile) });
            registry.AddComponent(newEntity, new MeshComponent { Type = PrimitiveMeshType.None, AssetGuid = guid.Value });
            
            if (parent != null)
            {
                ERus.Engine.ECS.RelationshipSystem.SetParent(newEntity, parent.Value, registry);
            }
            
            var netModule = _engine.GetModule<NetworkModule>();
            if (netModule != null && netModule.NetworkManager?.IsHost == true)
            {
                int netId = (netModule.NetworkManager?.IdentityMap.AssignNetworkId(registry, newEntity) ?? -1);
                netModule.Replication?.SendSpawn(netId, System.IO.Path.GetFileNameWithoutExtension(sourceFile), 0);
            }
            _controller.UndoSystem.Record(new ERus.Editor.EditorUI.Commands.EntityLifecycleCommand(newEntity, registry, ERus.Editor.EditorUI.Commands.LifecycleAction.Create));
        }
    }
}
