using LongYinRoster.Util;

namespace LongYinRoster.Core;

/// <summary>
/// Populate 후 부수효과(아이콘 갱신, 캐시 무효화 등) 처리.
/// v0.1: 무동작 + 디버그 로그. 통합 테스트(Task 23)에서 미반영 항목 발견 시 추가.
/// 예) ((Hero)player).heroIconDirty = true;
///     SomeManager.Instance.RebuildHeroCache();
/// </summary>
public static class PinpointPatcher
{
    public static void RefreshAfterApply(object player)
    {
        Logger.Debug("PinpointPatcher.RefreshAfterApply (no-op in v0.1)");
    }
}
