using BepInEx;
using BepInEx.Unity.IL2CPP;
using LongYinRoster.Util;

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
        Logger.Info($"Loaded {NAME} v{VERSION}");
    }
}
