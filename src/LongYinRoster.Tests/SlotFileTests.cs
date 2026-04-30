using System;
using System.IO;
using System.Text.Json;
using FluentAssertions;
using LongYinRoster.Core;
using LongYinRoster.Slots;
using Xunit;

namespace LongYinRoster.Tests;

public class SlotFileTests : IDisposable
{
    private readonly string _tmp = Path.Combine(Path.GetTempPath(),
        $"LongYinRoster.Tests.{Guid.NewGuid():N}");

    private string _tempDir => _tmp;

    public SlotFileTests() => Directory.CreateDirectory(_tmp);
    public void Dispose()
    {
        try { Directory.Delete(_tmp, recursive: true); } catch { }
    }

    private const string SamplePlayer = @"{""heroID"":0,""heroName"":""테스트""}";

    private SlotPayloadMeta MakeMeta(int slot, ApplySelection? applySelection = null) =>
        new SlotPayloadMeta(
            SchemaVersion: 1, ModVersion: "0.1.0", SlotIndex: slot,
            UserLabel: "테스트", UserComment: "",
            CaptureSource: "live", CaptureSourceDetail: "",
            CapturedAt: new DateTime(2026, 4, 27, 19, 0, 0),
            GameSaveVersion: "1.0.0 f8.2", GameSaveDetail: "",
            Summary: new SlotMetadata("테스트", "", false, 18, 1, 100f, 0, 0, 0, 0, 0L, 0),
            ApplySelection: applySelection ?? ApplySelection.V03Default());

    private SlotPayload SamplePayload(int idx) => new()
    {
        Meta = MakeMeta(idx),
        Player = SamplePlayer,
    };

    [Fact]
    public void Write_Then_Read_RoundTrips_The_Payload()
    {
        var path = Path.Combine(_tmp, "slot_01.json");
        SlotFile.Write(path, SamplePayload(1));
        var loaded = SlotFile.Read(path);

        loaded.Meta.SlotIndex.Should().Be(1);
        loaded.Meta.UserLabel.Should().Be("테스트");
        loaded.Meta.SchemaVersion.Should().Be(1);

        using var doc = JsonDocument.Parse(loaded.Player);
        doc.RootElement.GetProperty("heroName").GetString().Should().Be("테스트");
    }

    [Fact]
    public void Write_Is_Atomic_Via_Tmp_Then_Replace()
    {
        var path = Path.Combine(_tmp, "slot_02.json");
        SlotFile.Write(path, SamplePayload(2));
        File.Exists(path + ".tmp").Should().BeFalse("the .tmp staging file must be cleaned up");
        File.Exists(path).Should().BeTrue();
    }

    [Fact]
    public void Read_Throws_On_Unsupported_Schema_Version()
    {
        var path = Path.Combine(_tmp, "slot_03.json");
        File.WriteAllText(path, @"{""_meta"":{""schemaVersion"":99},""player"":{}}");
        var act = () => SlotFile.Read(path);
        act.Should().Throw<UnsupportedSchemaException>().WithMessage("*99*");
    }

    [Fact]
    public void Write_ThenRead_PreservesApplySelection()
    {
        var path = Path.Combine(_tempDir, "slot_05.json");
        var sel = new ApplySelection
        {
            Stat = false, Honor = true, TalentTag = false, Skin = false,
            SelfHouse = true, Identity = true, ActiveKungfu = false,
            ItemList = false, SelfStorage = true,
        };
        var meta = MakeMeta(slot: 5, applySelection: sel);
        SlotFile.Write(path, new SlotPayload { Meta = meta, Player = "{}" });

        var loaded = SlotFile.Read(path);
        loaded.Meta.ApplySelection.Stat.Should().BeFalse();
        loaded.Meta.ApplySelection.Honor.Should().BeTrue();
        loaded.Meta.ApplySelection.SelfHouse.Should().BeTrue();
        loaded.Meta.ApplySelection.Identity.Should().BeTrue();
        loaded.Meta.ApplySelection.ActiveKungfu.Should().BeFalse();
        loaded.Meta.ApplySelection.SelfStorage.Should().BeTrue();
    }

    [Fact]
    public void Read_LegacySlotWithoutApplySelection_FallsBackToV03Default()
    {
        var path = Path.Combine(_tempDir, "slot_legacy.json");
        // v0.2/v0.3 형식 — _meta.applySelection field 없음
        File.WriteAllText(path,
            "{\n  \"_meta\": { \"schemaVersion\": 1, \"slotIndex\": 1, \"userLabel\":\"x\", \"userComment\":\"\"," +
            " \"captureSource\":\"live\", \"captureSourceDetail\":\"\", \"capturedAt\":\"2026-01-01T00:00:00Z\"," +
            " \"gameSaveVersion\":\"\", \"gameSaveDetail\":\"\", \"modVersion\":\"\", " +
            " \"summary\":{ \"heroName\":\"h\", \"heroNickName\":\"n\", \"isFemale\":false, \"age\":20," +
            " \"generation\":1, \"fightScore\":0, \"kungfuCount\":0, \"kungfuMaxLvCount\":0," +
            " \"itemCount\":0, \"storageCount\":0, \"money\":0, \"talentCount\":0 } },\n" +
            " \"player\": {} }");

        var loaded = SlotFile.Read(path);
        // V03Default: 4 카테고리 on
        loaded.Meta.ApplySelection.Stat.Should().BeTrue();
        loaded.Meta.ApplySelection.Honor.Should().BeTrue();
        loaded.Meta.ApplySelection.TalentTag.Should().BeTrue();
        loaded.Meta.ApplySelection.Skin.Should().BeTrue();
        loaded.Meta.ApplySelection.Identity.Should().BeFalse();
        loaded.Meta.ApplySelection.ItemList.Should().BeFalse();
    }
}
