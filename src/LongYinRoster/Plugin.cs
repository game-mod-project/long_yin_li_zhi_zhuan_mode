using System.Linq;
using BepInEx;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using LongYinRoster.UI;
using LongYinRoster.Util;
using ModCfg = LongYinRoster.Config;

namespace LongYinRoster;

[BepInPlugin(GUID, NAME, VERSION)]
[BepInProcess("LongYinLiZhiZhuan.exe")]
public sealed class Plugin : BasePlugin
{
    public const string GUID    = "com.deepe.longyinroster";
    public const string NAME    = "LongYin Roster Mod";
    public const string VERSION = "0.7.11";

    public override void Load()
    {
        Logger.Init(this.Log);
        ModCfg.Bind(this.Config);
        AddComponent<ModWindow>();

        // Harmony patches: 모드 창 영역 안 mouse input 차단.
        var harmony = new Harmony(GUID);
        harmony.PatchAll(typeof(InputBlockerPatch));
        Core.RestKeepHeroTagPatch.Register(harmony);
        Core.GetMaxTagNumPatch.Register(harmony);
        Core.HeroDataCapBypassPatch.Register(harmony);
        Logger.Info($"Harmony: {harmony.GetPatchedMethods().Count()} method(s) patched");

        Logger.Info($"Loaded {NAME} v{VERSION}");
        Logger.Info("[v0.7.5] HangulDict: lazy init on first Translate() call");
        Logger.Info("[v0.7.6] SettingsPanel ready (F11+3) — hotkey rebind / ContainerPanel rect / 영속화");
        Logger.Info("[v0.7.7] Item editor ready — ItemDetailPanel [편집] 토글 + SelectorDialog (등급/품질/속성) + HeroSpeAddData (134 type)");
        Logger.Info("[v0.7.8] Player editor ready (F11+4) — Resource/Quick/HeroSpeAddData × 3 / 천부 (heroTagData) / 무공 (kungfuSkills) / 돌파속성 dialog");
        Logger.Info("[v0.7.10] GetMaxTagNumPatch registered + AttriTabPanel ready ([기본]/[속성] tab)");
        Logger.Info("[v0.7.10 Phase 3] HeroDataCapBypassPatch registered (자질값 cap 돌파, opt-in via Config.EnableUncapMax)");
        Logger.Info("[v0.7.11] ContainerPanel UX overhaul — collapse + split / 일괄선택 + 카운터 + 등급별 / 무공 secondary tab + 결과 카운터 / 삭제 confirm + Clone / button 강조 / corner resize");
    }
}
