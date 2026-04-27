using System;
using LongYinRoster.Util;

namespace LongYinRoster.Core;

/// <summary>
/// Hero 직렬화 / 역직렬화 게이트웨이 (1차 STUB).
///
/// IL2CPP-bound Newtonsoft.Json (`BepInEx/interop/Newtonsoft.Json.dll`) 의 API
/// 가 표준 NuGet 버전과 매우 다르고, 우리 호출 경로(System.Object → JsonConvert)
/// 들이 거의 모두 컴파일/실행에 실패함:
///   - JsonSerializerSettings 의 NullValueHandling/DefaultValueHandling 등 setter 없음
///   - JsonConvert.SerializeObject 가 Il2CppSystem.Object/Il2CppSystem.Type 를 받음
///   - JsonConvert.PopulateObject 자체가 부재
///   - JObject foreach 가 C# duck-typed 인터페이스와 호환되지 않음
///
/// 따라서 실제 직렬화 메커니즘은 Task 17 (live capture) / Task 18 (apply flow)
/// 통합 테스트 단계에서 게임을 실제로 실행하면서 확정한다. 현재 메서드들은
/// 호출 시점에 명확한 NotImplementedException 을 던져 콜 사이트 미흡 / 아직
/// 미구현 상태를 즉시 노출시킨다.
///
/// 후보 1: 게임 자체 SaveManager 의 Hero[] 직렬화 메서드를 Harmony 로 재호출
/// 후보 2: Il2CppInterop 의 generated Hero 클래스에 대해 reflection 기반 필드 복사
/// 후보 3: JObject 의 properties 를 `((IDictionary)obj).Keys` 등 우회 경로로 순회
///
/// Task 17/18 에서 실제로 동작하는 경로를 발견하면 이 stub 을 교체한다.
/// </summary>
public static class SerializerService
{
    /// <summary>Hero 객체를 JSON 문자열로 직렬화. v0.1 STUB.</summary>
    public static string Serialize(object hero)
    {
        Logger.Error("SerializerService.Serialize is a stub. " +
                     "IL2CPP-Newtonsoft API path TBD in Task 17 (live capture).");
        throw new NotImplementedException(
            "SerializerService.Serialize: IL2CPP path not yet established. " +
            "See class docs for candidate strategies.");
    }

    /// <summary>JSON 을 기존 객체에 in-place 로 적용 (PopulateObject 대체). v0.1 STUB.</summary>
    public static void Populate(string json, object target)
    {
        Logger.Error("SerializerService.Populate is a stub. " +
                     "IL2CPP-Newtonsoft has no PopulateObject; mechanism TBD in Task 18 (apply flow).");
        throw new NotImplementedException(
            "SerializerService.Populate: IL2CPP path not yet established. " +
            "See class docs for candidate strategies.");
    }
}
