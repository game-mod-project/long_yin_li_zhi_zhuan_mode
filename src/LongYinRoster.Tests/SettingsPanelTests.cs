using LongYinRoster.UI;
using LongYinRoster.UI.Layout;
using Shouldly;
using UnityEngine;
using Xunit;

namespace LongYinRoster.Tests;

/// <summary>
/// v0.7.6 Task 2 — SettingsPanel buffer / conflict / dirty / restore-defaults logic.
/// IMGUI 호출 (OnGUI / Draw) 은 Unity runtime dependency 라 인게임 smoke 만 검증.
/// HydrateFromValues 는 internal — InternalsVisibleTo 로 접근.
/// </summary>
public class SettingsPanelTests
{
    private static SettingsPanel MakePanel(
        KeyCode main = KeyCode.F11, KeyCode ch = KeyCode.Alpha1,
        KeyCode co = KeyCode.Alpha2, KeyCode se = KeyCode.Alpha3,
        float x = 150f, float y = 100f, float w = 800f, float h = 760f)
    {
        var p = new SettingsPanel();
        p.HydrateFromValues(main, ch, co, se, x, y, w, h);
        return p;
    }

    [Fact]
    public void Buffer_HydrateFromValues_AppliesAllFields()
    {
        var p = MakePanel(main: KeyCode.F12, ch: KeyCode.K, co: KeyCode.M, se: KeyCode.N,
                          x: 200, y: 150, w: 900, h: 700);
        p.BufferMain.ShouldBe(KeyCode.F12);
        p.BufferCharacter.ShouldBe(KeyCode.K);
        p.BufferContainer.ShouldBe(KeyCode.M);
        p.BufferSettings.ShouldBe(KeyCode.N);
        p.BufferContainerX.ShouldBe(200f);
        p.BufferContainerY.ShouldBe(150f);
        p.BufferContainerW.ShouldBe(900f);
        p.BufferContainerH.ShouldBe(700f);
    }

    [Fact]
    public void Conflict_DetectsDuplicateHotkey()
    {
        var p = MakePanel(ch: KeyCode.Alpha1, co: KeyCode.Alpha1);
        p.HasConflict.ShouldBeTrue();
        p.ConflictMessage.ShouldContain("Alpha1");
    }

    [Fact]
    public void Conflict_AllowsUniqueHotkeys()
    {
        var p = MakePanel();
        p.HasConflict.ShouldBeFalse();
        p.ConflictMessage.ShouldBe("");
    }

    [Fact]
    public void Conflict_IgnoresKeyCodeNone()
    {
        // 두 row 가 KeyCode.None 이면 conflict 아니라 (할당 안 됨)
        var p = MakePanel(main: KeyCode.None, ch: KeyCode.None);
        p.HasConflict.ShouldBeFalse();
    }

    [Fact]
    public void RestoreDefaults_ResetsBufferOnly()
    {
        var p = MakePanel(main: KeyCode.K, ch: KeyCode.M, co: KeyCode.N, se: KeyCode.O,
                          x: 999, y: 888, w: 777, h: 666);
        p.DoRestoreDefaults();
        p.BufferMain.ShouldBe(KeyCode.F11);
        p.BufferCharacter.ShouldBe(KeyCode.Alpha1);
        p.BufferContainer.ShouldBe(KeyCode.Alpha2);
        p.BufferSettings.ShouldBe(KeyCode.Alpha3);
        p.BufferContainerX.ShouldBe(150f);
        p.BufferContainerY.ShouldBe(100f);
        p.BufferContainerW.ShouldBe(800f);
        p.BufferContainerH.ShouldBe(760f);
    }

    [Fact]
    public void IsDirty_FalseAfterHydrate()
    {
        var p = MakePanel();
        p.IsDirty.ShouldBeFalse();
    }

    [Fact]
    public void IsDirty_TrueAfterBufferChange()
    {
        var p = MakePanel();
        p.SetBufferMain(KeyCode.F12);
        p.IsDirty.ShouldBeTrue();
    }

    [Fact]
    public void IsDirty_FalseAfterRevertingChanges()
    {
        var p = MakePanel();
        p.SetBufferMain(KeyCode.F12);
        p.SetBufferMain(KeyCode.F11);
        p.IsDirty.ShouldBeFalse();
    }

    [Fact]
    public void CanSave_FalseWhenConflict()
    {
        var p = MakePanel(ch: KeyCode.Alpha1, co: KeyCode.Alpha1);
        p.SetBufferMain(KeyCode.F12);   // dirty 강제
        p.HasConflict.ShouldBeTrue();
        p.CanSave.ShouldBeFalse();
    }

    [Fact]
    public void CanSave_FalseWhenNotDirty()
    {
        var p = MakePanel();
        p.HasConflict.ShouldBeFalse();
        p.IsDirty.ShouldBeFalse();
        p.CanSave.ShouldBeFalse();
    }

    [Fact]
    public void CanSave_TrueWhenDirtyAndUnique()
    {
        var p = MakePanel();
        p.SetBufferMain(KeyCode.F12);
        p.HasConflict.ShouldBeFalse();
        p.CanSave.ShouldBeTrue();
    }

    [Fact]
    public void SetBufferRect_UpdatesAllFour()
    {
        var p = MakePanel();
        p.SetBufferContainerRect(200, 250, 900, 700);
        p.BufferContainerX.ShouldBe(200f);
        p.BufferContainerY.ShouldBe(250f);
        p.BufferContainerW.ShouldBe(900f);
        p.BufferContainerH.ShouldBe(700f);
        p.IsDirty.ShouldBeTrue();
    }

    // ── v0.8.0 S1 — 설정 패널 자기 rect 버퍼 ──

    [Fact]
    public void SelfRect_DefaultsAfterHydrate()
    {
        var p = MakePanel();
        (p.BufferSelfX, p.BufferSelfY, p.BufferSelfW, p.BufferSelfH).ShouldBe((200f, 120f, 480f, 600f));
        p.IsDirty.ShouldBeFalse();
    }

    [Fact]
    public void SetBufferSelfRect_MarksDirty()
    {
        var p = MakePanel();
        p.SetBufferSelfRect(10, 20, 700, 800);
        (p.BufferSelfX, p.BufferSelfY, p.BufferSelfW, p.BufferSelfH).ShouldBe((10f, 20f, 700f, 800f));
        p.IsDirty.ShouldBeTrue();
    }

    [Fact]
    public void RestoreDefaults_ResetsSelfRectToo()
    {
        var p = MakePanel();
        p.SetBufferSelfRect(10, 20, 700, 800);
        p.DoRestoreDefaults();
        (p.BufferSelfX, p.BufferSelfY, p.BufferSelfW, p.BufferSelfH).ShouldBe((200f, 120f, 480f, 600f));
    }

    [Fact]
    public void SelfRect_TextParse_IgnoresGarbage()
    {
        SettingsPanel.TryParseRectField("abc", 100f, out _).ShouldBeFalse();
        SettingsPanel.TryParseRectField("10", 100f, out _).ShouldBeFalse();     // 최소 미만 무시
        SettingsPanel.TryParseRectField("640", 100f, out var v).ShouldBeTrue();
        v.ShouldBe(640f);
    }

    [Fact]
    public void Window_StartsHiddenWithSettingsMinSize()
    {
        var p = new SettingsPanel();
        p.Visible.ShouldBeFalse();
        p.Window.Rect.width.ShouldBe(SettingsLayout.MinSize.MinW);
    }

    // ── 리뷰 후속(2026-09-29) — 드래그/리사이즈로 바뀐 창 rect 를 저장이 되돌리지 않도록 버퍼 동기화 ──

    [Fact]
    public void SyncSelfRectFromWindow_FollowsWindowWhenNotDirty()
    {
        var p = MakePanel();
        p.Window.Hydrate(1920, 1080);
        p.Window.SetRect(50, 60, 700, 500);          // 헤더 드래그/코너 리사이즈 결과 (미바인딩이라 저장 없음)
        p.SyncSelfRectFromWindow();
        (p.BufferSelfX, p.BufferSelfY, p.BufferSelfW, p.BufferSelfH).ShouldBe((50f, 60f, 700f, 500f));
        p.IsDirty.ShouldBeFalse();                   // 창 조작은 '편집' 이 아니다 — 저장 버튼이 켜지면 안 됨
        p.IsSelfRectDirty.ShouldBeFalse();
    }

    [Fact]
    public void SyncSelfRectFromWindow_KeepsUserEditWhenDirty()
    {
        var p = MakePanel();
        p.Window.Hydrate(1920, 1080);
        p.SetBufferSelfRect(10, 20, 700, 800);       // 사용자가 필드에 입력 중
        p.Window.SetRect(50, 60, 650, 500);
        p.SyncSelfRectFromWindow();
        (p.BufferSelfX, p.BufferSelfY, p.BufferSelfW, p.BufferSelfH).ShouldBe((10f, 20f, 700f, 800f));
        p.IsSelfRectDirty.ShouldBeTrue();
    }

    [Fact]
    public void ContainerRect_TextParse_BelowLayoutMin_Ignored()
    {
        // 설정 패널의 컨테이너 W/H 필드 하한은 ContainerLayout.MinSize (구 하한 100) — 700 은 무시, 900 은 통과
        SettingsPanel.TryParseRectField("700", ContainerLayout.MinSize.MinW, out _).ShouldBeFalse();
        SettingsPanel.TryParseRectField("900", ContainerLayout.MinSize.MinW, out var v).ShouldBeTrue();
        v.ShouldBe(900f);
    }
}
