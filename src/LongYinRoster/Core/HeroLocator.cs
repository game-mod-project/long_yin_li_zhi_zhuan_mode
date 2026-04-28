using System;
using System.Linq;
using System.Reflection;
using LongYinRoster.Util;
using Logger = LongYinRoster.Util.Logger;

namespace LongYinRoster.Core;

/// <summary>
/// 게임 안에서 heroID=0 인 HeroData(플레이어) 객체를 찾는다.
///
/// 조사 결과:
/// - HeroData 는 [Serializable] POCO (UnityEngine.Object 상속 안 함).
///   따라서 Resources.FindObjectsOfTypeAll 경로는 영구히 작동 불가.
/// - 유일한 신뢰 경로는 GameDataController.Instance.gameSaveData.HeroList 순회.
///
/// Task 17 1차 검증에서 양 경로 모두 null 반환했고 단계별 진단 로그가 없어
/// 정확한 실패점 식별 불가했다. 이 버전은 단계별 Logger.Info 와 generic
/// singleton base 에 대응하는 BindingFlags.FlattenHierarchy + property→field
/// fallback 을 추가한 evidence-gathering 빌드.
/// </summary>
public static class HeroLocator
{
    private static object? _cached;
    private static DateTime _lastNegativeAt = DateTime.MinValue;
    private static readonly TimeSpan NegativeCacheTtl = TimeSpan.FromSeconds(1);

    /// <summary>
    /// 양성 캐시 → 매번 실제 시도. CaptureCurrent 등 사용자 액션 경로용.
    /// 매번 단계별 진단 로그를 찍는다.
    /// </summary>
    public static object? GetPlayer()
    {
        if (_cached != null && IsValidPlayer(_cached)) return _cached;

        var viaManager = TryViaGameDataController();
        if (viaManager != null) { _cached = viaManager; return viaManager; }

        return null;
    }

    /// <summary>
    /// IMGUI OnGUI 매 프레임 호출용. 음성 결과를 1초 캐싱해서 로그 폭주 차단.
    /// </summary>
    public static bool IsInGame()
    {
        if (_cached != null && IsValidPlayer(_cached)) return true;
        if (DateTime.UtcNow - _lastNegativeAt < NegativeCacheTtl) return false;

        var p = GetPlayer();
        if (p == null) _lastNegativeAt = DateTime.UtcNow;
        return p != null;
    }

    public static void InvalidateCache()
    {
        _cached = null;
        _lastNegativeAt = DateTime.MinValue;
    }

    // ------------------------------------------------------------------ path

    private static object? TryViaGameDataController()
    {
        try
        {
            var ctrlType = FindTypeByName("GameDataController");
            if (ctrlType == null)
            {
                Logger.Warn("HeroLocator: GameDataController type not found in any loaded assembly");
                return null;
            }
            Logger.Info($"HeroLocator: GameDataController = {ctrlType.AssemblyQualifiedName}");

            var inst = ReadStaticMember(ctrlType, "Instance");
            if (inst == null)
            {
                Logger.Warn("HeroLocator: GameDataController.Instance is null (game not started or different singleton accessor)");
                return null;
            }
            Logger.Info($"HeroLocator: Instance runtime type = {inst.GetType().FullName}");

            var saveData = ReadInstanceMember(inst, "gameSaveData");
            if (saveData == null)
            {
                Logger.Warn("HeroLocator: gameSaveData member returned null (member missing or value is null)");
                return null;
            }
            Logger.Info($"HeroLocator: gameSaveData runtime type = {saveData.GetType().FullName}");

            var heroList = ReadInstanceMember(saveData, "HeroList");
            if (heroList == null)
            {
                Logger.Warn("HeroLocator: HeroList member returned null");
                return null;
            }
            Logger.Info($"HeroLocator: HeroList runtime type = {heroList.GetType().FullName}");

            // Il2CppSystem.Collections.Generic.List<T> 는 .NET IEnumerable 미구현.
            // Count + indexer (Item property 또는 get_Item(int) 메서드) reflection 으로 순회.
            var listType = heroList.GetType();
            var countProp = listType.GetProperty("Count", InstanceFlags);
            if (countProp == null)
            {
                Logger.Warn($"HeroLocator: HeroList type {listType.FullName} has no Count property");
                return null;
            }
            int n = Convert.ToInt32(countProp.GetValue(heroList));
            Logger.Info($"HeroLocator: HeroList Count = {n}");

            var itemProp      = listType.GetProperty("Item", InstanceFlags);
            var getItemMethod = listType.GetMethod("get_Item", InstanceFlags, null, new[] { typeof(int) }, null);
            if (itemProp == null && getItemMethod == null)
            {
                Logger.Warn($"HeroLocator: HeroList type {listType.FullName} has no indexer (Item / get_Item(int))");
                return null;
            }

            int validIds = 0;
            for (int i = 0; i < n; i++)
            {
                object? h = itemProp != null
                    ? itemProp.GetValue(heroList, new object[] { i })
                    : getItemMethod!.Invoke(heroList, new object[] { i });
                if (h == null) continue;
                if (TryGetHeroId(h, out var id))
                {
                    validIds++;
                    if (id == 0)
                    {
                        Logger.Info($"HeroLocator: matched heroID=0 at index {i}");
                        return h;
                    }
                }
            }
            Logger.Warn($"HeroLocator: iterated {n} entries ({validIds} with readable heroID), no heroID==0 found");
        }
        catch (Exception ex)
        {
            Logger.Warn($"HeroLocator manager path threw: {ex.GetType().Name}: {ex.Message}");
        }
        return null;
    }

    // -------------------------------------------------------- reflection helpers

    private const BindingFlags StaticFlags =
        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.FlattenHierarchy;

    private const BindingFlags InstanceFlags =
        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

    private static object? ReadStaticMember(Type t, string name)
    {
        // property -> field, then common alternate singleton names
        var p = t.GetProperty(name, StaticFlags);
        if (p != null) return p.GetValue(null);
        var f = t.GetField(name, StaticFlags);
        if (f != null) return f.GetValue(null);

        foreach (var alt in new[] { "instance", "_instance", "s_Instance", "s_instance" })
        {
            var pa = t.GetProperty(alt, StaticFlags);
            if (pa != null) { Logger.Info($"HeroLocator: static fallback hit property '{alt}' on {t.Name}"); return pa.GetValue(null); }
            var fa = t.GetField(alt, StaticFlags);
            if (fa != null) { Logger.Info($"HeroLocator: static fallback hit field '{alt}' on {t.Name}"); return fa.GetValue(null); }
        }
        return null;
    }

    private static object? ReadInstanceMember(object obj, string name)
    {
        var t = obj.GetType();
        var p = t.GetProperty(name, InstanceFlags);
        if (p != null) return p.GetValue(obj);
        var f = t.GetField(name, InstanceFlags);
        if (f != null) return f.GetValue(obj);
        return null;
    }

    private static Type? FindTypeByName(string name)
    {
        return AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(SafeGetTypes)
            .FirstOrDefault(t => t.Name == name
                && (t.Namespace == null
                    || !t.Namespace.StartsWith("LongYinRoster", StringComparison.Ordinal)));
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
            var p = t.GetProperty("heroID", InstanceFlags);
            if (p != null) { var v = p.GetValue(obj); if (v != null) { id = Convert.ToInt32(v); return true; } }

            var f = t.GetField("heroID", InstanceFlags);
            if (f != null) { var v = f.GetValue(obj); if (v != null) { id = Convert.ToInt32(v); return true; } }
        }
        catch { }
        return false;
    }
}
