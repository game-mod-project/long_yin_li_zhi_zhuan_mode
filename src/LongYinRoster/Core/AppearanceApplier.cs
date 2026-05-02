using System;
using System.Reflection;
using System.Text.Json;
using Logger = LongYinRoster.Util.Logger;

namespace LongYinRoster.Core;

/// <summary>
/// v0.6.1 — 외형 (faceData / skinColorDark / voicePitch) Replace.
///
/// 슬롯 JSON 구조:
///   - faceData: { faceID: [9 int array] }   — 얼굴형/눈/눈썹/머리카락/입/코/기타/.../...
///   - skinColorDark: float (0.0 ~ 1.0)
///   - voicePitch: float
///
/// 외형 panel 의 "1/3/10/3/7/5/3/0/-1/-1/0" 형식 코드 = isFemale + faceID + skinColorDark.
/// 본 Applier 는 faceID + skinColorDark + voicePitch 만 다룸 (gender 는 영구 — capture 시점에 fixed).
///
/// partPosture sub-data 는 slot JSON 에 캡처 안 됨 → out-of-scope (v0.6.x 후속).
/// 의상 (skinID/skinLv) 는 v0.4 Skin 카테고리에서 별도 처리.
///
/// UI cache invalidate — RefreshSelfState (Step 8) 가 처리. 추가 trigger 불필요 가설.
/// </summary>
public static class AppearanceApplier
{
    public sealed class Result
    {
        public bool Skipped { get; set; }
        public string? Reason { get; set; }
        public bool FaceIDApplied { get; set; }
        public bool SkinColorApplied { get; set; }
        public bool VoicePitchApplied { get; set; }
    }

    private const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

    public static Result Apply(object? player, JsonElement slot, ApplySelection sel)
    {
        var res = new Result();

        if (!sel.Appearance) { res.Skipped = true; res.Reason = "appearance (selection off)"; return res; }
        if (player == null)  { res.Skipped = true; res.Reason = "player null (test mode)"; return res; }

        // faceData.faceID 적용
        if (slot.TryGetProperty("faceData", out var fdJson) && fdJson.ValueKind == JsonValueKind.Object)
        {
            var fd = ReadFieldOrProperty(player, "faceData");
            if (fd != null)
            {
                Logger.Info($"Appearance Apply: faceData runtime type = {fd.GetType().FullName}");
                try
                {
                    // ItemListApplier.ApplyJsonToObject 재사용 — faceID array 포함 모든 fields deep-copy
                    ItemListApplier.ApplyJsonToObject(fdJson, fd, depth: 0);
                    res.FaceIDApplied = true;

                    // 진단 — 적용된 faceID dump
                    var fid = ReadFieldOrProperty(fd, "faceID");
                    if (fid != null)
                    {
                        int n = IL2CppListOps.Count(fid);
                        var vals = new System.Collections.Generic.List<string>();
                        for (int i = 0; i < n; i++)
                        {
                            var v = IL2CppListOps.Get(fid, i);
                            vals.Add(v?.ToString() ?? "?");
                        }
                        Logger.Info($"Appearance Apply: faceID = [{string.Join(",", vals)}]");
                    }
                }
                catch (Exception ex)
                {
                    Logger.Warn($"Appearance Apply faceData: {ex.GetType().Name}: {ex.Message}");
                }
            }
            else
                Logger.Info("Appearance Apply: player.faceData null");
        }
        else
            Logger.Info("Appearance Apply: slot.faceData 미존재");

        // skinColorDark 적용 (top-level scalar)
        if (slot.TryGetProperty("skinColorDark", out var scd) && scd.ValueKind == JsonValueKind.Number)
        {
            try
            {
                SetScalar(player, "skinColorDark", scd.GetSingle());
                res.SkinColorApplied = true;
                Logger.Info($"Appearance Apply: skinColorDark = {scd.GetSingle()}");
            }
            catch (Exception ex)
            {
                Logger.Warn($"Appearance Apply skinColorDark: {ex.GetType().Name}: {ex.Message}");
            }
        }

        // voicePitch 적용 (top-level scalar)
        if (slot.TryGetProperty("voicePitch", out var vp) && vp.ValueKind == JsonValueKind.Number)
        {
            try
            {
                SetScalar(player, "voicePitch", vp.GetSingle());
                res.VoicePitchApplied = true;
                Logger.Info($"Appearance Apply: voicePitch = {vp.GetSingle()}");
            }
            catch (Exception ex)
            {
                Logger.Warn($"Appearance Apply voicePitch: {ex.GetType().Name}: {ex.Message}");
            }
        }

        // v0.6.4 — partPosture 적용. SerializerService 가 _partPostureFloats array 로 inject.
        // PartPostureData.partPosture (List<float>) 에 clear + add via reflection.
        if (slot.TryGetProperty("_partPostureFloats", out var pp) && pp.ValueKind == JsonValueKind.Array)
        {
            try
            {
                var pData = ReadFieldOrProperty(player, "partPosture");
                if (pData != null)
                {
                    var innerList = ReadFieldOrProperty(pData, "partPosture");
                    if (innerList != null)
                    {
                        IL2CppListOps.Clear(innerList);
                        var listType = innerList.GetType();
                        var addM = listType.GetMethod("Add", F);
                        if (addM != null)
                        {
                            int added = 0;
                            for (int i = 0; i < pp.GetArrayLength(); i++)
                            {
                                if (pp[i].ValueKind != JsonValueKind.Number) continue;
                                addM.Invoke(innerList, new object[] { pp[i].GetSingle() });
                                added++;
                            }
                            Logger.Info($"Appearance Apply: partPosture restored {added} float values");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Warn($"Appearance Apply partPosture: {ex.GetType().Name}: {ex.Message}");
            }
        }
        else
            Logger.Info("Appearance Apply: slot._partPostureFloats 미존재 (slot 구버전 — re-capture 필요)");

        // UI cache invalidate — 메인 화면 캐릭터 icon 갱신 위해 dirty flags + counter increment
        TrySetBool(player, "HeroIconDirty", true);
        TrySetBool(player, "heroDetailDirty", true);
        TrySetBool(player, "heroBuffDirty", true);
        IncrementInt(player, "heroIconDirtyCount");

        // 진단 — HeroData 의 Icon / Refresh 관련 method 후보 dump (1회)
        DiscoverIconRefreshMethods(player.GetType());

        // v0.6.4 진단 — partPosture / posture / part / hair / body 관련 field 후보 dump (1회)
        DiscoverPostureFields(player.GetType());

        // game-self refresh method 시도 (no-arg)
        TryInvokeNoArg(player, "RefreshHeroIcon");
        TryInvokeNoArg(player, "RefreshIcon");
        TryInvokeNoArg(player, "RefreshPortrait");
        TryInvokeNoArg(player, "RefreshFaceData");
        TryInvokeNoArg(player, "RefreshAppearance");
        TryInvokeNoArg(player, "RebuildHeroIcon");
        TryInvokeNoArg(player, "ResetFaceImage");
        TryInvokeNoArg(player, "RefreshFace");

        Logger.Info($"Appearance Apply done — face={res.FaceIDApplied} skin={res.SkinColorApplied} voice={res.VoicePitchApplied}");
        return res;
    }

    public static Result Restore(object? player, JsonElement backup)
        => Apply(player, backup, new ApplySelection { Appearance = true });

    private static void SetScalar(object obj, string name, float value)
    {
        var t = obj.GetType();
        var p = t.GetProperty(name, F);
        if (p != null && p.CanWrite)
        {
            if (p.PropertyType == typeof(float))  p.SetValue(obj, value);
            else if (p.PropertyType == typeof(double)) p.SetValue(obj, (double)value);
            else p.SetValue(obj, Convert.ChangeType(value, p.PropertyType));
            return;
        }
        var f = t.GetField(name, F);
        if (f != null)
        {
            if (f.FieldType == typeof(float))  f.SetValue(obj, value);
            else if (f.FieldType == typeof(double)) f.SetValue(obj, (double)value);
            else f.SetValue(obj, Convert.ChangeType(value, f.FieldType));
        }
    }

    private static bool _postureFieldsDumped;
    private static void DiscoverPostureFields(Type heroDataType)
    {
        if (_postureFieldsDumped) return;
        _postureFieldsDumped = true;
        try
        {
            var related = new System.Collections.Generic.List<string>();
            string[] keywords = { "Posture", "Part", "Hair", "Body", "Skel" };
            foreach (var p in heroDataType.GetProperties(F))
            {
                var n = p.Name;
                foreach (var k in keywords)
                {
                    if (n.IndexOf(k, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        related.Add($"prop {p.PropertyType.Name} {n}");
                        break;
                    }
                }
            }
            foreach (var f in heroDataType.GetFields(F))
            {
                var n = f.Name;
                foreach (var k in keywords)
                {
                    if (n.IndexOf(k, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        related.Add($"field {f.FieldType.Name} {n}");
                        break;
                    }
                }
            }
            Logger.Info("Appearance: HeroData posture-related fields = " + string.Join("; ", related));

            // PartPostureData 자체의 fields/properties dump (Capture 확장 위해)
            var ppProp = heroDataType.GetProperty("partPosture", F);
            if (ppProp != null)
            {
                var ppType = ppProp.PropertyType;
                Logger.Info($"PartPostureData type fullname = {ppType.FullName}");

                // PartPostureData.partPosture 내부 List<T> 의 element type T 식별
                var innerListProp = ppType.GetProperty("partPosture", F);
                if (innerListProp != null && innerListProp.PropertyType.IsGenericType)
                {
                    var innerListType = innerListProp.PropertyType;
                    var elemType = innerListType.GetGenericArguments()[0];
                    Logger.Info($"PartPostureData.partPosture[i] element type = {elemType.FullName}");
                    var elemFields = new System.Collections.Generic.List<string>();
                    foreach (var p in elemType.GetProperties(F))
                        elemFields.Add($"prop {p.PropertyType.Name} {p.Name}");
                    foreach (var f in elemType.GetFields(F))
                        elemFields.Add($"field {f.FieldType.Name} {f.Name}");
                    Logger.Info($"  element fields ({elemFields.Count}):");
                    foreach (var s in elemFields)
                        Logger.Info($"    {s}");
                }
            }
        }
        catch { }
    }

    private static bool _refreshMethodsDumped;
    private static void DiscoverIconRefreshMethods(Type heroDataType)
    {
        if (_refreshMethodsDumped) return;
        _refreshMethodsDumped = true;
        try
        {
            var related = new System.Collections.Generic.List<string>();
            foreach (var m in heroDataType.GetMethods(F))
            {
                var n = m.Name;
                if (n.IndexOf("Icon",     StringComparison.OrdinalIgnoreCase) >= 0
                 || n.IndexOf("Portrait", StringComparison.OrdinalIgnoreCase) >= 0
                 || n.IndexOf("Face",     StringComparison.OrdinalIgnoreCase) >= 0
                 || n.IndexOf("Refresh",  StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    var ps = m.GetParameters();
                    var sig = n + "(" + string.Join(",", System.Linq.Enumerable.Select(ps, p => p.ParameterType.Name)) + ")";
                    if (!related.Contains(sig)) related.Add(sig);
                }
            }
            Logger.Info("Appearance: HeroData icon/face/refresh-related methods = " + string.Join(", ", related));
        }
        catch { }
    }

    private static void TryInvokeNoArg(object obj, string methodName)
    {
        try
        {
            var t = obj.GetType();
            var m = t.GetMethod(methodName, F, null, Type.EmptyTypes, null);
            if (m == null) return;
            m.Invoke(obj, null);
            Logger.Info($"Appearance: invoked {methodName}()");
        }
        catch (Exception ex)
        {
            Logger.Info($"Appearance: {methodName} threw: {ex.GetType().Name}");
        }
    }

    private static void IncrementInt(object obj, string name)
    {
        try
        {
            var t = obj.GetType();
            var p = t.GetProperty(name, F);
            if (p != null && p.CanWrite && p.PropertyType == typeof(int))
            {
                int cur = (int)(p.GetValue(obj) ?? 0);
                p.SetValue(obj, cur + 1);
                return;
            }
            var f = t.GetField(name, F);
            if (f != null && f.FieldType == typeof(int))
            {
                int cur = (int)(f.GetValue(obj) ?? 0);
                f.SetValue(obj, cur + 1);
            }
        }
        catch { }
    }

    private static void TrySetBool(object obj, string name, bool value)
    {
        try
        {
            var t = obj.GetType();
            var p = t.GetProperty(name, F);
            if (p != null && p.CanWrite && p.PropertyType == typeof(bool)) { p.SetValue(obj, value); return; }
            var f = t.GetField(name, F);
            if (f != null && f.FieldType == typeof(bool)) f.SetValue(obj, value);
        }
        catch { }
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
