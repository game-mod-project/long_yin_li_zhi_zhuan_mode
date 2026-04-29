# HeroData / Hero-related Manager Method Dump

**Source**: `BepInEx/LogOutput.log`, `[F12]` 핸들러 (v0.3 plan Task 2)
**Date**: 2026-04-29
**Game version**: 1.0.0 f8.2
**heroType**: `HeroData, Assembly-CSharp, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null` (전역 namespace)

이 문서는 게임 binary 의 reflection enumeration 결과로, v0.3 PinpointPatcher 의 매트릭스 (`spec §7.2`) / method 매핑 (`spec §7.2.1`) 의 evidence base 다. dump 는 `HeroDataDump.cs` 가 `BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance` 로 enumerate 한 결과 — IL2CPP strip 후의 잔재 method 까지 포함.

**카운트 요약** (sort -u 후):
- methods: 759 (중복 제거 후 — overload 다수)
- properties: 177
- fields: 2
- managers: 2

---

## HeroData self — methods (759, alphabetical)

```
AreaData GetArea()
AudioClip GetHeroDieSound()
AudioClip GetHeroHurtSound()
AudioClip GetHeroLittleTalkSound()
AudioClip GetHeroMeetSound(String)
AudioClip GetHeroRecoverSound()
AudioClip GetHeroShoutSound()
BattlePrepareSpellData get_battlePrepareSpellData()
BigMapPos GetBigMapPos()
BigMapPos get_bigMapPos()
Boolean AttackSelfTeam()
Boolean AttackSkillSlotUnlocked(Int32)
Boolean BattleControlable()
Boolean CanMove()
Boolean CanPlayerMeet()
Boolean CanSetName()
Boolean CanUseAttackSkill()
Boolean CanUseSkill(KungfuSkillLvData)
Boolean Equals(Object)
Boolean FullState()
Boolean HaveBrother(Int32)
Boolean HaveForceFunction(Int32)
Boolean HaveFriend(Int32)
Boolean HaveHater(Int32)
Boolean HaveHobby(ItemData)
Boolean HaveLover()
Boolean HaveMission(String)
Boolean HavePrelover(Int32)
Boolean HaveRelationBetterThanFriend(Int32,Boolean,Boolean)
Boolean HaveResource(Int32,Single)
Boolean HaveResource(List`1)
Boolean HaveResource(ResourceData)
Boolean HaveSetName()
Boolean HaveSpeInteractWithNPC()
Boolean HaveStudent(Int32)
Boolean HaveTag(Int32)
Boolean HaveTeacher()
Boolean HaveTeacherStudentRelation(Int32)
Boolean IsPlayerSameForce()
Boolean IsSpeTeammate()
Boolean ItemControlable()
Boolean ItemExchangeable()
Boolean ItemLockable()
Boolean LoadFaceCode(String)
Boolean MeetForceJobRequire(Int32)
Boolean MissionKeepInTeam()
Boolean NoLoyal()
Boolean PlayerLeadForce()
Boolean SameForce(HeroData)
Boolean StuffStoppable()
Boolean UseSpeSkeleton()
Boolean _RandomAttriAndSkill_b__521_0(Int32)
Boolean _RandomAttriAndSkill_b__521_1(Int32)
Boolean get_HeroIconDirty()
Boolean get_WasCollected()
Boolean get_autoLeaveTeamDestroy()
Boolean get_bodyGuard()
Boolean get_bookWriteWorking()
Boolean get_dailyAIManaged()
Boolean get_dead()
Boolean get_equipLock()
Boolean get_fightForceEnter()
Boolean get_fightProtectTarget()
Boolean get_fightSpeControl()
Boolean get_haveMeet()
Boolean get_heroBuffDirty()
Boolean get_heroDetailDirty()
Boolean get_hide()
Boolean get_inHill()
Boolean get_inMountain()
Boolean get_inPrison()
Boolean get_inSafeArea()
Boolean get_inTeam()
Boolean get_inWater()
Boolean get_interestingStar()
Boolean get_isFemale()
Boolean get_isGovern()
Boolean get_isHornord()
Boolean get_isLeader()
Boolean get_isRandomEnemy()
Boolean get_isSummon()
Boolean get_isTempHero()
Boolean get_loveAble()
Boolean get_needRemove()
Boolean get_outsideForce()
Boolean get_playerSetSkin()
Boolean get_recruitAble()
Boolean get_recruitByPlayer()
Boolean get_rest()
Boolean get_skillLock()
Boolean get_speHero()
Boolean get_summonControlable()
Boolean get_tempPlotHero()
Boolean get_wantRemove()
Color GetSkinColorByDark()
ForceData GetForce(Boolean)
HeroAIData get_heroAIData()
HeroAIData get_heroAIDataArriveTargetRecord()
HeroAISettingData get_heroAISettingData()
HeroData GetForceLeader()
HeroData GetTeacher()
HeroEquipmentData get_nowEquipment()
HeroFaceData get_faceData()
HeroSpeAddData get_baseAddData()
HeroSpeAddData get_heroBuff()
HeroSpeAddData get_totalAddData()
HeroTagData FindTag(Int32)
Int32 GetAISettingFocus(AISettingType)
Int32 GetAISettingPriorityLv(AISettingType)
Int32 GetAreaID(Boolean)
Int32 GetAskJoinTeamCostFavor()
Int32 GetAskJoinTeamMaxLv()
Int32 GetAskJoinTeamNeedFavor()
Int32 GetBadFameFineMoney()
Int32 GetBaseMoveRange()
Int32 GetBetrayForceBadFame()
Int32 GetBetrayForceBadTime()
Int32 GetBodyGuardNum()
Int32 GetBountyMissionNum()
Int32 GetBountyPirce()
Int32 GetDefaultSkinID()
Int32 GetEquipmentWeightLv()
Int32 GetFightTime(HeroData)
Int32 GetFullRecoverTime(Single)
Int32 GetHashCode()
Int32 GetHeroPermanentTagNum()
Int32 GetHeroSoundAgeID()
Int32 GetHitMoveRange()
Int32 GetHornorChangeMaxArea()
Int32 GetMaxBountyMissionNum()
Int32 GetMaxDoctorTime()
Int32 GetMaxStudent()
Int32 GetMaxTagNum()
Int32 GetMaxTeamMate()
Int32 GetMissMeetingReduceContribution()
Int32 GetMoveRange()
Int32 GetPreferWeaponType()
Int32 GetRecruitCost(Boolean,Single)
Int32 GetRecruitUnlockCost()
Int32 GetSelfCureExternalInjuryTime(Single,Single)
Int32 GetSelfCureInternalInjuryTime(Single,Single)
Int32 GetSelfCurePoisonInjuryTime(Single,Single)
Int32 GetUpgradeForceLvNeedContribution()
Int32 GetUpgradeForceLvNeedContribution(Single)
Int32 GetUpgradeForceLvNeedSkillNum()
Int32 GetWeaponResearchWeaponType()
Int32 MaxSelfCureTime()
Int32 get_Lover()
Int32 get_Teacher()
Int32 get_age()
Int32 get_atAreaID()
Int32 get_autoLeaveTeamDay()
Int32 get_belongForceID()
Int32 get_branchLeaderAreaID()
Int32 get_changeSkinCd()
Int32 get_cureType()
Int32 get_defaultSkinID()
Int32 get_dodgeSkillSaveRecord()
Int32 get_forceJobCD()
Int32 get_forceJobID()
Int32 get_forceJobType()
Int32 get_generation()
Int32 get_governLv()
Int32 get_heroForceLv()
Int32 get_heroID()
Int32 get_heroIconDirtyCount()
Int32 get_hornorLv()
Int32 get_horseArmorSaveRecord()
Int32 get_horseSaveRecord()
Int32 get_internalSkillSaveRecord()
Int32 get_missionNumCount()
Int32 get_nature()
Int32 get_nowActiveSkill()
Int32 get_playerBecomeTeacherTime()
Int32 get_plotNumCount()
Int32 get_population()
Int32 get_salary()
Int32 get_selfCureTime()
Int32 get_servantForceID()
Int32 get_setSkinID()
Int32 get_setSkinLv()
Int32 get_skillForceID()
Int32 get_skinID()
Int32 get_skinLv()
Int32 get_studyNewSkillSetting()
Int32 get_summonID()
Int32 get_summonMoveRange()
Int32 get_talent()
Int32 get_teamLeader()
Int32 get_uniqueSkillSaveRecord()
IntPtr get_ObjectClass()
IntPtr get_Pointer()
ItemData FindRandomItem(Int32,Int32,Boolean,Int32)
ItemData FindSameBook(ItemData)
ItemData get_horse()
ItemData get_horseArmor()
ItemListData get_itemListData()
ItemListData get_selfStorage()
KungfuSkillLvData FindRandomSkill(Int32,HeroData)
KungfuSkillLvData FindSkill(Int32)
KungfuSkillLvData GetNowActiveSkill()
KungfuSkillLvData GetSkill(KungfuSkillLvData,Boolean,Boolean)
KungfuSkillLvData get_dodgeSkill()
KungfuSkillLvData get_internalSkill()
KungfuSkillLvData get_uniqueSkill()
List`1 get_Brothers()
List`1 get_Friends()
List`1 get_Haters()
List`1 get_PreLovers()
List`1 get_Relatives()
List`1 get_Students()
List`1 get_attackSkillSaveRecord()
List`1 get_attackSkills()
List`1 get_autoSetting()
List`1 get_baseAttri()
List`1 get_baseFightSkill()
List`1 get_baseLivingSkill()
List`1 get_expLivingSkill()
List`1 get_goodKungfuSkillName()
List`1 get_heroTagData()
List`1 get_hobby()
List`1 get_kungfuSkillFocus()
List`1 get_kungfuSkills()
List`1 get_livingSkillFocus()
List`1 get_maxAttri()
List`1 get_maxFightSkill()
List`1 get_maxLivingSkill()
List`1 get_missions()
List`1 get_recordLog()
List`1 get_skillCount()
List`1 get_skillMaxPracticeExpData()
List`1 get_teamMates()
List`1 get_totalAttri()
List`1 get_totalFightSkill()
List`1 get_totalLivingSkill()
MissionData FindMission(String)
MissionData get_forceMission()
Object Clone()
Object MemberwiseClone()
PartPostureData get_partPosture()
PlayerInteractionTimeData get_playerInteractionTimeData()
Single ChangeExternalInjury(Single,Boolean,Boolean,Boolean)
Single ChangeInternalInjury(Single,Boolean,Boolean,Boolean)
Single ChangePoisonInjury(Single,Boolean,Boolean,Boolean)
Single Favor(Boolean)
Single GetAttriRate(Int32)
Single GetBadFameRate(HeroData)
Single GetBaseAttriNum(BaseAttriType)
Single GetBookExpRate(KungfuSkillLvData)
Single GetDamageResist()
Single GetDebateScore()
Single GetExploreStepRate()
Single GetExtraHpRecoverValueRate()
Single GetExtraMaxHp()
Single GetExtraMaxMana()
Single GetFameForceLv()
Single GetFameRate()
Single GetFavorRate(Single)
Single GetFavorValueRate(Boolean)
Single GetFightExpRate(KungfuSkillLvData)
Single GetFightScore(Boolean)
Single GetFinalTravelSpeed()
Single GetForceBookStorageExpRate(Int32)
Single GetForceJobEffectSkillNum()
Single GetForceJobEffectSkillNum(Int32,Int32)
Single GetForceJobSpeAddResult(Int32)
Single GetForceStorageDiscount(ItemListData)
Single GetGameDifficultyExpRate()
Single GetGameDifficultyFameRate()
Single GetGovernExtraFameRate()
Single GetGovernReduceBadFameRate()
Single GetGovernUpgradeCost()
Single GetHeroItemLv(Boolean)
Single GetHeroSoundAgePitch()
Single GetHeroSoundVoiceAgePitch()
Single GetHornorAddFavorRate()
Single GetHornorReduceBadFameRate()
Single GetHornorUpgradeCost()
Single GetHorseScore()
Single GetHorseTravelSpeed()
Single GetHorseTravelSpeed(Boolean,Boolean)
Single GetHpPercent()
Single GetIdentifyKnowledge()
Single GetItemFavorValue(ItemData,Single)
Single GetLivingSkillExpMax(Int32)
Single GetLoyalExpRate()
Single GetLoyalWorkRate()
Single GetManaPercent()
Single GetMaxAttri(Int32)
Single GetMaxBuyValue(Single)
Single GetMaxFavor(Single)
Single GetMaxFightSkill(Int32)
Single GetMaxLivingSkill(Int32)
Single GetMaxSkillNum(Int32)
Single GetMedResist()
Single GetMeetNeedFame()
Single GetNatureFavorRate()
Single GetNextForceLvFame()
Single GetPostureCurePostureRate()
Single GetPostureCurePower()
Single GetPostureValue()
Single GetPostureValue(Single)
Single GetPowerPercent()
Single GetRecoverRate(Single)
Single GetRestCurePostureRate()
Single GetRestCurePower()
Single GetRestCureRate()
Single GetRestValue()
Single GetSeeRange()
Single GetSelfCureExternalInjury(Single)
Single GetSelfCureExternalInjurySkill(Single)
Single GetSelfCureInjury(Single,Single)
Single GetSelfCureInternalInjury(Single)
Single GetSelfCureInternalInjurySkill(Single)
Single GetSelfCurePoisonInjury(Single)
Single GetSelfCurePoisonInjurySkill(Single)
Single GetSelfCurePostureRate()
Single GetSelfCurePower()
Single GetSelfCureRate()
Single GetSelfCureValue()
Single GetSkillPowerChargeSpeed(FightSkillType)
Single GetSkillRareLvExpRate(Int32)
Single GetStartFavor(HeroData)
Single GetTerrainChangeTravelSpeed()
Single GetTotalAttir()
Single GetTotalFightSkill()
Single GetTotalInjury()
Single GetTotalLivingSkill()
Single GetTotalTagPoint()
Single GetTradeValueRate(Boolean)
Single GetTradeValueRate(Boolean,Boolean)
Single GetTravelSpeed()
Single GetTravelSpeed(Boolean,Boolean)
Single GetUseItemValue(ItemData,Boolean)
Single GetWeatherChangeTravelSpeed()
Single GetWeighChangeTravelSpeed()
Single GetWoundResist()
Single ManageGetEquipPoison(ItemData,Boolean,Single,Single,Single)
Single ManageGetItemPoison(ItemData,Boolean,Single,Boolean)
Single OutsideForceExtraContributionRate(Int32)
Single SelfForceContrituion()
Single UsePoisonRate()
Single get_armor()
Single get_badFame()
Single get_chaos()
Single get_evil()
Single get_externalInjury()
Single get_fame()
Single get_favor()
Single get_fightScore()
Single get_forceContribution()
Single get_governContribution()
Single get_heroStrengthLv()
Single get_heroTagPoint()
Single get_hp()
Single get_internalInjury()
Single get_lastFightContribution()
Single get_lastMonthContribution()
Single get_lastYearContribution()
Single get_loyal()
Single get_mana()
Single get_manageAiHour()
Single get_maxMana()
Single get_maxPower()
Single get_maxhp()
Single get_medResist()
Single get_poisonInjury()
Single get_power()
Single get_realMaxHp()
Single get_realMaxMana()
Single get_realMaxPower()
Single get_restCureTotalRate()
Single get_selfHouseTotalAdd()
Single get_skinColorDark()
Single get_summonLv()
Single get_thisMonthContribution()
Single get_thisYearContribution()
Single get_voicePitch()
SkeletonAnimation GenerateHeroSkeleton(GameObject,Vector3)
SkeletonAnimation GenerateHeroSkeleton(SkeletonAnimation)
SkeletonGraphic GetSkeletonGraphic(Transform)
SkillMaxPracticeExpData GetSkillMaxPracticeExp(Int32)
String AtAreaName()
String GenerateFaceCode()
String GetFullSetName(Boolean)
String GetHeroForceLvDescribe(Boolean)
String GetHeroForceLvDescribeSimplify()
String GetHeroGovernName()
String GetHeroHornorName()
String GetHeroName(Boolean)
String GetHeroWeaponAttackAnim()
String GetHobbyDescribe()
String GetMeditationTopic()
String GetQuickDetail(Boolean,Boolean,Boolean,Boolean)
String GetRecordLog()
String GetRelationShipText(Int32,Boolean,Boolean)
String GetSkeletonHorseIdleAnim()
String GetSkeletonHorseRunAnim()
String GetSkeletonHorseWalkAnim()
String GetSkinName(Boolean,Int32,Int32)
String GetUpgradeForceLvNeedText()
String HeroFamilyName()
String HeroName(Boolean)
String Name(Boolean)
String ToString()
String get_heroFamilyName()
String get_heroName()
String get_heroNickName()
String get_settingName()
String get_summonSourceHero()
T Cast()
T TryCast()
T Unbox()
Type GetIl2CppType()
Type GetType()
Void AddBrother(Int32,Boolean)
Void AddBuff(Int32,Single)
Void AddFriend(Int32,Boolean)
Void AddHater(Int32,Boolean)
Void AddLog(String)
Void AddPrelover(Int32,Boolean)
Void AddSkillBookExp(Single,KungfuSkillLvData,Boolean)
Void AddSkillFightExp(Single,KungfuSkillLvData,Boolean)
Void AddSkillMaxPracticeExp(SkillMaxPracticeExpData)
Void AddStudent(Int32,Boolean)
Void AddTag(Int32,Single,String,Boolean,Boolean)
Void AddTempTag(HeroTagDataBase,Int32,Boolean)
Void AutoChangeLoyal()
Void AutoCureSelfInjury()
Void AutoFightQuickChangeState(Single,Boolean)
Void AutoGetFightExp()
Void AutoGetFightExp(Single)
Void AutoManageEquipPoison()
Void BattleChangeSkillFightExp(Single,KungfuSkillLvData,Boolean)
Void ChangeAttri(Int32,Single,Boolean,Boolean)
Void ChangeAttriAndSkill(HeroSpeAddDataType,Single)
Void ChangeBadFame(Single,Boolean,HeroData,Boolean)
Void ChangeFame(Single,Boolean)
Void ChangeFavor(Single,Boolean,Single,Single,Boolean)
Void ChangeFightSkill(Int32,Single,Boolean,Boolean)
Void ChangeForceContribution(Single,Boolean,Int32)
Void ChangeGovernContribution(Single,Boolean)
Void ChangeGovernLv(Int32)
Void ChangeHeroForceLv(Int32,Boolean)
Void ChangeHeroMissionResult(MissionData,MissionTargetType,String,Int32,Single)
Void ChangeHeroMissionResult(MissionTargetType,Int32,Single)
Void ChangeHeroMissionResult(MissionTargetType,Single)
Void ChangeHeroMissionResult(MissionTargetType,String,Int32,Single)
Void ChangeHeroMissionResult(MissionTargetType,String,Single)
Void ChangeHornorLv(Int32)
Void ChangeHp(Single,Boolean,Boolean,Boolean,Boolean)
Void ChangeLivingSkill(Int32,Single,Boolean,Boolean)
Void ChangeLivingSkillExp(Int32,Single,Boolean)
Void ChangeLoyal(Single,Boolean)
Void ChangeMana(Single,Boolean,Boolean,Boolean)
Void ChangeMaxAttri(Int32,Int32,Boolean)
Void ChangeMaxFightSkill(Int32,Int32,Boolean)
Void ChangeMaxHp(Single,Boolean)
Void ChangeMaxLivingSkill(Int32,Int32,Boolean)
Void ChangeMaxMana(Single,Boolean)
Void ChangeMaxPower(Single,Boolean)
Void ChangeMoney(Int32,Boolean)
Void ChangePower(Single,Boolean)
Void ChangeRandomInjury(Single,Boolean,Boolean)
Void ChangeResource(Int32,Single,Boolean,Boolean,Int32,Single)
Void ChangeResource(List`1,Boolean,Boolean)
Void ChangeSelfHouseTotalAdd(Single)
Void ChangeSkillPower(SkillChangePowerType,Single)
Void ChangeTagPoint(Single,Boolean)
Void CheckForceJobDetailDirty(EquipmentData)
Void CheckForceJobDetailDirty(HeroSpeAddData)
Void CheckForceJobDetailDirty(Int32)
Void CheckHeroDetailDirty(Boolean)
Void CheckHeroFameForceLv()
Void CheckOutForceContribution()
Void CheckPlayerMakeLoverUnhappy()
Void CheckSkillUpgrade(KungfuSkillLvData)
Void ClearAllTempTag()
Void ClearContributionRecord()
Void ClearForceJob()
Void CostResource(Int32,Single)
Void CostResource(List`1)
Void CostResource(ResourceData)
Void CosumeMedFood(ItemData,Boolean,HeroData,Single)
Void CreateGCHandle(IntPtr)
Void DeadToAlive()
Void DisUnderstandTag(Int32)
Void EquipHorse(ItemData,Boolean)
Void EquipItem(ItemData,Boolean,Boolean)
Void EquipSkill(KungfuSkillLvData,Boolean)
Void FieldGetter(String,String,Object&)
Void FieldSetter(String,String,Object)
Void FightReset()
Void FightSettingReset()
Void Finalize()
Void FullRecover(Boolean)
Void GetBounty(Int32,HeroData,Boolean)
Void GetDebateSpeBuff(Int32)
Void GetFoodSpeBuff(ItemData)
Void GetItem(ItemData,Boolean)
Void GetItem(ItemData,Boolean,Boolean,Int32,Boolean)
Void GetItem(ItemListData,Boolean,Boolean,Int32)
Void GetWineSpeBuff(ItemData)
Void GoInPrison()
Void GoOutPrison()
Void JoinForce(Int32,Int32,Int32,Boolean,Boolean)
Void JoinForceServant(Int32)
Void LeaveForce(Boolean,Boolean)
Void LeaveServantForce()
Void LoseAllItem()
Void LoseAllSkill()
Void LoseItem(ItemData,Boolean)
Void LoseSkill(KungfuSkillLvData)
Void ManageAIInPrison(Boolean)
Void ManageHeroForceLvChangeMaxAttri(Int32)
Void ManageHeroHorseMove(Single)
Void ManageHeroHorseMove(Single,Boolean)
Void ManageHeroHorseRest(Single)
Void ManageHeroHorseTime(Single)
Void ManagePoisonEquipment(ItemData)
Void ManagePoisonItem(ItemData)
Void ManageTagTime()
Void OnDeserializedMethod(StreamingContext)
Void OnSerializingMethod(StreamingContext)
Void PlayHeroSound(AudioClip,Single,Single)
Void RandomAttriAndSkill()
Void RandomBigMapMovePos()
Void RandomFaceData(Boolean)
Void RecoverState()
Void RefreshHeroSalaryAndPopulation()
Void RefreshHeroSkeleton(SkeletonAnimation)
Void RefreshHorseState(Boolean)
Void RefreshMaxAttriAndSkill()
Void RefreshSkeletonHorse(SkeletonAnimation)
Void RemoveAllDebuff()
Void RemoveAllPrelover(Boolean)
Void RemoveAllStudent(Boolean)
Void RemoveBrother(Int32,Boolean)
Void RemoveFriend(Int32,Boolean)
Void RemoveHater(Int32,Boolean)
Void RemoveLover(Boolean)
Void RemovePrelover(Int32,Boolean)
Void RemoveRelative(Int32,Boolean)
Void RemoveStudent(Int32,Boolean)
Void RemoveTag(Int32,Boolean)
Void RemoveTag(String,Boolean)
Void RemoveTeacher(Boolean)
Void ResetAI()
Void ResetAutoSetting()
Void ResetDefaultSkin()
Void ResetHeroSkillID()
Void ResetLoyal()
Void SetFavor(Single,Boolean)
Void SetForce(Int32,Int32)
Void SetHeroAIData(HeroAIData)
Void SetHeroFavorUI(GameObject,Boolean)
Void SetHeroForceLv(Int32)
Void SetHeroID(Int32)
Void SetHeroMeet(Boolean,Single)
Void SetHeroWeapon(SkeletonAnimation)
Void SetHeroWeapon(SkeletonAnimation,String)
Void SetHpBar(GameObject)
Void SetLover(Int32,Boolean)
Void SetMeetFavor(Boolean,Single)
Void SetMpBar(GameObject)
Void SetNeedRemove()
Void SetNowActiveSkill(KungfuSkillLvData)
Void SetPowerBar(GameObject)
Void SetRandomEnemyBadFame()
Void SetSkeletonFaceSlot(SkeletonAnimation,Int32)
Void SetSkeletonGraphic(Transform,Int32,Int32)
Void SetSkeletonGraphicFaceSlot(SkeletonGraphic,Int32,Int32)
Void SetSkeletonGraphicSkinColor(SkeletonGraphic)
Void SetSkeletonSkinColor(SkeletonAnimation)
Void SetSkillWeapon(SkeletonAnimation,String)
Void SetSkin(Int32,Int32)
Void TryIdentifyAllItem(Boolean)
Void UnderstandTag(Int32,Boolean)
Void UnderstandTag(String,Boolean)
Void UnequipHorse(ItemData,Boolean,Boolean)
Void UnequipItem(ItemData,Boolean,Boolean)
Void UnequipSkill(KungfuSkillLvData,Boolean)
Void UpgradeSkill(KungfuSkillLvData)
Void UpgradeTempTag(String,HeroSpeAddDataType,Single,Int32)
Void UseMedFood(ItemData,Boolean,Boolean,HeroData)
Void set_Brothers(List`1)
Void set_Friends(List`1)
Void set_Haters(List`1)
Void set_HeroIconDirty(Boolean)
Void set_Lover(Int32)
Void set_PreLovers(List`1)
Void set_Relatives(List`1)
Void set_Students(List`1)
Void set_Teacher(Int32)
Void set_age(Int32)
Void set_armor(Single)
Void set_atAreaID(Int32)
Void set_attackSkillSaveRecord(List`1)
Void set_attackSkills(List`1)
Void set_autoLeaveTeamDay(Int32)
Void set_autoLeaveTeamDestroy(Boolean)
Void set_autoSetting(List`1)
Void set_badFame(Single)
Void set_baseAddData(HeroSpeAddData)
Void set_baseAttri(List`1)
Void set_baseFightSkill(List`1)
Void set_baseLivingSkill(List`1)
Void set_battlePrepareSpellData(BattlePrepareSpellData)
Void set_belongForceID(Int32)
Void set_bigMapPos(BigMapPos)
Void set_bodyGuard(Boolean)
Void set_bookWriteWorking(Boolean)
Void set_branchLeaderAreaID(Int32)
Void set_changeSkinCd(Int32)
Void set_chaos(Single)
Void set_cureType(Int32)
Void set_dailyAIManaged(Boolean)
Void set_dead(Boolean)
Void set_defaultSkinID(Int32)
Void set_dodgeSkill(KungfuSkillLvData)
Void set_dodgeSkillSaveRecord(Int32)
Void set_equipLock(Boolean)
Void set_evil(Single)
Void set_expLivingSkill(List`1)
Void set_externalInjury(Single)
Void set_faceData(HeroFaceData)
Void set_fame(Single)
Void set_favor(Single)
Void set_fightForceEnter(Boolean)
Void set_fightProtectTarget(Boolean)
Void set_fightScore(Single)
Void set_fightSpeControl(Boolean)
Void set_forceContribution(Single)
Void set_forceJobCD(Int32)
Void set_forceJobID(Int32)
Void set_forceJobType(Int32)
Void set_forceMission(MissionData)
Void set_generation(Int32)
Void set_goodKungfuSkillName(List`1)
Void set_governContribution(Single)
Void set_governLv(Int32)
Void set_haveMeet(Boolean)
Void set_heroAIData(HeroAIData)
Void set_heroAIDataArriveTargetRecord(HeroAIData)
Void set_heroAISettingData(HeroAISettingData)
Void set_heroBuff(HeroSpeAddData)
Void set_heroBuffDirty(Boolean)
Void set_heroDetailDirty(Boolean)
Void set_heroFamilyName(String)
Void set_heroForceLv(Int32)
Void set_heroID(Int32)
Void set_heroIconDirtyCount(Int32)
Void set_heroName(String)
Void set_heroNickName(String)
Void set_heroStrengthLv(Single)
Void set_heroTagData(List`1)
Void set_heroTagPoint(Single)
Void set_hide(Boolean)
Void set_hobby(List`1)
Void set_hornorLv(Int32)
Void set_horse(ItemData)
Void set_horseArmor(ItemData)
Void set_horseArmorSaveRecord(Int32)
Void set_horseSaveRecord(Int32)
Void set_hp(Single)
Void set_inHill(Boolean)
Void set_inMountain(Boolean)
Void set_inPrison(Boolean)
Void set_inSafeArea(Boolean)
Void set_inTeam(Boolean)
Void set_inWater(Boolean)
Void set_interestingStar(Boolean)
Void set_internalInjury(Single)
Void set_internalSkill(KungfuSkillLvData)
Void set_internalSkillSaveRecord(Int32)
Void set_isFemale(Boolean)
Void set_isGovern(Boolean)
Void set_isHornord(Boolean)
Void set_isLeader(Boolean)
Void set_isRandomEnemy(Boolean)
Void set_isSummon(Boolean)
Void set_isTempHero(Boolean)
Void set_itemListData(ItemListData)
Void set_kungfuSkillFocus(List`1)
Void set_kungfuSkills(List`1)
Void set_lastFightContribution(Single)
Void set_lastMonthContribution(Single)
Void set_lastYearContribution(Single)
Void set_livingSkillFocus(List`1)
Void set_loveAble(Boolean)
Void set_loyal(Single)
Void set_mana(Single)
Void set_manageAiHour(Single)
Void set_maxAttri(List`1)
Void set_maxFightSkill(List`1)
Void set_maxLivingSkill(List`1)
Void set_maxMana(Single)
Void set_maxPower(Single)
Void set_maxhp(Single)
Void set_medResist(Single)
Void set_missionNumCount(Int32)
Void set_missions(List`1)
Void set_nature(Int32)
Void set_needRemove(Boolean)
Void set_nowActiveSkill(Int32)
Void set_nowEquipment(HeroEquipmentData)
Void set_outsideForce(Boolean)
Void set_partPosture(PartPostureData)
Void set_playerBecomeTeacherTime(Int32)
Void set_playerInteractionTimeData(PlayerInteractionTimeData)
Void set_playerSetSkin(Boolean)
Void set_plotNumCount(Int32)
Void set_poisonInjury(Single)
Void set_population(Int32)
Void set_power(Single)
Void set_realMaxHp(Single)
Void set_realMaxMana(Single)
Void set_realMaxPower(Single)
Void set_recordLog(List`1)
Void set_recruitAble(Boolean)
Void set_recruitByPlayer(Boolean)
Void set_rest(Boolean)
Void set_restCureTotalRate(Single)
Void set_salary(Int32)
Void set_selfCureTime(Int32)
Void set_selfHouseTotalAdd(Single)
Void set_selfStorage(ItemListData)
Void set_servantForceID(Int32)
Void set_setSkinID(Int32)
Void set_setSkinLv(Int32)
Void set_settingName(String)
Void set_skillCount(List`1)
Void set_skillForceID(Int32)
Void set_skillLock(Boolean)
Void set_skillMaxPracticeExpData(List`1)
Void set_skinColorDark(Single)
Void set_skinID(Int32)
Void set_skinLv(Int32)
Void set_speHero(Boolean)
Void set_studyNewSkillSetting(Int32)
Void set_summonControlable(Boolean)
Void set_summonID(Int32)
Void set_summonLv(Single)
Void set_summonMoveRange(Int32)
Void set_summonSourceHero(String)
Void set_talent(Int32)
Void set_teamLeader(Int32)
Void set_teamMates(List`1)
Void set_tempPlotHero(Boolean)
Void set_thisMonthContribution(Single)
Void set_thisYearContribution(Single)
Void set_totalAddData(HeroSpeAddData)
Void set_totalAttri(List`1)
Void set_totalFightSkill(List`1)
Void set_totalLivingSkill(List`1)
Void set_uniqueSkill(KungfuSkillLvData)
Void set_uniqueSkillSaveRecord(Int32)
Void set_voicePitch(Single)
Void set_wantRemove(Boolean)
```

---

## HeroData self — properties (177, alphabetical)

각 라인은 `Type Name { get=true/false, set=true/false }` 형식. (참고: `set=True` 가 game-self method 가 아닌 property setter — \§2.2 N3 정책에 따라 직접 reflection setter 호출은 거부.)

```
BattlePrepareSpellData battlePrepareSpellData { get=True, set=True }
BigMapPos bigMapPos { get=True, set=True }
Boolean HeroIconDirty { get=True, set=True }
Boolean WasCollected { get=True, set=False }
Boolean autoLeaveTeamDestroy { get=True, set=True }
Boolean bodyGuard { get=True, set=True }
Boolean bookWriteWorking { get=True, set=True }
Boolean dailyAIManaged { get=True, set=True }
Boolean dead { get=True, set=True }
Boolean equipLock { get=True, set=True }
Boolean fightForceEnter { get=True, set=True }
Boolean fightProtectTarget { get=True, set=True }
Boolean fightSpeControl { get=True, set=True }
Boolean haveMeet { get=True, set=True }
Boolean heroBuffDirty { get=True, set=True }
Boolean heroDetailDirty { get=True, set=True }
Boolean hide { get=True, set=True }
Boolean inHill { get=True, set=True }
Boolean inMountain { get=True, set=True }
Boolean inPrison { get=True, set=True }
Boolean inSafeArea { get=True, set=True }
Boolean inTeam { get=True, set=True }
Boolean inWater { get=True, set=True }
Boolean interestingStar { get=True, set=True }
Boolean isFemale { get=True, set=True }
Boolean isGovern { get=True, set=True }
Boolean isHornord { get=True, set=True }
Boolean isLeader { get=True, set=True }
Boolean isRandomEnemy { get=True, set=True }
Boolean isSummon { get=True, set=True }
Boolean isTempHero { get=True, set=True }
Boolean loveAble { get=True, set=True }
Boolean needRemove { get=True, set=True }
Boolean outsideForce { get=True, set=True }
Boolean playerSetSkin { get=True, set=True }
Boolean recruitAble { get=True, set=True }
Boolean recruitByPlayer { get=True, set=True }
Boolean rest { get=True, set=True }
Boolean skillLock { get=True, set=True }
Boolean speHero { get=True, set=True }
Boolean summonControlable { get=True, set=True }
Boolean tempPlotHero { get=True, set=True }
Boolean wantRemove { get=True, set=True }
HeroAIData heroAIData { get=True, set=True }
HeroAIData heroAIDataArriveTargetRecord { get=True, set=True }
HeroAISettingData heroAISettingData { get=True, set=True }
HeroEquipmentData nowEquipment { get=True, set=True }
HeroFaceData faceData { get=True, set=True }
HeroSpeAddData baseAddData { get=True, set=True }
HeroSpeAddData heroBuff { get=True, set=True }
HeroSpeAddData totalAddData { get=True, set=True }
Int32 Lover { get=True, set=True }
Int32 Teacher { get=True, set=True }
Int32 age { get=True, set=True }
Int32 atAreaID { get=True, set=True }
Int32 autoLeaveTeamDay { get=True, set=True }
Int32 belongForceID { get=True, set=True }
Int32 branchLeaderAreaID { get=True, set=True }
Int32 changeSkinCd { get=True, set=True }
Int32 cureType { get=True, set=True }
Int32 defaultSkinID { get=True, set=True }
Int32 dodgeSkillSaveRecord { get=True, set=True }
Int32 forceJobCD { get=True, set=True }
Int32 forceJobID { get=True, set=True }
Int32 forceJobType { get=True, set=True }
Int32 generation { get=True, set=True }
Int32 governLv { get=True, set=True }
Int32 heroForceLv { get=True, set=True }
Int32 heroID { get=True, set=True }
Int32 heroIconDirtyCount { get=True, set=True }
Int32 hornorLv { get=True, set=True }
Int32 horseArmorSaveRecord { get=True, set=True }
Int32 horseSaveRecord { get=True, set=True }
Int32 internalSkillSaveRecord { get=True, set=True }
Int32 missionNumCount { get=True, set=True }
Int32 nature { get=True, set=True }
Int32 nowActiveSkill { get=True, set=True }
Int32 playerBecomeTeacherTime { get=True, set=True }
Int32 plotNumCount { get=True, set=True }
Int32 population { get=True, set=True }
Int32 salary { get=True, set=True }
Int32 selfCureTime { get=True, set=True }
Int32 servantForceID { get=True, set=True }
Int32 setSkinID { get=True, set=True }
Int32 setSkinLv { get=True, set=True }
Int32 skillForceID { get=True, set=True }
Int32 skinID { get=True, set=True }
Int32 skinLv { get=True, set=True }
Int32 studyNewSkillSetting { get=True, set=True }
Int32 summonID { get=True, set=True }
Int32 summonMoveRange { get=True, set=True }
Int32 talent { get=True, set=True }
Int32 teamLeader { get=True, set=True }
Int32 uniqueSkillSaveRecord { get=True, set=True }
IntPtr ObjectClass { get=True, set=False }
IntPtr Pointer { get=True, set=False }
ItemData horse { get=True, set=True }
ItemData horseArmor { get=True, set=True }
ItemListData itemListData { get=True, set=True }
ItemListData selfStorage { get=True, set=True }
KungfuSkillLvData dodgeSkill { get=True, set=True }
KungfuSkillLvData internalSkill { get=True, set=True }
KungfuSkillLvData uniqueSkill { get=True, set=True }
List`1 Brothers { get=True, set=True }
List`1 Friends { get=True, set=True }
List`1 Haters { get=True, set=True }
List`1 PreLovers { get=True, set=True }
List`1 Relatives { get=True, set=True }
List`1 Students { get=True, set=True }
List`1 attackSkillSaveRecord { get=True, set=True }
List`1 attackSkills { get=True, set=True }
List`1 autoSetting { get=True, set=True }
List`1 baseAttri { get=True, set=True }
List`1 baseFightSkill { get=True, set=True }
List`1 baseLivingSkill { get=True, set=True }
List`1 expLivingSkill { get=True, set=True }
List`1 goodKungfuSkillName { get=True, set=True }
List`1 heroTagData { get=True, set=True }
List`1 hobby { get=True, set=True }
List`1 kungfuSkillFocus { get=True, set=True }
List`1 kungfuSkills { get=True, set=True }
List`1 livingSkillFocus { get=True, set=True }
List`1 maxAttri { get=True, set=True }
List`1 maxFightSkill { get=True, set=True }
List`1 maxLivingSkill { get=True, set=True }
List`1 missions { get=True, set=True }
List`1 recordLog { get=True, set=True }
List`1 skillCount { get=True, set=True }
List`1 skillMaxPracticeExpData { get=True, set=True }
List`1 teamMates { get=True, set=True }
List`1 totalAttri { get=True, set=True }
List`1 totalFightSkill { get=True, set=True }
List`1 totalLivingSkill { get=True, set=True }
MissionData forceMission { get=True, set=True }
PartPostureData partPosture { get=True, set=True }
PlayerInteractionTimeData playerInteractionTimeData { get=True, set=True }
Single armor { get=True, set=True }
Single badFame { get=True, set=True }
Single chaos { get=True, set=True }
Single evil { get=True, set=True }
Single externalInjury { get=True, set=True }
Single fame { get=True, set=True }
Single favor { get=True, set=True }
Single fightScore { get=True, set=True }
Single forceContribution { get=True, set=True }
Single governContribution { get=True, set=True }
Single heroStrengthLv { get=True, set=True }
Single heroTagPoint { get=True, set=True }
Single hp { get=True, set=True }
Single internalInjury { get=True, set=True }
Single lastFightContribution { get=True, set=True }
Single lastMonthContribution { get=True, set=True }
Single lastYearContribution { get=True, set=True }
Single loyal { get=True, set=True }
Single mana { get=True, set=True }
Single manageAiHour { get=True, set=True }
Single maxMana { get=True, set=True }
Single maxPower { get=True, set=True }
Single maxhp { get=True, set=True }
Single medResist { get=True, set=True }
Single poisonInjury { get=True, set=True }
Single power { get=True, set=True }
Single realMaxHp { get=True, set=True }
Single realMaxMana { get=True, set=True }
Single realMaxPower { get=True, set=True }
Single restCureTotalRate { get=True, set=True }
Single selfHouseTotalAdd { get=True, set=True }
Single skinColorDark { get=True, set=True }
Single summonLv { get=True, set=True }
Single thisMonthContribution { get=True, set=True }
Single thisYearContribution { get=True, set=True }
Single voicePitch { get=True, set=True }
String heroFamilyName { get=True, set=True }
String heroName { get=True, set=True }
String heroNickName { get=True, set=True }
String settingName { get=True, set=True }
String summonSourceHero { get=True, set=True }
```

---

## HeroData self — fields (2)

Il2CppInterop wrapper 의 internal bookkeeping. game state 와 무관.

```
Boolean isWrapped
IntPtr pooledPtr
```

---

## Hero-related Manager candidates (2)

`Refresh|Update|OnHero|Rebuild` prefix + `HeroData` 인자 받는 매니저 enumerate 결과. 예상보다 적게 잡혔다 — game 의 hero 표시 매니저 (`HeroIconManager`, `HeroPanelController` 등) 는 `HeroData` 인자 없이 `int heroID` 또는 internal state 로 동작하기 때문 추정.

```
AuctionController.RefreshOfferMoney(Single,HeroData)
BigMapController.RefreshBigMapNPC(HeroData)
```

**해석**:
- `AuctionController.RefreshOfferMoney(Single, HeroData)` — 경매 시 호출, hero 갱신용 아님 (Apply 후 호출 가치 없음).
- `BigMapController.RefreshBigMapNPC(HeroData)` — 큰 지도 NPC 표시 갱신. **Apply 후 호출 가치 있음** — Step 7 의 유일 호출 대상.
- 그 외 hero icon / panel / portrait 매니저는 자기 책임으로 lazy refresh — game frame 의 자연 update 에 위임 (spec §4.5 / §7.2.1 의 Step 7 책임 축소).
