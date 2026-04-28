using System;
using Newtonsoft.Json;

namespace LongYinRoster.Core;

/// <summary>
/// HeroData 직렬화 게이트웨이.
///
/// v0.1 의 범위: Serialize (game → JSON) 만 사용. 캡처 / 디스크 저장 / 디테일 패널 표시에
/// 충분.
///
/// Apply (slot → game) 흐름은 v0.1 에서 제외:
///   - JsonSerializer.Populate(reader, il2target) — IL2CPP 환경에서 silent no-op
///   - DeserializeObject + IntPtr wrap + HeroList swap — 부분 작동하나 game-state 의
///     reference 필드(장비/무공/포트레이트/문파)와 link 안 됨. v0.2 에서 PinpointPatcher
///     기반 partial-field copy 로 재설계.
///
/// IL2CPP-bound Newtonsoft.Json 의 사용 가능 표면 (HANDOFF §4.1 참고):
///   - JsonConvert.SerializeObject(Il2CppSystem.Object) → string  ✓
///   - JsonConvert.PopulateObject                                 ✗ (부재)
///   - JsonSerializer.Populate(JsonReader, Object)                ✓ but no-op in IL2CPP
///   - JsonConvert.DeserializeObject(string, Il2CppSystem.Type)   ✓ but returns base
///
/// HeroData 의 [OnSerializing] / [OnDeserialized] 콜백은 [Serializable] 표시되어 있어
/// 직렬화 경로에서 게임 자체 hook 이 자동 발화한다.
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
}
