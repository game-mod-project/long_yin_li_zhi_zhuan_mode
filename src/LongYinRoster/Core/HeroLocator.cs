using System;
using System.Linq;
using System.Reflection;
using LongYinRoster.Util;
using Logger = LongYinRoster.Util.Logger;

namespace LongYinRoster.Core;

/// <summary>
/// 게임 안에서 heroID=0 인 HeroData(플레이어) 객체를 찾는다.
///
/// 조사 결과(Task 17 직전):
/// - 게임에서 영웅 타입 실명은 <c>HeroData</c> (174 프로퍼티, [Serializable]).
/// - 영웅 컬렉션은 <c>GameDataController.Instance.gameSaveData.HeroList</c> (List&lt;HeroData&gt;).
/// - 따라서 GameDataController 싱글톤을 통한 경로가 가장 신뢰성 있고 빠르다.
///
/// 우선순위:
///   1) 캐시 검증
///   2) GameDataController.Instance.gameSaveData.HeroList 순회 (정도)
///   3) FindObjectsOfTypeAll&lt;HeroData&gt; 백업 경로
/// </summary>
public static class HeroLocator
{
    private static object? _cached;

    public static object? GetPlayer()
    {
        if (_cached != null && IsValidPlayer(_cached)) return _cached;

        // 1) 게임 매니저 경로 — 가장 신뢰
        var viaManager = TryViaGameDataController();
        if (viaManager != null) { _cached = viaManager; return viaManager; }

        // 2) Resources.FindObjectsOfTypeAll 백업
        var viaScan = TryViaScan();
        if (viaScan != null) { _cached = viaScan; return viaScan; }

        return null;
    }

    public static bool IsInGame() => GetPlayer() != null;

    public static void InvalidateCache() => _cached = null;

    // ------------------------------------------------------------------ paths

    private static object? TryViaGameDataController()
    {
        try
        {
            var ctrlType = FindTypeByName("GameDataController");
            if (ctrlType == null) return null;

            var inst = ctrlType.GetProperty("Instance",
                BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
            if (inst == null) return null;

            var saveData = ctrlType.GetProperty("gameSaveData",
                BindingFlags.Public | BindingFlags.Instance)?.GetValue(inst);
            if (saveData == null) return null;

            var heroListProp = saveData.GetType().GetProperty("HeroList",
                BindingFlags.Public | BindingFlags.Instance);
            var heroList = heroListProp?.GetValue(saveData);
            if (heroList == null) return null;

            // List<HeroData> 는 IEnumerable. foreach 시도.
            if (heroList is System.Collections.IEnumerable iter)
            {
                foreach (var h in iter)
                {
                    if (h != null && TryGetHeroId(h, out var id) && id == 0)
                        return h;
                }
            }
        }
        catch (Exception ex) { Logger.Warn($"HeroLocator manager path: {ex.Message}"); }
        return null;
    }

    private static object? TryViaScan()
    {
        try
        {
            var heroType = FindTypeByName("HeroData");
            if (heroType == null) { Logger.Warn("HeroData type not found"); return null; }

            var il2Type = Il2CppInterop.Runtime.Il2CppType.From(heroType);
            var found = UnityEngine.Resources.FindObjectsOfTypeAll(il2Type);
            foreach (var obj in found)
            {
                if (obj == null) continue;
                if (TryGetHeroId(obj, out var id) && id == 0) return obj;
            }
        }
        catch (Exception ex) { Logger.Warn($"HeroLocator scan: {ex.Message}"); }
        return null;
    }

    // ------------------------------------------------------------------ helpers

    private static Type? FindTypeByName(string name)
    {
        return AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(SafeGetTypes)
            .FirstOrDefault(t => t.Name == name && t.Namespace != "LongYinRoster.Core");
    }

    private static Type[] SafeGetTypes(Assembly a)
    {
        try { return a.GetTypes(); } catch { return Array.Empty<Type>(); }
    }

    private static bool IsValidPlayer(object obj) =>
        TryGetHeroId(obj, out var id) && id == 0;

    private static bool TryGetHeroId(object obj, out int id)
    {
        id = -1;
        var t = obj.GetType();
        try
        {
            // HeroData 는 [Serializable] POCO. Il2CppInterop proxy 는 보통
            // 실제 IL2CPP 필드를 'get_X / set_X' 프로퍼티 쌍으로 노출한다.
            var p = t.GetProperty("heroID", BindingFlags.Public | BindingFlags.Instance);
            if (p != null) { var v = p.GetValue(obj); if (v != null) { id = Convert.ToInt32(v); return true; } }

            var f = t.GetField("heroID", BindingFlags.Public | BindingFlags.Instance);
            if (f != null) { var v = f.GetValue(obj); if (v != null) { id = Convert.ToInt32(v); return true; } }
        }
        catch { }
        return false;
    }
}
