using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ArcanesGroupFrames.Framework.NativeUI.Target
{
    /// <summary>
    /// VISUAL-ONLY suppression of the candidate native NewTargetWindow root.
    /// Never deactivates a native GameObject or MonoBehaviour. The native
    /// CanvasGroup is restored when the replacement is off or loading begins.
    /// </summary>
    internal static class NativeTargetHudBridge
    {
        private const string Key = "ArcaneUI.TargetFrame.NativeHud";
        internal const string ConfirmedPath =
            "DontDestroyOnLoad:UI/UIElements/TargCanv/NewTargetWindow";
        private static CanvasGroup _group;
        private static bool _added;
        private static float _oldAlpha;
        private static bool _oldInteractable, _oldBlocksRaycasts;
        private static Transform _bound;
        private static string _path;
        private static bool _suppress;
        private static float _nextSearch;

        internal static string Path { get { return _path ?? ""; } }
        internal static bool SuppressNative { get { return _suppress; } }
        internal static bool IsBound { get { return _bound != null && _group != null; } }

        internal static void Load()
        {
            // Candidate path reported in-game; do not suppress until located exactly once.
            _path = ConfirmedPath;
            // Test build: preserve the native target window unless the user
            // explicitly enables hiding on the Target Frame > Native HUD tab.
            _suppress = PlayerPrefs.GetInt(Key + ".AutoSuppressConfirmed", 0) == 1;
        }
        internal static void SetPath(string path)
        {
            Restore();
            _path = path ?? "";
            PlayerPrefs.SetString(Key + ".Path", _path);
            PlayerPrefs.Save();
        }
        internal static void SetSuppress(bool value)
        {
            _suppress = value;
            PlayerPrefs.SetInt(Key + ".AutoSuppressConfirmed", value ? 1 : 0);
            PlayerPrefs.Save();
            if (!value) Restore();
        }
        internal static void Update(bool targetFrameVisible)
        {
            if (!targetFrameVisible || !_suppress || !NativeUiLayering.CanShowGameplayUi)
            {
                Restore();
                return;
            }
            if (_bound == null && Time.unscaledTime >= _nextSearch)
            {
                _nextSearch = Time.unscaledTime + 1f;
                var candidate = Locate(ConfirmedPath);
                if (candidate == null || IsUnsafe(candidate)) return;
                Bind(candidate);
            }
            if (_group != null)
            {
                // The original HUD may set its own CanvasGroup properties on Update.
                _group.alpha = 0f;
                _group.blocksRaycasts = false;
                _group.interactable = false;
            }
        }
        internal static void Restore()
        {
            if (_group != null)
            {
                if (_added) UnityEngine.Object.Destroy(_group);
                else
                {
                    _group.alpha = _oldAlpha;
                    _group.blocksRaycasts = _oldBlocksRaycasts;
                    _group.interactable = _oldInteractable;
                }
            }
            _group = null;
            _bound = null;
            _added = false;
            _nextSearch = 0f;
        }
        private static void Bind(Transform selected)
        {
            Restore();
            _bound = selected;
            _group = selected.GetComponent<CanvasGroup>();
            _added = _group == null;
            if (_added) _group = selected.gameObject.AddComponent<CanvasGroup>();
            else
            {
                _oldAlpha = _group.alpha;
                _oldInteractable = _group.interactable;
                _oldBlocksRaycasts = _group.blocksRaycasts;
            }
        }
        private static bool IsUnsafe(Transform t)
        {
            if (t == null || t.parent == null) return true;
            var component = t.GetComponent<UnityEngine.Canvas>();
            if (component != null) return true;
            var name = t.name.ToLowerInvariant();
            return name == "canvas" || name == "hud" || name == "ui" ||
                   name == "mainui" || name == "screen" || name == "root";
        }
        private static string HierarchyPath(Transform t)
        {
            var names = new List<string>();
            for (Transform cursor = t; cursor != null; cursor = cursor.parent)
                names.Add(cursor.name);
            names.Reverse();
            return t.gameObject.scene.name + ":" + string.Join("/", names.ToArray());
        }
        private static Transform Locate(string path)
        {
            Transform found = null;
            foreach (var r in UnityEngine.Object.FindObjectsOfType<RectTransform>())
            {
                if (r == null || !r.gameObject.activeInHierarchy) continue;
                if (HierarchyPath(r) != path) continue;
                if (found != null) return null; // ambiguous: do not hide either.
                found = r;
            }
            return found;
        }
        internal static List<string> CandidatePaths()
        {
            var found = new List<string>();
            foreach (var r in UnityEngine.Object.FindObjectsOfType<RectTransform>())
            {
                if (r == null || !r.gameObject.activeInHierarchy || IsUnsafe(r)) continue;
                string path = HierarchyPath(r);
                string name = r.name.ToLowerInvariant();
                if (path.IndexOf("ArcaneUI", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    path.IndexOf("ArcanesGroupFrames", StringComparison.OrdinalIgnoreCase) >= 0)
                    continue;
                if (name.Contains("player") || name.Contains("health") ||
                    name.Contains("hpbar") || name.Contains("mana") || name.Contains("portrait"))
                    found.Add(path);
                if (found.Count >= 60) break;
            }
            found.Sort(StringComparer.OrdinalIgnoreCase);
            return found;
        }
    }
}
