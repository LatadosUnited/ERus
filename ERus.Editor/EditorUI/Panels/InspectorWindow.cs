using System;
using System.Linq;
using ImGuiNET;
using System.Numerics;
using ERus.Engine.Core;
using ERus.Engine.Modules;
using ERus.Engine.ECS;
using ERus.Engine.Scripting;

namespace ERus.Editor.EditorUI.Panels;

public class InspectorWindow : EditorWindow
{
    private readonly EditorUIController _controller;
    private readonly ERus.Engine.Core.Engine _engine;
    private System.Collections.Concurrent.ConcurrentQueue<Action> _mainThreadActions = new();

    private string? _currentEditBeforeJson = null;
    private bool _isEditing = false;

    private void TrackUndo(string propertyName, Registry registry, Entity entity)
    {
        if (ImGui.IsItemActivated() && !_isEditing)
        {
            _currentEditBeforeJson = ERus.Engine.ECS.SceneSerializer.SerializeEntityToJson(entity, registry);
            _isEditing = true;
        }
        
        if (ImGui.IsItemDeactivatedAfterEdit() && _isEditing && _currentEditBeforeJson != null)
        {
            string afterJson = ERus.Engine.ECS.SceneSerializer.SerializeEntityToJson(entity, registry);
            _controller.UndoSystem.Record(new ERus.Editor.EditorUI.Commands.EntityEditCommand(entity, registry, _currentEditBeforeJson, afterJson, propertyName));
            _isEditing = false;
            _currentEditBeforeJson = null;
        }
    }

    public InspectorWindow(EditorUIController controller, ERus.Engine.Core.Engine engine) : base("Inspector") 
    {
        _controller = controller;
        _engine = engine;
    }

    private void DrawPropertyLabel(string label)
    {
        ImGui.TableNextRow();
        ImGui.TableNextColumn();
        ImGui.TextUnformatted(label);
        ImGui.TableNextColumn();
        ImGui.PushItemWidth(-1);
    }

    protected override void DrawContent()
    {
        while (_mainThreadActions.TryDequeue(out var action)) action();

        if (EditorServices.Selection.SelectedEntities.Count == 0)
        {
            ImGui.TextColored(new Vector4(0.5f, 0.5f, 0.5f, 1.0f), "Nenhuma Entidade Selecionada");
            return;
        }

        var ecsModule = _engine.GetModule<ECSModule>();
        if (ecsModule == null) return;
        
        var registry = ecsModule.ActiveScene.Registry;

        if (EditorServices.Selection.SelectedEntities.Count > 1)
        {
            ImGui.TextColored(new Vector4(0.5f, 0.8f, 1.0f, 1.0f),
                $"{EditorServices.Selection.SelectedEntities.Count} Entidades Selecionadas");
            ImGui.Separator();

            DrawBatchTransformEditor(registry);
            ImGui.Separator();
            DrawBatchDestroyButton(registry);
            return;
        }

        var entity = EditorServices.Selection.SelectedEntity!.Value;

        // TAG COMPONENT
        if (registry.HasComponent<TagComponent>(entity))
        {
            ref var tag = ref registry.GetComponent<TagComponent>(entity);
            string name = tag.Name ?? "Entity";
            
            ImGui.PushFont(ImGui.GetIO().Fonts.Fonts[0]); // assuming 0 is default
            ImGui.PushItemWidth(-1);
            if (ImGui.InputText("##Name", ref name, 128))
            {
                tag.Name = name;
                var netModule = _engine.GetModule<ERus.Engine.Modules.NetworkModule>();
                if (netModule != null && registry.HasComponent<NetworkIdentityComponent>(entity))
                {
                    var netId = registry.GetComponent<NetworkIdentityComponent>(entity).NetworkId;
                    netModule.Replication?.SendRename(netId, name);
                }
            }
            TrackUndo("Tag Name", registry, entity);
            ImGui.PopItemWidth();
            ImGui.PopFont();
            ImGui.Separator();
        }

        // TRANSFORM COMPONENT
        if (registry.HasComponent<TransformComponent>(entity))
        {
            if (ImGui.CollapsingHeader("Transform", ImGuiTreeNodeFlags.DefaultOpen))
            {
                if (ImGui.BeginTable("TransformTable", 2, ImGuiTableFlags.BordersInnerV | ImGuiTableFlags.SizingStretchProp))
                {
                    ImGui.TableSetupColumn("Property", ImGuiTableColumnFlags.WidthFixed, 100.0f);
                    ImGui.TableSetupColumn("Value", ImGuiTableColumnFlags.WidthStretch);
                    
                    ref var transform = ref registry.GetComponent<TransformComponent>(entity);

                    System.Numerics.Vector3 pos = new System.Numerics.Vector3(transform.Position.X, transform.Position.Y, transform.Position.Z);
                    System.Numerics.Vector3 rot = new System.Numerics.Vector3(transform.Rotation.X, transform.Rotation.Y, transform.Rotation.Z);
                    System.Numerics.Vector3 scale = new System.Numerics.Vector3(transform.Scale.X, transform.Scale.Y, transform.Scale.Z);

                    bool edited = false;

                    DrawPropertyLabel("Position");
                    if (ImGui.DragFloat3("##Pos", ref pos, 0.1f)) { edited = true; }
                    TrackUndo("Position", registry, entity);
                    ImGui.PopItemWidth();

                    DrawPropertyLabel("Rotation");
                    if (ImGui.DragFloat3("##Rot", ref rot, 1.0f)) { edited = true; }
                    TrackUndo("Rotation", registry, entity);
                    ImGui.PopItemWidth();

                    DrawPropertyLabel("Scale");
                    if (ImGui.DragFloat3("##Sca", ref scale, 0.1f)) { edited = true; }
                    TrackUndo("Scale", registry, entity);
                    ImGui.PopItemWidth();

                    ImGui.EndTable();

                    if (edited)
                    {
                        transform.Position = new Silk.NET.Maths.Vector3D<float>(pos.X, pos.Y, pos.Z);
                        transform.Rotation = new Silk.NET.Maths.Vector3D<float>(rot.X, rot.Y, rot.Z);
                        transform.Scale = new Silk.NET.Maths.Vector3D<float>(scale.X, scale.Y, scale.Z);
                    }
                }
            }
        }

        // MESH COMPONENT
        if (registry.HasComponent<MeshComponent>(entity))
        {
            if (ImGui.CollapsingHeader("Mesh Renderer", ImGuiTreeNodeFlags.DefaultOpen))
            {
                if (ImGui.BeginTable("MeshTable", 2, ImGuiTableFlags.BordersInnerV | ImGuiTableFlags.SizingStretchProp))
                {
                    ImGui.TableSetupColumn("Property", ImGuiTableColumnFlags.WidthFixed, 100.0f);
                    ImGui.TableSetupColumn("Value", ImGuiTableColumnFlags.WidthStretch);

                    ref var mesh = ref registry.GetComponent<MeshComponent>(entity);
                    
                    DrawPropertyLabel("Primitive Type");
                    int currentType = (int)mesh.Type;
                    string[] types = Enum.GetNames(typeof(PrimitiveMeshType));
                    if (ImGui.Combo("##PrimitiveType", ref currentType, types, types.Length))
                    {
                        mesh.Type = (PrimitiveMeshType)currentType;
                        var netModule = _engine.GetModule<ERus.Engine.Modules.NetworkModule>();
                        if (netModule != null && registry.HasComponent<NetworkIdentityComponent>(entity))
                        {
                            var netId = registry.GetComponent<NetworkIdentityComponent>(entity).NetworkId;
                            netModule.Replication?.SendUpdateMesh(netId, currentType, mesh.AssetHash ?? "");
                        }
                    }
                    ImGui.PopItemWidth();

                    DrawPropertyLabel("Asset Path (.obj/.gltf)");
                    string path = "";
                    if (mesh.AssetGuid != Guid.Empty)
                    {
                        path = _engine.AssetDatabase.GetPathByGuid(mesh.AssetGuid) ?? "(Desconhecido / Falta)";
                    }
                    else
                    {
                        path = "(Nenhum)";
                    }
                    
                    ImGui.BeginDisabled();
                    ImGui.InputText("##AssetPath", ref path, 512);
                    ImGui.EndDisabled();
                    if (ImGui.BeginDragDropTarget())
                    {
                        var payload = ImGui.AcceptDragDropPayload("ASSET_PATH");
                        unsafe
                        {
                            if (payload.NativePtr != null)
                            {
                                string dropped = ERus.Editor.EditorUI.Managers.DragDropState.DraggedPayload;
                                if (dropped.EndsWith(".obj", StringComparison.OrdinalIgnoreCase) ||
                                    dropped.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase) ||
                                    dropped.EndsWith(".gltf", StringComparison.OrdinalIgnoreCase))
                                {
                                    var guid = _engine.AssetDatabase.GetGuidByPath(dropped);
                                    if (guid.HasValue)
                                    {
                                        mesh.AssetGuid = guid.Value;
                                    }
                                    mesh.AssetHash = null; // Limpa o hash até calcular o novo
                                    
                                    var netModule = _engine.GetModule<ERus.Engine.Modules.NetworkModule>();
                                    if (netModule != null)
                                    {
                                        var targetEntity = entity;
                                        _ = netModule.NetworkManager?.AssetSync?.AnnounceAssetAsync(dropped, (hash) => {
                                            _mainThreadActions.Enqueue(() => {
                                                if (registry.IsAlive(targetEntity) && registry.HasComponent<MeshComponent>(targetEntity))
                                                {
                                                    ref var updatedMesh = ref registry.GetComponent<MeshComponent>(targetEntity);
                                                    updatedMesh.AssetHash = hash;
                                                    
                                                    if (registry.HasComponent<NetworkIdentityComponent>(targetEntity))
                                                    {
                                                        var netId = registry.GetComponent<NetworkIdentityComponent>(targetEntity).NetworkId;
                                                        netModule.Replication?.SendUpdateMesh(netId, (int)updatedMesh.Type, hash);
                                                    }
                                                }
                                            });
                                        });
                                    }
                                }
                            }
                        }
                        ImGui.EndDragDropTarget();
                    }
                    ImGui.PopItemWidth();

                    ImGui.EndTable();
                }
            }
        }

        // RIGIDBODY COMPONENT
        if (registry.HasComponent<RigidBodyComponent>(entity))
        {
            if (ImGui.CollapsingHeader("Rigidbody", ImGuiTreeNodeFlags.DefaultOpen))
            {
                if (ImGui.BeginTable("RigidbodyTable", 2, ImGuiTableFlags.BordersInnerV | ImGuiTableFlags.SizingStretchProp))
                {
                    ImGui.TableSetupColumn("Property", ImGuiTableColumnFlags.WidthFixed, 100.0f);
                    ImGui.TableSetupColumn("Value", ImGuiTableColumnFlags.WidthStretch);

                    ref var rb = ref registry.GetComponent<RigidBodyComponent>(entity);
                    
                    bool isKinematic = rb.IsKinematic;
                    DrawPropertyLabel("Is Kinematic");
                    if (ImGui.Checkbox("##IsKinematic", ref isKinematic)) rb.IsKinematic = isKinematic;
                    ImGui.PopItemWidth();

                    bool useGravity = rb.UseGravity;
                    DrawPropertyLabel("Use Gravity");
                    if (ImGui.Checkbox("##UseGravity", ref useGravity)) rb.UseGravity = useGravity;
                    ImGui.PopItemWidth();

                    float mass = rb.Mass;
                    DrawPropertyLabel("Mass");
                    if (ImGui.DragFloat("##Mass", ref mass, 0.1f, 0.01f, 10000f)) rb.Mass = mass;
                    ImGui.PopItemWidth();

                    float linearDrag = rb.LinearDrag;
                    DrawPropertyLabel("Linear Drag");
                    if (ImGui.DragFloat("##LinearDrag", ref linearDrag, 0.1f, 0.0f, 100f)) rb.LinearDrag = linearDrag;
                    ImGui.PopItemWidth();

                    float angularDrag = rb.AngularDrag;
                    DrawPropertyLabel("Angular Drag");
                    if (ImGui.DragFloat("##AngularDrag", ref angularDrag, 0.1f, 0.0f, 100f)) rb.AngularDrag = angularDrag;
                    ImGui.PopItemWidth();

                    int constraints = (int)rb.Constraints;
                    string[] cNames = Enum.GetNames(typeof(RigidbodyConstraints));
                    int[] cValues = (int[])Enum.GetValues(typeof(RigidbodyConstraints));
                    DrawPropertyLabel("Constraints");
                    if (ImGui.BeginCombo("##Constraints", rb.Constraints.ToString()))
                    {
                        for (int i = 0; i < cNames.Length; i++)
                        {
                            if (cValues[i] == 0) continue; // Skip "None"
                            if (cValues[i] == (int)RigidbodyConstraints.FreezePosition ||
                                cValues[i] == (int)RigidbodyConstraints.FreezeRotation ||
                                cValues[i] == (int)RigidbodyConstraints.FreezeAll) continue; // Skip composite names
                            
                            bool isSelected = (constraints & cValues[i]) == cValues[i];
                            if (ImGui.Checkbox(cNames[i], ref isSelected))
                            {
                                if (isSelected) constraints |= cValues[i];
                                else constraints &= ~cValues[i];
                                rb.Constraints = (RigidbodyConstraints)constraints;
                            }
                        }
                        ImGui.EndCombo();
                    }
                    ImGui.PopItemWidth();

                    ImGui.EndTable();
                }
                
                ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.7f, 0.3f, 0.2f, 1.0f));
                ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.8f, 0.4f, 0.3f, 1.0f));
                ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(0.9f, 0.5f, 0.4f, 1.0f));
                if (ImGui.Button("Remove Rigidbody", new Vector2(-1, 22)))
                {
                    registry.RemoveComponent<RigidBodyComponent>(entity);
                }
                ImGui.PopStyleColor(3);
            }
        }

        // BOX COLLIDER COMPONENT
        if (registry.HasComponent<BoxColliderComponent>(entity))
        {
            if (ImGui.CollapsingHeader("Box Collider", ImGuiTreeNodeFlags.DefaultOpen))
            {
                if (ImGui.BeginTable("BoxColliderTable", 2, ImGuiTableFlags.BordersInnerV | ImGuiTableFlags.SizingStretchProp))
                {
                    ImGui.TableSetupColumn("Property", ImGuiTableColumnFlags.WidthFixed, 100.0f);
                    ImGui.TableSetupColumn("Value", ImGuiTableColumnFlags.WidthStretch);
                    ref var coll = ref registry.GetComponent<BoxColliderComponent>(entity);
                    
                    System.Numerics.Vector3 size = new System.Numerics.Vector3(coll.Size.X, coll.Size.Y, coll.Size.Z);
                    DrawPropertyLabel("Size");
                    if (ImGui.DragFloat3("##BoxSize", ref size, 0.1f)) coll.Size = new Silk.NET.Maths.Vector3D<float>(size.X, size.Y, size.Z);
                    ImGui.PopItemWidth();
                    
                    System.Numerics.Vector3 center = new System.Numerics.Vector3(coll.Center.X, coll.Center.Y, coll.Center.Z);
                    DrawPropertyLabel("Center");
                    if (ImGui.DragFloat3("##BoxCenter", ref center, 0.1f)) coll.Center = new Silk.NET.Maths.Vector3D<float>(center.X, center.Y, center.Z);
                    ImGui.PopItemWidth();

                    bool isTrigger = coll.IsTrigger;
                    DrawPropertyLabel("Is Trigger");
                    if (ImGui.Checkbox("##BoxIsTrigger", ref isTrigger)) coll.IsTrigger = isTrigger;
                    ImGui.PopItemWidth();
                    ImGui.EndTable();
                }
                ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.7f, 0.3f, 0.2f, 1.0f));
                ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.8f, 0.4f, 0.3f, 1.0f));
                ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(0.9f, 0.5f, 0.4f, 1.0f));
                if (ImGui.Button("Remove Box Collider", new Vector2(-1, 22))) registry.RemoveComponent<BoxColliderComponent>(entity);
                ImGui.PopStyleColor(3);
            }
        }

        // SPHERE COLLIDER COMPONENT
        if (registry.HasComponent<SphereColliderComponent>(entity))
        {
            if (ImGui.CollapsingHeader("Sphere Collider", ImGuiTreeNodeFlags.DefaultOpen))
            {
                if (ImGui.BeginTable("SphereColliderTable", 2, ImGuiTableFlags.BordersInnerV | ImGuiTableFlags.SizingStretchProp))
                {
                    ImGui.TableSetupColumn("Property", ImGuiTableColumnFlags.WidthFixed, 100.0f);
                    ImGui.TableSetupColumn("Value", ImGuiTableColumnFlags.WidthStretch);
                    ref var coll = ref registry.GetComponent<SphereColliderComponent>(entity);
                    
                    float radius = coll.Radius;
                    DrawPropertyLabel("Radius");
                    if (ImGui.DragFloat("##SphereRadius", ref radius, 0.1f)) coll.Radius = radius;
                    ImGui.PopItemWidth();
                    
                    System.Numerics.Vector3 center = new System.Numerics.Vector3(coll.Center.X, coll.Center.Y, coll.Center.Z);
                    DrawPropertyLabel("Center");
                    if (ImGui.DragFloat3("##SphereCenter", ref center, 0.1f)) coll.Center = new Silk.NET.Maths.Vector3D<float>(center.X, center.Y, center.Z);
                    ImGui.PopItemWidth();

                    bool isTrigger = coll.IsTrigger;
                    DrawPropertyLabel("Is Trigger");
                    if (ImGui.Checkbox("##SphereIsTrigger", ref isTrigger)) coll.IsTrigger = isTrigger;
                    ImGui.PopItemWidth();
                    ImGui.EndTable();
                }
                ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.7f, 0.3f, 0.2f, 1.0f));
                ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.8f, 0.4f, 0.3f, 1.0f));
                ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(0.9f, 0.5f, 0.4f, 1.0f));
                if (ImGui.Button("Remove Sphere Collider", new Vector2(-1, 22))) registry.RemoveComponent<SphereColliderComponent>(entity);
                ImGui.PopStyleColor(3);
            }
        }

        // CAPSULE COLLIDER COMPONENT
        if (registry.HasComponent<CapsuleColliderComponent>(entity))
        {
            if (ImGui.CollapsingHeader("Capsule Collider", ImGuiTreeNodeFlags.DefaultOpen))
            {
                if (ImGui.BeginTable("CapsuleColliderTable", 2, ImGuiTableFlags.BordersInnerV | ImGuiTableFlags.SizingStretchProp))
                {
                    ImGui.TableSetupColumn("Property", ImGuiTableColumnFlags.WidthFixed, 100.0f);
                    ImGui.TableSetupColumn("Value", ImGuiTableColumnFlags.WidthStretch);
                    ref var coll = ref registry.GetComponent<CapsuleColliderComponent>(entity);
                    
                    float radius = coll.Radius;
                    DrawPropertyLabel("Radius");
                    if (ImGui.DragFloat("##CapsuleRadius", ref radius, 0.1f)) coll.Radius = radius;
                    ImGui.PopItemWidth();

                    float height = coll.Height;
                    DrawPropertyLabel("Height");
                    if (ImGui.DragFloat("##CapsuleHeight", ref height, 0.1f)) coll.Height = height;
                    ImGui.PopItemWidth();
                    
                    System.Numerics.Vector3 center = new System.Numerics.Vector3(coll.Center.X, coll.Center.Y, coll.Center.Z);
                    DrawPropertyLabel("Center");
                    if (ImGui.DragFloat3("##CapsuleCenter", ref center, 0.1f)) coll.Center = new Silk.NET.Maths.Vector3D<float>(center.X, center.Y, center.Z);
                    ImGui.PopItemWidth();

                    bool isTrigger = coll.IsTrigger;
                    DrawPropertyLabel("Is Trigger");
                    if (ImGui.Checkbox("##CapsuleIsTrigger", ref isTrigger)) coll.IsTrigger = isTrigger;
                    ImGui.PopItemWidth();
                    ImGui.EndTable();
                }
                ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.7f, 0.3f, 0.2f, 1.0f));
                ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.8f, 0.4f, 0.3f, 1.0f));
                ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(0.9f, 0.5f, 0.4f, 1.0f));
                if (ImGui.Button("Remove Capsule Collider", new Vector2(-1, 22))) registry.RemoveComponent<CapsuleColliderComponent>(entity);
                ImGui.PopStyleColor(3);
            }
        }

        // CYLINDER COLLIDER COMPONENT
        if (registry.HasComponent<CylinderColliderComponent>(entity))
        {
            if (ImGui.CollapsingHeader("Cylinder Collider", ImGuiTreeNodeFlags.DefaultOpen))
            {
                if (ImGui.BeginTable("CylinderColliderTable", 2, ImGuiTableFlags.BordersInnerV | ImGuiTableFlags.SizingStretchProp))
                {
                    ImGui.TableSetupColumn("Property", ImGuiTableColumnFlags.WidthFixed, 100.0f);
                    ImGui.TableSetupColumn("Value", ImGuiTableColumnFlags.WidthStretch);
                    ref var coll = ref registry.GetComponent<CylinderColliderComponent>(entity);
                    
                    float radius = coll.Radius;
                    DrawPropertyLabel("Radius");
                    if (ImGui.DragFloat("##CylinderRadius", ref radius, 0.1f)) coll.Radius = radius;
                    ImGui.PopItemWidth();

                    float height = coll.Height;
                    DrawPropertyLabel("Height");
                    if (ImGui.DragFloat("##CylinderHeight", ref height, 0.1f)) coll.Height = height;
                    ImGui.PopItemWidth();
                    
                    System.Numerics.Vector3 center = new System.Numerics.Vector3(coll.Center.X, coll.Center.Y, coll.Center.Z);
                    DrawPropertyLabel("Center");
                    if (ImGui.DragFloat3("##CylinderCenter", ref center, 0.1f)) coll.Center = new Silk.NET.Maths.Vector3D<float>(center.X, center.Y, center.Z);
                    ImGui.PopItemWidth();

                    bool isTrigger = coll.IsTrigger;
                    DrawPropertyLabel("Is Trigger");
                    if (ImGui.Checkbox("##CylinderIsTrigger", ref isTrigger)) coll.IsTrigger = isTrigger;
                    ImGui.PopItemWidth();
                    ImGui.EndTable();
                }
                ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.7f, 0.3f, 0.2f, 1.0f));
                ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.8f, 0.4f, 0.3f, 1.0f));
                ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(0.9f, 0.5f, 0.4f, 1.0f));
                if (ImGui.Button("Remove Cylinder Collider", new Vector2(-1, 22))) registry.RemoveComponent<CylinderColliderComponent>(entity);
                ImGui.PopStyleColor(3);
            }
        }

        // MESH COLLIDER COMPONENT
        if (registry.HasComponent<MeshColliderComponent>(entity))
        {
            if (ImGui.CollapsingHeader("Mesh Collider", ImGuiTreeNodeFlags.DefaultOpen))
            {
                if (ImGui.BeginTable("MeshColliderTable", 2, ImGuiTableFlags.BordersInnerV | ImGuiTableFlags.SizingStretchProp))
                {
                    ImGui.TableSetupColumn("Property", ImGuiTableColumnFlags.WidthFixed, 100.0f);
                    ImGui.TableSetupColumn("Value", ImGuiTableColumnFlags.WidthStretch);
                    ref var coll = ref registry.GetComponent<MeshColliderComponent>(entity);

                    bool isConvex = coll.IsConvex;
                    DrawPropertyLabel("Is Convex");
                    if (ImGui.Checkbox("##MeshIsConvex", ref isConvex)) coll.IsConvex = isConvex;
                    ImGui.PopItemWidth();

                    System.Numerics.Vector3 center = new System.Numerics.Vector3(coll.Center.X, coll.Center.Y, coll.Center.Z);
                    DrawPropertyLabel("Center");
                    if (ImGui.DragFloat3("##MeshCenter", ref center, 0.1f)) coll.Center = new Silk.NET.Maths.Vector3D<float>(center.X, center.Y, center.Z);
                    ImGui.PopItemWidth();

                    bool isTrigger = coll.IsTrigger;
                    DrawPropertyLabel("Is Trigger");
                    if (ImGui.Checkbox("##MeshIsTrigger", ref isTrigger)) coll.IsTrigger = isTrigger;
                    ImGui.PopItemWidth();
                    
                    DrawPropertyLabel("Asset Path (.obj/.gltf)");
                    string path = "";
                    if (coll.AssetGuid != Guid.Empty) path = _engine.AssetDatabase.GetPathByGuid(coll.AssetGuid) ?? "(Desconhecido / Falta)";
                    else path = "(Nenhum)";
                    
                    ImGui.BeginDisabled();
                    ImGui.InputText("##MeshCollPath", ref path, 512);
                    ImGui.EndDisabled();
                    if (ImGui.BeginDragDropTarget())
                    {
                        var payload = ImGui.AcceptDragDropPayload("ASSET_PATH");
                        unsafe {
                            if (payload.NativePtr != null)
                            {
                                string dropped = ERus.Editor.EditorUI.Managers.DragDropState.DraggedPayload;
                                if (dropped.EndsWith(".obj", StringComparison.OrdinalIgnoreCase) ||
                                    dropped.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase) ||
                                    dropped.EndsWith(".gltf", StringComparison.OrdinalIgnoreCase))
                                {
                                    var guid = _engine.AssetDatabase.GetGuidByPath(dropped);
                                    if (guid.HasValue) coll.AssetGuid = guid.Value;
                                }
                            }
                        }
                        ImGui.EndDragDropTarget();
                    }
                    ImGui.PopItemWidth();

                    ImGui.EndTable();
                }
                ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.7f, 0.3f, 0.2f, 1.0f));
                ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.8f, 0.4f, 0.3f, 1.0f));
                ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(0.9f, 0.5f, 0.4f, 1.0f));
                if (ImGui.Button("Remove Mesh Collider", new Vector2(-1, 22))) registry.RemoveComponent<MeshColliderComponent>(entity);
                ImGui.PopStyleColor(3);
            }
        }

        // CAMERA COMPONENT
        if (registry.HasComponent<CameraComponent>(entity))
        {
            if (ImGui.CollapsingHeader("Camera", ImGuiTreeNodeFlags.DefaultOpen))
            {
                if (ImGui.BeginTable("CameraTable", 2, ImGuiTableFlags.BordersInnerV | ImGuiTableFlags.SizingStretchProp))
                {
                    ImGui.TableSetupColumn("Property", ImGuiTableColumnFlags.WidthFixed, 100.0f);
                    ImGui.TableSetupColumn("Value", ImGuiTableColumnFlags.WidthStretch);

                    ref var cam = ref registry.GetComponent<CameraComponent>(entity);
                    
                    bool isPrimary = cam.IsPrimary;
                    DrawPropertyLabel("Primary");
                    if (ImGui.Checkbox("##Primary", ref isPrimary)) cam.IsPrimary = isPrimary;
                    ImGui.PopItemWidth();
                    
                    float fov = cam.FieldOfView;
                    DrawPropertyLabel("Field of View");
                    if (ImGui.SliderFloat("##FOV", ref fov, 10f, 120f)) cam.FieldOfView = fov;
                    ImGui.PopItemWidth();

                    float near = cam.NearClip;
                    DrawPropertyLabel("Near Clip");
                    if (ImGui.DragFloat("##Near", ref near, 0.01f, 0.01f, 100f)) cam.NearClip = near;
                    ImGui.PopItemWidth();

                    float far = cam.FarClip;
                    DrawPropertyLabel("Far Clip");
                    if (ImGui.DragFloat("##Far", ref far, 1f, 10f, 10000f)) cam.FarClip = far;
                    ImGui.PopItemWidth();

                    ImGui.EndTable();
                }
            }
        }

        // SCRIPT COMPONENT
        if (registry.HasComponent<ScriptComponent>(entity))
        {
            ref var scriptComp = ref registry.GetComponent<ScriptComponent>(entity);
            
            for (int i = 0; i < scriptComp.Scripts.Count; i++)
            {
                var scriptData = scriptComp.Scripts[i];
                string typeName = scriptData.ScriptTypeName ?? "(nenhum)";
                
                if (ImGui.CollapsingHeader($"Script: {typeName}##{i}", ImGuiTreeNodeFlags.DefaultOpen))
                {
                    if (ImGui.BeginTable($"ScriptTable##{i}", 2, ImGuiTableFlags.BordersInnerV | ImGuiTableFlags.SizingStretchProp))
                    {
                        ImGui.TableSetupColumn("Property", ImGuiTableColumnFlags.WidthFixed, 100.0f);
                        ImGui.TableSetupColumn("Value", ImGuiTableColumnFlags.WidthStretch);
                        
                        var scriptModule = _engine.GetModule<ScriptModule>();
                        if (scriptModule != null && !string.IsNullOrEmpty(typeName))
                        {
                            var scriptType = scriptModule.AvailableScriptTypes.FirstOrDefault(t => t.Name == typeName);
                            if (scriptType != null)
                            {
                                var fields = scriptType.GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                                foreach (var field in fields)
                                {
                                    DrawPropertyLabel(field.Name);
                                    
                                    string strVal = "";
                                    if (scriptData.FieldValues.ContainsKey(field.Name))
                                    {
                                        strVal = scriptData.FieldValues[field.Name];
                                    }
                                    
                                    bool changed = false;

                                    if (field.FieldType == typeof(float))
                                    {
                                        float val = 0f;
                                        float.TryParse(strVal, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out val);
                                        if (ImGui.DragFloat($"##{field.Name}_{i}", ref val, 0.1f))
                                        {
                                            strVal = val.ToString(System.Globalization.CultureInfo.InvariantCulture);
                                            changed = true;
                                        }
                                    }
                                    else if (field.FieldType == typeof(int))
                                    {
                                        int val = 0;
                                        int.TryParse(strVal, out val);
                                        if (ImGui.DragInt($"##{field.Name}_{i}", ref val))
                                        {
                                            strVal = val.ToString();
                                            changed = true;
                                        }
                                    }
                                    else if (field.FieldType == typeof(bool))
                                    {
                                        bool val = false;
                                        bool.TryParse(strVal, out val);
                                        if (ImGui.Checkbox($"##{field.Name}_{i}", ref val))
                                        {
                                            strVal = val.ToString();
                                            changed = true;
                                        }
                                    }
                                    else if (field.FieldType == typeof(string))
                                    {
                                        if (strVal == null) strVal = "";
                                        if (ImGui.InputText($"##{field.Name}_{i}", ref strVal, 256))
                                        {
                                            changed = true;
                                        }
                                    }
                                    else
                                    {
                                        ImGui.TextDisabled(field.FieldType.Name);
                                    }
                                    
                                    ImGui.PopItemWidth();

                                    if (changed)
                                    {
                                        scriptData.FieldValues[field.Name] = strVal;
                                    }
                                }
                            }
                        }
                        ImGui.EndTable();
                    }

                    ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.7f, 0.3f, 0.2f, 1.0f));
                    ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.8f, 0.4f, 0.3f, 1.0f));
                    ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(0.9f, 0.5f, 0.4f, 1.0f));
                    if (ImGui.Button($"Remove Script##{i}", new Vector2(-1, 22)))
                    {
                        scriptComp.Scripts.RemoveAt(i);
                        i--; // Ajustar index apos remocao
                    }
                    ImGui.PopStyleColor(3);
                }
            }
            
            if (scriptComp.Scripts.Count == 0)
            {
                registry.RemoveComponent<ScriptComponent>(entity);
            }
        }

        ImGui.Separator();
        ImGui.Spacing();

        if (ImGui.Button($"{FontAwesome.Plus} Add Component", new Vector2(-1, 25)))
        {
            ImGui.OpenPopup("AddComponentPopup");
        }

        if (ImGui.BeginPopup("AddComponentPopup"))
        {
            if (!registry.HasComponent<CameraComponent>(entity))
            {
                if (ImGui.MenuItem("Camera"))
                {
                    registry.AddComponent(entity, new CameraComponent());
                    ImGui.CloseCurrentPopup();
                }
            }

            if (ImGui.BeginMenu("Physics"))
            {
                if (!registry.HasComponent<RigidBodyComponent>(entity) && ImGui.MenuItem("Rigidbody"))
                {
                    registry.AddComponent(entity, new RigidBodyComponent());
                    ImGui.CloseCurrentPopup();
                }
                ImGui.Separator();
                if (!registry.HasComponent<BoxColliderComponent>(entity) && ImGui.MenuItem("Box Collider"))
                {
                    registry.AddComponent(entity, new BoxColliderComponent());
                    ImGui.CloseCurrentPopup();
                }
                if (!registry.HasComponent<SphereColliderComponent>(entity) && ImGui.MenuItem("Sphere Collider"))
                {
                    registry.AddComponent(entity, new SphereColliderComponent());
                    ImGui.CloseCurrentPopup();
                }
                if (!registry.HasComponent<CapsuleColliderComponent>(entity) && ImGui.MenuItem("Capsule Collider"))
                {
                    registry.AddComponent(entity, new CapsuleColliderComponent());
                    ImGui.CloseCurrentPopup();
                }
                if (!registry.HasComponent<CylinderColliderComponent>(entity) && ImGui.MenuItem("Cylinder Collider"))
                {
                    registry.AddComponent(entity, new CylinderColliderComponent());
                    ImGui.CloseCurrentPopup();
                }
                if (!registry.HasComponent<MeshColliderComponent>(entity) && ImGui.MenuItem("Mesh Collider"))
                {
                    registry.AddComponent(entity, new MeshColliderComponent());
                    ImGui.CloseCurrentPopup();
                }
                ImGui.EndMenu();
            }

            var scriptModule = _engine.GetModule<ScriptModule>();
            if (scriptModule != null && scriptModule.AvailableScriptTypes.Count > 0)
            {
                if (ImGui.BeginMenu("Script"))
                {
                    foreach (var scriptType in scriptModule.AvailableScriptTypes)
                    {
                        if (ImGui.MenuItem(scriptType.Name))
                        {
                            if (!registry.HasComponent<ScriptComponent>(entity))
                            {
                                registry.AddComponent(entity, new ScriptComponent());
                            }
                            ref var scriptComp = ref registry.GetComponent<ScriptComponent>(entity);
                            scriptComp.Scripts.Add(new ScriptData { ScriptTypeName = scriptType.Name });
                            ImGui.CloseCurrentPopup();
                        }
                    }
                    ImGui.EndMenu();
                }
            }
            else
            {
                ImGui.BeginDisabled();
                ImGui.MenuItem("Script (nenhum disponível)");
                ImGui.EndDisabled();
            }

            ImGui.EndPopup();
        }

        ImGui.Spacing();

        DrawDestroyButton(entity, registry);
    }

    private void DrawBatchTransformEditor(Registry registry)
    {
        if (!ImGui.CollapsingHeader("Transform (Batch)", ImGuiTreeNodeFlags.DefaultOpen))
            return;

        ImGui.TextColored(new Vector4(0.7f, 0.7f, 0.4f, 1.0f), "Delta aplicado a todas:");
        
        var deltaPos = Vector3.Zero;
        var deltaRot = Vector3.Zero;
        var deltaScale = Vector3.Zero;

        bool edited = false;
        
        if (ImGui.BeginTable("BatchTransformTable", 2, ImGuiTableFlags.BordersInnerV | ImGuiTableFlags.SizingStretchProp))
        {
            ImGui.TableSetupColumn("Property", ImGuiTableColumnFlags.WidthFixed, 100.0f);
            ImGui.TableSetupColumn("Value", ImGuiTableColumnFlags.WidthStretch);

            DrawPropertyLabel("Δ Position");
            if (ImGui.DragFloat3("##DeltaPos", ref deltaPos, 0.1f)) edited = true;
            ImGui.PopItemWidth();

            DrawPropertyLabel("Δ Rotation");
            if (ImGui.DragFloat3("##DeltaRot", ref deltaRot, 0.1f)) edited = true;
            ImGui.PopItemWidth();

            DrawPropertyLabel("Δ Scale");
            if (ImGui.DragFloat3("##DeltaSca", ref deltaScale, 0.1f)) edited = true;
            ImGui.PopItemWidth();

            ImGui.EndTable();
        }

        if (edited)
        {
            var netModule = _engine.GetModule<NetworkModule>();

            foreach (var entity in EditorServices.Selection.SelectedEntities)
            {
                if (!registry.HasComponent<TransformComponent>(entity)) continue;
                ref var t = ref registry.GetComponent<TransformComponent>(entity);

                t.Position = new Silk.NET.Maths.Vector3D<float>(
                    t.Position.X + deltaPos.X, t.Position.Y + deltaPos.Y, t.Position.Z + deltaPos.Z);
                t.Rotation = new Silk.NET.Maths.Vector3D<float>(
                    t.Rotation.X + deltaRot.X, t.Rotation.Y + deltaRot.Y, t.Rotation.Z + deltaRot.Z);
                t.Scale = new Silk.NET.Maths.Vector3D<float>(
                    t.Scale.X + deltaScale.X, t.Scale.Y + deltaScale.Y, t.Scale.Z + deltaScale.Z);

                if (netModule != null && registry.HasComponent<NetworkIdentityComponent>(entity))
                {
                    var netId = registry.GetComponent<NetworkIdentityComponent>(entity).NetworkId;

                }
            }
        }
    }

    private void DrawBatchDestroyButton(Registry registry)
    {
        ImGui.Spacing();
        ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.8f, 0.2f, 0.2f, 1.0f));
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.9f, 0.3f, 0.3f, 1.0f));
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(1.0f, 0.4f, 0.4f, 1.0f));

        if (ImGui.Button($"{FontAwesome.Trash} Destroy {EditorServices.Selection.SelectedEntities.Count} Entities", new Vector2(-1, 30)))
        {
            var netModule = _engine.GetModule<NetworkModule>();
            foreach (var entity in EditorServices.Selection.SelectedEntities.ToList())
            {
                if (netModule != null && registry.HasComponent<NetworkIdentityComponent>(entity))
                {
                    var netId = registry.GetComponent<NetworkIdentityComponent>(entity).NetworkId;
                    netModule.Replication?.SendDestroy(netId);
                }
                registry.DestroyEntity(entity);
            }
            EditorServices.Selection.ClearSelection();
        }

        ImGui.PopStyleColor(3);
    }

    private void DrawDestroyButton(Entity entity, Registry registry)
    {
        ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.8f, 0.2f, 0.2f, 1.0f));
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.9f, 0.3f, 0.3f, 1.0f));
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(1.0f, 0.4f, 0.4f, 1.0f));
        
        if (ImGui.Button($"{FontAwesome.Trash} Destroy Entity", new Vector2(-1, 30)))
        {
            var netModule = _engine.GetModule<NetworkModule>();
            if (netModule != null && registry.HasComponent<NetworkIdentityComponent>(entity))
            {
                var netId = registry.GetComponent<NetworkIdentityComponent>(entity).NetworkId;
                netModule.Replication?.SendDestroy(netId);
            }
            registry.DestroyEntity(entity);
            EditorServices.Selection.SelectedEntity = null;
        }

        ImGui.PopStyleColor(3);
    }
}



