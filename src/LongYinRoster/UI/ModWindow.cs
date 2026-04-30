using System;
using System.Linq;
using LongYinRoster.Slots;
using LongYinRoster.Util;
using UnityEngine;
using Logger = LongYinRoster.Util.Logger;

namespace LongYinRoster.UI;

/// <summary>
/// IMGUI 메인 창. F11 토글. BepInEx 6 IL2CPP-CoreCLR 환경에서 MonoBehaviour
/// 서브클래스는 IntPtr 생성자가 필요하다 (Il2CppInterop 인스턴스화 경로).
///
/// v0.1 범위: 라이브 캡처 (game → slot), 같은 슬롯 덮어쓰기, 슬롯 메타 보기.
/// Apply (slot → game), 자동백업 복원은 v0.2 에서 재설계 — IL2CPP 환경의 game-state
/// reference 필드(장비/무공/포트레이트/문파)와 link 가 깨지는 문제 때문.
/// </summary>
public sealed class ModWindow : MonoBehaviour
{
    public ModWindow(IntPtr handle) : base(handle) { }

    private bool _visible;
    private Rect _rect;
    private static readonly int WindowId = "LongYinRoster".GetHashCode();

    // Harmony Input patch 가 매 frame 참조하는 static singleton.
    private static ModWindow? _instance;

    /// <summary>
    /// 게임의 Input.GetMouseButton* 호출을 차단해야 하는지 여부.
    /// 메인 창이 보이고 mouse 가 창 영역 안 OR 다이얼로그가 떠있으면 true.
    /// </summary>
    public static bool ShouldBlockMouse
    {
        get
        {
            if (_instance == null || !_instance._visible) return false;
            if (_instance._confirm.IsVisible
                || _instance._input.IsVisible
                || _instance._picker.IsVisible) return true;

            var mp      = Input.mousePosition;
            var screenY = Screen.height - mp.y;
            return _instance._rect.Contains(new Vector2(mp.x, screenY));
        }
    }

    public SlotRepository Repo { get; private set; } = null!;

    private readonly SlotListPanel    _list     = new();
    private readonly SlotDetailPanel  _detail   = new();
    private readonly ConfirmDialog    _confirm  = new();
    private readonly InputDialog      _input    = new();
    private readonly FilePickerDialog _picker   = new();

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
        // Apply (slot → game) 와 Restore (slot 0 → game) 는 v0.1 미지원 — 디테일 패널이
        // 버튼을 disabled 표시한다.

        Logger.Info($"ModWindow Awake (slots dir: {slotDir})");
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
                    Summary: summary),
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
                    Summary: summary),
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

    /// <summary>
    /// 진단용 — player reference 의 RefHash, ToString, IL2CPP Pointer 를 모두 기록.
    /// 같은 player 가 다른 Pointer 를 갖거나 frame 마다 RefHash 가 변하면 stale wrapper 가설 단서.
    /// </summary>
    private static void LogPlayerRef(string tag, object? player)
    {
        if (player == null) { Logger.Info($"{tag}: player=NULL"); return; }
        var hash = System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(player);
        Logger.Info($"{tag}: player.RefHash={hash:X8}, ToString={player}");
        // IL2CPP IntPtr (Il2CppInterop pattern)
        try
        {
            var ptrField = player.GetType().GetField("Pointer",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Instance);
            if (ptrField != null)
                Logger.Info($"{tag}: player.Pointer={ptrField.GetValue(player)}");
            else
                Logger.Info($"{tag}: player.Pointer=(field not found)");
        }
        catch (Exception ex) { Logger.Info($"{tag}: Pointer read err: {ex.Message}"); }
    }

    /// <summary>
    /// HeroList[0] 의 reference 를 HeroLocator 우회 fresh fetch.
    /// our player 와 reference equality 비교 — 다르면 stale 가설 확정.
    /// </summary>
    private static object? FreshFetchHero0()
    {
        try
        {
            var ctrlType = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a => { try { return a.GetTypes(); } catch { return Array.Empty<Type>(); } })
                .FirstOrDefault(t => t.Name == "GameDataController" && t.Namespace == null);
            if (ctrlType == null) { Logger.Warn("FreshFetchHero0: GameDataController type not found"); return null; }

            const System.Reflection.BindingFlags S =
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.FlattenHierarchy;
            const System.Reflection.BindingFlags I =
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Instance;

            var inst = ctrlType.GetProperty("Instance", S)?.GetValue(null) ?? ctrlType.GetField("Instance", S)?.GetValue(null);
            if (inst == null) { Logger.Warn("FreshFetchHero0: Instance null"); return null; }

            var saveData = inst.GetType().GetProperty("gameSaveData", I)?.GetValue(inst) ?? inst.GetType().GetField("gameSaveData", I)?.GetValue(inst);
            if (saveData == null) { Logger.Warn("FreshFetchHero0: gameSaveData null"); return null; }

            var heroList = saveData.GetType().GetProperty("HeroList", I)?.GetValue(saveData) ?? saveData.GetType().GetField("HeroList", I)?.GetValue(saveData);
            if (heroList == null) { Logger.Warn("FreshFetchHero0: HeroList null"); return null; }

            // first item via Item indexer or get_Item(int)
            var itemProp = heroList.GetType().GetProperty("Item", I);
            if (itemProp != null) return itemProp.GetValue(heroList, new object[] { 0 });
            var getItem = heroList.GetType().GetMethod("get_Item", I, null, new[] { typeof(int) }, null);
            if (getItem != null) return getItem.Invoke(heroList, new object[] { 0 });
            Logger.Warn("FreshFetchHero0: no indexer");
            return null;
        }
        catch (Exception ex) { Logger.Error($"FreshFetchHero0: {ex}"); return null; }
    }

    private void Update()
    {
        if (Input.GetKeyDown(Config.ToggleHotkey.Value)) Toggle();

        // [F12] HeroDataDump trigger — v0.3 plan Task 1 임시 핸들러. plan Task 21 에서 제거.
        if (Input.GetKeyDown(KeyCode.F12)) Core.HeroDataDump.DumpToLog();

        // [F11 + S] — SetSimpleFields 단독 smoke. detail 보강.
        if (Input.GetKey(KeyCode.F11) && Input.GetKeyDown(KeyCode.S))
        {
            Logger.Info($"smoke S: handler invoked at frame {Time.frameCount}");
            try
            {
                Core.HeroLocator.InvalidateCache();
                var player = Core.HeroLocator.GetPlayer();
                LogPlayerRef("smoke S", player);
                if (player == null) return;

                var fresh = FreshFetchHero0();
                LogPlayerRef("smoke S fresh", fresh);
                Logger.Info($"smoke S ref-equal: {object.ReferenceEquals(player, fresh)}");

                if (!Repo.All[1].IsEmpty)
                {
                    // BEFORE stat read (fame/hp/mana)
                    var t0 = player.GetType();
                    const System.Reflection.BindingFlags F0 =
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic |
                        System.Reflection.BindingFlags.Instance;
                    object? Read(string n) => t0.GetProperty(n, F0)?.GetValue(player) ?? t0.GetField(n, F0)?.GetValue(player);
                    var fameB = Read("fame");
                    var hpB = Read("hp");
                    var manaB = Read("mana");
                    Logger.Info($"smoke S BEFORE: fame={fameB}, hp={hpB}, mana={manaB}");

                    var slot1 = Slots.SlotFile.Read(Repo.PathFor(1));
                    var stripped = Core.PortabilityFilter.StripForApply(slot1.Player);
                    using var doc = System.Text.Json.JsonDocument.Parse(stripped);
                    var res = new Core.ApplyResult();
                    typeof(Core.PinpointPatcher).GetMethod("SetSimpleFields",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!
                        .Invoke(null, new object[] { doc.RootElement, player, res });
                    Logger.Info($"smoke S: applied={res.AppliedFields.Count} skipped={res.SkippedFields.Count} warned={res.WarnedFields.Count}");
                    foreach (var f in res.AppliedFields) Logger.Info($"  applied: {f}");
                    foreach (var f in res.SkippedFields) Logger.Info($"  skipped: {f}");
                    foreach (var f in res.WarnedFields)  Logger.Info($"  warn: {f}");

                    // AFTER stat read (immediate)
                    var fameA = Read("fame");
                    var hpA = Read("hp");
                    var manaA = Read("mana");
                    Logger.Info($"smoke S AFTER: fame={fameA}, hp={hpA}, mana={manaA}");
                    Logger.Info($"smoke S CHANGED: fame={!object.Equals(fameB, fameA)}, hp={!object.Equals(hpB, hpA)}, mana={!object.Equals(manaB, manaA)}");
                }
                else
                {
                    Logger.Warn("smoke S: slot 1 empty — capture first");
                }
            }
            catch (Exception ex) { Logger.Error($"smoke S: {ex}"); }
        }

        // [F11 + 1] 진단 — 가설 A vs B. ChangeHornorLv / ChangeGovernLv (flag 없는 single-arg).
        // 가설 B (IL2CPP invoke 자체 함정) 가설 A (Boolean flag 함정) 결정.
        if (Input.GetKey(KeyCode.F11) && Input.GetKeyDown(KeyCode.Alpha1))
        {
            Logger.Info($"smoke 1: handler invoked at frame {Time.frameCount}");
            try
            {
                Core.HeroLocator.InvalidateCache();
                var player = Core.HeroLocator.GetPlayer();
                LogPlayerRef("smoke 1", player);
                if (player == null) return;

                var fresh = FreshFetchHero0();
                LogPlayerRef("smoke 1 fresh", fresh);
                Logger.Info($"smoke 1 ref-equal: {object.ReferenceEquals(player, fresh)}");

                var t = player.GetType();
                const System.Reflection.BindingFlags F =
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic |
                    System.Reflection.BindingFlags.Instance;

                // current 값 read (before)
                var hornorBefore = t.GetProperty("hornorLv", F)?.GetValue(player) ?? t.GetField("hornorLv", F)?.GetValue(player);
                var governBefore = t.GetProperty("governLv", F)?.GetValue(player) ?? t.GetField("governLv", F)?.GetValue(player);
                Logger.Info($"smoke 1 BEFORE: hornorLv={hornorBefore}, governLv={governBefore}");

                // ChangeHornorLv(+5) / ChangeGovernLv(+3) 호출
                var hornorM = t.GetMethod("ChangeHornorLv", F, null, new[] { typeof(int) }, null);
                var governM = t.GetMethod("ChangeGovernLv", F, null, new[] { typeof(int) }, null);
                if (hornorM == null) Logger.Warn("smoke 1: ChangeHornorLv method NOT FOUND");
                else
                {
                    Logger.Info($"smoke 1: ChangeHornorLv method = {hornorM} (declaringType={hornorM.DeclaringType?.FullName})");
                    hornorM.Invoke(player, new object[] { 5 });
                }
                if (governM == null) Logger.Warn("smoke 1: ChangeGovernLv method NOT FOUND");
                else
                {
                    Logger.Info($"smoke 1: ChangeGovernLv method = {governM}");
                    governM.Invoke(player, new object[] { 3 });
                }

                // current 값 read (after)
                var hornorAfter = t.GetProperty("hornorLv", F)?.GetValue(player) ?? t.GetField("hornorLv", F)?.GetValue(player);
                var governAfter = t.GetProperty("governLv", F)?.GetValue(player) ?? t.GetField("governLv", F)?.GetValue(player);
                Logger.Info($"smoke 1 AFTER:  hornorLv={hornorAfter}, governLv={governAfter}");
                Logger.Info($"smoke 1 RESULT: hornorLv changed={!object.Equals(hornorBefore, hornorAfter)}, governLv changed={!object.Equals(governBefore, governAfter)}");
            }
            catch (Exception ex) { Logger.Error($"smoke 1: {ex}"); }
        }

        // [F11 + 2] 진단 — Boolean padding=true 로 ChangeFame(+1000, true) 호출.
        // fame 변경 → 가설 A (Boolean flag 함정) 지지.
        if (Input.GetKey(KeyCode.F11) && Input.GetKeyDown(KeyCode.Alpha2))
        {
            Logger.Info($"smoke 2: handler invoked at frame {Time.frameCount}");
            try
            {
                Core.HeroLocator.InvalidateCache();
                var player = Core.HeroLocator.GetPlayer();
                LogPlayerRef("smoke 2", player);
                if (player == null) return;

                var fresh = FreshFetchHero0();
                LogPlayerRef("smoke 2 fresh", fresh);
                Logger.Info($"smoke 2 ref-equal: {object.ReferenceEquals(player, fresh)}");

                var t = player.GetType();
                const System.Reflection.BindingFlags F =
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic |
                    System.Reflection.BindingFlags.Instance;

                var fameProp = t.GetProperty("fame", F);
                var fameField = t.GetField("fame", F);
                object? ReadFame() => fameProp?.GetValue(player) ?? fameField?.GetValue(player);
                object? ReadFreshFame() => fresh == null ? null : fameProp?.GetValue(fresh) ?? fameField?.GetValue(fresh);

                var fameBefore = ReadFame();
                var fameFreshBefore = ReadFreshFame();
                Logger.Info($"smoke 2 BEFORE: ourFame={fameBefore}, freshFame={fameFreshBefore}");

                // try padding=false (default — Task 7 의 InvokeMethod 처럼)
                var fameM = t.GetMethod("ChangeFame", F, null, new[] { typeof(float), typeof(bool) }, null);
                if (fameM == null)
                    Logger.Warn("smoke 2: ChangeFame(Single,Boolean) method NOT FOUND");
                else
                {
                    Logger.Info($"smoke 2: ChangeFame method = {fameM} (declaringType={fameM.DeclaringType?.FullName})");

                    // (a) padding=false
                    fameM.Invoke(player, new object[] { 1000f, false });
                    var fameAfterFalse = ReadFame();
                    var fameFreshAfterFalse = ReadFreshFame();
                    Logger.Info($"smoke 2 AFTER false: ourFame={fameAfterFalse}, freshFame={fameFreshAfterFalse}");

                    // (b) padding=true
                    fameM.Invoke(player, new object[] { 1000f, true });
                    var fameAfterTrue = ReadFame();
                    var fameFreshAfterTrue = ReadFreshFame();
                    Logger.Info($"smoke 2 AFTER true:  ourFame={fameAfterTrue}, freshFame={fameFreshAfterTrue}");
                }
            }
            catch (Exception ex) { Logger.Error($"smoke 2: {ex}"); }
        }

        // [F11 + 3] 진단 — type / method DeclaringType inspect (IL2CPP wrapper 여부).
        if (Input.GetKey(KeyCode.F11) && Input.GetKeyDown(KeyCode.Alpha3))
        {
            Logger.Info($"smoke 3: handler invoked at frame {Time.frameCount}");
            try
            {
                Core.HeroLocator.InvalidateCache();
                var player = Core.HeroLocator.GetPlayer();
                LogPlayerRef("smoke 3", player);
                if (player == null) return;

                var fresh = FreshFetchHero0();
                LogPlayerRef("smoke 3 fresh", fresh);
                Logger.Info($"smoke 3 ref-equal: {object.ReferenceEquals(player, fresh)}");

                var t = player.GetType();
                Logger.Info($"smoke 3: player type = {t.AssemblyQualifiedName}");
                Logger.Info($"smoke 3: BaseType = {t.BaseType?.FullName}");
                Logger.Info($"smoke 3: namespace = {t.Namespace}");
                Logger.Info($"smoke 3: assembly = {t.Assembly.FullName}");

                const System.Reflection.BindingFlags F =
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic |
                    System.Reflection.BindingFlags.Instance;
                foreach (var name in new[] { "ChangeHornorLv", "ChangeFame", "SetFavor", "ChangeAttri" })
                {
                    var ms = t.GetMethods(F);
                    foreach (var m in ms)
                    {
                        if (m.Name != name) continue;
                        var pars = string.Join(",", m.GetParameters().Select(p => p.ParameterType.FullName));
                        Logger.Info($"smoke 3: {name} -> {m.DeclaringType?.FullName}.{name}({pars})");
                    }
                }
            }
            catch (Exception ex) { Logger.Error($"smoke 3: {ex}"); }
        }

        // [F11 + T] — RebuildHeroTagData 단독 smoke (Task 11).
        if (Input.GetKey(KeyCode.F11) && Input.GetKeyDown(KeyCode.T))
        {
            Logger.Info($"smoke T: handler invoked at frame {Time.frameCount}");
            try
            {
                Core.HeroLocator.InvalidateCache();
                var player = Core.HeroLocator.GetPlayer();
                LogPlayerRef("smoke T", player);
                if (player == null) return;
                if (Repo.All[1].IsEmpty) { Logger.Warn("smoke T: slot 1 empty"); return; }

                var slot1 = Slots.SlotFile.Read(Repo.PathFor(1));
                var stripped = Core.PortabilityFilter.StripForApply(slot1.Player);
                using var doc = System.Text.Json.JsonDocument.Parse(stripped);
                var res = new Core.ApplyResult();
                typeof(Core.PinpointPatcher).GetMethod("RebuildHeroTagData",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!
                    .Invoke(null, new object[] { doc.RootElement, player, res });
                Logger.Info($"smoke T: applied={res.AppliedFields.Count} skipped={res.SkippedFields.Count} warned={res.WarnedFields.Count}");
                foreach (var f in res.AppliedFields) Logger.Info($"  applied: {f}");
                foreach (var f in res.SkippedFields) Logger.Info($"  skipped: {f}");
                foreach (var f in res.WarnedFields)  Logger.Info($"  warn: {f}");
            }
            catch (Exception ex) { Logger.Error($"smoke T: {ex}"); }
        }

        // [F11 + R] — RefreshSelfState 단독 smoke (Task 12).
        if (Input.GetKey(KeyCode.F11) && Input.GetKeyDown(KeyCode.R))
        {
            Logger.Info($"smoke R: handler invoked at frame {Time.frameCount}");
            try
            {
                Core.HeroLocator.InvalidateCache();
                var player = Core.HeroLocator.GetPlayer();
                LogPlayerRef("smoke R", player);
                if (player == null) return;
                var res = new Core.ApplyResult();
                typeof(Core.PinpointPatcher).GetMethod("RefreshSelfState",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!
                    .Invoke(null, new object[] { player, res });
                Logger.Info($"smoke R: applied={res.AppliedFields.Count} skipped={res.SkippedFields.Count} warned={res.WarnedFields.Count}");
                foreach (var f in res.AppliedFields) Logger.Info($"  applied: {f}");
                foreach (var f in res.SkippedFields) Logger.Info($"  skipped: {f}");
                foreach (var f in res.WarnedFields)  Logger.Info($"  warn: {f}");
            }
            catch (Exception ex) { Logger.Error($"smoke R: {ex}"); }
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
        if (!_visible) return;

        // 다이얼로그 표시 중에는 메인 창 입력을 차단해 modal 효과를 만든다.
        bool modal = _confirm.IsVisible || _input.IsVisible || _picker.IsVisible;
        GUI.enabled = !modal;
        _rect = GUILayout.Window(WindowId, _rect, (GUI.WindowFunction)DrawWindow,
                                 KoreanStrings.AppTitle);
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
        GUILayout.BeginHorizontal();
        _list.Draw(Repo, 240f);
        GUILayout.Space(8);
        _detail.Draw(Repo.All[_list.Selected], inGame: Core.HeroLocator.IsInGame());
        GUILayout.EndHorizontal();
        GUI.DragWindow(new Rect(0, 0, 10000, 24));
    }
}
