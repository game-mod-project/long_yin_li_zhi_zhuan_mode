using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.Json;
using LongYinRoster.Core;
using Logger = LongYinRoster.Util.Logger;

namespace LongYinRoster.Containers;

/// <summary>
/// 컨테이너 operations.
///   JSON-only:    AppendItemsJson / RemoveItemsByIndex / ExtractItemsByIndex
///   Game 통합:    ExtractGameItemsToJson / AddItemsJsonToGame / RemoveGameItems
///
/// Game 통합 부분은 ItemListApplier.ApplyJsonToObject 패턴 mirror — IL2CPP wrapper
/// reflection + ItemType ctor + GetItem game-self method 호출.
/// </summary>
public static class ContainerOps
{
    private const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

    // -------------------------------------------- JSON-only operations

    public static string AppendItemsJson(string existingArrayJson, string toAppendArrayJson)
    {
        using var ex = JsonDocument.Parse(existingArrayJson);
        using var ad = JsonDocument.Parse(toAppendArrayJson);
        using var ms = new MemoryStream();
        using (var w = new Utf8JsonWriter(ms))
        {
            w.WriteStartArray();
            foreach (var e in ex.RootElement.EnumerateArray()) e.WriteTo(w);
            foreach (var e in ad.RootElement.EnumerateArray()) e.WriteTo(w);
            w.WriteEndArray();
        }
        return Encoding.UTF8.GetString(ms.ToArray());
    }

    public static string RemoveItemsByIndex(string arrayJson, HashSet<int> removeIndices)
    {
        using var doc = JsonDocument.Parse(arrayJson);
        using var ms = new MemoryStream();
        using (var w = new Utf8JsonWriter(ms))
        {
            w.WriteStartArray();
            int i = 0;
            foreach (var e in doc.RootElement.EnumerateArray())
            {
                if (!removeIndices.Contains(i)) e.WriteTo(w);
                i++;
            }
            w.WriteEndArray();
        }
        return Encoding.UTF8.GetString(ms.ToArray());
    }

    public static string ExtractItemsByIndex(string arrayJson, HashSet<int> indices)
    {
        using var doc = JsonDocument.Parse(arrayJson);
        using var ms = new MemoryStream();
        using (var w = new Utf8JsonWriter(ms))
        {
            w.WriteStartArray();
            int i = 0;
            foreach (var e in doc.RootElement.EnumerateArray())
            {
                if (indices.Contains(i)) e.WriteTo(w);
                i++;
            }
            w.WriteEndArray();
        }
        return Encoding.UTF8.GetString(ms.ToArray());
    }

    // -------------------------------------------- Game 통합

    public sealed class GameMoveResult
    {
        public int    Succeeded     { get; set; }
        public int    Failed        { get; set; }
        public float  OverCapWeight { get; set; }   // v0.7.1 — 인벤 over-cap 발생 무게 (kg, allowOvercap=true 분기)
        public string? Reason       { get; set; }
    }

    /// <summary>
    /// game's IL2Cpp List 의 지정 index entries 를 JSON array string 으로 추출.
    /// </summary>
    public static string ExtractGameItemsToJson(object il2List, HashSet<int> indices)
    {
        using var ms = new MemoryStream();
        using (var w = new Utf8JsonWriter(ms))
        {
            w.WriteStartArray();
            int n = IL2CppListOps.Count(il2List);
            for (int i = 0; i < n; i++)
            {
                if (!indices.Contains(i)) continue;
                var item = IL2CppListOps.Get(il2List, i);
                if (item == null) continue;
                WriteItemAsJson(w, item);
            }
            w.WriteEndArray();
        }
        return Encoding.UTF8.GetString(ms.ToArray());
    }

    /// <summary>
    /// JSON array 의 각 entry 를 ItemData wrapper 로 deep-copy 후 player 의 GetItem 호출.
    /// 가득 참 시 partial — Succeeded/Failed 반환.
    /// </summary>
    public static GameMoveResult AddItemsJsonToGame(object player, string itemsJson, int maxCapacity)
    {
        var res = new GameMoveResult();
        try
        {
            using var doc = JsonDocument.Parse(itemsJson);
            var arr = doc.RootElement;
            if (arr.ValueKind != JsonValueKind.Array) { res.Reason = "itemsJson 이 array 아님"; return res; }

            var ild = ReadFieldOrProperty(player, "itemListData");
            var allItem = ild != null ? ReadFieldOrProperty(ild, "allItem") : null;
            if (allItem == null) { res.Reason = "player.itemListData.allItem null"; return res; }
            int curN = IL2CppListOps.Count(allItem);

            Type? wrapperType = null;
            for (int k = 0; k < curN && wrapperType == null; k++)
            {
                var s = IL2CppListOps.Get(allItem, k);
                if (s != null) wrapperType = s.GetType();
            }
            if (wrapperType == null) { res.Reason = "wrapperType 미발견 (인벤토리 비어있음)"; return res; }

            ConstructorInfo? ctor = null;
            Type? itemTypeEnum = null;
            foreach (var c in wrapperType.GetConstructors(F))
            {
                var ps = c.GetParameters();
                if (ps.Length == 1 && ps[0].ParameterType.IsEnum && ps[0].ParameterType.Name == "ItemType")
                {
                    ctor = c; itemTypeEnum = ps[0].ParameterType; break;
                }
            }
            if (ctor == null) { res.Reason = "ItemType ctor 미발견"; return res; }

            int available = Math.Max(0, maxCapacity - curN);

            for (int i = 0; i < arr.GetArrayLength(); i++)
            {
                if (res.Succeeded >= available) { res.Failed++; continue; }
                var entry = arr[i];
                try
                {
                    int type = entry.TryGetProperty("type", out var tEl) && tEl.ValueKind == JsonValueKind.Number ? tEl.GetInt32() : 0;
                    var wrapper = ctor.Invoke(new object[] { Enum.ToObject(itemTypeEnum!, type) });
                    ItemListApplier.ApplyJsonToObject(entry, wrapper, depth: 0);
                    InvokeMethod(player, "GetItem", new object[] { wrapper, false });
                    res.Succeeded++;
                }
                catch (Exception ex)
                {
                    res.Failed++;
                    Logger.Warn($"ContainerOps.AddItemsJsonToGame entry[{i}]: {ex.GetType().Name}: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            res.Reason = $"AddItemsJsonToGame threw: {ex.Message}";
        }
        return res;
    }

    /// <summary>
    /// game's IL2Cpp List 의 지정 index entries 직접 RemoveAt. 큰 인덱스부터 제거 (앞 보존).
    /// </summary>
    public static int RemoveGameItems(object il2List, HashSet<int> indices)
    {
        var listType = il2List.GetType();
        var removeAtM = listType.GetMethod("RemoveAt", F, null, new[] { typeof(int) }, null);
        if (removeAtM == null) return 0;
        var sorted = new List<int>(indices);
        sorted.Sort();
        sorted.Reverse();
        int removed = 0;
        foreach (var idx in sorted)
        {
            try { removeAtM.Invoke(il2List, new object[] { idx }); removed++; }
            catch { }
        }
        return removed;
    }

    // -------------------------------------------- helpers

    private static object? ReadFieldOrProperty(object obj, string name)
    {
        var t = obj.GetType();
        var p = t.GetProperty(name, F);
        if (p != null) return p.GetValue(obj);
        var f = t.GetField(name, F);
        if (f != null) return f.GetValue(obj);
        return null;
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
            bool compat = true;
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == null) continue;
                if (!ps[i].ParameterType.IsAssignableFrom(args[i].GetType())) { compat = false; break; }
            }
            if (!compat) continue;
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

    private static void WriteItemAsJson(Utf8JsonWriter w, object item)
    {
        w.WriteStartObject();
        var t = item.GetType();
        WriteProp(w, item, t, "itemID");
        WriteProp(w, item, t, "type");
        WriteProp(w, item, t, "subType");
        WriteProp(w, item, t, "name");
        WriteProp(w, item, t, "value");
        WriteProp(w, item, t, "itemLv");
        WriteProp(w, item, t, "rareLv");
        WriteProp(w, item, t, "weight");
        WriteProp(w, item, t, "isNew");
        WriteProp(w, item, t, "poisonNum");
        WriteProp(w, item, t, "poisonNumDetected");
        WriteSubData(w, item, "equipmentData");
        WriteSubData(w, item, "medFoodData");
        WriteSubData(w, item, "bookData");
        WriteSubData(w, item, "treasureData");
        WriteSubData(w, item, "materialData");
        WriteSubData(w, item, "horseData");
        w.WriteEndObject();
    }

    private static void WriteProp(Utf8JsonWriter w, object obj, Type t, string name)
    {
        var p = t.GetProperty(name, F);
        var f = (p == null) ? t.GetField(name, F) : null;
        if (p == null && f == null) return;
        var v = p?.GetValue(obj) ?? f?.GetValue(obj);
        if (v == null) { w.WriteNull(name); return; }
        switch (v)
        {
            case int i:    w.WriteNumber(name, i); break;
            case long l:   w.WriteNumber(name, l); break;
            case float fl: w.WriteNumber(name, fl); break;
            case double d: w.WriteNumber(name, d); break;
            case bool b:   w.WriteBoolean(name, b); break;
            case string s: w.WriteString(name, s); break;
            default:
                if (v.GetType().IsEnum) w.WriteNumber(name, Convert.ToInt32(v));
                else w.WriteString(name, v.ToString() ?? "");
                break;
        }
    }

    private static void WriteSubData(Utf8JsonWriter w, object item, string subName)
    {
        var sd = ReadFieldOrProperty(item, subName);
        if (sd == null) { w.WriteNull(subName); return; }
        w.WritePropertyName(subName);
        WriteObjectRecursive(w, sd, depth: 0);
    }

    private static void WriteObjectRecursive(Utf8JsonWriter w, object obj, int depth)
    {
        if (depth > 6) { w.WriteNullValue(); return; }
        var t = obj.GetType();
        w.WriteStartObject();
        foreach (var p in t.GetProperties(F))
        {
            var name = p.Name;
            if (name == "ObjectClass" || name == "Pointer" || name == "WasCollected") continue;
            try
            {
                var v = p.GetValue(obj);
                WriteValue(w, name, v, depth);
            }
            catch { }
        }
        w.WriteEndObject();
    }

    private static void WriteValue(Utf8JsonWriter w, string name, object? v, int depth)
    {
        if (v == null) { w.WriteNull(name); return; }
        var vt = v.GetType();
        if (vt == typeof(int))    { w.WriteNumber(name, (int)v); return; }
        if (vt == typeof(long))   { w.WriteNumber(name, (long)v); return; }
        if (vt == typeof(float))  { w.WriteNumber(name, (float)v); return; }
        if (vt == typeof(double)) { w.WriteNumber(name, (double)v); return; }
        if (vt == typeof(bool))   { w.WriteBoolean(name, (bool)v); return; }
        if (vt == typeof(string)) { w.WriteString(name, (string)v); return; }
        if (vt.IsEnum)            { w.WriteNumber(name, Convert.ToInt32(v)); return; }
        try
        {
            var listIface = vt.GetInterface("IList");
            if (listIface != null)
            {
                int n = IL2CppListOps.Count(v);
                if (n >= 0)
                {
                    w.WriteStartArray(name);
                    for (int i = 0; i < n; i++)
                    {
                        var ev = IL2CppListOps.Get(v, i);
                        if (ev == null) { w.WriteNullValue(); continue; }
                        var et = ev.GetType();
                        if (et == typeof(int))    { w.WriteNumberValue((int)ev); continue; }
                        if (et == typeof(float))  { w.WriteNumberValue((float)ev); continue; }
                        if (et == typeof(double)) { w.WriteNumberValue((double)ev); continue; }
                        if (et == typeof(bool))   { w.WriteBooleanValue((bool)ev); continue; }
                        if (et == typeof(string)) { w.WriteStringValue((string)ev); continue; }
                        w.WriteStringValue(ev.ToString() ?? "");
                    }
                    w.WriteEndArray();
                    return;
                }
            }
            w.WritePropertyName(name);
            WriteObjectRecursive(w, v, depth + 1);
        }
        catch { w.WriteNull(name); }
    }
}
