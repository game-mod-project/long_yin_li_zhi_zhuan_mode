using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using Newtonsoft.Json;
using Logger = LongYinRoster.Util.Logger;

namespace LongYinRoster.Core;

/// <summary>
/// HeroData 직렬화 게이트웨이.
///
/// v0.1 의 범위: Serialize (game → JSON) 만 사용. 캡처 / 디스크 저장 / 디테일 패널 표시에
/// 충분.
///
/// v0.6.4 — JsonConvert 가 IL2CPP wrapper 의 일부 sub-object 를 제외하므로 (e.g.,
/// partPosture 의 PartPostureData) post-serialization 에 reflection 으로 추가 데이터
/// 주입. 현재 inject 대상:
///   - _partPostureFloats: PartPostureData.partPosture (List<float>) 의 값 배열
///
/// IL2CPP-bound Newtonsoft.Json 의 사용 가능 표면 (HANDOFF §4.1 참고):
///   - JsonConvert.SerializeObject(Il2CppSystem.Object) → string  ✓
///   - JsonConvert.PopulateObject                                 ✗ (부재)
/// </summary>
public static class SerializerService
{
    private const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

    /// <summary>HeroData 객체를 JSON 문자열로 직렬화. 게임 Hero 파일 형식과 동일 + extras.</summary>
    public static string Serialize(object hero)
    {
        if (hero == null) throw new ArgumentNullException(nameof(hero));

        if (hero is not Il2CppSystem.Object il2)
        {
            throw new InvalidOperationException(
                "Serialize requires an Il2CppSystem.Object (HeroData proxy). Got: " + hero.GetType().FullName);
        }

        var json = JsonConvert.SerializeObject(il2);

        // v0.6.4 — partPosture (PartPostureData wrapper) 가 JsonConvert 에서 제외되므로
        // reflection 으로 List<float> 값 추출해 JSON 에 주입.
        json = InjectPartPostureFloats(json, hero);

        return json;
    }

    private static string InjectPartPostureFloats(string playerJson, object hero)
    {
        try
        {
            var pp = ReadFieldOrProperty(hero, "partPosture");
            if (pp == null) return playerJson;
            var inner = ReadFieldOrProperty(pp, "partPosture");
            if (inner == null) return playerJson;
            int n = IL2CppListOps.Count(inner);
            if (n <= 0) return playerJson;

            var sb = new StringBuilder();
            sb.Append("[");
            for (int i = 0; i < n; i++)
            {
                if (i > 0) sb.Append(",");
                var v = IL2CppListOps.Get(inner, i);
                sb.Append(Convert.ToSingle(v).ToString(System.Globalization.CultureInfo.InvariantCulture));
            }
            sb.Append("]");

            // playerJson 이 `{...}` 형태. 닫는 `}` 직전에 `,"_partPostureFloats":[...]` 주입.
            int closing = playerJson.LastIndexOf('}');
            if (closing < 0) return playerJson;
            string head = playerJson.Substring(0, closing).TrimEnd();
            string tail = playerJson.Substring(closing);
            string injection = (head.EndsWith("{") ? "" : ",") +
                               "\"_partPostureFloats\":" + sb.ToString();
            return head + injection + tail;
        }
        catch (Exception ex)
        {
            Logger.Warn($"InjectPartPostureFloats: {ex.GetType().Name}: {ex.Message}");
            return playerJson;
        }
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
