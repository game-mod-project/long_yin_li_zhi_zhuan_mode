using LongYinRoster.UI;
using Xunit;

namespace LongYinRoster.Tests;

public class ContainerPanelFormatTests
{
    [Fact]
    public void FormatCount_NormalInventory_NoMarker()
    {
        // 인벤토리 정상 (over-cap 아님)
        string s = ContainerPanel.FormatCount("인벤토리", 45, 720.3f, 964f, allowOvercap: true);
        Assert.Equal("인벤토리 (45개, 720.3 / 964.0 kg)", s);
    }

    [Fact]
    public void FormatCount_OvercapInventory_AppendsMarker()
    {
        // 인벤토리 over-cap (currentWeight > maxWeight + allowOvercap=true)
        string s = ContainerPanel.FormatCount("인벤토리", 180, 1020.5f, 964f, allowOvercap: true);
        Assert.Equal("인벤토리 (180개, 1020.5 / 964.0 kg) ⚠ 초과", s);
    }

    [Fact]
    public void FormatCount_StorageNoMarkerEvenIfNumericallyOver()
    {
        // 창고 hard cap — allowOvercap=false 면 cur > max 라도 마커 미부착
        string s = ContainerPanel.FormatCount("창고", 32, 320.5f, 300f, allowOvercap: false);
        Assert.Equal("창고 (32개, 320.5 / 300.0 kg)", s);
    }

    [Fact]
    public void FormatCount_AtCapacity_NoMarker()
    {
        // 정확히 한계 — over-cap 아님
        string s = ContainerPanel.FormatCount("인벤토리", 100, 964.0f, 964f, allowOvercap: true);
        Assert.Equal("인벤토리 (100개, 964.0 / 964.0 kg)", s);
    }
}
