using System;
using System.Collections.Generic;
using System.Reflection;
using LongYinRoster.Util;
using Logger = LongYinRoster.Util.Logger;

namespace LongYinRoster.Core;

/// <summary>
/// v0.7.8 Phase 5 — kungfuSkills (List&lt;KungfuSkillLvData&gt;) add/remove/edit 헬퍼.
/// Spike 결과 (2026-05-09) + cheat 검증 (SkillManager.cs:315 `skill.lv`):
///   - List Count = 168 (player 보유 무공 수)
///   - 진짜 level field 명 = **`lv`** (short form, NOT `level`)
///   - `lv` = read-only (game internal calculate from fightExp/bookExp). 변경 = `Upgrade(int)` method
///   - `fightExp` / `bookExp` / `skillID` / `equiped`: propWrite=True
///   - add: `player.GetSkill(KungfuSkillLvData wrapper, bool, bool)` — 3-arg
///   - remove: `player.LoseSkill(KungfuSkillLvData wrapper)` — single skill
///   - active query: `player.GetNowActiveSkill()`
/// </summary>
public static class KungfuSkillEditor
{
    private const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

    /// <summary>현재 player 의 무공 list — (skillID, level, fightExp, bookExp, equiped).</summary>
    public static List<(int SkillID, int Level, float FightExp, float BookExp, bool Equiped)> GetEntries(object player)
    {
        var result = new List<(int, int, float, float, bool)>();
        if (player == null) return result;
        try
        {
            var skillList = ReadFieldOrProperty(player, "kungfuSkills");
            if (skillList == null) return result;

            var countProp = skillList.GetType().GetProperty("Count", F);
            int n = countProp != null ? Convert.ToInt32(countProp.GetValue(skillList)) : 0;
            var indexer = skillList.GetType().GetMethod("get_Item", F);
            if (indexer == null) return result;

            for (int i = 0; i < n; i++)
            {
                var entry = indexer.Invoke(skillList, new object[] { i });
                if (entry == null) continue;
                int  skillID  = ReadInt(entry, "skillID");
                int  level    = ReadInt(entry, "lv");   // cheat 검증: KungfuSkillLvData.lv (NOT "level")
                float fight   = ReadFloat(entry, "fightExp");
                float book    = ReadFloat(entry, "bookExp");
                bool  equiped = ReadBool(entry, "equiped");
                result.Add((skillID, level, fight, book, equiped));
            }
        }
        catch (Exception ex)
        {
            Logger.WarnOnce("KungfuSkillEditor", $"KungfuSkillEditor.GetEntries: {ex.GetType().Name}: {ex.Message}");
        }
        return result;
    }

    /// <summary>
    /// skillID 의 KungfuSkillLvData wrapper instance 를 list 에서 검색.
    /// LoseSkill / set_fightExp 등에 인자로 사용.
    /// </summary>
    public static object? FindSkillWrapper(object player, int skillID)
    {
        if (player == null) return null;
        try
        {
            var skillList = ReadFieldOrProperty(player, "kungfuSkills");
            if (skillList == null) return null;
            var countProp = skillList.GetType().GetProperty("Count", F);
            int n = countProp != null ? Convert.ToInt32(countProp.GetValue(skillList)) : 0;
            var indexer = skillList.GetType().GetMethod("get_Item", F);
            if (indexer == null) return null;
            for (int i = 0; i < n; i++)
            {
                var entry = indexer.Invoke(skillList, new object[] { i });
                if (entry == null) continue;
                if (ReadInt(entry, "skillID") == skillID) return entry;
            }
        }
        catch (Exception ex)
        {
            Logger.WarnOnce("KungfuSkillEditor", $"KungfuSkillEditor.FindSkillWrapper({skillID}): {ex.GetType().Name}: {ex.Message}");
        }
        return null;
    }

    /// <summary>
    /// 신규 skill 추가. KungfuSkillLvData(skillID) ctor 로 wrapper 생성 → player.GetSkill(wrapper, false, false).
    /// IL2CPP 환경에선 ctor 도 reflection 으로 접근. 이미 보유 시 GetSkill 가 no-op 또는 error.
    /// </summary>
    public static bool TryAddSkill(object player, int skillID)
    {
        if (player == null) return false;
        if (skillID <= 0) return false;
        try
        {
            // 이미 있으면 skip
            if (FindSkillWrapper(player, skillID) != null) return true;

            // KungfuSkillLvData type 찾기
            var wrapperType = Type.GetType("KungfuSkillLvData, Assembly-CSharp");
            if (wrapperType == null)
            {
                // fallback — 기존 entry 의 type 으로 ctor 검색
                var existingList = ReadFieldOrProperty(player, "kungfuSkills");
                if (existingList != null)
                {
                    var indexer = existingList.GetType().GetMethod("get_Item", F);
                    var countProp = existingList.GetType().GetProperty("Count", F);
                    int n = countProp != null ? Convert.ToInt32(countProp.GetValue(existingList)) : 0;
                    if (n > 0 && indexer != null)
                    {
                        var existing = indexer.Invoke(existingList, new object[] { 0 });
                        if (existing != null) wrapperType = existing.GetType();
                    }
                }
            }
            if (wrapperType == null) { Logger.Warn("KungfuSkillEditor.TryAddSkill: KungfuSkillLvData type 미발견"); return false; }

            // ctor (int) 또는 (int, int) 시도
            ConstructorInfo? ctor = null;
            foreach (var c in wrapperType.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            {
                var ps = c.GetParameters();
                if (ps.Length == 1 && ps[0].ParameterType == typeof(int)) { ctor = c; break; }
            }
            object? wrapper = null;
            if (ctor != null) wrapper = ctor.Invoke(new object[] { skillID });
            else
            {
                // skillID 만 set 하는 다른 방법 — Activator 사용
                wrapper = Activator.CreateInstance(wrapperType);
                if (wrapper != null) TryReflectionSetter(wrapper, "skillID", skillID);
            }
            if (wrapper == null) return false;

            // player.GetSkill(wrapper, false, false) — 3-arg variant
            var getSkillM = FindMethod(player, "GetSkill", new[] { wrapperType, typeof(bool), typeof(bool) });
            if (getSkillM != null)
            {
                getSkillM.Invoke(player, new object[] { wrapper, false, false });
                return true;
            }
            // fallback — 1-arg
            var getSkill1 = FindMethod(player, "GetSkill", new[] { wrapperType });
            if (getSkill1 != null)
            {
                getSkill1.Invoke(player, new object[] { wrapper });
                return true;
            }
        }
        catch (Exception ex)
        {
            Logger.WarnOnce("KungfuSkillEditor", $"KungfuSkillEditor.TryAddSkill({skillID}): {ex.GetType().Name}: {ex.Message}");
        }
        return false;
    }

    /// <summary>player.LoseSkill(wrapper) 호출.</summary>
    public static bool TryRemoveSkill(object player, int skillID)
    {
        if (player == null) return false;
        try
        {
            var wrapper = FindSkillWrapper(player, skillID);
            if (wrapper == null) return false;
            var loseM = FindMethod(player, "LoseSkill", new[] { wrapper.GetType() });
            if (loseM != null)
            {
                loseM.Invoke(player, new object[] { wrapper });
                return true;
            }
        }
        catch (Exception ex)
        {
            Logger.WarnOnce("KungfuSkillEditor", $"KungfuSkillEditor.TryRemoveSkill({skillID}): {ex.GetType().Name}: {ex.Message}");
        }
        return false;
    }

    /// <summary>fightExp / bookExp 직접 set (Property setter 작동 검증, level 은 read-only 라 별도 path).</summary>
    public static bool TrySetExp(object player, int skillID, string field, float value)
    {
        if (player == null || (field != "fightExp" && field != "bookExp")) return false;
        var wrapper = FindSkillWrapper(player, skillID);
        if (wrapper == null) return false;
        return TryReflectionSetter(wrapper, field, value);
    }

    /// <summary>
    /// v0.7.8 — 돌파속성 sub-data wrapper access.
    /// fieldName: "speEquipData" / "speUseData" / "extraAddData" (HeroSpeAddData 객체)
    /// </summary>
    public static object? GetSubAddData(object player, int skillID, string fieldName)
    {
        var wrapper = FindSkillWrapper(player, skillID);
        if (wrapper == null) return null;
        return ReadFieldOrProperty(wrapper, fieldName);
    }

    /// <summary>
    /// v0.7.8 — 돌파속성 single value (equipUseSpeAddValue / damageUseSpeAddValue) read.
    /// </summary>
    public static float GetSingleValue(object player, int skillID, string fieldName)
    {
        var wrapper = FindSkillWrapper(player, skillID);
        if (wrapper == null) return 0f;
        try
        {
            var v = ReadFieldOrProperty(wrapper, fieldName);
            if (v == null) return 0f;
            return Convert.ToSingle(v);
        }
        catch { return 0f; }
    }

    /// <summary>v0.7.8 — 돌파속성 single value reflection setter.</summary>
    public static bool TrySetSingleValue(object player, int skillID, string fieldName, float value)
    {
        var wrapper = FindSkillWrapper(player, skillID);
        if (wrapper == null) return false;
        return TryReflectionSetter(wrapper, fieldName, value);
    }

    // ───── helpers ─────

    private static MethodInfo? FindMethod(object obj, string name, Type[] paramTypes)
    {
        foreach (var m in obj.GetType().GetMethods(F))
        {
            if (m.Name != name) continue;
            var ps = m.GetParameters();
            if (ps.Length != paramTypes.Length) continue;
            bool match = true;
            for (int i = 0; i < ps.Length; i++)
            {
                if (ps[i].ParameterType != paramTypes[i]
                    && !ps[i].ParameterType.IsAssignableFrom(paramTypes[i])
                    && !paramTypes[i].IsAssignableFrom(ps[i].ParameterType))
                { match = false; break; }
            }
            if (match) return m;
        }
        return null;
    }

    private static bool TryReflectionSetter(object obj, string name, object value)
    {
        try
        {
            var t = obj.GetType();
            var p = t.GetProperty(name, F);
            if (p != null && p.CanWrite)
            {
                p.SetValue(obj, Convert.ChangeType(value, p.PropertyType));
                return true;
            }
            var f = t.GetField(name, F);
            if (f != null)
            {
                f.SetValue(obj, Convert.ChangeType(value, f.FieldType));
                return true;
            }
        }
        catch (Exception ex)
        {
            Logger.WarnOnce("KungfuSkillEditor", $"KungfuSkillEditor.TryReflectionSetter({name}, {value}): {ex.GetType().Name}: {ex.Message}");
        }
        return false;
    }

    private static object? ReadFieldOrProperty(object obj, string name)
    {
        var t = obj.GetType();
        var f = t.GetField(name, F);
        if (f != null) { try { return f.GetValue(obj); } catch { return null; } }
        var p = t.GetProperty(name, F);
        if (p != null) { try { return p.GetValue(obj); } catch { return null; } }
        return null;
    }

    private static int ReadInt(object obj, string name)
    {
        try { var v = ReadFieldOrProperty(obj, name); return v == null ? 0 : Convert.ToInt32(v); }
        catch { return 0; }
    }
    private static float ReadFloat(object obj, string name)
    {
        try { var v = ReadFieldOrProperty(obj, name); return v == null ? 0f : Convert.ToSingle(v); }
        catch { return 0f; }
    }
    private static bool ReadBool(object obj, string name)
    {
        try { var v = ReadFieldOrProperty(obj, name); return v is bool b && b; }
        catch { return false; }
    }
}
