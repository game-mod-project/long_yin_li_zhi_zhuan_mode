namespace LongYinRoster.Util;

/// <summary>모든 UI 문자열을 한 곳에서 관리. 향후 다국어 시 분리 진입점.</summary>
public static class KoreanStrings
{
    public const string AppTitle              = "Roster Mod";
    public const string HotkeyHint            = "F11 to close";
    public const string SaveCurrentBtn        = "[+] 현재 캐릭터 저장";
    public const string ImportFromFileBtn     = "[F] 파일에서";
    public const string SettingsBtn           = "⚙ 설정";

    public const string SlotEmpty             = "(비어있음)";
    public const string SlotAutoBackup        = "자동백업";
    public const string AutoBackupEmpty       = "자동 백업 (없음)";

    public const string ApplyBtn              = "▼ 현재 플레이어로 덮어쓰기";
    public const string RenameBtn             = "이름변경";
    public const string CommentBtn            = "메모";
    public const string DeleteBtn             = "×삭제";
    public const string RestoreBtn            = "복원";

    public const string ConfirmTitleApply     = "⚠ 플레이어 덮어쓰기 확인";
    public const string ConfirmApplyMain      = "{0}의 데이터로 현재 플레이어를 덮어씁니다.";
    public const string ConfirmApplyPolicy    = "※ 문파/위치/관계는 보존, 캐릭터 본질만 교체";
    public const string AutoBackupCheckbox    = "덮어쓰기 직전 슬롯 00에 자동 저장";
    public const string Cancel                = "취소";
    public const string Apply                 = "덮어쓰기";

    public const string ConfirmTitleDelete    = "슬롯 삭제";
    public const string ConfirmDeleteMain     = "슬롯 {0}을(를) 삭제합니다. 되돌릴 수 없습니다.";
    public const string Delete                = "삭제";

    public const string FilePickerTitle       = "파일에서 가져오기";
    public const string FilePickerImport      = "이 슬롯에서 가져오기";
    public const string FilePickerCurrentLoad = "⚠현재 로드 중";

    public const string ToastCaptured         = "✔ 슬롯 {0}에 캡처되었습니다.";
    public const string ToastApplied          = "✔ 슬롯 {0}의 데이터로 덮어썼습니다. 슬롯 00에 직전 상태 자동저장.";
    public const string ToastAppliedNoBackup  = "✔ 슬롯 {0}의 데이터로 덮어썼습니다.";
    public const string ToastRestored         = "✔ 슬롯 00에서 복원했습니다.";
    public const string ToastDeleted          = "✔ 슬롯 {0}을(를) 삭제했습니다.";
    public const string ToastRenamed          = "✔ 슬롯 {0} 이름을 변경했습니다.";

    public const string ToastErrCapture       = "✘ 캡처 실패: {0}. (자세한 내용: BepInEx 로그)";
    public const string ToastErrApply         = "✘ 덮어쓰기 실패: {0}. (자세한 내용: BepInEx 로그)";
    public const string ToastErrAutoBackup    = "✘ 자동백업에 실패해 덮어쓰기를 취소했습니다. 디스크 공간을 확인하세요.";
    public const string ToastErrSlotsFull     = "✘ 빈 슬롯이 없습니다.";
    public const string ToastErrNoPlayer      = "✘ 플레이어를 찾을 수 없습니다. 게임에 입장한 뒤 시도하세요.";

    public const string EmptyStateNoSlots     = "왼쪽 [+] 버튼으로 첫 캐릭터를 저장하세요";
    public const string EmptyStateNoGame      = "게임에 입장한 뒤 사용 가능합니다";

    public const string GameVersionMismatch   = "이 슬롯은 게임 버전 {0}에서 캡처되었습니다. 현재 버전은 {1}입니다. 그래도 적용하시겠습니까?";
    public const string SchemaUnsupported     = "지원하지 않는 슬롯 포맷 (schemaVersion={0})";
}
