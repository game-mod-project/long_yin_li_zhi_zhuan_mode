using LongYinRoster.Core;
using Shouldly;
using Xunit;

namespace LongYinRoster.Tests;

public class ItemDetailReflectorRawTests
{
    private sealed class FakeEquipmentData
    {
        public int enhanceLv = 3;
        public bool equiped = true;
        // IL2CPP wrapper meta — should be filtered
        public System.IntPtr Pointer = (System.IntPtr)123;
        public string ObjectClass = "stub";
    }

    private sealed class FakeBookData
    {
        public int learnLv = 5;
        public int maxLearnLv = 10;
    }

    private sealed class FakeItem
    {
        public string name = "多情飞刀";
        public int type = 0;
        public int subType = 0;
        public int itemLv = 5;
        public int rareLv = 5;
        public float weight = 2.5f;
        public FakeEquipmentData equipmentData = new();
        public FakeBookData bookData = null!;   // type=0 inactive book
        // IL2CPP wrapper meta on item itself
        public System.IntPtr Pointer = (System.IntPtr)456;
    }

    [Fact]
    public void GetRawFields_NullItem_ReturnsEmpty()
    {
        ItemDetailReflector.GetRawFields(null).ShouldBeEmpty();
    }

    [Fact]
    public void GetRawFields_DumpsItemFields()
    {
        var item = new FakeItem();
        var raw = ItemDetailReflector.GetRawFields(item);
        raw.ShouldContain(x => x.FieldName == "name" && x.Value == "多情飞刀");
        raw.ShouldContain(x => x.FieldName == "type" && x.Value == "0");
        raw.ShouldContain(x => x.FieldName == "weight" && x.Value == "2.5");
    }

    [Fact]
    public void GetRawFields_FiltersIL2CppMeta()
    {
        var item = new FakeItem();
        var raw = ItemDetailReflector.GetRawFields(item);
        raw.ShouldNotContain(x => x.FieldName == "Pointer");
        raw.ShouldNotContain(x => x.FieldName == "ObjectClass");
    }

    [Fact]
    public void GetRawFields_DumpsActiveSubDataOnly()
    {
        var item = new FakeItem();   // type=0 → equipmentData active, bookData null
        var raw = ItemDetailReflector.GetRawFields(item);
        raw.ShouldContain(x => x.FieldName == "[equipmentData] enhanceLv" && x.Value == "3");
        raw.ShouldContain(x => x.FieldName == "[equipmentData] equiped" && x.Value == "True");
        raw.ShouldNotContain(x => x.FieldName.StartsWith("[bookData]"));
    }
}
