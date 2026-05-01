using System;
using System.Reflection;
using Logger = LongYinRoster.Util.Logger;

namespace LongYinRoster.Core.Probes;

/// <summary>
/// v0.5 외형 PoC Phase 2. portraitID + gender setter direct 시도 + refresh method 호출.
/// PASS 기준: 게임 화면 초상화 즉시 변경 + save-reload 후 유지 (사용자 G1 게이트).
/// </summary>
internal static class ProbePortraitRefresh
{
    private const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

    public static void Run(object player)
    {
        var t = player.GetType();
        Logger.Info($"player type: {t.FullName}");

        // 1. 현재값 read
        int currentPortrait = ReadInt(player, "portraitID");
        int currentGender   = ReadInt(player, "gender");
        Logger.Info($"current portraitID={currentPortrait}, gender={currentGender}");

        // 2. setter direct 시도 — read-back 검증
        int newPortrait = currentPortrait + 1;
        TrySetterDirect(player, "set_portraitID", newPortrait);
        int afterSetter = ReadInt(player, "portraitID");
        Logger.Info($"after setter direct: portraitID={afterSetter} (expected {newPortrait})");

        if (afterSetter != newPortrait)
        {
            Logger.Warn("setter direct silent no-op — fallback: backing field reflection");
            TryFieldDirect(player, "portraitID", newPortrait);
            int afterField = ReadInt(player, "portraitID");
            Logger.Info($"after field set: portraitID={afterField} (expected {newPortrait})");
        }

        // 3. 후보 refresh method 들 순회 호출
        // T6 (static dump) 가 skip 되었으므로 흔한 후보 names 직접 시도.
        // RefreshSelfState 는 v0.4 의 PinpointPatcher 가 이미 사용 — sprite cache invalidate 가 동시에 일어날 가능성.
        string[] candidateMethods =
        {
            "RefreshPortrait",
            "ReloadPortrait",
            "UpdatePortrait",
            "RefreshFaceData",
            "RefreshFace",
            "RefreshAvatar",
            "RefreshSprite",
            "RefreshSelfState",  // v0.4 알려진 — 부수효과로 portrait 도 refresh 될 가능성
        };

        foreach (var name in candidateMethods)
        {
            TryCall(player, name);
        }

        Logger.Info("=== Phase 2 done. 화면 + save-reload 후 G1 판정 ===");
    }

    private static int ReadInt(object obj, string field)
    {
        var prop = obj.GetType().GetProperty(field, F);
        if (prop != null)
        {
            try { return (int)(prop.GetValue(obj) ?? 0); }
            catch (Exception ex) { Logger.Warn($"read property {field} threw: {ex.GetType().Name}: {ex.Message}"); return -1; }
        }
        var fld = obj.GetType().GetField(field, F);
        if (fld != null)
        {
            try { return (int)(fld.GetValue(obj) ?? 0); }
            catch (Exception ex) { Logger.Warn($"read field {field} threw: {ex.GetType().Name}: {ex.Message}"); return -1; }
        }
        Logger.Warn($"field/property '{field}' not found");
        return -1;
    }

    private static void TrySetterDirect(object obj, string methodName, int value)
    {
        var m = obj.GetType().GetMethod(methodName, F);
        if (m == null) { Logger.Warn($"method '{methodName}' not found"); return; }
        try { m.Invoke(obj, new object[] { value }); Logger.Info($"called {methodName}({value})"); }
        catch (Exception ex) { Logger.Warn($"{methodName} threw: {ex.GetType().Name}: {ex.Message}"); }
    }

    private static void TryFieldDirect(object obj, string field, int value)
    {
        var fld = obj.GetType().GetField(field, F);
        if (fld == null) { Logger.Warn($"field '{field}' not found"); return; }
        try { fld.SetValue(obj, value); Logger.Info($"field-set {field}={value}"); }
        catch (Exception ex) { Logger.Warn($"field-set {field} threw: {ex.GetType().Name}: {ex.Message}"); }
    }

    private static void TryCall(object obj, string methodName)
    {
        var m = obj.GetType().GetMethod(methodName, F, null, Type.EmptyTypes, null);
        if (m == null) { Logger.Info($"method '{methodName}': not found (skip)"); return; }
        try { m.Invoke(obj, null); Logger.Info($"called {methodName}() — ok"); }
        catch (Exception ex) { Logger.Warn($"{methodName} threw: {ex.GetType().Name}: {ex.Message}"); }
    }
}
