using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using LongYinRoster.Util;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

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

public static class SaveFileScanner
{
    /// <summary>
    /// Hero 파일의 처음 N 바이트만 읽어 첫 영웅(heroID=0)의 핵심 메타만 추출.
    /// 잘린 JSON에서도 graceful 하게 반환. 구현은 IL2CPP-bound Newtonsoft 호환을 위해
    /// 수동 brace-counting으로 hero[0] JSON substring을 추출한 뒤 JObject.Parse 한다.
    /// </summary>
    public static HeroHeader ParseHeader(string heroFilePath, int headerByteLimit = 4096)
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

            // hero[0] = 배열의 첫 객체. '[' 다음의 첫 '{' 부터 매칭되는 '}' 까지를 substring 으로 잘라냄.
            int start = slice.IndexOf('{');
            if (start < 0) return new HeroHeader("", "", 0f);

            int end   = -1;
            int depth = 0;
            bool inString  = false;
            bool escape    = false;
            for (int i = start; i < slice.Length; i++)
            {
                char c = slice[i];
                if (escape)         { escape = false; continue; }
                if (c == '\\')      { escape = true;  continue; }
                if (c == '"')       { inString = !inString; continue; }
                if (inString)       continue;
                if (c == '{')       depth++;
                else if (c == '}')
                {
                    depth--;
                    if (depth == 0) { end = i; break; }
                }
            }
            if (end < 0)
            {
                // hero[0] 가 슬라이스 안에서 닫히지 않음 → 잘린 JSON.
                return new HeroHeader("", "", 0f);
            }

            var heroJson = slice.Substring(start, end - start + 1);
            var obj      = JObject.Parse(heroJson);

            string heroName     = obj["heroName"]?.ToString()     ?? "";
            string heroNickName = obj["heroNickName"]?.ToString() ?? "";
            float  fightScore   = 0f;
            var    fsTok        = obj["fightScore"];
            if (fsTok != null)
            {
                var s = fsTok.ToString();
                if (!string.IsNullOrEmpty(s) && float.TryParse(s,
                        System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture,
                        out var f))
                    fightScore = f;
            }
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
                    var infoJson = JObject.Parse(File.ReadAllText(info));
                    saveDetail = (string?)infoJson["SaveDetail"] ?? "";
                    DateTime.TryParse((string?)infoJson["SaveTime"], out saveTime);
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

    /// <summary>주어진 SaveSlot 의 Hero 파일 전체를 읽어 heroID=0 인 영웅 JSON 반환.</summary>
    public static JObject LoadHero0(int saveSlotIndex)
    {
        var path = Path.Combine(PathProvider.GameSaveDir, $"SaveSlot{saveSlotIndex}", "Hero");
        var arr  = JArray.Parse(File.ReadAllText(path));
        for (int i = 0; i < arr.Count; i++)
        {
            var item = arr[i];
            if (item is JObject obj && (int?)obj["heroID"] == 0)
                return obj;
        }
        throw new InvalidOperationException($"heroID=0 not found in SaveSlot{saveSlotIndex}");
    }
}
