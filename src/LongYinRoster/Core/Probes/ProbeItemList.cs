using System;
using System.Reflection;
using Logger = LongYinRoster.Util.Logger;

namespace LongYinRoster.Core.Probes;

/// <summary>
/// v0.5.3 Spike Phase 1 — 인벤토리 game-self method + ItemData ctor discovery.
///
/// 3 modes:
///   Step1 = HeroData method dump (Lose|Add|Get|Remove*Item* 시그니처)
///   Step2 = ItemData wrapper type ctor + static method dump
///   Step3 = persistence baseline (현재 itemListData.allItem 의 first 10 entries)
/// </summary>
public static class ProbeItemList
{
    public enum Mode { Step1, Step2, Step3 }

    private const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

    public static void Run(Mode mode)
    {
        var player = HeroLocator.GetPlayer();
        if (player == null) { Logger.Warn("Spike: player null"); return; }

        switch (mode)
        {
            case Mode.Step1: RunStep1(player); break;
            case Mode.Step2: RunStep2(player); break;
            case Mode.Step3: RunStep3(player); break;
        }
    }

    private static void RunStep1(object player)
    {
        var t = player.GetType();
        var pattern = new System.Text.RegularExpressions.Regex(
            @"^(Lose|Add|Get|Remove|Drop)(All)?Item",
            System.Text.RegularExpressions.RegexOptions.Compiled);

        Logger.Info("=== Spike Step1 — HeroData *Item* method dump ===");
        foreach (var m in t.GetMethods(F))
        {
            if (!pattern.IsMatch(m.Name)) continue;
            var ps = m.GetParameters();
            var sig = string.Join(", ", System.Linq.Enumerable.Select(ps, p => $"{p.ParameterType.Name} {p.Name}"));
            Logger.Info($"method: {m.ReturnType.Name} {m.Name}({sig})");
        }
        Logger.Info("=== Spike Step1 end ===");
    }

    private static void RunStep2(object player)
    {
        var ild = ReadField(player, "itemListData");
        if (ild == null) { Logger.Warn("Spike Step2: itemListData null"); return; }
        var allItem = ReadField(ild, "allItem");
        if (allItem == null) { Logger.Warn("Spike Step2: allItem null"); return; }

        int count = IL2CppListOps.Count(allItem);
        Logger.Info($"Spike Step2: itemListData.allItem count={count}");
        if (count == 0) { Logger.Warn("Spike Step2: allItem 비어있음 — wrapper type 알 수 없음"); return; }

        var sample = IL2CppListOps.Get(allItem, 0);
        if (sample == null) { Logger.Warn("Spike Step2: sample null"); return; }
        var wrapperType = sample.GetType();
        Logger.Info($"=== Spike Step2 — ItemData ({wrapperType.FullName}) dump ===");

        Logger.Info("--- Constructors ---");
        foreach (var ctor in wrapperType.GetConstructors(F | BindingFlags.Static))
        {
            var ps = ctor.GetParameters();
            var sig = string.Join(", ", System.Linq.Enumerable.Select(ps, p => $"{p.ParameterType.Name} {p.Name}"));
            Logger.Info($"ctor: ({sig})");
        }

        Logger.Info("--- Static methods ---");
        foreach (var m in wrapperType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static))
        {
            var ps = m.GetParameters();
            var sig = string.Join(", ", System.Linq.Enumerable.Select(ps, p => $"{p.ParameterType.Name} {p.Name}"));
            Logger.Info($"static: {m.ReturnType.Name} {m.Name}({sig})");
        }
        Logger.Info("=== Spike Step2 end ===");
    }

    private static void RunStep3(object player)
    {
        var ild = ReadField(player, "itemListData");
        if (ild == null) { Logger.Warn("Spike Step3: itemListData null"); return; }
        var allItem = ReadField(ild, "allItem");
        if (allItem == null) { Logger.Warn("Spike Step3: allItem null"); return; }

        int count = IL2CppListOps.Count(allItem);
        Logger.Info($"Spike Step3: itemListData.allItem count={count}");
        int dumpN = System.Math.Min(count, 10);
        for (int i = 0; i < dumpN; i++)
        {
            var w = IL2CppListOps.Get(allItem, i);
            if (w == null) continue;
            int id = (int)(ReadField(w, "itemID") ?? -1);
            int cnt = (int)(ReadField(w, "itemCount") ?? -1);
            Logger.Info($"Spike Step3: [{i}] itemID={id} itemCount={cnt}");
        }
        Logger.Info("Spike Step3: save → reload → 위 list 와 일치하는지 사용자 확인");
    }

    private static object? ReadField(object obj, string name)
    {
        var t = obj.GetType();
        var p = t.GetProperty(name, F);
        if (p != null) return p.GetValue(obj);
        var f = t.GetField(name, F);
        if (f != null) return f.GetValue(obj);
        return null;
    }
}
