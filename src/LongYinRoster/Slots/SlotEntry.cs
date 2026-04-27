namespace LongYinRoster.Slots;

public readonly record struct SlotEntry(
    int           Index,
    bool          IsEmpty,
    SlotPayloadMeta? Meta,
    string        FilePath);
