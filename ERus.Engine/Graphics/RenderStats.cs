namespace ERus.Engine.Graphics;

/// <summary>
/// Contadores do último frame renderizado.
/// O renderer já computava culled/drawn mas descartava os valores; expostos aqui
/// para alimentar o overlay de profiler previsto em v0.5.50.
/// </summary>
public struct RenderStats
{
    public int EntitiesDrawn;
    public int EntitiesCulled;

    public void Reset()
    {
        EntitiesDrawn = 0;
        EntitiesCulled = 0;
    }
}
