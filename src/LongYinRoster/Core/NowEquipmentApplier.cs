using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using Logger = LongYinRoster.Util.Logger;

namespace LongYinRoster.Core;

/// <summary>
/// v0.6.0 — 장비 슬롯 (HeroData.nowEquipment) Replace via name-based matching.
///
/// 슬롯 JSON 의 nowEquipment.*SaveRecord = capture 시점 itemListData.allItem 의 grid index.
/// game-self GetItem 이 인벤토리 add 시 type 별 자동 정렬하므로 Apply 후 grid 순서가
/// 어긋난다 → integer index 매칭 안 됨. v0.6.0 시도 1차 (direct list.Add) 는
/// inventory cache 우회로 NullReferenceException flood.
///
/// 최종 전략: name + itemLv + rareLv + (equipmentData.enhanceLv) 로 identity key 만들어
/// 새 인벤토리에서 매칭. 첫 매치를 EquipItem.
///
/// game-self method 식별 (1차 dump 결과):
///   - EquipItem(ItemData, Boolean, Boolean)
///   - UnequipItem(ItemData, Boolean, Boolean)
/// </summary>
public static class NowEquipmentApplier
{
    public sealed class Result
    {
        public bool Skipped { get; set; }
        public string? Reason { get; set; }
        public int UnequipedCount { get; set; }
        public int EquipedCount { get; set; }
        public int FailedCount { get; set; }
    }

    private const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

    private static readonly string[] SaveRecordKeys =
        { "weaponSaveRecord", "armorSaveRecord", "helmetSaveRecord", "shoesSaveRecord", "decorationSaveRecord" };

    public static Result Apply(object? player, JsonElement slot, ApplySelection sel)
    {
        var res = new Result();

        if (!sel.NowEquipment) { res.Skipped = true; res.Reason = "nowEquipment (selection off)"; return res; }
        if (player == null)    { res.Skipped = true; res.Reason = "player null (test mode)"; return res; }

        if (!slot.TryGetProperty("nowEquipment", out var neJson) || neJson.ValueKind != JsonValueKind.Object)
        {
            res.Skipped = true; res.Reason = "slot.nowEquipment 미존재"; return res;
        }
        if (!slot.TryGetProperty("itemListData", out var ildJson) ||
            !ildJson.TryGetProperty("allItem", out var slotAllItem) ||
            slotAllItem.ValueKind != JsonValueKind.Array)
        {
            res.Skipped = true; res.Reason = "slot.itemListData.allItem 미존재"; return res;
        }

        var t = player.GetType();
        DiscoverEquipMethods(t);

        MethodInfo? equipItemM = null, unequipItemM = null;
        foreach (var m in t.GetMethods(F))
        {
            if (m.Name == "EquipItem" && m.GetParameters().Length == 3) equipItemM = m;
            else if (m.Name == "UnequipItem" && m.GetParameters().Length == 3) unequipItemM = m;
        }
        if (equipItemM == null)
        {
            res.Skipped = true; res.Reason = "EquipItem(ItemData,bool,bool) method 부재"; return res;
        }

        var ild = ReadFieldOrProperty(player, "itemListData");
        var allItem = ild != null ? ReadFieldOrProperty(ild, "allItem") : null;
        if (allItem == null)
        {
            res.Skipped = true; res.Reason = "player.itemListData.allItem null"; return res;
        }
        int allItemCount = IL2CppListOps.Count(allItem);
        Logger.Info($"NowEquipment Apply: allItem count = {allItemCount}");

        int slotW = ArrayLength(neJson, "weaponSaveRecord");
        int slotA = ArrayLength(neJson, "armorSaveRecord");
        int slotH = ArrayLength(neJson, "helmetSaveRecord");
        int slotS = ArrayLength(neJson, "shoesSaveRecord");
        int slotD = ArrayLength(neJson, "decorationSaveRecord");
        Logger.Info($"NowEquipment Apply: 슬롯 → weapon={slotW} armor={slotA} helmet={slotH} shoes={slotS} decoration={slotD}");

        // Step 1: 현재 장착된 item 모두 해제 (clean state) — 사용 중인 player 의 nowEquipment 기준
        if (unequipItemM != null)
        {
            var ne = ReadFieldOrProperty(player, "nowEquipment");
            if (ne != null)
            {
                foreach (var key in SaveRecordKeys)
                {
                    var saveList = ReadFieldOrProperty(ne, key);
                    if (saveList == null) continue;
                    int saveCount = IL2CppListOps.Count(saveList);
                    var snapshot = new List<int>();
                    for (int i = 0; i < saveCount; i++)
                    {
                        var v = IL2CppListOps.Get(saveList, i);
                        if (v != null && int.TryParse(v.ToString(), out var idx)) snapshot.Add(idx);
                    }
                    foreach (int idx in snapshot)
                    {
                        if (idx < 0 || idx >= allItemCount) continue;
                        var item = IL2CppListOps.Get(allItem, idx);
                        if (item == null) continue;
                        try
                        {
                            unequipItemM.Invoke(player, new object[] { item, false, false });
                            res.UnequipedCount++;
                        }
                        catch (Exception ex)
                        {
                            Logger.Info($"  UnequipItem idx={idx} ({key}): {ex.GetType().Name}: {ex.Message}");
                        }
                    }
                }
                Logger.Info($"NowEquipment Apply: unequipped {res.UnequipedCount} items (clean state)");
            }
        }

        // Step 2: 슬롯의 *SaveRecord 의 각 index 에서 capture 시점의 item identity 추출 후
        // 새 인벤토리에서 매칭하여 EquipItem
        var matchedIndices = new HashSet<int>();  // 이미 매칭된 index 재사용 방지 (장신구 2개 대응)
        int slotItemCount = slotAllItem.GetArrayLength();
        foreach (var key in SaveRecordKeys)
        {
            if (!neJson.TryGetProperty(key, out var arr) || arr.ValueKind != JsonValueKind.Array) continue;
            for (int i = 0; i < arr.GetArrayLength(); i++)
            {
                if (arr[i].ValueKind != JsonValueKind.Number) continue;
                int slotIdx = arr[i].GetInt32();
                if (slotIdx < 0 || slotIdx >= slotItemCount)
                {
                    Logger.Warn($"NowEquipment Apply: {key}[{i}]={slotIdx} out of slot range ({slotItemCount})");
                    res.FailedCount++;
                    continue;
                }
                var capturedEntry = slotAllItem[slotIdx];
                var identity = ItemIdentity.From(capturedEntry);
                if (identity == null)
                {
                    Logger.Warn($"NowEquipment Apply: {key}[{i}] slot allItem[{slotIdx}] identity 추출 실패");
                    res.FailedCount++;
                    continue;
                }

                int matchIdx = FindMatchingItem(allItem, allItemCount, identity, matchedIndices);
                if (matchIdx < 0)
                {
                    Logger.Warn($"NowEquipment Apply: {key}[{i}] '{identity}' — 새 인벤토리에서 매칭 안 됨");
                    res.FailedCount++;
                    continue;
                }
                matchedIndices.Add(matchIdx);
                var item = IL2CppListOps.Get(allItem, matchIdx);
                if (item == null) { res.FailedCount++; continue; }

                try
                {
                    // ItemListApplier 의 deep-copy 가 equipmentData.equiped=true 로 이미 set 했을 수
                    // 있음. EquipItem 이 already-equipped 상태로 보고 silent skip 회피 위해 강제 false
                    // reset 후 호출 (game 의 정식 equip logic 실행).
                    ResetEquipedFlag(item);
                    equipItemM.Invoke(player, new object[] { item, false, false });
                    res.EquipedCount++;
                    Logger.Info($"NowEquipment Apply: equipped {key}[{i}] '{identity}' → allItem[{matchIdx}]");
                }
                catch (Exception ex)
                {
                    Logger.Warn($"NowEquipment Apply: EquipItem '{identity}': {ex.GetType().Name}: {ex.Message}");
                    res.FailedCount++;
                }
            }
        }

        // Step 3: 말 + 마구 (horseSaveRecord / horseArmorSaveRecord — top-level scalar integer)
        // 둘 다 type=6 (horse) — subType=0 말, subType=1 마구. EquipHorse(ItemData,bool) 가
        // 라우팅. -1 이면 미장착.
        MethodInfo? equipHorseM = null, unequipHorseM = null;
        foreach (var m in t.GetMethods(F))
        {
            if (m.Name == "EquipHorse" && m.GetParameters().Length == 2) equipHorseM = m;
            else if (m.Name == "UnequipHorse" && m.GetParameters().Length == 3) unequipHorseM = m;
        }
        if (equipHorseM != null)
        {
            // 현재 장착 말/마구 해제 (clean state) — 사용 중인 player 의 indices 기준
            if (unequipHorseM != null)
            {
                foreach (var horseField in new[] { "horseSaveRecord", "horseArmorSaveRecord" })
                {
                    var curRaw = ReadFieldOrProperty(player, horseField);
                    if (curRaw == null) continue;
                    if (!int.TryParse(curRaw.ToString(), out var curIdx)) continue;
                    if (curIdx < 0 || curIdx >= allItemCount) continue;
                    var curItem = IL2CppListOps.Get(allItem, curIdx);
                    if (curItem == null) continue;
                    try
                    {
                        unequipHorseM.Invoke(player, new object[] { curItem, false, false });
                        res.UnequipedCount++;
                    }
                    catch (Exception ex)
                    {
                        Logger.Info($"  UnequipHorse {horseField}={curIdx}: {ex.GetType().Name}: {ex.Message}");
                    }
                }
            }

            foreach (var horseField in new[] { "horseSaveRecord", "horseArmorSaveRecord" })
            {
                if (!slot.TryGetProperty(horseField, out var hsr) || hsr.ValueKind != JsonValueKind.Number) continue;
                int slotIdx = hsr.GetInt32();
                if (slotIdx < 0)
                {
                    Logger.Info($"NowEquipment Apply: {horseField}={slotIdx} — 미장착");
                    continue;
                }
                if (slotIdx >= slotItemCount)
                {
                    Logger.Warn($"NowEquipment Apply: {horseField}={slotIdx} out of slot range ({slotItemCount})");
                    res.FailedCount++;
                    continue;
                }
                var capturedEntry = slotAllItem[slotIdx];
                var identity = ItemIdentity.From(capturedEntry);
                if (identity == null)
                {
                    Logger.Warn($"NowEquipment Apply: {horseField} slot allItem[{slotIdx}] identity 추출 실패");
                    res.FailedCount++;
                    continue;
                }
                int matchIdx = FindMatchingItem(allItem, allItemCount, identity, matchedIndices);
                if (matchIdx < 0)
                {
                    Logger.Warn($"NowEquipment Apply: {horseField} '{identity}' — 매칭 안 됨");
                    res.FailedCount++;
                    continue;
                }
                matchedIndices.Add(matchIdx);
                var hitem = IL2CppListOps.Get(allItem, matchIdx);
                if (hitem == null) { res.FailedCount++; continue; }

                try
                {
                    ResetHorseEquipedFlag(hitem);
                    equipHorseM.Invoke(player, new object[] { hitem, false });
                    res.EquipedCount++;
                    Logger.Info($"NowEquipment Apply: {horseField} '{identity}' → allItem[{matchIdx}] (EquipHorse)");
                }
                catch (Exception ex)
                {
                    Logger.Warn($"NowEquipment Apply: EquipHorse '{identity}': {ex.GetType().Name}: {ex.Message}");
                    res.FailedCount++;
                }
            }
        }
        else
        {
            Logger.Info("NowEquipment: EquipHorse method 부재 — 말/마구 적용 skip");
        }

        Logger.Info($"NowEquipment Apply done — unequipped={res.UnequipedCount} equipped={res.EquipedCount} failed={res.FailedCount}");
        return res;
    }

    public static Result Restore(object? player, JsonElement backup)
        => Apply(player, backup, new ApplySelection { NowEquipment = true });

    private sealed record ItemIdentity(string Name, int Type, int SubType, int ItemLv, int RareLv, int EnhanceLv)
    {
        public static ItemIdentity? From(JsonElement entry)
        {
            string name = entry.TryGetProperty("name", out var n) && n.ValueKind == JsonValueKind.String
                          ? (n.GetString() ?? "") : "";
            if (string.IsNullOrEmpty(name)) return null;
            int type    = entry.TryGetProperty("type",    out var t) && t.ValueKind == JsonValueKind.Number ? t.GetInt32() : 0;
            int subType = entry.TryGetProperty("subType", out var s) && s.ValueKind == JsonValueKind.Number ? s.GetInt32() : 0;
            int itemLv  = entry.TryGetProperty("itemLv",  out var l) && l.ValueKind == JsonValueKind.Number ? l.GetInt32() : 0;
            int rareLv  = entry.TryGetProperty("rareLv",  out var r) && r.ValueKind == JsonValueKind.Number ? r.GetInt32() : 0;
            int enh = 0;
            if (entry.TryGetProperty("equipmentData", out var ed) && ed.ValueKind == JsonValueKind.Object)
                if (ed.TryGetProperty("enhanceLv", out var eh) && eh.ValueKind == JsonValueKind.Number)
                    enh = eh.GetInt32();
            return new ItemIdentity(name, type, subType, itemLv, rareLv, enh);
        }
    }

    private static int FindMatchingItem(object allItem, int count, ItemIdentity id, HashSet<int> exclude)
    {
        for (int i = 0; i < count; i++)
        {
            if (exclude.Contains(i)) continue;
            var item = IL2CppListOps.Get(allItem, i);
            if (item == null) continue;
            try
            {
                var t = item.GetType();
                string name = ReadString(item, t, "name");
                if (name != id.Name) continue;
                int type    = ReadInt(item, t, "type");
                int subType = ReadInt(item, t, "subType");
                int itemLv  = ReadInt(item, t, "itemLv");
                int rareLv  = ReadInt(item, t, "rareLv");
                int enh = 0;
                var eq = ReadFieldOrProperty(item, "equipmentData");
                if (eq != null) enh = ReadInt(eq, eq.GetType(), "enhanceLv");

                if (type == id.Type && subType == id.SubType && itemLv == id.ItemLv
                    && rareLv == id.RareLv && enh == id.EnhanceLv)
                    return i;
            }
            catch { }
        }
        // 1차 매칭 실패 — name 만으로 fallback
        for (int i = 0; i < count; i++)
        {
            if (exclude.Contains(i)) continue;
            var item = IL2CppListOps.Get(allItem, i);
            if (item == null) continue;
            try
            {
                string name = ReadString(item, item.GetType(), "name");
                if (name == id.Name) return i;
            }
            catch { }
        }
        return -1;
    }

    private static string ReadString(object obj, Type t, string name)
    {
        var p = t.GetProperty(name, F);
        if (p != null) return p.GetValue(obj) as string ?? "";
        var f = t.GetField(name, F);
        if (f != null) return f.GetValue(obj) as string ?? "";
        return "";
    }

    private static void ResetEquipedFlag(object item)
    {
        try
        {
            var ed = ReadFieldOrProperty(item, "equipmentData");
            if (ed != null) SetEquipedFalse(ed);
        }
        catch { }
    }

    private static void ResetHorseEquipedFlag(object item)
    {
        try
        {
            var hd = ReadFieldOrProperty(item, "horseData");
            if (hd != null) SetEquipedFalse(hd);
        }
        catch { }
    }

    private static void SetEquipedFalse(object subData)
    {
        var st = subData.GetType();
        var p = st.GetProperty("equiped", F);
        if (p != null && p.CanWrite) { p.SetValue(subData, false); return; }
        var f = st.GetField("equiped", F);
        if (f != null) f.SetValue(subData, false);
    }

    private static int ReadInt(object obj, Type t, string name)
    {
        var p = t.GetProperty(name, F);
        if (p != null) { var v = p.GetValue(obj); return v != null ? Convert.ToInt32(v) : 0; }
        var f = t.GetField(name, F);
        if (f != null) { var v = f.GetValue(obj); return v != null ? Convert.ToInt32(v) : 0; }
        return 0;
    }

    private static bool _equipMethodsDumped;
    private static void DiscoverEquipMethods(Type heroDataType)
    {
        if (_equipMethodsDumped) return;
        _equipMethodsDumped = true;
        try
        {
            var related = new List<string>();
            foreach (var m in heroDataType.GetMethods(F))
            {
                var n = m.Name;
                if (n.IndexOf("Equip", StringComparison.OrdinalIgnoreCase) >= 0
                 || n.IndexOf("Wear",  StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    var ps = m.GetParameters();
                    var sig = n + "(" + string.Join(",", ps.Select(p => p.ParameterType.Name)) + ")";
                    if (!related.Contains(sig)) related.Add(sig);
                }
            }
            Logger.Info("NowEquipment: HeroData equip-related methods = " + string.Join(", ", related));
        }
        catch { }
    }

    private static int ArrayLength(JsonElement obj, string name)
    {
        if (!obj.TryGetProperty(name, out var arr)) return 0;
        if (arr.ValueKind != JsonValueKind.Array) return 0;
        return arr.GetArrayLength();
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
}
