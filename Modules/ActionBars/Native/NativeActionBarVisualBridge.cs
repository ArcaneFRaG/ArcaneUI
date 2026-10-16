using System;
using UnityEngine;

namespace ArcanesGroupFrames.Modules.ActionBars.Native
{
    /// <summary>
    /// Optional reversible native hotbar visual suppression. Never disables
    /// Hotkeys or their gameplay execution. Bound once by exact hierarchy.
    /// </summary>
    internal static class NativeActionBarVisualBridge
    {
        private static Transform _native;
        private static CanvasGroup _group;
        private static bool _added;
        private static float _oldAlpha;
        private static bool _oldBlocks;
        private static bool _oldInteractive;
        private static float _nextProbe;
        private static bool _didProbe;

        internal static void Update(bool hide)
        {
            if (!hide) { Restore(); return; }
            if (_group == null)
            {
                if (!_didProbe || Time.unscaledTime >= _nextProbe)
                {
                    _didProbe = true;
                    _nextProbe = Time.unscaledTime + 15f;
                    // Prefer exact root path over expensive global UI searches.
                    GameObject found = GameObject.Find("UI/UIElements/Canvas/HotbarPar");
                    if (found != null) Bind(found.transform);
                    else
                    {
                        foreach (RectTransform rect in Resources.FindObjectsOfTypeAll<RectTransform>())
                        {
                            if (rect == null || rect.name != "HotbarPar" || !rect.gameObject.scene.IsValid()) continue;
                            if (rect.parent == null || rect.parent.name != "Canvas" ||
                                rect.parent.parent == null || rect.parent.parent.name != "UIElements") continue;
                            Bind(rect);
                            break;
                        }
                    }
                }
            }
            if (_group == null) return;
            if (_group.alpha != 0f) _group.alpha = 0f;
            if (_group.blocksRaycasts) _group.blocksRaycasts = false;
            if (_group.interactable) _group.interactable = false;
        }
        private static void Bind(Transform native)
        {
            if (native == null) return;
            if (_native != null && _native != native) Restore();
            _native = native;
            _group = native.GetComponent<CanvasGroup>();
            _added = _group == null;
            if (_added) _group = native.gameObject.AddComponent<CanvasGroup>();
            else
            {
                _oldAlpha = _group.alpha;
                _oldBlocks = _group.blocksRaycasts;
                _oldInteractive = _group.interactable;
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
                    _group.blocksRaycasts = _oldBlocks;
                    _group.interactable = _oldInteractive;
                }
            }
            _native = null;
            _group = null;
            _added = false;
            _didProbe = false;
            _nextProbe = 0f;
        }
    }
}
