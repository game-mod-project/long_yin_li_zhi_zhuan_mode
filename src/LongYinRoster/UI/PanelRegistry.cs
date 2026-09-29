using System.Collections.Generic;

namespace LongYinRoster.UI;

/// <summary>v0.8.0 — 이관된 패널 창의 Hydrate/Persist 를 묶는다. ModWindow 의 설정 주입·저장 블록을 단계별로 흡수.</summary>
public sealed class PanelRegistry
{
    private readonly List<PanelWindow> _windows = new();

    public void Register(PanelWindow window) => _windows.Add(window);

    public void HydrateAll(float screenW, float screenH)
    {
        foreach (var w in _windows) w.Hydrate(screenW, screenH);
    }

    public void ReloadRects(float screenW, float screenH)
    {
        foreach (var w in _windows) w.ReloadRect(screenW, screenH);
    }

    public void PersistAll()
    {
        foreach (var w in _windows) w.Persist();
    }
}
