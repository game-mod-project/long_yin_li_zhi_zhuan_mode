using System;
using System.Collections.Generic;
using System.IO;
using LongYinRoster.Util;

namespace LongYinRoster.Slots;

public sealed class SlotRepository
{
    private readonly string _dir;
    private readonly int    _max;
    private readonly List<SlotEntry> _entries = new();

    public SlotRepository(string slotDir, int maxUserSlots = 20)
    {
        _dir = slotDir;
        _max = maxUserSlots;
        Directory.CreateDirectory(_dir);
        Reload();
    }

    public IReadOnlyList<SlotEntry> All => _entries;

    public string PathFor(int index) =>
        Path.Combine(_dir, $"slot_{index:D2}.json");

    public void Reload()
    {
        _entries.Clear();
        for (int i = 0; i <= _max; i++)
        {
            var path = PathFor(i);
            if (File.Exists(path))
            {
                try
                {
                    var p = SlotFile.Read(path);
                    _entries.Add(new SlotEntry(i, false, p.Meta, path));
                }
                catch (UnsupportedSchemaException ex)
                {
                    Logger.Warn($"slot {i}: {ex.Message}");
                    _entries.Add(new SlotEntry(i, false, null, path)); // shown as broken
                }
                catch (Exception ex)
                {
                    Logger.Error($"slot {i} unreadable: {ex.Message}");
                    _entries.Add(new SlotEntry(i, true, null, path));
                }
            }
            else
            {
                _entries.Add(new SlotEntry(i, true, null, path));
            }
        }
    }

    public void Write(int index, SlotPayload payload)
    {
        if (index == 0)
            throw new InvalidOperationException(
                "slot 0 is auto-backup-only; use WriteAutoBackup instead");
        if (index < 1 || index > _max)
            throw new ArgumentOutOfRangeException(nameof(index));

        SlotFile.Write(PathFor(index), payload);
        Logger.Info($"slot {index} written ({payload.Meta.UserLabel})");
    }

    public void WriteAutoBackup(SlotPayload payload)
    {
        SlotFile.Write(PathFor(0), payload);
        Logger.Info("slot 0 auto-backup written");
    }

    public void Delete(int index)
    {
        var path = PathFor(index);
        if (File.Exists(path)) File.Delete(path);
        Logger.Info($"slot {index} deleted");
    }

    public int AllocateNextFree()
    {
        for (int i = 1; i <= _max; i++)
            if (_entries[i].IsEmpty) return i;
        return -1;
    }

    public void Rename(int index, string newLabel) =>
        UpdateMeta(index, m => m with { UserLabel = newLabel });

    public void UpdateComment(int index, string newComment) =>
        UpdateMeta(index, m => m with { UserComment = newComment });

    private void UpdateMeta(int index, Func<SlotPayloadMeta, SlotPayloadMeta> patch)
    {
        var path = PathFor(index);
        var loaded = SlotFile.Read(path);
        var updated = new SlotPayload { Meta = patch(loaded.Meta), Player = loaded.Player };
        SlotFile.Write(path, updated);
    }
}
