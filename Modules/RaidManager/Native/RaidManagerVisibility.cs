using System.Collections.Generic;
using UnityEngine;
using ArcanesGroupFrames.Framework.Modules;
using Object = UnityEngine.Object;

namespace ArcanesGroupFrames
{
    internal static class RaidManagerVisibility
    {
        private static RaidManager _raidManager;

        private static GameObject _memberListRoot;

        private static CanvasGroup _memberListCanvasGroup;

        private static bool _lastRaidActive;

        private static bool _initialized;

        private static bool _lastReplacementEnabled;
        private static bool _originalCaptured;
        private static float _originalAlpha;
        private static bool _originalInteractable;
        private static bool _originalBlocksRaycasts;

        private static float _resolveRetryTimer;


        internal static bool IsVisible
        {
            get;
            private set;
        } = true;


        // ============================================================
        // AUTOMATIC RAID STATE
        // ============================================================

        internal static void Initialize()
        {
            _initialized =
                true;

            _lastRaidActive =
                GameData.RaidActive;

            _lastReplacementEnabled = ArcaneUiModuleSettings.RaidFramesEnabled;

            _resolveRetryTimer =
                0f;


            if (_lastRaidActive && _lastReplacementEnabled)
            {
                RefreshForActiveRaid();
            }
            else
            {
                IsVisible =
                    true;
            }
        }


        internal static void UpdateAutomatic()
        {
            bool raidActive =
                GameData.RaidActive;

            bool replacing = ArcaneUiModuleSettings.RaidFramesEnabled;

            if (!_initialized)
            {
                Initialize();
                return;
            }


            if (replacing != _lastReplacementEnabled)
            {
                _lastReplacementEnabled = replacing;
                _resolveRetryTimer = 0f;
                if (raidActive && replacing) RefreshForActiveRaid();
                else RestoreAndRelease();
            }

            if (raidActive != _lastRaidActive)
            {
                _lastRaidActive =
                    raidActive;

                _resolveRetryTimer =
                    0f;


                if (raidActive && replacing)
                {
                    RefreshForActiveRaid();
                }
                else
                {
                    RestoreAndRelease();
                }


                return;
            }


            if (!raidActive || !replacing)
            {
                return;
            }


            // Scene loads can recreate the native RaidManager without
            // changing RaidActive. Retry at a low frequency until the
            // native member-list hierarchy is available again.
            if (_memberListCanvasGroup == null)
            {
                _resolveRetryTimer -=
                    Time.unscaledDeltaTime;


                if (_resolveRetryTimer <= 0f)
                {
                    _resolveRetryTimer =
                        0.75f;

                    RefreshForActiveRaid();
                }
            }
        }


        internal static void RefreshForModuleSetting()
        {
            UpdateAutomatic();
        }

        private static void RefreshForActiveRaid()
        {
            // A previous native hierarchy may have been replaced on scene load.
            // Do not carry its visibility snapshot to a new object.
            RestoreOriginalState();
            _originalCaptured = false;
            _raidManager =
                null;

            _memberListRoot =
                null;

            _memberListCanvasGroup =
                null;


            SetVisible(
                false);
        }


        private static void RestoreAndRelease()
        {
            RestoreOriginalState();


            IsVisible =
                true;

            _raidManager =
                null;

            _memberListRoot =
                null;

            _memberListCanvasGroup =
                null;
            _originalCaptured = false;
        }


        // ============================================================
        // VISIBILITY
        // ============================================================

        internal static void SetVisible(
            bool visible)
        {
            IsVisible =
                visible;


            if (!TryResolve())
            {
                Plugin.LogWarning(
                    "Could not resolve the native raid-member list.");

                return;
            }


            _memberListCanvasGroup.alpha =
                visible
                    ? 1f
                    : 0f;

            _memberListCanvasGroup.interactable =
                visible;

            _memberListCanvasGroup.blocksRaycasts =
                visible;


}


        internal static void Refresh()
        {
            _raidManager =
                null;

            _memberListRoot =
                null;

            _memberListCanvasGroup =
                null;


            SetVisible(
                IsVisible);
        }


        // ============================================================
        // RESOLVE NATIVE MEMBER LIST
        // ============================================================

        private static bool TryResolve()
        {
            if (_raidManager != null &&
                _memberListRoot != null &&
                _memberListCanvasGroup != null)
            {
                return true;
            }


            _raidManager =
                Object.FindObjectOfType<RaidManager>(
                    true);

            if (_raidManager == null)
            {
                Plugin.LogWarning(
                    "Could not find RaidManager.");

                return false;
            }


            RaidMemberSlot[] slots =
                _raidManager
                    .GetComponentsInChildren<RaidMemberSlot>(
                        true);


            if (slots == null ||
                slots.Length == 0)
            {
                Plugin.LogWarning(
                    "RaidManager contains no RaidMemberSlot components.");

                return false;
            }


            Transform commonParent =
                FindCommonParent(
                    slots);


            if (commonParent == null)
            {
                Plugin.LogWarning(
                    "Could not determine common parent for raid-member slots.");

                return false;
            }


            _memberListRoot =
                commonParent.gameObject;


_memberListCanvasGroup =
                _memberListRoot
                    .GetComponent<CanvasGroup>();


            if (_memberListCanvasGroup == null)
            {
                _memberListCanvasGroup =
                    _memberListRoot
                        .AddComponent<CanvasGroup>();


}


            // Only snapshot the original native state once per resolved root.
            // A replacement must never overwrite the saved native values.
            if (!_originalCaptured)
            {
                _originalAlpha = _memberListCanvasGroup.alpha;
                _originalInteractable = _memberListCanvasGroup.interactable;
                _originalBlocksRaycasts = _memberListCanvasGroup.blocksRaycasts;
                _originalCaptured = true;
            }

            return true;
        }

        private static void RestoreOriginalState()
        {
            if (_memberListCanvasGroup == null || !_originalCaptured) return;
            _memberListCanvasGroup.alpha = _originalAlpha;
            _memberListCanvasGroup.interactable = _originalInteractable;
            _memberListCanvasGroup.blocksRaycasts = _originalBlocksRaycasts;
        }


        // ============================================================
        // COMMON PARENT
        // ============================================================

        private static Transform FindCommonParent(
            RaidMemberSlot[] slots)
        {
            if (slots == null ||
                slots.Length == 0)
            {
                return null;
            }


            List<Transform> firstPath =
                BuildParentPath(
                    slots[0].transform);


            for (int i = 0;
                 i < firstPath.Count;
                 i++)
            {
                Transform candidate =
                    firstPath[i];


                bool containsAll =
                    true;


                for (int s = 1;
                     s < slots.Length;
                     s++)
                {
                    if (!IsDescendantOf(
                        slots[s].transform,
                        candidate))
                    {
                        containsAll =
                            false;

                        break;
                    }
                }


                if (containsAll)
                {
                    return candidate;
                }
            }


            return null;
        }


        private static List<Transform> BuildParentPath(
            Transform child)
        {
            List<Transform> result =
                new List<Transform>();


            Transform current =
                child;


            while (current != null)
            {
                result.Add(
                    current);

                current =
                    current.parent;
            }


            return result;
        }


        private static bool IsDescendantOf(
            Transform child,
            Transform possibleParent)
        {
            Transform current =
                child;


            while (current != null)
            {
                if (current ==
                    possibleParent)
                {
                    return true;
                }

                current =
                    current.parent;
            }


            return false;
        }


        // ============================================================
        // DIAGNOSTICS
        // ============================================================

        private static string GetHierarchyPath(
            Transform transform)
        {
            if (transform == null)
            {
                return "<null>";
            }


            string path =
                transform.name;


            Transform current =
                transform.parent;


            while (current != null)
            {
                path =
                    current.name +
                    "/" +
                    path;

                current =
                    current.parent;
            }


            return path;
        }


        // ============================================================
        // RESET
        // ============================================================

        internal static void Reset()
        {
            RestoreOriginalState();


            _raidManager =
                null;

            _memberListRoot =
                null;

            _memberListCanvasGroup =
                null;


            IsVisible =
                true;

            _lastRaidActive =
                false;

            _initialized =
                false;

            _resolveRetryTimer =
                0f;
            _originalCaptured = false;
            _lastReplacementEnabled = false;
        }
    }
}