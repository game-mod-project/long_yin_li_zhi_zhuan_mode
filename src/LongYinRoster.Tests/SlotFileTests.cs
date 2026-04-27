using System;
using System.IO;
using FluentAssertions;
using LongYinRoster.Slots;
using Newtonsoft.Json.Linq;
using Xunit;

namespace LongYinRoster.Tests;

public class SlotFileTests : IDisposable
{
    private readonly string _tmp = Path.Combine(Path.GetTempPath(),
        $"LongYinRoster.Tests.{Guid.NewGuid():N}");

    public SlotFileTests() => Directory.CreateDirectory(_tmp);
    public void Dispose()
    {
        try { Directory.Delete(_tmp, recursive: true); } catch { }
    }

    private static JObject SamplePlayer() => JObject.Parse(@"{""heroID"":0,""heroName"":""테스트""}");

    private SlotPayload SamplePayload(int idx) => new()
    {
        Meta = new SlotPayloadMeta(
            SchemaVersion: 1, ModVersion: "0.1.0", SlotIndex: idx,
            UserLabel: "테스트", UserComment: "",
            CaptureSource: "live", CaptureSourceDetail: "",
            CapturedAt: new DateTime(2026, 4, 27, 19, 0, 0),
            GameSaveVersion: "1.0.0 f8.2", GameSaveDetail: "",
            Summary: new SlotMetadata("테스트", "", false, 18, 1, 100f, 0, 0, 0, 0, 0L, 0)),
        Player = SamplePlayer(),
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
        ((string?)loaded.Player["heroName"]).Should().Be("테스트");
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
}
