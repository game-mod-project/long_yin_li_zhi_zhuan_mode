using System;
using System.Reflection;
using LongYinRoster.Core;
using Logger = LongYinRoster.Util.Logger;

namespace LongYinRoster.Core.Probes;

/// <summary>
/// v0.7.1 Phase 1 spike — player.itemListData / player.selfStorage 의
/// capacity 후보 property/field enumerate. F12 핸들러로 1회 호출 후 BepInEx
/// 로그 분석. 결정 후 본 파일은 git 에 보존하되 [F12] handler 는 제거 (release 전).
/// </summary>
public static class ProbeItemListCapacity
{
    private const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

    private static readonly string[] Keywords = new[]
    {
        "capacity", "max", "limit", "size", "volume", "count"
    };

    public static void Run()
    {
        Logger.Info("=== ProbeItemListCapacity.Run ===");
        var p = HeroLocator.GetPlayer();
        if (p == null) { Logger.Warn("player null — 게임 진입 후 시도"); return; }
        DumpOne(p, "itemListData");
        DumpOne(p, "selfStorage");
        Logger.Info("=== ProbeItemListCapacity.Run end ===");
    }

    private static void DumpOne(object player, string fieldName)
    {
        var ild = ReadFieldOrProperty(player, fieldName);
        if (ild == null) { Logger.Warn($"{fieldName} null"); return; }
        var t = ild.GetType();
        Logger.Info($"--- {fieldName} type={t.FullName} ---");

        foreach (var prop in t.GetProperties(F))
        {
            string n = prop.Name.ToLowerInvariant();
            foreach (var kw in Keywords)
            {
                if (n.Contains(kw))
                {
                    object? v = null;
                    try { v = prop.GetValue(ild); } catch (Exception ex) { v = $"<throw {ex.GetType().Name}>"; }
                    Logger.Info($"  prop {prop.PropertyType.Name} {prop.Name} = {v}");
                    break;
                }
            }
        }
        foreach (var fld in t.GetFields(F))
        {
            string n = fld.Name.ToLowerInvariant();
            foreach (var kw in Keywords)
            {
                if (n.Contains(kw))
                {
                    object? v = null;
                    try { v = fld.GetValue(ild); } catch (Exception ex) { v = $"<throw {ex.GetType().Name}>"; }
                    Logger.Info($"  fld  {fld.FieldType.Name} {fld.Name} = {v}");
                    break;
                }
            }
        }
    }

    private static object? ReadFieldOrProperty(object obj, string name)
    {
        var t = obj.GetType();
        var prop = t.GetProperty(name, F);
        if (prop != null) return prop.GetValue(obj);
        var fld = t.GetField(name, F);
        if (fld != null) return fld.GetValue(obj);
        return null;
    }
}
