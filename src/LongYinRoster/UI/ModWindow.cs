using System;
using LongYinRoster.Slots;
using LongYinRoster.Util;
using UnityEngine;
using Logger = LongYinRoster.Util.Logger;

namespace LongYinRoster.UI;

/// <summary>
/// IMGUI 메인 창. F11 토글. BepInEx 6 IL2CPP-CoreCLR 환경에서 MonoBehaviour
/// 서브클래스는 IntPtr 생성자가 필요하다 (Il2CppInterop 인스턴스화 경로).
/// </summary>
public sealed class ModWindow : MonoBehaviour
{
    public ModWindow(IntPtr handle) : base(handle) { }

    private bool _visible;
    private Rect _rect;
    private static readonly int WindowId = "LongYinRoster".GetHashCode();

    public SlotRepository Repo { get; private set; } = null!;

    private readonly SlotListPanel   _list    = new();
    private readonly SlotDetailPanel _detail  = new();
    private readonly ConfirmDialog   _confirm = new();

    private void Awake()
    {
        _rect = new Rect(Config.WindowX.Value, Config.WindowY.Value,
                         Config.WindowW.Value, Config.WindowH.Value);

        var slotDir = PathProvider.Resolve(Config.SlotDirectory.Value);
        Repo = new SlotRepository(slotDir, Config.MaxSlots.Value);

        // wire panel callbacks
        _list.OnSaveCurrentRequested = RequestCapture;

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

    private void Update()
    {
        if (Input.GetKeyDown(Config.ToggleHotkey.Value)) Toggle();
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

        // confirm dialog 표시 중에는 메인 창 입력을 차단해 modal 효과를 만든다.
        GUI.enabled = !_confirm.IsVisible;
        _rect = GUILayout.Window(WindowId, _rect, (GUI.WindowFunction)DrawWindow,
                                 KoreanStrings.AppTitle);
        GUI.enabled = true;

        _confirm.Draw();

        // persist position/size
        Config.WindowX.Value = _rect.x;
        Config.WindowY.Value = _rect.y;
        Config.WindowW.Value = _rect.width;
        Config.WindowH.Value = _rect.height;
    }

    private void DrawWindow(int id)
    {
        GUILayout.BeginHorizontal();
        _list.Draw(Repo, 240f);
        GUILayout.Space(8);
        _detail.Draw(Repo.All[_list.Selected], inGame: Core.HeroLocator.IsInGame());
        GUILayout.EndHorizontal();
        GUI.DragWindow(new Rect(0, 0, 10000, 24));
    }
}
