namespace LongYinRoster.Util;

/// <summary>모든 UI 문자열을 한 곳에서 관리. 향후 다국어 시 분리 진입점.</summary>
public static class KoreanStrings
{
    public const string AppTitle              = "Roster Mod";

    // v0.7.7 — Item editor
    public const string EditModeBtn                  = "편집";
    public const string EditApplyBtn                 = "적용";
    public const string EditDisclaimer               = "⚠ 편집한 값은 게임 save 후 영속. Apply/Restore 흐름과 별개.";
    public const string EditModeContainerOnly        = "외부 컨테이너 편집 안 됨 (인벤·창고만)";
    public const string EditFieldParseError          = "{0}: {1}";
    public const string EditApplyOk                  = "✔ {0} = {1} 적용";
    public const string EditApplyFailed              = "✘ 변경 실패: {0} ({1})";
    public const string EditFieldNotFoundForCategory = "이 카테고리에 편집 가능한 필드 없음";

    // v0.7.7 — Stat editor (HeroSpeAddData)
    public const string StatEditSection_Base    = "기본 속성 (baseAddData)";
    public const string StatEditSection_Extra   = "추가 속성 (extraAddData)";
    public const string StatEditEditBtn         = "수정";
    public const string StatEditDeleteBtn       = "삭제";
    public const string StatEditAddBtn          = "추가";
    public const string StatEditAddRowLabel     = "추가:";
    public const string StatEditTypeIdxHint     = "※ 0=근력, 1=민첩, 2=지력, 3=의지, 4=체질, 6=내공, 7=경공, 24=의술, 32=요리 ...";
    public const string StatEditApplyOk         = "✔ {0} = {1} 적용";
    public const string StatEditDeleteOk        = "✔ {0} 삭제";
    public const string StatEditAddOk           = "✔ {0} = {1} 추가";
    public const string StatEditFailed          = "✘ {0} 실패";
    public const string StatEditTypeIdxInvalid  = "type idx 범위 0~99";
    public const string StatEditValueInvalid    = "값 형식 잘못됨 (소수)";

    // v0.7.8 — Player editor
    public const string PlayerEditorTitle         = "플레이어 편집";
    public const string PlayerEditorSection_Resource = "▼ 자원 / 최대값";
    public const string PlayerEditorSection_BaseAdd  = "▼ 기본 보정 (baseAddData)";
    public const string PlayerEditorSection_HeroBuff = "▼ 임시 buff (heroBuff)";
    public const string PlayerEditorSection_TotalAdd = "▼ 합산 (totalAddData) ⚠ derived";
    public const string PlayerEditorSection_Tag      = "▼ 천부 (heroTagData)";
    public const string PlayerEditorSection_Kungfu   = "▼ 무공 (kungfuSkills)";
    public const string PlayerEditorQuickFullHeal    = "전체 회복";   // v0.7.8 통합 — 생명·체력·내력 모두
    public const string PlayerEditorQuickRestoreEnergy = "내력/체력 채움 (deprecated, 통합)";
    public const string PlayerEditorQuickCureInjuries = "부상 치료";
    public const string PlayerEditorTotalAddWarn     = "⚠ totalAddData 는 derived — RefreshMaxAttriAndSkill 호출 시 재계산되어 변경 사라질 수 있음";
    public const string PlayerEditorApplyOk          = "✔ {0} = {1} 적용";
    public const string PlayerEditorApplyFailed      = "✘ 변경 실패: {0} ({1})";
    public const string PlayerEditorQuickOk          = "✔ {0} 적용";
    public const string PlayerEditorQuickFailed      = "✘ {0} 실패";
    public const string PlayerEditorTagPending       = "▶ 천부 list editor — Phase 4 (spike 결과 후 활성)";
    public const string PlayerEditorKungfuPending    = "▶ 무공 list editor — Phase 5 (spike 결과 후 활성)";

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

    public const string ConfirmTitleCaptureOverwrite = "슬롯 캡처 덮어쓰기 확인";
    public const string ConfirmCaptureOverwriteMain  = "슬롯 {0}({1})의 기존 데이터를 현재 캐릭터로 덮어씁니다. 되돌릴 수 없습니다.";
    public const string Overwrite                    = "덮어쓰기";

    public const string ConfirmTitleDelete    = "슬롯 삭제";
    public const string ConfirmDeleteMain     = "슬롯 {0}을(를) 삭제합니다. 되돌릴 수 없습니다.";
    public const string Delete                = "삭제";

    public const string InputTitleRename      = "슬롯 이름 변경";
    public const string InputPromptRename     = "슬롯 {0} 의 새 이름을 입력하세요.";
    public const string InputTitleComment     = "슬롯 메모 변경";
    public const string InputPromptComment    = "슬롯 {0} 의 메모를 입력하세요. (비워두면 메모 제거)";
    public const string SaveBtn               = "저장";

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

    // v0.3 신규
    public const string ToastApplyOk                 = "✓ 슬롯 {0} 적용됨 ({1}개 필드, {2}개 미지원)";
    public const string ToastApplyDisabled           = "✘ Apply 가 설정에서 비활성됨";
    public const string ToastErrSlotRead             = "✘ 슬롯 {0} 읽기/파싱 실패: {1}";
    public const string ToastErrApplyAutoRestored    = "✘ 적용 실패: {0}. 자동복원 시도됨 (로그 확인)";
    public const string ToastErrApplyNoBackup        = "✘ 적용 실패: {0}. 자동백업 비활성 — 수동 복구";
    public const string ToastErrEmptySlot            = "✘ 슬롯이 비어 있습니다";
    public const string ToastErrNoBackup             = "✘ 자동백업이 없습니다";
    public const string ConfirmTitleRestore          = "↶ 자동백업 복원 확인";
    public const string ConfirmRestoreMain           = "Apply 직전 상태로 되돌립니다.\n현재 캐릭터 본질이 슬롯 0 의 자동백업으로 교체됩니다.";
    public const string Restore                      = "복원";

    public const string EmptyStateNoSlots     = "왼쪽 [+] 버튼으로 첫 캐릭터를 저장하세요";
    public const string EmptyStateNoGame      = "게임에 입장한 뒤 사용 가능합니다";

    public const string GameVersionMismatch   = "이 슬롯은 게임 버전 {0}에서 캡처되었습니다. 현재 버전은 {1}입니다. 그래도 적용하시겠습니까?";
    public const string SchemaUnsupported     = "지원하지 않는 슬롯 포맷 (schemaVersion={0})";

    // v0.4 — 체크박스 카테고리
    public const string Cat_Stat            = "스탯";
    public const string Cat_Honor           = "명예";
    public const string Cat_TalentTag       = "천부";
    public const string Cat_Skin            = "스킨";
    public const string Cat_SelfHouse       = "자기집 add";
    public const string Cat_Identity        = "정체성";
    public const string Cat_ActiveKungfu    = "무공 active";
    public const string Cat_ItemList        = "인벤토리";
    public const string Cat_SelfStorage     = "창고";
    // v0.5 — 외형 카테고리
    public const string Cat_Appearance      = "외형";
    // v0.5.2 — 무공 list 카테고리
    public const string Cat_KungfuList      = "무공 목록";
    // v0.6.0 — 착용 장비 (nowEquipment — ItemList capability 공유, allItem grid index 참조)
    public const string Cat_NowEquipment    = "착용 장비";
    public const string Cat_DisabledSuffix  = " (v0.5+ 후보)";
    public const string ApplySectionHeader  = "─── Apply 항목 ───";

    // v0.7.0.1 — 컨테이너 안내 메시지
    public const string ToastContainerNotSelected     = "컨테이너를 먼저 [신규] 버튼으로 생성하세요";
    public const string ToastContainerEmptyChecks     = "선택된 항목이 없습니다";
    public const string ToastContainerOpsThrew        = "컨테이너 작업 실패: {0} (BepInEx 로그 확인)";
    public const string ToastContainerNeedGameEnter   = "게임 진입 후 사용 가능합니다";
    public const string ToastContainerCreated         = "신규 컨테이너 #{0} 생성 완료";

    // v0.7.1 — 컨테이너 UX 개선 (무게 기반)
    public const string Lbl_Inventory          = "인벤토리";
    public const string Lbl_Storage            = "창고";
    public const string Lbl_Container          = "컨테이너";
    public const string Lbl_OvercapMarker      = " ⚠ 초과";

    // v0.7.2 D-3 검색·정렬
    public const string Tip_GradeQualityUnavailable = "등급/품질 reflection 미발견 — 정렬 불가 (spike 재시도 권장)";

    public const string BtnInvMove             = "← 인벤으로 이동";
    public const string BtnInvCopy             = "← 인벤으로 복사";
    public const string BtnStoMove             = "← 창고로 이동";
    public const string BtnStoCopy             = "← 창고로 복사";

    public const string ToastInvOk             = "인벤토리로 {0}개 처리";
    public const string ToastInvOvercap        = "인벤토리로 {0}개 처리 ({1:F1}/{2:F1} kg 초과 — 이동속도 저하)";
    public const string ToastStoOk             = "창고로 {0}개 처리";
    public const string ToastStoPartial        = "창고로 {0}개 처리 ({1}개는 무게 초과로 컨테이너에 남김)";
    public const string ToastStoFull           = "창고 무게 한계 — 처리 불가";
}
