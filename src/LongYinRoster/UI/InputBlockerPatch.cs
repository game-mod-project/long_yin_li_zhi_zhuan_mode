using HarmonyLib;
using UnityEngine;

namespace LongYinRoster.UI;

/// <summary>
/// 모드 창이 보이는 동안 게임의 마우스 input 호출을 차단한다.
///
/// 배경: Unity 의 Update 가 OnGUI 보다 먼저 호출되어 IMGUI 의 Event.Use() 만으로는
/// 게임 input 을 막을 수 없다. Time.timeScale = 0 도 일부 game 의 mouse-driven UI/
/// transition 은 무관 (마을 건물 클릭이 통과). 따라서 가장 robust 한 방법은
/// UnityEngine.Input.GetMouseButton* 자체를 Harmony Prefix 로 가로채 우리 창 영역
/// 안일 때 false 를 반환시키는 것.
///
/// LongYin InGame Cheat 가 같은 메서드들을 patch 하지만 Harmony 는 multiple prefix
/// 호환 — 두 mod 모두 자기 prefix 가 호출됨. priority 는 default.
/// </summary>
[HarmonyPatch(typeof(Input))]
public static class InputBlockerPatch
{
    [HarmonyPatch(nameof(Input.GetMouseButtonDown), typeof(int))]
    [HarmonyPrefix]
    private static bool GetMouseButtonDown_Prefix(int button, ref bool __result)
    {
        if (ModWindow.ShouldBlockMouse)
        {
            __result = false;
            return false;
        }
        return true;
    }

    [HarmonyPatch(nameof(Input.GetMouseButton), typeof(int))]
    [HarmonyPrefix]
    private static bool GetMouseButton_Prefix(int button, ref bool __result)
    {
        if (ModWindow.ShouldBlockMouse)
        {
            __result = false;
            return false;
        }
        return true;
    }

    [HarmonyPatch(nameof(Input.GetMouseButtonUp), typeof(int))]
    [HarmonyPrefix]
    private static bool GetMouseButtonUp_Prefix(int button, ref bool __result)
    {
        if (ModWindow.ShouldBlockMouse)
        {
            __result = false;
            return false;
        }
        return true;
    }

    // Mouse ScrollWheel — 게임이 zoom 에 사용. 모드 창 영역 안에서 휠 돌릴 때 차단
    // (FilePickerDialog 의 ScrollView 가 휠 받으면서 게임 zoom 이 함께 돌아가는 문제).
    [HarmonyPatch(nameof(Input.GetAxis), typeof(string))]
    [HarmonyPrefix]
    private static bool GetAxis_Prefix(string axisName, ref float __result)
    {
        if (ModWindow.ShouldBlockMouse && axisName == "Mouse ScrollWheel")
        {
            __result = 0f;
            return false;
        }
        return true;
    }

    [HarmonyPatch(nameof(Input.GetAxisRaw), typeof(string))]
    [HarmonyPrefix]
    private static bool GetAxisRaw_Prefix(string axisName, ref float __result)
    {
        if (ModWindow.ShouldBlockMouse && axisName == "Mouse ScrollWheel")
        {
            __result = 0f;
            return false;
        }
        return true;
    }
}
