using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using LongYinRoster.Containers;
using LongYinRoster.Slots;
using LongYinRoster.Util;
using UnityEngine;
using BepInEx;
using Logger = LongYinRoster.Util.Logger;

namespace LongYinRoster.UI;

/// <summary>
/// IMGUI 메인 창. F11 토글. BepInEx 6 IL2CPP-CoreCLR 환경에서 MonoBehaviour
/// 서브클래스는 IntPtr 생성자가 필요하다 (Il2CppInterop 인스턴스화 경로).
///
/// v0.3: 라이브 캡처 + Apply (slot → game) + 자동백업 복원 모두 지원.
/// PinpointPatcher 7-step pipeline 으로 캐릭터 본질만 교체.
/// </summary>
public sealed class ModWindow : MonoBehaviour
{
    public ModWindow(IntPtr handle) : base(handle) { }

    private bool _visible;
    private Rect _rect;
    private static readonly int WindowId = "LongYinRoster".GetHashCode();

    // Harmony Input patch 가 매 frame 참조하는 static singleton.
    private static ModWindow? _instance;

    // v0.4 — PinpointPatcher.Probe 결과 cache. OnGUI 첫 frame 에서 lazy probe.
    public Core.Capabilities Capabilities { get; private set; } = Core.Capabilities.AllOff();
    private bool _capabilitiesProbed = false;

    /// <summary>
    /// 게임의 Input.GetMouseButton* 호출을 차단해야 하는지 여부.
    /// 메인 창이 보이고 mouse 가 창 영역 안 OR 다이얼로그가 떠있으면 true.
    /// </summary>
    public static bool ShouldBlockMouse
    {
        get
        {
            if (_instance == null) return false;

            var mp      = Input.mousePosition;
            var screenY = Screen.height - mp.y;
            var pos     = new Vector2(mp.x, screenY);

            // v0.7.0 — ModeSelector 메뉴 / ContainerPanel 영역도 차단
            if (_instance._modeSelector.MenuVisible && _instance._modeSelector.WindowRect.Contains(pos)) return true;
            if (_instance._containerPanel.Visible    && _instance._containerPanel.WindowRect.Contains(pos)) return true;

            if (!_instance._visible) return false;
            if (_instance._confirm.IsVisible
                || _instance._input.IsVisible
                || _instance._picker.IsVisible) return true;

            return _instance._rect.Contains(pos);
        }
    }

    public SlotRepository Repo { get; private set; } = null!;

    private readonly SlotListPanel    _list     = new();
    private readonly SlotDetailPanel  _detail   = new();
    private readonly ConfirmDialog    _confirm  = new();
    private readonly InputDialog      _input    = new();
    private readonly FilePickerDialog _picker   = new();

    // v0.7.0 — 모드 선택 + 컨테이너 panel
    private readonly ModeSelector     _modeSelector  = new();
    private readonly ContainerPanel   _containerPanel = new();
    private ContainerRepository?      _containerRepo;
    private ContainerOpsHelper?       _containerOps;

    private void Awake()
    {
        _instance = this;

        _rect = new Rect(Config.WindowX.Value, Config.WindowY.Value,
                         Config.WindowW.Value, Config.WindowH.Value);

        var slotDir = PathProvider.Resolve(Config.SlotDirectory.Value);
        Repo = new SlotRepository(slotDir, Config.MaxSlots.Value);

        // wire panel callbacks
        _list.OnSaveCurrentRequested    = RequestCapture;
        _list.OnImportFromFileRequested = RequestImportFromFile;
        _detail.OnRenameRequested       = RequestRename;
        _detail.OnCommentRequested      = RequestComment;
        _detail.OnDeleteRequested       = RequestDelete;
        _detail.OnApplyRequested        = RequestApply;
        _detail.OnRestoreRequested      = RequestRestore;
        _detail.OnApplySelectionChanged = (slotIndex, sel) =>
        {
            try
            {
                Repo.UpdateApplySelection(slotIndex, sel);
                // Reload 안 함 — selection 만 변경된 거라 file mtime 외 다른 변화 없음 (UI 재그릴 필요 없음 — sel 객체 자체가 mutation 됨)
            }
            catch (Exception ex)
            {
                ToastService.Push($"✘ 슬롯 {slotIndex} selection 저장 실패: {ex.Message}", ToastKind.Error);
                Logger.Error($"UpdateApplySelection(slot={slotIndex}) failed: {ex}");
            }
        };

        // v0.7.0 — 컨테이너 repository + ops helper + UI panel wiring
        var containerDir = Path.Combine(Paths.PluginPath, "LongYinRoster", "Containers");
        _containerRepo = new ContainerRepository(containerDir);
        _containerOps  = new ContainerOpsHelper(_containerRepo);
        _containerPanel.SetRepository(_containerRepo);
        _containerPanel.OnContainerSelected = idx => {
            _containerOps.CurrentContainerIndex = idx;
            RefreshContainerRows();
        };
        _containerPanel.OnInventoryToContainerMove = checks => DoGameToContainer(getList: GetPlayerInventoryList, checks, removeFromGame: true);
        _containerPanel.OnInventoryToContainerCopy = checks => DoGameToContainer(getList: GetPlayerInventoryList, checks, removeFromGame: false);
        _containerPanel.OnStorageToContainerMove   = checks => DoGameToContainer(getList: GetPlayerStorageList,   checks, removeFromGame: true);
        _containerPanel.OnStorageToContainerCopy   = checks => DoGameToContainer(getList: GetPlayerStorageList,   checks, removeFromGame: false);
        _containerPanel.OnContainerToInventoryMove = checks => DoContainerToInventory(checks, removeFromContainer: true);
        _containerPanel.OnContainerToInventoryCopy = checks => DoContainerToInventory(checks, removeFromContainer: false);
        _containerPanel.OnContainerToStorageMove   = checks => DoContainerToStorage  (checks, removeFromContainer: true);
        _containerPanel.OnContainerToStorageCopy   = checks => DoContainerToStorage  (checks, removeFromContainer: false);
        _containerPanel.OnContainerDelete           = checks => {
            var r = _containerOps.DeleteFromContainer(checks);
            _containerPanel.Toast($"삭제: {r.Succeeded}개" + (string.IsNullOrEmpty(r.Reason) ? "" : $" — {r.Reason}"));
            RefreshContainerRows();
        };

        Logger.Info($"ModWindow Awake (slots dir: {slotDir}, containers dir: {containerDir})");
    }

    // ---------------------------------------------------------------- v0.7.0 컨테이너 helper

    private const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

    private object? GetPlayerInventoryList()
    {
        var p = Core.HeroLocator.GetPlayer();
        if (p == null) return null;
        var ild = ReadFieldOrProperty(p, "itemListData");
        return ild != null ? ReadFieldOrProperty(ild, "allItem") : null;
    }

    private object? GetPlayerStorageList()
    {
        var p = Core.HeroLocator.GetPlayer();
        if (p == null) return null;
        var ss = ReadFieldOrProperty(p, "selfStorage");
        return ss != null ? ReadFieldOrProperty(ss, "allItem") : null;
    }

    private static object? ReadFieldOrProperty(object obj, string name)
    {
        var t = obj.GetType();
        var prop = t.GetProperty(name, F);
        if (prop != null) return prop.GetValue(obj);
        var fld = t.GetField(name, F);
        if (fld != null) return fld.GetValue(obj);
        return null;
    }

    private void RefreshAllContainerRows()
    {
        var inv = GetPlayerInventoryList();
        var sto = GetPlayerStorageList();
        var ild = GetPlayerItemListData();
        var ssd = GetPlayerSelfStorage();
        float invMax = Core.ItemListReflector.GetMaxWeight(ild, Config.InventoryMaxWeight.Value);
        float stoMax = Core.ItemListReflector.GetMaxWeight(ssd, Config.StorageMaxWeight.Value);
        // 임시 — Task 7 에서 진짜 raw item source (game list iteration) 로 교체
        _containerPanel.SetInventoryRows(inv != null ? ContainerRowBuilder.FromGameAllItem(inv) : new List<ContainerPanel.ItemRow>(), new List<object>(), invMax);
        _containerPanel.SetStorageRows  (sto != null ? ContainerRowBuilder.FromGameAllItem(sto) : new List<ContainerPanel.ItemRow>(), new List<object>(), stoMax);
        RefreshContainerRows();
    }

    private object? GetPlayerItemListData()
    {
        var p = Core.HeroLocator.GetPlayer();
        return p != null ? ReadFieldOrProperty(p, "itemListData") : null;
    }

    private object? GetPlayerSelfStorage()
    {
        var p = Core.HeroLocator.GetPlayer();
        return p != null ? ReadFieldOrProperty(p, "selfStorage") : null;
    }

    private void RefreshContainerRows()
    {
        if (_containerOps == null || _containerRepo == null) return;
        if (_containerOps.CurrentContainerIndex > 0)
            _containerPanel.SetContainerRows(ContainerRowBuilder.FromJsonArray(_containerRepo.LoadItemsJson(_containerOps.CurrentContainerIndex)), new List<object>());
        else
            _containerPanel.SetContainerRows(new List<ContainerPanel.ItemRow>(), new List<object>());
    }

    private void DoGameToContainer(Func<object?> getList, HashSet<int> checks, bool removeFromGame)
    {
        if (_containerOps == null) return;
        var lst = getList();
        if (lst == null) { _containerPanel.Toast("게임 진입 후 사용 가능"); return; }

        // 이동 시 착용 장비 자동 unequip
        if (removeFromGame)
        {
            var p = Core.HeroLocator.GetPlayer();
            if (p != null)
            {
                MethodInfo? unequipM = null;
                foreach (var m in p.GetType().GetMethods(F))
                    if (m.Name == "UnequipItem" && m.GetParameters().Length == 3) { unequipM = m; break; }
                int unequipped = 0;
                foreach (int idx in checks)
                {
                    var item = Core.IL2CppListOps.Get(lst, idx);
                    if (item == null) continue;
                    var ed = ReadFieldOrProperty(item, "equipmentData");
                    if (ed == null) continue;
                    var eq = ReadFieldOrProperty(ed, "equiped");
                    if (eq is bool b && b)
                    {
                        try { unequipM?.Invoke(p, new object[] { item, false, false }); unequipped++; } catch { }
                    }
                }
                if (unequipped > 0) _containerPanel.Toast($"착용 중 {unequipped}개 자동 해제 후 이동");
            }
        }

        var r = _containerOps.GameToContainer(lst, checks, removeFromGame);
        _containerPanel.Toast($"{(removeFromGame ? "이동" : "복사")}: {r.Succeeded}개" + (r.Failed > 0 ? $" / {r.Failed}개 실패" : "") + (string.IsNullOrEmpty(r.Reason) ? "" : $" — {r.Reason}"));
        RefreshAllContainerRows();
    }

    private void DoContainerToInventory(HashSet<int> checks, bool removeFromContainer)
    {
        if (_containerOps == null) return;
        var p = Core.HeroLocator.GetPlayer();
        if (p == null) { _containerPanel.Toast(KoreanStrings.ToastContainerNeedGameEnter); return; }
        float maxW = Core.ItemListReflector.GetMaxWeight(GetPlayerItemListData(), Config.InventoryMaxWeight.Value);
        var r = _containerOps.ContainerToInventory(p, checks, removeFromContainer, maxW);
        if (!string.IsNullOrEmpty(r.Reason) && r.Succeeded == 0)
        {
            _containerPanel.Toast(r.Reason);
        }
        else if (r.OverCapWeight > 0f)
        {
            // 인벤 over-cap 발생: 현재 무게 = inventory wrapper.weight 합산 (refresh 전 값)
            float finalW = 0f;
            var inv = GetPlayerInventoryList();
            if (inv != null)
            {
                int n = Core.IL2CppListOps.Count(inv);
                for (int i = 0; i < n; i++)
                {
                    var item = Core.IL2CppListOps.Get(inv, i);
                    if (item == null) continue;
                    var w = ReadFieldOrProperty(item, "weight");
                    if (w is float wf) finalW += wf;
                }
            }
            _containerPanel.Toast(string.Format(KoreanStrings.ToastInvOvercap, r.Succeeded, finalW, maxW));
        }
        else
        {
            _containerPanel.Toast(string.Format(KoreanStrings.ToastInvOk, r.Succeeded));
        }
        RefreshAllContainerRows();
    }

    private void DoContainerToStorage(HashSet<int> checks, bool removeFromContainer)
    {
        if (_containerOps == null) return;
        var p = Core.HeroLocator.GetPlayer();
        if (p == null) { _containerPanel.Toast(KoreanStrings.ToastContainerNeedGameEnter); return; }
        float maxW = Core.ItemListReflector.GetMaxWeight(GetPlayerSelfStorage(), Config.StorageMaxWeight.Value);
        var r = _containerOps.ContainerToStorage(p, checks, removeFromContainer, maxW);
        if (!string.IsNullOrEmpty(r.Reason) && r.Succeeded == 0 && r.Failed == 0)
        {
            _containerPanel.Toast(r.Reason);
        }
        else if (r.Succeeded == 0 && r.Failed > 0)
        {
            _containerPanel.Toast(KoreanStrings.ToastStoFull);
        }
        else if (r.Failed > 0)
        {
            _containerPanel.Toast(string.Format(KoreanStrings.ToastStoPartial, r.Succeeded, r.Failed));
        }
        else
        {
            _containerPanel.Toast(string.Format(KoreanStrings.ToastStoOk, r.Succeeded));
        }
        RefreshAllContainerRows();
    }

    /// <summary>
    /// [+] 버튼 핸들러. 사용자가 선택한 슬롯이 차있으면 확인 다이얼로그를 띄운다.
    /// 선택이 자동백업(0) 이거나 OOR 이면 가장 낮은 빈 사용자 슬롯으로 fallback.
    /// </summary>
    private void RequestCapture()
    {
        int slot = _list.Selected;
        if (slot <= 0 || slot >= Repo.All.Count)
            slot = Repo.AllocateNextFree();

        if (slot < 0)
        {
            ToastService.Push(KoreanStrings.ToastErrSlotsFull, ToastKind.Error);
            return;
        }

        var entry = Repo.All[slot];
        if (entry.IsEmpty)
        {
            DoCapture(slot);
        }
        else
        {
            var existingLabel = entry.Meta?.UserLabel ?? "";
            _confirm.Show(
                title: KoreanStrings.ConfirmTitleCaptureOverwrite,
                body: string.Format(KoreanStrings.ConfirmCaptureOverwriteMain, slot, existingLabel),
                confirmLabel: KoreanStrings.Overwrite,
                onConfirm: () => DoCapture(slot));
        }
    }

    private void DoCapture(int slot)
    {
        var player = Core.HeroLocator.GetPlayer();
        if (player == null)
        {
            ToastService.Push(KoreanStrings.ToastErrNoPlayer, ToastKind.Error);
            return;
        }

        try
        {
            var json    = Core.SerializerService.Serialize(player);
            var summary = SlotMetadata.FromPlayerJson(json);

            var label = $"{summary.HeroName} {summary.HeroNickName} {DateTime.Now:MM-dd HH:mm}";
            var payload = new SlotPayload
            {
                Meta = new SlotPayloadMeta(
                    SchemaVersion: SlotFile.CurrentSchemaVersion,
                    ModVersion: Plugin.VERSION,
                    SlotIndex: slot,
                    UserLabel: label,
                    UserComment: "",
                    CaptureSource: "live",
                    CaptureSourceDetail: "",
                    CapturedAt: DateTime.Now,
                    GameSaveVersion: "1.0.0 f8.2",
                    GameSaveDetail: "",
                    Summary: summary,
                    ApplySelection: Core.ApplySelection.V03Default()),
                Player = json,
            };
            Repo.Write(slot, payload);
            Repo.Reload();
            ToastService.Push(string.Format(KoreanStrings.ToastCaptured, slot), ToastKind.Success);
        }
        catch (Exception ex)
        {
            ToastService.Push(string.Format(KoreanStrings.ToastErrCapture, ex.Message), ToastKind.Error);
            Logger.Error($"DoCapture(slot={slot}) failed: {ex}");
        }
    }

    // ---------------------------------------------------------------- D 단계 핸들러

    private void RequestRename(int slot)
    {
        if (slot < 1 || slot >= Repo.All.Count) return;
        var entry = Repo.All[slot];
        if (entry.IsEmpty || entry.Meta == null) return;

        _input.Show(
            title: KoreanStrings.InputTitleRename,
            prompt: string.Format(KoreanStrings.InputPromptRename, slot),
            initialValue: entry.Meta.UserLabel,
            confirmLabel: KoreanStrings.SaveBtn,
            onConfirm: newLabel =>
            {
                try
                {
                    Repo.Rename(slot, newLabel);
                    Repo.Reload();
                    ToastService.Push(string.Format(KoreanStrings.ToastRenamed, slot), ToastKind.Success);
                }
                catch (Exception ex)
                {
                    ToastService.Push($"✘ 이름 변경 실패: {ex.Message}", ToastKind.Error);
                    Logger.Error($"Rename(slot={slot}) failed: {ex}");
                }
            });
    }

    private void RequestComment(int slot)
    {
        if (slot < 1 || slot >= Repo.All.Count) return;
        var entry = Repo.All[slot];
        if (entry.IsEmpty || entry.Meta == null) return;

        _input.Show(
            title: KoreanStrings.InputTitleComment,
            prompt: string.Format(KoreanStrings.InputPromptComment, slot),
            initialValue: entry.Meta.UserComment,
            confirmLabel: KoreanStrings.SaveBtn,
            onConfirm: newComment =>
            {
                try
                {
                    Repo.UpdateComment(slot, newComment);
                    Repo.Reload();
                    ToastService.Push($"✔ 슬롯 {slot} 메모를 저장했습니다.", ToastKind.Success);
                }
                catch (Exception ex)
                {
                    ToastService.Push($"✘ 메모 저장 실패: {ex.Message}", ToastKind.Error);
                    Logger.Error($"UpdateComment(slot={slot}) failed: {ex}");
                }
            });
    }

    private void RequestDelete(int slot)
    {
        if (slot < 1 || slot >= Repo.All.Count) return;
        var entry = Repo.All[slot];
        if (entry.IsEmpty) return;

        var label = entry.Meta?.UserLabel ?? "";
        _confirm.Show(
            title: KoreanStrings.ConfirmTitleDelete,
            body: string.Format(KoreanStrings.ConfirmDeleteMain, $"{slot} ({label})"),
            confirmLabel: KoreanStrings.Delete,
            onConfirm: () =>
            {
                try
                {
                    Repo.Delete(slot);
                    Repo.Reload();
                    ToastService.Push(string.Format(KoreanStrings.ToastDeleted, slot), ToastKind.Success);
                }
                catch (Exception ex)
                {
                    ToastService.Push($"✘ 슬롯 삭제 실패: {ex.Message}", ToastKind.Error);
                    Logger.Error($"Delete(slot={slot}) failed: {ex}");
                }
            });
    }

    // ---------------------------------------------------------------- v0.3 Apply / Restore

    private void RequestApply(int slot)
    {
        if (slot < 1 || slot >= Repo.All.Count)
        {
            ToastService.Push(KoreanStrings.ToastErrEmptySlot, ToastKind.Error);
            return;
        }
        var entry = Repo.All[slot];
        if (entry.IsEmpty || entry.Meta == null)
        {
            ToastService.Push(KoreanStrings.ToastErrEmptySlot, ToastKind.Error);
            return;
        }

        var label = entry.Meta.UserLabel;
        var body  = string.Format(KoreanStrings.ConfirmApplyMain, $"슬롯 {slot} · {label}")
                  + "\n" + KoreanStrings.ConfirmApplyPolicy;
        _confirm.Show(
            title: KoreanStrings.ConfirmTitleApply,
            body:  body,
            confirmLabel: KoreanStrings.Apply,
            onConfirm: () => DoApply(slot, Config.AutoBackupBeforeApply.Value));
    }

    private void DoApply(int slot, bool doAutoBackup)
    {
        var player = Core.HeroLocator.GetPlayer();
        if (player == null)
        {
            ToastService.Push(KoreanStrings.ToastErrNoPlayer, ToastKind.Error);
            return;
        }
        if (!Config.AllowApplyToGame.Value)
        {
            ToastService.Push(KoreanStrings.ToastApplyDisabled, ToastKind.Error);
            return;
        }

        // 1. 자동백업 (slot 0)
        if (doAutoBackup)
        {
            try
            {
                var nowJson    = Core.SerializerService.Serialize(player);
                var nowSummary = SlotMetadata.FromPlayerJson(nowJson);
                var backupLabel = $"{nowSummary.HeroName} {nowSummary.HeroNickName} (Apply 직전 자동백업)";
                var payload = new SlotPayload
                {
                    Meta = new SlotPayloadMeta(
                        SchemaVersion: SlotFile.CurrentSchemaVersion,
                        ModVersion: Plugin.VERSION,
                        SlotIndex: 0,
                        UserLabel: backupLabel,
                        UserComment: $"slot {slot} 적용 직전",
                        CaptureSource: "auto",
                        CaptureSourceDetail: $"pre-apply-from-slot-{slot}",
                        CapturedAt: DateTime.Now,
                        GameSaveVersion: "1.0.0 f8.2",
                        GameSaveDetail: "",
                        Summary: nowSummary,
                        ApplySelection: Core.ApplySelection.V03Default()),
                    Player = nowJson,
                };
                Repo.WriteAutoBackup(payload);
            }
            catch (Exception ex)
            {
                ToastService.Push(string.Format(KoreanStrings.ToastErrAutoBackup), ToastKind.Error);
                Logger.Error($"DoApply auto-backup failed: {ex}");
                return;
            }
        }

        // 2. 슬롯 데이터 read + strip
        SlotPayload loaded;
        string stripped;
        try
        {
            loaded   = SlotFile.Read(Repo.PathFor(slot));
            stripped = Core.PortabilityFilter.StripForApply(loaded.Player);
        }
        catch (Exception ex)
        {
            ToastService.Push(string.Format(KoreanStrings.ToastErrSlotRead, slot, ex.Message), ToastKind.Error);
            Logger.Error($"DoApply slot read failed (slot={slot}): {ex}");
            return;
        }

        // 3. PinpointPatcher 호출
        Core.ApplyResult res;
        try
        {
            // v0.4: slot 0 (Restore) → RestoreAll, others → 슬롯 메타의 ApplySelection
            var selection = (slot == 0)
                ? Core.ApplySelection.RestoreAll()
                : loaded.Meta.ApplySelection;
            res = Core.PinpointPatcher.Apply(stripped, player, selection);
        }
        catch (Exception ex)
        {
            Logger.Error($"PinpointPatcher.Apply top-level throw: {ex}");
            if (doAutoBackup) AttemptAutoRestore(player);
            ToastService.Push(string.Format(
                doAutoBackup ? KoreanStrings.ToastErrApplyAutoRestored
                             : KoreanStrings.ToastErrApplyNoBackup, ex.Message), ToastKind.Error);
            return;
        }

        // 4. fatal 결과 처리
        if (res.HasFatalError)
        {
            string firstErr = res.StepErrors.Count > 0 ? res.StepErrors[0].Message : "fatal step";
            if (doAutoBackup) AttemptAutoRestore(player);
            ToastService.Push(string.Format(
                doAutoBackup ? KoreanStrings.ToastErrApplyAutoRestored
                             : KoreanStrings.ToastErrApplyNoBackup, firstErr), ToastKind.Error);
            return;
        }

        // 5. 성공 (warn/skip 포함)
        Repo.Reload();
        ToastService.Push(string.Format(KoreanStrings.ToastApplyOk,
                                        slot, res.AppliedFields.Count, res.SkippedFields.Count),
                          ToastKind.Success);
        Logger.Info($"Apply OK slot={slot} applied={res.AppliedFields.Count} " +
                    $"skipped={res.SkippedFields.Count} warned={res.WarnedFields.Count}");
    }

    private void AttemptAutoRestore(object player)
    {
        try
        {
            var slot0 = SlotFile.Read(Repo.PathFor(0));
            var stripped = Core.PortabilityFilter.StripForApply(slot0.Player);
            var res = Core.PinpointPatcher.Apply(stripped, player, Core.ApplySelection.RestoreAll());
            if (res.HasFatalError)
                Logger.Error("Auto-restore also failed — game state may be inconsistent");
            else
                Logger.Info($"Auto-restore OK applied={res.AppliedFields.Count}");
        }
        catch (Exception ex)
        {
            Logger.Error($"Auto-restore threw: {ex}");
        }
    }

    private void RequestRestore(int _slotArg)
    {
        // SlotDetailPanel 의 OnRestoreRequested 가 entry.Index 를 넘기지만 Restore 는 항상 슬롯 0.
        if (Repo.All[0].IsEmpty)
        {
            ToastService.Push(KoreanStrings.ToastErrNoBackup, ToastKind.Error);
            return;
        }
        var label = Repo.All[0].Meta?.UserLabel ?? KoreanStrings.SlotAutoBackup;
        _confirm.Show(
            title: KoreanStrings.ConfirmTitleRestore,
            body:  KoreanStrings.ConfirmRestoreMain + $"\n원본: {label}",
            confirmLabel: KoreanStrings.Restore,
            onConfirm: () => DoApply(slot: 0, doAutoBackup: false));
    }

    /// <summary>
    /// [F] 파일에서 핸들러 — 게임 자체 SaveSlot 0~10 list → 사용자 선택 → mod 슬롯에 import.
    /// </summary>
    private void RequestImportFromFile()
    {
        try
        {
            var slots = SaveFileScanner.ListAvailable();
            _picker.Show(slots, DoImportFromFile);
        }
        catch (Exception ex)
        {
            ToastService.Push($"✘ 게임 저장 슬롯 스캔 실패: {ex.Message}", ToastKind.Error);
            Logger.Error($"RequestImportFromFile failed: {ex}");
        }
    }

    private void DoImportFromFile(int gameSaveSlotIndex)
    {
        // mod 슬롯 결정 — 현재 _list.Selected 가 빈 사용자 슬롯이면 그것, 아니면 다음 빈 슬롯.
        int modSlot = _list.Selected;
        if (modSlot <= 0 || modSlot >= Repo.All.Count || !Repo.All[modSlot].IsEmpty)
            modSlot = Repo.AllocateNextFree();

        if (modSlot < 0)
        {
            ToastService.Push(KoreanStrings.ToastErrSlotsFull, ToastKind.Error);
            return;
        }

        try
        {
            var json    = SaveFileScanner.LoadHero0(gameSaveSlotIndex);
            var summary = SlotMetadata.FromPlayerJson(json);

            var label = $"{summary.HeroName} {summary.HeroNickName} (SaveSlot{gameSaveSlotIndex})";
            var payload = new SlotPayload
            {
                Meta = new SlotPayloadMeta(
                    SchemaVersion: SlotFile.CurrentSchemaVersion,
                    ModVersion: Plugin.VERSION,
                    SlotIndex: modSlot,
                    UserLabel: label,
                    UserComment: "",
                    CaptureSource: "file",
                    CaptureSourceDetail: $"SaveSlot{gameSaveSlotIndex}/Hero",
                    CapturedAt: DateTime.Now,
                    GameSaveVersion: "1.0.0 f8.2",
                    GameSaveDetail: "",
                    Summary: summary,
                    ApplySelection: Core.ApplySelection.V03Default()),
                Player = json,
            };
            Repo.Write(modSlot, payload);
            Repo.Reload();
            ToastService.Push(string.Format(KoreanStrings.ToastCaptured, modSlot), ToastKind.Success);
            Logger.Info($"slot {modSlot} imported from SaveSlot{gameSaveSlotIndex}");
        }
        catch (Exception ex)
        {
            ToastService.Push(string.Format(KoreanStrings.ToastErrCapture, ex.Message), ToastKind.Error);
            Logger.Error($"DoImportFromFile(SaveSlot{gameSaveSlotIndex} → mod {modSlot}) failed: {ex}");
        }
    }

    private ModeSelector.Mode _lastSeenMode = ModeSelector.Mode.None;
    private bool _lastVisible = false;
    private bool _lastContainerVisible = false;

    private void Update()
    {
        // v0.7.0 — F11 단독: 모드 선택 메뉴 / F11+1: 캐릭터 / F11+2: 컨테이너
        if (HotkeyMap.MainKeyPressedAlone()) _modeSelector.Toggle();
        if (HotkeyMap.CharacterShortcut())
        {
            _modeSelector.SetMode(ModeSelector.Mode.Character);
            // SetMode 가 transition 발생시킴 — 아래 transition handler 가 처리
        }
        if (HotkeyMap.ContainerShortcut())
        {
            _modeSelector.SetMode(ModeSelector.Mode.Container);
        }

        // ModeSelector 의 mode 변경을 transition 으로 처리 — 매 프레임 polling 안 함.
        if (_modeSelector.CurrentMode != _lastSeenMode)
        {
            _lastSeenMode = _modeSelector.CurrentMode;
            if (_modeSelector.CurrentMode == ModeSelector.Mode.Character)
            {
                if (!_visible) Toggle();
                _containerPanel.Visible = false;
            }
            else if (_modeSelector.CurrentMode == ModeSelector.Mode.Container)
            {
                _containerPanel.Visible = true;
                if (_visible) Toggle();
                RefreshAllContainerRows();
            }
        }

        // X 닫기 검출 — visible 이 true → false 로 변경 시 mode 리셋해서 재오픈 허용.
        if (_lastVisible && !_visible)
        {
            _modeSelector.SetMode(ModeSelector.Mode.None);
            _lastSeenMode = ModeSelector.Mode.None;
        }
        if (_lastContainerVisible && !_containerPanel.Visible)
        {
            _modeSelector.SetMode(ModeSelector.Mode.None);
            _lastSeenMode = ModeSelector.Mode.None;
        }
        _lastVisible = _visible;
        _lastContainerVisible = _containerPanel.Visible;

        // v0.5.3 Spike — F12 trigger, mod 창 visible 동안 1-3 으로 Mode 직접 설정 (release 전 cleanup)
        if (Input.GetKeyDown(KeyCode.F12)) Core.Probes.ProbeSubData.Run();   // v0.7.4 spike — Task 9 에서 ProbeRunner.Trigger() 복원
        if (_visible)
        {
            if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1))
                Core.Probes.ProbeRunner.SetMode(Core.Probes.ProbeItemList.Mode.Step1);
            else if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2))
                Core.Probes.ProbeRunner.SetMode(Core.Probes.ProbeItemList.Mode.Step2);
            else if (Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3))
                Core.Probes.ProbeRunner.SetMode(Core.Probes.ProbeItemList.Mode.Step3);
            else if (Input.GetKeyDown(KeyCode.Alpha4) || Input.GetKeyDown(KeyCode.Keypad4))
                Core.Probes.ProbeRunner.SetMode(Core.Probes.ProbeItemList.Mode.Step4);
        }

        if (_visible && Config.PauseGameWhileOpen.Value && Time.timeScale != 0f)
            Time.timeScale = 0f;
    }

    public void Toggle()
    {
        _visible = !_visible;
        if (_visible) Repo.Reload();
        if (Config.PauseGameWhileOpen.Value)
            Time.timeScale = _visible ? 0f : 1f;
        Logger.Info($"ModWindow toggle → visible={_visible}");
    }

    private void OnGUI()
    {
        ToastService.Draw();

        // v0.7.0 — ModeSelector + ContainerPanel (캐릭터 panel 과 독립)
        _modeSelector.OnGUI();
        _containerPanel.OnGUI();

        if (!_visible) return;

        // v0.4 — lazy Probe at first OnGUI (player must be loaded)
        if (!_capabilitiesProbed)
        {
            Capabilities = Core.PinpointPatcher.Probe();
            _capabilitiesProbed = true;
        }

        // 다이얼로그 표시 중에는 메인 창 입력을 차단해 modal 효과를 만든다.
        bool modal = _confirm.IsVisible || _input.IsVisible || _picker.IsVisible;
        GUI.enabled = !modal;
        _rect = GUILayout.Window(WindowId, _rect, (GUI.WindowFunction)DrawWindow, "");
        GUI.enabled = true;

        _confirm.Draw();
        _input.Draw();
        _picker.Draw();

        // persist position/size
        Config.WindowX.Value = _rect.x;
        Config.WindowY.Value = _rect.y;
        Config.WindowW.Value = _rect.width;
        Config.WindowH.Value = _rect.height;
    }

    private void DrawWindow(int id)
    {
        DialogStyle.FillBackground(_rect.width, _rect.height);
        DialogStyle.DrawHeader(_rect.width, KoreanStrings.AppTitle);

        // v0.7.0 — 닫기 X 버튼 (창 우상단) — 헤더 높이 28 안에 배치
        if (GUI.Button(new Rect(_rect.width - 28, 4, 22, 20), "X"))
            Toggle();

        GUILayout.Space(DialogStyle.HeaderHeight);
        GUILayout.BeginHorizontal();
        _list.Draw(Repo, 240f);
        GUILayout.Space(8);
        _detail.Draw(Repo.All[_list.Selected], inGame: Core.HeroLocator.IsInGame(), cap: Capabilities);
        GUILayout.EndHorizontal();
        // DragWindow 영역 — 헤더 전체 (X 버튼 제외)
        GUI.DragWindow(new Rect(0, 0, _rect.width - 32, DialogStyle.HeaderHeight));
    }
}
