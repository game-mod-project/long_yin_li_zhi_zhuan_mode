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
    public const string VERSION = "0.1.0";

    public override void Load()
    {
        Logger.Init(this.Log);
        ModCfg.Bind(this.Config);
        AddComponent<ModWindow>();

        // Harmony patches: 모드 창 영역 안 mouse input 차단.
        var harmony = new Harmony(GUID);
        harmony.PatchAll(typeof(InputBlockerPatch));
        Logger.Info($"Harmony: {harmony.GetPatchedMethods().Count()} method(s) patched");

        Logger.Info($"Loaded {NAME} v{VERSION}");
    }
}
