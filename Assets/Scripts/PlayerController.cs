using System;
using ERus.Engine.Scripting;

public class PlayerController : ERusScript
{
    public float Speed = 10.5f;
    public string PlayerName = "Heroi";
    public int Health = 100;
    public bool IsInvincible = false;

    private ERus.Engine.Input.InputAction? _moveAction;
    private ERus.Engine.Input.InputAction? _jumpAction;

    public override void Start()
    {
        Log($"[{PlayerName}] nasceu com {Health} de vida. Velocidade: {Speed}");
        
        // Cacheia as actions para evitar lookups de string no Update
        _moveAction = ERus.Engine.Input.Input.GetAction("Player", "Move");
        _jumpAction = ERus.Engine.Input.Input.GetAction("Player", "Jump");
    }

    public override void Update()
    {
        if (_moveAction != null)
        {
            var move = _moveAction.ReadVector2();
            if (move.LengthSquared > 0)
            {
                Log($"[{PlayerName}] Movendo: X={move.X:F2}, Y={move.Y:F2}");
            }
        }

        if (_jumpAction != null && _jumpAction.WasPressedThisFrame())
        {
            Log($"[{PlayerName}] PULO ATIVADO!");
        }
    }
}
