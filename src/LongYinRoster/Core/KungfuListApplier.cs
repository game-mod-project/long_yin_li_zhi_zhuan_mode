using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.Json;
using Logger = LongYinRoster.Util.Logger;

namespace LongYinRoster.Core;

/// <summary>
/// v0.5.2 — 무공 list Replace (clear + add all).
///
/// Spike Phase 1 결과로 확정된 path:
///   - Clear: HeroData.LoseAllSkill() — parameterless
///   - Wrapper ctor: KungfuSkillLvData(int _skillID) — Spike Step 6 발견
///   - Property setter: lv / fightExp / bookExp / 기타 reflection set
///   - Add: HeroData.GetSkill(KungfuSkillLvData wrapper, bool showInfo=false, bool speShow=false)
///
/// v0.4 PoC A1 의 KungfuSkillLvData wrapper ctor IL2CPP 한계 — Spike 에서 false 로 검증.
/// (int _skillID) ctor 가 정상 작동.
/// </summary>
public static class KungfuListApplier
{
    public sealed class Result
    {
        public bool Skipped { get; set; }
        public string? Reason { get; set; }
        public int RemovedCount { get; set; }
        public int AddedCount { get; set; }
        public int FailedCount { get; set; }
    }

    public sealed record KungfuEntry(int SkillID, int Lv, float FightExp, float BookExp);

    private const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
    private const string ClearMethodName = "LoseAllSkill";
    private const string AddMethodName   = "GetSkill";

    public static IReadOnlyList<KungfuEntry> ExtractKungfuList(JsonElement slot)
    {
        var list = new List<KungfuEntry>();
        if (!slot.TryGetProperty("kungfuSkills", out var arr) || arr.ValueKind != JsonValueKind.Array)
            return list;

        for (int i = 0; i < arr.GetArrayLength(); i++)
        {
            var entry = arr[i];
            if (entry.ValueKind != JsonValueKind.Object) continue;
            if (!entry.TryGetProperty("skillID", out var idEl) || idEl.ValueKind != JsonValueKind.Number) continue;
            int skillID = idEl.GetInt32();
            int lv = entry.TryGetProperty("lv", out var lvEl) && lvEl.ValueKind == JsonValueKind.Number ? lvEl.GetInt32() : 1;
            float fe = entry.TryGetProperty("fightExp", out var feEl) && feEl.ValueKind == JsonValueKind.Number ? feEl.GetSingle() : 0f;
            float be = entry.TryGetProperty("bookExp", out var beEl) && beEl.ValueKind == JsonValueKind.Number ? beEl.GetSingle() : 0f;
            list.Add(new KungfuEntry(skillID, lv, fe, be));
        }
        return list;
    }

    public static Result Apply(object? player, JsonElement slot, ApplySelection sel)
    {
        var res = new Result();

        if (!sel.KungfuList)
        {
            res.Skipped = true;
            res.Reason = "kungfuList (selection off)";
            return res;
        }

        var list = ExtractKungfuList(slot);
        if (player == null)
        {
            res.Skipped = true;
            res.Reason = "player null (test mode)";
            return res;
        }

        var ksList = ReadFieldOrProperty(player, "kungfuSkills");
        if (ksList == null)
        {
            res.Skipped = true;
            res.Reason = "kungfuSkills null";
            return res;
        }

        // Wrapper type 발견 — 첫 element 의 type 또는 ksList element type
        Type? wrapperType = null;
        if (IL2CppListOps.Count(ksList) > 0)
        {
            var sample = IL2CppListOps.Get(ksList, 0);
            if (sample != null) wrapperType = sample.GetType();
        }
        if (wrapperType == null)
        {
            res.Skipped = true;
            res.Reason = "wrapperType null (kungfuSkills empty before clear)";
            return res;
        }

        // Wrapper ctor (int _skillID) 발견
        var wrapperCtor = wrapperType.GetConstructor(F, null, new[] { typeof(int) }, null);
        if (wrapperCtor == null)
        {
            res.Skipped = true;
            res.Reason = $"wrapper ctor (int) not found on {wrapperType.FullName}";
            return res;
        }

        // Clear phase
        int beforeCount = IL2CppListOps.Count(ksList);
        try
        {
            InvokeMethod(player, ClearMethodName, Array.Empty<object>());
            int afterCount = IL2CppListOps.Count(ksList);
            res.RemovedCount = beforeCount - afterCount;
            Logger.Info($"KungfuList clear ({ClearMethodName}): {beforeCount} → {afterCount}");
        }
        catch (Exception ex)
        {
            Logger.Warn($"KungfuList clear: {ex.GetType().Name}: {ex.Message}");
            res.Skipped = true;
            res.Reason = $"clear failed: {ex.Message}";
            return res;
        }

        // Add phase — wrapper 생성 + property set + GetSkill 호출
        foreach (var entry in list)
        {
            try
            {
                var wrapper = wrapperCtor.Invoke(new object[] { entry.SkillID });
                TrySetMember(wrapper, "lv", entry.Lv);
                TrySetMember(wrapper, "fightExp", entry.FightExp);
                TrySetMember(wrapper, "bookExp", entry.BookExp);

                // GetSkill(wrapper, false, false) — showInfo=false, speShow=false
                InvokeMethod(player, AddMethodName, new object[] { wrapper, false, false });
                res.AddedCount++;
            }
            catch (Exception ex)
            {
                res.FailedCount++;
                Logger.Warn($"KungfuList add skillID={entry.SkillID}: {ex.GetType().Name}: {ex.Message}");
            }
        }

        Logger.Info($"KungfuList Apply done — removed={res.RemovedCount} added={res.AddedCount} failed={res.FailedCount}");
        return res;
    }

    public static Result Restore(object? player, JsonElement backup)
    {
        return Apply(player, backup, new ApplySelection { KungfuList = true });
    }

    private static object? ReadFieldOrProperty(object obj, string name)
    {
        var t = obj.GetType();
        var p = t.GetProperty(name, F);
        if (p != null) return p.GetValue(obj);
        var f = t.GetField(name, F);
        if (f != null) return f.GetValue(obj);
        return null;
    }

    private static void TrySetMember(object obj, string name, object value)
    {
        var t = obj.GetType();
        var p = t.GetProperty(name, F);
        if (p != null && p.CanWrite)
        {
            try { p.SetValue(obj, value); } catch { }
            return;
        }
        var f = t.GetField(name, F);
        if (f != null)
        {
            try { f.SetValue(obj, value); } catch { }
        }
    }

    private static void InvokeMethod(object obj, string methodName, object[] args)
    {
        var t = obj.GetType();
        MethodInfo? best = null;
        foreach (var m in t.GetMethods(F))
        {
            if (m.Name != methodName) continue;
            var ps = m.GetParameters();
            if (ps.Length < args.Length) continue;
            if (best == null || ps.Length < best.GetParameters().Length) best = m;
        }
        if (best == null) throw new MissingMethodException(t.FullName, methodName);
        var ps2 = best.GetParameters();
        var full = new object?[ps2.Length];
        for (int i = 0; i < ps2.Length; i++)
            full[i] = i < args.Length ? args[i]
                : (ps2[i].ParameterType.IsValueType ? Activator.CreateInstance(ps2[i].ParameterType) : null);
        best.Invoke(obj, full);
    }
}
