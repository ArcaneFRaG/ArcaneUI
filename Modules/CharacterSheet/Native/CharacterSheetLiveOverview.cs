using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using ArcanesGroupFrames.Framework.Theming;
using ArcanesGroupFrames.Framework.NativeUI;
using ArcanesGroupFrames.Framework.Canvas;
using ArcanesGroupFrames.Modules.CharacterSheet.Configuration;

namespace ArcanesGroupFrames.Modules.CharacterSheet.Native
{
    /// <summary>
    /// Native-backed three-column character sheet. Erenshor owns actual item
    /// operations and page controllers; the custom window rehosts only verified
    /// controls and falls back to native UI when it cannot verify them.
    /// Numeric values are mirrored from native Text/TMP labels, never calculated
    /// from guessed Stats fields. HP/MP and player identity have known safe fallbacks.
    /// Unknown native labels are shown as unavailable, never fictional values.
    /// </summary>
    internal sealed class CharacterSheetLiveOverview
    {
        private struct SourceLabel
        {
            internal string Value;
            internal Vector3 World;
        }
        private sealed class Field
        {
            internal string Label;
            internal string[] Aliases;
            internal Text Output;
            internal Field(string label, params string[] aliases) { Label = label; Aliases = aliases; }
        }
        private sealed class MovedControl
        {
            internal RectTransform Rect;
            internal Transform Parent;
            internal int Sibling;
            internal Vector2 AnchorMin, AnchorMax, Pivot, Size, Position, OffsetMin, OffsetMax;
            internal Vector3 Scale;
            internal Quaternion Rotation;
        }
        private readonly List<MovedControl> _moved = new List<MovedControl>();
        private readonly List<Image> _iconMirrors = new List<Image>();
        private RectTransform _gearPanel;
        private RectTransform _tabPanel;
        private CanvasGroup _nativeOverlaySuppression;
        private bool _nativeSuppressed;
        private int _activeTab;
        private readonly List<Button> _nativeTabButtons = new List<Button>();
        private readonly List<UnityEngine.Events.UnityAction> _nativeTabHandlers =
            new List<UnityEngine.Events.UnityAction>();
        private readonly List<Toggle> _nativeTabToggles = new List<Toggle>();
        private readonly List<UnityEngine.Events.UnityAction<bool>> _nativeToggleHandlers =
            new List<UnityEngine.Events.UnityAction<bool>>();
        private CharacterSheetModelPreview _model;
        private bool _gearReady;
        private float _nextVisibilityPoll;
        private bool _nativeWindowOpen;
        private int _nativeEquipmentSlots;
        private int _nativeTabs;
        private static readonly string[] SlotLabels =
        { "Head", "Neck", "Back", "Shoulder", "Chest", "Hands", "Wrists", "Waist",
          "Legs", "Feet", "Ring", "Ring", "Primary", "Secondary", "Ranged", "Aura",
          "Charm", "Accessory", "Accessory", "Extra" };
        internal int NativeSlots { get { return _nativeEquipmentSlots; } }
        internal int NativeTabs { get { return _nativeTabs; } }


        private static readonly Field[] Specification =
        {
            new Field("HP", "HP", "HEALTH"),
            new Field("AC", "AC", "ARMOR CLASS", "ARMOUR CLASS"),
            new Field("MP", "MP", "MANA"),
            new Field("Str", "STR", "STRENGTH"),
            new Field("End", "END", "ENDURANCE"),
            new Field("Dex", "DEX", "DEXTERITY"),
            new Field("Agi", "AGI", "AGILITY"),
            new Field("Int", "INT", "INTELLIGENCE"),
            new Field("Wis", "WIS", "WISDOM"),
            new Field("Cha", "CHA", "CHARISMA"),
            new Field("Res", "RES", "RESILIENCE"),
            new Field("Haste", "HASTE"),
            new Field("Atk Bonus", "ATK BONUS", "ATTACK BONUS"),
            new Field("HP Regen", "HP REGEN", "HEALTH REGEN", "HP REGENERATION"),
            new Field("MP Regen", "MP REGEN", "MANA REGEN", "MP REGENERATION"),
            new Field("Magic", "MAGIC", "MAGIC RESIST"),
            new Field("Poison", "POISON", "POISON RESIST"),
            new Field("Elem", "ELEM", "ELEMENTAL", "ELEMENTAL RESIST"),
            new Field("Void", "VOID", "VOID RESIST")
        };
        private readonly RectTransform _native;
        private RectTransform _viewport;
        private RectTransform _ownedStatsLayer;
        private readonly ArcaneCanvasManager _canvas;
        private readonly ThemeManager _themes;
        private readonly Dictionary<string, Field> _fields = new Dictionary<string, Field>(StringComparer.Ordinal);
        private readonly List<SourceLabel> _sources = new List<SourceLabel>();
        private RectTransform _root;
        private RectTransform _safePanel;
        private Text _safeIdentity;
        private Text _safeAura;
        private readonly Dictionary<string, Text> _safeStatValues = new Dictionary<string, Text>(StringComparer.Ordinal);
        private Text _identity;
        private Text _aura;
        private Text _availability;
        private float _nextUpdate;
        private int _available;
        internal int AvailableFields { get { return _available; } }
        internal bool Visible { get { return _safePanel != null && _safePanel.gameObject.activeInHierarchy; } }

        internal CharacterSheetLiveOverview(RectTransform native, ThemeManager themes,
            ArcaneCanvasManager canvas)
        {
            _native = native;
            _themes = themes;
            _canvas = canvas;
            // The native InvPar hierarchy can be nested in a tiny camera-space
            // RectTransform. Host the read-only companion on ArcaneUI's known
            // full-screen overlay canvas instead. Never instantiate the unverified
            // three-column item-rehosting interface in a production build.
            _ownedStatsLayer = canvas != null && canvas.IsReady
                ? canvas.CreateLayer("ArcaneUI_CharacterStatsLayer") : null;
            _viewport = _ownedStatsLayer;
        }

        private static RectTransform Rectangle(Transform parent, string name, float x, float y, float w, float h)
        {
            var obj = new GameObject(name, typeof(RectTransform));
            var rt = obj.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(x, -y);
            rt.sizeDelta = new Vector2(w, h);
            return rt;
        }
        private static Text Label(Transform parent, string name, float x, float y, float w, float h,
            int size, Color color, TextAnchor align, bool bold)
        {
            var rect = Rectangle(parent, name, x, y, w, h);
            var t = rect.gameObject.AddComponent<Text>();
            t.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            t.fontSize = size;
            t.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
            t.alignment = align;
            t.color = color;
            t.supportRichText = false;
            t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            return t;
        }
        private static void Fill(Transform parent, string name, float x, float y, float w, float h, Color color)
        {
            var img = Rectangle(parent, name, x, y, w, h).gameObject.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
        }
        private static bool Own(Transform node)
        {
            for (; node != null; node = node.parent)
                if (node.name.StartsWith("ArcaneUI", StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }
        // A single shared three-column sheet, not a disconnected stat sidebar.
        // Only explicit native slot/button controls are moved; their components and
        // listeners stay intact. See RestoreControls() before destroying this view.
        private void Create()
        {
            if (_native == null || _native.parent == null) return;
            float statsW = CharacterSheetSettings.OverviewWidth;
            float w = 300f + 320f + statsW + 36f;
            const float h = 742f;
            // Sibling of the full-screen canvas content, not child of InvPar.
            // Keep native gameplay controls under InvPar, with reversible
            // reparenting for only confirmed native equipment buttons.
            Transform overlayParent = _viewport != null ? (Transform)_viewport : _native.parent;
            _root = Rectangle(overlayParent, "ArcaneUI_CharacterThreeColumn", 0, 0, w, h);
            _root.gameObject.SetActive(false); // hidden until the native sheet opens
            _root.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            var group = _root.gameObject.AddComponent<CanvasGroup>();
            group.interactable = true;
            group.blocksRaycasts = true;
            UiTheme theme = _themes != null ? _themes.Current : null;
            Color surface = CharacterSheetSettings.InheritTheme && theme != null ? theme.Panel :
                CharacterSheetSettings.Parse(CharacterSheetSettings.BackgroundHex, new Color(.10f, .13f, .19f));
            Color accent = CharacterSheetSettings.InheritTheme && theme != null ? theme.Accent :
                CharacterSheetSettings.Parse(CharacterSheetSettings.AccentHex, new Color(.82f, .67f, .42f));
            Color white = CharacterSheetSettings.InheritTheme && theme != null ? theme.Text :
                CharacterSheetSettings.Parse(CharacterSheetSettings.TextHex, Color.white);
            Image panel = _root.gameObject.AddComponent<Image>();
            panel.color = new Color(surface.r, surface.g, surface.b, .98f);
            panel.raycastTarget = true; // shield old, now-overlapped native artwork
            if (CharacterSheetSettings.DrawFrame)
            {
                var border = _root.gameObject.AddComponent<Outline>();
                border.effectColor = accent;
                border.effectDistance = new Vector2(2f, -2f);
            }
            Fill(_root, "ArcaneUI_TopRule", 12, 12, w - 24, 2, accent);
            Label(_root, "ArcaneUI_SheetTitle", 16, 18, w - 32, 30, 24, white,
                TextAnchor.MiddleCenter, true).text = "CHARACTER";
            _identity = Label(_root, "ArcaneUI_SheetIdentity", 16, 48, w - 32, 35, 19,
                white, TextAnchor.MiddleCenter, true);
            _aura = Label(_root, "ArcaneUI_SheetAura", 16, 83, w - 32, 22, 13,
                accent, TextAnchor.MiddleCenter, false);

            // LEFT: actual Erenshor equipment slot buttons when identifiable.
            const float leftX = 18f;
            Label(_root, "ArcaneUI_EquipHeading", leftX, 110, 280, 28, 19,
                white, TextAnchor.MiddleCenter, true).text = "EQUIPPED ITEMS";
            _gearPanel = Rectangle(_root, "ArcaneUI_NativeEquipmentHost", leftX + 8f, 151f, 264f, 481f);
            for (int i = 0; i < SlotLabels.Length; i++)
            {
                int col = i % 4, row = i / 4;
                RectTransform place = Rectangle(_gearPanel, "ArcaneUI_EmptyEquip_" + i,
                    5 + col * 65, 5 + row * 80, 57, 57);
                var img = place.gameObject.AddComponent<Image>();
                img.color = new Color(accent.r, accent.g, accent.b, .17f);
                img.raycastTarget = false;
                Label(_gearPanel, "ArcaneUI_EquipSlotCaption_" + i,
                    2 + col * 65, 62 + row * 80, 63, 17, 12,
                    white, TextAnchor.MiddleCenter, false).text = SlotLabels[i];
            }
            Fill(_root, "ArcaneUI_LeftRule", 303, 110, 1, 545, accent);

            // MIDDLE: isolated RenderTexture of the live player's meshes; no
            // cloned scripts, colliders, stats or gameplay Character component.
            const float centerX = 315f;
            Label(_root, "ArcaneUI_ModelHeading", centerX, 110, 308, 28, 19,
                white, TextAnchor.MiddleCenter, true).text = "CHARACTER MODEL";
            RectTransform stage = Rectangle(_root, "ArcaneUI_ModelStage", centerX + 12, 151, 284, 481);
            Image stageBg = stage.gameObject.AddComponent<Image>();
            stageBg.color = new Color(0, 0, 0, .28f); stageBg.raycastTarget = false;
            var modelImage = Rectangle(stage, "ArcaneUI_LiveModelImage", 0, 0, 284, 455)
                .gameObject.AddComponent<RawImage>();
            modelImage.raycastTarget = false;
            Label(stage, "ArcaneUI_ModelFallback", 9, 435, 266, 36, 12,
                white, TextAnchor.MiddleCenter, false).text = "Live character model (when available)";
            if (CharacterSheetSettings.ShowModelPreview)
                _model = new CharacterSheetModelPreview(modelImage);
            Fill(_root, "ArcaneUI_MidRule", 630, 110, 1, 545, accent);

            // RIGHT: all the attributes in the supplied native screenshot.
            float sx = 643f, rw = statsW - 12f;
            Label(_root, "ArcaneUI_StatsHeading", sx, 110, rw, 28, 19,
                white, TextAnchor.MiddleCenter, true).text = "STATS";
            for (int i = 0; i < 3; i++)
                Row(_root, Specification[i], sx + 10, 151 + i * 33, rw - 20, accent, white);
            Fill(_root, "ArcaneUI_StatRule1", sx + 8, 254, rw - 16, 1, accent);
            for (int i = 3; i <= 12; i++)
            {
                bool right = i >= 10;
                int n = right ? i - 10 : i - 3;
                float cw = (rw - 30f) / 2f;
                Row(_root, Specification[i], sx + 9 + (right ? cw + 12 : 0),
                    272 + n * 39, cw, accent, white);
            }
            Row(_root, Specification[13], sx + rw / 2f + 8f, 389, (rw - 30f) / 2f, accent, white);
            Row(_root, Specification[14], sx + rw / 2f + 8f, 428, (rw - 30f) / 2f, accent, white);
            Fill(_root, "ArcaneUI_StatRule2", sx + 8, 567, rw - 16, 1, accent);
            Label(_root, "ArcaneUI_ResistTitle", sx + 12, 573, rw - 24, 26, 17,
                white, TextAnchor.MiddleCenter, true).text = "RESIST VS";
            for (int i = 15; i < 19; i++)
            {
                bool right = i >= 17;
                int n = (i - 15) % 2;
                float cw = (rw - 30f) / 2f;
                Row(_root, Specification[i], sx + 9 + (right ? cw + 12 : 0),
                    608 + n * 32, cw, accent, white);
            }
            Fill(_root, "ArcaneUI_BottomRule", 12, 670, w - 24, 1, accent);
            _tabPanel = Rectangle(_root, "ArcaneUI_NativeTabHost", (w - 440f)/2f, 676, 440, 53);
            _availability = Label(_root, "ArcaneUI_LiveFooter", 10, 717, w - 20, 21, 12,
                white, TextAnchor.MiddleCenter, false);
            Reposition();
            RefreshNow();
        }
        private void Row(Transform parent, Field field, float x, float y, float w, Color accent, Color white)
        {
            // Definition instances are shared; display state belongs to this window.
            field = new Field(field.Label, field.Aliases);
            Label(parent, "ArcaneUI_StatLabel_" + field.Label.Replace(' ', '_'), x, y, w * .49f, 23, 15,
                white, TextAnchor.MiddleLeft, true).text = field.Label + ":";
            field.Output = Label(parent, "ArcaneUI_StatValue_" + field.Label.Replace(' ', '_'), x + w * .48f,
                y, w * .52f, 23, 16, accent, TextAnchor.MiddleRight, true);
            field.Output.text = "—";
            _fields[field.Label] = field;
        }
        private void Reposition()
        {
            if (_root == null || _native == null) return;
            RectTransform parent = _root.parent as RectTransform;
            if (parent == null) return;
            _root.anchorMin = _root.anchorMax = new Vector2(.5f, .5f);
            _root.pivot = new Vector2(.5f, .5f);
            _root.anchoredPosition = Vector2.zero;
            // Use the real full-screen canvas viewport, not InvPar's small
            // layout parent. Retain the user's scale setting as an upper bound
            // and fit the window on screen without clipping.
            float availableW = Mathf.Max(1f, parent.rect.width - 48f);
            float availableH = Mathf.Max(1f, parent.rect.height - 36f);
            float fitW = availableW / Mathf.Max(1f, _root.rect.width);
            float fitH = availableH / Mathf.Max(1f, _root.rect.height);
            float scale = Mathf.Min(CharacterSheetSettings.OverviewScale, fitW, fitH);
            _root.localScale = Vector3.one * Mathf.Clamp(scale, .01f, 1.25f);
            _root.SetAsLastSibling();
        }

        // The character-sheet key is already handled by Erenshor. We follow
        // its real window state rather than intercepting/replacing native keys.
        // A hidden/inactive/off-screen InvPar must never leave our standalone
        // canvas overlay stuck on screen.
        private bool NativeWindowIsOpen(RectTransform native)
        {
            if (native == null || !native.gameObject.activeInHierarchy) return false;
            Transform playerInv = native.Find("PlayerInv");
            if (playerInv != null && !playerInv.gameObject.activeInHierarchy) return false;
            for (Transform p = playerInv != null ? playerInv : (Transform)native;
                 p != null; p = p.parent)
            {
                foreach (CanvasGroup group in p.GetComponents<CanvasGroup>())
                    if (group != null && group != _nativeOverlaySuppression &&
                        group.enabled && group.alpha <= .01f)
                        return false;
                if (Mathf.Abs(p.lossyScale.x) < .005f || Mathf.Abs(p.lossyScale.y) < .005f)
                    return false;
            }
            RectTransform panel = playerInv != null ? playerInv as RectTransform : native;
            if (panel != null && Screen.width > 0 && Screen.height > 0)
            {
                Vector3[] corners = new Vector3[4];
                panel.GetWorldCorners(corners);
                UnityEngine.Canvas canvas = panel.GetComponentInParent<UnityEngine.Canvas>();
                Camera camera = canvas != null && canvas.rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay
                    ? canvas.rootCanvas.worldCamera : null;
                float minX = float.MaxValue, maxX = float.MinValue;
                float minY = float.MaxValue, maxY = float.MinValue;
                for (int i = 0; i < 4; i++)
                {
                    Vector2 screen = RectTransformUtility.WorldToScreenPoint(camera, corners[i]);
                    minX = Mathf.Min(minX, screen.x); maxX = Mathf.Max(maxX, screen.x);
                    minY = Mathf.Min(minY, screen.y); maxY = Mathf.Max(maxY, screen.y);
                }
                if (maxX < 0f || minX > Screen.width || maxY < 0f || minY > Screen.height)
                    return false;
            }
            // InvPar may remain active when only the inventory is open. Require
            // actual active character-stat labels before drawing a companion.
            int matched = 0;
            foreach (Text label in native.GetComponentsInChildren<Text>(false))
            {
                if (label == null || !label.enabled || Own(label.transform)) continue;
                string entry = Clean(label.text);
                if (IsVitalText(entry)) matched++;
                if (matched >= 2) return true;
            }
            foreach (TMP_Text label in native.GetComponentsInChildren<TMP_Text>(false))
            {
                if (label == null || !label.enabled || Own(label.transform)) continue;
                string entry = Clean(label.text);
                if (IsVitalText(entry)) matched++;
                if (matched >= 2) return true;
            }
            return false;
        }
        private static bool IsVitalText(string text)
        {
            return text.StartsWith("HP:", StringComparison.OrdinalIgnoreCase) ||
                text.StartsWith("MP:", StringComparison.OrdinalIgnoreCase) ||
                text.StartsWith("AC:", StringComparison.OrdinalIgnoreCase) ||
                text.StartsWith("HP ", StringComparison.OrdinalIgnoreCase) ||
                text.StartsWith("MP ", StringComparison.OrdinalIgnoreCase);
        }
        private static string Clean(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return Regex.Replace(s, "<[^>]*>", "").Replace("\u00a0", " ").Trim();
        }
        private void CollectSources()
        {
            _sources.Clear();
            if (_native == null) return;
            foreach (Text t in _native.GetComponentsInChildren<Text>(true))
                if (t != null && t.enabled && t.gameObject.activeInHierarchy && !Own(t.transform))
                    AddSource(t.text, t.transform.position);
            foreach (TMP_Text t in _native.GetComponentsInChildren<TMP_Text>(true))
                if (t != null && t.enabled && t.gameObject.activeInHierarchy && !Own(t.transform))
                    AddSource(t.text, t.transform.position);
        }
        private void AddSource(string raw, Vector3 position)
        {
            foreach (string line in Clean(raw).Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string s = line.Trim();
                if (s.Length > 0 && s.Length < 180)
                    _sources.Add(new SourceLabel { Value = s, World = position });
            }
        }
        private static bool Prefix(string content, string alias, out string remainder)
        {
            remainder = "";
            if (!content.StartsWith(alias, StringComparison.OrdinalIgnoreCase)) return false;
            if (content.Length > alias.Length && char.IsLetter(content[alias.Length])) return false;
            string tail = content.Substring(alias.Length).Trim();
            if (!tail.StartsWith(":")) return false;
            remainder = tail.Substring(1).Trim();
            return true;
        }
        private string FindValue(params string[] aliases)
        {
            for (int a = 0; a < aliases.Length; a++)
            for (int i = 0; i < _sources.Count; i++)
            {
                string value;
                if (!Prefix(_sources[i].Value, aliases[a], out value)) continue;
                if (value.Length > 0) return value;
                // Some builds use separate Text objects for labels and values.
                // Only accept the nearest numeric text on the same row, to avoid
                // silently attaching a number belonging to a different field.
                SourceLabel source = _sources[i];
                float best = float.MaxValue;
                string matched = "";
                for (int j = 0; j < _sources.Count; j++)
                {
                    if (i == j) continue;
                    string candidate = _sources[j].Value;
                    if (candidate.Length < 1 || !char.IsDigit(candidate[0])) continue;
                    Vector3 delta = _native.InverseTransformPoint(_sources[j].World) -
                        _native.InverseTransformPoint(source.World);
                    if (Mathf.Abs(delta.y) > 17f || delta.x < -3f || delta.x > 270f) continue;
                    float distance = Mathf.Abs(delta.x) + Mathf.Abs(delta.y) * 2f;
                    if (distance < best) { best = distance; matched = candidate; }
                }
                if (!string.IsNullOrEmpty(matched)) return matched;
            }
            return "";
        }
        private static bool IsEquipmentName(string name)
        {
            string n = name.ToLowerInvariant();
            return n.Contains("equip") || n.Contains("gear") ||
                n.Contains("slot_equip") || n.Contains("itemslot_equip") ||
                n.Contains("itemslot") || n.Contains("slot") ||
                n.Contains("ring") || n.Contains("neck") || n.Contains("amulet") ||
                n.Contains("head") || n.Contains("chest") || n.Contains("hands") ||
                n.Contains("leg") || n.Contains("feet") || n.Contains("boots") ||
                n.Contains("waist") || n.Contains("wrist") || n.Contains("back") ||
                n.Contains("primary") || n.Contains("secondary") || n.Contains("weapon") ||
                n.Contains("aura") || n.Contains("shoulder");
        }
        private static bool HasNativeInteraction(RectTransform rect)
        {
            foreach (Component c in rect.GetComponents<Component>())
            {
                if (c == null) continue;
                if (c is Selectable || c is IPointerClickHandler || c is IBeginDragHandler ||
                    c is IDragHandler || c is IDropHandler || c is Collider2D) return true;
                string n = c.GetType().Name.ToLowerInvariant();
                if (n.Contains("itemicon") || n.Contains("itemslot") ||
                    n.Contains("equipslot") || n.Contains("inventoryslot") ||
                    n.Contains("equipped")) return true;
            }
            // A native slot can put its icon collider/ItemIcon script on a child.
            // Move the full slot root, not the child image, so mouse handlers live.
            foreach (Transform child in rect)
                foreach (Component c in child.GetComponents<Component>())
                {
                    if (c == null || c is Transform) continue;
                    string n = c.GetType().Name.ToLowerInvariant();
                    if (n.Contains("itemicon") || n.Contains("equipslot") || c is Collider2D)
                        return true;
                }
            return false;
        }
        private static bool Under(Transform candidate, Transform ancestor)
        {
            for (Transform p = candidate; p != null; p = p.parent)
                if (p == ancestor) return true;
            return false;
        }
        private MovedControl MoveNative(RectTransform rect, RectTransform host, float x, float y, float w, float h)
        {
            var state = new MovedControl
            {
                Rect = rect, Parent = rect.parent, Sibling = rect.GetSiblingIndex(),
                AnchorMin = rect.anchorMin, AnchorMax = rect.anchorMax, Pivot = rect.pivot,
                Size = rect.sizeDelta, Position = rect.anchoredPosition,
                OffsetMin = rect.offsetMin, OffsetMax = rect.offsetMax,
                Scale = rect.localScale, Rotation = rect.localRotation
            };
            _moved.Add(state);
            rect.SetParent(host, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.sizeDelta = new Vector2(w, h);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.identity;
            return state;
        }
        private void RestoreNativePresentation()
        {
            if (_nativeOverlaySuppression != null)
            {
                // This CanvasGroup is owned by ArcaneUI, never modify a game's
                // existing group. Restoring alpha/interaction is immediate.
                _nativeOverlaySuppression.alpha = 1f;
                _nativeOverlaySuppression.interactable = true;
                _nativeOverlaySuppression.blocksRaycasts = true;
            }
            _nativeSuppressed = false;
        }
        private void SetNativePresentation(bool useCustom)
        {
            // Never hide the native interactive controls without a complete,
            // verified equipment-slot + page-controller integration.
            bool safe = !CharacterSheetSettings.SafeNativeMode && useCustom &&
                _nativeEquipmentSlots >= 20 && _nativeTabs == 3;
            if (!safe) { RestoreNativePresentation(); return; }
            if (_nativeOverlaySuppression == null && _native != null)
                _nativeOverlaySuppression = _native.gameObject.AddComponent<CanvasGroup>();
            if (_nativeOverlaySuppression == null) return;
            _nativeOverlaySuppression.alpha = 0f;
            _nativeOverlaySuppression.interactable = false;
            _nativeOverlaySuppression.blocksRaycasts = false;
            _nativeSuppressed = true;
        }
        private void SetPage(int index)
        {
            // Native Erenshor owns page navigation. Never close InvPar or the
            // custom sheet in response to a tab. An unmapped native tab is not
            // replaced by an ArcaneUI click proxy.
            _activeTab = Mathf.Clamp(index, 0, 2);
            _nextVisibilityPoll = 0f;
        }
        private void RestoreControls()
        {
            RestoreNativePresentation();
            for (int i = _moved.Count - 1; i >= 0; i--)
            {
                MovedControl s = _moved[i];
                if (s.Rect == null || s.Parent == null) continue;
                s.Rect.SetParent(s.Parent, false);
                s.Rect.SetSiblingIndex(Mathf.Min(s.Sibling, s.Rect.parent.childCount - 1));
                s.Rect.anchorMin = s.AnchorMin; s.Rect.anchorMax = s.AnchorMax;
                s.Rect.pivot = s.Pivot; s.Rect.sizeDelta = s.Size;
                s.Rect.anchoredPosition = s.Position;
                s.Rect.offsetMin = s.OffsetMin; s.Rect.offsetMax = s.OffsetMax;
                s.Rect.localScale = s.Scale; s.Rect.localRotation = s.Rotation;
            }
            _moved.Clear();
            _nativeEquipmentSlots = 0;
            _nativeTabs = 0;
        }
        // The character key closes the native window, not ArcaneUI. Return
        // any temporarily reparented native item controls immediately on
        // close, so opening bags/inventory elsewhere cannot lose its buttons.
        private void SuspendNativeControls()
        {
            if (!_gearReady) return;
            RestoreControls();
            _gearReady = false;
            for (int i = 0; i < _iconMirrors.Count; i++)
                if (_iconMirrors[i] != null)
                    UnityEngine.Object.Destroy(_iconMirrors[i].gameObject);
            _iconMirrors.Clear();
            if (_gearPanel != null)
            {
                for (int i = _gearPanel.childCount - 1; i >= 0; i--)
                {
                    Transform child = _gearPanel.GetChild(i);
                    if (child != null && (child.name.StartsWith("ArcaneUI_EquipmentFallback", StringComparison.Ordinal) ||
                        child.name.StartsWith("ArcaneUI_MissingSlotData", StringComparison.Ordinal) ||
                        child.name.StartsWith("ArcaneUI_ReadOnlyGear_", StringComparison.Ordinal)))
                        UnityEngine.Object.Destroy(child.gameObject);
                }
            }
            if (_tabPanel != null)
            {
                for (int i = _tabPanel.childCount - 1; i >= 0; i--)
                {
                    Transform child = _tabPanel.GetChild(i);
                    if (child != null && child.name.StartsWith("ArcaneUI_TabFallback_", StringComparison.Ordinal))
                        UnityEngine.Object.Destroy(child.gameObject);
                }
            }
        }

        private void DiscoverNativeControls()
        {
            if (_gearReady || _native == null) return;
            _gearReady = true;
            // Until the native equipped-slot component mapping is verified, any
            // position/name-based heuristic can catch icons, inventory items,
            // or controls without their true drop/drag handlers. Never move
            // those controls or copy decorative sprite images as fake slots.
            Plugin.LogWarning("ArcaneUI character: equipment slot mapping is unverified. " +
                "No native item controls or tabs relocated; use INSPECT SLOTS.");
            InspectNativeSlots();
        }

        private Button CreateTabFallback(int index, string label)
        {
            RectTransform rect = Rectangle(_tabPanel, "ArcaneUI_TabFallback_" + label,
                index * 148 + 3f, 2f, 140, 36);
            Image bg = rect.gameObject.AddComponent<Image>();
            bg.color = new Color(.19f, .24f, .30f, .98f);
            Button button = rect.gameObject.AddComponent<Button>();
            Label(rect, "ArcaneUI_TabText_" + label, 2, 2, 136, 32, 14,
                Color.white, TextAnchor.MiddleCenter, true).text = label;
            return button;
        }
        internal List<string> LastNativeSlotSnapshot()
        {
            var results = new List<string>();
            if (_native == null) return results;
            foreach (MovedControl state in _moved)
            {
                if (state.Rect == null) continue;
                results.Add("MOVED " + state.Rect.name + " | original parent=" +
                    (state.Parent != null ? state.Parent.name : "(destroyed)"));
            }
            foreach (RectTransform rect in _native.GetComponentsInChildren<RectTransform>(true))
            {
                if (rect == null || Own(rect)) continue;
                if (rect.rect.width < 25 || rect.rect.width > 110 ||
                    rect.rect.height < 25 || rect.rect.height > 110) continue;
                string components = "";
                foreach (Component c in rect.GetComponents<Component>())
                    if (c != null && !(c is RectTransform)) components += c.GetType().Name + ",";
                if (components.Contains("Item") || components.Contains("Slot") ||
                    components.Contains("Button") || HasNativeInteraction(rect))
                {
                    string hierarchy = rect.name;
                    for (Transform walk = rect.parent; walk != null && walk != _native; walk = walk.parent)
                        hierarchy = walk.name + "/" + hierarchy;
                    results.Add(hierarchy + " | " + components + " | parent=" +
                        (rect.parent != null ? rect.parent.name : "(none)"));
                    if (results.Count >= 90) break;
                }
            }
            return results;
        }
        internal void InspectNativeSlots()
        {
            List<string> diagnostics = LastNativeSlotSnapshot();
            Plugin.LogInfo("ArcaneUI equipment slot diagnostics for " +
                (_native != null ? _native.name : "(not bound)"));
            for (int i = 0; i < diagnostics.Count; i++)
                Plugin.LogInfo("ArcaneUI equipment node " + (i + 1) + ": " + diagnostics[i]);
        }

        // Native-safe companion: visible only with Erenshor's character sheet.
        // It displays real native values but NEVER imitates clickable item slots.
        private void EnsureNativeSafePanel()
        {
            if (_safePanel != null || _native == null) return;
            Transform parent = _viewport != null ? (Transform)_viewport : _native.parent;
            if (parent == null) return;
            UiTheme theme = _themes != null ? _themes.Current : null;
            Color accent = CharacterSheetSettings.InheritTheme && theme != null ? theme.Accent :
                CharacterSheetSettings.Parse(CharacterSheetSettings.AccentHex, new Color(.82f,.67f,.42f));
            Color surface = CharacterSheetSettings.InheritTheme && theme != null ? theme.Panel :
                CharacterSheetSettings.Parse(CharacterSheetSettings.BackgroundHex, new Color(.10f,.13f,.19f));
            Color white = CharacterSheetSettings.InheritTheme && theme != null ? theme.Text :
                CharacterSheetSettings.Parse(CharacterSheetSettings.TextHex, Color.white);
            _safePanel = Rectangle(parent, "ArcaneUI_CharacterStatsCompanion", 0, 0, 350, 670);
            var overlayCanvas = _safePanel.gameObject.AddComponent<UnityEngine.Canvas>();
            overlayCanvas.overrideSorting = true;
            overlayCanvas.sortingOrder = 200;
            _safePanel.anchorMin = _safePanel.anchorMax = new Vector2(.5f, .5f);
            _safePanel.pivot = new Vector2(.5f, .5f);
            _safePanel.sizeDelta = new Vector2(350, 670);
            // The companion must never intercept mouse input for the game's
            // equipment slots, tabs, or NPC windows.
            CanvasGroup safeGroup = _safePanel.gameObject.AddComponent<CanvasGroup>();
            safeGroup.interactable = false;
            safeGroup.blocksRaycasts = false;
            Image surfaceImage = _safePanel.gameObject.AddComponent<Image>();
            surfaceImage.color = new Color(surface.r, surface.g, surface.b, .98f);
            surfaceImage.raycastTarget = false;
            Label(_safePanel, "ArcaneUI_SafeHeading", 15, 12, 320, 27, 21, white,
                TextAnchor.MiddleCenter, true).text = "CHARACTER STATS";
            _safeIdentity = Label(_safePanel, "ArcaneUI_SafeIdentity", 15, 42, 320, 34, 18,
                white, TextAnchor.MiddleCenter, true);
            _safeAura = Label(_safePanel, "ArcaneUI_SafeAura", 15, 78, 320, 22, 14,
                accent, TextAnchor.MiddleCenter, false);
            for (int i = 0; i < Specification.Length; i++)
            {
                int row = i < 3 ? i : i + 1;
                float y = 117f + row * 25f;
                Label(_safePanel, "ArcaneUI_SafeStatName_" + i, 22, y, 155, 23, 15,
                    white, TextAnchor.MiddleLeft, false).text = Specification[i].Label;
                _safeStatValues[Specification[i].Label] = Label(_safePanel,
                    "ArcaneUI_SafeStatValue_" + i, 175, y, 151, 23, 15, accent,
                    TextAnchor.MiddleRight, true);
            }
            Label(_safePanel, "ArcaneUI_SafeHint", 15, 636, 320, 25, 12, white,
                TextAnchor.MiddleCenter, false).text = "Equipment and tabs remain interactive in Erenshor.";
            _safePanel.gameObject.SetActive(false);
        }

        private static Camera CanvasCamera(UnityEngine.Canvas canvas)
        {
            if (canvas == null) return null;
            UnityEngine.Canvas root = canvas.rootCanvas != null ? canvas.rootCanvas : canvas;
            return root.renderMode == RenderMode.ScreenSpaceOverlay ? null : root.worldCamera;
        }

        private bool RepositionNativeSafePanel()
        {
            if (_safePanel == null || _native == null) return false;
            RectTransform host = _safePanel.parent as RectTransform;
            if (host == null || host.rect.width <= 0f || host.rect.height <= 0f) return false;

            // InvPar can live on a camera-space or scaled native canvas. Use
            // the source and destination canvas cameras separately; assuming
            // ScreenSpaceOverlay (null) caused the companion to appear in a
            // clipped corner on some Erenshor UI configurations.
            UnityEngine.Canvas sourceCanvas = _native.GetComponentInParent<UnityEngine.Canvas>();
            UnityEngine.Canvas destinationCanvas = host.GetComponentInParent<UnityEngine.Canvas>();
            Camera sourceCamera = CanvasCamera(sourceCanvas);
            Camera destinationCamera = CanvasCamera(destinationCanvas);
            Vector3[] corners = new Vector3[4];
            // Prefer the complete InvPar window bounds. Placing beside only
            // PlayerInv can cover the native stats/tabs beside that child.
            RectTransform nativeGeometry = _native;
            if (_native.rect.width > Screen.width * .85f ||
                _native.rect.height > Screen.height * .95f)
            {
                RectTransform inventory = _native.Find("PlayerInv") as RectTransform;
                if (inventory != null) nativeGeometry = inventory;
            }
            nativeGeometry.GetWorldCorners(corners);
            float left = float.PositiveInfinity, right = float.NegativeInfinity;
            float bottom = float.PositiveInfinity, top = float.NegativeInfinity;
            for (int i = 0; i < corners.Length; i++)
            {
                Vector2 local;
                Vector2 projected = RectTransformUtility.WorldToScreenPoint(sourceCamera, corners[i]);
                if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(host, projected,
                    destinationCamera, out local)) return false;
                left = Mathf.Min(left, local.x);
                right = Mathf.Max(right, local.x);
                bottom = Mathf.Min(bottom, local.y);
                top = Mathf.Max(top, local.y);
            }
            if (float.IsInfinity(left) || float.IsInfinity(right) ||
                float.IsNaN(left) || float.IsNaN(right) ||
                right - left < 50f || top - bottom < 50f) return false;

            float margin = 16f;
            float usableHeight = Mathf.Max(1f, host.rect.height - margin * 2f);
            float usableWidth = Mathf.Max(1f, host.rect.width - margin * 2f);
            float scale = Mathf.Min(1f, usableHeight / 670f, usableWidth / 350f);
            scale = Mathf.Max(.1f, scale);
            float halfW = 175f * scale;
            float halfH = 335f * scale;
            float minX = host.rect.xMin + halfW + margin;
            float maxX = host.rect.xMax - halfW - margin;
            float minY = host.rect.yMin + halfH + margin;
            float maxY = host.rect.yMax - halfH - margin;
            float desiredRight = right + halfW + margin;
            float desiredLeft = left - halfW - margin;
            float desiredX = desiredRight + halfW <= host.rect.xMax - margin
                ? desiredRight : desiredLeft - halfW >= host.rect.xMin + margin
                    ? desiredLeft : (left + right) * .5f;
            // Never cover the character's native equipment area if there is
            // insufficient space on either side. The working native sheet wins.
            if (desiredRight + halfW > host.rect.xMax - margin &&
                desiredLeft - halfW < host.rect.xMin + margin) return false;
            float desiredY = (bottom + top) * .5f;

            _safePanel.anchorMin = _safePanel.anchorMax = new Vector2(.5f, .5f);
            _safePanel.pivot = new Vector2(.5f, .5f);
            _safePanel.sizeDelta = new Vector2(350f, 670f);
            _safePanel.localScale = Vector3.one * scale;
            _safePanel.anchoredPosition = new Vector2(
                Mathf.Clamp(desiredX, minX, Mathf.Max(minX, maxX)),
                Mathf.Clamp(desiredY, minY, Mathf.Max(minY, maxY)));
            _safePanel.SetAsLastSibling();
            return true;
        }

        private void UpdateNativeSafeValues()
        {
            CollectSources();
            Stats stats = GameData.PlayerStats;
            string name = stats != null ? stats.MyName : "";
            string className = stats != null ? RaidClassColors.ResolveClassNameForActor(stats, name) : "";
            _safeIdentity.text = (string.IsNullOrEmpty(name) ? "CHARACTER" : name) +
                "  ·  " + className + (stats != null ? "  ·  Level " + stats.Level : "");
            string aura = FindValue("AURA");
            _safeAura.text = string.IsNullOrEmpty(aura) ? "" : "Aura: " + aura;
            _available = 0;
            foreach (Field field in Specification)
            {
                Text output;
                if (!_safeStatValues.TryGetValue(field.Label, out output)) continue;
                string val = FindValue(field.Aliases);
                if (string.IsNullOrEmpty(val) && stats != null)
                {
                    if (field.Label == "HP") val = stats.CurrentHP + " / " + stats.CurrentMaxHP;
                    if (field.Label == "MP") val = stats.CurrentMana + " / " + stats.GetCurrentMaxMana();
                }
                output.text = string.IsNullOrEmpty(val) ? "—" : val;
                if (!string.IsNullOrEmpty(val)) _available++;
            }
        }

        internal void RefreshNow()
        {
            if (_native == null) return;
            _nextUpdate = 0f;
            Update();
        }
        internal void Update()
        {
            // The runtime may discover InvPar before the ArcaneUI canvas is
            // ready. Retry on subsequent frames rather than permanently
            // leaving the Character Sheet invisible for this login.
            if (_viewport == null && _canvas != null && _canvas.IsReady)
            {
                _ownedStatsLayer = _canvas.CreateLayer("ArcaneUI_CharacterStatsLayer");
                _viewport = _ownedStatsLayer;
            }
            if (_native == null || _viewport == null) return;
            // 10 Hz polling is enough to track the game's native key/close
            // action without per-frame CanvasGroup/geometry allocations.
            if (Time.unscaledTime >= _nextVisibilityPoll ||
                !_native.gameObject.activeInHierarchy)
            {
                _nextVisibilityPoll = Time.unscaledTime + .1f;
                _nativeWindowOpen = NativeWindowIsOpen(_native);
            }
            if (!_nativeWindowOpen) _activeTab = 0;
            // ArcaneUI settings are modal. Never draw the companion over the
            // settings shell or mistake its preview for an open character sheet.
            bool safeVisible = CharacterSheetSettings.ShowOverview && _nativeWindowOpen &&
                !ArcaneUiModalInputGuard.IsBlocking;
            if (safeVisible && _safePanel == null) EnsureNativeSafePanel();
            if (_safePanel != null)
            {
                // Validate the native-window geometry BEFORE activating any
                // graphics. Invalid camera/layout data must never produce the
                // clipped or centre-screen panels seen in earlier test builds.
                bool showCompanion = safeVisible && RepositionNativeSafePanel();
                if (_safePanel.gameObject.activeSelf != showCompanion)
                    _safePanel.gameObject.SetActive(showCompanion);
                if (showCompanion && Time.unscaledTime >= _nextUpdate)
                {
                    _nextUpdate = Time.unscaledTime + .33f;
                    UpdateNativeSafeValues();
                }
            }
            // There is deliberately no speculative equipment rehosting or
            // duplicate three-column panel. All item clicks/tabs stay native.
            if (_root == null) return;
            bool show = false; // Unverified experimental rehosting is permanently parked in this build.
            if (_root.gameObject.activeSelf != show)
            {
                if (!show) SuspendNativeControls();
                _root.gameObject.SetActive(show);
                Plugin.LogInfo("ArcaneUI character sheet " + (show ? "opened" : "closed") +
                    " with the native InvPar window.");
            }
            if (!show)
            {
                RestoreNativePresentation();
                return;
            }
            Reposition();
            if (Time.unscaledTime < _nextUpdate) return;
            _nextUpdate = Time.unscaledTime + .33f;
            CollectSources();
            Stats stats = GameData.PlayerStats;
            string name = stats != null ? stats.MyName : "";
            string className = stats != null ? RaidClassColors.ResolveClassNameForActor(stats, name) : "";
            string level = FindValue("LEVEL", "LVL");
            if (string.IsNullOrEmpty(level) && stats != null) level = stats.Level.ToString();
            _identity.text = string.IsNullOrEmpty(name) ? "CHARACTER" : name + "\n" +
                (string.IsNullOrEmpty(className) ? "" : className + "  ") +
                (string.IsNullOrEmpty(level) ? "" : "Level " + level);
            string aura = FindValue("AURA");
            _aura.text = string.IsNullOrEmpty(aura) ? "" : "Aura: " + aura;
            _available = 0;
            foreach (Field field in Specification)
            {
                Field output;
                if (!_fields.TryGetValue(field.Label, out output)) continue;
                string val = FindValue(field.Aliases);
                if (string.IsNullOrEmpty(val) && stats != null)
                {
                    if (field.Label == "HP") val = stats.CurrentHP + " / " + stats.CurrentMaxHP;
                    if (field.Label == "MP") val = stats.CurrentMana + " / " + stats.GetCurrentMaxMana();
                }
                output.Output.text = string.IsNullOrEmpty(val) ? "—" : val;
                if (!string.IsNullOrEmpty(val)) _available++;
            }
            if (!_gearReady) DiscoverNativeControls();
            SetNativePresentation(_nativeEquipmentSlots >= 8 && _nativeTabs == 3);
            if (_model != null) _model.Update();
            _availability.text = _available + "/19 live stats  |  " +
                _nativeEquipmentSlots + " equipped native controls | " + _nativeTabs +
                " linked tabs | " + (_nativeSuppressed ? "native artwork hidden" : "native controls preserved");
        }
        internal List<string> LastNativeTextSnapshot()
        {
            var result = new List<string>();
            for (int i = 0; i < _sources.Count && result.Count < 80; i++)
                if (!string.IsNullOrWhiteSpace(_sources[i].Value)) result.Add(_sources[i].Value);
            return result;
        }
        internal void InspectNativeValues()
        {
            Plugin.LogInfo("ArcaneUI character overview: " + _sources.Count +
                " native visible text labels, " + _available + "/19 matched stats.");
            int n = 0;
            for (int i = 0; i < _sources.Count && n < 80; i++)
            {
                string label = _sources[i].Value;
                if (string.IsNullOrWhiteSpace(label)) continue;
                Plugin.LogInfo("ArcaneUI character native text " + (++n) + ": " + label);
            }
        }
        internal void LogSlotDiagnostics() { InspectNativeSlots(); }
        internal void Destroy()
        {
            for (int i = 0; i < _nativeTabButtons.Count; i++)
                if (_nativeTabButtons[i] != null)
                    _nativeTabButtons[i].onClick.RemoveListener(_nativeTabHandlers[i]);
            _nativeTabButtons.Clear();
            _nativeTabHandlers.Clear();
            for (int i = 0; i < _nativeTabToggles.Count; i++)
                if (_nativeTabToggles[i] != null)
                    _nativeTabToggles[i].onValueChanged.RemoveListener(_nativeToggleHandlers[i]);
            _nativeTabToggles.Clear();
            _nativeToggleHandlers.Clear();
            SuspendNativeControls();
            RestoreControls();
            if (_model != null) { _model.Destroy(); _model = null; }
            _fields.Clear();
            _iconMirrors.Clear();
            if (_nativeOverlaySuppression != null)
                UnityEngine.Object.Destroy(_nativeOverlaySuppression);
            _nativeOverlaySuppression = null;
            if (_safePanel != null) UnityEngine.Object.Destroy(_safePanel.gameObject);
            _safePanel = null;
            _safeStatValues.Clear();
            if (_ownedStatsLayer != null)
                UnityEngine.Object.Destroy(_ownedStatsLayer.gameObject);
            if (_root != null) UnityEngine.Object.Destroy(_root.gameObject);
            _root = null;
        }
    }
}
