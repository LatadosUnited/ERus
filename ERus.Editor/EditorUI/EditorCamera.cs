using System;
using System.Numerics;
using ImGuiNET;

namespace ERus.Editor.EditorUI;

public class EditorCamera
{
    public Vector3 Position { get; set; } = new Vector3(0, 0, 5);
    public float Yaw { get; set; } = -90.0f; // Olhando para o eixo -Z
    public float Pitch { get; set; } = 0.0f;

    public float Speed { get; set; } = 5.0f;
    public float Sensitivity { get; set; } = 0.2f;

    public Matrix4x4 GetViewMatrix()
    {
        var front = GetForwardVector();
        return Matrix4x4.CreateLookAt(Position, Position + front, Vector3.UnitY);
    }

    public Vector3 GetForwardVector()
    {
        float yawRad = Yaw * (MathF.PI / 180.0f);
        float pitchRad = Pitch * (MathF.PI / 180.0f);

        Vector3 front;
        front.X = MathF.Cos(yawRad) * MathF.Cos(pitchRad);
        front.Y = MathF.Sin(pitchRad);
        front.Z = MathF.Sin(yawRad) * MathF.Cos(pitchRad);
        return Vector3.Normalize(front);
    }

    public void Update(float deltaTime, bool isSceneHovered)
    {
        // Só atualiza a câmera se a janela Scene estiver com foco/hover
        // ou se já estiver com o botão direito pressionado previamente nela.
        if (isSceneHovered && ImGui.IsMouseDown(ImGuiMouseButton.Right))
        {
            var io = ImGui.GetIO();
            var delta = io.MouseDelta;

            Yaw += delta.X * Sensitivity;
            Pitch -= delta.Y * Sensitivity;

            // Trava o pitch para não virar de cabeça para baixo
            if (Pitch > 89.0f) Pitch = 89.0f;
            if (Pitch < -89.0f) Pitch = -89.0f;

            var forward = GetForwardVector();
            var right = Vector3.Normalize(Vector3.Cross(forward, Vector3.UnitY));

            float velocity = Speed * deltaTime;

            // Shift para correr
            if (ImGui.IsKeyDown(ImGuiKey.ModShift)) velocity *= 3.0f;

            if (ImGui.IsKeyDown(ImGuiKey.W)) Position += forward * velocity;
            if (ImGui.IsKeyDown(ImGuiKey.S)) Position -= forward * velocity;
            if (ImGui.IsKeyDown(ImGuiKey.A)) Position -= right * velocity;
            if (ImGui.IsKeyDown(ImGuiKey.D)) Position += right * velocity;
            
            // Subir e Descer
            if (ImGui.IsKeyDown(ImGuiKey.E)) Position += Vector3.UnitY * velocity;
            if (ImGui.IsKeyDown(ImGuiKey.Q)) Position -= Vector3.UnitY * velocity;
        }
    }
}


