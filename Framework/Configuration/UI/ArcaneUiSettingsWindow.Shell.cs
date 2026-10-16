using System;
using UnityEngine;
using UnityEngine.UI;
using ArcanesGroupFrames.Modules.CooldownAuras.Editor;

namespace ArcanesGroupFrames.Framework.Configuration.UI
{
    // Phase 9.3C: shared configuration shell. This is strictly presentation;
    // existing gameplay modules, setters, and preference keys remain unchanged.
    internal sealed partial class ArcaneUiSettingsWindow
    {
        private Text _contextStatusLabel;
        private Text _contextDisplayName;

        private void ClearHeaderSearch()
        {
            if (string.IsNullOrEmpty(_filter)) return;
            _filter = string.Empty;
            if (_window == null) return;
            Transform search = _window.Find("HeaderSearch");
            if (search == null) return;
            InputField input = search.GetComponent<InputField>();
            if (input != null) input.SetTextWithoutNotify(string.Empty);
        }

        private void FitWindowToCanvas()
        {
            if (_layer == null || _window == null) return;
            float availableWidth = _layer.rect.width - 40f;
            float availableHeight = _layer.rect.height - 40f;
            if (availableWidth <= 0f || availableHeight <= 0f) return;
            float desiredWidth = _previewVisible ? ExpandedWidth : Width;
            float scale = Mathf.Min(1f, availableWidth / desiredWidth, availableHeight / Height);
            _window.localScale = new Vector3(scale, scale, 1f);
        }

        private static string GetCategory(string page)
        {
            switch (page)
            {
                case "player": case "target": case "party": case "raid":
                case "manager": case "enemytracker": case "interactions": case "cast": case "fotf":
                    return "frames";
                case "action": case "xp": return "action";
                case "character": case "inventory": return "character";
                case "chat": return "chat";
                case "minimap": return "minimap";
                case "nameplates": return "nameplates";
                case "auras": return "auras";
                case "editor": return "editor";
                case "themes": return "themes";
                default: return "general";
            }
        }

        // One vertical, scrollable menu. Category headings and the relevant
        // module pages use fixed-width buttons, not a squashed horizontal grid.
        private void RenderTopNavigation()
        {
            if (_topNavigation == null) return;
            ScrollRect scroll = _topNavigation.GetComponentInParent<ScrollRect>();
            float scrollPosition = scroll != null ? scroll.verticalNormalizedPosition : 1f;
            ArcaneUiControls.ClearChildren(_topNavigation);
            string[] ids = { "global", "action", "player", "character", "chat", "minimap",
                "nameplates", "auras", "editor", "themes" };
            string[] labels = { "General", "Action Bars & XP", "Unit Frames", "Character",
                "Chat", "Minimap", "Nameplates", "Cooldowns & Auras", "Layout Editor", "Themes" };
            string currentCategory = GetCategory(_page);
            for (int i = 0; i < ids.Length; ++i)
            {
                string categoryPage = ids[i];
                string category = GetCategory(categoryPage);
                bool expanded = currentCategory == category;
                RectTransform groupRow = ArcaneUiControls.Row(_topNavigation,
                    "NavGroup_" + categoryPage, 32f, Color.clear);
                Button categoryButton = ArcaneUiControls.Button(groupRow,
                    "Category_" + categoryPage, labels[i], 0f, 0f, 173f, 30f,
                    expanded ? Theme.TabActive : Theme.ButtonFill,
                    expanded ? Color.white : Theme.Text,
                    () => SelectPage(categoryPage), 12);
                StyleSidebarCaption(categoryButton, 12);
                if (expanded)
                {
                    ArcaneUiControls.Panel(groupRow, "ActiveMarker", 0f, 2f, 3f, 26f,
                        Theme.Accent, false);
                    PageInfo[] pages = SidebarPagesForCategory(category);
                    // Single-page categories have no redundant nested links.
                    if (pages.Length <= 1) continue;
                    foreach (PageInfo page in pages)
                    {
                        PageInfo selectedPage = page;
                        bool active = _page == selectedPage.Id;
                        RectTransform childRow = ArcaneUiControls.Row(_topNavigation,
                            "NavPage_" + selectedPage.Id, 28f, Color.clear);
                        Button pageButton = ArcaneUiControls.Button(childRow,
                            "Open_" + selectedPage.Id, selectedPage.Label,
                            13f, 0f, 160f, 27f,
                            active ? Theme.TabActive : Theme.Panel,
                            active ? Color.white : Theme.Text,
                            () => SelectPage(selectedPage.Id), 11);
                        StyleSidebarCaption(pageButton, 11);
                    }
                }
            }
            ArcaneUiControls.RefreshScrollLayout(_topNavigation);
            if (scroll != null) scroll.verticalNormalizedPosition = scrollPosition;
        }

        private static void StyleSidebarCaption(Button button, int fontSize)
        {
            if (button == null) return;
            Text label = button.GetComponentInChildren<Text>();
            if (label == null) return;
            label.alignment = TextAnchor.MiddleLeft;
            label.fontSize = fontSize;
            label.resizeTextForBestFit = true;
            label.resizeTextMinSize = 10;
            label.resizeTextMaxSize = fontSize;
            // Never render one letter per line when the available width is tight.
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.verticalOverflow = VerticalWrapMode.Truncate;
        }

        private static PageInfo[] SidebarPagesForCategory(string category)
        {
            switch (category)
            {
                case "frames": return FramePages;
                case "action": return new[] { CombatPages[0], CombatPages[1] };
                case "character": return CharacterPages;
                default: return new PageInfo[0];
            }
        }

        private void RenderContextPanel()
        {
            if (_contextPanel == null) return;
            ArcaneUiControls.ClearChildren(_contextPanel);
            _contextStatusLabel = null;
            _contextDisplayName = null;
            ArcaneUiControls.Label(_contextPanel, "Title", "LIVE PREVIEW", 20f, 12f, 360f, 24f,
                13, Theme.Accent, TextAnchor.MiddleLeft, FontStyle.Bold);
            ArcaneUiControls.Button(_contextPanel, "HidePreview", "X", 458f, 11f,
                28f, 25f, Theme.ButtonFill, Theme.Text, TogglePreviewPanel, 11);

            // Move the EXISTING clickable frame/nameplate/aura renderer into
            // the right panel. This is the only preview surface in settings.
            // Its coordinate system is preserved so LinkPreview, unit-frame
            // hit targets and nameplate sample buttons keep working.
            var viewport = ArcaneUiControls.Panel(_contextPanel, "PreviewViewport",
                16f, 52f, 470f, 372f,
                ArcaneUiControls.Multiply(Theme.Background, 1.14f), false);
            viewport.gameObject.AddComponent<RectMask2D>();
            _previewHost = ArcaneUiControls.Panel(viewport, "InteractiveFramePreview", 0f, 0f,
                814f, 640f, Color.clear, false);
            _previewHost.localScale = Vector3.one * .575f;
            _previewHost.gameObject.SetActive(false);

            var summary = ArcaneUiControls.Panel(_contextPanel, "PreviewSummary", 16f, 438f,
                470f, 75f, Card, false);
            ArcaneUiControls.Label(summary, "Heading", "SELECTED MODULE", 14f, 6f,
                432f, 16f, 10, Theme.Accent, TextAnchor.MiddleLeft, FontStyle.Bold);
            _contextDisplayName = ArcaneUiControls.Label(summary, "Name", "", 14f, 24f,
                432f, 22f, 13, Theme.Text, TextAnchor.MiddleLeft, FontStyle.Bold);
            _contextStatusLabel = ArcaneUiControls.Label(summary, "Description", "", 14f, 46f,
                438f, 24f, 10, Muted, TextAnchor.UpperLeft);

            // Contextual shortcuts do not duplicate the live preview.
            ArcaneUiControls.Button(_contextPanel, "QuickGeneral", "GENERAL",
                16f, 516f, 143f, 32f, Card, Theme.Text, () => SelectPage("global"), 11);
            ArcaneUiControls.Button(_contextPanel, "QuickThemes", "THEMES",
                174f, 516f, 142f, 32f, Card, Theme.Text, () => SelectPage("themes"), 11);
            ArcaneUiControls.Button(_contextPanel, "QuickAuras", "EDIT LAYOUT",
                331f, 516f, 155f, 32f, Theme.Accent, Color.white, _openLayoutEditor, 11);
            UpdateContextPanel();
        }

        private void UpdateContextPanel()
        {
            if (_contextPanel == null || !_contextPanel.gameObject.activeInHierarchy) return;
            string label;
            string details;
            switch (_page)
            {
                case "fotf": label = "FOTF Class Resources"; details = "Click Emberweaver Heat or Flashfire in the preview to edit their appearance."; break;
                case "player": label = "Player Frame"; details = "Interactive preview: click health, mana, labels or cast elements to edit."; break;
                case "target": label = "Target Frame"; details = "Interactive target preview, including casting and status indicators."; break;
                case "party": label = "Party Member"; details = "Preview responds to member dimensions, fill, text and styling."; break;
                case "raid": label = "Raid Member"; details = "Click a frame element to jump to its editable setting."; break;
                case "manager": label = "Raid Manager"; details = "Visual style sample of the raid manager."; break;
                case "character": label = "Character Sheet"; details = "Shared InvPar root with PlayerInv inventory; native controls preserved."; break;
                case "minimap": label = "Minimap"; details = "Live native viewport shape, map visibility, border and Layout Editor controls."; break;
                case "enemytracker": label = "Enemy Tracker"; details = "Opt-in native enemy row mirror. Original Sim commands remain game-owned."; break;
                case "chat": label = "Chat"; details = "Native chat styling, typography, safe positioning and visibility."; break;
                case "inventory": label = "Inventory"; details = "Styles PlayerInv within the same native character + inventory window."; break;
                case "nameplates": label = "Overhead Nameplate"; details = "Switch hostile/friendly/SimPlayer/target samples above."; break;
                case "action": label = "Action Bars"; details = "Appearance settings change the live action-bar style sample."; break;
                case "xp": label = "XP / Ascension Bar"; details = "Customize normal XP and persistent max-level Ascension XP progress."; break;
                case "auras":
                    var aura = CustomAuraLibrary.Selected;
                    label = aura != null ? aura.Name : "Cooldowns & Auras";
                    details = aura != null ? aura.Display + " • " + aura.Trigger : "Create an aura in the manager to preview it.";
                    break;
                default:
                    label = GetPageName(_page);
                    details = "Preview content is available for frames, nameplates and auras.";
                    break;
            }
            if (_contextDisplayName != null) _contextDisplayName.text = label;
            if (_contextStatusLabel != null) _contextStatusLabel.text = details;
        }
    }
}
