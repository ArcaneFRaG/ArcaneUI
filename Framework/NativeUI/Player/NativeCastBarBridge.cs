using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ArcanesGroupFrames.Framework.NativeUI.Player
{
    /// <summary>
    /// Hides only a separately identifiable native SCREEN-SPACE player cast bar.
    /// This does not deactivate objects, patch CastSpell, or affect world-space
    /// NPC/SimPlayer casting/nameplate displays.  The optional selection path is
    /// available because the game's cast-bar hierarchy is build-dependent.
    /// </summary>
    internal static class NativeCastBarBridge
    {
        private const string Key = "ArcaneUI.PlayerCastBar.NativeVisual";
        private static readonly List<Candidate> Candidates = new List<Candidate>();
        private static CanvasGroup _group;
        private static Transform _bound;
        private static bool _added;
        private static float _oldAlpha;
        private static bool _oldInteractable;
        private static bool _oldBlocksRaycasts;
        // A global RectTransform scan is slow in a heavily populated UI.
        // Remember native cast-bar objects across active/inactive cycles.
        private const float MissingBarRescanSeconds = 8f;
        private static float _nextScan;
        private static bool _didInitialDiscovery;
        private static bool _previousCasting;
        private static PlayerControl _castingOwner;
        private static CastSpell _castingComponent;
        private static Transform _cachedMatch;
        private static string _cachedMatchPath = "";
        private static bool _hideNative = true;
        private static string _chosenPath = "";
        private static string _boundPath = "";
        private static string _diagnostic = "Not scanned";
        private static int _selectedIndex = -1;
        private static bool _exactProbeDone;
        private const string ConfirmedNativePath = "DontDestroyOnLoad:UI/CastBarPar/CastBarBG";

        private struct Candidate
        {
            internal Transform Transform;
            internal string Path;
            internal int Score;
            internal bool Confident;
        }

        internal static bool HideNative { get { return _hideNative; } }
        internal static string SelectedPath { get { return string.IsNullOrEmpty(_chosenPath) ? "Automatic" : _chosenPath; } }
        internal static string Status
        {
            get
            {
                if (!_hideNative) return "Native cast bar is allowed to display.";
                if (_bound != null) return "Hidden: " + _boundPath;
                if (!string.IsNullOrEmpty(_chosenPath)) return "Waiting for selected native visual: " + _chosenPath;
                return _diagnostic;
            }
        }

        internal static void Load()
        {
            _hideNative = PlayerPrefs.GetInt(Key + ".Enabled", 1) != 0;
            _chosenPath = PlayerPrefs.GetString(Key + ".Path", "");
            _diagnostic = "Waiting for confirmed UI/CastBarPar/CastBarBG";
            _exactProbeDone = false;
        }

        internal static void SetHideNative(bool value)
        {
            _hideNative = value;
            PlayerPrefs.SetInt(Key + ".Enabled", value ? 1 : 0);
            PlayerPrefs.Save();
            if (!value) Restore();
            else
            {
                _nextScan = 0f;
                _didInitialDiscovery = false;
                _exactProbeDone = false;
            }
        }

        internal static void Update(bool replacementVisible)
        {
            if (!_hideNative || !replacementVisible || !NativeUiLayering.CanShowGameplayUi)
            {
                // Do not repeatedly reset state while the module is disabled.
                if (_bound != null || _group != null) Restore();
                return;
            }

            // IMPORTANT: the native object can become inactive between casts.
            // Keep its CanvasGroup instead of rediscovering the entire UI tree.
            if (_group == null)
            {
                if (_bound != null)
                {
                    _bound = null;
                    _boundPath = "";
                }

                // Prefer the exact cast-bar hierarchy confirmed in-game.
                // We hide CastBarPar, not just CastBarBG, so fill/text cannot leak.
                // One targeted inactive-object probe only; no per-frame global scan.
                if (!_exactProbeDone && _group == null && (string.IsNullOrEmpty(_chosenPath) || _chosenPath == ConfirmedNativePath ||
                     _chosenPath == "DontDestroyOnLoad:UI/CastBarPar"))
                {
                    _exactProbeDone = true;
                    TryBindConfirmedCastBar();
                }

                // Reuse a native node seen earlier (including inactive nodes).
                // Only a destroyed node / changed manual choice needs discovery.
                if (_group == null && _cachedMatch != null && _cachedMatch.gameObject.scene.IsValid() &&
                    (string.IsNullOrEmpty(_chosenPath) ||
                     string.Equals(_cachedMatchPath, _chosenPath, StringComparison.Ordinal)))
                    Bind(_cachedMatch, _cachedMatchPath);

                // A failed scan should NOT create a hitch every few seconds
                // during idle time. Retry on a new cast, subject to cooldown.
                // Stats.MySpells is not publicly accessible in every Erenshor
                // assembly. Native CastSpell lives on PlayerControl's GameObject;
                // cache that component and query only its read-only casting state.
                // This is an idle-time fallback for triggering a discovery scan,
                // not a spell execution hook.
                bool castingNow = false;
                try
                {
                    PlayerControl playerControl = GameData.PlayerControl;
                    if (_castingOwner != playerControl || _castingComponent == null)
                    {
                        _castingOwner = playerControl;
                        _castingComponent = playerControl != null
                            ? playerControl.GetComponent<CastSpell>() : null;
                    }
                    if (_castingComponent != null)
                        castingNow = _castingComponent.isCasting();
                }
                catch { castingNow = false; }
                bool castJustStarted = castingNow && !_previousCasting;
                _previousCasting = castingNow;
                if (_group == null && Time.unscaledTime >= _nextScan &&
                    (!_didInitialDiscovery || castJustStarted))
                {
                    _didInitialDiscovery = true;
                    _nextScan = Time.unscaledTime + MissingBarRescanSeconds;
                    Discover();
                    Transform match = null;
                    string path = null;
                    if (!string.IsNullOrEmpty(_chosenPath))
                    {
                        foreach (Candidate c in Candidates)
                            if (string.Equals(c.Path, _chosenPath, StringComparison.Ordinal))
                            { match = c.Transform; path = c.Path; break; }
                    }
                    else
                    {
                        // Auto-select only one unambiguous native cast root.
                        int highest = 0;
                        bool ambiguous = false;
                        foreach (Candidate c in Candidates)
                        {
                            if (!c.Confident) continue;
                            if (c.Score > highest)
                            {
                                highest = c.Score;
                                match = c.Transform;
                                path = c.Path;
                                ambiguous = false;
                            }
                            else if (c.Score == highest && !IsAncestor(c.Transform, match) &&
                                !IsAncestor(match, c.Transform))
                                ambiguous = true;
                            else if (c.Score == highest && IsAncestor(c.Transform, match))
                            {
                                match = c.Transform;
                                path = c.Path;
                            }
                        }
                        if (ambiguous)
                        {
                            match = null;
                            _diagnostic = "Multiple native cast-bar candidates. Scan and select the right visual.";
                        }
                    }

                    // Bind inactive native UI as well. It will already be
                    // hidden when the next cast activates that same object.
                    if (match != null)
                        Bind(match, path);
                    else if (Candidates.Count == 0)
                        _diagnostic = "Cast-bar visual not found. Scan during a cast to locate its path.";
                    else if (string.IsNullOrEmpty(_chosenPath) &&
                        !_diagnostic.StartsWith("Multiple", StringComparison.Ordinal))
                        _diagnostic = "Cast-bar candidate needs confirmation. Use Scan and Select.";
                }
            }

            if (_group != null)
            {
                // Native code can change the group during a cast. Only set
                // values when changed to avoid unnecessary Unity UI rebuilds.
                if (_group.alpha != 0f) _group.alpha = 0f;
                if (_group.blocksRaycasts) _group.blocksRaycasts = false;
                if (_group.interactable) _group.interactable = false;
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
                    _group.interactable = _oldInteractable;
                    _group.blocksRaycasts = _oldBlocksRaycasts;
                }
            }
            _group = null;
            _bound = null;
            _boundPath = "";
            _added = false;
            _nextScan = 0f;
            _didInitialDiscovery = false;
            _previousCasting = false;
        }

        private static void Bind(Transform selected, string path)
        {
            Restore();
            _bound = selected;
            _boundPath = path;
            _cachedMatch = selected;
            _cachedMatchPath = path;
            _group = selected.GetComponent<CanvasGroup>();
            _added = _group == null;
            if (_added) _group = selected.gameObject.AddComponent<CanvasGroup>();
            else
            {
                _oldAlpha = _group.alpha;
                _oldInteractable = _group.interactable;
                _oldBlocksRaycasts = _group.blocksRaycasts;
            }
            _diagnostic = "Native cast-bar visual identified";
        }

        private static void TryBindConfirmedCastBar()
        {
            RectTransform[] all;
            try { all = Resources.FindObjectsOfTypeAll<RectTransform>(); }
            catch { return; }
            foreach (RectTransform rect in all)
            {
                if (rect == null || rect.name != "CastBarBG" || !rect.gameObject.scene.IsValid()) continue;
                Transform parent = rect.parent;
                if (parent == null || parent.name != "CastBarPar" ||
                    parent.parent == null || parent.parent.name != "UI") continue;
                // Guard against accidentally hiding prefab instances or unrelated UI.
                if (HierarchyPath(rect) != ConfirmedNativePath) continue;
                Bind(parent, "DontDestroyOnLoad:UI/CastBarPar");
                _diagnostic = "Confirmed native CastBarBG: hiding parent CastBarPar";
                return;
            }
        }

        /// <summary>Populate paths even when the cast bar is inactive/hidden.</summary>
        private static void Discover()
        {
            Candidates.Clear();
            // Unlike FindObjectsOfType, this can locate a cast UI before the
            // first spell; reject assets/prefabs by requiring a valid scene.
            RectTransform[] all = Resources.FindObjectsOfTypeAll<RectTransform>();
            foreach (RectTransform rect in all)
            {
                if (rect == null || !rect.gameObject.scene.IsValid()) continue;
                // Reject most nodes BEFORE walking hierarchy or looking up
                // parent canvases (both much more expensive than name checks).
                int score = Score(rect.name);
                if (score <= 0) continue;
                if (rect.rect.width > 1000f || rect.rect.height > 500f) continue;
                UnityEngine.Canvas canvas = rect.GetComponentInParent<UnityEngine.Canvas>(true);
                if (canvas == null || canvas.renderMode == RenderMode.WorldSpace) continue;
                string path = HierarchyPath(rect);
                if (path.IndexOf("ArcaneUI", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    path.IndexOf("ArcanesGroupFrames", StringComparison.OrdinalIgnoreCase) >= 0)
                    continue;
                // Don't suppress native target, nameplate, spellbook or party/raid UI.
                if (ContainsPathToken(path, "NamePlate") || ContainsPathToken(path, "Target") ||
                    ContainsPathToken(path, "NPC") || ContainsPathToken(path, "SimPlayer") ||
                    ContainsPathToken(path, "Raid") || ContainsPathToken(path, "Party") ||
                    ContainsPathToken(path, "SpellBook") || ContainsPathToken(path, "Inventory"))
                    continue;
                bool confident = score >= 100;
                // Prioritise native UIElements player/HUD paths; never bind
                // generic canvases or game-wide UI roots by substring alone.
                if (path.IndexOf("/UIElements/", StringComparison.OrdinalIgnoreCase) >= 0)
                    score += 20;
                if (ContainsPathToken(path, "Player")) score += 10;
                Candidates.Add(new Candidate { Transform = rect, Path = path, Score = score, Confident = confident });
            }
            Candidates.Sort((a, b) => b.Score != a.Score ? b.Score.CompareTo(a.Score) :
                string.Compare(a.Path, b.Path, StringComparison.OrdinalIgnoreCase));
        }

        private static int Score(string name)
        {
            if (string.IsNullOrEmpty(name)) return 0;
            // Skip allocating normalized names for unrelated Unity UI objects.
            if (name.IndexOf("cast", StringComparison.OrdinalIgnoreCase) < 0 &&
                name.IndexOf("charg", StringComparison.OrdinalIgnoreCase) < 0)
                return 0;
            string n = name.ToLowerInvariant().Replace("_", "").Replace(" ", "").Replace("-", "");
            if (n == "castbar" || n == "castingbar" || n == "spellcastbar" ||
                n == "playercastbar" || n == "spellcastingbar" || n == "castmeter") return 120;
            if (n == "spellcast" || n == "playercasting" || n == "castprogress" ||
                n == "castprogressbar" || n == "castingprogress" ||
                n == "spellchargebar" || n == "castgauge") return 100;
            if (n.Contains("castbar") || n.Contains("castingbar") ||
                n.Contains("spellcast") || n.Contains("castprogress")) return 70;
            if (n.Contains("casting") || n.Contains("casttime") || n.Contains("chargebar")) return 35;
            return 0;
        }

        private static bool ContainsPathToken(string path, string token)
        {
            string[] names = path.Split('/');
            foreach (string name in names)
                if (name.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            return false;
        }
        private static bool IsAncestor(Transform possibleParent, Transform child)
        {
            if (possibleParent == null || child == null) return false;
            for (Transform t = child.parent; t != null; t = t.parent)
                if (t == possibleParent) return true;
            return false;
        }
        private static string HierarchyPath(Transform child)
        {
            var names = new List<string>();
            for (Transform t = child; t != null; t = t.parent) names.Add(t.name);
            names.Reverse();
            return child.gameObject.scene.name + ":" + string.Join("/", names.ToArray());
        }

        internal static void ScanAndLog()
        {
            Discover();
            _selectedIndex = -1;
            Plugin.LogDiagnosticWarning("ArcaneUI NativeCastBar SCAN: " + Candidates.Count +
                " candidate(s); current mode=" + SelectedPath);
            for (int i = 0; i < Candidates.Count && i < 40; i++)
            {
                Candidate c = Candidates[i];
                Plugin.LogDiagnosticWarning("ArcaneUI NativeCastBar [" + i + "] score=" + c.Score +
                    " active=" + (c.Transform != null && c.Transform.gameObject.activeInHierarchy) +
                    " path=" + c.Path);
            }
            if (Candidates.Count == 0)
                Plugin.LogDiagnosticWarning("ArcaneUI NativeCastBar: no matching screen-space UI element. " +
                    "Scan while actively casting; world-space nameplate bars are deliberately excluded.");
            _diagnostic = Candidates.Count == 0 ? "No screen-space cast-bar candidate detected" :
                Candidates.Count + " candidate(s) found. Use Select Next if automatic hiding fails.";
        }

        internal static void SelectNextCandidate()
        {
            if (Candidates.Count == 0) Discover();
            if (Candidates.Count == 0)
            {
                _diagnostic = "No candidate found; run Scan while casting.";
                return;
            }
            _selectedIndex++;
            // Cycle to Automatic after the last manually-selected candidate.
            if (_selectedIndex >= Candidates.Count) _selectedIndex = -1;
            _chosenPath = _selectedIndex < 0 ? "" : Candidates[_selectedIndex].Path;
            _cachedMatch = null;
            _cachedMatchPath = "";
            PlayerPrefs.SetString(Key + ".Path", _chosenPath);
            PlayerPrefs.Save();
            Restore();
        }
    }
}
