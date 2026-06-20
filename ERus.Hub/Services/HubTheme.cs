using System.Numerics;
using ImGuiNET;

namespace ERus.Hub;

public static class HubTheme
{
    public static void Apply()
    {
        var style = ImGui.GetStyle();
        var colors = style.Colors;

        // Geometria
        style.WindowRounding = 6.0f;
        style.FrameRounding = 4.0f;
        style.GrabRounding = 4.0f;
        style.PopupRounding = 4.0f;
        style.ScrollbarRounding = 4.0f;
        style.TabRounding = 4.0f;
        
        style.WindowPadding = new Vector2(12, 12);
        style.FramePadding = new Vector2(8, 4);
        style.ItemSpacing = new Vector2(8, 6);

        // Paleta Dark Premium
        colors[(int)ImGuiCol.Text] = new Vector4(0.90f, 0.90f, 0.90f, 1.00f);
        colors[(int)ImGuiCol.TextDisabled] = new Vector4(0.50f, 0.50f, 0.50f, 1.00f);
        
        colors[(int)ImGuiCol.WindowBg] = new Vector4(0.08f, 0.09f, 0.10f, 1.00f);
        colors[(int)ImGuiCol.ChildBg] = new Vector4(0.10f, 0.11f, 0.12f, 1.00f);
        colors[(int)ImGuiCol.PopupBg] = new Vector4(0.12f, 0.13f, 0.15f, 1.00f);
        
        colors[(int)ImGuiCol.Border] = new Vector4(0.20f, 0.22f, 0.24f, 1.00f);
        colors[(int)ImGuiCol.BorderShadow] = new Vector4(0.00f, 0.00f, 0.00f, 0.00f);
        
        colors[(int)ImGuiCol.FrameBg] = new Vector4(0.15f, 0.16f, 0.18f, 1.00f);
        colors[(int)ImGuiCol.FrameBgHovered] = new Vector4(0.20f, 0.22f, 0.25f, 1.00f);
        colors[(int)ImGuiCol.FrameBgActive] = new Vector4(0.25f, 0.28f, 0.32f, 1.00f);
        
        colors[(int)ImGuiCol.TitleBg] = new Vector4(0.08f, 0.09f, 0.10f, 1.00f);
        colors[(int)ImGuiCol.TitleBgActive] = new Vector4(0.08f, 0.09f, 0.10f, 1.00f);
        colors[(int)ImGuiCol.TitleBgCollapsed] = new Vector4(0.00f, 0.00f, 0.00f, 0.51f);
        
        // Cor principal (Accent) - Azul elegante
        colors[(int)ImGuiCol.Button] = new Vector4(0.18f, 0.35f, 0.58f, 1.00f);
        colors[(int)ImGuiCol.ButtonHovered] = new Vector4(0.24f, 0.45f, 0.72f, 1.00f);
        colors[(int)ImGuiCol.ButtonActive] = new Vector4(0.14f, 0.28f, 0.48f, 1.00f);
        
        colors[(int)ImGuiCol.Header] = new Vector4(0.18f, 0.35f, 0.58f, 1.00f);
        colors[(int)ImGuiCol.HeaderHovered] = new Vector4(0.24f, 0.45f, 0.72f, 1.00f);
        colors[(int)ImGuiCol.HeaderActive] = new Vector4(0.14f, 0.28f, 0.48f, 1.00f);
        
        colors[(int)ImGuiCol.Tab] = new Vector4(0.12f, 0.13f, 0.15f, 1.00f);
        colors[(int)ImGuiCol.TabHovered] = new Vector4(0.24f, 0.45f, 0.72f, 1.00f);
        colors[(int)ImGuiCol.TabActive] = new Vector4(0.18f, 0.35f, 0.58f, 1.00f);
        colors[(int)ImGuiCol.TabUnfocused] = new Vector4(0.08f, 0.09f, 0.10f, 1.00f);
        colors[(int)ImGuiCol.TabUnfocusedActive] = new Vector4(0.12f, 0.13f, 0.15f, 1.00f);
        
        colors[(int)ImGuiCol.CheckMark] = new Vector4(0.35f, 0.65f, 0.90f, 1.00f);
        colors[(int)ImGuiCol.SliderGrab] = new Vector4(0.24f, 0.45f, 0.72f, 1.00f);
        colors[(int)ImGuiCol.SliderGrabActive] = new Vector4(0.35f, 0.65f, 0.90f, 1.00f);
        
        // Retorna a escala da fonte para 1.0 para evitar embaçamento (blur) do anti-aliasing
        ImGui.GetIO().FontGlobalScale = 1.0f;
    }
}
