using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using ArcanesGroupFrames.Framework.Modules;
using ArcanesGroupFrames;
using ArcanesGroupFrames.Framework.Theming;
using ArcanesGroupFrames.Framework.Canvas;
using ArcanesGroupFrames.Modules.CharacterSheet.Configuration;

namespace ArcanesGroupFrames.Modules.CharacterSheet.Native
{
    /// <summary>
    /// Phase 10.5: Decorates the ACTUAL native character window without replacing
    /// any interactive equipment item, stat readout, tooltip, or action handler.
    /// Only named/confirmed native window roots are touched; nothing is hidden.
    /// Unknown Erenshor builds require manual window selection in /arcaneui.
    /// </summary>
    internal sealed class NativeCharacterSheetDecorator
    {
        private readonly ThemeManager _themes;
        private readonly ArcaneCanvasManager _canvas;
        private CharacterSheetBoundedView _overview;
        private readonly List<string> _lastNativeTexts = new List<string>();
        private readonly List<string> _lastNativeSlots = new List<string>();
        private RectTransform _nativeRoot;
        private GameObject _decoration;
        private readonly List<GameObject> _slotDecorations = new List<GameObject>();
        private Image _nativeBackground;
        private Color _originalNativeColor;
        private float _nextLookup;
        private sealed class Candidate
        {
            internal string Path;
            internal int Score;
            internal bool Active;
            internal string Origin;
            internal string Display
            {
                get
                {
                    string role = Path.EndsWith("/InvPar", StringComparison.Ordinal) ? "CHARACTER + INVENTORY ROOT | " :
                        Path.EndsWith("/InvPar/PlayerInv", StringComparison.Ordinal) ? "INVENTORY CONTENT | " :
                        Path.EndsWith("/InvPar/PlayerInv/CosmeticPanel", StringComparison.Ordinal) ? "COSMETICS PANEL | " : "";
                    return role + (Active ? "OPEN" : "INACTIVE") + " | score " + Score + " | " + Origin + " | " + Path;
                }
            }
        }

        private readonly List<Candidate> _candidates = new List<Candidate>();
        private readonly Dictionary<string, Candidate> _captured = new Dictionary<string, Candidate>(StringComparer.Ordinal);
        private readonly HashSet<int> _watchBaseline = new HashSet<int>();
        private readonly HashSet<int> _watchRecorded = new HashSet<int>();
        private int _candidateIndex;
        private float _watchUntil;
        private float _nextWatchPoll;
        private string _status = "No character-sheet window selected.";

        internal NativeCharacterSheetDecorator(ThemeManager themes, ArcaneCanvasManager canvas)
        { _themes = themes; _canvas = canvas; }
        internal string Status
        {
            get
            {
                if (!ArcaneUiModuleSettings.CharacterSheetEnabled)
                    return "ArcaneUI Character Sheet is disabled under Character > General. Native character sheet remains available.";
                if (_overview != null)
                {
                    if (CharacterSheetSettings.SafeNativeMode)
                        return "Native character sheet fallback enabled. The bounded two-column replacement is paused. " + _overview.Status;
                    if (!CharacterSheetSettings.ShowOverview)
                        return "Bounded two-column character sheet disabled under Character > Appearance. " + _overview.Status;
                    return _status + " " + _overview.Status;
                }
                return _status;
            }
        }
        internal int OverviewNativeTabs { get { return _overview != null ? _overview.NativeTabs : 0; } }
        internal bool OverviewIsVisible { get { return _overview != null && _overview.IsCustomVisible; } }
        internal int OverviewStats { get { return _overview != null ? _overview.AvailableFields : 0; } }
        internal int OverviewNativeControls { get { return _overview != null ? _overview.NativeSlots : 0; } }
        internal void InspectNativeSlotControls()
        {
            if (_overview != null) { _overview.LogSlotDiagnostics(); return; }
            if (_lastNativeSlots.Count == 0)
            {
                _status = "Open the native character sheet once, then inspect the cached equipment hierarchy.";
                return;
            }
            Plugin.LogInfo("ArcaneUI cached native equipment controls: " + _lastNativeSlots.Count);
            for (int i = 0; i < _lastNativeSlots.Count; i++)
                Plugin.LogInfo("ArcaneUI equipment node " + (i + 1) + ": " + _lastNativeSlots[i]);
        }
        internal void InspectNativeStats()
        {
            if (_overview != null) { _overview.InspectNativeValues(); return; }
            if (_lastNativeTexts.Count == 0)
            {
                _status = "Open the character sheet with Character enabled to collect native values, then inspect again.";
                return;
            }
            Plugin.LogInfo("ArcaneUI last native character snapshot: " + _lastNativeTexts.Count + " labels from last open character window.");
            for (int i = 0; i < _lastNativeTexts.Count; i++)
                Plugin.LogInfo("ArcaneUI character native text " + (i + 1) + ": " + _lastNativeTexts[i]);
        }
        internal string SelectedPath { get { return CharacterSheetSettings.NativePath; } }
        internal int CandidateCount { get { return _candidates.Count; } }
        internal string CandidateName
        {
            get { return _candidates.Count == 0 ? "No candidates" : _candidates[Mathf.Clamp(_candidateIndex, 0, _candidates.Count - 1)].Display; }
        }
        internal int CandidateIndex { get { return _candidateIndex + 1; } }
        internal bool CaptureArmed { get { return _watchUntil > Time.unscaledTime; } }
        internal int CapturedCount { get { return _captured.Count; } }

        private static bool IsOwn(Transform rect)
        {
            for (Transform t = rect; t != null; t = t.parent)
            {
                if (t.name.StartsWith("ArcaneUI", StringComparison.OrdinalIgnoreCase) ||
                    t.name.StartsWith("ArcaneUi", StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        internal static string FullPath(Transform t)
        {
            var names = new List<string>();
            for (Transform current = t; current != null; current = current.parent)
                names.Add(current.name);
            names.Reverse();
            return t.gameObject.scene.name + ":" + string.Join("/", names.ToArray());
        }

        // Intentionally independent of GameObject names. Erenshor can use a
        // generic panel name or have its page inactive while the options modal
        // is open. We NEVER automatically bind a discovered window.
        private static bool IsSafePanel(RectTransform rect)
        {
            if (rect == null || IsOwn(rect)) return false;
            if (rect.GetComponent<Canvas>() != null) return false; // never style an entire UI canvas
            string n = rect.name.ToLowerInvariant();
            if (n.Contains("button") || n.Contains("tooltip") || n.Contains("icon") ||
                n.Contains("label") || n.Contains("scrollbar") || n.Contains("backdrop")) return false;
            float width = rect.rect.width, height = rect.rect.height;
            if (width < 120f || height < 110f) return false;
            return true;
        }

        // Known native hierarchy observed from Erenshor's UI log:
        // DontDestroyOnLoad:UI/UIElements/InvPar/PlayerInv/CosmeticPanel
        // InvPar owns the combined character + inventory UI, PlayerInv is the
        // inventory area. The parent/child structure is required, not a name alone.
        internal static RectTransform FindKnownCharacterRoot(bool includeInactive)
        {
            RectTransform unique = null;
            foreach (RectTransform rect in UnityEngine.Object.FindObjectsOfType<RectTransform>(includeInactive))
            {
                if (rect == null || rect.name != "InvPar" || IsOwn(rect) || rect.GetComponent<Canvas>() != null) continue;
                Transform parent = rect.parent;
                if (parent == null || parent.name != "UIElements") continue;
                Transform child = rect.Find("PlayerInv");
                if (child == null || child.GetComponent<RectTransform>() == null) continue;
                if (unique != null) return null; // ambiguous: never select arbitrary UI
                unique = rect;
            }
            return unique;
        }

        internal string KnownCharacterPath
        {
            get
            {
                RectTransform known = FindKnownCharacterRoot(true);
                return known != null ? FullPath(known) : "";
            }
        }

        internal void SelectKnownCharacterRoot()
        {
            string path = KnownCharacterPath;
            if (string.IsNullOrEmpty(path))
            {
                _status = "The InvPar / PlayerInv hierarchy is not present or is ambiguous. Open the native inventory and rescan.";
                return;
            }
            SetSelectedPath(path);
            _status = "Selected verified character + inventory root: " + path;
        }

        private static bool HasHint(string s)
        {
            return s.Contains("character") || s.Contains("equipment") ||
                s.Contains("paperdoll") || s.Contains("charwindow") ||
                s.Contains("charscreen") || s.Contains("charsheet") ||
                s.Contains("statswindow") || s.Contains("statsscreen") ||
                s.Contains("statpanel") || s.Contains("gearwindow");
        }

        private static int ScorePanel(RectTransform rect, out int children)
        {
            children = 0;
            if (!IsSafePanel(rect)) return -1;
            string name = rect.name.ToLowerInvariant();
            int score = HasHint(name) ? 9 : 0;
            if (name.Contains("window") || name.Contains("panel") ||
                name.Contains("sheet") || name.Contains("page")) score++;
            if (rect.name == "InvPar" && rect.Find("PlayerInv") != null &&
                rect.parent != null && rect.parent.name == "UIElements") score += 50;
            else if (rect.name == "PlayerInv" && rect.parent != null &&
                rect.parent.name == "InvPar") score += 30;
            // Only inspect a bounded number of descendants for discovery.
            // Scans are user-triggered or limited to 45 seconds of capture.
            var nodes = rect.GetComponentsInChildren<RectTransform>(true);
            children = nodes.Length;
            if (children < 5) return -1;
            int gear = 0, stats = 0;
            for (int i = 1; i < nodes.Length && i < 180; i++)
            {
                if (nodes[i] == null || IsOwn(nodes[i])) continue;
                string n = nodes[i].name.ToLowerInvariant();
                if (n.Contains("headslot") || n.Contains("chestslot") ||
                    n.Contains("primaryslot") || n.Contains("secondaryslot") ||
                    n.Contains("equipslot") || n.Contains("gear_slot") ||
                    n.Contains("equipment") || n.Contains("auraslot")) gear++;
                if (n.Contains("strength") || n.Contains("dexterity") ||
                    n.Contains("intelligence") || n.Contains("wisdom") ||
                    n.Contains("resist") || n.Contains("armorclass") ||
                    n.Contains("stattext") || n.Contains("statlabel")) stats++;
            }
            score += Mathf.Min(gear, 4) * 2 + Mathf.Min(stats, 4) * 2;
            if (gear > 0 && stats > 0) score += 4;
            if (rect.gameObject.activeInHierarchy) score += 2;
            return score;
        }

        private static Candidate Describe(RectTransform rect, string origin)
        {
            int children;
            int score = ScorePanel(rect, out children);
            if (score < 0) return null;
            return new Candidate
            {
                Path = FullPath(rect), Active = rect.gameObject.activeInHierarchy,
                Score = score, Origin = origin
            };
        }

        private static int SortCandidates(Candidate a, Candidate b)
        {
            int score = b.Score.CompareTo(a.Score);
            if (score != 0) return score;
            if (a.Active != b.Active) return a.Active ? -1 : 1;
            return string.Compare(a.Path, b.Path, StringComparison.OrdinalIgnoreCase);
        }

        private void InsertCandidate(Dictionary<string, Candidate> map, Candidate candidate)
        {
            if (candidate == null) return;
            Candidate previous;
            if (!map.TryGetValue(candidate.Path, out previous) || candidate.Score > previous.Score ||
                (candidate.Active && !previous.Active)) map[candidate.Path] = candidate;
        }

        internal void ScanCandidates(bool includeGeneric = false)
        {
            var discovered = new Dictionary<string, Candidate>(StringComparer.Ordinal);
            foreach (Candidate c in _captured.Values) InsertCandidate(discovered, c);
            int inspected = 0;
            int semantic = 0;
            foreach (RectTransform rect in UnityEngine.Object.FindObjectsOfType<RectTransform>(true))
            {
                if (!IsSafePanel(rect)) continue;
                inspected++;
                Candidate c = Describe(rect, "scan");
                if (c == null) continue;
                bool match = c.Score >= (c.Active ? 6 : 4);
                if (match) semantic++;
                if (!match && !includeGeneric) continue;
                InsertCandidate(discovered, c);
            }
            _candidates.Clear();
            _candidates.AddRange(discovered.Values);
            _candidates.Sort(SortCandidates);
            if (_candidates.Count > 60) _candidates.RemoveRange(60, _candidates.Count - 60);
            _candidateIndex = 0;
            for (int i = 0; i < _candidates.Count; i++)
                if (_candidates[i].Path == CharacterSheetSettings.NativePath) _candidateIndex = i;
            _status = _candidates.Count == 0
                ? "No named/semantic character panels identified (" + inspected + " eligible panels inspected). " +
                  "Use ALL PANELS or ARM CAPTURE; the native sheet may have a generic name or close with settings."
                : _candidates.Count + " candidate(s); " + semantic + " match(es) from scan, " +
                  _captured.Count + " captured. Select only the actual character window.";
            Plugin.LogInfo("ArcaneUI character discovery: " + _status);
            for (int i = 0; i < _candidates.Count && i < 35; i++)
                Plugin.LogInfo("ArcaneUI character candidate " + (i + 1) + "/" + _candidates.Count + ": " + _candidates[i].Display);
        }

        // Settings has an input-blocking modal: arming first and then closing
        // options allows a native character window to actually open. The
        // inspector remembers newly activated panels for later explicit review.
        internal void ArmCapture()
        {
            _captured.Clear();
            _candidates.Clear();
            _candidateIndex = 0;
            _watchBaseline.Clear();
            _watchRecorded.Clear();
            foreach (RectTransform rect in UnityEngine.Object.FindObjectsOfType<RectTransform>(true))
                if (rect != null && rect.gameObject.activeInHierarchy && !IsOwn(rect))
                    _watchBaseline.Add(rect.GetInstanceID());
            _watchUntil = Time.unscaledTime + 45f;
            _nextWatchPoll = 0f;
            _status = "Capture armed for 45 seconds. Open the native Character window now, then return to /arcaneui > Character > Native.";
            Plugin.LogInfo("ArcaneUI character detection armed: opening the native window will be recorded. Nothing will be auto-selected or hidden.");
        }

        private void WatchOpenWindows()
        {
            if (_watchUntil <= 0f) return;
            if (Time.unscaledTime >= _watchUntil)
            {
                _watchUntil = 0f;
                _watchBaseline.Clear();
                _watchRecorded.Clear();
                _status = "Capture finished: " + _captured.Count + " panel(s) recorded. Use REVIEW CAPTURE or ALL PANELS.";
                Plugin.LogInfo("ArcaneUI character detection finished with " + _captured.Count + " new panels.");
                return;
            }
            if (Time.unscaledTime < _nextWatchPoll) return;
            _nextWatchPoll = Time.unscaledTime + .5f;
            foreach (RectTransform rect in UnityEngine.Object.FindObjectsOfType<RectTransform>())
            {
                if (rect == null || _watchBaseline.Contains(rect.GetInstanceID()) ||
                    _watchRecorded.Contains(rect.GetInstanceID()) || !IsSafePanel(rect)) continue;
                Candidate c = Describe(rect, "opened during capture");
                if (c != null)
                {
                    _watchRecorded.Add(rect.GetInstanceID());
                    InsertCandidate(_captured, c);
                }
            }
        }

        internal void ReviewCapture()
        {
            _candidates.Clear();
            _candidates.AddRange(_captured.Values);
            _candidates.Sort(SortCandidates);
            if (_candidates.Count > 60) _candidates.RemoveRange(60, _candidates.Count - 60);
            _candidateIndex = 0;
            _status = _candidates.Count == 0
                ? "No newly activated panels captured. Try ALL PANELS; the native window may already be active."
                : "Captured " + _candidates.Count + " newly active panel(s). Select the actual character window.";
            Plugin.LogInfo("ArcaneUI character review: " + _status);
            for (int i = 0; i < _candidates.Count && i < 35; i++)
                Plugin.LogInfo("ArcaneUI captured panel " + (i + 1) + ": " + _candidates[i].Display);
        }

        internal void NextCandidate()
        {
            if (_candidates.Count > 0) _candidateIndex = (_candidateIndex + 1) % _candidates.Count;
        }

        internal void SelectCandidate()
        {
            if (_candidates.Count == 0) return;
            SetSelectedPath(_candidates[_candidateIndex].Path);
            _watchUntil = 0f;
            _watchBaseline.Clear();
            _watchRecorded.Clear();
        }

        internal void SetSelectedPath(string path)
        {
            Restore();
            CharacterSheetSettings.SetNativePath(path);
            _nextLookup = 0f;
            _status = string.IsNullOrEmpty(path) ? "Native window selection cleared." : "Saved native window: " + path;
        }

        internal void Update()
        {
            WatchOpenWindows(); // Works while the styling module is disabled.
            if (!ArcaneUiModuleSettings.CharacterSheetEnabled || !NativeUiLayering.CanShowGameplayUi)
            {
                Restore();
                return;
            }
            if (_nativeRoot != null)
            {
                if (!_nativeRoot.gameObject.activeInHierarchy || IsOwn(_nativeRoot)) { Restore(); return; }
                if (_overview != null) _overview.Update();
                return;
            }
            if (Time.unscaledTime < _nextLookup) return;
            _nextLookup = Time.unscaledTime + 3f;
            RectTransform found = null;
            string exact = CharacterSheetSettings.NativePath;
            if (string.IsNullOrEmpty(exact))
            {
                // Auto-bind ONLY the confirmed InvPar -> PlayerInv relationship.
                // This is never an arbitrary scored panel match or global UI suppression.
                found = FindKnownCharacterRoot(false);
                if (found == null)
                {
                    _status = "Waiting for Erenshor's InvPar / PlayerInv character + inventory window.";
                    return;
                }
                Bind(found);
                return;
            }
            // A persisted selection of PlayerInv/CosmeticPanel predating our
            // hierarchy discovery is not a valid CHARACTER root. Keep it saved
            // for inspection but refuse to decorate the wrong panel.
            if (exact.EndsWith("/InvPar/PlayerInv", StringComparison.Ordinal) ||
                exact.EndsWith("/InvPar/PlayerInv/CosmeticPanel", StringComparison.Ordinal))
            {
                // Repair legacy saved paths that point inside inventory. They
                // prevented any character UI from binding after an upgrade.
                RectTransform known = FindKnownCharacterRoot(false);
                if (known != null)
                {
                    CharacterSheetSettings.SetNativePath(FullPath(known));
                    _status = "Repaired old inventory-child binding to the shared InvPar character root.";
                    Bind(known);
                    return;
                }
                _status = "Saved character binding points inside inventory; waiting for InvPar to become available.";
                return;
            }
            foreach (RectTransform r in UnityEngine.Object.FindObjectsOfType<RectTransform>())
            {
                // Explicitly selected paths can have generic names, so we do
                // not reapply the name heuristic. Safety still forbids canvases.
                if (!IsSafePanel(r) || FullPath(r) != exact) continue;
                if (found != null) { _status = "Ambiguous native windows; no style applied."; return; }
                found = r;
            }
            if (found != null) Bind(found);
            else _status = "Waiting for selected native character window to open: " + exact;
        }

        private void Bind(RectTransform root)
        {
            Restore();
            _nativeRoot = root;
            _nativeBackground = root.GetComponent<Image>(); // only the selected root; never item icons
            if (_nativeBackground != null) _originalNativeColor = _nativeBackground.color;
            ApplyStyle();
            _status = "Bound to " + FullPath(root) + ". Native equipment and inventory controls remain interactive.";
        }

        internal void ApplyStyle()
        {
            if (_nativeRoot == null) return;
            ClearDecorations();
            UiTheme theme = _themes != null ? _themes.Current : null;
            Color accent = CharacterSheetSettings.InheritTheme && theme != null
                ? theme.Accent : CharacterSheetSettings.Parse(CharacterSheetSettings.AccentHex, new Color(.82f, .67f, .42f));
            Color panel = CharacterSheetSettings.InheritTheme && theme != null
                ? theme.Panel : CharacterSheetSettings.Parse(CharacterSheetSettings.BackgroundHex, new Color(.1f,.13f,.19f));
            Color text = CharacterSheetSettings.InheritTheme && theme != null
                ? theme.Text : CharacterSheetSettings.Parse(CharacterSheetSettings.TextHex, Color.white);

            // Native-safe mode must never paint decorations onto a shared
            // inventory root: InvPar may also contain other windows or be
            // off-screen while the character page is closed. Keep the genuine
            // Erenshor sheet completely untouched, with a separate read-only
            // stats companion only when the native window is actually open.
            if (CharacterSheetSettings.SafeNativeMode)
            {
                if (_nativeBackground != null) _nativeBackground.color = _originalNativeColor;
                if (_overview != null) _overview.Destroy();
                _overview = new CharacterSheetBoundedView(_nativeRoot, _themes);
                return;
            }

            // The character and inventory share InvPar. Never draw a full
            // native-root decoration over item controls or modify native
            // inventory bounds. The bounded view owns its visuals and activates
            // only after every native slot and tab has been verified.
            if (_nativeBackground != null) _nativeBackground.color = _originalNativeColor;
            if (_overview != null) _overview.Destroy();
            _overview = new CharacterSheetBoundedView(_nativeRoot, _themes);
            return;

        }

        // Conservative: only transforms with explicit equipment-slot semantics.
        // Never touch the underlying ItemIcon, Image, or their event handlers.
        private void DecorateEquipmentSlots()
        {
            if (_nativeRoot == null) return;
            for (int i = 0; i < _slotDecorations.Count; i++)
                if (_slotDecorations[i] != null) UnityEngine.Object.Destroy(_slotDecorations[i]);
            _slotDecorations.Clear();
            if (!CharacterSheetSettings.FrameEquipment || CharacterSheetSettings.SlotThickness <= 0f) return;
            UiTheme theme = _themes != null ? _themes.Current : null;
            Color accent = CharacterSheetSettings.InheritTheme && theme != null
                ? theme.Accent : CharacterSheetSettings.Parse(CharacterSheetSettings.AccentHex, Color.yellow);
            int drawn = 0;
            foreach (RectTransform rect in _nativeRoot.GetComponentsInChildren<RectTransform>(true))
            {
                if (rect == null || rect == _nativeRoot || IsOwn(rect) || !rect.gameObject.activeInHierarchy) continue;
                // Inventory content is styled independently, not as equipment slots.
                bool inventoryDescendant = false;
                for (Transform walk = rect; walk != null && walk != _nativeRoot; walk = walk.parent)
                    if (walk.name == "PlayerInv") { inventoryDescendant = true; break; }
                if (inventoryDescendant) continue;
                string n = rect.name.ToLowerInvariant();
                if (!(n.Contains("equipmentslot") || n.Contains("equipslot") ||
                    n.Contains("gear_slot") || n.StartsWith("slot_equip") ||
                    n == "primaryslot" || n == "secondaryslot" || n == "auraslot" ||
                    n == "headslot" || n == "chestslot" || n == "legsslot" ||
                    n == "ringslot")) continue;
                float w = rect.rect.width, h = rect.rect.height;
                if (w < 22f || h < 22f || w > 130f || h > 130f) continue;
                GameObject frame = new GameObject("ArcaneUI_EquipmentSlotBorder", typeof(RectTransform));
                RectTransform slotSurface = frame.GetComponent<RectTransform>();
                slotSurface.SetParent(rect, false);
                var slotLayout = frame.AddComponent<LayoutElement>();
                slotLayout.ignoreLayout = true;
                Stretch(slotSurface);
                float t = CharacterSheetSettings.SlotThickness;
                Edge(slotSurface, "T", new Vector2(0,1), new Vector2(1,1), new Vector2(0,-t), Vector2.zero, accent);
                Edge(slotSurface, "B", new Vector2(0,0), new Vector2(1,0), Vector2.zero, new Vector2(0,t), accent);
                Edge(slotSurface, "L", new Vector2(0,0), new Vector2(0,1), Vector2.zero, new Vector2(t,0), accent);
                Edge(slotSurface, "R", new Vector2(1,0), new Vector2(1,1), new Vector2(-t,0), Vector2.zero, accent);
                _slotDecorations.Add(frame);
                if (++drawn >= 40) break;
            }
        }

        private static RectTransform MakeGraphic(Transform parent, string name, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return rect;
        }
        private static void Stretch(RectTransform r)
        {
            r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
            r.pivot = new Vector2(.5f,.5f);
            r.offsetMin = Vector2.zero; r.offsetMax = Vector2.zero;
        }
        private static void Edge(Transform parent, string name, Vector2 min, Vector2 max,
            Vector2 offsetMin, Vector2 offsetMax, Color color)
        {
            RectTransform rt = MakeGraphic(parent, name, color);
            rt.anchorMin = min; rt.anchorMax = max;
            rt.offsetMin = offsetMin; rt.offsetMax = offsetMax;
        }
        private void ClearDecorations()
        {
            for (int i = 0; i < _slotDecorations.Count; i++)
                if (_slotDecorations[i] != null) UnityEngine.Object.Destroy(_slotDecorations[i]);
            _slotDecorations.Clear();
            if (_decoration != null) UnityEngine.Object.Destroy(_decoration);
            _decoration = null;
        }
        internal void Restore()
        {
            if (_overview != null)
            {
                List<string> last = _overview.LastNativeTextSnapshot();
                if (last.Count > 0)
                {
                    _lastNativeTexts.Clear();
                    _lastNativeTexts.AddRange(last);
                }
                List<string> slots = _overview.LastNativeSlotSnapshot();
                if (slots.Count > 0)
                {
                    _lastNativeSlots.Clear();
                    _lastNativeSlots.AddRange(slots);
                }
                _overview.Destroy();
                _overview = null;
            }
            if (_nativeBackground != null) _nativeBackground.color = _originalNativeColor;
            ClearDecorations();
            _nativeRoot = null;
            _nativeBackground = null;
        }
        internal void Shutdown()
        {
            Restore();
            _candidates.Clear();
            _captured.Clear();
            _watchBaseline.Clear();
            _watchRecorded.Clear();
            _watchUntil = 0f;
        }
    }
}
