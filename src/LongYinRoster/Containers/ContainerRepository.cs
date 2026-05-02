using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace LongYinRoster.Containers;

/// <summary>
/// 다중 컨테이너 디스크 io. SlotRepository 패턴 mirror.
/// 파일 명: container_NN.json (NN = 0-padded index, 무제한)
/// </summary>
public sealed class ContainerRepository
{
    private readonly string _dir;
    private static readonly Regex FileRegex = new(@"^container_(\d+)\.json$", RegexOptions.Compiled);

    public ContainerRepository(string directory)
    {
        _dir = directory;
        Directory.CreateDirectory(_dir);
    }

    public int CreateNew(string name)
    {
        int idx = NextIndex();
        var meta = new ContainerMetadata { ContainerIndex = idx, ContainerName = name };
        var json = ContainerFile.Compose(meta, "[]");
        File.WriteAllText(PathFor(idx), json);
        return idx;
    }

    public List<ContainerMetadata> List()
    {
        var result = new List<ContainerMetadata>();
        foreach (var f in Directory.GetFiles(_dir, "container_*.json"))
        {
            var m = FileRegex.Match(Path.GetFileName(f));
            if (!m.Success) continue;
            try
            {
                var parsed = ContainerFile.Parse(File.ReadAllText(f));
                result.Add(parsed.Metadata);
            }
            catch { }
        }
        result.Sort((a, b) => a.ContainerIndex.CompareTo(b.ContainerIndex));
        return result;
    }

    public ContainerMetadata? LoadMetadata(int idx)
    {
        var f = PathFor(idx);
        if (!File.Exists(f)) return null;
        try { return ContainerFile.Parse(File.ReadAllText(f)).Metadata; }
        catch { return null; }
    }

    public string LoadItemsJson(int idx)
    {
        var f = PathFor(idx);
        if (!File.Exists(f)) return "[]";
        try { return ContainerFile.Parse(File.ReadAllText(f)).ItemsJson; }
        catch { return "[]"; }
    }

    public void SaveItemsJson(int idx, string itemsJson)
    {
        var meta = LoadMetadata(idx) ?? new ContainerMetadata { ContainerIndex = idx };
        File.WriteAllText(PathFor(idx), ContainerFile.Compose(meta, itemsJson));
    }

    public void Rename(int idx, string newName)
    {
        var meta = LoadMetadata(idx);
        if (meta == null) return;
        meta.ContainerName = newName;
        var items = LoadItemsJson(idx);
        File.WriteAllText(PathFor(idx), ContainerFile.Compose(meta, items));
    }

    public void Delete(int idx)
    {
        var f = PathFor(idx);
        if (File.Exists(f)) File.Delete(f);
    }

    private string PathFor(int idx) => Path.Combine(_dir, $"container_{idx:D2}.json");

    private int NextIndex()
    {
        int max = 0;
        foreach (var f in Directory.GetFiles(_dir, "container_*.json"))
        {
            var m = FileRegex.Match(Path.GetFileName(f));
            if (m.Success && int.TryParse(m.Groups[1].Value, out var i) && i > max) max = i;
        }
        return max + 1;
    }
}
