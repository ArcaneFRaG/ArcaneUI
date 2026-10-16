using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using ArcanesGroupFrames.Framework.Theming;
using ArcanesGroupFrames.Framework.NativeUI;
using ArcanesGroupFrames.Modules.CharacterSheet.Configuration;

namespace ArcanesGroupFrames.Modules.CharacterSheet.Native
{
    /// <summary>
    /// Character / equipment presentation whose rectangle IS the original
    /// InvPar rectangle.  No arbitrary full-screen overlay, no duplicated item
    /// icons, and no second inventory.  The original PlayerInv is retained in
    /// the same InvPar hierarchy.  The optional two-column view is activated
    /// only when all real native item and tab controls can be mapped uniquely.
    /// Otherwise the fully interactive original remains visible.
    /// </summary>
    internal sealed partial class CharacterSheetBoundedView
    {
        // Verified by the 2026-10-11 Erenshor in-game hierarchy dump.
        // Only the 17 equipment containers under InvPanel are visible here.
        // PlayerInv/PHSlot is a separate native helper slot, NOT an equipment
        // container. Keep it in its native hierarchy, without rehosting it.
        // Rehosting the slot CONTAINER (not its ItemIcon) retains native item
        // collision, tooltips, dragging, and the slot identity/name.
        private static readonly string[] Slots = {
            "HeadSlot", "NeckSlot", "ChestSlot", "BackSlot",
            "HandSlot", "ArmSlot", "LeftWristSlot", "RightWristSlot",
            "LegSlot", "WaistSlot", "FootSlot", "CharmSlotL",
            "RingSlotL", "RingSlotR", "Primary", "Secondary",
            "AuraSlot"
        };
        private static readonly string[] NativeTabNames = {
            "EquipButton", "AscButton", "Cosmetic"
        };
        private static readonly string[] StatNames = {
            "HP", "AC", "MP", "Str", "End", "Dex", "Agi", "Int", "Wis", "Cha",
            "Res", "Haste", "Atk Bonus", "HP Regen", "MP Regen", "Magic", "Poison", "Elem", "Void"
        };
        // Descriptive only. These hover notes do not derive, replace or modify
        // any native stat values; class/proficiency scaling remains game-owned.
        private static readonly string[] StatTooltipTitles = {
            "Hit Points", "Armor Class", "Mana Points", "Strength", "Endurance",
            "Dexterity", "Agility", "Intelligence", "Wisdom", "Charisma",
            "Resonance / Res", "Haste", "Attack Bonus", "Health Regeneration",
            "Mana Regeneration", "Magic Resistance", "Poison Resistance",
            "Elemental Resistance", "Void Resistance"
        };
        private static readonly string[] StatTooltipDescriptions = {
            "Your current and maximum health. Reaching zero HP defeats your character.",
            "Physical protection. Higher AC can cause incoming attacks to miss and helps mitigate hits.",
            "Your current and maximum mana, which is spent on spells and other mana-using actions.",
            "Influences melee and bow damage. Its exact contribution varies by class and proficiency.",
            "Improves maximum health and health regeneration; benefits depend on proficiency and class.",
            "Influences melee hit chance, critical chance, damage and blocking effectiveness.",
            "Helps determine armor class and the chance to evade physical attacks.",
            "Improves magical damage, spell criticals, maximum mana and wand damage.",
            "Improves healing spell strength and mana regeneration.",
            "Influences spell accuracy, magic effectiveness and resistance interactions.",
            "Resonance is the chance for a spell to repeat instantly without another mana cost. If your game version labels this Resilience, consult its native stat description.",
            "Reduces the delay between basic weapon attacks, increasing attack speed.",
            "An attack-roll bonus that helps physical attacks connect and can affect hit quality and criticals.",
            "How quickly your character regains health over time.",
            "How quickly your character regains mana over time.",
            "Helps reduce incoming magic-based damage. The game's native resistance rules determine mitigation.",
            "Helps reduce incoming poison damage and related effects.",
            "Helps reduce incoming elemental damage and related effects.",
            "Helps reduce incoming void damage and related effects."
        };
        private static readonly string[][] Aliases = {
            new [] {"HP", "HEALTH"}, new [] {"AC", "ARMOR CLASS", "ARMOUR CLASS"},
            new [] {"MP", "MANA"}, new [] {"STR", "STRENGTH"},
            new [] {"END", "ENDURANCE"}, new [] {"DEX", "DEXTERITY"},
            new [] {"AGI", "AGILITY"}, new [] {"INT", "INTELLIGENCE"},
            new [] {"WIS", "WISDOM"}, new [] {"CHA", "CHARISMA"},
            new [] {"RES", "RESILIENCE"}, new [] {"HASTE"},
            new [] {"ATK BONUS", "ATTACK BONUS"}, new [] {"HP REGEN", "HP REGENERATION"},
            new [] {"MP REGEN", "MANA REGEN"}, new [] {"MAGIC", "MAGIC RESIST"},
            new [] {"POISON", "POISON RESIST"}, new [] {"ELEM", "ELEMENTAL"},
            new [] {"VOID", "VOID RESIST"}
        };
        private sealed class NativeControl
        {
            internal RectTransform Rect;
            internal Transform Parent;
            internal int Sibling;
            internal Vector2 AnchorMin, AnchorMax, Pivot, Position, Size, OffsetMin, OffsetMax;
            internal Vector3 Scale;
            internal Quaternion Rotation;
        }
        private readonly RectTransform _native;
        private readonly ThemeManager _themes;
        private readonly List<NativeControl> _moved = new List<NativeControl>();
        private readonly List<RectTransform> _slotMatches = new List<RectTransform>();
        private readonly List<UnityEngine.UI.Selectable> _tabs = new List<UnityEngine.UI.Selectable>();
        private readonly List<string> _lastSlots = new List<string>();
        private readonly List<string> _lastTexts = new List<string>();
        private readonly List<string> _sourceValues = new List<string>();
        private readonly List<Vector3> _sourcePositions = new List<Vector3>();
        private readonly List<Button> _customTabButtons = new List<Button>();
        private RectTransform _nativeEquipment;
        private RectTransform _background;
        private RectTransform _panel;
        private RectTransform _equipmentHost;
        private RectTransform _tabsHost;
        private RectTransform _statsHost;
        private readonly List<RectTransform> _tabHosts = new List<RectTransform>();
        private RectTransform _nativeAscension;
        private RectTransform _nativeCosmetic;
        private RectTransform _nativeStatWindow;
        private RectTransform _alternateBackdrop;
        private Text _alternateHeading;
        private Text _alternateDescription;
        private sealed class NativeVisibility
        {
            internal CanvasGroup Group;
            internal bool Added;
            internal float Alpha;
            internal bool Interactable;
            internal bool BlocksRaycasts;
        }
        private readonly List<NativeVisibility> _nativeVisibility = new List<NativeVisibility>();
        private sealed class NativePageTint
        {
            internal Image Image;
            internal Color Original;
        }
        private readonly List<NativePageTint> _nativePageTints = new List<NativePageTint>();
        private readonly List<RectTransform> _slotHosts = new List<RectTransform>();
        private readonly List<Text> _values = new List<Text>();
        private readonly List<RectTransform> _tabIndicators = new List<RectTransform>();
        private Text _identity;
        private Image _classArtwork;
        private Text _classInitial;
        private Text _proficiencyValues;
        private RectTransform _statTooltip;
        private Text _statTooltipText;
        private string _lastProficiencySignature = "";
        private float _nextInspect;
        private float _nextStats;
        private bool _verified;
        private bool _shown;
        private int _activeTab;
        private int _available;
        private int _mappedSlots;
        private int _mappedTabs;
        private bool _loggedMissing;
        private bool _loggedMapped;
        private bool _boundsAvailable;
        internal int AvailableFields { get { return _available; } }
        internal int NativeSlots { get { return _mappedSlots; } }
        internal int NativeTabs { get { return _mappedTabs; } }
        internal bool IsCustomVisible { get { return _shown && _panel != null && _panel.gameObject.activeSelf; } }
        internal string Status
        {
            get
            {
                if (_verified)
                    return IsCustomVisible ? "Bounded " + CharacterSheetSettings.LayoutPreset + " character sheet visible (" + CharacterSheetSettings.VisualTheme + ")."
                        : "All 17 verified native equipment slot containers and 3 tabs mapped. Waiting for the native character page to open.";
                return "Two-column sheet waiting for verified Erenshor controls: " + _mappedSlots +
                    "/" + Slots.Length + " interactive equipment slots, " + _mappedTabs +
                    "/3 tabs found. Original Erenshor character/inventory window is preserved. " +
                    "Open the character sheet and use INSPECT SLOTS to identify the actual control names.";
            }
        }

        internal CharacterSheetBoundedView(RectTransform native, ThemeManager themes)
        {
            _native = native;
            _themes = themes;
        }

        private static bool Own(Transform node)
        {
            for (Transform t = node; t != null; t = t.parent)
                if (t.name.StartsWith("ArcaneUI", StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }
        private static bool Under(Transform child, Transform ancestor)
        {
            for (Transform t = child; t != null; t = t.parent)
                if (t == ancestor) return true;
            return false;
        }
        private static string Clean(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return "";
            return System.Text.RegularExpressions.Regex.Replace(raw, "<[^>]*>", "").Trim();
        }
        private bool NativeOpen()
        {
            if (_native == null || !_native.gameObject.activeInHierarchy ||
                ArcaneUiModalInputGuard.IsBlocking) return false;
            RectTransform playerInv = _native.Find("PlayerInv") as RectTransform;
            if (playerInv == null || !playerInv.gameObject.activeInHierarchy) return false;
            foreach (CanvasGroup g in _native.GetComponentsInParent<CanvasGroup>(true))
                if (g != null && g.enabled && g.alpha < .01f) return false;
            RectTransform equipment = playerInv.Find("InvPanel") as RectTransform;
            RectTransform ascension = _nativeAscension != null ? _nativeAscension :
                playerInv.Find("AAScreen") as RectTransform;
            RectTransform cosmetic = _nativeCosmetic != null ? _nativeCosmetic :
                playerInv.Find("CosmeticPanel") as RectTransform;
            return _shown || (equipment != null && equipment.gameObject.activeInHierarchy) ||
                (ascension != null && ascension.gameObject.activeInHierarchy) ||
                (cosmetic != null && cosmetic.gameObject.activeInHierarchy);
        }
        private int NativePage()
        {
            if (_nativeCosmetic != null && _nativeCosmetic.gameObject.activeSelf) return 2;
            if (_nativeAscension != null && _nativeAscension.gameObject.activeSelf) return 1;
            return 0;
        }
        private static bool HasOwnInput(RectTransform rect)
        {
            if (rect == null) return false;
            foreach (Component c in rect.GetComponents<Component>())
            {
                if (c == null) continue;
                if (c is IPointerClickHandler || c is IDropHandler || c is IBeginDragHandler || c is UnityEngine.UI.Selectable) return true;
                string type = c.GetType().Name;
                if (type.IndexOf("ItemIcon", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    type.IndexOf("EquipmentSlot", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            }
            return false;
        }
        private static bool SlotName(string n, string target)
        {
            string s = n.ToLowerInvariant().Replace("_", "").Replace(" ", "");
            string t = target.ToLowerInvariant().Replace(" ", "");
            if (target == "Primary") return s.Contains("primary") || s.Contains("mainhand");
            if (target == "Secondary") return s.Contains("secondary") || s.Contains("offhand");
            if (target == "Wrists") return s.Contains("wrist");
            if (target == "Ring") return s.Contains("ring") || s.Contains("finger");
            if (target == "Accessory") return s.Contains("accessory") || s.Contains("trinket");
            if (target == "Feet") return s.Contains("feet") || s.Contains("boot");
            if (target == "Legs") return s.Contains("leg") || s.Contains("pants");
            return s.Contains(t);
        }
        private static string TabName(UnityEngine.UI.Selectable tab)
        {
            if (tab == null) return "";
            string candidate = tab.name.ToLowerInvariant();
            string normalized = candidate.Replace("_", "").Replace(" ", "");
            if (normalized == "equipment" || normalized == "equipmenttab" ||
                normalized == "ascension" || normalized == "ascensiontab" ||
                normalized == "cosmetic" || normalized == "cosmetictab") return normalized;
            foreach (Text t in tab.GetComponentsInChildren<Text>(true))
            {
                string s = Clean(t.text).ToLowerInvariant();
                if (s == "equipment" || s == "ascension" || s == "cosmetic") return s;
            }
            foreach (TMP_Text t in tab.GetComponentsInChildren<TMP_Text>(true))
            {
                string s = Clean(t.text).ToLowerInvariant();
                if (s == "equipment" || s == "ascension" || s == "cosmetic") return s;
            }
            return "";
        }
        private bool Discover()
        {
            _slotMatches.Clear();
            _tabs.Clear();
            _lastSlots.Clear();
            _mappedSlots = 0;
            _mappedTabs = 0;
            _verified = false;
            if (_native == null) return false;

            RectTransform playerInv = _native.Find("PlayerInv") as RectTransform;
            RectTransform invPanel = playerInv != null
                ? playerInv.Find("InvPanel") as RectTransform : null;
            if (playerInv == null || invPanel == null) return false;

            // Only accept the 17 verified equipment containers under InvPanel.
            // PHSlot, inventory General slots, cosmetic slots and bare ItemIcon
            // children MUST NOT be rehosted as equipment.
            for (int i = 0; i < Slots.Length; i++)
            {
                RectTransform slot = invPanel.Find(Slots[i]) as RectTransform;
                if (slot == null) continue;

                bool hasNativeItem = false;
                foreach (Component component in slot.GetComponentsInChildren<Component>(true))
                {
                    if (component != null && component.GetType().Name == "ItemIcon" &&
                        Under(component.transform, slot))
                    {
                        hasNativeItem = true;
                        break;
                    }
                }
                if (!hasNativeItem) continue;
                _slotMatches.Add(slot);
                _lastSlots.Add(NativeCharacterSheetDecorator.FullPath(slot) +
                    " | native slot container; ItemIcon and BoxCollider2D retained");
            }

            // Exact names were confirmed from the logs. These are the THREE
            // real Unity UI Buttons whose existing OnClick handlers switch
            // native Equipment, Ascension, and Cosmetic pages.
            for (int i = 0; i < NativeTabNames.Length; i++)
            {
                Transform node = playerInv.Find(NativeTabNames[i]);
                Button nativeButton = node != null ? node.GetComponent<Button>() : null;
                if (nativeButton != null) _tabs.Add(nativeButton);
            }

            _mappedSlots = _slotMatches.Count;
            _mappedTabs = _tabs.Count;
            _verified = _mappedSlots == Slots.Length && _mappedTabs == NativeTabNames.Length;
            if (_verified) _loggedMissing = false;
            if (!_verified && !_loggedMissing)
            {
                _loggedMissing = true;
                Plugin.LogWarning("ArcaneUI bounded sheet: " + _mappedSlots + "/" + Slots.Length +
                    " native equipment containers, " + _mappedTabs + "/3 exact tabs. " +
                    "Keeping the native character window. Use INSPECT SLOTS if these change.");
            }
            else if (_verified && !_loggedMapped)
            {
                _loggedMapped = true;
                Plugin.LogInfo("ArcaneUI bounded sheet: mapped " + Slots.Length + " native equipment containers " +
                    "and all three page buttons from PlayerInv.");
            }
            return _verified;
        }
        // The observed PlayerInv RectTransform is the native character-window
        // boundary (450 x 825 on the test build). Keeping the custom view as
        // its CHILD ensures native ItemIcon and tab controls remain inside the
        // same PlayerInv ancestry after temporary reparenting. InvWindow stays
        // owned by the native game; no bag elements are copied or moved.
        private bool AlignNativeBounds()
        {
            if (_native == null || _panel == null) return false;
            RectTransform playerInv = _native.Find("PlayerInv") as RectTransform;
            if (playerInv == null || _panel.parent != playerInv ||
                playerInv.rect.width < 300f || playerInv.rect.height < 400f) return false;
            _panel.anchorMin = Vector2.zero;
            _panel.anchorMax = Vector2.one;
            _panel.pivot = new Vector2(.5f, .5f);
            _panel.offsetMin = Vector2.zero;
            _panel.offsetMax = Vector2.zero;
            _panel.localScale = Vector3.one;
            return true;
        }

        private static RectTransform Rect(Transform parent, string name, Vector2 min, Vector2 max,
            Vector2 offsetMin, Vector2 offsetMax)
        {
            var r = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            r.SetParent(parent, false);
            r.anchorMin = min;
            r.anchorMax = max;
            r.offsetMin = offsetMin;
            r.offsetMax = offsetMax;
            return r;
        }
        private static Text CreateText(Transform parent, string name, string value, int size,
            TextAnchor alignment, Color color)
        {
            var text = new GameObject(name, typeof(RectTransform), typeof(Text)).GetComponent<Text>();
            text.transform.SetParent(parent, false);
            text.rectTransform.anchorMin = Vector2.zero;
            text.rectTransform.anchorMax = Vector2.one;
            text.rectTransform.offsetMin = Vector2.zero;
            text.rectTransform.offsetMax = Vector2.zero;
            text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            text.fontSize = size;
            text.fontStyle = size >= 15 ? FontStyle.Bold : FontStyle.Normal;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = 10;
            text.resizeTextMaxSize = size;
            text.alignment = alignment;
            text.color = color;
            text.text = value;
            text.raycastTarget = false;
            return text;
        }
        private static RectTransform Box(Transform parent, string name, Vector2 min, Vector2 max, Color color,
            Vector2 offsetMin, Vector2 offsetMax)
        {
            RectTransform r = Rect(parent, name, min, max, offsetMin, offsetMax);
            Image image = r.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return r;
        }
        // Purely visual style tokens; no native UI controls or native materials are changed.
        private sealed class SheetPalette
        {
            internal Color Surface, Header, Equipment, Stats, Slot, Section;
            internal Color RowA, RowB, Accent, Body, Muted, Tab, Selected, Trim;
        }

        private static Color Hex(string value) { return CharacterSheetSettings.Parse(value, Color.white); }

        private SheetPalette Palette()
        {
            SheetPalette p;
            switch (CharacterSheetSettings.VisualTheme)
            {
                case "Refined Fantasy":
                    p = new SheetPalette {
                        Surface=Hex("#181720"), Header=Hex("#29212A"), Equipment=Hex("#24212A"),
                        Stats=Hex("#211F27"), Slot=Hex("#3B3032"), Section=Hex("#3A2930"),
                        RowA=Hex("#302A30"), RowB=Hex("#26232C"), Accent=Hex("#DFB76D"),
                        Body=Hex("#EEE3CC"), Muted=Hex("#AFA599"), Tab=Hex("#2C2730"),
                        Selected=Hex("#554034"), Trim=Hex("#94734A") };
                    break;
                case "Minimal Clean":
                    p = new SheetPalette {
                        Surface=Hex("#151B22"), Header=Hex("#1E2934"), Equipment=Hex("#1A242D"),
                        Stats=Hex("#1B2630"), Slot=Hex("#273540"), Section=Hex("#23323B"),
                        RowA=Hex("#25323D"), RowB=Hex("#1D2A34"), Accent=Hex("#8FCDCD"),
                        Body=Hex("#E8F1F4"), Muted=Hex("#B3C6CC"), Tab=Hex("#202D38"),
                        Selected=Hex("#2C5962"), Trim=Hex("#47656D") };
                    break;
                default: // Arcane Dark
                    p = new SheetPalette {
                        Surface=Hex("#141F2B"), Header=Hex("#172C3A"), Equipment=Hex("#182735"),
                        Stats=Hex("#182B38"), Slot=Hex("#213847"), Section=Hex("#1A3947"),
                        RowA=Hex("#233746"), RowB=Hex("#1C2D3B"), Accent=Hex("#D3AD70"),
                        Body=Hex("#EBF0F2"), Muted=Hex("#B9CCD2"), Tab=Hex("#20313F"),
                        Selected=Hex("#285366"), Trim=Hex("#437287") };
                    break;
            }
            // Optional per-character palette overrides keep the selected theme's
            // layered depth; plain PlayerPrefs colours do not replace native sprites.
            if (CharacterSheetSettings.UseCustomPalette)
            {
                Color baseColor = CharacterSheetSettings.Parse(CharacterSheetSettings.BackgroundHex, p.Surface);
                p.Surface = baseColor;
                p.Header = Color.Lerp(baseColor, p.Header, .46f);
                p.Equipment = Color.Lerp(baseColor, p.Equipment, .54f);
                p.Stats = Color.Lerp(baseColor, p.Stats, .54f);
                p.Section = Color.Lerp(baseColor, p.Section, .67f);
                p.RowA = Color.Lerp(baseColor, p.RowA, .7f);
                p.RowB = Color.Lerp(baseColor, p.RowB, .7f);
                p.Accent = CharacterSheetSettings.Parse(CharacterSheetSettings.AccentHex, p.Accent);
                p.Body = CharacterSheetSettings.Parse(CharacterSheetSettings.TextHex, p.Body);
            }
            // An explicit global-theme opt-in can override text/accent without
            // destroying each character preset's own layered surfaces.
            UiTheme global = _themes != null ? _themes.Current : null;
            if (CharacterSheetSettings.InheritTheme && global != null)
            {
                p.Accent = global.Accent;
                p.Body = global.Text;
            }
            return p;
        }

        private static void OutlineBox(RectTransform rect, Color color, float thickness)
        {
            if (rect == null || thickness <= 0f) return;
            Image img = rect.GetComponent<Image>();
            if (img == null) return;
            Outline outline = rect.gameObject.AddComponent<Outline>();
            outline.effectColor = color;
            outline.effectDistance = new Vector2(thickness, -thickness);
            outline.useGraphicAlpha = false;
        }

        // A lightweight, ArcaneUI-owned tooltip positioned inside PlayerInv.
        // It does not touch native tooltips, equipment, values or input bindings.
        private void ShowStatTooltip(int index)
        {
            if (_panel == null || !_shown || _activeTab != 0 ||
                index < 0 || index >= StatTooltipTitles.Length) return;
            if (_statTooltip == null)
            {
                SheetPalette palette = Palette();
                RectTransform popup = new GameObject("ArcaneUI_StatTooltip",
                    typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
                popup.SetParent(_panel, false);
                popup.anchorMin = popup.anchorMax = new Vector2(.5f, .5f);
                popup.pivot = new Vector2(.5f, .5f);
                popup.sizeDelta = new Vector2(252f, 94f);
                Image bg = popup.GetComponent<Image>();
                bg.color = new Color(palette.Surface.r, palette.Surface.g,
                    palette.Surface.b, .98f);
                bg.raycastTarget = false;
                OutlineBox(popup, palette.Accent, 1f);
                _statTooltipText = CreateText(popup, "ArcaneUI_StatTooltipText", "", 13,
                    TextAnchor.MiddleLeft, palette.Body);
                _statTooltipText.rectTransform.offsetMin = new Vector2(10f, 7f);
                _statTooltipText.rectTransform.offsetMax = new Vector2(-10f, -7f);
                _statTooltipText.horizontalOverflow = HorizontalWrapMode.Wrap;
                _statTooltipText.verticalOverflow = VerticalWrapMode.Truncate;
                _statTooltipText.resizeTextForBestFit = true;
                _statTooltipText.resizeTextMinSize = 11;
                _statTooltipText.resizeTextMaxSize = 13;
                _statTooltip = popup;
            }
            _statTooltipText.text = StatTooltipTitles[index] + "\n" +
                StatTooltipDescriptions[index];
            _statTooltip.gameObject.SetActive(true);
            _statTooltip.SetAsLastSibling();
            PositionStatTooltip();
        }

        private void HideStatTooltip()
        {
            if (_statTooltip != null) _statTooltip.gameObject.SetActive(false);
        }

        private void PositionStatTooltip()
        {
            if (_statTooltip == null || !_statTooltip.gameObject.activeSelf || _panel == null) return;
            Canvas canvas = _panel.GetComponentInParent<Canvas>();
            Camera camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? canvas.worldCamera : null;
            Vector2 cursor;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _panel, Input.mousePosition, camera, out cursor)) return;
            float w = _panel.rect.width * .5f, h = _panel.rect.height * .5f;
            float halfW = _statTooltip.rect.width * .5f;
            float halfH = _statTooltip.rect.height * .5f;
            float x = Mathf.Clamp(cursor.x - halfW - 16f, -w + halfW + 5f, w - halfW - 5f);
            float y = Mathf.Clamp(cursor.y + halfH + 10f, -h + halfH + 5f, h - halfH - 5f);
            _statTooltip.anchoredPosition = new Vector2(x, y);
        }

        private void CreatePanel()
        {
            if (_native == null || _panel != null) return;
            // The character view is a child of native PlayerInv, not a new
            // floating window. It fills exactly the native character bounds.
            RectTransform playerInv = _native.Find("PlayerInv") as RectTransform;
            if (playerInv == null) return;
            _panel = Rect(playerInv, "ArcaneUI_BoundedCharacterSheet", Vector2.zero,
                Vector2.one, Vector2.zero, Vector2.zero);
            _panel.SetAsLastSibling();
            _panel.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            SheetPalette theme = Palette();
            Color accent = theme.Accent;
            _background = Box(_panel, "ArcaneUI_Background", Vector2.zero, Vector2.one,
                theme.Surface, Vector2.zero, Vector2.zero);
            if (CharacterSheetSettings.DrawFrame)
                OutlineBox(_background, theme.Trim, CharacterSheetSettings.BorderThickness);
            // Layered heading and trim, fully inside the original PlayerInv rectangle.
            RectTransform header = Box(_panel, "ArcaneUI_Header", new Vector2(0f, .91f), Vector2.one,
                theme.Header, new Vector2(4f, 0f), new Vector2(-4f, -4f));
            _identity = CreateText(header, "ArcaneUI_CharacterIdentity", "CHARACTER", 15,
                CharacterSheetSettings.HeaderAlignment == "Left" ? TextAnchor.MiddleLeft : TextAnchor.MiddleCenter,
                theme.Body);
            // A genuine native class icon is mirrored (never reparented) into
            // the bounded header. FOTF replaces the native icon artwork for
            // its classes, so this also supports FOTF without shipping assets.
            RectTransform classBadge = Box(header, "ArcaneUI_ClassBadge",
                new Vector2(0f, .1f), new Vector2(0f, .9f), theme.Slot,
                new Vector2(11f, 0f), new Vector2(61f, 0f));
            OutlineBox(classBadge, theme.Trim, 1f);
            // The new badge overlays Erenshor's original inventory controls.
            // Forward its click to the real proficiency allocation button;
            // never replicate point-spending logic in ArcaneUI.
            Image badgeHit = classBadge.GetComponent<Image>();
            badgeHit.raycastTarget = true;
            Button proficiencyButton = classBadge.gameObject.AddComponent<Button>();
            proficiencyButton.targetGraphic = badgeHit;
            proficiencyButton.transition = Selectable.Transition.ColorTint;
            proficiencyButton.onClick.AddListener(OpenNativeProficiencies);
            RectTransform artworkRect = Rect(classBadge, "ArcaneUI_ClassArtwork",
                Vector2.zero, Vector2.one, new Vector2(3f, 3f), new Vector2(-3f, -3f));
            _classArtwork = artworkRect.gameObject.AddComponent<Image>();
            _classArtwork.preserveAspect = true;
            _classArtwork.raycastTarget = false;
            _classArtwork.enabled = false;
            _classInitial = CreateText(classBadge, "ArcaneUI_ClassInitial", "?", 20,
                TextAnchor.MiddleCenter, theme.Accent);
            // Leave a dedicated left gutter for the class icon in both
            // centre-aligned and left-aligned character header variants.
            _identity.alignment = TextAnchor.MiddleLeft;
            _identity.rectTransform.offsetMin = new Vector2(71f, 0f);
            _identity.rectTransform.offsetMax = new Vector2(-6f, 0f);
            _identity.resizeTextForBestFit = true;
            Box(header, "ArcaneUI_HeaderTrim", Vector2.zero, new Vector2(1f, .025f),
                theme.Trim, Vector2.zero, Vector2.zero);
            RectTransform content = Rect(_panel, "ArcaneUI_Columns", new Vector2(0f,.085f),
                new Vector2(1f,.91f), new Vector2(4f,4f), new Vector2(-4f,-3f));
            float equipWidth = CharacterSheetSettings.EquipmentWidth;
            _equipmentHost = Box(content, "ArcaneUI_Equipment", Vector2.zero,
                new Vector2(equipWidth, 1f), theme.Equipment,
                Vector2.zero, new Vector2(-3f,0f));
            _statsHost = Box(content, "ArcaneUI_Stats", new Vector2(equipWidth,0f),
                Vector2.one, theme.Stats, new Vector2(3f,0f), Vector2.zero);
            // The divider visually joins the two sections without intercepting input.
            Box(content, "ArcaneUI_ColumnDivider", new Vector2(equipWidth, .012f),
                new Vector2(equipWidth, .99f), theme.Trim,
                new Vector2(-.75f,0f), new Vector2(.75f,0f));
            if (CharacterSheetSettings.VisualTheme == "Refined Fantasy")
            {
                OutlineBox(_equipmentHost, theme.Trim, 1f);
                OutlineBox(_statsHost, theme.Trim, 1f);
            }
            RectTransform itemTitle = Rect(_equipmentHost, "ArcaneUI_EquipmentTitle", new Vector2(0,.94f),
                Vector2.one, Vector2.zero, Vector2.zero);
            CreateText(itemTitle, "ArcaneUI_ItemCaption", "EQUIPMENT", 12, TextAnchor.MiddleCenter, accent);
            RectTransform statsTitle = Rect(_statsHost, "ArcaneUI_StatsTitle", new Vector2(0,.94f),
                Vector2.one, Vector2.zero, Vector2.zero);
            CreateText(statsTitle, "ArcaneUI_StatsCaption", "STATS", 12, TextAnchor.MiddleCenter, accent);
            for (int i = 0; i < Slots.Length; i++)
            {
                int col = i % 2, row = i / 2;
                float left = col * .5f + .02f, right = (col+1)*.5f - .02f;
                // With an odd number of actual equipment slots, centre the
                // final Aura slot instead of displaying an empty helper tile.
                if (i == Slots.Length - 1 && Slots.Length % 2 != 0)
                {
                    left = .25f;
                    right = .75f;
                }
                float high = .928f - row * .101f, low = high - .095f;
                RectTransform host = Box(_equipmentHost, "ArcaneUI_EquipmentSlot_" + i,
                    new Vector2(left,low), new Vector2(right,high),
                    theme.Slot, Vector2.zero, Vector2.zero);
                if (CharacterSheetSettings.FrameEquipment)
                    OutlineBox(host, theme.Trim, Mathf.Clamp(CharacterSheetSettings.SlotThickness, 0f, 3f));
                _slotHosts.Add(host);
            }
            // Native-sized stats column: five labelled sections, all 19 values.
            // Header rows are included in the vertical budget so the last
            // resistance never gets clipped by the bottom tab bar.
            string[] sectionTitles = { "VITALS", "ATTRIBUTES", "COMBAT", "REGENERATION", "RESISTANCES" };
            int[] sectionStarts = { 0, 3, 11, 13, 15, StatNames.Length };
            float cursor = .94f;
            for (int section = 0; section < sectionTitles.Length; section++)
            {
                float headerHeight = .034f;
                RectTransform sectionHeader = Box(_statsHost, "ArcaneUI_StatsSection_" + section,
                    new Vector2(.025f, cursor - headerHeight), new Vector2(.975f, cursor),
                    theme.Section, Vector2.zero, Vector2.zero);
                CreateText(sectionHeader, "ArcaneUI_SectionLabel", sectionTitles[section], 13,
                    CharacterSheetSettings.HeaderAlignment == "Left" ? TextAnchor.MiddleLeft : TextAnchor.MiddleCenter, accent);
                if (CharacterSheetSettings.SectionDividers)
                    Box(sectionHeader, "ArcaneUI_SectionTrim", Vector2.zero,
                        new Vector2(1f,.065f), theme.Trim, Vector2.zero, Vector2.zero);
                cursor -= headerHeight + .004f;
                for (int i = sectionStarts[section]; i < sectionStarts[section + 1]; i++)
                {
                    const float rowHeight = .0285f;
                    RectTransform row = Box(_statsHost, "ArcaneUI_StatRow_" + i,
                        new Vector2(.025f, cursor - rowHeight), new Vector2(.975f, cursor),
                        i % 2 == 0 ? theme.RowA : theme.RowB, Vector2.zero, Vector2.zero);
                    // Wider stats area permits a readable, fixed font size
                    // without shrinking when values such as HP/MP are long.
                    RectTransform key = Rect(row, "Key", Vector2.zero, new Vector2(.5f, 1f),
                        new Vector2(7f, 0f), Vector2.zero);
                    int bodySize = Mathf.RoundToInt(CharacterSheetSettings.StatFontSize);
                    Text label = CreateText(key, "ArcaneUI_StatName", StatNames[i], bodySize,
                        TextAnchor.MiddleLeft, theme.Body);
                    label.resizeTextForBestFit = false;
                    RectTransform value = Rect(row, "Value", new Vector2(.5f, 0f), Vector2.one,
                        Vector2.zero, new Vector2(-7f, 0f));
                    Text amount = CreateText(value, "ArcaneUI_StatValue", "—", bodySize + 1,
                        TextAnchor.MiddleRight, accent);
                    amount.resizeTextForBestFit = false;
                    _values.Add(amount);
                    // One invisible-to-gameplay hover region covers both the
                    // label and its value; it never triggers a native action.
                    Image hitArea = row.GetComponent<Image>();
                    if (hitArea != null) hitArea.raycastTarget = true;
                    EventTrigger hover = row.gameObject.AddComponent<EventTrigger>();
                    int statIndex = i;
                    EventTrigger.Entry enter = new EventTrigger.Entry();
                    enter.eventID = EventTriggerType.PointerEnter;
                    enter.callback.AddListener(data => ShowStatTooltip(statIndex));
                    hover.triggers.Add(enter);
                    EventTrigger.Entry exit = new EventTrigger.Entry();
                    exit.eventID = EventTriggerType.PointerExit;
                    exit.callback.AddListener(data => HideStatTooltip());
                    hover.triggers.Add(exit);
                    cursor -= rowHeight + .002f;
                }
                cursor -= .003f;
            }
            // The original five stat groups remain. Their vertical spacing
            // is compacted slightly to restore the missing Proficiencies area
            // within PlayerInv, without covering the native bottom tab bar.
            float proficiencyTop = Mathf.Max(.104f, cursor - .004f);
            RectTransform proficiencySection = Box(_statsHost,
                "ArcaneUI_Proficiencies", new Vector2(.025f, .012f),
                new Vector2(.975f, proficiencyTop), theme.RowA,
                Vector2.zero, Vector2.zero);
            RectTransform proficiencyTitle = Box(proficiencySection,
                "ArcaneUI_ProficiencyHeading", new Vector2(0f, .72f),
                Vector2.one, theme.Section, Vector2.zero, Vector2.zero);
            CreateText(proficiencyTitle, "ArcaneUI_ProficiencyCaption", "PROFICIENCIES", 12,
                TextAnchor.MiddleCenter, accent);
            RectTransform proficiencyContent = Rect(proficiencySection,
                "ArcaneUI_ProficiencyContent", Vector2.zero,
                new Vector2(1f, .72f), new Vector2(6f, 0f), new Vector2(-6f, 0f));
            _proficiencyValues = CreateText(proficiencyContent,
                "ArcaneUI_ProficiencyValues", "—", 11,
                TextAnchor.MiddleCenter, theme.Body);
            _proficiencyValues.horizontalOverflow = HorizontalWrapMode.Wrap;
            _proficiencyValues.verticalOverflow = VerticalWrapMode.Truncate;
            _proficiencyValues.resizeTextForBestFit = true;
            _proficiencyValues.resizeTextMinSize = 10;
            _proficiencyValues.resizeTextMaxSize = 11;
            // Ascension and Cosmetic have dedicated page backdrops. Their real
            // native controls remain under PlayerInv (same hierarchy as before),
            // and are rendered above this backdrop when that page is selected.
            // Equipment stats/gear are never duplicated on these pages.
            _alternateBackdrop = Box(content, "ArcaneUI_AlternatePage",
                Vector2.zero, Vector2.one, theme.Stats,
                new Vector2(3f, 1f), new Vector2(-3f, -1f));
            RectTransform alternateTitle = Rect(_alternateBackdrop,
                "ArcaneUI_AlternateTitle", new Vector2(0f, .91f), Vector2.one,
                new Vector2(12f, 0f), new Vector2(-12f, 0f));
            _alternateHeading = CreateText(alternateTitle, "ArcaneUI_PageHeading",
                "ASCENSION", 16, TextAnchor.MiddleCenter, accent);
            RectTransform alternateHelp = Rect(_alternateBackdrop,
                "ArcaneUI_AlternateHint", Vector2.zero, new Vector2(1f, .08f),
                new Vector2(8f, 0f), new Vector2(-8f, 0f));
            _alternateDescription = CreateText(alternateHelp, "ArcaneUI_PageHint",
                "Native ascension skills and controls", 11, TextAnchor.MiddleCenter,
                theme.Muted);
            // 10.5S: two native-driven Ascension webs and a separate wardrobe
            // layout use the same character palette as the Equipment page.
            // If native controls cannot be mapped, Erenshor's original screen
            // remains accessible rather than displaying nonfunctional art.
            CreateAscensionWeb(_alternateBackdrop, theme);
            CreateCosmeticWardrobe(_alternateBackdrop, theme);
            _alternateBackdrop.gameObject.SetActive(false);
            _tabsHost = Rect(_panel, "ArcaneUI_TabBar", Vector2.zero,
                new Vector2(1f,.085f), new Vector2(4f,4f), new Vector2(-4f,-3f));
            // Tab bar sits above the native subpage content and continues to
            // accept clicks when the original AAScreen/CosmeticPanel is on top.
            UnityEngine.Canvas tabsCanvas = _tabsHost.gameObject.AddComponent<UnityEngine.Canvas>();
            tabsCanvas.overrideSorting = true;
            tabsCanvas.sortingOrder = 250;
            _tabsHost.gameObject.AddComponent<GraphicRaycaster>();
            string[] tabLabels = { "Equipment", "Ascension", "Cosmetic" };
            for (int i = 0; i < tabLabels.Length; ++i)
            {
                RectTransform host = Box(_tabsHost, "ArcaneUI_TabCell_" + i,
                    new Vector2(i/3f,0f), new Vector2((i+1)/3f,1f),
                    theme.Tab, new Vector2(2f,0f), new Vector2(-2f,0f));
                _tabHosts.Add(host);
                Image image = host.GetComponent<Image>();
                image.raycastTarget = true;
                Button control = host.gameObject.AddComponent<Button>();
                control.targetGraphic = image;
                int page = i;
                control.onClick.AddListener(() => SwitchTab(page));
                _customTabButtons.Add(control);
                CreateText(host, "ArcaneUI_TabLabel", tabLabels[i], 12,
                    TextAnchor.MiddleCenter, theme.Body);
                RectTransform indicator = Box(host, "ArcaneUI_ActiveTabTrim", Vector2.zero,
                    new Vector2(1f,.045f), theme.Accent, Vector2.zero, Vector2.zero);
                _tabIndicators.Add(indicator);
                indicator.gameObject.SetActive(i == 0);
                if (CharacterSheetSettings.VisualTheme == "Refined Fantasy")
                    OutlineBox(host, theme.Trim, 1f);
            }
            _panel.gameObject.SetActive(false);
            _boundsAvailable = AlignNativeBounds();
        }
        private void MoveNative(RectTransform rect, RectTransform parent, bool preserveSlotSize = false)
        {
            NativeControl s = new NativeControl {
                Rect = rect, Parent = rect.parent, Sibling = rect.GetSiblingIndex(),
                AnchorMin = rect.anchorMin, AnchorMax = rect.anchorMax,
                Pivot = rect.pivot, Position = rect.anchoredPosition, Size = rect.sizeDelta,
                OffsetMin = rect.offsetMin, OffsetMax = rect.offsetMax,
                Scale = rect.localScale, Rotation = rect.localRotation
            };
            _moved.Add(s);
            rect.SetParent(parent, false);
            if (preserveSlotSize)
            {
                // Keep actual 64x64 item collider, native ItemIcon, and visual
                // aspect instead of stretching it to an 80x65 grid cell.
                rect.anchorMin = new Vector2(.5f,.5f);
                rect.anchorMax = new Vector2(.5f,.5f);
                rect.pivot = new Vector2(.5f,.5f);
                rect.anchoredPosition = Vector2.zero;
                rect.sizeDelta = new Vector2(64f,64f);
            }
            else
            {
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
            }
            rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.identity;
        }
        private void RestoreControls()
        {
            for (int i = _moved.Count - 1; i >= 0; --i)
            {
                NativeControl s = _moved[i];
                if (s.Rect == null || s.Parent == null) continue;
                if (s.Rect.parent != s.Parent) s.Rect.SetParent(s.Parent, false);
                s.Rect.SetSiblingIndex(Mathf.Min(s.Sibling, s.Rect.parent.childCount - 1));
                s.Rect.anchorMin = s.AnchorMin;
                s.Rect.anchorMax = s.AnchorMax;
                s.Rect.pivot = s.Pivot;
                s.Rect.sizeDelta = s.Size;
                s.Rect.anchoredPosition = s.Position;
                s.Rect.offsetMin = s.OffsetMin;
                s.Rect.offsetMax = s.OffsetMax;
                s.Rect.localScale = s.Scale;
                s.Rect.localRotation = s.Rotation;
            }
            _moved.Clear();
        }
        private void SetNativeVisibility(RectTransform rect, bool hidden)
        {
            if (rect == null) return;
            CanvasGroup group = rect.GetComponent<CanvasGroup>();
            NativeVisibility original = null;
            for (int i = 0; i < _nativeVisibility.Count; i++)
                if (_nativeVisibility[i].Group == group && group != null)
                { original = _nativeVisibility[i]; break; }
            if (original == null)
            {
                bool added = group == null;
                if (added) group = rect.gameObject.AddComponent<CanvasGroup>();
                original = new NativeVisibility {
                    Group = group, Added = added, Alpha = group.alpha,
                    Interactable = group.interactable,
                    BlocksRaycasts = group.blocksRaycasts
                };
                _nativeVisibility.Add(original);
            }
            if (hidden)
            {
                group.alpha = 0f;
                group.interactable = false;
                group.blocksRaycasts = false;
            }
            else
            {
                group.alpha = original.Alpha;
                group.interactable = original.Interactable;
                group.blocksRaycasts = original.BlocksRaycasts;
            }
        }
        private void RestoreNativeVisibility()
        {
            for (int i = _nativeVisibility.Count - 1; i >= 0; --i)
            {
                NativeVisibility original = _nativeVisibility[i];
                if (original.Group == null) continue;
                original.Group.alpha = original.Alpha;
                original.Group.interactable = original.Interactable;
                original.Group.blocksRaycasts = original.BlocksRaycasts;
                if (original.Added) UnityEngine.Object.Destroy(original.Group);
            }
            _nativeVisibility.Clear();
        }
        private void TintNativePage(RectTransform rect)
        {
            if (rect == null) return;
            // Only the top-level panel backdrop; all buttons, skill icons,
            // cosmetic slots and other gameplay visuals remain untouched.
            Image image = rect.GetComponent<Image>();
            if (image == null) return;
            for (int i = 0; i < _nativePageTints.Count; i++)
                if (_nativePageTints[i].Image == image) return;
            _nativePageTints.Add(new NativePageTint { Image = image, Original = image.color });
            image.color = new Color(.085f, .13f, .17f, .97f);
        }
        private void RestoreNativePageTints()
        {
            foreach (NativePageTint entry in _nativePageTints)
                if (entry.Image != null) entry.Image.color = entry.Original;
            _nativePageTints.Clear();
        }
        private void PositionNativePage(RectTransform rect)
        {
            if (rect == null) return;
            // The page retains its original PlayerInv parent and native
            // handlers. Only its presentation rectangle changes temporarily.
            NativeControl saved = new NativeControl {
                Rect = rect, Parent = rect.parent, Sibling = rect.GetSiblingIndex(),
                AnchorMin = rect.anchorMin, AnchorMax = rect.anchorMax,
                Pivot = rect.pivot, Position = rect.anchoredPosition, Size = rect.sizeDelta,
                OffsetMin = rect.offsetMin, OffsetMax = rect.offsetMax,
                Scale = rect.localScale, Rotation = rect.localRotation
            };
            _moved.Add(saved);
            rect.anchorMin = new Vector2(0f, .085f);
            rect.anchorMax = new Vector2(1f, .91f);
            rect.offsetMin = new Vector2(5f, 4f);
            rect.offsetMax = new Vector2(-5f, -3f);
            rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.identity;
        }
        private void ApplyNativePage(int page)
        {
            if (_nativeEquipment != null) _nativeEquipment.gameObject.SetActive(page == 0);
            if (_nativeAscension != null) _nativeAscension.gameObject.SetActive(page == 1);
            if (_nativeCosmetic != null) _nativeCosmetic.gameObject.SetActive(page == 2);
        }
        private void ShowPage(int page)
        {
            HideStatTooltip();
            _activeTab = Mathf.Clamp(page, 0, 2);
            if (!_shown || _panel == null) return;
            bool equipment = _activeTab == 0;
            if (_equipmentHost != null) _equipmentHost.gameObject.SetActive(equipment);
            if (_statsHost != null) _statsHost.gameObject.SetActive(equipment);
            // Do not show the Equipment-only stat area on alternative pages.
            // The backing panel is retained, with the selected native page
            // drawn over it while ArcaneUI's persistent tab bar sorts above.
            if (_background != null) _background.gameObject.SetActive(true);
            if (_alternateBackdrop != null) _alternateBackdrop.gameObject.SetActive(!equipment);
            if (_alternateHeading != null)
                _alternateHeading.text = _activeTab == 1 ? "ASCENSION" : "COSMETICS";
            if (_alternateDescription != null)
                _alternateDescription.text = _activeTab == 1
                    ? "Choose your ascension skills · native point controls"
                    : "Cosmetic equipment and hide-slot controls";
            // The dedicated page visuals replace the old prototype headings;
            // native fallback remains visible on an unmapped game hierarchy.
            if (_alternateHeading != null) _alternateHeading.gameObject.SetActive(false);
            if (_alternateDescription != null) _alternateDescription.gameObject.SetActive(false);
            bool showWeb = _activeTab == 1 && _ascensionMapped && !_ascensionNativeView;
            if (_ascensionCustom != null)
                _ascensionCustom.gameObject.SetActive(showWeb);
            if (_ascensionViewToggle != null)
                _ascensionViewToggle.gameObject.SetActive(_activeTab == 1 && _ascensionMapped);
            SetNativeVisibility(_nativeAscension, showWeb);
            ShowCosmeticWardrobe(_activeTab == 2);
            // The native stat window is a separate persistent sibling in
            // PlayerInv; without hiding it it bleeds through BOTH other tabs.
            SetNativeVisibility(_nativeStatWindow, true);
            RectTransform selected = _activeTab == 1 ? _nativeAscension :
                (_activeTab == 2 ? _nativeCosmetic : null);
            // A mapped ArcaneUI page must remain above the original page.
            // Reordering native AAScreen to the top concealed the web on some
            // builds, leaving the unchanged Erenshor Ascension display.
            bool nativeFallback = _activeTab == 1 ? (!_ascensionMapped || _ascensionNativeView) :
                (_activeTab == 2 && !_cosmeticMapped);
            if (selected != null && nativeFallback) selected.SetAsLastSibling();
            else if (_panel != null) _panel.SetAsLastSibling();
            for (int i = 0; i < _customTabButtons.Count; i++)
            {
                Button button = _customTabButtons[i];
                if (button == null) continue;
                Image image = button.GetComponent<Image>();
                SheetPalette palette = Palette();
                if (image != null) image.color = i == _activeTab
                    ? palette.Selected : palette.Tab;
                if (i < _tabIndicators.Count && _tabIndicators[i] != null)
                    _tabIndicators[i].gameObject.SetActive(i == _activeTab);
            }
        }
        private void SwitchTab(int page)
        {
            if (!_shown || page < 0 || page >= _tabs.Count) return;
            // The ArcaneUI buttons are independent of the original buttons'
            // hierarchy, so equipment can always be selected after Ascension.
            // Dispatch the native OnClick first for all game-side changes.
            Button nativeButton = _tabs[page] as Button;
            try
            {
                if (nativeButton != null && nativeButton.onClick != null)
                    nativeButton.onClick.Invoke();
            }
            catch (Exception ex)
            {
                Plugin.LogWarning("ArcaneUI character native tab callback failed: " + ex.GetType().Name);
            }
            if (page == 1 && _activeTab != 1) _ascensionNativeView = false;
            // Reconcile the visible native pages even if a native button
            // toggled its previous state. This touches UI state, not skills.
            RectTransform playerInv = _native != null ? _native.Find("PlayerInv") as RectTransform : null;
            // Some native page callbacks deactivate PlayerInv rather than only
            // switching a child panel. Keep the shared sheet visible for a
            // deliberate tab click, but never reopen a closed InvPar root.
            if (playerInv != null && !playerInv.gameObject.activeSelf &&
                _native != null && _native.gameObject.activeInHierarchy)
                playerInv.gameObject.SetActive(true);
            ApplyNativePage(page);
            ShowPage(page);
            if (page == 1)
            {
                _nativeAscensionSwitch[0] = FindNativeAscensionSwitch("General");
                _nativeAscensionSwitch[1] = FindNativeAscensionSwitch("Class");
                SetAscensionMode(_ascensionMode);
            }
        }
        private void EnterCustom()
        {
            if (!_verified || _shown) return;
            CreatePanel();
            if (_panel == null || !AlignNativeBounds()) return;
            RectTransform playerInv = _native.Find("PlayerInv") as RectTransform;
            if (playerInv == null || _customTabButtons.Count != _tabs.Count) return;
            _nativeEquipment = playerInv.Find("InvPanel") as RectTransform;
            _nativeAscension = playerInv.Find("AAScreen") as RectTransform;
            _nativeCosmetic = playerInv.Find("CosmeticPanel") as RectTransform;
            _nativeStatWindow = playerInv.Find("StatWindow") as RectTransform;
            if (_nativeEquipment == null || _nativeAscension == null || _nativeCosmetic == null) return;
            for (int i = 0; i < Slots.Length; ++i)
                MoveNative(_slotMatches[i], _slotHosts[i], true);
            PositionNativePage(_nativeAscension);
            PositionNativePage(_nativeCosmetic);
            TintNativePage(_nativeAscension);
            TintNativePage(_nativeCosmetic);
            MountCosmeticWardrobe();
            // Suppress duplicate native tab artwork while preserving all
            // OnClick handlers; custom tabs invoke those exact handlers.
            for (int i = 0; i < _tabs.Count; i++)
                SetNativeVisibility(_tabs[i].transform as RectTransform, true);
            SetNativeVisibility(_nativeStatWindow, true);
            // Render the ArcaneUI frame above native pages; the background is
            // hidden for Ascension/Cosmetic so the native buttons receive input.
            _panel.SetAsLastSibling();
            _panel.gameObject.SetActive(true);
            _shown = true;
            ApplyNativePage(0);
            ShowPage(0);
        }
        private void LeaveCustom()
        {
            if (_shown)
            {
                // Erenshor's native tab state can survive a window close.
                // Reset to Equipment before releasing native controls, so the
                // next open never becomes trapped on the Ascension page.
                ApplyNativePage(0);
            }
            RestoreControls();
            LeaveCosmeticWardrobe();
            RestoreNativeVisibility();
            RestoreNativePageTints();
            _ascensionMapped = false;
            _ascensionNativeView = false;
            _ascensionSignature = "";
            _ascensionLines.Clear();
            _selectedAscensionLine = -1;
            _nativeAscensionSwitch[0] = null;
            _nativeAscensionSwitch[1] = null;
            HideStatTooltip();
            if (_panel != null) _panel.gameObject.SetActive(false);
            _shown = false;
            _activeTab = 0;
            _nativeEquipment = null;
            _nativeAscension = null;
            _nativeCosmetic = null;
            _nativeStatWindow = null;
        }
        private void RefreshValues()
        {
            _sourceValues.Clear();
            _sourcePositions.Clear();
            _lastTexts.Clear();
            if (_native == null) return;
            foreach (Text t in _native.GetComponentsInChildren<Text>(true))
                if (t != null && !Own(t.transform) && t.enabled)
                    AddNativeText(t.text, t.transform.position);
            foreach (TMP_Text t in _native.GetComponentsInChildren<TMP_Text>(true))
                if (t != null && !Own(t.transform) && t.enabled)
                    AddNativeText(t.text, t.transform.position);
            for (int n = 0; n < StatNames.Length; n++)
            {
                string value = "";
                for (int a = 0; a < Aliases[n].Length && value.Length == 0; a++)
                for (int k = 0; k < _sourceValues.Count && value.Length == 0; k++)
                {
                    string s = _sourceValues[k];
                    string prefix = Aliases[n][a];
                    if (!s.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) || s.Length <= prefix.Length) continue;
                    string rest = s.Substring(prefix.Length).Trim();
                    if (rest.StartsWith(":"))
                    {
                        value = rest.Substring(1).Trim();
                        if (value.Length == 0)
                        {
                            Vector3 origin = _native.InverseTransformPoint(_sourcePositions[k]);
                            float distance = float.MaxValue;
                            for (int j = 0; j < _sourceValues.Count; j++)
                            {
                                string candidate = _sourceValues[j];
                                if (candidate.Length == 0 || !char.IsDigit(candidate[0])) continue;
                                Vector3 other = _native.InverseTransformPoint(_sourcePositions[j]);
                                float dx = other.x - origin.x;
                                float dy = Mathf.Abs(other.y - origin.y);
                                if (dx < -3f || dx > 230f || dy > 15f) continue;
                                float d = dx + dy * 2f;
                                if (d < distance) { value = candidate; distance = d; }
                            }
                        }
                    }
                }
                Stats stats = GameData.PlayerStats;
                if (stats != null)
                {
                    if (n == 0) value = stats.CurrentHP + " / " + stats.CurrentMaxHP;
                    if (n == 2) value = stats.CurrentMana + " / " + stats.GetCurrentMaxMana();
                }
                _values[n].text = value.Length > 0 ? value : "—";
                if (value.Length > 0) _available++;
            }
            if (_identity != null)
            {
                Stats stats = GameData.PlayerStats;
                _identity.text = stats == null ? "CHARACTER" :
                    stats.MyName + "  ·  " + RaidClassColors.ResolveClassNameForActor(stats, stats.MyName) +
                    "  ·  Level " + stats.Level;
            }
            RefreshClassIcon();
            RefreshProficiencies();
        }

        // Read-only character decoration. Never remove, reparent, or change
        // the original ClassIcon component; FOTF's class icon patches own it.
        private void RefreshClassIcon()
        {
            if (_classArtwork == null) return;
            Stats player = GameData.PlayerStats;
            string resolved = player == null ? "" :
                RaidClassColors.ResolveClassNameForActor(player, player.MyName);
            Sprite icon = null;
            // Class objects (including FOTF expanded class identities) may
            // expose their artwork as a Sprite property or field.
            object classObject = player != null ? (object)player.CharacterClass : null;
            // The three original FOTF classes patch Erenshor's native
            // ClassIcon directly while their Stats class can still be an
            // Arcanist/Paladin donor. For those, prefer the patched image.
            bool nativePatchPreferred = string.Equals(resolved, "Blightcaller", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(resolved, "Emberweaver", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(resolved, "Lightbringer", StringComparison.OrdinalIgnoreCase);
            if (!nativePatchPreferred && classObject != null)
                icon = ReadClassSprite(classObject);
            // Native ClassIcon gets patched by the earlier FOTF class modules.
            // It also supplies the game's own icons for native characters.
            if (icon == null && _native != null)
            {
                RectTransform playerInv = _native.Find("PlayerInv") as RectTransform;
                if (playerInv != null)
                {
                    foreach (ClassIcon marker in playerInv.GetComponentsInChildren<ClassIcon>(true))
                    {
                        if (marker == null || Own(marker.transform)) continue;
                        Image source = marker.GetComponent<Image>();
                        if (source == null || source.sprite == null) continue;
                        icon = source.sprite;
                        break;
                    }
                }
            }
            if (icon == null && classObject != null) icon = ReadClassSprite(classObject);
            _classArtwork.sprite = icon;
            _classArtwork.color = Color.white;
            _classArtwork.enabled = icon != null;
            if (_classInitial != null)
            {
                _classInitial.gameObject.SetActive(icon == null);
                _classInitial.text = string.IsNullOrEmpty(resolved) ? "?" :
                    resolved.Substring(0, 1).ToUpperInvariant();
                _classInitial.color = RaidClassColors.GetColor(resolved);
            }
        }

        private static Sprite ReadClassSprite(object nativeClass)
        {
            if (nativeClass == null) return null;
            const System.Reflection.BindingFlags flags =
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Instance;
            Type type = nativeClass.GetType();
            foreach (string member in new [] {
                "ClassIcon", "ClassSprite", "IconSprite", "Icon", "Sprite", "ClassImage" })
            {
                try
                {
                    System.Reflection.FieldInfo field = type.GetField(member, flags);
                    if (field != null && typeof(Sprite).IsAssignableFrom(field.FieldType))
                    {
                        Sprite value = field.GetValue(nativeClass) as Sprite;
                        if (value != null) return value;
                    }
                    System.Reflection.PropertyInfo property = type.GetProperty(member, flags);
                    if (property != null && property.GetIndexParameters().Length == 0 &&
                        typeof(Sprite).IsAssignableFrom(property.PropertyType))
                    {
                        Sprite value = property.GetValue(nativeClass, null) as Sprite;
                        if (value != null) return value;
                    }
                }
                catch { /* An optional metadata field is not available. */ }
            }
            return null;
        }

        // The native proficiency '+' is not necessarily a Button underneath
        // PlayerInv. On some Erenshor builds it is a pointer component on an
        // inventory HUD sibling, while the allocation panel lives elsewhere in
        // UIElements. Never attempt to add points ourselves.
        private void OpenNativeProficiencies()
        {
            if (_native == null || GameData.PlayerStats == null) return;
            Transform inv = _native.Find("PlayerInv");
            if (inv == null) return;
            // The host is normally InvPar; UIElements is the shared native UI
            // parent containing the inventory and the proficiency popup.
            Transform searchRoot = _native.parent != null ? _native.parent : _native;
            Transform alreadyOpen = FindProficiencyPopup(searchRoot, true);
            if (alreadyOpen != null)
            {
                Plugin.LogInfo("ArcaneUI class icon: native proficiency allocation popup already open.");
                return;
            }

            // Prefer exact native inventory opening callbacks (including
            // non-Button pointer controls and runtime-registered UI events).
            // Never forward a click to an allocation row's '+' control.
            var candidates = new List<ProficiencyOpenControl>();
            Transform popupContainer = FindProficiencyPopup(searchRoot, false);
            foreach (Button button in searchRoot.GetComponentsInChildren<Button>(true))
            {
                if (button == null || Own(button.transform) ||
                    (popupContainer != null && Under(button.transform, popupContainer))) continue;
                int score = ProficiencyOpenScore(button.transform, inv, button);
                if (score > 0)
                    candidates.Add(new ProficiencyOpenControl { Node = button.transform,
                        Button = button, Score = score });
            }
            foreach (MonoBehaviour component in searchRoot.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (component == null || component is Button ||
                    !(component is IPointerClickHandler) || Own(component.transform) ||
                    (popupContainer != null && Under(component.transform, popupContainer))) continue;
                // Do not prefer generic artwork or arbitrary Unity click targets.
                int score = ProficiencyOpenScore(component.transform, inv, null);
                if (score > 0)
                    candidates.Add(new ProficiencyOpenControl { Node = component.transform,
                        Pointer = component as IPointerClickHandler, Score = score });
            }
            ProficiencyOpenControl best = null;
            bool ambiguous = false;
            foreach (ProficiencyOpenControl candidate in candidates)
            {
                if (candidate.Score < 14) continue;
                if (best == null || candidate.Score > best.Score)
                {
                    best = candidate;
                    ambiguous = false;
                }
                else if (candidate.Score == best.Score && candidate.Node != best.Node)
                    ambiguous = true;
            }
            if (best != null && !ambiguous)
            {
                try
                {
                    if (best.Button != null)
                        best.Button.onClick.Invoke();
                    else if (EventSystem.current != null && best.Pointer != null)
                    {
                        var data = new PointerEventData(EventSystem.current);
                        data.button = PointerEventData.InputButton.Left;
                        ExecuteEvents.Execute(best.Node.gameObject, data, ExecuteEvents.pointerClickHandler);
                    }
                    else throw new InvalidOperationException("Native pointer event system unavailable");
                    Plugin.LogInfo("ArcaneUI class icon: invoked native proficiency opener " +
                        NativeCharacterSheetDecorator.FullPath(best.Node) +
                        " (score " + best.Score + ").");
                    return;
                }
                catch (Exception ex)
                {
                    Plugin.LogWarning("ArcaneUI class icon: native opener failed: " + ex.GetType().Name);
                }
            }

            // Some versions expose the allocation panel directly but have
            // no discoverable opening Button. Show ONLY one positively identified
            // inactive native popup, never a guessed child or unrelated UI.
            Transform popup = popupContainer;
            if (popup != null)
            {
                try
                {
                    popup.gameObject.SetActive(true);
                    popup.SetAsLastSibling();
                    Plugin.LogInfo("ArcaneUI class icon: opened native proficiency panel " +
                        NativeCharacterSheetDecorator.FullPath(popup) + ".");
                    return;
                }
                catch (Exception ex)
                {
                    Plugin.LogWarning("ArcaneUI class icon: native panel activation failed: " + ex.GetType().Name);
                }
            }

            // The logged Proficiency Level Up hierarchy is a *panel*.
            // If the exact native panel could not be positively validated,
            // report its state without guessing which child to click.
            Transform proficiencyHost = searchRoot.Find("Proficiency Level Up");
            if (proficiencyHost != null)
            {
                int uiButtons = proficiencyHost.GetComponentsInChildren<Button>(true).Length;
                int pointerControls = 0;
                foreach (MonoBehaviour behaviour in proficiencyHost.GetComponentsInChildren<MonoBehaviour>(true))
                    if (behaviour != null && behaviour is IPointerClickHandler)
                        pointerControls++;
                Plugin.LogWarning("ArcaneUI class icon: native proficiency host " +
                    NativeCharacterSheetDecorator.FullPath(proficiencyHost) +
                    " activeSelf=" + proficiencyHost.gameObject.activeSelf +
                    " activeInHierarchy=" + proficiencyHost.gameObject.activeInHierarchy +
                    " buttons=" + uiButtons + " pointerHandlers=" + pointerControls +
                    " (not forcibly activated). ");
            }
            Plugin.LogWarning("ArcaneUI class icon: native proficiency opener unresolved " +
                (ambiguous ? "(ambiguous candidates)." : "(no matching native control).") +
                " No proficiency points were modified. Candidate inventory controls follow:");
            // One bounded diagnostic on failure is more useful than repeatedly
            // guessing at hidden Unity controls. It contains no character data.
            int emitted = 0;
            candidates.Sort((a,b) => b.Score.CompareTo(a.Score));
            foreach (ProficiencyOpenControl candidate in candidates)
            {
                if (emitted++ >= 12) break;
                Plugin.LogInfo("ArcaneUI prof candidate: " + candidate.Score + " " +
                    NativeCharacterSheetDecorator.FullPath(candidate.Node));
            }
        }

        private sealed class ProficiencyOpenControl
        {
            internal Transform Node;
            internal Button Button;
            internal IPointerClickHandler Pointer;
            internal int Score;
        }

        private static bool ProfWord(string value)
        {
            return value.IndexOf("prof", StringComparison.OrdinalIgnoreCase) >= 0 ||
                value.IndexOf("allocation", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool ProfSpendingControl(string low)
        {
            return low.Contains("respec") || low.Contains("reset") ||
                low.Contains("ascension") || low.Contains("close") ||
                low.Contains("cancel") || low.Contains("decrease") ||
                low.Contains("increment") || low.Contains("spendpoint") ||
                low.Contains("addprofpoint") || low.Contains("allocpoint") ||
                low.Contains("assignpoint");
        }

        private static int ProficiencyOpenScore(Transform node, Transform inv, Button button)
        {
            // Avoid popup rows where '+' means spending points, not opening
            // the menu. Keep all candidate controls in native UI ancestry.
            if (node == null || Own(node)) return 0;
            Transform t = node;
            string names = "";
            for (int i=0; t != null && i<4; i++, t=t.parent)
                names += " " + t.name;
            string low = names.ToLowerInvariant();
            if (ProfSpendingControl(low)) return 0;
            int score = 0;
            if (low.Contains("proficien")) score += 19;
            else if (low.Contains("prof")) score += 15;
            if (low.Contains("point") && (low.Contains("open") || low.Contains("menu") ||
                low.Contains("button") || low.Contains("plus"))) score += 9;
            if (low.Contains("allocat") && low.Contains("button")) score += 4;
            if (low.Contains("toggle") || low.Contains("show") || low.Contains("open")) score += 10;
            // Capture meaningful sprite names on icon-only native controls.
            Image img = node.GetComponent<Image>();
            if (img != null && img.sprite != null)
            {
                string sprite = img.sprite.name;
                if (ProfWord(sprite)) score += 17;
            }
            if (button != null && button.onClick != null)
                for (int k=0; k<button.onClick.GetPersistentEventCount(); k++)
                {
                    string method = button.onClick.GetPersistentMethodName(k);
                    if (ProfSpendingControl(method.ToLowerInvariant())) return 0;
                    if (ProfWord(method)) score += 20;
                }
            // The native '+' can be a child of the inventory header with no
            // descriptive GameObject name. Only identify it by geography if
            // it is also labelled as a point/proficiency control; generic
            // plus icons are deliberately never invoked.
            if (low.Contains("point") && Under(node, inv)) score += 4;
            if (low.Contains("plus") && (low.Contains("prof") || low.Contains("point"))) score += 5;
            return score;
        }

        // Verified from the live UI hierarchy (2026-10-13):
        // DontDestroyOnLoad:UI/UIElements/Proficiency Level Up has eight
        // Image children that scored equally in the old opener search.
        // Those are CONTENT of one native proficiency panel, not eight
        // independent inventory opener buttons. Resolve their common native
        // parent first; never click an individual row or '+' while opening it.
        private static Transform FindNamedNativeProficiencyPanel(Transform root, bool requireOpen)
        {
            if (root == null) return null;
            Transform nativeUi = root.name == "UIElements" ? root : root.Find("UIElements");
            if (nativeUi == null || Own(nativeUi)) return null;
            Transform panel = nativeUi.Find("Proficiency Level Up");
            if (panel == null || Own(panel)) return null;
            RectTransform rect = panel as RectTransform;
            if (rect == null) return null;

            // Seven proficiency allocations plus the eighth native control
            // appear in the observed hierarchy. Requiring at least seven
            // distinct interactive children prevents treating a similarly
            // named particle/image effect as a modal menu.
            var controls = new HashSet<GameObject>();
            foreach (Button button in panel.GetComponentsInChildren<Button>(true))
                if (button != null && !Own(button.transform))
                    controls.Add(button.gameObject);
            foreach (MonoBehaviour component in panel.GetComponentsInChildren<MonoBehaviour>(true))
                if (component != null && component is IPointerClickHandler &&
                    !Own(component.transform))
                    controls.Add(component.gameObject);
            if (controls.Count < 7) return null;

            // Never activate a hidden parent that would reveal unrelated UI.
            if (panel.parent == null || !panel.parent.gameObject.activeInHierarchy)
                return null;
            if (requireOpen)
            {
                if (!panel.gameObject.activeInHierarchy) return null;
                foreach (CanvasGroup group in panel.GetComponentsInParent<CanvasGroup>(true))
                    if (group != null && group.enabled && group.alpha < 0.01f)
                        return null;
            }
            else if (panel.gameObject.activeSelf) return null;
            return panel;
        }

        private static Transform FindProficiencyPopup(Transform root, bool requireOpen)
        {
            if (root == null) return null;
            // Prefer the one exact native parent exposed by the user's log.
            Transform named = FindNamedNativeProficiencyPanel(root, requireOpen);
            if (named != null) return named;
            Transform found = null;
            foreach (RectTransform panel in root.GetComponentsInChildren<RectTransform>(true))
            {
                if (panel == null || Own(panel) || panel.rect.width < 180f ||
                    panel.rect.height < 120f) continue;
                string low = panel.name.ToLowerInvariant();
                if (!ProfWord(low) || !(
                    low.Contains("panel") || low.Contains("window") ||
                    low.Contains("screen") || low.Contains("popup") ||
                    low.Contains("allocation"))) continue;
                if (low.Contains("button") || low.Contains("title") ||
                    low.Contains("tooltip") || low.Contains("respec") ||
                    low.Contains("icon")) continue;
                bool isOpen = panel.gameObject.activeInHierarchy;
                if (requireOpen && !isOpen) continue;
                if (!requireOpen && (panel.gameObject.activeSelf ||
                    panel.parent == null || !panel.parent.gameObject.activeInHierarchy)) continue;
                // The selected popup must be self-contained: never activate
                // the whole inventory tree or a container that owns it.
                if (root == panel || panel.GetComponentInChildren<Button>(true) == null) continue;
                if (found != null && found != panel) return null;
                found = panel;
            }
            if (found != null) return found;
            // Older native versions give the panel a generic GameObject name.
            // Only accept an exact allocation title and an inactive popup
            // ancestor with its own interactive proficiency rows.
            if (requireOpen) return null;
            foreach (Text label in root.GetComponentsInChildren<Text>(true))
            {
                if (label == null || Own(label.transform) ||
                    !IsProficiencyAllocationTitle(label.text)) continue;
                Transform panel = PopupAncestorFromTitle(label.transform, root);
                if (panel == null) continue;
                if (found != null && found != panel) return null;
                found = panel;
            }
            foreach (TextMeshProUGUI label in root.GetComponentsInChildren<TextMeshProUGUI>(true))
            {
                if (label == null || Own(label.transform) ||
                    !IsProficiencyAllocationTitle(label.text)) continue;
                Transform panel = PopupAncestorFromTitle(label.transform, root);
                if (panel == null) continue;
                if (found != null && found != panel) return null;
                found = panel;
            }
            return found;
        }

        private static bool IsProficiencyAllocationTitle(string title)
        {
            if (string.IsNullOrEmpty(title)) return false;
            string low = Clean(title).ToLowerInvariant();
            return low.Contains("proficiency point allocation") ||
                low.Contains("allocate proficiency points");
        }

        private static Transform PopupAncestorFromTitle(Transform title, Transform root)
        {
            for (Transform t = title.parent; t != null && t != root; t = t.parent)
            {
                RectTransform rect = t as RectTransform;
                if (rect == null || t.gameObject.activeSelf || Own(t) ||
                    t.parent == null || !t.parent.gameObject.activeInHierarchy ||
                    rect.rect.width < 180f || rect.rect.height < 120f) continue;
                if (t.GetComponentsInChildren<Button>(true).Length < 2) continue;
                return t;
            }
            return null;
        }

        private void RefreshProficiencies()
        {
            if (_proficiencyValues == null) return;
            var learned = new List<string>();
            try
            {
                Stats player = GameData.PlayerStats;
                Character actor = player != null ? player.Myself : null;
                UseSkill skillbook = actor != null ? actor.MySkills : null;
                if (skillbook == null && GameData.PlayerControl != null)
                    skillbook = GameData.PlayerControl.GetComponent<UseSkill>();
                if (skillbook != null && skillbook.KnownSkills != null)
                {
                    foreach (Skill skill in skillbook.KnownSkills)
                    {
                        if (skill == null || string.IsNullOrWhiteSpace(skill.SkillName) ||
                            skill.SkillName.IndexOf("Proficiency", StringComparison.OrdinalIgnoreCase) < 0)
                            continue;
                        if (!learned.Contains(skill.SkillName)) learned.Add(skill.SkillName);
                    }
                }
                // Some game versions expose proficiency labels through their
                // Class asset, rather than as learned Skill entries. Only copy
                // explicit metadata; never infer allowed weapons/armor.
                if (player != null && player.CharacterClass != null)
                {
                    object cls = player.CharacterClass;
                    const System.Reflection.BindingFlags flags =
                        System.Reflection.BindingFlags.Public |
                        System.Reflection.BindingFlags.NonPublic |
                        System.Reflection.BindingFlags.Instance;
                    foreach (string member in new [] { "Proficiencies", "WeaponProficiencies",
                        "ArmorProficiencies", "ArmourProficiencies" })
                    {
                        object value = null;
                        try
                        {
                            System.Reflection.FieldInfo field = cls.GetType().GetField(member, flags);
                            if (field != null) value = field.GetValue(cls);
                            if (value == null)
                            {
                                System.Reflection.PropertyInfo property = cls.GetType().GetProperty(member, flags);
                                if (property != null && property.GetIndexParameters().Length == 0)
                                    value = property.GetValue(cls, null);
                            }
                        }
                        catch { continue; }
                        if (value is string)
                        {
                            string literal = ((string)value).Trim();
                            if (literal.Length > 0 && !learned.Contains(literal)) learned.Add(literal);
                        }
                        else if (value is System.Collections.IEnumerable)
                        {
                            foreach (object entry in (System.Collections.IEnumerable)value)
                            {
                                if (entry == null || learned.Count >= 8) break;
                                string item = entry.ToString().Trim();
                                if (item.Length > 0 && !learned.Contains(item)) learned.Add(item);
                            }
                        }
                    }
                }
            }
            catch { /* Optional proficiency sources must not break the sheet. */ }
            learned.Sort(StringComparer.OrdinalIgnoreCase);
            // Character-sheet real estate is bounded; show verified learned
            // proficiency names, not guessed ranks or eligibility values.
            string output = learned.Count == 0 ? "—" :
                string.Join("  ·  ", learned.GetRange(0, Math.Min(4, learned.Count)).ToArray()) +
                (learned.Count > 4 ? "  +" + (learned.Count - 4) + " more" : "");
            if (_lastProficiencySignature != output)
            {
                _lastProficiencySignature = output;
                _proficiencyValues.text = output;
            }
        }

        private void AddNativeText(string raw, Vector3 pos)
        {
            foreach (string line in Clean(raw).Split(new[] {'\n','\r'}, StringSplitOptions.RemoveEmptyEntries))
            {
                string value = line.Trim();
                if (value.Length == 0 || value.Length > 150) continue;
                _sourceValues.Add(value);
                _sourcePositions.Add(pos);
                if (_lastTexts.Count < 120) _lastTexts.Add(value);
            }
        }
        internal void Update()
        {
            if (_native == null) return;
            bool open = NativeOpen();
            if (!open)
            {
                if (_shown) LeaveCustom();
                _activeTab = 0;
                return;
            }
            if (Time.unscaledTime >= _nextInspect)
            {
                _nextInspect = Time.unscaledTime + 2f;
                if (!_shown) Discover();
            }
            bool enabled = CharacterSheetSettings.ShowOverview && !CharacterSheetSettings.SafeNativeMode;
            if (enabled && _verified && !_shown)
            {
                RectTransform playerInv = _native.Find("PlayerInv") as RectTransform;
                RectTransform equip = playerInv != null ? playerInv.Find("InvPanel") as RectTransform : null;
                if (equip != null && equip.gameObject.activeInHierarchy) EnterCustom();
            }
            if (_shown && (!enabled || !AlignNativeBounds())) LeaveCustom();
            if (_shown)
            {
                PositionStatTooltip();
                // Custom navigation is authoritative while mounted. Native
                // page objects stay in their original hierarchy for gameplay.
                if (Time.unscaledTime >= _nextStats)
                {
                    _nextStats = Time.unscaledTime + .35f;
                    _available = 0;
                    if (_activeTab == 0) RefreshValues();
                }
                if (_activeTab == 1) RefreshAscensionWeb(false);
            }
        }
        internal void RefreshNow() { _nextInspect = 0f; _nextStats = 0f; Update(); }
        internal void LogSlotDiagnostics()
        {
            if (_lastSlots.Count == 0) Discover();
            Plugin.LogInfo("ArcaneUI bounded character controls: " + _mappedSlots + "/" + Slots.Length +
                " slots, " + _mappedTabs + "/3 native tabs, inventory child=" +
                (_native != null && _native.Find("PlayerInv") != null));
            for (int i = 0; i < _lastSlots.Count; i++)
                Plugin.LogInfo("ArcaneUI equipment node " + (i+1) + ": " + _lastSlots[i]);
            // Log the actual native hierarchy, including components attached to
            // slot ancestors rather than the icon child. This is diagnostic only:
            // no item controls are moved, detached or modified.
            if (_native == null) return;
            RectTransform[] nodes = _native.GetComponentsInChildren<RectTransform>(true);
            int logged = 0;
            for (int i = 0; i < nodes.Length && logged < 240; ++i)
            {
                RectTransform r = nodes[i];
                if (r == null || Own(r) || r == _native) continue;
                string name = r.name.ToLowerInvariant();
                bool relevantName = name.Contains("slot") || name.Contains("equip") ||
                    name.Contains("item") || name.Contains("ring") || name.Contains("hand") ||
                    name.Contains("ascension") || name.Contains("cosmetic") || name.Contains("tab") ||
                    name.Contains("gear") || name.Contains("playerinv") || name.Contains("aura");
                if (!relevantName && !HasOwnInput(r)) continue;
                Component[] comps = r.GetComponents<Component>();
                var types = new System.Text.StringBuilder();
                for (int j = 0; j < comps.Length; j++)
                {
                    if (comps[j] == null) continue;
                    if (types.Length > 0) types.Append(",");
                    types.Append(comps[j].GetType().Name);
                }
                Plugin.LogInfo("ArcaneUI character hierarchy " + (++logged) + ": " +
                    NativeCharacterSheetDecorator.FullPath(r) +
                    " active=" + r.gameObject.activeInHierarchy +
                    " size=" + Mathf.RoundToInt(r.rect.width) + "x" + Mathf.RoundToInt(r.rect.height) +
                    " components=" + types);
            }
        }
        internal void InspectNativeValues()
        {
            Plugin.LogInfo("ArcaneUI native character text snapshot: " + _lastTexts.Count);
            for (int i = 0; i < _lastTexts.Count; i++)
                Plugin.LogInfo("ArcaneUI character native text " + (i+1) + ": " + _lastTexts[i]);
        }
        internal List<string> LastNativeSlotSnapshot() { return new List<string>(_lastSlots); }
        internal List<string> LastNativeTextSnapshot() { return new List<string>(_lastTexts); }
        internal void Destroy()
        {
            LeaveCustom();
            _customTabButtons.Clear();
            _tabIndicators.Clear();
            if (_panel != null) UnityEngine.Object.Destroy(_panel.gameObject);
            _panel = null;
            _statTooltip = null;
            _statTooltipText = null;
        }
    }
}
