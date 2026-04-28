using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using LongYinRoster.Util;

namespace LongYinRoster.Slots;

public sealed record HeroHeader(
    string HeroName,
    string HeroNickName,
    float  FightScore);

public sealed record SaveSlotInfo(
    int      SlotIndex,
    bool     Exists,
    bool     IsCurrentlyLoaded,
    string   SaveDetail,
    DateTime SaveTime,
    string   HeroName,
    string   HeroNickName,
    float    FightScore);

/// <summary>
/// 게임 자체 SaveSlot N (0~10) 디렉토리에서 Hero/Info 파일을 읽어 영웅 메타와 raw JSON 을
/// 추출한다. IL2CPP-bound Newtonsoft 의존을 피하려고 모든 파싱은 System.Text.Json + 수동
/// brace-counter 로 처리.
/// </summary>
public static class SaveFileScanner
{
    /// <summary>
    /// Hero 파일의 처음 N 바이트만 읽어 첫 영웅(heroID=0)의 핵심 메타만 추출.
    /// 잘린 JSON 에서도 graceful 하게 반환. brace-counting 으로 hero[0] JSON substring 을
    /// 잘라낸 뒤 JsonDocument.Parse 로 필드 추출.
    /// 기본값 524288 (512KB) 은 hero[0] 최대 관측 크기(~237KB) 의 ~2배 안전마진.
    /// 35MB 전체 파일 대비 1.5% 만 읽으므로 ListAvailable 응답성 유지.
    /// </summary>
    public static HeroHeader ParseHeader(string heroFilePath, int headerByteLimit = 524288)
    {
        try
        {
            byte[] buf;
            using (var fs = new FileStream(heroFilePath, FileMode.Open, FileAccess.Read))
            {
                var len = (int)Math.Min(headerByteLimit, fs.Length);
                buf = new byte[len];
                fs.Read(buf, 0, len);
            }
            var slice = Encoding.UTF8.GetString(buf);
            var heroJson = ExtractFirstObject(slice);
            if (heroJson == null) return new HeroHeader("", "", 0f);

            using var doc = JsonDocument.Parse(heroJson);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return new HeroHeader("", "", 0f);

            string heroName     = TryGetString(root, "heroName");
            string heroNickName = TryGetString(root, "heroNickName");
            float  fightScore   = TryGetFloat(root, "fightScore");
            return new HeroHeader(heroName, heroNickName, fightScore);
        }
        catch (Exception ex)
        {
            Logger.Warn($"ParseHeader({heroFilePath}): {ex.Message}");
            return new HeroHeader("", "", 0f);
        }
    }

    /// <summary>Save/SaveSlot0~10 폴더를 스캔해 11줄 정보 반환.</summary>
    public static List<SaveSlotInfo> ListAvailable(int? currentlyLoadedSlot = null)
    {
        var saveRoot = PathProvider.GameSaveDir;
        var result   = new List<SaveSlotInfo>(11);

        for (int i = 0; i <= 10; i++)
        {
            var dir   = Path.Combine(saveRoot, $"SaveSlot{i}");
            var info  = Path.Combine(dir, "Info");
            var hero  = Path.Combine(dir, "Hero");
            var exists = File.Exists(info) && File.Exists(hero);

            string saveDetail = "";
            DateTime saveTime = default;
            HeroHeader hdr = new("", "", 0f);

            if (exists)
            {
                try
                {
                    using var infoDoc = JsonDocument.Parse(File.ReadAllText(info));
                    var infoRoot = infoDoc.RootElement;
                    saveDetail = TryGetString(infoRoot, "SaveDetail");
                    var saveTimeStr = TryGetString(infoRoot, "SaveTime");
                    if (!string.IsNullOrEmpty(saveTimeStr))
                        DateTime.TryParse(saveTimeStr, out saveTime);
                }
                catch (Exception ex) { Logger.Warn($"Info parse failed for slot {i}: {ex.Message}"); }

                hdr = ParseHeader(hero);
            }

            result.Add(new SaveSlotInfo(
                SlotIndex: i,
                Exists: exists,
                IsCurrentlyLoaded: currentlyLoadedSlot == i,
                SaveDetail: saveDetail,
                SaveTime: saveTime,
                HeroName: hdr.HeroName,
                HeroNickName: hdr.HeroNickName,
                FightScore: hdr.FightScore));
        }
        return result;
    }

    /// <summary>
    /// 주어진 SaveSlot 의 Hero 파일에서 heroID=0 영웅의 raw JSON string 을 반환한다.
    /// SlotPayload.Player 와 같은 형식 (raw HeroData JSON) 이라 import 흐름에서 그대로 사용 가능.
    /// </summary>
    public static string LoadHero0(int saveSlotIndex)
    {
        var path = Path.Combine(PathProvider.GameSaveDir, $"SaveSlot{saveSlotIndex}", "Hero");
        var text = File.ReadAllText(path);

        using var doc = JsonDocument.Parse(text);
        var root = doc.RootElement;
        if (root.ValueKind != JsonValueKind.Array)
            throw new InvalidDataException($"SaveSlot{saveSlotIndex}/Hero is not a JSON array");

        foreach (var el in root.EnumerateArray())
        {
            if (el.ValueKind != JsonValueKind.Object) continue;
            if (el.TryGetProperty("heroID", out var idEl)
                && idEl.ValueKind == JsonValueKind.Number
                && idEl.TryGetInt32(out var id)
                && id == 0)
            {
                return el.GetRawText();
            }
        }
        throw new InvalidOperationException($"heroID=0 not found in SaveSlot{saveSlotIndex}");
    }

    // -------------------------------------------------------------- helpers

    /// <summary>
    /// 잘린 또는 큰 JSON 슬라이스에서 첫 '{' 부터 매칭되는 '}' 까지 substring 을 추출.
    /// 매칭 못 찾으면 null. JsonDocument 가 잘린 JSON 에 대해 throw 하기 전에 단축회로.
    /// </summary>
    private static string? ExtractFirstObject(string slice)
    {
        int start = slice.IndexOf('{');
        if (start < 0) return null;

        int depth = 0;
        bool inString = false;
        bool escape   = false;
        for (int i = start; i < slice.Length; i++)
        {
            char c = slice[i];
            if (escape)    { escape = false; continue; }
            if (c == '\\') { escape = true;  continue; }
            if (c == '"')  { inString = !inString; continue; }
            if (inString)  continue;
            if (c == '{')  depth++;
            else if (c == '}')
            {
                depth--;
                if (depth == 0) return slice.Substring(start, i - start + 1);
            }
        }
        return null;  // 매칭되는 '}' 못 찾음 → 잘린 JSON
    }

    private static string TryGetString(JsonElement el, string key) =>
        el.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.String
            ? v.GetString() ?? "" : "";

    private static float TryGetFloat(JsonElement el, string key)
    {
        if (!el.TryGetProperty(key, out var v)) return 0f;
        if (v.ValueKind == JsonValueKind.Number && v.TryGetSingle(out var f)) return f;
        return 0f;
    }
}
