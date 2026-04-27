using System;
using LongYinRoster.Util;
using Newtonsoft.Json;
using Logger = LongYinRoster.Util.Logger;

namespace LongYinRoster.Core;

/// <summary>
/// HeroData 직렬화 / 역직렬화 게이트웨이.
///
/// 조사(Task 17 직전) 결과 IL2CPP-bound Newtonsoft.Json 의 사용 가능 표면:
/// - JsonConvert.SerializeObject(Il2CppSystem.Object) → string ✓
/// - JsonConvert.PopulateObject ✗ (부재)
/// - JsonSerializer.Populate(JsonReader, Object) ✓ (인스턴스 메서드, PopulateObject 대체)
/// - JsonSerializer.Create() ✓
/// - JsonTextReader(Il2CppSystem.IO.TextReader) ✓
///
/// HeroData 의 onSerializing/onDeserialized 콜백이 [Serializable] 표시되어 있어
/// 이 경로로 호출 시 게임 자체 직렬화 hook 이 자동 발화된다.
/// </summary>
public static class SerializerService
{
    /// <summary>HeroData 객체를 JSON 문자열로 직렬화. 게임 Hero 파일 형식과 동일.</summary>
    public static string Serialize(object hero)
    {
        if (hero == null) throw new ArgumentNullException(nameof(hero));

        if (hero is not Il2CppSystem.Object il2)
        {
            throw new InvalidOperationException(
                "Serialize requires an Il2CppSystem.Object (HeroData proxy). Got: " + hero.GetType().FullName);
        }

        return JsonConvert.SerializeObject(il2);
    }

    /// <summary>JSON 을 기존 HeroData 에 in-place 로 적용 (PopulateObject 대체 경로).</summary>
    /// <remarks>Task 18 의 적용 흐름에서 호출. 1차 구현 — 통합 테스트로 검증 필요.</remarks>
    public static void Populate(string json, object target)
    {
        if (string.IsNullOrEmpty(json)) throw new ArgumentException("json is empty", nameof(json));
        if (target == null) throw new ArgumentNullException(nameof(target));

        if (target is not Il2CppSystem.Object il2target)
        {
            throw new InvalidOperationException(
                "Populate requires an Il2CppSystem.Object target (HeroData proxy).");
        }

        try
        {
            var sr     = new Il2CppSystem.IO.StringReader(json);
            var reader = new JsonTextReader(sr);
            var ser    = JsonSerializer.Create();
            ser.Populate(reader, il2target);
            Logger.Info($"Populate succeeded on {target.GetType().Name}");
        }
        catch (Exception ex)
        {
            Logger.Error($"Populate failed: {ex.Message}");
            throw;
        }
    }
}
