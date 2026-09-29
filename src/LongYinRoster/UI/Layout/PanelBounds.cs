namespace LongYinRoster.UI.Layout;

/// <summary>v0.8.0 — 패널 최소 크기. 최대는 항상 화면 크기라 상수로 두지 않는다.
/// 각 패널 계산기(*Layout.MinSize)가 고정 부분 합 + 주 리스트 3행으로 계산해 낸다.</summary>
public readonly record struct PanelBounds(float MinW, float MinH);
