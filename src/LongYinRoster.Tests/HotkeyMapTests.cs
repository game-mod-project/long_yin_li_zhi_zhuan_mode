using LongYinRoster.Util;
using Shouldly;
using UnityEngine;
using Xunit;

namespace LongYinRoster.Tests;

/// <summary>
/// v0.7.6 Task 1 — HotkeyMap.NumpadFor 의 Alpha↔Keypad 매핑 검증.
/// HotkeyMap.Bind() 자체는 BepInEx ConfigEntry 의존이라 인게임 smoke 만.
/// </summary>
public class HotkeyMapTests
{
    [Theory]
    [InlineData(KeyCode.Alpha0, KeyCode.Keypad0)]
    [InlineData(KeyCode.Alpha1, KeyCode.Keypad1)]
    [InlineData(KeyCode.Alpha5, KeyCode.Keypad5)]
    [InlineData(KeyCode.Alpha9, KeyCode.Keypad9)]
    public void NumpadFor_AlphaKeys_ReturnsKeypadEquivalent(KeyCode alpha, KeyCode expected)
    {
        HotkeyMap.NumpadFor(alpha).ShouldBe(expected);
    }

    [Theory]
    [InlineData(KeyCode.F11)]
    [InlineData(KeyCode.A)]
    [InlineData(KeyCode.Escape)]
    [InlineData(KeyCode.None)]
    [InlineData(KeyCode.Keypad1)]   // 이미 Keypad — Alpha 가 아니라 None 반환
    public void NumpadFor_NonAlphaKeys_ReturnsNone(KeyCode key)
    {
        HotkeyMap.NumpadFor(key).ShouldBe(KeyCode.None);
    }

    [Fact]
    public void Numpad_Pair_DistinctFromAlpha()
    {
        // Sanity — Alpha2 != Keypad2 enum value (UI capture 시 두 값 분리 검증)
        ((int)KeyCode.Alpha2).ShouldNotBe((int)KeyCode.Keypad2);
    }
}
