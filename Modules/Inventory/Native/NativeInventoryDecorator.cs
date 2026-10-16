using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using ArcanesGroupFrames;
using ArcanesGroupFrames.Framework.Modules;
using ArcanesGroupFrames.Framework.Theming;
using ArcanesGroupFrames.Modules.Inventory.Configuration;
using ArcanesGroupFrames.Modules.CharacterSheet.Native;

namespace ArcanesGroupFrames.Modules.Inventory.Native
{
    /// <summary>
    /// ArcaneUI inventory presentation on Erenshor's verified bag window:
    /// UI/UIElements/InvPar/PlayerInv/InvWindow.  The native General (n)
    /// slot GameObjects, their ItemIcon scripts and BoxCollider2D components
    /// remain exactly where they are.  All ArcaneUI graphics are non-raycast
    /// children which are destroyed on disable, leaving native play intact.
    /// </summary>
    internal sealed class NativeInventoryDecorator
    {
        private readonly ThemeManager _themes;
        private RectTransform _root;
        private Image _nativeBackground;
        private Color _originalColor;
        private GameObject _decoration;
        private readonly List<GameObject> _slotDecorations = new List<GameObject>();
        private float _nextLookup;
        private string _status = "Waiting for native InvPar/PlayerInv/InvWindow.";
        private int _lastSlotCount;
        private bool _loggedBound;
        internal string Status { get { return _status; } }
        internal int SlotBorders { get { return _lastSlotCount; } }
        internal string NativePath { get { return _root != null ? NativeCharacterSheetDecorator.FullPath(_root) :
            "Auto: UI/UIElements/InvPar/PlayerInv/InvWindow"; } }
        internal NativeInventoryDecorator(ThemeManager themes) { _themes = themes; }

        private static bool IsOwn(Transform t)
        {
            for (; t != null; t = t.parent)
                if (t.name.StartsWith("ArcaneUI", StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        private static bool IsVerifiedBagWindow(RectTransform r)
        {
            return r != null && r.name == "InvWindow" &&
                r.parent != null && r.parent.name == "PlayerInv" &&
                r.parent.parent != null && r.parent.parent.name == "InvPar" &&
                r.parent.parent.parent != null && r.parent.parent.parent.name == "UIElements" &&
                r.GetComponent<Canvas>() == null && !IsOwn(r);
        }

        private static RectTransform FindNativeInventory(bool includeInactive)
        {
            RectTransform match = null;
            foreach (RectTransform candidate in UnityEngine.Object.FindObjectsOfType<RectTransform>(includeInactive))
            {
                if (!IsVerifiedBagWindow(candidate)) continue;
                if (match != null) return null;
                match = candidate;
            }
            return match;
        }

        internal void Update()
        {
            if (!ArcaneUiModuleSettings.InventoryEnabled || !NativeUiLayering.CanShowGameplayUi)
            { if (_root != null) Restore(); return; }
            if (_root != null)
            {
                if (!IsVerifiedBagWindow(_root) || !_root.gameObject.activeInHierarchy) Restore();
                else return;
            }
            if (Time.unscaledTime < _nextLookup) return;
            _nextLookup = Time.unscaledTime + 1.5f;
            RectTransform match = FindNativeInventory(false);
            if (match == null)
            {
                _status = "Open the native Inventory tab: waiting for active PlayerInv/InvWindow.";
                return;
            }
            Bind(match);
        }

        private void Bind(RectTransform root)
        {
            Restore();
            _root = root;
            _nativeBackground = root.GetComponent<Image>();
            if (_nativeBackground != null) _originalColor = _nativeBackground.color;
            ApplyStyle();
            if (!_loggedBound)
            {
                _loggedBound = true;
                Plugin.LogInfo("ArcaneUI inventory: bound native InvWindow and styled " + _lastSlotCount +
                    " verified General item slots. Native ItemIcon interactions are unchanged.");
            }
        }

        private static Color Parse(string hex, Color fallback)
        {
            Color c;
            return ColorUtility.TryParseHtmlString(hex, out c) ? c : fallback;
        }
        private static RectTransform Graphic(Transform parent, string name, Color color)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
            RectTransform r = go.GetComponent<RectTransform>();
            r.SetParent(parent, false);
            Image image = go.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return r;
        }
        private static void Stretch(RectTransform r)
        {
            r.anchorMin = Vector2.zero;
            r.anchorMax = Vector2.one;
            r.pivot = new Vector2(.5f, .5f);
            r.offsetMin = Vector2.zero;
            r.offsetMax = Vector2.zero;
        }
        private static void Border(Transform parent, float thickness, Color color)
        {
            if (thickness <= 0f) return;
            Edge(parent, "Top", new Vector2(0,1), new Vector2(1,1),
                new Vector2(0,-thickness), Vector2.zero, color);
            Edge(parent, "Bottom", new Vector2(0,0), new Vector2(1,0),
                Vector2.zero, new Vector2(0,thickness), color);
            Edge(parent, "Left", new Vector2(0,0), new Vector2(0,1),
                Vector2.zero, new Vector2(thickness,0), color);
            Edge(parent, "Right", new Vector2(1,0), new Vector2(1,1),
                new Vector2(-thickness,0), Vector2.zero, color);
        }
        private static void Edge(Transform parent, string name, Vector2 a, Vector2 b,
            Vector2 minimum, Vector2 maximum, Color color)
        {
            RectTransform r = Graphic(parent, name, color);
            r.anchorMin = a;
            r.anchorMax = b;
            r.offsetMin = minimum;
            r.offsetMax = maximum;
        }
        private static Text Label(Transform parent, string name, string value, int size, Color color)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Text));
            Text t = go.GetComponent<Text>();
            t.transform.SetParent(parent, false);
            Stretch(t.rectTransform);
            t.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            t.fontSize = size;
            t.fontStyle = FontStyle.Bold;
            t.alignment = TextAnchor.MiddleLeft;
            t.color = color;
            t.raycastTarget = false;
            t.text = value;
            return t;
        }

        // Real slots found in the player's runtime log: InvWindow/General (n).
        // Cosmetic, mouse, trash and equipment slots must not be treated as bags.
        private static bool IsBagSlot(RectTransform rect)
        {
            if (rect == null || rect.parent == null || rect.parent.name != "InvWindow") return false;
            string name = rect.name;
            if (!name.StartsWith("General (", StringComparison.Ordinal) || !name.EndsWith(")", StringComparison.Ordinal))
                return false;
            int index;
            if (!int.TryParse(name.Substring(9, name.Length - 10), out index) || index < 0 || index > 300)
                return false;
            Transform icon = rect.Find("ItemIcon (2)");
            if (icon == null)
                foreach (Transform child in rect)
                    if (child.name.StartsWith("ItemIcon", StringComparison.Ordinal)) { icon = child; break; }
            return icon != null && icon.GetComponent<BoxCollider2D>() != null &&
                icon.GetComponent("ItemIcon") != null;
        }

        private int StyleNativeSlots(Color accent, Color surface)
        {
            if (_root == null || !InventorySettings.FrameSlots || InventorySettings.SlotThickness <= 0f) return 0;
            int limit = Mathf.RoundToInt(InventorySettings.MaxSlotBorders);
            int count = 0;
            foreach (RectTransform slot in _root.GetComponentsInChildren<RectTransform>(true))
            {
                if (count >= limit) break;
                if (!IsBagSlot(slot)) continue;
                // Place visual decoration first, behind ItemIcon/ItemSparkler.
                RectTransform plate = Graphic(slot, "ArcaneUI_BagSlotPlate",
                    new Color(surface.r * .63f, surface.g * .73f, surface.b * .82f,
                        InventorySettings.SlotFillOpacity / 100f));
                Stretch(plate);
                plate.SetAsFirstSibling();
                if (InventorySettings.VisualPreset != "Minimal Clean")
                    Border(plate, InventorySettings.SlotThickness, new Color(accent.r, accent.g, accent.b, .88f));
                else Border(plate, Mathf.Min(1f, InventorySettings.SlotThickness),
                    new Color(accent.r, accent.g, accent.b, .55f));
                // Inner, subtle separation of items from their slot borders.
                RectTransform inside = Graphic(plate, "ArcaneUI_BagSlotInset",
                    new Color(.03f, .08f, .11f, .4f));
                inside.anchorMin = Vector2.zero;
                inside.anchorMax = Vector2.one;
                inside.offsetMin = Vector2.one * InventorySettings.SlotInset;
                inside.offsetMax = -Vector2.one * InventorySettings.SlotInset;
                _slotDecorations.Add(plate.gameObject);
                count++;
            }
            return count;
        }

        internal void ApplyStyle()
        {
            if (_root == null) return;
            ClearDecorations();
            UiTheme theme = _themes != null ? _themes.Current : null;
            Color accent = InventorySettings.InheritTheme && theme != null ? theme.Accent :
                Parse(InventorySettings.AccentHex, new Color(.82f,.67f,.42f));
            Color surface = InventorySettings.InheritTheme && theme != null ? theme.Panel :
                Parse(InventorySettings.BackgroundHex, new Color(.10f,.15f,.21f));
            if (_nativeBackground != null)
                _nativeBackground.color = InventorySettings.TintNativeBackground
                    ? Color.Lerp(_originalColor, new Color(surface.r, surface.g, surface.b, _originalColor.a),
                        InventorySettings.NativeTintStrength / 100f)
                    : _originalColor;
            // Actual replacement surface is a first-child graphic below all
            // Erenshor slot objects; nothing above it receives ArcaneUI input.
            RectTransform background = Graphic(_root, "ArcaneUI_InventorySurface",
                new Color(surface.r, surface.g, surface.b, .99f));
            _decoration = background.gameObject;
            Stretch(background);
            background.SetAsFirstSibling();
            if (InventorySettings.DrawFrame)
                Border(background, InventorySettings.VisualPreset == "Minimal Clean" ?
                    Mathf.Min(1f, InventorySettings.BorderThickness) : InventorySettings.BorderThickness, accent);
            if (InventorySettings.VisualPreset == "Refined Fantasy")
            {
                // Decorative side rails stay behind native currency and icon controls.
                RectTransform rail = Graphic(background, "ArcaneUI_FantasySideRail",
                    new Color(accent.r, accent.g, accent.b, .48f));
                rail.anchorMin = new Vector2(1f, 0f);
                rail.anchorMax = Vector2.one;
                rail.offsetMin = new Vector2(-4f, 8f);
                rail.offsetMax = new Vector2(-2f, -8f);
            }
            RectTransform leftInset = Graphic(background, "ArcaneUI_InventoryAccent",
                new Color(accent.r, accent.g, accent.b, .72f));
            leftInset.anchorMin = new Vector2(0f, 0f);
            leftInset.anchorMax = new Vector2(0f, 1f);
            leftInset.offsetMin = Vector2.zero;
            leftInset.offsetMax = new Vector2(2f, 0f);
            // Keep currency, bags and item icons at their original coordinates.
            // The optional title is a background treatment, not a header that
            // consumes layout space or adds an interactive raycast surface.
            if (InventorySettings.ShowHeader)
            {
                float h = InventorySettings.HeaderHeight;
                RectTransform top = Graphic(background, "ArcaneUI_InventoryHeader",
                    new Color(.04f, .09f, .13f, InventorySettings.HeaderOpacity / 100f));
                top.anchorMin = new Vector2(0f, 1f);
                top.anchorMax = new Vector2(1f, 1f);
                top.pivot = new Vector2(.5f, 1f);
                top.anchoredPosition = Vector2.zero;
                top.sizeDelta = new Vector2(0f, h);
                // The native money / title labels already occupy this narrow
                // header region. Draw only the themed backing here rather than
                // laying a second INVENTORY caption across the coin counter.
            }
            _lastSlotCount = StyleNativeSlots(accent, surface);
            _status = "ArcaneUI themed inventory at " + NativePath + " | " + _lastSlotCount +
                " verified interactive General slots; native icon/drag/drop preserved.";
        }

        internal void InspectNativeHierarchy()
        {
            RectTransform root = _root != null ? _root : FindNativeInventory(true);
            if (root == null)
            {
                _status = "Cannot find a unique InvWindow under InvPar/PlayerInv.";
                Plugin.LogInfo("ArcaneUI inventory inspector: " + _status);
                return;
            }
            RectTransform[] nodes = root.GetComponentsInChildren<RectTransform>(true);
            _status = "Inventory inspector logged " + Mathf.Min(nodes.Length, 140) + " of " + nodes.Length +
                " transforms, showing the actual bag controls and dimensions.";
            Plugin.LogInfo("ArcaneUI inventory inspector: " + NativeCharacterSheetDecorator.FullPath(root) +
                " transforms=" + nodes.Length + " themed native slots=" + _lastSlotCount);
            int n = 0;
            foreach (RectTransform node in nodes)
            {
                if (node == null || IsOwn(node)) continue;
                if (n++ >= 140) break;
                var names = new StringBuilder();
                Component[] comps = node.GetComponents<Component>();
                for (int i = 0; i < comps.Length && i < 8; i++)
                {
                    if (comps[i] == null) continue;
                    if (names.Length > 0) names.Append(',');
                    names.Append(comps[i].GetType().Name);
                }
                Plugin.LogInfo("ArcaneUI inventory node " + n + ": " +
                    NativeCharacterSheetDecorator.FullPath(node) +
                    " active=" + node.gameObject.activeInHierarchy +
                    " size=" + Mathf.RoundToInt(node.rect.width) + "x" + Mathf.RoundToInt(node.rect.height) +
                    " components=" + names);
            }
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
            if (_nativeBackground != null) _nativeBackground.color = _originalColor;
            ClearDecorations();
            _root = null;
            _nativeBackground = null;
            _lastSlotCount = 0;
        }
        internal void Shutdown() { Restore(); }
    }
}
