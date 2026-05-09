# LongYinRoster v0.7.7 Implementation Plan — Item editor

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** ItemDetailPanel 의 view-only 필드를 edit-able 로 확장 — 7 카테고리 × ~17 distinct field. Hybrid 적용 (reflection + read-back + regenerate fallback) + Aggressive sanitize (Range + CountValueAndWeight + SaveDataSanitizer + RefreshSelfState).

**Architecture:** Layered — (0) Spike: reflection setter strip 검증 → (1) `ItemEditField` matrix POCO → (2) `ItemEditApplier` pipeline (8 step) + tests → (3) ItemDetailPanel edit mode UI → (4) ContainerPanel focus stale reset + 외부 컨테이너 disable → (5) Smoke ~28.

**Tech Stack:** C# / .NET (BepInEx 6 IL2CPP), reflection 기반 setter + game-self method (`ItemData.Clone`, `CountValueAndWeight`, `HeroData.RefreshSelfState`), xUnit + Shouldly.

**Spec:** [docs/superpowers/specs/2026-05-08-longyin-roster-mod-v0.7.7-design.md](../specs/2026-05-08-longyin-roster-mod-v0.7.7-design.md)
**Roadmap:** [docs/superpowers/specs/2026-05-05-longyin-roster-mod-roadmap-v0.7.4-to-v0.8.md](../specs/2026-05-05-longyin-roster-mod-roadmap-v0.7.4-to-v0.8.md) §2.4 + G1 Decision (2026-05-08, GO)

---

## Task 0: Spike — Reflection setter strip + sub-data identity 검증

**Files:**
- Edit (temp): `src/LongYinRoster/UI/ModWindow.cs` — F12 핸들러 안에 임시 spike 코드

**Goal:** ItemData reflection setter 가 IL2CPP 빌드에서 silent no-op 인지 검증 (HeroData v0.2 strip 교훈). spec §7.1 Risk 의 fallback (regenerate) 활성화 시점 결정.

- [ ] **Step 0.1: 임시 spike 코드 추가**

  ```csharp
  // ModWindow.Update() 안 — F12 spike (release 전 제거)
  if (Input.GetKeyDown(KeyCode.F12))
  {
      var inv = GetPlayerInventoryList();
      if (inv != null)
      {
          int n = Core.IL2CppListOps.Count(inv);
          if (n > 0)
          {
              var item = Core.IL2CppListOps.Get(inv, 0);
              if (item != null)
              {
                  // (a) Top-level reflection setter
                  var rareLvField = item.GetType().GetField("rareLv", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                  if (rareLvField != null)
                  {
                      object before = rareLvField.GetValue(item) ?? -1;
                      try { rareLvField.SetValue(item, 5); }
                      catch (Exception ex) { Logger.Warn($"[v0.7.7 Spike] rareLv setter threw: {ex.GetType().Name}"); }
                      object after = rareLvField.GetValue(item) ?? -1;
                      Logger.Info($"[v0.7.7 Spike] rareLv: {before} → {after} (target=5)");
                  }
                  // (b) Sub-data nested reflection setter
                  var ed = ReadFieldOrProperty(item, "equipmentData");
                  if (ed != null)
                  {
                      var enhField = ed.GetType().GetField("enhanceLv", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                      if (enhField != null)
                      {
                          object before = enhField.GetValue(ed) ?? -1;
                          try { enhField.SetValue(ed, 7); }
                          catch (Exception ex) { Logger.Warn($"[v0.7.7 Spike] enhanceLv setter threw: {ex.GetType().Name}"); }
                          object after = enhField.GetValue(ed) ?? -1;
                          Logger.Info($"[v0.7.7 Spike] equipmentData.enhanceLv: {before} → {after} (target=7)");
                      }
                  }
                  // (c) ItemData.CountValueAndWeight 호출 가능성
                  var cwm = item.GetType().GetMethod("CountValueAndWeight", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                  Logger.Info($"[v0.7.7 Spike] CountValueAndWeight method present: {cwm != null}");
                  // (d) ItemData.Clone 호출 가능성
                  var cloneM = item.GetType().GetMethod("Clone", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                  Logger.Info($"[v0.7.7 Spike] Clone method present: {cloneM != null}");
              }
          }
      }
  }
  ```

- [ ] **Step 0.2: 빌드 + 인게임 진입 + F12 입력**
  ```pwsh
  DOTNET_CLI_UI_LANGUAGE=en dotnet build src/LongYinRoster/LongYinRoster.csproj -c Release
  ```
  - 게임 실행 → 인벤 첫 item (Equipment 가정) → F12 → BepInEx 콘솔 + LogOutput.log 로그 확인.

- [ ] **Step 0.3: 결과 분류**
  - **결과 A (PASS — 기대)**: `rareLv: 0 → 5`, `equipmentData.enhanceLv: 3 → 7`, CountValueAndWeight + Clone present. → spec §1.2 의 reflection-우선 path 그대로. regenerate fallback 은 read-back mismatch 시에만 활성.
  - **결과 B (Top-level strip)**: `rareLv: 0 → 0` (silent no-op). → spec §1.2 step 5 즉시 활성, regenerate 가 default path.
  - **결과 C (Sub-data strip)**: `rareLv: 0 → 5` 통과, `equipmentData.enhanceLv: 3 → 3` no-op. → sub-data 만 regenerate, top-level reflection.
  - **결과 D (Method 부재)**: CountValueAndWeight 또는 Clone 미존재. → spec §1.2 step 6 / step 5 fallback 재설계 필요.

- [ ] **Step 0.4: spike dump 작성**
  - Create: `docs/superpowers/dumps/2026-05-08-v0.7.7-setter-spike.md` (~30 LOC)
  - 결과 A/B/C/D + LogOutput grep 인용 + Layer 1~5 영향 명시

- [ ] **Step 0.5: temp probe 제거**
  - F12 핸들러 안 spike 코드 삭제 (smoke 첫 frame 까지는 유지 — 사용자 검증 후 제거)
  - commit message: `spike: v0.7.7 reflection setter 검증 — A/B/C/D`

**Decision Gate:** Step 0.3 결과에 따라 Layer 2 (`ItemEditApplier.Apply`) 의 step 4/5 분기 로직 구체화. Layer 1 (`ItemEditField` matrix) 는 결과 무관 진행 가능.

---

## Task 1: ItemEditField matrix (Layer 1)

**Files:**
- Create: `src/LongYinRoster/Core/ItemEditField.cs`
- Create: `src/LongYinRoster.Tests/ItemEditFieldTests.cs`

**Goal:** 17 distinct field 의 range / 라벨 / kind matrix POCO + parse helper.

### Subtask 1.1: ItemEditFieldTests (TDD red)

- [ ] **Step 1.1.1: 신규 test file**

  ```csharp
  using LongYinRoster.Core;
  using Shouldly;
  using Xunit;

  namespace LongYinRoster.Tests;

  public class ItemEditFieldTests
  {
      [Theory]
      [InlineData("3", true, 3)]
      [InlineData("0", true, 0)]
      [InlineData("9", true, 9)]
      [InlineData("-1", false, 0)]
      [InlineData("10", false, 0)]
      [InlineData("foo", false, 0)]
      public void TryParseInt_RangeValidated(string input, bool expectOk, int expectVal)
      {
          var f = new ItemEditField("equipmentData.enhanceLv", "강화", ItemEditFieldKind.Int, 0, 9);
          var ok = f.TryParse(input, out object value, out string error);
          ok.ShouldBe(expectOk);
          if (ok) ((int)value).ShouldBe(expectVal);
      }

      [Theory]
      [InlineData("0.5", true, 0.5f)]
      [InlineData("9.99", true, 9.99f)]
      [InlineData("0.005", false, 0f)]   // < min 0.01
      [InlineData("10.0", false, 0f)]    // > max 9.99
      [InlineData("foo", false, 0f)]
      public void TryParseFloat_RangeValidated(string input, bool expectOk, float expectVal)
      {
          var f = new ItemEditField("horseData.favorRate", "호감 율", ItemEditFieldKind.Float, 0.01f, 9.99f);
          var ok = f.TryParse(input, out object value, out string error);
          ok.ShouldBe(expectOk);
          if (ok) ((float)value).ShouldBe(expectVal);
      }

      [Theory]
      [InlineData("true", true, true)]
      [InlineData("false", true, false)]
      [InlineData("1", true, true)]
      [InlineData("0", true, false)]
      [InlineData("foo", false, false)]
      public void TryParseBool_AcceptsCommonForms(string input, bool expectOk, bool expectVal)
      {
          var f = new ItemEditField("treasureData.fullIdentified", "완전 감정", ItemEditFieldKind.Bool, 0, 1);
          var ok = f.TryParse(input, out object value, out string error);
          ok.ShouldBe(expectOk);
          if (ok) ((bool)value).ShouldBe(expectVal);
      }

      [Fact]
      public void Matrix_Equipment_HasEnhanceLv()
      {
          var fields = ItemEditFieldMatrix.ForCategory(0);
          fields.ShouldContain(f => f.Path == "equipmentData.enhanceLv");
      }

      [Fact]
      public void Matrix_Material_OnlyCommon()
      {
          var fields = ItemEditFieldMatrix.ForCategory(5);
          // common 3 만
          fields.Count.ShouldBe(3);
          fields.ShouldContain(f => f.Path == "rareLv");
          fields.ShouldContain(f => f.Path == "itemLv");
          fields.ShouldContain(f => f.Path == "value");
      }

      [Fact]
      public void Matrix_Horse_AllStats()
      {
          var fields = ItemEditFieldMatrix.ForCategory(6);
          fields.ShouldContain(f => f.Path == "horseData.speedAdd");
          fields.ShouldContain(f => f.Path == "horseData.maxWeightAdd");
          fields.ShouldContain(f => f.Path == "horseData.favorRate");
      }

      [Fact]
      public void Matrix_UnknownType_Empty()
      {
          var fields = ItemEditFieldMatrix.ForCategory(99);
          fields.ShouldBeEmpty();
      }
  }
  ```

- [ ] **Step 1.1.2: dotnet test → fail (red — ItemEditField 미존재)**

### Subtask 1.2: ItemEditField + Matrix 구현

- [ ] **Step 1.2.1: ItemEditField.cs 작성**

  ```csharp
  using System.Collections.Generic;
  using System.Globalization;

  namespace LongYinRoster.Core;

  public enum ItemEditFieldKind { Int, Float, Bool }

  public sealed class ItemEditField
  {
      public string Path { get; }
      public string KrLabel { get; }
      public ItemEditFieldKind Kind { get; }
      public float Min { get; }
      public float Max { get; }

      public ItemEditField(string path, string label, ItemEditFieldKind kind, float min, float max)
      { Path = path; KrLabel = label; Kind = kind; Min = min; Max = max; }

      public bool TryParse(string input, out object value, out string error)
      {
          error = "";
          value = Kind switch { ItemEditFieldKind.Int => 0, ItemEditFieldKind.Float => 0f, _ => false };
          if (string.IsNullOrEmpty(input)) { error = "빈 입력"; return false; }
          switch (Kind)
          {
              case ItemEditFieldKind.Int:
                  if (!int.TryParse(input, NumberStyles.Integer, CultureInfo.InvariantCulture, out int i)) { error = "정수 형식 아님"; return false; }
                  if (i < Min || i > Max) { error = $"범위: {Min:F0}~{Max:F0}"; return false; }
                  value = i; return true;
              case ItemEditFieldKind.Float:
                  if (!float.TryParse(input, NumberStyles.Float, CultureInfo.InvariantCulture, out float f)) { error = "소수 형식 아님"; return false; }
                  if (f < Min || f > Max) { error = $"범위: {Min:F2}~{Max:F2}"; return false; }
                  value = f; return true;
              case ItemEditFieldKind.Bool:
                  if (input == "true" || input == "1" || input == "yes" || input == "예") { value = true; return true; }
                  if (input == "false" || input == "0" || input == "no" || input == "아니오") { value = false; return true; }
                  error = "true/false 또는 1/0";
                  return false;
          }
          error = "Unknown kind";
          return false;
      }
  }

  public static class ItemEditFieldMatrix
  {
      public static readonly IReadOnlyList<ItemEditField> Common = new[]
      {
          new ItemEditField("rareLv", "등급", ItemEditFieldKind.Int, 0, 5),
          new ItemEditField("itemLv", "품질", ItemEditFieldKind.Int, 0, 5),
          new ItemEditField("value",  "가격", ItemEditFieldKind.Int, 0, 9999999),
      };
      public static readonly IReadOnlyList<ItemEditField> Equipment = Concat(Common, new[]
      {
          new ItemEditField("equipmentData.enhanceLv",    "강화",     ItemEditFieldKind.Int, 0, 9),
          new ItemEditField("equipmentData.speEnhanceLv", "특수 강화", ItemEditFieldKind.Int, 0, 9),
          new ItemEditField("equipmentData.speWeightLv",  "무게 경감", ItemEditFieldKind.Int, 0, 9),
      });
      public static readonly IReadOnlyList<ItemEditField> MedFood = Concat(Common, new[]
      {
          new ItemEditField("medFoodData.enhanceLv",          "강화",      ItemEditFieldKind.Int, 0, 9),
          new ItemEditField("medFoodData.randomSpeAddValue",  "추가 보정", ItemEditFieldKind.Int, 0, 999),
      });
      public static readonly IReadOnlyList<ItemEditField> Book = Concat(Common, new[]
      {
          new ItemEditField("bookData.skillID", "무공 ID", ItemEditFieldKind.Int, 0, 99999),
      });
      public static readonly IReadOnlyList<ItemEditField> Treasure = Concat(Common, new[]
      {
          new ItemEditField("treasureData.fullIdentified",      "완전 감정",      ItemEditFieldKind.Bool,  0, 1),
          new ItemEditField("treasureData.identifyKnowledgeNeed", "감정 필요 지식", ItemEditFieldKind.Float, 0, 9999),
      });
      public static readonly IReadOnlyList<ItemEditField> Horse = Concat(Common, new[]
      {
          new ItemEditField("horseData.speedAdd",     "속도(+Add)",    ItemEditFieldKind.Float, 0,    9999),
          new ItemEditField("horseData.powerAdd",     "힘(+Add)",      ItemEditFieldKind.Float, 0,    9999),
          new ItemEditField("horseData.sprintAdd",    "스프린트(+Add)", ItemEditFieldKind.Float, 0,    9999),
          new ItemEditField("horseData.resistAdd",    "인내(+Add)",    ItemEditFieldKind.Float, 0,    9999),
          new ItemEditField("horseData.maxWeightAdd", "최대무게 추가",  ItemEditFieldKind.Float, 0,    99999),
          new ItemEditField("horseData.favorRate",    "호감 율",       ItemEditFieldKind.Float, 0.01f, 9.99f),
      });
      public static readonly IReadOnlyList<ItemEditField> Material = Common;

      public static IReadOnlyList<ItemEditField> ForCategory(int type) => type switch
      {
          0 => Equipment, 2 => MedFood, 3 => Book,
          4 => Treasure, 5 => Material, 6 => Horse,
          _ => System.Array.Empty<ItemEditField>(),
      };

      private static ItemEditField[] Concat(IReadOnlyList<ItemEditField> a, ItemEditField[] b)
      {
          var result = new ItemEditField[a.Count + b.Length];
          for (int i = 0; i < a.Count; i++) result[i] = a[i];
          b.CopyTo(result, a.Count);
          return result;
      }
  }
  ```

- [ ] **Step 1.2.2: dotnet test → green** (~13 case 추가)

- [ ] **Step 1.2.3: commit**
  - Message: `feat(core): v0.7.7 Layer 1 — ItemEditField + matrix (17 fields × 7 categories)`

---

## Task 2: ItemEditApplier (Layer 2)

**Files:**
- Create: `src/LongYinRoster/Core/ItemEditApplier.cs`
- Create: `src/LongYinRoster.Tests/ItemEditApplierTests.cs`

**Goal:** 8-step pipeline (parse → sanitize → reflection setter → read-back → regenerate fallback → CountValueAndWeight → RefreshSelfState → result).

### Subtask 2.1: ItemEditApplierTests (TDD red — POCO mock)

- [ ] **Step 2.1.1: 신규 test file**

  IL2CPP 의존 없이 reflection 만 검증 — POCO mock 사용:

  ```csharp
  public class ItemEditApplierTests
  {
      private sealed class FakeItem
      {
          public int rareLv;
          public int itemLv;
          public int value;
          public int type;
          public FakeEquipment? equipmentData;
          public FakeHorse? horseData;
      }
      private sealed class FakeEquipment { public int enhanceLv; public int speEnhanceLv; public bool equiped; }
      private sealed class FakeHorse { public float speedAdd; public bool equiped; }

      [Fact]
      public void Apply_TopLevelInt_SetterPasses()
      {
          var item = new FakeItem { rareLv = 0, type = 0 };
          var field = new ItemEditField("rareLv", "등급", ItemEditFieldKind.Int, 0, 5);
          var r = ItemEditApplier.Apply(item, field, 3, player: null);
          r.Success.ShouldBeTrue();
          r.Method.ShouldBe("reflection");
          item.rareLv.ShouldBe(3);
      }

      [Fact]
      public void Apply_NestedSubData_SetterPasses()
      {
          var item = new FakeItem { type = 0, equipmentData = new FakeEquipment { enhanceLv = 0 } };
          var field = new ItemEditField("equipmentData.enhanceLv", "강화", ItemEditFieldKind.Int, 0, 9);
          var r = ItemEditApplier.Apply(item, field, 5, player: null);
          r.Success.ShouldBeTrue();
          item.equipmentData!.enhanceLv.ShouldBe(5);
      }

      [Fact]
      public void Apply_InvalidPath_ReturnsError()
      {
          var item = new FakeItem();
          var field = new ItemEditField("nonExistent.nope", "?", ItemEditFieldKind.Int, 0, 9);
          var r = ItemEditApplier.Apply(item, field, 1, player: null);
          r.Success.ShouldBeFalse();
          r.Error.ShouldNotBeNullOrEmpty();
      }

      [Fact]
      public void Apply_NaN_SanitizedToFallback()
      {
          var item = new FakeItem { type = 6, horseData = new FakeHorse { speedAdd = 100f } };
          var field = new ItemEditField("horseData.speedAdd", "속도", ItemEditFieldKind.Float, 0, 9999);
          var r = ItemEditApplier.Apply(item, field, float.NaN, player: null);
          // sanitize → 0 (fallback)
          r.Success.ShouldBeTrue();
          item.horseData!.speedAdd.ShouldBe(0f);
      }

      [Fact]
      public void Apply_Infinity_SanitizedToMax()
      {
          var item = new FakeItem { type = 6, horseData = new FakeHorse { speedAdd = 100f } };
          var field = new ItemEditField("horseData.speedAdd", "속도", ItemEditFieldKind.Float, 0, 9999);
          var r = ItemEditApplier.Apply(item, field, float.PositiveInfinity, player: null);
          r.Success.ShouldBeTrue();
          item.horseData!.speedAdd.ShouldBe(9999f);
      }

      [Fact]
      public void Apply_DotPath_TwoSegments()
      {
          var item = new FakeItem { equipmentData = new FakeEquipment { speEnhanceLv = 0 } };
          var field = new ItemEditField("equipmentData.speEnhanceLv", "특수 강화", ItemEditFieldKind.Int, 0, 9);
          var r = ItemEditApplier.Apply(item, field, 4, player: null);
          item.equipmentData!.speEnhanceLv.ShouldBe(4);
      }

      [Fact]
      public void Apply_Bool_SetterPasses()
      {
          var item = new FakeItem { type = 0, equipmentData = new FakeEquipment { equiped = false } };
          var field = new ItemEditField("equipmentData.equiped", "착용중", ItemEditFieldKind.Bool, 0, 1);
          var r = ItemEditApplier.Apply(item, field, true, player: null);
          item.equipmentData!.equiped.ShouldBeTrue();
      }

      [Fact]
      public void Apply_NullSubData_ReturnsError()
      {
          var item = new FakeItem { type = 0, equipmentData = null };
          var field = new ItemEditField("equipmentData.enhanceLv", "강화", ItemEditFieldKind.Int, 0, 9);
          var r = ItemEditApplier.Apply(item, field, 5, player: null);
          r.Success.ShouldBeFalse();
          r.Error.ShouldContain("equipmentData");
      }
  }
  ```

- [ ] **Step 2.1.2: dotnet test → fail (red)**

### Subtask 2.2: ItemEditApplier 구현

- [ ] **Step 2.2.1: ItemEditApplier.cs 작성**

  ```csharp
  using System;
  using System.Reflection;
  using LongYinRoster.Util;
  using Logger = LongYinRoster.Util.Logger;

  namespace LongYinRoster.Core;

  public sealed class ItemEditResult
  {
      public bool Success { get; init; }
      public string? Error { get; init; }
      public string? Method { get; init; }
      public bool TriggeredRefreshSelfState { get; init; }
  }

  public static class ItemEditApplier
  {
      private const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

      public static ItemEditResult Apply(object item, ItemEditField field, object value, object? player)
      {
          if (item == null) return new() { Success = false, Error = "item is null" };

          // 2. SaveDataSanitizer
          object sanitized = Sanitize(value, field);

          // 3. Reflection setter (with read-back)
          if (TryReflectionSetter(item, field.Path, sanitized, out var error))
          {
              // 6. CountValueAndWeight (game-self method, IL2CPP only — POCO mock 안 호출)
              TryInvokeCountValueAndWeight(item);
              // 7. RefreshSelfState if equipped
              bool refreshed = false;
              if (player != null && IsEquipped(item))
              {
                  refreshed = TryInvokeRefreshSelfState(player);
              }
              return new() { Success = true, Method = "reflection", TriggeredRefreshSelfState = refreshed };
          }

          // 5. Regenerate fallback (skillID 같은 identity-impact)
          // POCO mock 환경에선 Clone 미구현 → reflection failure 그대로 return
          return new() { Success = false, Error = error };
      }

      internal static bool TryReflectionSetter(object item, string path, object value, out string error)
      {
          error = "";
          var segments = path.Split('.');
          object cursor = item;
          for (int i = 0; i < segments.Length - 1; i++)
          {
              var sub = ReadFieldOrProperty(cursor, segments[i]);
              if (sub == null) { error = $"{segments[i]} is null"; return false; }
              cursor = sub;
          }
          var leaf = segments[^1];
          var t = cursor.GetType();
          var f = t.GetField(leaf, F);
          if (f != null)
          {
              object before = f.GetValue(cursor)!;
              try { f.SetValue(cursor, value); }
              catch (Exception ex) { error = ex.Message; return false; }
              object after = f.GetValue(cursor)!;
              if (!Equals(after, value))
              {
                  error = $"silent fail: {leaf} {before} → {after} (target={value})";
                  return false;
              }
              return true;
          }
          var p = t.GetProperty(leaf, F);
          if (p != null && p.CanWrite)
          {
              try { p.SetValue(cursor, value); }
              catch (Exception ex) { error = ex.Message; return false; }
              return Equals(p.GetValue(cursor), value);
          }
          error = $"{leaf} not found";
          return false;
      }

      private static object Sanitize(object value, ItemEditField field)
      {
          if (value is float f)
          {
              if (float.IsNaN(f)) return 0f;
              if (float.IsPositiveInfinity(f)) return field.Max;
              if (float.IsNegativeInfinity(f)) return field.Min;
              if (f < field.Min) return field.Min;
              if (f > field.Max) return field.Max;
              return f;
          }
          if (value is int i)
          {
              if (i < field.Min) return (int)field.Min;
              if (i > field.Max) return (int)field.Max;
              return i;
          }
          return value;
      }

      private static object? ReadFieldOrProperty(object obj, string name)
      {
          var t = obj.GetType();
          var f = t.GetField(name, F);
          if (f != null) return f.GetValue(obj);
          var p = t.GetProperty(name, F);
          if (p != null) return p.GetValue(obj);
          return null;
      }

      private static bool IsEquipped(object item)
      {
          var ed = ReadFieldOrProperty(item, "equipmentData");
          if (ed != null && ReadFieldOrProperty(ed, "equiped") is bool eb && eb) return true;
          var hd = ReadFieldOrProperty(item, "horseData");
          if (hd != null && ReadFieldOrProperty(hd, "equiped") is bool hb && hb) return true;
          return false;
      }

      private static void TryInvokeCountValueAndWeight(object item)
      {
          try
          {
              var m = item.GetType().GetMethod("CountValueAndWeight", F, null, Type.EmptyTypes, null);
              m?.Invoke(item, null);
          }
          catch (Exception ex) { Logger.Warn($"CountValueAndWeight: {ex.GetType().Name}: {ex.Message}"); }
      }

      private static bool TryInvokeRefreshSelfState(object player)
      {
          try
          {
              var m = player.GetType().GetMethod("RefreshSelfState", F, null, Type.EmptyTypes, null);
              if (m != null) { m.Invoke(player, null); return true; }
              return false;
          }
          catch (Exception ex) { Logger.Warn($"RefreshSelfState: {ex.GetType().Name}: {ex.Message}"); return false; }
      }
  }
  ```

- [ ] **Step 2.2.2: dotnet test → green** (~8 case 추가)

- [ ] **Step 2.2.3: csproj include**

  Tests project csproj 에 `Core/ItemEditField.cs` + `Core/ItemEditApplier.cs` Compile include 추가.

- [ ] **Step 2.2.4: commit**
  - Message: `feat(core): v0.7.7 Layer 2 — ItemEditApplier (8-step pipeline) + 8 tests`

**Note**: Task 0 spike 결과 B (top-level strip) 또는 C (sub-data strip) 면 본 layer 의 regenerate fallback (`Clone + AddCloneWithLv`) 추가 구현. POCO mock 으론 검증 불가 → 인게임 smoke 만.

---

## Task 3: ItemDetailPanel edit mode UI

**Files:**
- Edit: `src/LongYinRoster/UI/ItemDetailPanel.cs`
- Edit: `src/LongYinRoster/Util/KoreanStrings.cs`

**Goal:** [편집] 토글 + curated 라벨 옆 textfield/dropdown/checkbox + 행별 [적용] 버튼 + Disclaimer + 외부 컨테이너 시 disabled.

### Subtask 3.1: KoreanStrings 갱신

- [ ] **Step 3.1.1: 새 라벨 상수 추가**

  ```csharp
  public const string EditModeBtn               = "편집";
  public const string EditApplyBtn              = "적용";
  public const string EditDisclaimer            = "⚠ 편집한 값은 게임 save 후 영속. Apply/Restore 흐름과 별개.";
  public const string EditModeContainerOnly     = "외부 컨테이너 편집 안 됨 (인벤·창고만)";
  public const string EditFieldRangeError       = "{0} 범위: {1}~{2}";
  public const string EditFieldParseError       = "{0}: 잘못된 입력 ({1})";
  public const string EditApplyOk               = "✔ {0} = {1} 적용";
  public const string EditApplyFailed           = "✘ 변경 실패: {0} ({1})";
  public const string EditFieldNotFoundForCategory = "이 카테고리에 편집 가능한 필드 없음";
  ```

### Subtask 3.2: ItemDetailPanel 변경

- [ ] **Step 3.2.1: edit mode 상태 + textfield buffer**

  ```csharp
  private bool _editMode = false;
  private readonly Dictionary<string, string> _textBuf = new();   // path → text
  ```

- [ ] **Step 3.2.2: 헤더 [편집] 토글**

  ```csharp
  // X 버튼 옆에 [편집] 토글 — 외부 컨테이너 area 시 disabled
  bool isExternalContainer = _containerPanel.Focus?.Area == ContainerArea.Container;
  var prevEnabled = GUI.enabled;
  GUI.enabled = !isExternalContainer;
  var prevColor = GUI.color;
  if (_editMode) GUI.color = Color.cyan;
  if (GUI.Button(new Rect(_rect.width - 76, 4, 44, 20), KoreanStrings.EditModeBtn))
  {
      _editMode = !_editMode;
      _textBuf.Clear();
  }
  GUI.color = prevColor;
  GUI.enabled = prevEnabled;
  // X 버튼은 그 옆 (위치 - 28 기존)
  ```

- [ ] **Step 3.2.3: edit mode 활성 시 disclaimer + curated edit row**

  ```csharp
  if (_editMode)
  {
      var prev = GUI.color;
      GUI.color = new Color(1f, 0.85f, 0.5f, 1f);  // 주황 경고
      GUILayout.Label(KoreanStrings.EditDisclaimer);
      GUI.color = prev;
  }
  if (isExternalContainer && _editMode)
  {
      _editMode = false;   // 강제 off
      ToastService.Push(KoreanStrings.EditModeContainerOnly, ToastKind.Info);
  }
  ```

- [ ] **Step 3.2.4: curated 행마다 [적용] 버튼 (mode=edit)**

  ```csharp
  // 현재 v0.7.4 의 curated 출력 loop 안에 분기 추가
  foreach (var (label, value) in curated)
  {
      GUILayout.BeginHorizontal();
      GUILayout.Label($"  {label}: ", GUILayout.Width(120));

      if (_editMode)
      {
          // path 매칭 — label → ItemEditField (KrLabel 비교)
          var field = FindFieldByLabel(label);
          if (field != null)
          {
              if (!_textBuf.ContainsKey(field.Path))
                  _textBuf[field.Path] = ExtractInitialText(value, field);
              _textBuf[field.Path] = GUILayout.TextField(_textBuf[field.Path], GUILayout.Width(80));
              if (GUILayout.Button(KoreanStrings.EditApplyBtn, GUILayout.Width(50)))
              {
                  ApplyField(field, _textBuf[field.Path]);
              }
          }
          else
          {
              GUILayout.Label(value);   // edit-able 아닌 derived 필드 (무게)
          }
      }
      else
      {
          GUILayout.Label(value);
      }
      GUILayout.EndHorizontal();
  }
  ```

- [ ] **Step 3.2.5: ApplyField helper**

  ```csharp
  private void ApplyField(ItemEditField field, string input)
  {
      if (!field.TryParse(input, out object value, out string error))
      {
          ToastService.Push(string.Format(KoreanStrings.EditFieldParseError, field.KrLabel, error), ToastKind.Error);
          return;
      }
      var item = _containerPanel.GetFocusedRawItem();
      if (item == null) { ToastService.Push("focused item null", ToastKind.Error); return; }
      var player = Core.HeroLocator.GetPlayer();

      var r = ItemEditApplier.Apply(item, field, value, player);
      if (r.Success)
      {
          ToastService.Push(string.Format(KoreanStrings.EditApplyOk, field.KrLabel, value), ToastKind.Success);
          // textfield buffer 갱신 (재계산된 값 반영을 위해 reset)
          _textBuf.Remove(field.Path);
          // ContainerPanel row refresh 요청 (외부에서 wire-up)
          OnAppliedRefreshRequest?.Invoke();
      }
      else
      {
          ToastService.Push(string.Format(KoreanStrings.EditApplyFailed, field.KrLabel, r.Error ?? "?"), ToastKind.Error);
      }
  }

  public Action? OnAppliedRefreshRequest;   // ModWindow 가 wire — RefreshAllContainerRows 호출
  ```

- [ ] **Step 3.2.6: ModWindow wiring**

  ```csharp
  // Awake() 안 ItemDetailPanel.Init 다음
  _itemDetailPanel.OnAppliedRefreshRequest = () => RefreshAllContainerRows();
  ```

- [ ] **Step 3.2.7: commit**
  - Message: `feat(ui): v0.7.7 Layer 3 — ItemDetailPanel 편집 mode + textfield + 적용 버튼`

---

## Task 4: ContainerPanel focus stale reset

**Files:**
- Edit: `src/LongYinRoster/UI/ContainerPanel.cs` (또는 ItemDetailPanel 자체)

**Goal:** Focus 변경 (다른 item 클릭) 시 ItemDetailPanel 의 _editMode = false / _textBuf clear. v0.7.4 D-1 stale focus 패턴 mirror.

- [ ] **Step 4.1: ItemDetailPanel.OnFocusChanged hook**

  ItemDetailPanel 의 OnGUI 첫 frame 에서 focus 변화 detect:

  ```csharp
  private object? _lastFocusedRawRef;

  // OnGUI 안
  var current = _containerPanel.GetFocusedRawItem();
  if (!ReferenceEquals(current, _lastFocusedRawRef))
  {
      // focus 변경 — edit buffer reset (textfield 의 stale 값 폐기)
      _textBuf.Clear();
      // edit mode 는 유지 (사용자가 명시적으로 off 안 했으면 다음 item 도 편집 가능)
      _lastFocusedRawRef = current;
  }
  ```

- [ ] **Step 4.2: ContainerPanel area 변경 시 edit mode 강제 off**

  외부 컨테이너 area 로 focus 시 _editMode 강제 false (이미 Subtask 3.2.3 에서 처리됨).

- [ ] **Step 4.3: dotnet test → green (logic 변경 없음 — UI 만)**

- [ ] **Step 4.4: commit**
  - Message: `feat(ui): v0.7.7 Layer 4 — ItemDetailPanel focus stale reset + 외부 컨테이너 disable`

---

## Task 5: 인게임 Smoke (~28 시나리오)

**Files:**
- Create: `docs/superpowers/dumps/2026-05-XX-v0.7.7-smoke-results.md`

**Goal:** Spec §6.1 의 28 신규 + v0.7.6 회귀 28 = 총 ~56 시나리오 검증.

### Subtask 5.1: 빌드 + 게임 진입

- [ ] **Step 5.1.1: 게임 닫기 + 빌드**
  ```pwsh
  tasklist | grep -i LongYinLiZhiZhuan
  DOTNET_CLI_UI_LANGUAGE=en dotnet build src/LongYinRoster/LongYinRoster.csproj -c Release
  ```

- [ ] **Step 5.1.2: 로그 클리어**
  ```pwsh
  Clear-Content "E:/Games/龙胤立志传.v1.0.0f8.2/LongYinLiZhiZhuan/BepInEx/LogOutput.log"
  ```

### Subtask 5.2: 신규 28 시나리오 (Spec §6.1 표 그대로)

각 시나리오 PASS/FAIL 기록.

### Subtask 5.3: 회귀 28 시나리오 (v0.7.6 smoke mirror)

특히 ItemDetailPanel view-only 동작 + ContainerPanel 영속화 회귀.

### Subtask 5.4: Strip 검증

```pwsh
Select-String -Path ".../LogOutput.log" -Pattern "Method unstripping failed"
Select-String -Path ".../LogOutput.log" -Pattern "ItemDetailPanel\..*threw"
Select-String -Path ".../LogOutput.log" -Pattern "ItemEditApplier"
```

### Subtask 5.5: smoke dump 작성

iteration fix narrative + strip 결과 + ItemEditApplier method/error 통계.

- [ ] commit: `docs: v0.7.7 인게임 smoke 결과 — N/N PASS`

---

## Task 6: Release prep

**Files:**
- Edit: `src/LongYinRoster/Plugin.cs` (VERSION 0.7.6 → 0.7.7 + Logger.Info v0.7.7 line)
- Edit: `README.md`
- Edit: `docs/HANDOFF.md` (release entry + baseline + 다음 sub-project + G2 게이트)
- Edit: `docs/superpowers/specs/2026-05-05-longyin-roster-mod-roadmap-v0.7.4-to-v0.8.md` (메타 §2.4 Result append + G1 후속)

- [ ] **Step 6.1: VERSION bump** — `Plugin.cs` const 0.7.6 → 0.7.7
- [ ] **Step 6.2: README.md** — 신규 기능 한 줄
- [ ] **Step 6.3: HANDOFF.md** — release entry / baseline / G2 게이트 진입 명시
- [ ] **Step 6.4: 메타 spec §2.4 Result append**
- [ ] **Step 6.5: dist zip + GitHub release**
- [ ] **Step 6.6: git tag v0.7.7**

---

## 예상 commit 시퀀스

| commit | 내용 | tests |
|---|---|---|
| 1 | `spike: v0.7.7 reflection setter 검증` | 238 |
| 2 | `feat(core): v0.7.7 Layer 1 — ItemEditField + matrix` | 251 (+13) |
| 3 | `feat(core): v0.7.7 Layer 2 — ItemEditApplier + 8 tests` | 259 (+8) |
| 4 | `feat(ui): v0.7.7 Layer 3 — ItemDetailPanel edit mode UI` | 259 |
| 5 | `feat(ui): v0.7.7 Layer 4 — Focus stale reset + 외부 컨테이너 disable` | 259 |
| 6 | `docs: v0.7.7 인게임 smoke 결과` | 259 |
| 7 | `chore(release): v0.7.7` | 259 |

총 7 commits + 1 tag.

## 위험 / 변동 요인

- **Task 0 결과 B/C**: Layer 2 의 regenerate fallback 활성 + commit 1~2개 추가
- **Task 0 결과 D**: spec §1.2 의 step 5/6 fallback 재설계 — 별도 mini-spike 추가
- **Smoke 회귀 발견**: iteration fix commit (v0.7.6 = 3 iter 였음, v0.7.7 은 통합 작업 많아 2~5 iter 가능성)
- **RefreshSelfState 부작용**: 사용자 보고 시 spec §7.2 의 fallback (D → C 로 hotfix) 적용
- **Book.skillID 검증 문제**: kungfuSkillDataBase iterate 비용 — 첫 frame lazy cache, 이후 O(1) check

## 참고 자산 / dumps

- `dumps/2026-05-03-v0.7.4-subdata-spike.md` — sub-data wrapper 인벤토리
- `dumps/2026-05-05-v075-cheat-feature-reference.md` §2 (ItemGenerator.AddCloneWithLv / ItemData.Clone / TryCast / CountValueAndWeight) + §9 (SaveDataSanitizer pattern)
- v0.7.6 plan & smoke dump — IMGUI 패턴 + ConfigEntry 영속화 baseline
- v0.7.4 D-1 plan & spike — focus + sub-data wrapper IL2CPP identity

## Spec 통과 검증

본 plan = spec §9 cycle 의 Layer 분해 충실. 모든 spec 결정 (Q1~Q6 + 자유 입력 3) 이 plan 단계로 매핑됨.
