using LongYinRoster.Containers;
using LongYinRoster.UI;
using LongYinRoster.UI.Layout;
using Shouldly;
using System.Collections.Generic;
using Xunit;

namespace LongYinRoster.Tests;

public class ContainerPanelFocusTests
{
    private static ContainerPanel.ItemRow Row(int idx) => new ContainerPanel.ItemRow
    {
        Index = idx, Name = $"item{idx}", Type = 0, SubType = 0, EnhanceLv = 0, Weight = 1f, Equipped = false,
    };

    [Fact]
    public void GetFocusedRawItem_NoFocus_ReturnsNull()
    {
        var panel = new ContainerPanel();
        panel.GetFocusedRawItem().ShouldBeNull();
    }

    [Fact]
    public void GetFocusedRawItem_OOB_ClearsAndReturnsNull()
    {
        var panel = new ContainerPanel();
        var rows = new List<ContainerPanel.ItemRow> { Row(0), Row(1) };
        var raw = new List<object> { "item0", "item1" };
        panel.SetInventoryRows(rows, raw);
        panel.SetFocus(ContainerArea.Inventory, 5);   // OOB
        panel.GetFocusedRawItem().ShouldBeNull();
        panel.HasFocus.ShouldBeFalse();   // auto-clear
    }

    [Fact]
    public void Window_StartsHiddenWithContainerMinSize()
    {
        // 구 cfg 의 600×400(구 MIN_W/H) 은 Hydrate 가 758×588 로 올린다(PanelWindow.Hydrate_RaisesBelowMinToMin) — 여기선 생성 직후 최소 크기 확인
        var panel = new ContainerPanel();
        panel.Visible.ShouldBeFalse();
        (panel.Window.Rect.width, panel.Window.Rect.height).ShouldBe((ContainerLayout.MinSize.MinW, ContainerLayout.MinSize.MinH));
    }
}
