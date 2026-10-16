using HarmonyLib;
using UnityEngine;

namespace ArcanesGroupFrames
{
    internal static class NativeGroupFramesVisibility
    {
        internal static void Apply(SimPlayerGrouping grouping)
        {
            if (grouping == null)
            {
                return;
            }

            bool hide = PartyFramesUI.IsReplacementActive();

            if (hide)
            {
                SetActive(grouping.ParOne, false);
                SetActive(grouping.ParTwo, false);
                SetActive(grouping.ParThree, false);
                SetActive(grouping.ParFour, false);
            }
        }

        internal static void Reset()
        {
            SimPlayerGrouping grouping = GameData.SimPlayerGrouping;
            if (grouping == null)
            {
                return;
            }

            RestoreSlot(grouping.ParOne, 0);
            RestoreSlot(grouping.ParTwo, 1);
            RestoreSlot(grouping.ParThree, 2);
            RestoreSlot(grouping.ParFour, 3);
        }

        private static void RestoreSlot(GameObject obj, int index)
        {
            if (obj == null)
            {
                return;
            }

            bool active = false;

            if (GameData.GroupMembers != null &&
                index >= 0 &&
                index < 4)
            {
                active = GameData.GroupMembers[index] != null;
            }

            SetActive(obj, active);
        }

        private static void SetActive(GameObject obj, bool active)
        {
            if (obj != null && obj.activeSelf != active)
            {
                obj.SetActive(active);
            }
        }
    }

    [HarmonyPatch(typeof(SimPlayerGrouping), "FixedUpdate")]
    internal static class Patch_HideNativeGroupFrames
    {
        private static void Postfix(SimPlayerGrouping __instance)
        {
            NativeGroupFramesVisibility.Apply(__instance);
        }
    }
}
