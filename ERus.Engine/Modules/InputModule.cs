using System;
using Silk.NET.Input;
using Silk.NET.Maths;
using ERus.Engine.Core;
using ERus.Engine.Input;

namespace ERus.Engine.Modules;

public class InputModule : IEngineModule
{
    private IKeyboard? _keyboard;
    private IMouse? _mouse;

    // Hardware Snapshots
    private readonly bool[] _keySnapshot = new bool[512];
    private readonly bool[] _mouseSnapshot = new bool[16];

    private static readonly Key[] _allKeys = Enum.GetValues<Key>();
    private static readonly MouseButton[] _allMouseButtons = Enum.GetValues<MouseButton>();

    public void Initialize(Core.Engine engine)
    {
        if (engine.Input.Keyboards.Count > 0)
            _keyboard = engine.Input.Keyboards[0];
            
        if (engine.Input.Mice.Count > 0)
            _mouse = engine.Input.Mice[0];
            
        // Carrega um profile padrao se não houver um. Em jogos reais isso vem do path do projeto.
        if (ERus.Engine.Input.Input.ActiveProfile == null)
        {
            ERus.Engine.Input.Input.LoadProfile("input_profile.json");
        }
    }

    public void Update(double deltaTime)
    {
        TakeHardwareSnapshot();
        ProcessActions();
    }

    private void TakeHardwareSnapshot()
    {
        if (_keyboard != null)
        {
            for (int i = 0; i < _allKeys.Length; i++)
            {
                Key k = _allKeys[i];
                if (k != Key.Unknown)
                {
                    _keySnapshot[(int)k] = _keyboard.IsKeyPressed(k);
                }
            }
        }

        if (_mouse != null)
        {
            for (int i = 0; i < _allMouseButtons.Length; i++)
            {
                MouseButton mb = _allMouseButtons[i];
                if (mb != MouseButton.Unknown)
                {
                    _mouseSnapshot[(int)mb] = _mouse.IsButtonPressed(mb);
                }
            }
        }
    }

    private void ProcessActions()
    {
        var profile = ERus.Engine.Input.Input.ActiveProfile;
        if (profile == null) return;

        for (int m = 0; m < profile.Maps.Count; m++)
        {
            var map = profile.Maps[m];
            if (!map.IsActive) continue;

            for (int a = 0; a < map.Actions.Count; a++)
            {
                var action = map.Actions[a];
                
                bool oldPressed = action._isPressed;
                bool currentlyPressed = false;
                Vector2D<float> composedVector = Vector2D<float>.Zero;

                for (int b = 0; b < action.Bindings.Count; b++)
                {
                    var binding = action.Bindings[b];
                    bool isBindingActive = false;

                    if (binding.Source == InputSourceType.Keyboard)
                    {
                        int keyIndex = (int)binding.KeyTarget;
                        if (keyIndex >= 0 && keyIndex < _keySnapshot.Length)
                            isBindingActive = _keySnapshot[keyIndex];
                    }
                    else if (binding.Source == InputSourceType.Mouse)
                    {
                        int mouseIndex = (int)binding.MouseTarget;
                        if (mouseIndex >= 0 && mouseIndex < _mouseSnapshot.Length)
                            isBindingActive = _mouseSnapshot[mouseIndex];
                    }

                    if (isBindingActive)
                    {
                        currentlyPressed = true;
                        
                        if (action.Type == InputActionType.Axis2D)
                        {
                            if (binding.TargetComponent == InputBindingTarget.PositiveX) composedVector.X += 1.0f;
                            else if (binding.TargetComponent == InputBindingTarget.NegativeX) composedVector.X -= 1.0f;
                            else if (binding.TargetComponent == InputBindingTarget.PositiveY) composedVector.Y += 1.0f;
                            else if (binding.TargetComponent == InputBindingTarget.NegativeY) composedVector.Y -= 1.0f;
                        }
                    }
                }

                // Normalização do vetor (evita raiz quadrada desnecessária se não for eixo 2D)
                if (action.Type == InputActionType.Axis2D)
                {
                    if (composedVector.LengthSquared > 1.0f)
                    {
                        composedVector = Vector2D.Normalize(composedVector);
                    }
                    action._vectorValue = composedVector;
                }

                action._isPressed = currentlyPressed;
                
                // Reset natural frame-a-frame sem interferência da ordem dos scripts
                action._wasPressedThisFrame = currentlyPressed && !oldPressed;
                action._wasReleasedThisFrame = !currentlyPressed && oldPressed;
            }
        }
    }

    public void Render(double deltaTime) { }
    public void Dispose() { }
}
