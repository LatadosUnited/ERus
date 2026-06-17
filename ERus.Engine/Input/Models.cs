using System.Collections.Generic;
using Silk.NET.Input;
using Silk.NET.Maths;
using System;

namespace ERus.Engine.Input;

public enum InputActionType
{
    Button,
    Axis2D
}

public enum InputBindingTarget
{
    Button,
    PositiveX,
    NegativeX,
    PositiveY,
    NegativeY
}

public enum InputSourceType
{
    Keyboard,
    Mouse
}

public class InputBinding
{
    public InputSourceType Source { get; set; }
    public Key KeyTarget { get; set; } = Key.Unknown;
    public MouseButton MouseTarget { get; set; } = MouseButton.Unknown;
    public InputBindingTarget TargetComponent { get; set; } = InputBindingTarget.Button;
}

public class InputAction
{
    public string Name { get; set; } = string.Empty;
    public InputActionType Type { get; set; } = InputActionType.Button;
    public List<InputBinding> Bindings { get; set; } = new List<InputBinding>();

    // No boxing - Typed specific fields
    internal bool _isPressed;
    internal bool _wasPressedThisFrame;
    internal bool _wasReleasedThisFrame;
    internal Vector2D<float> _vectorValue;

    // Public Facade
    public bool IsPressed() => _isPressed;
    public bool WasPressedThisFrame() => _wasPressedThisFrame;
    public bool WasReleasedThisFrame() => _wasReleasedThisFrame;
    
    public Vector2D<float> ReadVector2()
    {
        if (Type != InputActionType.Axis2D)
        {
            Console.WriteLine($"[Input] Aviso: Ação '{Name}' é do tipo {Type}, mas ReadVector2 foi chamado.");
            return Vector2D<float>.Zero;
        }
        return _vectorValue;
    }
}

public class InputActionMap
{
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public List<InputAction> Actions { get; set; } = new List<InputAction>();
}

public class InputProfile
{
    public List<InputActionMap> Maps { get; set; } = new List<InputActionMap>();
}
