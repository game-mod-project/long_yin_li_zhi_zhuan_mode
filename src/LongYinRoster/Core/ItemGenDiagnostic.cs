using System;
using System.Linq;
using System.Reflection;
using System.Text;
using Logger = LongYinRoster.Util.Logger;

namespace LongYinRoster.Core;

/// <summary>v0.7.13 임시 spike — generator 시그니처 + DB 접근자 dump. Task 8 에서 제거.</summary>
public static class ItemGenDiagnostic
{
    private const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

    public static void Dump()
    {
        var gc = GameControllerLocator.GetGameController();
        if (gc == null) { Logger.Info("[ItemGenDiag] GameController null"); return; }
        Logger.Info($"[ItemGenDiag] GameController type = {gc.GetType().FullName}");

        var sb = new StringBuilder();
        foreach (var m in gc.GetType().GetMethods(F)
                     .Where(m => m.Name.StartsWith("Generate", StringComparison.Ordinal))
                     .OrderBy(m => m.Name))
        {
            var ps = string.Join(", ", m.GetParameters().Select(p => $"{p.ParameterType.Name} {p.Name}"));
            sb.Append($"\n  {m.ReturnType.Name} {m.Name}({ps})");
        }
        Logger.Info($"[ItemGenDiag] GameController Generate* methods:{sb}");

        var gdcType = Type.GetType("GameDataController, Assembly-CSharp");
        var gdcInst = gdcType?.GetProperty("Instance",
            BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)?.GetValue(null);
        if (gdcInst != null)
        {
            var dbs = gdcInst.GetType().GetProperties(F)
                .Where(p => p.Name.IndexOf("DataBase", StringComparison.OrdinalIgnoreCase) >= 0
                         || p.Name.IndexOf("DB", StringComparison.Ordinal) >= 0)
                .Select(p => p.Name);
            Logger.Info($"[ItemGenDiag] GDC DB props: {string.Join(", ", dbs)}");
        }
    }
}
