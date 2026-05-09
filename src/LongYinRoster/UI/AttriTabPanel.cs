using System;
using System.Collections.Generic;
using LongYinRoster.Core;
using LongYinRoster.Util;
using UnityEngine;
using Logger = LongYinRoster.Util.Logger;

namespace LongYinRoster.UI;

/// <summary>v0.7.10 Phase 2 — buffer for [속성] secondary tab (test-friendly extracted).</summary>
public sealed class AttriTabBuffer
{
    public sealed record Row(AttriAxis Axis, int Index, string Label,
                             float OriginalBase, float OriginalMax)
    {
        public string BaseInput { get; set; } = "";
        public string MaxInput  { get; set; } = "";
        public bool   Dirty => BaseInput != OriginalBase.ToString("0")
                            || MaxInput  != OriginalMax.ToString("0");
    }

    private readonly Dictionary<(AttriAxis, int), Row> _rows = new();

    public void LoadFromHero(object hero)
    {
        _rows.Clear();
        AddRowsForAxis(hero, AttriAxis.Attri);
        AddRowsForAxis(hero, AttriAxis.FightSkill);
        AddRowsForAxis(hero, AttriAxis.LivingSkill);
    }

    private void AddRowsForAxis(object hero, AttriAxis axis)
    {
        int n = AttriLabels.Count(axis);
        for (int i = 0; i < n; i++)
        {
            var (b, m) = HeroAttriReflector.GetEntry(hero, axis, i);
            var row = new Row(axis, i, AttriLabels.For(axis, i), b, m)
            {
                BaseInput = b.ToString("0"),
                MaxInput  = m.ToString("0"),
            };
            _rows[(axis, i)] = row;
        }
    }

    public Row Get(AttriAxis axis, int idx) => _rows[(axis, idx)];

    public void SetBaseInput(AttriAxis axis, int idx, string s)
    {
        if (_rows.TryGetValue((axis, idx), out var r)) r.BaseInput = s;
    }

    public void SetMaxInput(AttriAxis axis, int idx, string s)
    {
        if (_rows.TryGetValue((axis, idx), out var r)) r.MaxInput = s;
    }

    public void BulkSetMax(AttriAxis axis, string s)
    {
        int n = AttriLabels.Count(axis);
        for (int i = 0; i < n; i++)
            if (_rows.TryGetValue((axis, i), out var r)) r.MaxInput = s;
    }

    public void Reset()
    {
        foreach (var r in _rows.Values)
        {
            r.BaseInput = r.OriginalBase.ToString("0");
            r.MaxInput  = r.OriginalMax.ToString("0");
        }
    }

    public bool IsDirty
    {
        get { foreach (var r in _rows.Values) if (r.Dirty) return true; return false; }
    }

    public List<Row> GetDirtyRows()
    {
        var list = new List<Row>();
        foreach (var r in _rows.Values) if (r.Dirty) list.Add(r);
        return list;
    }

    public IEnumerable<Row> EnumerateAxis(AttriAxis axis)
    {
        int n = AttriLabels.Count(axis);
        for (int i = 0; i < n; i++)
            if (_rows.TryGetValue((axis, i), out var r)) yield return r;
    }
}

/// <summary>
/// v0.7.10 Phase 2 — PlayerEditorPanel 의 [속성] secondary tab.
///
/// 720 width 분할 = 속성 240 / 무학 240 / 기예 240. row height 24.
/// row 형식 = [라벨 48] [base TextField 48] / [max TextField 48] [+buff 40] [→ effective 40]
/// 일괄 button = column 하단 [TextField] [전체 N].
/// [저장] / [되돌리기] = 탭 footer.
/// </summary>
public sealed class AttriTabPanel
{
    private readonly AttriTabBuffer _buffer = new();
    private object? _loadedFor;

    private string _bulkAttriInput       = "999";
    private string _bulkFightSkillInput  = "999";
    private string _bulkLivingSkillInput = "999";

    public void Draw(object hero)
    {
        if (hero == null)
        {
            GUILayout.Label("플레이어 정보 없음");
            return;
        }
        if (!ReferenceEquals(_loadedFor, hero))
        {
            _buffer.LoadFromHero(hero);
            _loadedFor = hero;
        }

        GUILayout.BeginHorizontal();
        DrawColumn(hero, AttriAxis.Attri, "속성", ref _bulkAttriInput);
        DrawColumn(hero, AttriAxis.FightSkill, "무학", ref _bulkFightSkillInput);
        DrawColumn(hero, AttriAxis.LivingSkill, "기예", ref _bulkLivingSkillInput);
        GUILayout.EndHorizontal();

        GUILayout.Space(8);
        DrawFooter(hero);
    }

    private void DrawColumn(object hero, AttriAxis axis, string title, ref string bulkInput)
    {
        GUILayout.BeginVertical(GUILayout.Width(240));
        GUILayout.Label(title);
        foreach (var row in _buffer.EnumerateAxis(axis))
        {
            DrawRow(hero, row);
        }
        GUILayout.Space(4);
        GUILayout.BeginHorizontal();
        bulkInput = GUILayout.TextField(bulkInput, GUILayout.Width(64));
        if (GUILayout.Button($"전체 {title} 자질", GUILayout.Width(120)))
        {
            _buffer.BulkSetMax(axis, bulkInput);
        }
        GUILayout.EndHorizontal();
        GUILayout.EndVertical();
    }

    private void DrawRow(object hero, AttriTabBuffer.Row row)
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label(row.Label, GUILayout.Width(48));
        string newBase = GUILayout.TextField(row.BaseInput, GUILayout.Width(48));
        if (newBase != row.BaseInput) _buffer.SetBaseInput(row.Axis, row.Index, newBase);
        GUILayout.Label("/", GUILayout.Width(8));
        string newMax = GUILayout.TextField(row.MaxInput, GUILayout.Width(48));
        if (newMax != row.MaxInput) _buffer.SetMaxInput(row.Axis, row.Index, newMax);

        float buff = HeroAttriReflector.GetBuff(hero, row.Axis, row.Index);
        float effective = (CharacterAttriEditor.TryParseInput(row.BaseInput, out var b) ? b : 0f) + buff;
        GUILayout.Label($"+{buff:0}", GUILayout.Width(40));
        GUILayout.Label($"→ {effective:0}", GUILayout.Width(40));
        GUILayout.EndHorizontal();
    }

    private void DrawFooter(object hero)
    {
        GUILayout.BeginHorizontal();
        GUI.enabled = _buffer.IsDirty;
        if (GUILayout.Button("저장", GUILayout.Width(80)))
        {
            ApplyDirty(hero);
        }
        GUI.enabled = true;

        if (GUILayout.Button("되돌리기", GUILayout.Width(80)))
        {
            _buffer.Reset();
        }
        GUILayout.Space(8);
        GUILayout.EndHorizontal();
    }

    private void ApplyDirty(object hero)
    {
        var dirty = _buffer.GetDirtyRows();
        int success = 0, failed = 0;
        foreach (var r in dirty)
        {
            bool baseOk = true, maxOk = true;
            if (CharacterAttriEditor.TryParseInput(r.BaseInput, out var bv))
            {
                if (Math.Abs(bv - r.OriginalBase) > 0.001f)
                    baseOk = CharacterAttriEditor.Change(hero, r.Axis, r.Index, bv);
            }
            else baseOk = false;

            if (CharacterAttriEditor.TryParseInput(r.MaxInput, out var mv))
            {
                if (Math.Abs(mv - r.OriginalMax) > 0.001f)
                    maxOk = CharacterAttriEditor.ChangeMax(hero, r.Axis, r.Index, mv);
            }
            else maxOk = false;

            if (baseOk && maxOk) success++; else failed++;
        }

        // sanitize 1회 (v0.7.7/v0.7.8 검증된 helper)
        try { PlayerEditApplier.RefreshMaxAttriAndSkill(hero); }
        catch (Exception ex) { Logger.WarnOnce("AttriTabPanel", $"Refresh threw: {ex.Message}"); }

        // re-load buffer from refreshed hero state
        _buffer.LoadFromHero(hero);
        _loadedFor = hero;

        ToastService.Push(failed == 0
            ? $"속성 {success} 항목 적용됨"
            : $"성공 {success} / 실패 {failed}");
    }
}
