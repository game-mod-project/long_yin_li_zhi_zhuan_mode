using System;
using System.Collections.Generic;
using System.Reflection;
using LongYinRoster.Util;
using Logger = LongYinRoster.Util.Logger;

namespace LongYinRoster.Core;

/// <summary>
/// v0.7.7 — HeroSpeAddData wrapper read/write 헬퍼.
/// Spike 결과 (2026-05-08): 내부 = `Dictionary&lt;int, float&gt; heroSpeAddData` (Property).
/// API: `Get(int) → float`, `Set(int, float) → HeroSpeAddData (chainable)`, `GetKeys() → List&lt;int&gt;`.
/// 본 모듈은 IL2CPP wrapper 도 POCO mock 도 reflection 으로 동일 처리.
/// </summary>
public static class HeroSpeAddDataReflector
{
    private const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

    /// <summary>현재 등록된 entry list (key=type idx, value=add 값) — Dictionary property iteration.</summary>
    public static List<(int Type, float Value)> GetEntries(object speAddData)
    {
        var result = new List<(int, float)>();
        if (speAddData == null) return result;
        try
        {
            // GetKeys() → List<int> 우선 시도
            var getKeysM = speAddData.GetType().GetMethod("GetKeys", F, null, Type.EmptyTypes, null);
            var getM     = FindGetMethod(speAddData);
            if (getKeysM != null && getM != null)
            {
                var keys = getKeysM.Invoke(speAddData, null);
                if (keys != null)
                {
                    var countProp = keys.GetType().GetProperty("Count", F);
                    var indexer   = keys.GetType().GetMethod("get_Item", F);
                    if (countProp != null && indexer != null)
                    {
                        int n = Convert.ToInt32(countProp.GetValue(keys));
                        for (int i = 0; i < n; i++)
                        {
                            int key = Convert.ToInt32(indexer.Invoke(keys, new object[] { i }));
                            float val = Convert.ToSingle(getM.Invoke(speAddData, new object[] { key }));
                            result.Add((key, val));
                        }
                        return result;
                    }
                }
            }

            // Dictionary property fallback (IL2CPP)
            var dictProp = speAddData.GetType().GetProperty("heroSpeAddData", F);
            if (dictProp != null)
            {
                var dict = dictProp.GetValue(speAddData);
                if (dict != null) ExtractDictEntries(dict, result);
            }
        }
        catch (Exception ex)
        {
            Logger.WarnOnce("HeroSpeAddDataReflector", $"HeroSpeAddDataReflector.GetEntries: {ex.GetType().Name}: {ex.Message}");
        }
        return result;
    }

    /// <summary>type idx 의 현재 값 read. 미발견 또는 실패 시 0.</summary>
    public static float GetValue(object speAddData, int type)
    {
        if (speAddData == null) return 0f;
        try
        {
            var getM = FindGetMethod(speAddData);
            if (getM != null)
            {
                var v = getM.Invoke(speAddData, new object[] { type });
                return Convert.ToSingle(v);
            }
        }
        catch (Exception ex)
        {
            Logger.WarnOnce("HeroSpeAddDataReflector", $"HeroSpeAddDataReflector.GetValue({type}): {ex.GetType().Name}: {ex.Message}");
        }
        return 0f;
    }

    /// <summary>Set(int, float) 호출. value=0 은 entry 추가 (Set 결과 chainable, 무시).</summary>
    public static bool TrySet(object speAddData, int type, float value)
    {
        if (speAddData == null) return false;
        try
        {
            // Set(int, float) overload 우선 (HeroSpeAddDataType 보다 int 가 IL2CPP enum 변환 회피)
            var setM = FindSetMethod(speAddData);
            if (setM != null)
            {
                setM.Invoke(speAddData, new object[] { type, value });
                return true;
            }
        }
        catch (Exception ex)
        {
            Logger.WarnOnce("HeroSpeAddDataReflector", $"HeroSpeAddDataReflector.TrySet({type}, {value}): {ex.GetType().Name}: {ex.Message}");
        }
        return false;
    }

    /// <summary>
    /// entry 제거. Remove method 부재 (spike 결과) → Dictionary 직접 access 시도.
    /// 실패 시 Set(type, 0) fallback (entry 는 dict 에 남지만 효과 0).
    /// </summary>
    public static bool TryRemove(object speAddData, int type)
    {
        if (speAddData == null) return false;
        try
        {
            // 1. Dictionary property direct access — heroSpeAddData.Remove(int)
            var dictProp = speAddData.GetType().GetProperty("heroSpeAddData", F);
            if (dictProp != null)
            {
                var dict = dictProp.GetValue(speAddData);
                if (dict != null)
                {
                    var removeM = FindDictRemoveMethod(dict);
                    if (removeM != null)
                    {
                        removeM.Invoke(dict, new object[] { type });
                        return true;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Logger.WarnOnce("HeroSpeAddDataReflector", $"HeroSpeAddDataReflector.TryRemove({type}) Dict.Remove: {ex.GetType().Name}: {ex.Message}");
        }

        // 2. Fallback — Set(type, 0)
        return TrySet(speAddData, type, 0f);
    }

    private static MethodInfo? FindGetMethod(object speAddData)
    {
        var t = speAddData.GetType();
        // Get(int) 우선 — IL2CPP enum 변환 회피
        foreach (var m in t.GetMethods(F))
        {
            if (m.Name != "Get") continue;
            var ps = m.GetParameters();
            if (ps.Length == 1 && ps[0].ParameterType == typeof(int)) return m;
        }
        // fallback: Get(HeroSpeAddDataType)
        foreach (var m in t.GetMethods(F))
        {
            if (m.Name != "Get") continue;
            var ps = m.GetParameters();
            if (ps.Length == 1) return m;
        }
        return null;
    }

    private static MethodInfo? FindSetMethod(object speAddData)
    {
        var t = speAddData.GetType();
        // Set(int, float) 우선
        foreach (var m in t.GetMethods(F))
        {
            if (m.Name != "Set") continue;
            var ps = m.GetParameters();
            if (ps.Length == 2 && ps[0].ParameterType == typeof(int)) return m;
        }
        // fallback: 첫 Set(_, _) 매치
        foreach (var m in t.GetMethods(F))
        {
            if (m.Name != "Set") continue;
            var ps = m.GetParameters();
            if (ps.Length == 2) return m;
        }
        return null;
    }

    private static MethodInfo? FindDictRemoveMethod(object dict)
    {
        var t = dict.GetType();
        foreach (var m in t.GetMethods(F))
        {
            if (m.Name != "Remove") continue;
            var ps = m.GetParameters();
            if (ps.Length == 1) return m;
        }
        return null;
    }

    private static void ExtractDictEntries(object dict, List<(int, float)> result)
    {
        // POCO Dictionary<int, float> 또는 IL2CPP 동등 — IDictionaryEnumerator 우선
        try
        {
            // foreach via GetEnumerator — IL2CPP 와 POCO 둘 다 IEnumerable 구현 가능
            var keysProp = dict.GetType().GetProperty("Keys", F);
            if (keysProp != null)
            {
                var keys = keysProp.GetValue(dict);
                if (keys is System.Collections.IEnumerable en)
                {
                    var indexer = dict.GetType().GetMethod("get_Item", F);
                    if (indexer != null)
                    {
                        foreach (var k in en)
                        {
                            int key = Convert.ToInt32(k);
                            var v = indexer.Invoke(dict, new object[] { key });
                            float val = Convert.ToSingle(v);
                            result.Add((key, val));
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Warn($"ExtractDictEntries: {ex.GetType().Name}: {ex.Message}");
        }
    }
}
