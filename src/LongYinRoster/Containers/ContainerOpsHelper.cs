using System.Collections.Generic;
using System.Text.Json;
using Logger = LongYinRoster.Util.Logger;

namespace LongYinRoster.Containers;

/// <summary>
/// ContainerPanel callback 처리 helper. 8개 작업 통합.
/// </summary>
public sealed class ContainerOpsHelper
{
    private readonly ContainerRepository _repo;
    public  int CurrentContainerIndex { get; set; } = -1;

    public ContainerOpsHelper(ContainerRepository repo) => _repo = repo;

    public sealed class Result
    {
        public int    Succeeded { get; set; }
        public int    Failed    { get; set; }
        public string Reason    { get; set; } = "";
    }

    public Result GameToContainer(object il2List, HashSet<int> indices, bool removeFromGame)
    {
        var res = new Result();
        if (CurrentContainerIndex < 0) { res.Reason = "컨테이너 미선택"; return res; }
        if (indices.Count == 0) { res.Reason = "선택된 항목 없음"; return res; }
        try
        {
            string extracted = ContainerOps.ExtractGameItemsToJson(il2List, indices);
            string existing  = _repo.LoadItemsJson(CurrentContainerIndex);
            string merged    = ContainerOps.AppendItemsJson(existing, extracted);
            _repo.SaveItemsJson(CurrentContainerIndex, merged);
            res.Succeeded = JsonDocument.Parse(extracted).RootElement.GetArrayLength();
            if (removeFromGame)
                ContainerOps.RemoveGameItems(il2List, indices);
        }
        catch (System.Exception ex)
        {
            res.Reason = $"GameToContainer threw: {ex.Message}";
            Logger.Warn(res.Reason);
        }
        return res;
    }

    public Result ContainerToGame(object player, HashSet<int> indices, bool removeFromContainer, int gameMaxCapacity = 171)
    {
        var res = new Result();
        if (CurrentContainerIndex < 0) { res.Reason = "컨테이너 미선택"; return res; }
        if (indices.Count == 0) { res.Reason = "선택된 항목 없음"; return res; }
        try
        {
            string existing  = _repo.LoadItemsJson(CurrentContainerIndex);
            string extracted = ContainerOps.ExtractItemsByIndex(existing, indices);
            var gr = ContainerOps.AddItemsJsonToGame(player, extracted, gameMaxCapacity);
            res.Succeeded = gr.Succeeded;
            res.Failed    = gr.Failed;
            if (removeFromContainer && gr.Succeeded > 0)
            {
                var sortedIndices = new List<int>(indices);
                sortedIndices.Sort();
                var toRemove = new HashSet<int>();
                for (int k = 0; k < gr.Succeeded && k < sortedIndices.Count; k++) toRemove.Add(sortedIndices[k]);
                string remaining = ContainerOps.RemoveItemsByIndex(existing, toRemove);
                _repo.SaveItemsJson(CurrentContainerIndex, remaining);
            }
            res.Reason = gr.Reason ?? "";
        }
        catch (System.Exception ex)
        {
            res.Reason = $"ContainerToGame threw: {ex.Message}";
            Logger.Warn(res.Reason);
        }
        return res;
    }

    public Result DeleteFromContainer(HashSet<int> indices)
    {
        var res = new Result();
        if (CurrentContainerIndex < 0) { res.Reason = "컨테이너 미선택"; return res; }
        if (indices.Count == 0) { res.Reason = "선택된 항목 없음"; return res; }
        try
        {
            string existing = _repo.LoadItemsJson(CurrentContainerIndex);
            string remaining = ContainerOps.RemoveItemsByIndex(existing, indices);
            _repo.SaveItemsJson(CurrentContainerIndex, remaining);
            res.Succeeded = indices.Count;
        }
        catch (System.Exception ex)
        {
            res.Reason = $"DeleteFromContainer threw: {ex.Message}";
            Logger.Warn(res.Reason);
        }
        return res;
    }
}
