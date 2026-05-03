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
        public int    Succeeded     { get; set; }
        public int    Failed        { get; set; }
        public float  OverCapWeight { get; set; }   // v0.7.1 — 인벤 over-cap 발생 무게 (kg)
        public string Reason        { get; set; } = "";
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

    /// <summary>
    /// v0.7.1: 컨테이너 → player.itemListData (인벤토리). over-weight 허용 (속도 페널티는 게임 메커니즘).
    /// </summary>
    public Result ContainerToInventory(object player, HashSet<int> indices, bool removeFromContainer, float maxWeight)
    {
        return ContainerToTarget(player, indices, removeFromContainer, maxWeight,
                                  allowOvercap: true, targetField: "itemListData");
    }

    /// <summary>
    /// v0.7.1: 컨테이너 → player.selfStorage (창고). hard cap (무게 초과 거절).
    /// </summary>
    public Result ContainerToStorage(object player, HashSet<int> indices, bool removeFromContainer, float maxWeight)
    {
        return ContainerToTarget(player, indices, removeFromContainer, maxWeight,
                                  allowOvercap: false, targetField: "selfStorage");
    }

    private Result ContainerToTarget(object player, HashSet<int> indices, bool removeFromContainer,
                                      float maxWeight, bool allowOvercap, string targetField)
    {
        var res = new Result();
        if (CurrentContainerIndex < 0) { res.Reason = "컨테이너 미선택"; return res; }
        if (indices.Count == 0) { res.Reason = "선택된 항목 없음"; return res; }
        try
        {
            string existing  = _repo.LoadItemsJson(CurrentContainerIndex);
            string extracted = ContainerOps.ExtractItemsByIndex(existing, indices);
            var gr = ContainerOps.AddItemsJsonToGame(player, extracted, maxWeight, allowOvercap, targetField);
            res.Succeeded     = gr.Succeeded;
            res.Failed        = gr.Failed;
            res.OverCapWeight = gr.OverCapWeight;
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
            res.Reason = $"ContainerToTarget({targetField}) threw: {ex.Message}";
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
