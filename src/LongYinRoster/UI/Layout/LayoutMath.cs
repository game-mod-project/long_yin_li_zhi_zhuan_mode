using System;
using UnityEngine;

namespace LongYinRoster.UI.Layout;

/// <summary>
/// v0.8.0 — 레이아웃 공통 수식. Unity 의존 없음(Rect 만 값 타입으로 사용) → UnityStubs 로 단위 테스트.
/// IMGUI 내부 계산(FlexibleSpace 등, IL2CPP strip 전례)에 기대지 않고 숫자를 직접 낸다.
/// </summary>
public static class LayoutMath
{
    /// <summary>높이 → 행 수. n행은 n*rowH + (n-1)*spacing 을 차지. min 미만으로 내려가지 않음(페이지 크기 하한).</summary>
    public static int RowsThatFit(float availableH, float rowH, float spacing, int min = 1)
    {
        if (rowH <= 0f || availableH <= 0f) return min;
        int n = (int)Math.Floor((availableH + spacing) / (rowH + spacing));
        return Math.Max(min, n);
    }

    /// <summary>
    /// 세로 분배(컨테이너 인벤/창고의 일반형). 각 섹션은 overhead(헤더·버튼 줄)를 먼저 차지하고,
    /// 잔여 = totalH - Σoverhead - gap*(n-1) 을 펼친 섹션끼리 weight 비율로 나눈다. 접힌 섹션은 0.
    /// 반환값은 각 섹션의 '내용' 높이(overhead 제외). 잔여가 음수면 전부 0.
    /// </summary>
    public static float[] SplitHeight(float totalH, float[] overheads, float[] weights, bool[] collapsed, float gap)
    {
        int n = overheads.Length;
        if (weights.Length != n || collapsed.Length != n)
            throw new ArgumentException("SplitHeight: overheads/weights/collapsed 길이가 다름");
        var result = new float[n];
        float fixedSum = 0f, weightSum = 0f;
        for (int i = 0; i < n; i++)
        {
            fixedSum += overheads[i];
            if (!collapsed[i]) weightSum += weights[i];
        }
        float remaining = totalH - fixedSum - gap * Math.Max(0, n - 1);
        if (remaining < 0f) remaining = 0f;
        for (int i = 0; i < n; i++)
            result[i] = (collapsed[i] || weightSum <= 0f) ? 0f : remaining * (weights[i] / weightSum);
        return result;
    }

    /// <summary>가로 분배. mins 를 먼저 보장하고 잔여 = totalW - Σmins - gap*(n-1) 을 weight 비율로. 잔여 음수면 mins 그대로.</summary>
    public static float[] SplitWidth(float totalW, float[] weights, float gap, float[] mins)
    {
        int n = weights.Length;
        if (mins.Length != n) throw new ArgumentException("SplitWidth: weights/mins 길이가 다름");
        var result = new float[n];
        float minSum = 0f, weightSum = 0f;
        for (int i = 0; i < n; i++) { minSum += mins[i]; weightSum += weights[i]; }
        float remaining = totalW - minSum - gap * Math.Max(0, n - 1);
        if (remaining < 0f) remaining = 0f;
        for (int i = 0; i < n; i++)
            result[i] = mins[i] + (weightSum <= 0f ? 0f : remaining * (weights[i] / weightSum));
        return result;
    }

    /// <summary>셀 폭 기준 한 줄에 들어가는 개수(탭·버튼 줄바꿈). 최소 1.</summary>
    public static int Wrap(float totalW, float cellW, float gap)
    {
        if (cellW <= 0f) return 1;
        int n = (int)Math.Floor((totalW + gap) / (cellW + gap));
        return Math.Max(1, n);
    }

    /// <summary>크기만 [min, max] 로 자른다(위치 불변). max &lt; min 이면 min 이 이긴다 — 화면이 최소보다 작아도 창은 사용 가능해야 함.</summary>
    public static Rect Clamp(Rect r, PanelBounds min, float maxW, float maxH)
    {
        float w = Math.Max(min.MinW, Math.Min(maxW, r.width));
        float h = Math.Max(min.MinH, Math.Min(maxH, r.height));
        return new Rect(r.x, r.y, w, h);
    }
}
