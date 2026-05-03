using Xunit;
using LongYinRoster.Containers;

namespace LongYinRoster.Tests;

public class ContainerRowBuilderTests
{
    [Fact]
    public void FromJsonArray_fills_sort_keys_for_v0_7_2()
    {
        string json = "[" +
            "{\"name\":\"补血弹\",\"type\":2,\"subType\":1,\"weight\":1.5,\"grade\":3,\"quality\":4}," +
            "{\"name\":\"无名刀\",\"type\":1,\"subType\":0,\"weight\":3.2}" +
            "]";

        var rows = ContainerRowBuilder.FromJsonArray(json);
        Assert.Equal(2, rows.Count);

        Assert.Equal("002.001", rows[0].CategoryKey);
        Assert.Equal("补血弹", rows[0].NameRaw);
        Assert.Equal(3, rows[0].GradeOrder);
        Assert.Equal(4, rows[0].QualityOrder);

        Assert.Equal("001.000", rows[1].CategoryKey);
        Assert.Equal("无名刀", rows[1].NameRaw);
        Assert.Equal(-1, rows[1].GradeOrder);
        Assert.Equal(-1, rows[1].QualityOrder);
    }

    [Fact]
    public void FromJsonArray_reads_itemLv_rareLv_from_game_json_format()
    {
        // 게임 ItemData JSON (spike 결과) 은 itemLv/rareLv 를 사용. 외부 디스크 컨테이너 파일은 게임 JSON 그대로.
        string json = "[{\"name\":\"绝世大剑\",\"type\":1,\"subType\":0,\"weight\":24.0,\"itemLv\":5,\"rareLv\":5}]";
        var rows = ContainerRowBuilder.FromJsonArray(json);
        Assert.Single(rows);
        Assert.Equal(5, rows[0].GradeOrder);
        Assert.Equal(5, rows[0].QualityOrder);
    }
}
