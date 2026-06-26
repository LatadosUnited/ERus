using System;
using System.Numerics;
using ImGuiNET;
using ERus.Engine.Input;
using Silk.NET.Input;
using System.Linq;

namespace ERus.Editor.EditorUI.Panels;

public class InputMapWindow
{
    public bool IsOpen { get; set; } = false;

    private int _selectedMapIndex = -1;
    private int _selectedActionIndex = -1;
    private int _selectedBindingIndex = -1;

    public void DrawWindow()
    {
        if (!IsOpen) return;

        bool isOpenTemp = IsOpen;
        if (ImGui.Begin("Input Mapping", ref isOpenTemp))
        {
            IsOpen = isOpenTemp;

            var profile = Input.ActiveProfile;
            if (profile == null)
            {
                ImGui.Text("Nenhum InputProfile ativo.");
                ImGui.End();
                return;
            }

            if (ImGui.Button("Save Profile"))
            {
                Input.SaveProfile("input_profile.json");
            }

            ImGui.Separator();

            ImGui.Columns(3, "InputMapColumns", true);

            // --- Coluna 1: Maps ---
            ImGui.Text("Maps");
            if (ImGui.Button("Add Map"))
            {
                profile.Maps.Add(new InputActionMap { Name = "New Map" });
            }
            ImGui.BeginChild("MapList", new Vector2(0, -1), ImGuiChildFlags.Border);
            for (int i = 0; i < profile.Maps.Count; i++)
            {
                var map = profile.Maps[i];
                if (ImGui.Selectable($"{map.Name}##Map{i}", _selectedMapIndex == i))
                {
                    _selectedMapIndex = i;
                    _selectedActionIndex = -1;
                    _selectedBindingIndex = -1;
                }
            }
            ImGui.EndChild();

            ImGui.NextColumn();

            // --- Coluna 2: Actions ---
            ImGui.Text("Actions");
            if (_selectedMapIndex >= 0 && _selectedMapIndex < profile.Maps.Count)
            {
                var map = profile.Maps[_selectedMapIndex];
                
                string mapName = map.Name;
                if (ImGui.InputText("Map Name", ref mapName, 64))
                {
                    map.Name = mapName;
                }

                if (ImGui.Button("Add Action"))
                {
                    map.Actions.Add(new InputAction { Name = "New Action" });
                }
                ImGui.SameLine();
                if (ImGui.Button("Remove Map"))
                {
                    profile.Maps.RemoveAt(_selectedMapIndex);
                    _selectedMapIndex = -1;
                }

                ImGui.BeginChild("ActionList", new Vector2(0, -1), ImGuiChildFlags.Border);
                for (int i = 0; i < map.Actions.Count; i++)
                {
                    var action = map.Actions[i];
                    if (ImGui.Selectable($"{action.Name} ({action.Type})##Action{i}", _selectedActionIndex == i))
                    {
                        _selectedActionIndex = i;
                        _selectedBindingIndex = -1;
                    }
                }
                ImGui.EndChild();
            }

            ImGui.NextColumn();

            // --- Coluna 3: Bindings e Detalhes da Ação ---
            ImGui.Text("Bindings");
            if (_selectedMapIndex >= 0 && _selectedMapIndex < profile.Maps.Count &&
                _selectedActionIndex >= 0 && _selectedActionIndex < profile.Maps[_selectedMapIndex].Actions.Count)
            {
                var action = profile.Maps[_selectedMapIndex].Actions[_selectedActionIndex];
                
                string actionName = action.Name;
                if (ImGui.InputText("Action Name", ref actionName, 64))
                {
                    action.Name = actionName;
                }

                int actionTypeInt = (int)action.Type;
                if (ImGui.Combo("Type", ref actionTypeInt, Enum.GetNames(typeof(InputActionType)), Enum.GetNames(typeof(InputActionType)).Length))
                {
                    action.Type = (InputActionType)actionTypeInt;
                }

                if (ImGui.Button("Add Binding"))
                {
                    action.Bindings.Add(new InputBinding());
                }
                ImGui.SameLine();
                if (ImGui.Button("Remove Action"))
                {
                    profile.Maps[_selectedMapIndex].Actions.RemoveAt(_selectedActionIndex);
                    _selectedActionIndex = -1;
                }

                ImGui.Separator();

                ImGui.BeginChild("BindingList", new Vector2(0, -1), ImGuiChildFlags.Border);
                for (int i = 0; i < action.Bindings.Count; i++)
                {
                    var binding = action.Bindings[i];
                    
                    bool isSelected = _selectedBindingIndex == i;
                    if (ImGui.Selectable($"Binding {i} ({binding.Source})##Binding{i}", isSelected))
                    {
                        _selectedBindingIndex = i;
                    }

                    if (isSelected)
                    {
                        int sourceInt = (int)binding.Source;
                        if (ImGui.Combo($"Source##{i}", ref sourceInt, Enum.GetNames(typeof(InputSourceType)), Enum.GetNames(typeof(InputSourceType)).Length))
                        {
                            binding.Source = (InputSourceType)sourceInt;
                        }

                        if (binding.Source == InputSourceType.Keyboard)
                        {
                            int keyInt = (int)binding.KeyTarget;
                            if (ImGui.Combo($"Key##{i}", ref keyInt, Enum.GetNames(typeof(Key)), Enum.GetNames(typeof(Key)).Length))
                            {
                                binding.KeyTarget = (Key)keyInt;
                            }
                        }
                        else if (binding.Source == InputSourceType.Mouse)
                        {
                            int mouseInt = (int)binding.MouseTarget;
                            if (ImGui.Combo($"Mouse Button##{i}", ref mouseInt, Enum.GetNames(typeof(MouseButton)), Enum.GetNames(typeof(MouseButton)).Length))
                            {
                                binding.MouseTarget = (MouseButton)mouseInt;
                            }
                        }

                        if (action.Type == InputActionType.Axis2D)
                        {
                            int targetInt = (int)binding.TargetComponent;
                            if (ImGui.Combo($"Target Axis##{i}", ref targetInt, Enum.GetNames(typeof(InputBindingTarget)), Enum.GetNames(typeof(InputBindingTarget)).Length))
                            {
                                binding.TargetComponent = (InputBindingTarget)targetInt;
                            }
                        }

                        if (ImGui.Button($"Remove Binding##{i}"))
                        {
                            action.Bindings.RemoveAt(i);
                            _selectedBindingIndex = -1;
                        }
                    }
                    ImGui.Separator();
                }
                ImGui.EndChild();
            }

            ImGui.Columns(1);
        }
        ImGui.End();
    }
}
