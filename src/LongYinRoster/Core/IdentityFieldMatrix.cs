using System.Collections.Generic;

namespace LongYinRoster.Core;

public enum IdentityPath { Setter, BackingField, Harmony }

public sealed record IdentityFieldEntry(
    string         Name,
    string         JsonPath,
    string         PropertyName,
    System.Type    Type,
    IdentityPath   Path,
    string?        BackingFieldName);

/// <summary>
/// 9 정체성 필드 매핑. PoC Task A2 결과 (commit 4887f01) — 시도 A (setter 직접) PASS:
///   - Setter:       property setter 직접 호출 (Newtonsoft Populate 함정 통과)
///   - BackingField: <heroName>k__BackingField 직접 set (setter no-op 일 때 fallback) — v0.4 미사용
///   - Harmony:      이 path 는 v0.4 미사용 — v0.5+ 후보
///
/// **잔여 risk**: in-memory PASS 만 검증. save → reload 후 변경값이 살아남는지는 D15
/// smoke item E 에서 추가 검증. 만약 fail 하면 Path = Harmony 로 회귀 + 별도 helper.
/// </summary>
public static class IdentityFieldMatrix
{
    public static readonly IReadOnlyList<IdentityFieldEntry> Entries = new[]
    {
        new IdentityFieldEntry("이름",       "heroName",       "heroName",       typeof(string), IdentityPath.Setter, null),
        new IdentityFieldEntry("별명",       "heroNickName",   "heroNickName",   typeof(string), IdentityPath.Setter, null),
        new IdentityFieldEntry("성씨",       "heroFamilyName", "heroFamilyName", typeof(string), IdentityPath.Setter, null),
        new IdentityFieldEntry("설정명",     "settingName",    "settingName",    typeof(string), IdentityPath.Setter, null),
        new IdentityFieldEntry("성별",       "isFemale",       "isFemale",       typeof(bool),   IdentityPath.Setter, null),
        new IdentityFieldEntry("나이",       "age",            "age",            typeof(int),    IdentityPath.Setter, null),
        new IdentityFieldEntry("천성",       "nature",         "nature",         typeof(int),    IdentityPath.Setter, null),
        new IdentityFieldEntry("재능",       "talent",         "talent",         typeof(int),    IdentityPath.Setter, null),
        new IdentityFieldEntry("세대",       "generation",     "generation",     typeof(int),    IdentityPath.Setter, null),
    };
}
