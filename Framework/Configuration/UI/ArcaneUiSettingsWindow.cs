using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using ArcanesGroupFrames.Framework.Canvas;
using ArcanesGroupFrames.Framework.Modules;
using ArcanesGroupFrames.Framework.Theming;
using ArcanesGroupFrames.Framework.Theming.Rendering;
using ArcanesGroupFrames.Framework.Configuration;
using ArcanesGroupFrames.Modules.UnitFrames.Player;
using ArcanesGroupFrames.Modules.UnitFrames.Target;
using ArcanesGroupFrames.Framework.NativeUI.Target;
using ArcanesGroupFrames.Framework.NativeUI.Player;
using ArcanesGroupFrames.Framework.NativeUI;
using ArcanesGroupFrames.Modules.Nameplates.Configuration;
using ArcanesGroupFrames.Modules.Nameplates.Native;
using ArcanesGroupFrames.Modules.Nameplates.UI;
using ArcanesGroupFrames.Modules.CooldownAuras.Configuration;
using ArcanesGroupFrames.Modules.Experience.Configuration;
using ArcanesGroupFrames.Modules.Experience.UI;
using ArcanesGroupFrames.Modules.Minimap.Configuration;
using ArcanesGroupFrames.Modules.EnemyTracker.Configuration;
using System.Collections.Generic;

namespace ArcanesGroupFrames.Framework.Configuration.UI
{
    // Phase 3: settings shell in the spirit of EllesmereUI, but fully original
    // geometry and controls. Uses *existing* frame setters and never replaces
    // their native gameplay interactions.
    internal sealed partial class ArcaneUiSettingsWindow
    {
        private const float Width = 1060f;
        private const float Height = 660f;
        // The normal editor remains compact. Opening preview EXPANDS the window
        // to the right of its main 1060px workspace instead of covering settings.
        private const float PreviewPanelWidth = 504f;
        private const float PreviewPanelGap = 12f;
        private const float ExpandedWidth = Width + PreviewPanelWidth + PreviewPanelGap + 8f;
        // Phase 9.3D: the sidebar is replaced by category/module navigation rows.
        // Keep the established settings-row width so existing native controls fit.
        private const float BodyX = 220f;
        private const float BodyWidth = 814f;

        private readonly ArcaneCanvasManager _canvas;
        private readonly ThemeManager _themes;
        private readonly UiModuleRegistry _modules;
        private readonly Action _openLayoutEditor;
        private RectTransform _layer;
        private RectTransform _window;
        private RectTransform _searchResults;
        private RectTransform _topNavigation;
        private RectTransform _contextPanel;
        private RectTransform _tabs;
        private RectTransform _content;
        private RectTransform _settingsScrollRoot;
        private ArcaneUiColorPickerDialog _activeColorPicker;
        private Text _pageTitle;
        private Text _pageSubheading;
        private RectTransform _previewHost;
        private RectTransform _previewFrame;
        private RectTransform _previewHealth;
        private RectTransform _previewMana;
        private Text _previewName;
        private Text _previewPercent;
        private Text _previewRawHealth;
        private Text _previewDetails;
        private Text _previewStyle;
        // Phase 8.3: interactive, in-window nameplate sample controls live in the
        // partial NameplatePreview source file. No NPC/Sim objects are spawned.
        private int _nameplatePreviewCategory;
        // Preview and search navigation share a single jump-to-setting path.
        private ArcaneUiUnitFrameView _playerPreview;
        private ArcaneUiFOTFResources _fotfPreview;
        private float _nextFOTFPreviewSync;
        private ArcaneUiUnitFrameView _targetPreview;
        private RectTransform _managerPreview;
        private ArcaneUiPreviewHoverHighlight _activePreviewHighlight;
        private Text _previewHoverHint;
        private string _previewHintDefault;
        private readonly Dictionary<string, RectTransform> _settingRows =
            new Dictionary<string, RectTransform>(StringComparer.OrdinalIgnoreCase);
        private string _pendingHighlight;
        private string _auraLibraryFilter = "";
        private string _auraWorkspaceTab = "Display";
        private static readonly List<SettingLocation> SettingIndex = MakeSettingIndex();
        private bool _pendingFrameRefresh;
        private string _pendingFramePage;
        private float _lastFrameRefreshAt;
        private string _page;
        private string _tab = "General";
        private string _filter = "";
        private bool _subscribed;
        private List<string> _hudCandidates = new List<string>();
        private int _hudCandidateIndex;
        private bool _warnedNoEventSystem;
        private RectTransform _bindingPicker;
        private bool _capturingRaidManagerHotkey;
        private bool _capturingActionBinding;
        private int _actionBindingBar;
        private int _actionBindingSlot;
        private string _actionBindingFeedback = "";
        private string _pendingActionShortcut = "";
        private int _pendingConflictBar = -1, _pendingConflictSlot = -1;
        private int _actionLayoutSlot = 1;
        private string _actionLayoutFeedback = "";
        private bool _confirmActionLayoutLoad;
        private bool _confirmActionLayoutSave;
        private int _actionSourceBar = 1;
        private int _actionSourceSlot = 1;
        private int _nativeSourceSlot = 1;
        private bool _confirmPrimaryHud;
        // Compact workspace: the interactive live preview is available on demand,
        // instead of permanently consuming nearly half the settings window.
        private bool _previewVisible;

        private int _suppressRaidManagerHotkeyUntilFrame = -1;

        // Prevent the captured hotkey from immediately toggling the raid
        // manager in the same frame as its assignment.
        internal bool IsCapturingRaidManagerHotkey
        {
            get { return _capturingRaidManagerHotkey ||
                Time.frameCount <= _suppressRaidManagerHotkeyUntilFrame; }
        }

        private struct PageInfo
        {
            public readonly string Id;
            public readonly string Label;
            public readonly bool Implemented;
            public PageInfo(string id, string label, bool implemented)
            {
                Id = id; Label = label; Implemented = implemented;
            }
        }

        private static readonly PageInfo[] GeneralPages =
        {
            new PageInfo("global", "General", true),
            new PageInfo("editor", "Unlock UI / Layout", true),
            new PageInfo("themes", "Themes", true)
        };

        private static readonly PageInfo[] FramePages =
        {
            new PageInfo("player", "Player Frame", true),
            new PageInfo("target", "Target Frame", true),
            new PageInfo("party", "Party Frames", true),
            new PageInfo("raid", "Raid Frames", true),
            new PageInfo("manager", "Raid Manager", true),
            new PageInfo("enemytracker", "Enemy Tracker", true),
            new PageInfo("interactions", "Click Casting", true),
            new PageInfo("cast", "Cast Bars", true),
            new PageInfo("fotf", "FOTF Resources", true)
        };

        private static readonly PageInfo[] CombatPages =
        {
            new PageInfo("action", "Action Bars", true),
            new PageInfo("xp", "XP Bar", true),
            new PageInfo("nameplates", "Nameplates", true),
            new PageInfo("auras", "Cooldowns & Auras", true)
        };

        private static readonly PageInfo[] CharacterPages =
        {
            new PageInfo("character", "Character Sheet", true),
            new PageInfo("inventory", "Inventory", true)
        };

        private static readonly PageInfo[] FuturePages =
        {
            new PageInfo("chat", "Chat", true),
            new PageInfo("minimap", "Minimap", true)
        };

        private struct SettingLocation
        {
            internal readonly string Page, Tab, Label;
            internal SettingLocation(string page, string tab, string label)
            { Page = page; Tab = tab; Label = label; }
        }
        private static void Index(List<SettingLocation> entries, string page, string tab, string labels)
        {
            foreach (string name in labels.Split('|'))
                if (!string.IsNullOrEmpty(name)) entries.Add(new SettingLocation(page, tab, name));
        }
        private static List<SettingLocation> MakeSettingIndex()
        {
            var result = new List<SettingLocation>();
            Index(result, "character", "General", "Enable ArcaneUI Character Sheet|Bind Character Window");
            Index(result, "character", "Appearance", "Enable Themed Character Sheet|Character Layout|Character Theme|Character Stat Font Size|Show Character Frame Border|Character Border Thickness|Show Skill Web Labels");
            Index(result, "character", "Equipment", "Highlight Character Equipment Slots|Character Slot Border Thickness");
            Index(result, "character", "Native", "Inspect Native Character Values|Scan Native Character Windows|Use Known Character Root|Next Character Candidate|Use This Character Window|Restore Native Character Sheet");
            Index(result, "xp", "General", "Enable XP Bar|Show At Maximum Level|Show XP Label|Show XP Percentage|Show XP Numbers");
            Index(result, "xp", "Appearance", "XP Bar Width|XP Bar Height|XP Bar Font Size|Normal XP Colour|Ascension XP Colour|XP Background Colour");
            Index(result, "xp", "Position", "XP Bar X Position|XP Bar Y Position");
            Index(result, "inventory", "General", "Enable ArcaneUI Inventory|Inspect Native Inventory");
            Index(result, "minimap", "General", "Use Native Minimap|Enable Minimap Skin|Edit Minimap Layout");
            Index(result, "minimap", "Appearance", "Minimap Frame Style|Show Minimap Border|Inherit Minimap Theme|Minimap Border Colour|Minimap Border Thickness|Minimap Border Opacity|Minimap Border Inset");
            Index(result, "minimap", "Layout", "Move Native Minimap|Minimap X Offset|Minimap Y Offset|Minimap Scale|Reset Minimap Layout");
            Index(result, "chat", "General", "Enable Themed Native Chat");
            Index(result, "chat", "Appearance", "Chat Theme|Inherit Global Chat Theme|Chat Accent Colour|Chat Background Colour|Show Chat Border|Chat Border Thickness|Tint Native Chat Background|Chat Background Opacity");
            Index(result, "chat", "Text", "Customize Chat Text|Chat Font Size|Chat Line Spacing|Transcript Font Family|Transcript Font Style|Chat Text Shadow|Chat Text Outline|Chat Text Effect Strength");
            Index(result, "chat", "Appearance", "Chat Border Style|Chat Border Opacity|Chat Horizontal Padding|Chat Vertical Padding|Chat Backdrop Style|Chat Gradient Second Colour|Use Custom Chat Border Colour|Custom Chat Border Colour");
            Index(result, "chat", "Layout", "Allow Moving Native Chat|Chat Position X|Chat Position Y|Allow Resizing Native Chat|Native Chat Width|Native Chat Height|Unlock Chat Drag Handles|Reset Chat Position and Size|Save Chat Preset 1|Load Chat Preset 1");
            Index(result, "chat", "Visibility", "Chat Skin Visibility|Unhovered Skin Opacity");
            Index(result, "inventory", "Appearance", "Use Global Inventory Theme|Inventory Accent Colour|Inventory Background Colour|Show Inventory Frame Border|Inventory Border Thickness|Show Inventory Title Header|Inventory Header Height|Inventory Header Opacity|Tint Native Inventory Background|Inventory Tint Strength");
            Index(result, "inventory", "Slots", "Highlight Native Inventory Slots|Inventory Slot Border Thickness|Maximum Slot Borders");
            Index(result, "inventory", "Native", "Inspect Native Inventory Hierarchy");
            Index(result, "global", "General", "Prefer ArcaneUI Replacements|Abbreviate Numbers|Player frame|Party frames|Raid frames|Enemy Tracker|Action bars|Buffs, Debuffs & Cooldowns|Global typography|Raid Manager|Unlock UI|Current theme|Show Lightbringer Absorb|Absorb Overlay Colour|Absorb Overlay Opacity|Absorb Minimum Visible Width");
            Index(result, "editor", "General", "Unlock UI");
            Index(result, "themes", "General", "Apply Custom Palette|Health Bar|Missing Health|Mana Bar|Frame Background|Panel Surface|Border|Accent|Frame Border|Panel Opacity|Health Bar Texture|Global Font Family|Global Font Size|Global Font Style|Global Font Outlines|Party Frames|Raid Frames|Raid Manager");
            Index(result, "player", "General", "Enable Player Frame|Show Mana Bar|Show Health Numbers|Show HP Percent|Show Mana Percent|Show Level|Show Class|Only Show In Combat|Layout editor");
            Index(result, "player", "Appearance", "Use Global Theme|Class-Coloured Health|Frame Width|Frame Height|Mana Bar Height|Bar Texture|Reset Texture Override|Font|Reset Font Override|Player Health Colour|Player Mana Colour|Player Background");
            Index(result, "player", "General", "Hide Original Player Frame");
            Index(result, "fotf", "General", "Show FOTF Resources|Show Resource Numbers|Preview FOTF class|FOTF Layout Editor");
            foreach (string part in new[] { "Primary", "Secondary" })
                Index(result, "fotf", part, part + " Visible|" + part + " Detached|" + part + " Width|" + part + " Height|" + part + " Cell Gap|" + part + " X Offset|" + part + " Y Gap|" + part + " Fill Colour|" + part + " Empty Colour|" + part + " Background Colour|" + part + " Border Colour|" + part + " Fill Opacity|" + part + " Background Opacity|" + part + " Border Thickness|" + part + " Bar Texture|" + part + " Fill Direction|" + part + " Text Mode|" + part + " Font|" + part + " Font Size|" + part + " Bold Text|" + part + " Text Outline|" + part + " Text Colour|" + part + " Text Alignment|" + part + " Label X|" + part + " Label Y|" + part + " Reset Style");
            Index(result, "cast", "General", "Hide Native Cast Bar|Player Cast Bar Settings|Target Cast Bar Settings");
            Index(result, "nameplates", "General", "Enable ArcaneUI Nameplates|Show NPC Nameplates|Show SimPlayer Nameplates|Show Friendly NPC Nameplates|Nameplate View Distance");
            Index(result, "auras", "General", "Enable Cooldown & Aura Tracker|Show Player Buffs|Show Player Debuffs|Show Target Buffs|Show Target Debuffs|Hide Native Player Buffs|Hide Native Player Debuffs|Hide Native Target Buffs|Hide Native Target Debuffs|Show Ability Cooldowns|Minimum Cooldown Shown");
            Index(result, "auras", "Appearance", "Selected Effect Frame|Icon Size|Icon Spacing|Maximum Icons|Icons Per Line|Growth Direction|Effect Sorting|Frame Style|Accent Colour|Frame Opacity|Show Frame Label|Show Frame Timers|Show Frame Stacks");
            Index(result, "auras", "Position", "Selected Effect Frame|Frame X Position|Frame Y Position|Open Layout Editor");
            Index(result, "auras", "Aura Manager", "Create New Aura|Duplicate Selected Aura|Delete Selected Aura|Aura Search|Display Name|Enable This Aura|Display Type|Aura Accent Colour|Display Remaining Time|Aura Width|Aura Height|Aura X Position|Aura Y Position|Unlock Custom Aura Dragging|Trigger Type|Match Name");
            Index(result, "nameplates", "Appearance", "Nameplate Width|Nameplate Health Height|Nameplate Vertical Offset|Health Fill Direction|Background Opacity|Hostile NPC Health Colour|Friendly NPC Health Colour|Target Highlight Colour|Target Scale");
            Index(result, "nameplates", "Text", "Show NPC Role Tags|NPC Tag Font Size|NPC Tag Colour");
            Index(result, "nameplates", "Text", "Show Character Names|Name Font Size|Name Character Limit|Show Health Percentage|Health Percentage Font Size|Name Text Colour");
            Index(result, "nameplates", "General", "Hide Original Nameplates");
            Index(result, "action", "General", "Action Bar Preset|Enable ArcaneUI Action Bars|Hide Original Erenshor Hotbar");
            Index(result, "action", "Themes", "Action Bar Preset|Action Bar Accent Colour|Reset Action Bar Accent|Action Bar Background Opacity");
            Index(result, "action", "Borders", "Action Bar Border Style|Action Bar Border Thickness|Action Bar Corner Size|Action Bar Glow Intensity");
            Index(result, "action", "Icons & Text", "Action Bar Icon Size|Action Bar Icon Spacing|Show Action Bar Hotkeys|Show Action Bar Cooldown Numbers|Show Action Tooltips");
            Index(result, "action", "Bars", "Select Action Bar|Create Additional Action Bar|Remove Selected Action Bar|Action Bar Slots|Action Bar Columns|Inherit Action Bar Style|Selected Bar Theme|Selected Bar Border|Selected Bar Accent Colour|Reset Selected Bar Accent|Selected Bar Glow|Selected Bar Visibility|Selected Bar Fade Duration|Selected Bar Hidden Opacity");
            Index(result, "action", "Keybinds", "Select Keybind Bar|Select Action Slot|Assigned Action|Capture Slot Keybind|Clear Slot Keybind");
            Index(result, "action", "Layouts", "Save Action Bar Layout|Load Action Bar Layout|Character Layout Slot");
            Index(result, "player", "Casting", "Show Player Cast Bar|Player Cast Bar Placement|Player Cast Bar Colour|Reset Player Cast Bar Colour|Player Cast Bar X Position|Player Cast Bar Y Position|Hide Native Cast Bar");
            Index(result, "player", "Text", "Cast Name Size|Cast Time Size");
            Index(result, "target", "General", "Enable Target Frame|Show Mana Bar|Show Health Numbers|Show HP Percent|Show Mana Percent|Show Level|Show Class|Layout editor");
            Index(result, "target", "Appearance", "Use Global Theme|Automatic Target Colours|Frame Width|Frame Height|Mana Bar Height|Bar Texture|Reset Texture Override|Font|Reset Font Override|Target Health Colour|Target Mana Colour|Target Background");
            Index(result, "target", "General", "Hide Original Target Frame");
            Index(result, "target", "Effects", "Show Target Buffs|Show Target Debuffs|Aura Icon Size|Aura Spacing|Aura Placement");
            Index(result, "target", "Casting", "Show Cast Bar|Cast Bar Placement|Cast Bar Colour|Reset Cast Bar Colour|Cast Bar X Position|Cast Bar Y Position|Cast Name Size|Cast Time Size");
            Index(result, "target", "General", "Only Show In Combat|Hide When No Target|Name Character Limit");
            Index(result, "party", "General", "Enable Party Frames|Unlock Frames|Show HP Percent|Show Raw Health|Class-Coloured Health|Show Mana Bars|Mouseover Casting|Target Highlight|Aggro Highlight|Status Icons|Reset Party Defaults");
            Index(result, "raid", "General", "Enable Raid Frames|Unlock Frames|Show HP Percent|Show Raw Health|Class-Coloured Health|Show Mana Bars|Mouseover Casting|Target Highlight|Aggro Highlight|Status Icons|Reset Raid Defaults");
            foreach (string page in new [] { "party", "raid" })
            {
                Index(result, page, "Appearance", "Use Global Theme|Frame Width|Frame Height|Mana Bar Height|Member Spacing|Target Highlight Thickness|Aggro Highlight Thickness|Health Fill Direction|Mana Fill Direction|Bar Texture|Class Health Colours|Static Health Colour|Missing Health Colour|Font|Name Font Size|HP Percent Font Size|Raw HP Font Size");
                if (page == "raid") Index(result, page, "Appearance", "Group Spacing");
                Index(result, page, "Click Casting", "Reset Click Casting|Healing Spell Diagnostics");
            }
            foreach (string page in new [] { "player", "target", "party", "raid" })
            {
                Index(result, page, "Advanced", "Frame Opacity|Health Fill Opacity|Mana Fill Opacity|Text Opacity|Show Frame Border|Frame Border Thickness|Frame Border Colour|Frame Text Colour|Show Unit Name|Show Health Text|Reset Advanced Frame Styling|Status Icon Opacity|Maximum Status Icons|Status Sort Order");
                if (page == "player" || page == "target")
                    Index(result, page, "Advanced", "Show Mana Text|Badge Content|Badge Placement|Badge Size|Badge X Offset|Badge Y Offset|Badge Background Colour");
                Index(result, page, "Text", "Name Character Limit");
                if (page == "player" || page == "target")
                    Index(result, page, "Appearance", "Health Fill Direction|Mana Fill Direction");
                string tags = page == "player" || page == "target" ? "Name|Level|Class|Raw Health|HP Percentage|Mana Numbers|Mana Percentage"
                                                : "Name|HP Percentage|Raw Health";
                foreach (string tag in tags.Split('|'))
                    Index(result, page, "Text", tag + " Size|" + tag + " Border|" + tag + " Alignment|" + tag + " X Position|" + tag + " Y Position");
            }
            Index(result, "enemytracker", "General", "Enable ArcaneUI Enemy Tracker|Highlight Active Group Buttons|Hide Original Enemy Tracker|Inspect Original Enemy Tracker");
            Index(result, "enemytracker", "Appearance", "Tracker Width|Enemy Row Height|Enemy Row Spacing|Maximum Rows|Health Bar Height|Health Bar Texture|Border Style|Border Width|Background Opacity|Show Tracker Heading|Reset Tracker Style");
            Index(result, "enemytracker", "Text", "Enemy Font|Enemy Name Font Size|Command Font Size|Name Alignment|Health Number Display|Bold Enemy Names|Enemy Tracker Text Shadows");
            Index(result, "enemytracker", "Colours", "Tracker Background Colour|Normal Health Colour|Low Health Colour|Selected Target Colour|Tracker Border Colour|Enemy Text Colour|Inactive Command Colour|Active Command Colour");
            Index(result, "enemytracker", "Visibility", "Tracker Visibility Mode|Show Tracker When Empty");
            Index(result, "enemytracker", "Layout", "Horizontal Offset|Vertical Offset|Edit Tracker Position");
            Index(result, "enemytracker", "Native", "Inspect Native Enemy Tracker");
            Index(result, "manager", "General", "Enable Raid Manager|Use Global Theme|Raid Manager Hotkey");
            Index(result, "interactions", "General", "Party Frame Click Casting|Raid Frame Click Casting");
            Index(result, "unit", "General", "Scan Native Target UI|Previous Target Candidate|Next Target Candidate|Log Target Candidates");
            return result;
        }

        private UiTheme Theme { get { return _themes.Current; } }
        private Color Muted { get { return new Color(Theme.Text.r, Theme.Text.g, Theme.Text.b, .62f); } }
        private Color Card { get { return ArcaneUiControls.Multiply(Theme.WidgetPanel.a > 0f ? Theme.WidgetPanel : Theme.Panel, 1.04f); } }
        private bool IsOpen { get { return _window != null && _window.gameObject.activeSelf; } }
        private Color WidgetButton(bool active = false) { return active ? Theme.ButtonActiveFill : Theme.ButtonFill; }
        private Color WidgetText { get { return Theme.ButtonText.a > 0f ? Theme.ButtonText : Theme.Text; } }
        private Color WidgetToggle(bool enabled) { return enabled ? Theme.ToggleOn : Theme.ToggleOff; }

        internal ArcaneUiSettingsWindow(ArcaneCanvasManager canvas, ThemeManager themes,
            UiModuleRegistry modules, Action openLayoutEditor)
        {
            _canvas = canvas;
            _themes = themes;
            _modules = modules;
            _openLayoutEditor = openLayoutEditor;
            // Older installs may have left the now-removed Profiles tab selected.
            // Redirect it without changing any other saved preference.
            _page = ArcaneUiPreferences.LastPage == "profiles" ||
                ArcaneUiPreferences.LastPage == "unit" ? "global" : ArcaneUiPreferences.LastPage;
        }

        internal void Initialize()
        {
            if (_subscribed) return;
            _themes.SetTheme(ArcaneUiPreferences.ThemeId);
            _themes.ThemeChanged += OnThemeChanged;
            ArcaneUiStyleService.Changed += OnStyleChanged;
            _subscribed = true;
        }

        internal void Update()
        {
            if (!IsOpen) return;
            FitWindowToCanvas();
            if (_page == "auras" && IsAuraEditorTab(_tab)) AnimateCustomAuraPreview();
            // The preview uses live, read-only FOTF counts when the selected
            // preview class matches the player. Repaint at a modest cadence so
            // gameplay changes (such as Bone Shards from Reclaim) are visible
            // without recreating the preview, controls or click highlights.
            if (_fotfPreview != null && (_page == "fotf" || _page == "player") &&
                Time.unscaledTime >= _nextFOTFPreviewSync)
            {
                _nextFOTFPreviewSync = Time.unscaledTime + .10f;
                UpdateFOTFResourcePreview();
            }
            // Coalesce expensive native frame rebuilds during slider drags.
            // The in-window preview updates on every movement.
            if (_pendingFrameRefresh && Time.unscaledTime - _lastFrameRefreshAt >= .075f)
                FlushFrameRefresh();
            if (_capturingRaidManagerHotkey)
            {
                if (!Input.anyKeyDown) return;
                if (Input.GetKeyDown(KeyCode.Escape))
                {
                    RaidFramesSettings.SetRaidManagerToggleKey(KeyCode.None);
                    FinishHotkeyCapture();
                    return;
                }
                foreach (KeyCode key in Enum.GetValues(typeof(KeyCode)))
                {
                    if (key == KeyCode.None || (key >= KeyCode.Mouse0 && key <= KeyCode.Mouse6))
                        continue;
                    if (!Input.GetKeyDown(key)) continue;
                    RaidFramesSettings.SetRaidManagerToggleKey(key);
                    FinishHotkeyCapture();
                    return;
                }
                return;
            }
            if (_capturingActionBinding)
            {
                if (!Input.anyKeyDown) return;
                if (Input.GetKeyDown(KeyCode.Escape))
                {
                    _capturingActionBinding = false;
                    _actionBindingFeedback = "Key capture cancelled. Existing binding unchanged.";
                    RenderPage();
                    return;
                }
                foreach (KeyCode key in Enum.GetValues(typeof(KeyCode)))
                {
                    if (key == KeyCode.None || key >= KeyCode.Mouse0 && key <= KeyCode.Mouse6 ||
                        ArcanesGroupFrames.Modules.ActionBars.Configuration.ActionBarPlanSettings.IsModifier(key))
                        continue;
                    if (!Input.GetKeyDown(key)) continue;
                    bool ctrl = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
                    bool alt = Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);
                    if (alt)
                    {
                        _capturingActionBinding = false;
                        _actionBindingFeedback = "Alt is disabled for in-game actions. Choose Ctrl or Shift.";
                        RenderPage();
                        return;
                    }
                    bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
                    string shortcut = ArcanesGroupFrames.Modules.ActionBars.Configuration.ActionBarPlanSettings.Describe(key, ctrl, alt, shift);
                    string error;
                    _pendingActionShortcut = "";
                    int conflictingBar, conflictingSlot;
                    if (ArcanesGroupFrames.Modules.ActionBars.Configuration.ActionBarPlanSettings.TryGetCustomConflict(
                        _actionBindingBar, _actionBindingSlot, shortcut, out conflictingBar, out conflictingSlot))
                    {
                        _pendingActionShortcut = shortcut;
                        _pendingConflictBar = conflictingBar;
                        _pendingConflictSlot = conflictingSlot;
                        _actionBindingFeedback = shortcut + " is used by " +
                            ArcanesGroupFrames.Modules.ActionBars.Configuration.ActionBarPlanSettings.Name(conflictingBar) +
                            ", slot " + conflictingSlot + ". Confirm replacement or cancel.";
                        _capturingActionBinding = false;
                        RenderPage();
                        return;
                    }
                    if (ArcanesGroupFrames.Modules.ActionBars.Configuration.ActionBarPlanSettings.TrySetBinding(
                        _actionBindingBar, _actionBindingSlot, shortcut, out error))
                    {
                        _actionBindingFeedback = "ArcaneUI shortcut saved: " + shortcut +
                            ". Native shortcuts unchanged.";
                        _capturingActionBinding = false;
                    }
                    else
                    {
                        _actionBindingFeedback = error;
                        _capturingActionBinding = false;
                    }
                    RenderPage();
                    return;
                }
                return;
            }
            if (_activeColorPicker != null)
            {
                if (Input.GetKeyDown(KeyCode.Escape)) _activeColorPicker.Cancel();
                return;
            }
            if (_bindingPicker != null && _bindingPicker.gameObject.activeSelf)
            {
                if (Input.GetKeyDown(KeyCode.Escape)) _bindingPicker.gameObject.SetActive(false);
                return;
            }
            if (_choicePopup != null) { UpdateChoicePopupDismissal(); return; }
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (_previewVisible) TogglePreviewPanel();
                else Close();
            }
        }

        internal void Open(string page = null)
        {
            if (!string.IsNullOrEmpty(page))
            {
                _page = page == "profiles" || page == "unit" ? "global" : page;
                _tab = "General";
            }
            if (!_canvas.IsReady) return;
            if (_window == null) BuildWindow();
            if (_window == null) return;
            _layer.gameObject.SetActive(true);
            _window.gameObject.SetActive(true);
            RenderNavigation();
            RenderPage();
            _window.SetAsLastSibling();
            _layer.SetAsLastSibling();
            ArcaneUiModalInputGuard.SetSettingsOpen(true);

            if (!_warnedNoEventSystem && EventSystem.current == null)
            {
                _warnedNoEventSystem = true;
                Plugin.LogWarning("ArcaneUI settings opened without an EventSystem. Game menus may not yet be initialized; controls require the native EventSystem.");
            }
        }

        internal void Toggle()
        {
            if (IsOpen) Close();
            else Open();
        }

        internal void Close()
        {
            CloseChoicePopup();
            // A dismissed dialog must not leave uncommitted colours in PlayerPrefs.
            if (_activeColorPicker != null) _activeColorPicker.Cancel();
            ClearPreviewHover();
            if (_pendingFrameRefresh) FlushFrameRefresh();
            PlayerPrefs.Save();
            _capturingRaidManagerHotkey = false;
            _capturingActionBinding = false;
            _pendingActionShortcut = "";
            if (_bindingPicker != null) _bindingPicker.gameObject.SetActive(false);
            if (_window != null) _window.gameObject.SetActive(false);
            // Also disable the full-screen input blocker when the menu closes.
            if (_layer != null) _layer.gameObject.SetActive(false);
            ArcaneUiModalInputGuard.SetSettingsOpen(false);
        }

        private void FinishHotkeyCapture()
        {
            _capturingRaidManagerHotkey = false;
            _suppressRaidManagerHotkeyUntilFrame = Time.frameCount + 1;
            RenderPage();
        }

        internal void Shutdown()
        {
            CloseChoicePopup();
            ArcaneUiModalInputGuard.SetSettingsOpen(false);
            if (_activeColorPicker != null) _activeColorPicker.Cancel();
            ClearPreviewHover();
            if (_subscribed)
            {
                _themes.ThemeChanged -= OnThemeChanged;
                ArcaneUiStyleService.Changed -= OnStyleChanged;
            }
            _subscribed = false;
            if (_layer != null) UnityEngine.Object.Destroy(_layer.gameObject);
            _layer = null;
            _window = null;
            _topNavigation = null;
            _contextPanel = null;
            _tabs = null;
            _content = null;
            _settingsScrollRoot = null;
            _previewHost = null;
            _enemyTrackerPreview = null;
            _actionPreviewHost = null;
            _previewFrame = null;
            _playerPreview = null;
            _fotfPreview = null;
            _targetPreview = null;
            _managerPreview = null;
            _settingRows.Clear();
            _previewHealth = null;
            _previewMana = null;
            _playerPreview = null;
            _fotfPreview = null;
            _targetPreview = null;
            _managerPreview = null;
            _settingRows.Clear();
            _bindingPicker = null;
            _capturingRaidManagerHotkey = false;
            _capturingActionBinding = false;
            _pendingFrameRefresh = false;
            _pendingFramePage = null;
        }

        private void OnStyleChanged()
        {
            // Preview and the visible config values use the same effective
            // style resolver as the live frame. No frame recreation here.
            // Never rebuild the page during a slider drag: destroying its
            // active Slider causes Unity to lose the mouse capture.
            if (IsOpen && (IsFramePage || _page == "player" || _page == "target" || _page == "manager")) UpdatePreview();
        }

        private void OnThemeChanged(UiTheme theme)
        {
            CloseChoicePopup();
            if (_activeColorPicker != null) _activeColorPicker.Cancel();
            if (_window == null) return;
            Vector2 lastPosition = _window.anchoredPosition;
            bool wasOpen = IsOpen;
            _window.gameObject.SetActive(false);
            UnityEngine.Object.Destroy(_window.gameObject);
            _window = null;
            _bindingPicker = null;
            BuildWindow();
            if (_window != null)
            {
                _window.anchoredPosition = lastPosition;
                _window.gameObject.SetActive(wasOpen);
                if (wasOpen) { RenderNavigation(); RenderPage(); }
            }
        }

        private void ScheduleFrameRefresh(bool immediate = false)
        {
            if (!IsFramePage) return;
            if (_pendingFrameRefresh && _pendingFramePage != _page) FlushFrameRefresh();
            _pendingFramePage = _page;
            _pendingFrameRefresh = true;
            if (immediate || Time.unscaledTime - _lastFrameRefreshAt >= .075f)
                FlushFrameRefresh();
        }

        private void FlushFrameRefresh()
        {
            _pendingFrameRefresh = false;
            string framePage = _pendingFramePage;
            _pendingFramePage = null;
            _lastFrameRefreshAt = Time.unscaledTime;
            if (framePage == "party") PartyFramesUI.SettingsChanged();
            else if (framePage == "raid") RaidFramesUI.SettingsChanged();
        }

        private void BuildWindow()
        {
            if (_layer == null)
            {
                _layer = _canvas.CreateLayer("ArcaneUI_Configuration");
                if (_layer == null) return;
                // Isolated nested Canvas: renders above Erenshor's normal
                // windows and the existing Group Frames/Raid Manager canvases.
                var modalCanvas = _layer.gameObject.AddComponent<UnityEngine.Canvas>();
                modalCanvas.overrideSorting = true;
                // Unity's maximum supported canvas sorting order. Existing ArcaneUI
                // tooltips use 32762; this modal must draw above them.
                modalCanvas.sortingOrder = 32767;
                _layer.gameObject.AddComponent<GraphicRaycaster>();

                // Block clicks from passing into gameplay and dim other UI
                // while configuration is open. Stretch across any screen size.
                var backdrop = ArcaneUiControls.Panel(_layer, "ArcaneUI_ModalBackdrop",
                    0, 0, 0, 0, new Color(0f, 0f, 0f, 0.58f));
                backdrop.anchorMin = Vector2.zero;
                backdrop.anchorMax = Vector2.one;
                backdrop.offsetMin = Vector2.zero;
                backdrop.offsetMax = Vector2.zero;
            }
            if (_layer == null) return;
            // Themes can specify translucent backgrounds for HUD elements,
            // but a configuration dialog itself must be fully opaque.
            Color opaqueBackground = new Color(Theme.Background.r, Theme.Background.g,
                Theme.Background.b, 1f);
            _window = ArcaneUiControls.Panel(_layer, "ArcaneUI_Settings", 0, 0,
                Width, Height, opaqueBackground);
            _window.anchorMin = new Vector2(.5f, .5f);
            _window.anchorMax = new Vector2(.5f, .5f);
            _window.pivot = new Vector2(.5f, .5f);
            _window.anchoredPosition = Vector2.zero;

            // Phase 11.6N: compact, single-workspace configuration inspired by
            // proven MMO add-on ergonomics. No profile system or decorative art.
            ArcaneUiControls.Panel(_window, "Header", 0, 0, Width, 62f, Theme.HeaderPanel, false);
            ArcaneUiControls.Panel(_window, "HeaderAccent", 0, 60f, Width, 2f, Theme.HeaderAccent, false);
            ArcaneUiControls.Label(_window, "Logo", "ARCANE", 17f, 13f, 103f, 32f, 21,
                Theme.Text, TextAnchor.MiddleLeft, FontStyle.Bold);
            ArcaneUiControls.Label(_window, "UiSuffix", "UI", 120f, 13f, 38f, 32f, 21,
                Theme.Accent, TextAnchor.MiddleLeft, FontStyle.Bold);
            var dragZone = ArcaneUiControls.Panel(_window, "MoveSettingsWindow", 0, 0,
                190f, 60f, Color.clear);
            var handle = dragZone.gameObject.AddComponent<UiWindowDragHandle>();
            handle.Window = _window;
            handle.Canvas = _canvas.Canvas;
            var headerSearch = ArcaneUiControls.Search(_window, "HeaderSearch", 703f, 14f,
                192f, 32f, "Search settings", Theme.InputBackground, Theme.InputText, value =>
                {
                    _filter = value ?? "";
                    RenderNavigation();
                });
            headerSearch.SetTextWithoutNotify(_filter);
            ArcaneUiControls.Button(_window, "PreviewToggle", "PREVIEW", 904f, 14f, 90f, 32f,
                Theme.ButtonFill, WidgetText, TogglePreviewPanel, 11);
            ArcaneUiControls.Button(_window, "Close", "X", 1010f, 14f, 32f, 32f,
                Theme.ButtonFill, WidgetText, Close, 12);

            // Fixed narrow module rail. Parent and child selections use normal
            // buttons; no persistent second horizontal menu or giant icon tabs.
            ArcaneUiControls.Panel(_window, "SidebarBackground", 0f, 63f, 210f, 559f,
                ArcaneUiControls.Multiply(Theme.Panel, .94f), false);
            ArcaneUiControls.Panel(_window, "SidebarSeparator", 209f, 63f, 1f, 559f,
                Theme.Border, false);
            // One properly sized, scrollable navigation tree. Old stacked category
            // tabs + a separate pages block frequently squeezed long labels into
            // unreadable vertical columns on some game/UI resolutions.
            ArcaneUiControls.Label(_window, "NavigationCaption", "NAVIGATION", 17f, 73f,
                178f, 17f, 10, Theme.Accent, TextAnchor.MiddleLeft, FontStyle.Bold);
            _topNavigation = ArcaneUiControls.ScrollContent(_window, "CategoryNavigation",
                10f, 94f, 195f, 513f, Color.clear);
            var navigationLayout = _topNavigation.GetComponent<VerticalLayoutGroup>();
            if (navigationLayout != null)
            {
                navigationLayout.spacing = 3f;
                navigationLayout.padding = new RectOffset(2, 2, 3, 10);
            }

            ArcaneUiControls.Panel(_window, "SettingsPanel", BodyX - 8f, 73f,
                BodyWidth + 16f, 542f, ArcaneUiControls.Multiply(Theme.Background, 1.08f), false);
            _pageTitle = ArcaneUiControls.Label(_window, "PageTitle", "General", BodyX, 76f,
                BodyWidth - 10f, 29f, 20, Theme.Text, TextAnchor.MiddleLeft, FontStyle.Bold);
            _pageSubheading = ArcaneUiControls.Label(_window, "PageSubtitle", "", BodyX, 107f,
                BodyWidth - 12f, 20f, 11, Muted);
            _tabs = ArcaneUiControls.Rect(_window, "PageTabs", BodyX, 134f, BodyWidth, 33f);
            _content = ArcaneUiControls.ScrollContent(_window, "SettingsScroll", BodyX, 173f,
                BodyWidth, 438f, Color.clear);
            var scrollLayout = _content.GetComponent<VerticalLayoutGroup>();
            if (scrollLayout != null)
            {
                scrollLayout.spacing = 4f;
                scrollLayout.padding = new RectOffset(2, 2, 3, 10);
            }
            var settingsScroll = _content.GetComponentInParent<ScrollRect>();
            _settingsScrollRoot = settingsScroll != null
                ? settingsScroll.GetComponent<RectTransform>() : null;

            // The same interactive preview is an optional RIGHT-HAND COLUMN,
            // not an overlay above the settings. Its 504x552 content geometry
            // stays unchanged so all existing click-to-setting preview links work.
            var previewDivider = ArcaneUiControls.Panel(_window, "PreviewDivider",
                Width + 3f, 70f, 1f, 546f, Theme.Border, false);
            previewDivider.gameObject.SetActive(_previewVisible);
            _contextPanel = ArcaneUiControls.Panel(_window, "ContextPanel",
                Width + PreviewPanelGap, 70f, PreviewPanelWidth, 552f, Theme.Panel);
            _contextPanel.gameObject.SetActive(_previewVisible);

            // Search results float above the settings without taking page width.
            _searchResults = ArcaneUiControls.Panel(_window, "SearchResults", 702f, 51f,
                338f, 292f, Theme.Panel);
            _searchResults.gameObject.AddComponent<RectMask2D>();
            _searchResults.gameObject.SetActive(false);

            ArcaneUiControls.Panel(_window, "FooterLine", 0f, 621f, Width, 1f, Theme.Border, false);
            ArcaneUiControls.Label(_window, "FooterInfo", "Changes save automatically  •  /arcaneui",
                16f, 626f, 700f, 26f, 11, Muted);
            ArcaneUiControls.Button(_window, "Done", "DONE", Width - 116f, 627f,
                100f, 27f, Theme.ButtonActiveFill, Color.white, Close, 11);
            CreateBindingPicker();
            ApplyPreviewWindowGeometry();
            FitWindowToCanvas();
        }

        // All preview geometry changes are non-persistent presentation state.
        // Never rebuild game frame UI or reset any individual module settings.
        private void ApplyPreviewWindowGeometry()
        {
            if (_window == null) return;
            float width = _previewVisible ? ExpandedWidth : Width;
            _window.sizeDelta = new Vector2(width, Height);
            string[] stretch = { "Header", "HeaderAccent", "FooterLine" };
            foreach (string name in stretch)
            {
                RectTransform element = _window.Find(name) as RectTransform;
                if (element != null)
                    element.sizeDelta = new Vector2(width, element.sizeDelta.y);
            }
            RectTransform close = _window.Find("Close") as RectTransform;
            if (close != null) close.anchoredPosition = new Vector2(width - 50f, -14f);
            RectTransform done = _window.Find("Done") as RectTransform;
            if (done != null) done.anchoredPosition = new Vector2(width - 116f, -627f);
            RectTransform divider = _window.Find("PreviewDivider") as RectTransform;
            if (divider != null) divider.gameObject.SetActive(_previewVisible);
            if (_contextPanel != null) _contextPanel.gameObject.SetActive(_previewVisible);
            FitWindowToCanvas();
        }

        private void TogglePreviewPanel()
        {
            CloseChoicePopup();
            _previewVisible = !_previewVisible;
            ApplyPreviewWindowGeometry();
            if (_contextPanel != null && _previewVisible) _contextPanel.SetAsLastSibling();
            // A preview may be absent for this page until explicitly opened.
            // Rebuild only on the click, never during routine slider changes.
            RenderPage();
        }

        private void RenderNavigation()
        {
            RenderTopNavigation();
            RenderSearchResults();
        }

        private void RenderSearchResults()
        {
            if (_searchResults == null) return;
            ArcaneUiControls.ClearChildren(_searchResults);
            string query = (_filter ?? "").Trim();
            _searchResults.gameObject.SetActive(query.Length > 0);
            if (query.Length == 0) return;
            ArcaneUiControls.Label(_searchResults, "Heading", "MATCHING SETTINGS", 12, 8,
                310, 22, 11, Theme.Accent, TextAnchor.MiddleLeft, FontStyle.Bold);
            var scroll = ArcaneUiControls.ScrollContent(_searchResults, "Results", 6, 35,
                326, 246, Color.clear);
            int count = 0;
            foreach (SettingLocation match in SettingIndex)
            {
                if (match.Tab == "Native" || match.Tab == "Native HUD" ||
                    match.Page == "unit" || TechnicalAction(match.Label) || TechnicalInformation(match.Label)) continue;
                if ((match.Label + " " + GetPageName(match.Page) + " " + match.Tab)
                    .IndexOf(query, StringComparison.OrdinalIgnoreCase) < 0) continue;
                if (++count > 45) break;
                var row = ArcaneUiControls.Row(scroll, "SearchMatch_" + count, 51, Card);
                ArcaneUiControls.Button(row, "Jump", match.Label, 4, 2, 320, 27,
                    Theme.Panel, Theme.Text,
                    () => JumpToSetting(match.Page, match.Tab, match.Label), 11);
                ArcaneUiControls.Label(row, "Path", GetPageName(match.Page) + " / " + match.Tab,
                    10, 30, 300, 16, 9, Muted);
            }
            if (count == 0)
            {
                var row = ArcaneUiControls.Row(scroll, "NoMatch", 46, Card);
                ArcaneUiControls.Label(row, "Label", "No matching settings", 9, 0, 300, 40, 11, Muted);
            }
            ArcaneUiControls.RefreshScrollLayout(scroll);
            _searchResults.SetAsLastSibling();
        }

        private void SelectPage(string id)
        {
            if (_pendingFrameRefresh) FlushFrameRefresh();
            ClearHeaderSearch();
            _page = id;
            _tab = "General";
            if (_bindingPicker != null) _bindingPicker.gameObject.SetActive(false);
            ArcaneUiPreferences.LastPage = id;
            RenderNavigation();
            RenderPage();
            ArcaneUiControls.RefreshScrollLayout(_content, true);
        }

        private void JumpToSetting(string page, string tab, string label)
        {
            if (_pendingFrameRefresh) FlushFrameRefresh();
            ClearHeaderSearch();
            _page = page;
            _tab = tab;
            _pendingHighlight = label;
            ArcaneUiPreferences.LastPage = page;
            if (_bindingPicker != null) _bindingPicker.gameObject.SetActive(false);
            RenderNavigation();
            RenderPage();
        }

        private void RegisterSetting(string label, RectTransform row)
        {
            if (row != null) _settingRows[label] = row;
        }

        private void HighlightSetting()
        {
            if (string.IsNullOrEmpty(_pendingHighlight)) return;
            RectTransform row;
            if (_settingRows.TryGetValue(_pendingHighlight, out row) && row != null)
            {
                Image img = row.GetComponent<Image>();
                if (img != null) img.color = Color.Lerp(Card, Theme.Accent, 0.24f);
                ArcaneUiControls.RefreshScrollLayout(_content);
                ScrollRect scroll = _settingsScrollRoot != null
                    ? _settingsScrollRoot.GetComponent<ScrollRect>() : null;
                if (scroll != null && scroll.viewport != null)
                {
                    float range = Mathf.Max(1f, _content.rect.height - scroll.viewport.rect.height);
                    Bounds rowBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(_content, row);
                    float rowTop = -rowBounds.max.y;
                    scroll.verticalNormalizedPosition = 1f - Mathf.Clamp01((rowTop - 12f) / range);
                }
            }
            _pendingHighlight = null;
        }

        private void ClearPreviewHover()
        {
            if (_activePreviewHighlight != null)
                _activePreviewHighlight.SetHighlighted(false);
            _activePreviewHighlight = null;
            _previewHoverHint = null;
            _previewHintDefault = null;
        }

        private void SetPreviewHint(Text hint, string defaultHint)
        {
            _previewHoverHint = hint;
            _previewHintDefault = defaultHint;
            if (hint != null) hint.text = defaultHint;
        }

        private void OnPreviewHover(ArcaneUiPreviewHoverHighlight hovered,
            bool isHovering, string setting)
        {
            if (isHovering)
            {
                if (_activePreviewHighlight != null && _activePreviewHighlight != hovered)
                    _activePreviewHighlight.SetHighlighted(false);
                _activePreviewHighlight = hovered;
                if (_previewHoverHint != null)
                    _previewHoverHint.text = setting + "  •  Click to edit";
            }
            else if (_activePreviewHighlight == hovered)
            {
                _activePreviewHighlight = null;
                if (_previewHoverHint != null)
                    _previewHoverHint.text = _previewHintDefault ?? "Click an element to edit.";
            }
        }

        private void LinkPreview(Graphic graphic, string tab, string label)
        {
            if (graphic == null) return;
            graphic.raycastTarget = true;
            var button = graphic.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.targetGraphic = graphic;
            button.onClick.AddListener(() => JumpToSetting(_page, tab, label));

            // Hover decoration lives on the preview graphic, not the real
            // unit-frame renderer. It follows that graphic's RectTransform
            // and remains visible during frame-size / tag-position edits.
            var highlight = graphic.gameObject.AddComponent<ArcaneUiPreviewHoverHighlight>();
            highlight.Initialize(Theme.Accent, graphic is Text,
                (source, hovered) => OnPreviewHover(source, hovered, label));
        }

        private bool IsFramePage { get { return _page == "party" || _page == "raid"; } }
        private void RenderPage()
        {
            CloseChoicePopup();
            if (_content == null) return;
            ClearPreviewHover();
            _previewFrame = null;
            _previewHealth = null;
            _previewMana = null;
            _previewName = null;
            _previewPercent = null;
            _previewRawHealth = null;
            _previewDetails = null;
            _previewStyle = null;
            // Preview child objects are destroyed/rebuilt as pages change.
            // Avoid keeping a stale resource renderer on a destroyed RectTransform.
            _fotfPreview = null;
            _enemyTrackerPreview = null;
            _pageTitle.text = GetPageName(_page);
            _pageSubheading.text = GetPageDescription(_page);
            RenderTabs();
            if (_previewVisible) RenderContextPanel();
            // The only interactive preview now lives inside the right column. Native
            // discovery needs the full settings area, so omit it on that tab.
            // Editing never changes the selected target or spawns characters.
            bool framePreview = (IsFramePage && _tab != "Click Casting") ||
                _page == "player" || _page == "target" || _page == "manager" ||
                _page == "nameplates" || _page == "auras" || _page == "action" || _page == "character" || _page == "inventory" || _page == "xp" || _page == "chat" || _page == "minimap" || _page == "fotf" || _page == "enemytracker";
            if (_previewHost != null)
            {
                _previewHost.gameObject.SetActive(framePreview && _previewVisible);
                if (framePreview && _previewVisible)
                {
                    if (_page == "player") RenderPlayerPreview();
                    else if (_page == "fotf") RenderFOTFResourcesPreview();
                    else if (_page == "target") RenderTargetPreview();
                    else if (_page == "manager") RenderManagerPreview();
                    else if (_page == "character") RenderCharacterSheetPreview();
                    else if (_page == "inventory") RenderInventoryPreview();
                    else if (_page == "chat") RenderChatPreview();
                    else if (_page == "minimap") RenderMinimapPreview();
                    else if (_page == "enemytracker") RenderEnemyTrackerPreview();
                    else if (_page == "nameplates") RenderNameplatePreview();
                    else if (_page == "auras")
                    {
                        if (IsAuraEditorTab(_tab)) RenderCustomAuraPreview();
                        else RenderCooldownAuraPreview();
                    }
                    else if (_page == "action") RenderActionBarPreview();
                    else RenderFramePreview(_page == "party");
                }
            }
            // IMPORTANT: Only move the outer ScrollRect, never _content.
            // _content must remain top-anchored at (0,0) inside its clipped
            // viewport so VerticalLayoutGroup can lay out full-width rows.
            if (_settingsScrollRoot != null)
            {
                // The preview no longer consumes vertical settings space.
                _settingsScrollRoot.anchoredPosition = new Vector2(BodyX, -173f);
                _settingsScrollRoot.sizeDelta = new Vector2(BodyWidth, 438f);
            }
            if (_bindingPicker != null) _bindingPicker.gameObject.SetActive(false);
            ArcaneUiControls.ClearChildren(_content);
            switch (_page)
            {
                case "global": RenderGlobal(); break;
                case "character": RenderCharacterSheet(); break;
                case "inventory": RenderInventory(); break;
                case "chat": RenderChat(); break;
                case "minimap": RenderMinimap(); break;
                case "enemytracker": RenderEnemyTracker(); break;
                case "xp": RenderExperienceBar(); break;
                case "player": RenderPlayer(); break;
                case "fotf": RenderFOTFResourceOptions(); break;
                case "target": RenderTarget(); break;
                case "cast": RenderCastBars(); break;
                case "nameplates": RenderNameplates(); break;
                case "auras": RenderCooldownAuras(); break;
                case "action": RenderActionBars(); break;
                case "editor": RenderLayoutEditorPage(); break;
                case "themes": RenderThemes(); break;
                case "party":
                case "raid": RenderFrames(); break;
                case "manager": RenderManager(); break;
                case "interactions": RenderInteractions(); break;
                case "unit": RenderTargetDiscovery(); break;
                default: RenderComingSoon(); break;
            }
            ArrangeTwoColumnSettings();
            ArcaneUiControls.RefreshScrollLayout(_content);
            HighlightSetting();
        }

        private void RenderTabs()
        {
            ArcaneUiControls.ClearChildren(_tabs);
            if (!IsFramePage && _page != "player" && _page != "target" && _page != "action" && _page != "nameplates" && _page != "auras" && _page != "character" && _page != "inventory" && _page != "xp" && _page != "chat" && _page != "minimap" && _page != "enemytracker" && _page != "fotf") return;
            if (_tab == "Native" || _tab == "Native HUD" || _tab == "Diagnostics")
                _tab = "General";
            string[] tabs = _page == "fotf" ? new[] { "General", "Primary", "Secondary" } : _page == "xp" ? new[] { "General", "Appearance", "Position" } :
                _page == "character" ? new[] { "General", "Appearance", "Equipment" } :
                _page == "inventory" ? new[] { "General", "Appearance", "Slots" } :
                _page == "chat" ? new[] { "General", "Appearance", "Text", "Layout", "Visibility" } :
                _page == "minimap" ? new[] { "General", "Appearance", "Layout" } :
                _page == "enemytracker" ? new[] { "General", "Appearance", "Text", "Colours", "Visibility", "Layout" } :
                _page == "auras" ? new[] { "General", "Appearance", "Position", "Aura Manager" } :
                _page == "nameplates" ?
                new[] { "General", "Appearance", "Text" } :
                _page == "action" ?
                new[] { "General", "Bars", "Keybinds", "Layouts", "Themes", "Borders", "Icons & Text" } :
                _page == "target" ?
                new[] { "General", "Appearance", "Advanced", "Text", "Effects", "Casting" } :
                _page == "player" ? new[] { "General", "Appearance", "Advanced", "Text", "Casting", "FOTF Resources" } :
                new[] { "General", "Appearance", "Advanced", "Text", "Click Casting" };
            for (int i = 0; i < tabs.Length; i++)
            {
                string tab = tabs[i];
                bool active = _tab == tab;
                bool compactTabs = _page == "target" || _page == "player" || _page == "action" || _page == "nameplates" || _page == "auras" || _page == "character" || _page == "inventory" || _page == "xp" || _page == "chat" || _page == "fotf" || _page == "enemytracker";
                // Six chat tabs are sized to fit the full 814px settings strip.
                // Previously seven tabs caused some controls to appear missing.
                float tabWidth = _page == "chat" ? 130f : _page == "action" ? 99f : compactTabs ? 111f : 140f;
                float tabStep = _page == "chat" ? 135f : _page == "action" ? 101f : compactTabs ? 116f : 151f;
                ArcaneUiControls.Button(_tabs, "Tab_" + tab, tab,
                    i * tabStep, 4, tabWidth, 32,
                    active ? Theme.Accent : Theme.Panel, active ? Color.white : Muted,
                    () => { _tab = tab; RenderPage(); }, compactTabs ? 10 : 12);
            }
        }

        private string GetPageName(string id)
        {
            foreach (var page in GeneralPages) if (page.Id == id) return page.Label;
            foreach (var page in FramePages) if (page.Id == id) return page.Label;
            foreach (var page in CombatPages) if (page.Id == id) return page.Label;
            foreach (var page in FuturePages) if (page.Id == id) return page.Label;
            foreach (var page in CharacterPages) if (page.Id == id) return page.Label;
            return "General";
        }

        private string GetPageDescription(string id)
        {
            switch (id)
            {
                case "global": return "Customize the overall ArcaneUI experience.";
                case "character": return "Style Erenshor’s character sheet with a readable native-safe stats companion.";
                case "chat": return "Native chat styling, typography, Layout Editor integration and visibility.";
                case "minimap": return "True minimap masking, native visibility, borders and safe layout positioning.";
                case "enemytracker": return "Customize enemy rows, health displays and combat command buttons.";
                case "xp": return "Level XP and persistent max-level Ascension XP bar, with separate appearance settings.";
                case "editor": return "Move, align, resize and save your ArcaneUI frames.";
                case "themes": return "Choose the colour and presentation style for ArcaneUI controls and themed widgets.";
                case "player": return "Customize your health, mana, cast bar and player information.";
                case "fotf": return "Fate of the Fallen resources, live connection status and detachable layouts.";
                case "target": return "Customize the target frame, visibility, effects and cast bar.";
                case "cast": return "Player and target cast bars, colours and display options.";
                case "nameplates": return "Style health and name displays above characters.";
                case "auras": return "Native cooldown and aura tracking, plus a workspace-style custom Aura Manager inspired by WeakAuras.";
                case "action": return "Playable native and additional action bars, direct spellbook drops, independent spell slots and per-slot keybinds.";
                case "unit": return "Read-only discovery of native Target Frame objects; replacement not yet enabled.";
                case "party": return "Real-time settings for your existing party frames.";
                case "raid": return "Real-time settings for your existing raid frames.";
                case "manager": return "Manage raid controls without changing native raid logic.";
                case "interactions": return "Existing click-casting, mouseover and target interactions.";
                default: return "This module is planned for a later development phase.";
            }
        }

        private void Section(string heading, string subtitle = null)
        {
            if (TechnicalInformation(heading)) return;
            var row = ArcaneUiControls.Row(_content, "Section_" + heading, subtitle == null ? 23 : 37,
                Color.clear);
            ArcaneUiControls.Label(row, "Heading", heading.ToUpperInvariant(), 8, 0, 730, 21,
                12, Theme.Text, TextAnchor.MiddleLeft, FontStyle.Bold);
            if (!string.IsNullOrEmpty(subtitle))
                ArcaneUiControls.Label(row, "Subtitle", subtitle, 8, 18, 740, 17, 10, Muted);
        }

        private static bool TechnicalInformation(string heading)
        {
            if (string.IsNullOrEmpty(heading)) return false;
            string name = heading.ToLowerInvariant();
            return name.Contains("diagnostic") || name.Contains("candidate") ||
                name.Contains("native game data") || name.Contains("source") ||
                name.Contains("binding") || name.Contains("backend") ||
                name.Contains("bridge") || name.Contains("hierarchy") ||
                name.Contains("discovery") || name.Contains("verified native") ||
                name.Contains("migration") || name.Contains("compatibility") ||
                name.Contains("native path") || name.Contains("native visual selection") ||
                name.Contains("suppression status") || name.Contains("renderer status") ||
                name.Contains("technical") || name.Contains("snapshot") ||
                name.Contains("map backend") || name.Contains("gameplay behaviours unchanged") ||
                name.Contains("layout persistence") || name.Contains("first release") ||
                name.Contains("native fallback") || name.Contains("native safety") ||
                name.Contains("native tracker safety") || name.Contains("character sheet detection") ||
                name.Contains("live casting progress") || name.Contains("native effects") ||
                name.Contains("confirmed path") || name.Contains("native gameplay") ||
                name.Contains("character sheet status") || name.Contains("native equipment") ||
                name.Contains("resource mapping") || name.Contains("supported numeric resource") ||
                name.Contains("lightbringer ward data") || name.Contains("native layout") ||
                name.Contains("layout ratios") || name.Contains("no native ui suppression") ||
                name.Contains("bound inventory path") || name.Contains("native size") ||
                name.Contains("native tracker safety");
        }

        private static bool TechnicalAction(string heading)
        {
            if (string.IsNullOrEmpty(heading)) return false;
            string name = heading.ToLowerInvariant();
            return name.StartsWith("inspect native") || name.StartsWith("inspect original") ||
                name.StartsWith("inspect ui") || name.StartsWith("scan native") ||
                name.StartsWith("scan target") || name.StartsWith("scan ui") ||
                name.StartsWith("log target") || name.StartsWith("native action bar scan") ||
                name.Contains("diagnostic") || name.Contains("candidate") ||
                name.StartsWith("select native") || name.StartsWith("refresh tracker") ||
                name.StartsWith("refresh nameplate") || name.StartsWith("inspect original") ||
                name.StartsWith("use native nameplate candidate") ||
                name.StartsWith("use known character root") ||
                name.StartsWith("bind character window");
        }

        private void Info(string heading, string description)
        {
            if (TechnicalInformation(heading)) return;
            var row = ArcaneUiControls.Row(_content, "Info_" + heading, 45, Card);
            ArcaneUiControls.Label(row, "Title", heading, 14, 3, 740, 21, 12,
                Theme.Text, TextAnchor.MiddleLeft, FontStyle.Bold);
            ArcaneUiControls.Label(row, "Description", description, 14, 24, 740, 18, 10, Muted);
        }

        private void ActionButton(string heading, string caption, string label, Action onClick)
        {
            if (TechnicalAction(heading)) return;
            var row = ArcaneUiControls.Row(_content, "Action_" + heading, 43, Card);
            RegisterSetting(heading, row);
            ArcaneUiControls.Label(row, "Heading", heading, 14, 2, 586, 20, 12,
                Theme.Text, TextAnchor.MiddleLeft, FontStyle.Bold);
            ArcaneUiControls.Label(row, "Description", caption, 14, 22, 575, 18, 10, Muted);
            ArcaneUiControls.Button(row, "Action", label, 625, 6, 147, 29,
                WidgetButton(true), Color.white, onClick, 11);
        }

        private void ToggleSetting(string label, string description, Func<bool> get, Action<bool> set)
        {
            var row = ArcaneUiControls.Row(_content, "Toggle_" + label, 43, Card);
            RegisterSetting(label, row);
            ArcaneUiControls.Label(row, "Label", label, 14, 2, 620, 20, 12, Theme.Text,
                TextAnchor.MiddleLeft, FontStyle.Bold);
            ArcaneUiControls.Label(row, "Description", description, 14, 22, 620, 18, 10, Muted);
            bool enabled = get();
            ArcaneUiControls.Button(row, "Toggle", enabled ? "ON" : "OFF", 698, 7, 74, 27,
                WidgetToggle(enabled), Color.white, () =>
                {
                    set(!get());
                    UpdatePreview();
                    ScheduleFrameRefresh(true);
                    RenderPage();
                }, 11);
        }

        private void SliderSetting(string label, float min, float max,
            Func<float> get, Action<float> set, string suffix = "", bool wholeNumbers = true)
        {
            // Compact, single-line slider row. The handle is confined to a
            // 16px slide area: Slider.UpdateVisuals stretches its handle's
            // anchors vertically, so the handle MUST have zero height delta.
            var row = ArcaneUiControls.Row(_content, "Slider_" + label, 38, Card);
            RegisterSetting(label, row);
            ArcaneUiControls.Label(row, "Label", label, 14, 6, 397, 22, 12,
                Theme.Text, TextAnchor.MiddleLeft, FontStyle.Bold);
            var valueLabel = ArcaneUiControls.Label(row, "Value", get().ToString("0.#") + suffix,
                706, 6, 64, 22, 12, Theme.Accent, TextAnchor.MiddleRight, FontStyle.Bold);

            var sliderRoot = ArcaneUiControls.Panel(row, "Slider", 433, 7, 257, 23,
                new Color(0f, 0f, 0f, 0f));
            var slider = sliderRoot.gameObject.AddComponent<Slider>();
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = min;
            slider.maxValue = max;
            slider.wholeNumbers = wholeNumbers;

            var track = ArcaneUiControls.Panel(sliderRoot, "Track", 0, 0, 257, 4, Theme.SliderTrack, false);
            track.anchorMin = new Vector2(0f, .5f);
            track.anchorMax = new Vector2(1f, .5f);
            track.pivot = new Vector2(.5f, .5f);
            track.anchoredPosition = Vector2.zero;
            track.sizeDelta = new Vector2(0f, 4f);

            var fillArea = ArcaneUiControls.Rect(sliderRoot, "FillArea", 0, 0, 257, 4);
            fillArea.anchorMin = new Vector2(0f, .5f);
            fillArea.anchorMax = new Vector2(1f, .5f);
            fillArea.pivot = new Vector2(.5f, .5f);
            fillArea.anchoredPosition = Vector2.zero;
            fillArea.sizeDelta = new Vector2(-10f, 4f);
            var fill = ArcaneUiControls.Panel(fillArea, "Fill", 0, 0, 0, 4, Theme.SliderFill, false);
            fill.anchorMin = new Vector2(0f, 0f);
            fill.anchorMax = new Vector2(0f, 1f);
            fill.pivot = new Vector2(0f, .5f);
            fill.anchoredPosition = Vector2.zero;
            fill.sizeDelta = Vector2.zero;

            var handleArea = ArcaneUiControls.Rect(sliderRoot, "HandleSlideArea", 0, 0, 257, 16);
            handleArea.anchorMin = new Vector2(0f, .5f);
            handleArea.anchorMax = new Vector2(1f, .5f);
            handleArea.pivot = new Vector2(.5f, .5f);
            handleArea.anchoredPosition = Vector2.zero;
            handleArea.sizeDelta = new Vector2(-10f, 16f);
            var handle = ArcaneUiControls.Panel(handleArea, "Handle", 0, 0, 10, 0, Theme.SliderHandle, false);
            handle.anchorMin = new Vector2(0f, 0f);
            handle.anchorMax = new Vector2(0f, 1f);
            handle.pivot = new Vector2(.5f, .5f);
            handle.anchoredPosition = Vector2.zero;
            handle.sizeDelta = new Vector2(10f, 0f);

            slider.targetGraphic = handle.GetComponent<Image>();
            slider.fillRect = fill;
            slider.handleRect = handle;
            slider.SetValueWithoutNotify(get());
            slider.onValueChanged.AddListener(value =>
            {
                set(value);
                valueLabel.text = get().ToString("0.#") + suffix;
                UpdatePreview();
                ScheduleFrameRefresh();
            });
        }

        private void TextTagSetting(string module, string label, string tag, int defaultSize)
        {
            Section(label);
            SliderSetting(label + " Size", 7, 36,
                () => ArcaneUiTextTagStyle.Size(module, tag, defaultSize),
                value =>
                {
                    ArcaneUiTextTagStyle.SetSize(module, tag, value);
                    RefreshTextStyles(module);
                }, " px");
            ChoiceSetting(label + " Alignment", "Optional Left / Centre / Right alignment (preserves original until changed).",
                ArcaneUiTextTagStyle.Alignment(module, tag), () =>
                {
                    ArcaneUiTextTagStyle.CycleAlignment(module, tag);
                    RefreshTextStyles(module);
                });
            ChoiceSetting(label + " Border", "None / Thin Border / Thick Border / Pixel Border / Soft Shadow",
                ArcaneUiTextTagStyle.OutlineMode(module, tag),
                () =>
                {
                    ArcaneUiTextTagStyle.CycleOutline(module, tag);
                    RefreshTextStyles(module);
                });
            SliderSetting(label + " X Position", -100, 100,
                () => ArcaneUiTextTagStyle.Position(module, tag).x,
                value =>
                {
                    Vector2 pos = ArcaneUiTextTagStyle.Position(module, tag);
                    ArcaneUiTextTagStyle.SetPosition(module, tag, value, pos.y);
                    RefreshTextStyles(module);
                }, " px");
            SliderSetting(label + " Y Position", -100, 100,
                () => ArcaneUiTextTagStyle.Position(module, tag).y,
                value =>
                {
                    Vector2 pos = ArcaneUiTextTagStyle.Position(module, tag);
                    ArcaneUiTextTagStyle.SetPosition(module, tag, pos.x, value);
                    RefreshTextStyles(module);
                }, " px");
        }

        private void NameLengthSetting(string module)
        {
            SliderSetting("Name Character Limit", 4, 60,
                () => ArcaneUiNameFormatting.Limit(module),
                v => { ArcaneUiNameFormatting.SetLimit(module, v); RefreshTextStyles(module); }, " chars");
        }
        private void FrameBarDirectionSetting(string module)
        {
            ChoiceSetting("Health Fill Direction", "Left to Right / Right to Left / Bottom to Top / Top to Bottom.",
                ArcaneUiBarDirection.HealthDescription(module), () =>
                {
                    ArcaneUiBarDirection.CycleHealth(module);
                    RefreshBarDirections(module);
                });
            ChoiceSetting("Mana Fill Direction", "Independent direction: left, right, bottom or top.",
                ArcaneUiBarDirection.ManaDescription(module), () =>
                {
                    ArcaneUiBarDirection.CycleMana(module);
                    RefreshBarDirections(module);
                });
        }

        private static void RefreshBarDirections(string module)
        {
            // Changing direction is a presentation-only operation. Never alter
            // party/raid membership or the Raid Manager's saved slot layout.
            if (module == ArcaneUiModuleSettings.Party) PartyFramesUI.RefreshTheme();
            else if (module == ArcaneUiModuleSettings.Raid) RaidFramesUI.RefreshTheme();
            else if (module == ArcaneUiModuleSettings.Player) RefreshPlayerLayout();
            else if (module == ArcaneUiModuleSettings.Target) RefreshTargetLayout();
        }

        private static void RefreshTextStyles(string module)
        {
            if (module == ArcaneUiModuleSettings.Party) PartyFramesUI.RefreshTheme();
            else if (module == ArcaneUiModuleSettings.Raid) RaidFramesUI.RefreshTheme();
            else if (module == ArcaneUiModuleSettings.Player) RefreshPlayerStyle();
            else if (module == ArcaneUiModuleSettings.Target) RefreshTargetStyle();
        }

        private void ChoiceSetting(string label, string description, string current, Action next)
        {
            string[] options = OptionsForChoice(label, description);
            if (options == null || options.Length == 0)
            {
                ActionButton(label, description + "  Current: " + current, "CHANGE", () =>
                {
                    next(); UpdatePreview(); ScheduleFrameRefresh(true); RenderPage();
                });
                return;
            }
            var row = ArcaneUiControls.Row(_content, "Choice_" + label, 44f, Card);
            RegisterSetting(label, row);
            ArcaneUiControls.Label(row, "Title", label, 14f, 2f, 485f, 20f, 12,
                Theme.Text, TextAnchor.MiddleLeft, FontStyle.Bold);
            ArcaneUiControls.Label(row, "Description", description, 14f, 22f, 586f, 19f, 10, Muted);
            Button button = ArcaneUiControls.Button(row, "Choose", current + "  ▼", 595f, 8f,
                176f, 28f, Theme.DropdownFill, WidgetText, null, 11);
            button.onClick.AddListener(() => OpenChoicePopup(button.GetComponent<RectTransform>(),
                options, current, chosen =>
                {
                    ApplyCycledChoice(current, chosen, options, next);
                    UpdatePreview(); ScheduleFrameRefresh(true); RenderPage();
                }));
        }

        // A compact, explicit override control. 'NEXT' picks a manual value;
        // 'GLOBAL' returns to the selected theme/global value without erasing
        // the module's saved legacy preferences. In Blank mode that means LOCAL.
        private void ModuleAppearanceChoice(string label, string description,
            string effectiveValue, string manualOverride, Action next, Action reset,
            Action<string> assign)
        {
            var row = ArcaneUiControls.Row(_content, "Manual_" + label, 44f, Card);
            RegisterSetting(label, row);
            ArcaneUiControls.Label(row, "Label", label, 14f, 2f, 475f, 20f, 12,
                Theme.Text, TextAnchor.MiddleLeft, FontStyle.Bold);
            string source = !string.IsNullOrEmpty(manualOverride) ? "Manual" :
                (ArcaneUiStyleService.IsBlankTheme ? "Local / saved" : "Theme / global");
            ArcaneUiControls.Label(row, "Value", source + ": " + effectiveValue,
                14f, 22f, 585f, 19f, 10, Muted);
            string current = string.IsNullOrEmpty(manualOverride) ? "Global" : manualOverride;
            string[] options = WithFirst("Global", label == "Font"
                ? UnitFrameAppearance.FontNames : UnitFrameAppearance.TextureNames);
            Button button = ArcaneUiControls.Button(row, "Select", current + "  ▼",
                596f, 10f, 176f, 28f, Theme.DropdownFill, WidgetText, null, 11);
            button.onClick.AddListener(() => OpenChoicePopup(button.GetComponent<RectTransform>(),
                options, current, selected =>
                {
                    if (selected == "Global") reset();
                    else if (assign != null) assign(selected);
                    UpdatePreview(); ScheduleFrameRefresh(true); RenderPage();
                }));
        }

        // A reusable clickable colour swatch. Keeps the original saved hex
        // setters, so pre-existing PlayerPrefs colours load without migration.
        private void HexSetting(string label, string description, Func<string> get, Action<string> set)
        {
            var row = ArcaneUiControls.Row(_content, "Hex_" + label, 43, Card);
            RegisterSetting(label, row);
            ArcaneUiControls.Label(row, "Title", label, 14, 2, 480, 20, 12,
                Theme.Text, TextAnchor.MiddleLeft, FontStyle.Bold);
            ArcaneUiControls.Label(row, "Description", description, 14, 21, 590, 18, 10, Muted);

            Color swatchColor;
            if (!UnityEngine.ColorUtility.TryParseHtmlString(get(), out swatchColor))
                swatchColor = Color.white;
            var pickerButton = ArcaneUiControls.Button(row, "EditColour", get(), 624, 7, 148, 27,
                Theme.DropdownFill, WidgetText, null, 11);
            var swatch = ArcaneUiControls.Panel(pickerButton.transform, "Swatch", 8, 5, 25, 17,
                swatchColor, false).GetComponent<Image>();
            var valueLabel = pickerButton.GetComponentInChildren<Text>();
            if (valueLabel != null) valueLabel.alignment = TextAnchor.MiddleRight;
            pickerButton.onClick.AddListener(() =>
            {
                if (_activeColorPicker != null) _activeColorPicker.Cancel();
                // Do not rebuild the settings page while the picker is open;
                // that would destroy the active input/drag targets.
                _activeColorPicker = ArcaneUiColorPickerDialog.Show(_window, label, get(),
                    Theme.Background, Card, Theme.Accent, Theme.Text,
                    hex =>
                    {
                        set(hex);
                        Color parsed;
                        if (ColorUtility.TryParseHtmlString(get(), out parsed)) swatch.color = parsed;
                        if (valueLabel != null) valueLabel.text = get();
                        UpdatePreview();
                        if (_page == "action") DrawActionPreview();
                        ScheduleFrameRefresh();
                    },
                    () => { _activeColorPicker = null; });
            });
        }

        private void NavigateToBindings(bool party)
        {
            SelectPage(party ? "party" : "raid");
            _tab = "Click Casting";
            RenderPage();
        }

        private void ModuleToggle(string id, string label, string description)
        {
            ToggleSetting(label, description,
                () => _modules.IsEnabled(id),
                value => _modules.SetEnabled(id, value));
        }

        private void RenderGlobal()
        {
            Section("NUMBER DISPLAY", "One number format for Player, Target, Party and Raid frames.");
            ToggleSetting("Abbreviate Numbers",
                "Show large health and mana values as 1.2K / 3.4M / 1.5B on every frame.",
                () => ArcaneUiValueFormatting.Abbreviate(),
                enabled =>
                {
                    ArcaneUiValueFormatting.SetAbbreviate(enabled);
                    // Repaint existing party and raid member text immediately;
                    // player and target views read the shared setting on their next update.
                    PartyFramesUI.RefreshTheme();
                    RaidFramesUI.RefreshTheme();
                });
            Section("MODULE ENABLEMENT", "Disable individual replacements to use Erenshor's native interface instead.");
            ModuleToggle(ArcaneUiModuleSettings.Player, "Player Frame",
                "New native-data frame. OFF preserves Erenshor's existing player HUD.");
            ModuleToggle(ArcaneUiModuleSettings.Party, "Party Frames",
                "OFF restores the native party-member frames.");
            ModuleToggle(ArcaneUiModuleSettings.Raid, "Raid Frames",
                "OFF restores the native raid-member list and its interactions.");
            ModuleToggle(ArcaneUiModuleSettings.Manager, "Raid Manager",
                "OFF hides custom raid controls; Erenshor's native manager remains usable.");
            Section("FATE OF THE FALLEN SUPPORT", "Optional Lightbringer absorb wards on Player, Target, Party and Raid health bars.");
            ToggleSetting("Show Lightbringer Absorb",
                "Show remaining wards over existing health. Automatically hidden when FOTF or ward data is unavailable.",
                () => FOTFAbsorbOverlay.Enabled, v => FOTFAbsorbOverlay.SetEnabled(v));
            HexSetting("Absorb Overlay Colour", "Colour of the translucent shield segment, independent of class colours.",
                () => FOTFAbsorbOverlay.ColourHex, v => FOTFAbsorbOverlay.SetColour(v));
            SliderSetting("Absorb Overlay Opacity", 10f, 100f,
                () => FOTFAbsorbOverlay.Opacity, v => FOTFAbsorbOverlay.SetOpacity(v), "%");
            SliderSetting("Absorb Minimum Visible Width", 0f, 30f,
                () => FOTFAbsorbOverlay.MinimumVisibleWidth,
                v => FOTFAbsorbOverlay.SetMinimumVisibleWidth(v), "%");
            Info("Absorb width accuracy",
                "Minimum Visible Width only enlarges the golden highlight for readability. " +
                "It does not increase the real shield. Set it to 0% for a strictly proportional segment.");
            Info("Lightbringer ward data",
                "Reads FateOfTheFallen.LightbringerAbsorbApi.GetRemainingAbsorb(Stats) when present. " +
                "Does not alter health, shield mechanics, spellcasting or target interactions. " +
                "The overlay is hidden until FOTF reports an active ward.");
            Section("WELCOME TO ARCANEUI", "Shape your UI in a few clicks. Everything saves as you change it.");
            Info("Individual Settings Persistence",
                "ArcaneUI saves module preferences as you change them. You do not need a profile to keep your theme, typography or frame positions.");
            Section("QUICK START", "Your most-used controls, in one place.");
            ActionButton("Prefer ArcaneUI Replacements",
                _confirmPrimaryHud ? "Confirm: enable ArcaneUI displays and hide available original visuals. Your positions, theme and bindings remain unchanged." :
                "Apply the new default visibility to this existing installation without changing your layout or colours.",
                _confirmPrimaryHud ? "CONFIRM" : "SET AS DEFAULT", () =>
                {
                    if (!_confirmPrimaryHud) { _confirmPrimaryHud = true; RenderPage(); return; }
                    _confirmPrimaryHud = false;
                    PreferArcaneUiHud();
                    RenderPage();
                });
            ActionButton("Unlock UI", "Drag and resize every active frame, including hidden buff/debuff placeholders.",
                "EDIT LAYOUT", _openLayoutEditor);
            ActionButton("Action bars", "Set bar layouts, slot sizes and shortcuts.",
                "ACTION BARS", () => SelectPage("action"));
            ActionButton("Buffs, Debuffs & Cooldowns", "Choose which effects appear and where their frames sit.",
                "OPEN AURAS", () => SelectPage("auras"));
            ActionButton("Player frame", "New player health and mana display sourced from native stats.",
                "PLAYER SETTINGS", () => SelectPage("player"));
            Section("APPEARANCE & FRAMES", "Tune the look without changing game controls.");
            ActionButton("Current theme", "Active: " + Theme.DisplayName,
                "CHOOSE THEME", () => SelectPage("themes"));
            ActionButton("Party frames", "Configure existing group frames and their appearance.",
                "PARTY SETTINGS", () => SelectPage("party"));
            ActionButton("Raid frames", "Configure raid frame appearance and behaviour.",
                "RAID SETTINGS", () => SelectPage("raid"));
            ActionButton("Enemy Tracker", "Customize target rows and party/raid attack commands.",
                "TRACKER", () => SelectPage("enemytracker"));
            ActionButton("Global typography", "Set font family, size, style and outline for the entire UI.",
                "FONTS", () => SelectPage("themes"));
            Info("Opening ArcaneUI",
                "Type /arcaneui in game chat to toggle this window. Escape closes it.");
        }

        // One-time, explicit opt-in for upgrades. This is NOT a saved profile:
        // it changes only presentation visibility switches, not any dimensions,
        // colours, keybinds, aura libraries, actions or frame positions.
        private static void PreferArcaneUiHud()
        {
            ArcaneUiModuleSettings.SetEnabled(ArcaneUiModuleSettings.Player, true);
            ArcaneUiModuleSettings.SetEnabled(ArcaneUiModuleSettings.Target, true);
            ArcaneUiModuleSettings.SetEnabled(ArcaneUiModuleSettings.Party, true);
            ArcaneUiModuleSettings.SetEnabled(ArcaneUiModuleSettings.Raid, true);
            ArcaneUiModuleSettings.SetEnabled(ArcaneUiModuleSettings.Manager, true);
            ArcaneUiModuleSettings.SetEnabled(ArcaneUiModuleSettings.Character, true);
            ArcaneUiModuleSettings.SetEnabled(ArcaneUiModuleSettings.Inventory, true);
            NativePlayerHudBridge.SetSuppress(true);
            NativeTargetHudBridge.SetSuppress(true);
            NativeCastBarBridge.SetHideNative(true);
            ArcanesGroupFrames.Modules.ActionBars.Configuration.ActionBarMirrorSettings.SetEnabled(true);
            ArcanesGroupFrames.Modules.ActionBars.Configuration.ActionBarMirrorSettings.SetHideNativeBar(true);
            EnemyTrackerSettings.SetEnabled(true);
            EnemyTrackerSettings.SetHideNative(true);
            NameplateSettings.SetEnabled(true);
            NameplateSettings.SetHideNative(true);
            ExperienceBarSettings.SetEnabled(true);
            CooldownAuraSettings.SetEnabled(true);
            ArcanesGroupFrames.Modules.Chat.Configuration.ChatSettings.SetBool("Enabled", true);
            MinimapSettings.SetBool("Enabled", true);
            MinimapSettings.SetBool("ShowNative", false);
            // Keep each saved effect-category preference. Only suppress the
            // native version of categories the player chose to show.
            ArcanesGroupFrames.Framework.NativeUI.Status.NativeBuffDebuffVisibility.SetSuppress(
                ArcanesGroupFrames.Framework.NativeUI.Status.NativeBuffDebuffVisibility.Channel.PlayerBuffs,
                CooldownAuraSettings.PlayerBuffs);
            ArcanesGroupFrames.Framework.NativeUI.Status.NativeBuffDebuffVisibility.SetSuppress(
                ArcanesGroupFrames.Framework.NativeUI.Status.NativeBuffDebuffVisibility.Channel.PlayerDebuffs,
                CooldownAuraSettings.PlayerDebuffs);
            ArcanesGroupFrames.Framework.NativeUI.Status.NativeBuffDebuffVisibility.SetSuppress(
                ArcanesGroupFrames.Framework.NativeUI.Status.NativeBuffDebuffVisibility.Channel.TargetBuffs,
                CooldownAuraSettings.TargetBuffs);
            ArcanesGroupFrames.Framework.NativeUI.Status.NativeBuffDebuffVisibility.SetSuppress(
                ArcanesGroupFrames.Framework.NativeUI.Status.NativeBuffDebuffVisibility.Channel.TargetDebuffs,
                CooldownAuraSettings.TargetDebuffs);
            PlayerPrefs.Save();
        }

        private void RenderLayoutEditorPage()
        {
            Section("LAYOUT EDITOR", "Edit the actual Party Frames, Raid Frames and Raid Manager positions.");
            ActionButton("Unlock UI", "Grid overlay, snapping, positioning and corner resize handles.",
                "OPEN EDITOR", _openLayoutEditor);
            Info("Editing", "Select a frame overlay, drag to move, or drag its bottom-right corner to resize. " +
                "The Raid Manager has a fixed native size.");
            Info("Undo / Redo and Layout Preset", "Undo or redo editor changes and save/load the " +
                "Default layout (positions, dimensions and group spacing). This preset does not " +
                "replace Erenshor's raid membership Save/Load behaviour.");
            Info("Back to Settings", "Use DONE or Escape to leave mover mode and return here. " +
                "Only /arcaneui is registered as a chat command.");
        }

        private void RenderThemes()
        {
            Section("THEME PRESETS", "Health, mana, borders, tabs, buttons and shared widget treatments change across themed frames.");
            AddTheme("dark", "Arcane Dark", "Flat charcoal panels, cyan highlights and clean modern widgets.");
            AddTheme("fantasy", "Refined Fantasy", "Warm brass palette and beveled bars, without added artwork.");
            AddTheme("traditional-rpg", "Traditional RPG", "Bronze, parchment tones and classic RPG widget styling — no illustrated frame overlays.");
            AddTheme("arcane", "Arcane Relic", "Violet and sapphire colours for a magical UI, without decorative overlays.");
            AddTheme("nordic", "Nordic Stone", "Frosted steel and slate colours in the standard compact layout.");
            AddTheme("crimson", "Crimson Warlord", "Crimson and dark-metal colours using the standard frame geometry.");
            AddTheme("verdant", "Verdant Grove", "Forest green and gold colours using normal ArcaneUI widgets.");
            AddTheme("minimal", "Minimal", "Restrained contrast, cool greys and smooth health bars.");
            AddTheme("blank", "Blank / Custom", "No preset colour or texture overrides; use your saved module settings.");
            if (ArcaneUiStyleService.IsBlankTheme)
                Info("Manual styling mode", "Party and Raid Frames retain their own health, texture, font and background settings. " +
                    "Class colours follow each module's existing choice. Use the Appearance pages to customize them. " +
                    "The Raid Manager keeps its original styling unless you enable the custom palette below.");
            if (ArcaneUiStyleService.IsBlankTheme)
            {
                Section("CUSTOM COLOUR PALETTE", "Optional, saved independently of the built-in presets.");
                ToggleSetting("Apply Custom Palette", "OFF preserves original module colours; ON applies the colours below to themed modules.",
                    () => ArcaneUiStyleService.BlankPaletteEnabled,
                    value => ArcaneUiStyleService.SetBlankPaletteEnabled(value));
                CustomColorSetting("Health Bar", "Health", "Colour of filled health bars when class colours are disabled.");
                CustomColorSetting("Missing Health", "MissingHealth", "Empty health / damage background.");
                CustomColorSetting("Mana Bar", "Mana", "Resource bar fill colour.");
                CustomColorSetting("Frame Background", "Background", "Panel background colour.");
                CustomColorSetting("Panel Surface", "Panel", "Secondary surfaces and raid group headers.");
                CustomColorSetting("Border", "Border", "Borders and slider tracks.");
                CustomColorSetting("Accent", "Accent", "Settings highlights and themed buttons.");
            }
            Section("GLOBAL STYLE", ArcaneUiStyleService.IsBlankTheme
                ? "Optional global texture/font overrides; Theme Default keeps module settings."
                : "Per-module manual texture and font selections take priority over these values.");
            ChoiceSetting("Health Bar Texture", "Global texture (Theme Default follows the preset).",
                ArcaneUiStyleService.GlobalBarTexture,
                () => ArcaneUiStyleService.SetGlobalBarTexture(ArcaneUiStyleService.NextTexture()));
            ChoiceSetting("Global Font Family", "Global ArcaneUI font (Theme Default follows the selected preset).",
                ArcaneUiStyleService.GlobalFont,
                () => ArcaneUiStyleService.SetGlobalFont(ArcaneUiStyleService.NextFont()));
            Section("GLOBAL TYPOGRAPHY", "Shared font defaults for ArcaneUI settings and supported custom HUD labels. Individual text overrides take precedence.");
            SliderSetting("Global Font Size", 75f, 150f, () => ArcaneUiGlobalTypography.FontScale,
                v => ArcaneUiGlobalTypography.SetFontScale(v), " %");
            ChoiceSetting("Global Font Style", "Module Default, Normal, Bold, Italic or Bold Italic.",
                ArcaneUiGlobalTypography.FontStyleName,
                () => { ArcaneUiGlobalTypography.CycleFontStyle(); RenderPage(); });
            ChoiceSetting("Global Font Outlines", "Module Default, None, Thin, Thick, Pixel or Soft Shadow.",
                ArcaneUiGlobalTypography.OutlineName,
                () => { ArcaneUiGlobalTypography.CycleOutline(); RenderPage(); });
            Info("Override priority", "Per-tag font sizes and outlines take priority. Per-module font family overrides remain authoritative. " +
                "Global size is a percentage so the header, frame names and HUD numbers retain their relative hierarchy.");
            SliderSetting("Frame Border", 1, 3,
                () => ArcaneUiStyleService.GlobalBorderSize,
                value => ArcaneUiStyleService.SetGlobalBorderSize((int)value), " px");
            SliderSetting("Panel Opacity", 30, 100,
                () => ArcaneUiStyleService.GlobalOpacity * 100f,
                value => ArcaneUiStyleService.SetGlobalOpacity(value / 100f), "%");
            Info("Colour priority", ArcaneUiStyleService.IsBlankTheme
                ? "Blank preserves existing module colours until Apply Custom Palette is enabled. Class-Coloured Health and per-module font/texture overrides remain independent."
                : "Presets override class-coloured health by default. Enable class colours per module, or edit that module's texture and font independently.");
            Section("THEME INHERITANCE");
            ToggleSetting("Party Frames", "Apply global font, textures, borders and surfaces.",
                () => ArcaneUiStyleService.Inherits(ArcaneUiModuleSettings.Party),
                value => ArcaneUiStyleService.SetInherits(ArcaneUiModuleSettings.Party, value));
            ToggleSetting("Raid Frames", "Keep independent sizes, colours, and click bindings.",
                () => ArcaneUiStyleService.Inherits(ArcaneUiModuleSettings.Raid),
                value => ArcaneUiStyleService.SetInherits(ArcaneUiModuleSettings.Raid, value));
            ToggleSetting("Raid Manager", "Restyle custom panel surfaces without rebuilding controls.",
                () => ArcaneUiStyleService.Inherits(ArcaneUiModuleSettings.Manager),
                value => ArcaneUiStyleService.SetInherits(ArcaneUiModuleSettings.Manager, value));
        }

        private void CustomColorSetting(string label, string channel, string help)
        {
            HexSetting(label, help,
                () => ArcaneUiStyleService.GetCustomColorHex(channel),
                value => ArcaneUiStyleService.SetCustomColorHex(channel, value));
        }

        private void AddTheme(string id, string name, string description)
        {
            UiTheme sample = _themes.GetTheme(id);
            bool selected = Theme.Id == id;
            var row = ArcaneUiControls.Row(_content, "Theme_" + id, 67, Card);
            ArcaneUiControls.Panel(row, "Swatch", 14, 11, 44, 42, sample.Background, false);
            ArcaneUiControls.Panel(row, "SwatchAccent", 14, 48, 44, 5, sample.Accent, false);
            ArcaneUiControls.Label(row, "Name", name, 72, 3, 515, 26, 13, Theme.Text,
                TextAnchor.MiddleLeft, FontStyle.Bold);
            ArcaneUiControls.Label(row, "Description", description, 72, 28, 520, 26, 11, Muted);
            ArcaneUiControls.Button(row, "Select", selected ? "ACTIVE" : "APPLY", 654, 18, 118, 31,
                selected ? Theme.WidgetBorder : WidgetButton(true), Color.white,
                () =>
                {
                    if (_themes.SetTheme(id)) ArcaneUiPreferences.ThemeId = id;
                }, 12);
        }

        private bool GetClassHealthColours(bool party)
        {
            string module = party ? ArcaneUiModuleSettings.Party : ArcaneUiModuleSettings.Raid;
            bool legacy = party ? PartyFramesSettings.UseClassColors : RaidFramesSettings.UseClassColors;
            return ArcaneUiStyleService.EffectiveClassColors(module, legacy);
        }

        private void SetClassHealthColours(bool party, bool enabled)
        {
            string module = party ? ArcaneUiModuleSettings.Party : ArcaneUiModuleSettings.Raid;
            if (ArcaneUiStyleService.UsesPresetPalette(module))
                ArcaneUiStyleService.SetThemeClassColors(module, enabled);
            else if (party)
                PartyFramesSettings.SetUseClassColors(enabled);
            else
                RaidFramesSettings.SetUseClassColors(enabled);
        }

        // Native player-stat unit frame (Phase 6). Settings never modify
        // Erenshor's Stats or PlayerControl and are independent from Group Frames.
        private void RenderFOTFResourceOptions()
        { RenderFOTFResourceOptionsFull(); }

        private void RenderPlayer()
        {
            if (_tab == "General")
            {
                Section("PLAYER FRAME", "Customize your character frame. Changes save automatically.");
                ModuleToggle(ArcaneUiModuleSettings.Player, "Enable Player Frame",
                    "OFF restores the existing native player HUD visual and interactions.");
                ToggleSetting("Show Mana Bar", "Display mana under the health bar.",
                    () => PlayerFrameSettings.ShowMana, v => { PlayerFrameSettings.SetShowMana(v); RefreshPlayerLayout(); });
                ToggleSetting("Show Health Numbers", "Display HP as current / maximum.",
                    () => PlayerFrameSettings.ShowNumbers, v => PlayerFrameSettings.SetShowNumbers(v));
                ToggleSetting("Show HP Percent", "Display the health percentage in the frame.",
                    () => PlayerFrameSettings.ShowPercent, v => PlayerFrameSettings.SetShowPercent(v));
                ToggleSetting("Show Mana Percent", "Show a separate mana percentage on the left of the mana bar.",
                    () => PlayerFrameSettings.ShowManaPercent, v => PlayerFrameSettings.SetShowManaPercent(v));
                ToggleSetting("Show Level", "Display the player level in its own text tag.",
                    () => PlayerFrameSettings.ShowLevel, v => PlayerFrameSettings.SetShowLevel(v));
                ToggleSetting("Show Class", "Display the class name independently of level.",
                    () => PlayerFrameSettings.ShowClass, v => PlayerFrameSettings.SetShowClass(v));
                ToggleSetting("Only Show In Combat", "Hide only when a readable native combat flag confirms out of combat.",
                    () => PlayerFrameSettings.ShowOnlyInCombat, v => PlayerFrameSettings.SetShowOnlyInCombat(v));
                ToggleSetting("Hide Original Player Frame", "Show only ArcaneUI's player frame when it is ready. The original returns automatically if ArcaneUI is disabled.",
                    () => NativePlayerHudBridge.SuppressNative, NativePlayerHudBridge.SetSuppress);
                ActionButton("Layout editor", "Move and resize the Player Frame alongside Party/Raid frames.",
                    "UNLOCK UI", _openLayoutEditor);
            }
            else if (_tab == "FOTF Resources") RenderFOTFResourceOptions();
            else if (_tab == "Advanced") RenderAdvancedFrame();
            else if (_tab == "Appearance")
            {
                Section("APPEARANCE", "Changes apply immediately, including Blank / Custom theme settings.");
                ToggleSetting("Use Global Theme", "Follow the chosen ArcaneUI colour palette, font and texture.",
                    () => PlayerFrameSettings.InheritTheme,
                    v => { PlayerFrameSettings.SetInheritTheme(v); RefreshPlayerStyle(); });
                ToggleSetting("Class-Coloured Health", "OFF uses the selected theme health colour.",
                    () => PlayerFrameSettings.UseClassColours,
                    v => { PlayerFrameSettings.SetUseClassColours(v); RefreshPlayerStyle(); });
                SliderSetting("Frame Width", 130, 450, () => PlayerFrameSettings.Width,
                    v => { PlayerFrameSettings.SetSize(v, PlayerFrameSettings.Height); RefreshPlayerLayout(); }, " px");
                SliderSetting("Frame Height", 42, 130, () => PlayerFrameSettings.Height,
                    v => { PlayerFrameSettings.SetSize(PlayerFrameSettings.Width, v); RefreshPlayerLayout(); }, " px");
                FrameBarDirectionSetting(ArcaneUiModuleSettings.Player);
                SliderSetting("Mana Bar Height", 10, 65,
                    () => ArcaneUiBarLayout.ManaPercent(ArcaneUiModuleSettings.Player),
                    value => { ArcaneUiBarLayout.SetManaPercent(ArcaneUiModuleSettings.Player, value);
                        RefreshPlayerLayout(); }, "%");
                ChoiceSetting("Bar Texture", "Manual override; Global follows the theme.",
                    string.IsNullOrEmpty(PlayerFrameSettings.Texture) ? "Global" : PlayerFrameSettings.Texture,
                    () => { PlayerFrameSettings.SetTexture(string.IsNullOrEmpty(PlayerFrameSettings.Texture)
                        ? UnitFrameAppearance.TextureNames[0] : UnitFrameAppearance.NextTexture(PlayerFrameSettings.Texture)); RefreshPlayerStyle(); });
                ActionButton("Reset Texture Override", "Return to the selected global theme texture.",
                    "GLOBAL", () => { PlayerFrameSettings.SetTexture(""); RefreshPlayerStyle(); RenderPage(); });
                ChoiceSetting("Font", "Manual override; Global follows the theme.",
                    string.IsNullOrEmpty(PlayerFrameSettings.Font) ? "Global" : PlayerFrameSettings.Font,
                    () => { PlayerFrameSettings.SetFont(string.IsNullOrEmpty(PlayerFrameSettings.Font)
                        ? UnitFrameAppearance.FontNames[0] : UnitFrameAppearance.NextFont(PlayerFrameSettings.Font)); RefreshPlayerStyle(); });
                ActionButton("Reset Font Override", "Return to the selected global font.",
                    "GLOBAL", () => { PlayerFrameSettings.SetFont(""); RefreshPlayerStyle(); RenderPage(); });
                Section("LOCAL COLOURS", "These apply when global styling is off, or Blank uses no global palette.");
                HexSetting("Player Health Colour", "Hex RGB health fill.",
                    () => PlayerFrameSettings.HealthHex, v => { PlayerFrameSettings.SetHealthHex(v); RefreshPlayerStyle(); });
                HexSetting("Player Mana Colour", "Hex RGB mana fill.",
                    () => PlayerFrameSettings.ManaHex, v => { PlayerFrameSettings.SetManaHex(v); RefreshPlayerStyle(); });
                HexSetting("Player Background", "Hex RGB empty-health and border colour.",
                    () => PlayerFrameSettings.BackgroundHex, v => { PlayerFrameSettings.SetBackgroundHex(v); RefreshPlayerStyle(); });
            }
            else if (_tab == "Text")
            {
                Section("PLAYER FRAME TEXT", "Each tag has independent size, border style and X/Y position.");
                NameLengthSetting(ArcaneUiModuleSettings.Player);
                TextTagSetting(ArcaneUiModuleSettings.Player, "Name", "name", 13);
                TextTagSetting(ArcaneUiModuleSettings.Player, "Level", "level", 11);
                TextTagSetting(ArcaneUiModuleSettings.Player, "Class", "class", 11);
                TextTagSetting(ArcaneUiModuleSettings.Player, "Raw Health", "health", 12);
                TextTagSetting(ArcaneUiModuleSettings.Player, "HP Percentage", "percent", 12);
                TextTagSetting(ArcaneUiModuleSettings.Player, "Mana Numbers", "mana", 10);
                TextTagSetting(ArcaneUiModuleSettings.Player, "Mana Percentage", "mana-percent", 10);
                TextTagSetting(ArcaneUiModuleSettings.Player, "Cast Name", "cast-name", 10);
                TextTagSetting(ArcaneUiModuleSettings.Player, "Cast Time", "cast-time", 10);
            }
            else if (_tab == "Casting")
            {
                Section("NATIVE CAST BAR VISIBILITY", "Keep only ArcaneUI's cast bar on screen.");
                NativeCastVisibilityToggle();
                Section("PLAYER CAST BAR", "Overlay Mana is the default: the frame stays the same size while you cast.");
                ToggleSetting("Show Player Cast Bar", "Display the native-confirmed player cast; the mana bar returns after casting ends.",
                    () => PlayerFrameSettings.ShowCastBar, v => PlayerFrameSettings.SetShowCastBar(v));
                ChoiceSetting("Player Cast Bar Placement", "Overlay Mana / Above Health / Below Frame / Detached.",
                    PlayerFrameSettings.CastPlacement, PlayerFrameSettings.CycleCastPlacement);
                HexSetting("Player Cast Bar Colour", "Theme accent by default, or pick an individual player casting colour.",
                    () => string.IsNullOrEmpty(PlayerFrameSettings.CastColourHex)
                        ? "#C69338" : PlayerFrameSettings.CastColourHex,
                    v => { PlayerFrameSettings.SetCastColour(v); RefreshPlayerStyle(); });
                ActionButton("Reset Player Cast Bar Colour", "Restore the current theme's accent colour.", "THEME DEFAULT",
                    () => { PlayerFrameSettings.ResetCastColour(); RefreshPlayerStyle(); RenderPage(); });
                SliderSetting("Player Cast Bar X Position", -250, 250,
                    () => PlayerFrameSettings.CastOffsetX, PlayerFrameSettings.SetCastOffsetX, " px");
                SliderSetting("Player Cast Bar Y Position", -250, 250,
                    () => PlayerFrameSettings.CastOffsetY, PlayerFrameSettings.SetCastOffsetY, " px");
                Info("Live casting progress", "Tracks the native StartSpell timestamp against " +
                    "SpellChargeTime / 60 for a nominal progress bar and countdown. " +
                    "Haste-modified timing needs in-game verification; unknown durations show an activity strip.");
            }
            else if (_tab == "Native HUD")
            {
                Section("NATIVE PLAYER HUD", "Automatic reversible suppression of the confirmed game UI root.");
                Info("Verified native visual", NativePlayerHudBridge.ConfirmedPath);
                ToggleSetting("Hide Native Player HUD", "ON hides only PlayerLifePar while ArcaneUI Player Frame is enabled. OFF shows both frames.",
                    () => NativePlayerHudBridge.SuppressNative,
                    v => { NativePlayerHudBridge.SetSuppress(v);
                        if (Plugin.Instance != null && Plugin.Instance.UiRuntime != null &&
                            Plugin.Instance.UiRuntime.PlayerFrame != null)
                            Plugin.Instance.UiRuntime.PlayerFrame.Update(); });
                Info("Native gameplay retained", "The player HUD is hidden using CanvasGroup. Erenshor's health, mana, targeting and gameplay scripts keep running.");
                Section("NATIVE CAST BAR", "The native cast bar is a separate visual from the health/mana HUD.");
                NativeCastVisibilityToggle();
                Info("Native cast bar status", NativeCastBarBridge.Status);
            }
        }
        private static void RefreshPlayerLayout()
        {
            if (Plugin.Instance != null && Plugin.Instance.UiRuntime != null &&
                Plugin.Instance.UiRuntime.PlayerFrame != null)
                Plugin.Instance.UiRuntime.PlayerFrame.RefreshLayout();
        }
        private static void RefreshPlayerStyle()
        {
            if (Plugin.Instance != null && Plugin.Instance.UiRuntime != null &&
                Plugin.Instance.UiRuntime.PlayerFrame != null)
                Plugin.Instance.UiRuntime.PlayerFrame.RefreshStyle();
        }

        private void RenderFrames()
        {
            bool party = _page == "party";
            string label = party ? "Party" : "Raid";
            if (_tab == "General")
            {
                Section("MODULE", "Turn off this replacement to return to Erenshor's native frames.");
                ModuleToggle(party ? ArcaneUiModuleSettings.Party : ArcaneUiModuleSettings.Raid,
                    "Enable " + label + " Frames",
                    party ? "OFF restores native party-member slots." :
                        "OFF restores the native raid-member list.");
                Section("DISPLAY & BEHAVIOUR", "Changes apply immediately when this module is enabled.");
                ToggleSetting("Unlock Frames", "Enable the existing drag handles for repositioning.",
                    () => party ? !PartyFramesSettings.Locked : !RaidFramesSettings.Locked,
                    value =>
                    {
                        if (party) PartyFramesUI.SetLocked(!value);
                        else RaidFramesUI.SetLocked(!value);
                    });
                ToggleSetting("Show HP Percent", "Show each unit's health percentage.",
                    () => party ? PartyFramesSettings.ShowHpPercent : RaidFramesSettings.ShowHpPercent,
                    value => { if (party) PartyFramesSettings.SetShowHpPercent(value);
                        else RaidFramesSettings.SetShowHpPercent(value); });
                ToggleSetting("Show Raw Health", "Display numeric health values.",
                    () => party ? PartyFramesSettings.ShowRawHealth : RaidFramesSettings.ShowRawHealth,
                    value => { if (party) PartyFramesSettings.SetShowRawHealth(value);
                        else RaidFramesSettings.SetShowRawHealth(value); });
                ToggleSetting("Class-Coloured Health", "OFF uses the selected theme's health colour (when themed).",
                    () => GetClassHealthColours(party),
                    value => SetClassHealthColours(party, value));
                ToggleSetting("Show Mana Bars", "Display power bars beneath unit frames.",
                    () => party ? PartyFramesSettings.ShowManaBars : RaidFramesSettings.ShowManaBars,
                    value => { if (party) { PartyFramesSettings.SetShowManaBars(value); }
                        else { RaidFramesSettings.SetShowManaBars(value); } });
                ToggleSetting("Mouseover Casting", "Allow supported beneficial spells on hovered frames.",
                    () => party ? PartyFramesSettings.EnableMouseoverCasting : RaidFramesSettings.EnableMouseoverCasting,
                    value => { if (party) PartyFramesSettings.SetEnableMouseoverCasting(value);
                        else RaidFramesSettings.SetEnableMouseoverCasting(value); });
                ToggleSetting("Target Highlight", "Highlight your current target in the frame list.",
                    () => party ? PartyFramesSettings.ShowTargetHighlight : RaidFramesSettings.ShowTargetHighlight,
                    value => { if (party) PartyFramesSettings.SetShowTargetHighlight(value);
                        else RaidFramesSettings.SetShowTargetHighlight(value); });
                ToggleSetting("Aggro Highlight", "Highlight units that are attracting enemy aggression.",
                    () => party ? PartyFramesSettings.ShowAggroHighlight : RaidFramesSettings.ShowAggroHighlight,
                    value => { if (party) PartyFramesSettings.SetShowAggroHighlight(value);
                        else RaidFramesSettings.SetShowAggroHighlight(value); });
                ToggleSetting("Status Icons", "Keep native-status information visible on unit frames.",
                    () => party ? PartyFramesSettings.ShowStatusIcons : RaidFramesSettings.ShowStatusIcons,
                    value => { if (party) PartyFramesSettings.SetShowStatusIcons(value);
                        else RaidFramesSettings.SetShowStatusIcons(value); });
                Section("RESET");
                ActionButton("Reset " + label + " Defaults", "Restore the saved default values of this frame module.",
                    "RESET DEFAULTS", () =>
                    {
                        if (party) { PartyFramesSettings.ResetDefaults(); PartyFramesUI.SettingsChanged(); }
                        else { RaidFramesSettings.ResetDefaults(); RaidFramesUI.SettingsChanged(); }
                        string module = party ? ArcaneUiModuleSettings.Party : ArcaneUiModuleSettings.Raid;
                        ArcaneUiStyleService.SetModuleTextureOverride(module, "");
                        ArcaneUiStyleService.SetModuleFontOverride(module, "");
                        RenderPage();
                    });
            }
            else if (_tab == "Advanced") RenderAdvancedFrame();
            else if (_tab == "Appearance")
            {
                string frameModule = party ? ArcaneUiModuleSettings.Party : ArcaneUiModuleSettings.Raid;
                Section("SHARED APPEARANCE", "Global themes override the old class colours by default.");
                ToggleSetting("Use Global Theme", "Presets style colours and panels; manual texture/font choices take priority. Blank keeps local styling.",
                    () => ArcaneUiStyleService.Inherits(frameModule),
                    value => ArcaneUiStyleService.SetInherits(frameModule, value));
                Section("SIZE & SPACING", "Drag to adjust. The live preview stays visible above the list.");
                SliderSetting("Frame Width", 90, 240,
                    () => party ? PartyFramesSettings.FrameWidth : RaidFramesSettings.FrameWidth,
                    v => { if (party) { PartyFramesSettings.SetFrameWidth(v); }
                        else { RaidFramesSettings.SetFrameWidth(v); } }, " px");
                SliderSetting("Frame Height", 24, 70,
                    () => party ? PartyFramesSettings.FrameHeight : RaidFramesSettings.FrameHeight,
                    v => { if (party) { PartyFramesSettings.SetFrameHeight(v); }
                        else { RaidFramesSettings.SetFrameHeight(v); } }, " px");
                SliderSetting("Mana Bar Height", 10, 65,
                    () => ArcaneUiBarLayout.ManaPercent(frameModule),
                    value => ArcaneUiBarLayout.SetManaPercent(frameModule, value), "%");
                SliderSetting("Member Spacing", 0, 10,
                    () => party ? PartyFramesSettings.MemberSpacing : RaidFramesSettings.MemberSpacing,
                    v => { if (party) { PartyFramesSettings.SetMemberSpacing(v); }
                        else { RaidFramesSettings.SetMemberSpacing(v); } }, " px");
                if (!party)
                    SliderSetting("Group Spacing", 0, 25,
                        () => RaidFramesSettings.GroupSpacing,
                        v => { RaidFramesSettings.SetGroupSpacing(v); }, " px");
                ChoiceSetting("Member Growth Direction", party
                        ? "Horizontal adds members to the right; vertical adds them below."
                        : "Raid: horizontal = members across, groups stacked downward; vertical = members down, groups side by side.",
                    (party ? PartyFramesSettings.GrowMembersHorizontally : RaidFramesSettings.GrowMembersHorizontally)
                        ? "Horizontal (Right)" : "Vertical (Down)",
                    () =>
                    {
                        if (party)
                        {
                            PartyFramesSettings.SetGrowMembersHorizontally(!PartyFramesSettings.GrowMembersHorizontally);
                            PartyFramesUI.SettingsChanged();
                        }
                        else
                        {
                            RaidFramesSettings.SetGrowMembersHorizontally(!RaidFramesSettings.GrowMembersHorizontally);
                            RaidFramesUI.SettingsChanged();
                        }
                    });
                Section("HIGHLIGHT THICKNESS");
                SliderSetting("Target Highlight Thickness", 1, 6,
                    () => party ? PartyFramesSettings.TargetHighlightThickness : RaidFramesSettings.TargetHighlightThickness,
                    v => { if (party) PartyFramesSettings.SetTargetHighlightThickness(v);
                        else RaidFramesSettings.SetTargetHighlightThickness(v); }, " px");
                SliderSetting("Aggro Highlight Thickness", 1, 6,
                    () => party ? PartyFramesSettings.AggroHighlightThickness : RaidFramesSettings.AggroHighlightThickness,
                    v => { if (party) PartyFramesSettings.SetAggroHighlightThickness(v);
                        else RaidFramesSettings.SetAggroHighlightThickness(v); }, " px");
                Section("HEALTH & MANA FILL", "Set health and mana fill directions independently for these " + label.ToLowerInvariant() + " frames.");
                FrameBarDirectionSetting(frameModule);
                Section("BARS & COLOURS", "Theme health is the default; enable class colours explicitly if desired.");
                ModuleAppearanceChoice("Bar Texture", "Choose an individual bar texture or follow the global style.",
                    ArcaneUiStyleService.EffectiveTexture(frameModule,
                        party ? PartyFramesSettings.BarTexture : RaidFramesSettings.BarTexture),
                    ArcaneUiStyleService.ModuleTextureOverride(frameModule),
                    () =>
                    {
                        string current = ArcaneUiStyleService.EffectiveTexture(frameModule,
                            party ? PartyFramesSettings.BarTexture : RaidFramesSettings.BarTexture);
                        string next = UnitFrameAppearance.NextTexture(current);
                        // Keep the original setting synchronized so Blank and
                        // opt-out mode also show the texture just selected.
                        if (party) PartyFramesSettings.SetBarTexture(next);
                        else RaidFramesSettings.SetBarTexture(next);
                        ArcaneUiStyleService.SetModuleTextureOverride(frameModule, next);
                    },
                    () => ArcaneUiStyleService.SetModuleTextureOverride(frameModule, ""),
                    value =>
                    {
                        if (party) PartyFramesSettings.SetBarTexture(value);
                        else RaidFramesSettings.SetBarTexture(value);
                        ArcaneUiStyleService.SetModuleTextureOverride(frameModule, value);
                    });
                ToggleSetting("Class Health Colours", "OFF: theme health when themed; static health when using local style.",
                    () => GetClassHealthColours(party),
                    value => SetClassHealthColours(party, value));
                HexSetting("Static Health Colour", "Hex #RRGGBB, used if class colours are disabled.",
                    () => party ? PartyFramesSettings.StaticHealthColorHex : RaidFramesSettings.StaticHealthColorHex,
                    value => { if (party) PartyFramesSettings.SetStaticHealthColorHex(value);
                        else RaidFramesSettings.SetStaticHealthColorHex(value); });
                HexSetting("Missing Health Colour", "Hex #RRGGBB for empty health / background.",
                    () => party ? PartyFramesSettings.MissingHealthColorHex : RaidFramesSettings.MissingHealthColorHex,
                    value => { if (party) PartyFramesSettings.SetMissingHealthColorHex(value);
                        else RaidFramesSettings.SetMissingHealthColorHex(value); });
                Section("TEXT");
                ModuleAppearanceChoice("Font", "Choose an individual font or follow the global style.",
                    ArcaneUiStyleService.EffectiveFont(frameModule,
                        party ? PartyFramesSettings.FontName : RaidFramesSettings.FontName),
                    ArcaneUiStyleService.ModuleFontOverride(frameModule),
                    () =>
                    {
                        string current = ArcaneUiStyleService.EffectiveFont(frameModule,
                            party ? PartyFramesSettings.FontName : RaidFramesSettings.FontName);
                        string next = UnitFrameAppearance.NextFont(current);
                        if (party) PartyFramesSettings.SetFontName(next);
                        else RaidFramesSettings.SetFontName(next);
                        ArcaneUiStyleService.SetModuleFontOverride(frameModule, next);
                    },
                    () => ArcaneUiStyleService.SetModuleFontOverride(frameModule, ""),
                    value =>
                    {
                        if (party) PartyFramesSettings.SetFontName(value);
                        else RaidFramesSettings.SetFontName(value);
                        ArcaneUiStyleService.SetModuleFontOverride(frameModule, value);
                    });

            }
            else if (_tab == "Text")
            {
                string module = party ? ArcaneUiModuleSettings.Party : ArcaneUiModuleSettings.Raid;
                Section(label.ToUpperInvariant() + " FRAME TEXT", "Font sizes and borders are independent for each tag.");
                NameLengthSetting(module);
                TextTagSetting(module, "Name", "name", party ? PartyFramesSettings.NameFontSize : RaidFramesSettings.NameFontSize);
                TextTagSetting(module, "HP Percentage", "percent", party ? PartyFramesSettings.HpPercentFontSize : RaidFramesSettings.HpPercentFontSize);
                TextTagSetting(module, "Raw Health", "health", party ? PartyFramesSettings.RawHpFontSize : RaidFramesSettings.RawHpFontSize);
            }
            else if (_tab == "Click Casting")
            {
                RenderClickCastingBindings(party);
            }
        }

        // Initial Target Frame implementation. Uses the shared view, tag styling
        // and native-visual bridge; casting/buffs remain future native work.
        private void RenderTarget()
        {
            if (_tab == "General")
            {
                Section("TARGET FRAME", "Customize the selected target's frame.");
                ModuleToggle(ArcaneUiModuleSettings.Target, "Enable Target Frame",
                    "Use ArcaneUI's target frame. Turning it off restores the original.");
                ToggleSetting("Hide Original Target Frame", "Hide the game's original target window only while ArcaneUI can draw its replacement.",
                    () => NativeTargetHudBridge.SuppressNative,
                    v => { NativeTargetHudBridge.SetSuppress(v); RefreshTargetLayout(); });
                ToggleSetting("Show Mana Bar", "Display the target mana bar.",
                    () => TargetFrameSettings.ShowMana, v => { TargetFrameSettings.SetShowMana(v); RefreshTargetLayout(); });
                ToggleSetting("Show Health Numbers", "Display current and maximum HP.",
                    () => TargetFrameSettings.ShowNumbers, v => TargetFrameSettings.SetShowNumbers(v));
                ToggleSetting("Show HP Percent", "Display the target health percentage.",
                    () => TargetFrameSettings.ShowPercent, v => TargetFrameSettings.SetShowPercent(v));
                ToggleSetting("Show Mana Percent", "Display target mana percentage.",
                    () => TargetFrameSettings.ShowManaPercent, v => TargetFrameSettings.SetShowManaPercent(v));
                ToggleSetting("Show Level", "Display the target level.",
                    () => TargetFrameSettings.ShowLevel, v => TargetFrameSettings.SetShowLevel(v));
                ToggleSetting("Show Class", "Display player/SimPlayer class, when available.",
                    () => TargetFrameSettings.ShowClass, v => TargetFrameSettings.SetShowClass(v));
                ToggleSetting("Hide When No Target", "Show a compact empty frame only when this is turned off.",
                    () => TargetFrameSettings.HideWhenNoTarget, v => TargetFrameSettings.SetHideWhenNoTarget(v));
                ToggleSetting("Only Show In Combat", "Hide when native target combat state is definitely false; leave visible if unavailable.",
                    () => TargetFrameSettings.ShowOnlyInCombat, v => TargetFrameSettings.SetShowOnlyInCombat(v));
                ActionButton("Layout editor", "Move and resize the Target Frame independently.",
                    "UNLOCK UI", _openLayoutEditor);
                Info("Native safety", "Buffs use native StatusEffects. Cast data displays only when verified spell and timing values are available; native context interactions are not replaced.");
            }
            else if (_tab == "Advanced") RenderAdvancedFrame();
            else if (_tab == "Appearance")
            {
                Section("TARGET APPEARANCE", "Manual overrides take priority over the selected theme.");
                ToggleSetting("Use Global Theme", "Follow the ArcaneUI theme palette.",
                    () => TargetFrameSettings.InheritTheme, v => { TargetFrameSettings.SetInheritTheme(v); RefreshTargetStyle(); });
                Info("Automatic Target Colours",
                    "SimPlayers use their class colour. Friendly NPCs use green. Other NPCs use the target health colour (theme or local).");
                SliderSetting("Frame Width", 130, 450, () => TargetFrameSettings.Width,
                    v => { TargetFrameSettings.SetSize(v, TargetFrameSettings.Height); RefreshTargetLayout(); }, " px");
                SliderSetting("Frame Height", 42, 130, () => TargetFrameSettings.Height,
                    v => { TargetFrameSettings.SetSize(TargetFrameSettings.Width, v); RefreshTargetLayout(); }, " px");
                FrameBarDirectionSetting(ArcaneUiModuleSettings.Target);
                SliderSetting("Mana Bar Height", 10, 65,
                    () => ArcaneUiBarLayout.ManaPercent(ArcaneUiModuleSettings.Target),
                    v => { ArcaneUiBarLayout.SetManaPercent(ArcaneUiModuleSettings.Target, v); RefreshTargetLayout(); }, "%");
                ChoiceSetting("Bar Texture", "Manual override; Global follows the theme.",
                    string.IsNullOrEmpty(TargetFrameSettings.Texture) ? "Global" : TargetFrameSettings.Texture,
                    () => { TargetFrameSettings.SetTexture(string.IsNullOrEmpty(TargetFrameSettings.Texture)
                        ? UnitFrameAppearance.TextureNames[0] : UnitFrameAppearance.NextTexture(TargetFrameSettings.Texture)); RefreshTargetStyle(); });
                ActionButton("Reset Texture Override", "Use the global texture instead.",
                    "GLOBAL", () => { TargetFrameSettings.SetTexture(""); RefreshTargetStyle(); RenderPage(); });
                ChoiceSetting("Font", "Manual override; Global follows the theme.",
                    string.IsNullOrEmpty(TargetFrameSettings.Font) ? "Global" : TargetFrameSettings.Font,
                    () => { TargetFrameSettings.SetFont(string.IsNullOrEmpty(TargetFrameSettings.Font)
                        ? UnitFrameAppearance.FontNames[0] : UnitFrameAppearance.NextFont(TargetFrameSettings.Font)); RefreshTargetStyle(); });
                ActionButton("Reset Font Override", "Use the global font instead.",
                    "GLOBAL", () => { TargetFrameSettings.SetFont(""); RefreshTargetStyle(); RenderPage(); });
                Section("LOCAL COLOURS", "Used with module theme inheritance disabled or Blank without palette.");
                HexSetting("Target Health Colour", "Hex RGB health fill.",
                    () => TargetFrameSettings.HealthHex, v => { TargetFrameSettings.SetHealthHex(v); RefreshTargetStyle(); });
                HexSetting("Target Mana Colour", "Hex RGB mana fill.",
                    () => TargetFrameSettings.ManaHex, v => { TargetFrameSettings.SetManaHex(v); RefreshTargetStyle(); });
                HexSetting("Target Background", "Hex RGB empty-health and border colour.",
                    () => TargetFrameSettings.BackgroundHex, v => { TargetFrameSettings.SetBackgroundHex(v); RefreshTargetStyle(); });
            }
            else if (_tab == "Text")
            {
                Section("TARGET TEXT TAGS", "Each tag has its own size, outline and X/Y position.");
                NameLengthSetting(ArcaneUiModuleSettings.Target);
                TextTagSetting(ArcaneUiModuleSettings.Target, "Cast Name", "cast-name", 10);
                TextTagSetting(ArcaneUiModuleSettings.Target, "Cast Time", "cast-time", 10);
                TextTagSetting(ArcaneUiModuleSettings.Target, "Name", "name", 13);
                TextTagSetting(ArcaneUiModuleSettings.Target, "Level", "level", 11);
                TextTagSetting(ArcaneUiModuleSettings.Target, "Class", "class", 11);
                TextTagSetting(ArcaneUiModuleSettings.Target, "Raw Health", "health", 12);
                TextTagSetting(ArcaneUiModuleSettings.Target, "HP Percentage", "percent", 12);
                TextTagSetting(ArcaneUiModuleSettings.Target, "Mana Numbers", "mana", 10);
                TextTagSetting(ArcaneUiModuleSettings.Target, "Mana Percentage", "mana-percent", 10);
            }
            else if (_tab == "Effects")
            {
                Section("TARGET BUFFS AND DEBUFFS", "Read native StatusEffects; icon display never changes spells or effects.");
                ToggleSetting("Show Target Buffs", "Display positive effects on the current target.",
                    () => TargetFrameSettings.ShowBuffs, v => TargetFrameSettings.SetShowBuffs(v));
                ToggleSetting("Show Target Debuffs", "Display negative effects on the current target.",
                    () => TargetFrameSettings.ShowDebuffs, v => TargetFrameSettings.SetShowDebuffs(v));
                SliderSetting("Aura Icon Size", 14, 48, () => TargetFrameSettings.AuraSize,
                    v => TargetFrameSettings.SetAuraSize(v), " px");
                SliderSetting("Aura Spacing", 0, 14, () => TargetFrameSettings.AuraSpacing,
                    v => TargetFrameSettings.SetAuraSpacing(v), " px");
                ChoiceSetting("Aura Placement", "Above, below, left or right of the target frame.",
                    TargetFrameSettings.AuraPlacement, TargetFrameSettings.CycleAuraPlacement);
            }
            else if (_tab == "Casting")
            {
                Section("TARGET CAST BAR", "Replaces the visible mana strip while casting by default.");
                ToggleSetting("Show Cast Bar", "Show verified native casts; use the mana strip as a cast bar with time when available.",
                    () => TargetFrameSettings.ShowCastBar, v => TargetFrameSettings.SetShowCastBar(v));
                ChoiceSetting("Cast Bar Placement", "Overlay Mana / Above Health / Below Frame / Detached.",
                    TargetFrameSettings.CastPlacement, TargetFrameSettings.CycleCastPlacement);
                HexSetting("Cast Bar Colour", "Theme accent by default; choose any custom RGB colour.",
                    () => string.IsNullOrEmpty(TargetFrameSettings.CastColourHex)
                        ? "#C69338" : TargetFrameSettings.CastColourHex,
                    v => { TargetFrameSettings.SetCastColour(v); RefreshTargetStyle(); });
                ActionButton("Reset Cast Bar Colour", "Use theme accent colour again.", "THEME DEFAULT",
                    () => { TargetFrameSettings.ResetCastColour(); RefreshTargetStyle(); RenderPage(); });
                SliderSetting("Cast Bar X Position", -250, 250, () => TargetFrameSettings.CastOffsetX,
                    TargetFrameSettings.SetCastOffsetX, " px");
                SliderSetting("Cast Bar Y Position", -250, 250, () => TargetFrameSettings.CastOffsetY,
                    TargetFrameSettings.SetCastOffsetY, " px");
                Info("Live casting progress", "Tracks native isCasting() and the StartSpell timestamp. " +
                    "A SpellChargeTime / 60 timeline animates the bar and countdown; " +
                    "haste timing still needs verification.");
            }
            else if (_tab == "Native HUD")
            {
                Section("NATIVE TARGET VISUAL", "Candidate path from the live target UI scanner.");
                Info("Candidate path", NativeTargetHudBridge.ConfirmedPath);
                Info("Binding", NativeTargetHudBridge.IsBound ? "Native NewTargetWindow is bound." : "Not bound. Select a target and enable this hide switch to test it.");
                ToggleSetting("Hide Native Target HUD", "EXPERIMENTAL: ON hides NewTargetWindow while ArcaneUI can render target stats. OFF keeps both visible.",
                    () => NativeTargetHudBridge.SuppressNative,
                    v => { NativeTargetHudBridge.SetSuppress(v);
                        if (Plugin.Instance != null && Plugin.Instance.UiRuntime != null &&
                            Plugin.Instance.UiRuntime.TargetFrame != null)
                            Plugin.Instance.UiRuntime.TargetFrame.Update(); });
                Info("Safe recovery", "Disabling the Target Frame module immediately restores the original CanvasGroup state. No native gameplay component is disabled.");
                ActionButton("Inspect UI hierarchy", "Review the previously discovered native target object paths.",
                    "SCAN UI", () => SelectPage("unit"));
            }
        }

        private static void RefreshTargetLayout()
        {
            if (Plugin.Instance != null && Plugin.Instance.UiRuntime != null &&
                Plugin.Instance.UiRuntime.TargetFrame != null)
                Plugin.Instance.UiRuntime.TargetFrame.RefreshLayout();
        }
        private static void RefreshTargetStyle()
        {
            if (Plugin.Instance != null && Plugin.Instance.UiRuntime != null &&
                Plugin.Instance.UiRuntime.TargetFrame != null)
                Plugin.Instance.UiRuntime.TargetFrame.RefreshStyle();
        }

        private void RenderTargetPreview()
        {
            ArcaneUiControls.ClearChildren(_previewHost);
            _targetPreview = new ArcaneUiUnitFrameView(_previewHost, "TargetPreview", ArcaneUiModuleSettings.Target);
            _targetPreview.Root.anchoredPosition = Vector2.zero;
            LinkTargetPreviewGraphic("HealthBackground", "Appearance", "Target Health Colour");
            LinkTargetPreviewGraphic("ManaBackground", "Appearance", "Mana Bar Height");
            LinkTargetPreviewGraphic("Name", "Text", "Name Size");
            LinkTargetPreviewGraphic("Level", "Text", "Level Size");
            LinkTargetPreviewGraphic("Class", "Text", "Class Size");
            LinkTargetPreviewGraphic("HealthNumbers", "Text", "Raw Health Size");
            LinkTargetPreviewGraphic("HealthPercent", "Text", "HP Percentage Size");
            LinkTargetPreviewGraphic("ManaNumbers", "Text", "Mana Numbers Size");
            LinkTargetPreviewGraphic("ManaPercent", "Text", "Mana Percentage Size");
            LinkTargetPreviewGraphic("CastBackground", "Casting", "Cast Bar Placement");
            LinkTargetPreviewGraphic("CastName", "Text", "Cast Name Size");
            LinkTargetPreviewGraphic("CastTime", "Text", "Cast Time Size");
            if (_tab == "Effects")
            {
                float auraY = Mathf.Min(96f, TargetFrameSettings.Height) + 11f;
                var buff = ArcaneUiControls.Panel(_targetPreview.Root, "BuffExample", 10, auraY, 25, 25,
                    new Color(.12f, .47f, .26f));
                ArcaneUiControls.Label(buff, "Initial", "B", 1, 0, 23, 23, 11, Color.white, TextAnchor.MiddleCenter);
                LinkPreview(buff.GetComponent<Image>(), "Effects", "Aura Icon Size");
                var debuff = ArcaneUiControls.Panel(_targetPreview.Root, "DebuffExample", 41, auraY, 25, 25,
                    new Color(.62f, .23f, .24f));
                ArcaneUiControls.Label(debuff, "Initial", "D", 1, 0, 23, 23, 11, Color.white, TextAnchor.MiddleCenter);
                LinkPreview(debuff.GetComponent<Image>(), "Effects", "Aura Placement");
            }
            UpdateTargetPreview();
        }
        private void LinkTargetPreviewGraphic(string name, string tab, string setting)
        {
            if (_targetPreview == null) return;
            var child = _targetPreview.Root.Find(name);
            if (child == null) child = _targetPreview.Root.Find("CastBackground/" + name);
            if (child != null) LinkPreview(child.GetComponent<Graphic>(), tab, setting);
        }
        private void UpdateTargetPreview()
        {
            if (_targetPreview == null) return;
            _targetPreview.SetGeometry(Mathf.Min(455f, TargetFrameSettings.Width),
                Mathf.Min(96f, TargetFrameSettings.Height), TargetFrameSettings.ShowMana);
            ArcaneUiFrameStyle style = ArcaneUiStyleService.FrameStyle(ArcaneUiModuleSettings.Target);
            if (!TargetFrameSettings.InheritTheme ||
                (ArcaneUiStyleService.IsBlankTheme && !ArcaneUiStyleService.BlankPaletteEnabled))
            {
                style.Health = UnitFrameAppearance.ParseColorOrDefault(TargetFrameSettings.HealthHex,
                    new Color(.36f,.61f,.52f));
                style.Mana = UnitFrameAppearance.ParseColorOrDefault(TargetFrameSettings.ManaHex,
                    new Color(.32f,.49f,.71f));
                style.MissingHealth = UnitFrameAppearance.ParseColorOrDefault(TargetFrameSettings.BackgroundHex,
                    new Color(.10f,.14f,.16f));
                style.ResourceBackground = style.MissingHealth;
                style.Border = style.MissingHealth;
                style.BarTexture = "Flat";
                style.FontName = "Default";
            }
            Character target = GameData.PlayerControl != null ? GameData.PlayerControl.CurrentTarget : null;
            Stats stats = target != null ? target.MyStats : null;
            string name = stats != null && !string.IsNullOrWhiteSpace(stats.MyName) ? stats.MyName : "Example Target";
            bool playerLike = target != null && (!target.isNPC || (target.MyNPC != null && target.MyNPC.SimPlayer));
            bool overrideHealth;
            Color overrideColour;
            TargetHealthColourResolver.Resolve(target, stats, name, out overrideHealth, out overrideColour);
            _targetPreview.SetStyle(style, overrideHealth, overrideColour,
                TargetFrameSettings.Texture, TargetFrameSettings.Font);
            _targetPreview.Render(name, stats != null ? "Lv " + stats.Level : "Lv 35",
                playerLike ? RaidClassColors.ResolveClassNameForActor(stats, name) : "",
                stats != null ? stats.CurrentHP : 800, stats != null ? stats.CurrentMaxHP : 1000,
                stats != null ? stats.CurrentMana : 320, stats != null ? stats.GetCurrentMaxMana() : 400,
                TargetFrameSettings.ShowNumbers, TargetFrameSettings.ShowPercent,
                TargetFrameSettings.ShowLevel, TargetFrameSettings.ShowClass, TargetFrameSettings.ShowManaPercent);
            _targetPreview.ApplyTextStyles();
            _targetPreview.SetCasting(_tab == "Casting", "Example Spell", .62f, 1.4f,
                TargetFrameSettings.CastPlacement, TargetFrameSettings.CastOffsetX, TargetFrameSettings.CastOffsetY);
            FitInteractiveUnitPreview(_targetPreview,
                Mathf.Min(455f, TargetFrameSettings.Width),
                Mathf.Min(96f, TargetFrameSettings.Height));
        }

        // The right-hand preview is a 814x640 logical surface rendered in a
        // 470x372 viewport. Fill that area with the actual clickable frame,
        // rather than leaving it at the old narrow banner's scale.
        private void FitInteractiveUnitPreview(ArcaneUiUnitFrameView preview,
            float width, float height)
        {
            if (preview == null) return;
            float scale = Mathf.Min(3.7f, 735f / Mathf.Max(1f, width),
                310f / Mathf.Max(1f, height));
            scale = Mathf.Max(.7f, scale);
            preview.Root.localScale = Vector3.one * scale;
            preview.Root.anchoredPosition = new Vector2(
                (814f - width * scale) * .5f,
                -(640f - height * scale) * .5f);
        }

        private void RenderPlayerPreview()
        {
            ArcaneUiControls.ClearChildren(_previewHost);
            _playerPreview = new ArcaneUiUnitFrameView(_previewHost, "PlayerPreview");
            _playerPreview.Root.anchoredPosition = Vector2.zero;
            // These exact view components also render the in-game Player Frame.
            LinkPreviewGraphic("HealthBackground", "Appearance", "Class-Coloured Health");
            LinkPreviewGraphic("ManaBackground", "Appearance", "Mana Bar Height");
            LinkPreviewGraphic("Name", "Text", "Name Size");
            LinkPreviewGraphic("Level", "Text", "Level Size");
            LinkPreviewGraphic("Class", "Text", "Class Size");
            LinkPreviewGraphic("HealthNumbers", "Text", "Raw Health Size");
            LinkPreviewGraphic("HealthPercent", "Text", "HP Percentage Size");
            LinkPreviewGraphic("ManaNumbers", "Text", "Mana Numbers Size");
            LinkPreviewGraphic("ManaPercent", "Text", "Mana Percentage Size");
            LinkPreviewGraphic("CastBackground", "Casting", "Player Cast Bar Placement");
            LinkPreviewGraphic("CastName", "Text", "Cast Name Size");
            LinkPreviewGraphic("CastTime", "Text", "Cast Time Size");
            UpdatePlayerPreview();
            if (_page == "fotf" || _tab == "FOTF Resources")
            {
                // Render the same player-frame resource component as the game.
                // Simulated snapshots are for the editor only.
                _fotfPreview = new ArcaneUiFOTFResources(_previewHost, _playerPreview.Root, true);
                UpdateFOTFResourcePreview();
                // FOTF preview is read-only. Its hit areas exist ONLY in the
                // settings preview, never on the live in-game player widget.
                LinkFOTFPreview(_fotfPreview.PrimaryClickArea, "Primary", "Primary Width");
                LinkFOTFPreview(_fotfPreview.SecondaryClickArea, "Secondary", "Secondary Width");
                SetPreviewHint(_contextStatusLabel, "Hover a FOTF resource to highlight; click to edit it.");
            }
        }

        private void RenderFOTFResourcesPreview()
        {
            RenderPlayerPreview();
        }

        private void UpdateFOTFResourcePreview()
        {
            if (_fotfPreview == null || _playerPreview == null) return;
            // Only the three fully implemented FOTF classes are in the active
            // test roster; Emberweaver is the only one with numeric class bars.
            int heat = 6, flashfire = 1;
            ArcaneUiFOTFBridge.Values live;
            if (ArcaneUiFOTFBridge.Read(out live) &&
                string.Equals(live.ClassKey, "Emberweaver", StringComparison.Ordinal))
            {
                heat = live.Primary;
                flashfire = live.Secondary;
            }
            float width = Mathf.Min(455f, PlayerFrameSettings.Width);
            float height = Mathf.Min(96f, PlayerFrameSettings.Height);
            _fotfPreview.UpdatePreview("Emberweaver", heat, 10, flashfire, 1,
                0, 0, width, height);
        }

        private void LinkPreviewGraphic(string name, string tab, string setting)
        {
            if (_playerPreview == null) return;
            var child = _playerPreview.Root.Find(name);
            if (child == null) child = _playerPreview.Root.Find("CastBackground/" + name);
            if (child != null) LinkPreview(child.GetComponent<Graphic>(), tab, setting);
        }

        private void UpdatePlayerPreview()
        {
            if (_playerPreview == null) return;
            float width = Mathf.Min(455f, PlayerFrameSettings.Width);
            float height = Mathf.Min(96f, PlayerFrameSettings.Height);
            _playerPreview.SetGeometry(width, height, PlayerFrameSettings.ShowMana);
            ArcaneUiFrameStyle style = ArcaneUiStyleService.FrameStyle(ArcaneUiModuleSettings.Player);
            if (!PlayerFrameSettings.InheritTheme ||
                (ArcaneUiStyleService.IsBlankTheme && !ArcaneUiStyleService.BlankPaletteEnabled))
            {
                style.Health = UnitFrameAppearance.ParseColorOrDefault(PlayerFrameSettings.HealthHex, new Color(.36f,.61f,.52f));
                style.Mana = UnitFrameAppearance.ParseColorOrDefault(PlayerFrameSettings.ManaHex, new Color(.32f,.49f,.71f));
                style.MissingHealth = UnitFrameAppearance.ParseColorOrDefault(PlayerFrameSettings.BackgroundHex, new Color(.10f,.14f,.16f));
                style.ResourceBackground = style.MissingHealth;
                style.Border = style.MissingHealth;
                style.BarTexture = "Flat";
                style.FontName = "Default";
            }
            Stats stats = GameData.PlayerStats;
            _playerPreview.SetStyle(style, PlayerFrameSettings.UseClassColours,
                RaidClassColors.GetColorForActor(stats, stats != null ? stats.MyName : null),
                PlayerFrameSettings.Texture, PlayerFrameSettings.Font);
            _playerPreview.Render(stats != null ? stats.MyName : "Example Player",
                stats != null ? "Lv " + stats.Level : "Lv 35",
                stats != null ? RaidClassColors.ResolveClassNameForActor(stats, stats.MyName) : "Arcanist",
                stats != null ? stats.CurrentHP : 830, stats != null ? stats.CurrentMaxHP : 1000,
                stats != null ? stats.CurrentMana : 250, stats != null ? stats.GetCurrentMaxMana() : 320,
                PlayerFrameSettings.ShowNumbers,
                PlayerFrameSettings.ShowPercent, PlayerFrameSettings.ShowLevel,
                PlayerFrameSettings.ShowClass, PlayerFrameSettings.ShowManaPercent);
            _playerPreview.ApplyTextStyles();
            _playerPreview.SetCasting(_tab == "Casting" && PlayerFrameSettings.ShowCastBar,
                "Example Player Spell", .62f, 1.4f, PlayerFrameSettings.CastPlacement,
                PlayerFrameSettings.CastOffsetX, PlayerFrameSettings.CastOffsetY);
            FitInteractiveUnitPreview(_playerPreview, width, height);
            UpdateFOTFResourcePreview();
        }

        private void RenderManagerPreview()
        {
            ArcaneUiControls.ClearChildren(_previewHost);
            ArcaneUiFrameStyle style = ArcaneUiStyleService.FrameStyle(ArcaneUiModuleSettings.Manager);
            _managerPreview = ArcaneUiControls.Panel(_previewHost, "RaidManagerExample", 0, 0,
                420, 75, style.MissingHealth, false);
            _managerPreview.localScale = Vector3.one * 1.65f;
            _managerPreview.anchoredPosition = new Vector2(
                (814f - 420f * 1.65f) * .5f,
                -(640f - 75f * 1.65f) * .5f);
            LinkPreview(_managerPreview.GetComponent<Image>(), "General", "Use Global Theme");
            ArcaneUiControls.Label(_managerPreview, "PanelTitle", "Raid Manager", 10, 4,
                184, 21, 13, style.Text, TextAnchor.MiddleLeft, FontStyle.Bold);
            ArcaneUiControls.Label(_managerPreview, "PanelInfo", "Roles  •  Healing  •  Raid Groups", 10, 27,
                325, 21, 11, style.Text);
            var btn = ArcaneUiControls.Button(_managerPreview, "ThemeLink", "Edit Appearance",
                263, 49, 144, 23, Theme.Accent, Color.white,
                () => JumpToSetting("manager", "General", "Use Global Theme"), 10);
        }

        private void RenderFramePreview(bool party)
        {
            if (_previewHost == null) return;
            ArcaneUiControls.ClearChildren(_previewHost);

            _previewFrame = ArcaneUiControls.Panel(_previewHost, "ExampleUnit", 17, 29,
                175, 53, Theme.Background, false);
            _previewFrame.localScale = Vector3.one;
            var previewOutline = _previewFrame.gameObject.AddComponent<Outline>();
            previewOutline.useGraphicAlpha = false;
            // Use actual Group Frames bar textures and font resolution.
            _previewHealth = ArcaneUiControls.Panel(_previewFrame, "Health", 0, 0,
                130, 43, new Color(.19f, .52f, .29f, 1f), false);
            _previewMana = ArcaneUiControls.Panel(_previewFrame, "Mana", 0, 43,
                96, 10, new Color(.17f, .35f, .77f, 1f), false);
            _previewName = ArcaneUiControls.Label(_previewFrame, "Name", "Example Tank",
                5, 1, 125, 21, 12, Color.white, TextAnchor.MiddleLeft, FontStyle.Bold);
            _previewPercent = ArcaneUiControls.Label(_previewFrame, "HpPercent", "82%",
                100, 1, 65, 21, 11, Color.white, TextAnchor.MiddleRight, FontStyle.Bold);
            _previewRawHealth = ArcaneUiControls.Label(_previewFrame, "RawHp", "820 / 1000",
                5, 24, 164, 18, 10, Color.white, TextAnchor.MiddleRight);
            _previewDetails = ArcaneUiControls.Label(_previewHost, "Dimensions", "",
                22, 578, 370, 30, 16, Theme.Text, TextAnchor.MiddleLeft, FontStyle.Bold);
            _previewStyle = ArcaneUiControls.Label(_previewHost, "StyleSummary", "",
                395, 576, 399, 58, 12, Muted, TextAnchor.UpperLeft);
            LinkPreview(_previewHealth.GetComponent<Image>(), "Appearance", "Class Health Colours");
            LinkPreview(_previewMana.GetComponent<Image>(), "Appearance", "Mana Bar Height");
            LinkPreview(_previewName, "Text", "Name Size");
            LinkPreview(_previewPercent, "Text", "HP Percentage Size");
            LinkPreview(_previewRawHealth, "Text", "Raw Health Size");
            UpdatePreview();
        }

        private void UpdatePreview()
        {
            UpdateContextPanel();
            if (_page == "nameplates") { UpdateNameplatePreview(); return; }
            if (_page == "auras") { UpdateCooldownAuraPreview(); return; }
            if (_page == "action") { DrawActionPreview(); return; }
            if (_previewHost == null || !_previewHost.gameObject.activeSelf) return;
            if (_page == "player") { UpdatePlayerPreview(); return; }
            if (_page == "fotf") { UpdateFOTFResourcePreview(); return; }
            if (_page == "target") { UpdateTargetPreview(); return; }
            if (_page == "manager") return;
            if (_previewFrame == null) return;
            bool party = _page == "party";
            float width = party ? PartyFramesSettings.FrameWidth : RaidFramesSettings.FrameWidth;
            float height = party ? PartyFramesSettings.FrameHeight : RaidFramesSettings.FrameHeight;
            float spacing = party ? PartyFramesSettings.MemberSpacing : RaidFramesSettings.MemberSpacing;
            bool mana = party ? PartyFramesSettings.ShowManaBars : RaidFramesSettings.ShowManaBars;
            bool hpPercent = party ? PartyFramesSettings.ShowHpPercent : RaidFramesSettings.ShowHpPercent;
            bool rawHealth = party ? PartyFramesSettings.ShowRawHealth : RaidFramesSettings.ShowRawHealth;
            string hex = party ? PartyFramesSettings.StaticHealthColorHex : RaidFramesSettings.StaticHealthColorHex;
            string missing = party ? PartyFramesSettings.MissingHealthColorHex : RaidFramesSettings.MissingHealthColorHex;
            string texture = party ? PartyFramesSettings.BarTexture : RaidFramesSettings.BarTexture;
            string font = party ? PartyFramesSettings.FontName : RaidFramesSettings.FontName;
            int nameSize = party ? PartyFramesSettings.NameFontSize : RaidFramesSettings.NameFontSize;
            int percentSize = party ? PartyFramesSettings.HpPercentFontSize : RaidFramesSettings.HpPercentFontSize;
            int rawSize = party ? PartyFramesSettings.RawHpFontSize : RaidFramesSettings.RawHpFontSize;

            float manaHeight = ArcaneUiBarLayout.ManaHeight(
                party ? ArcaneUiModuleSettings.Party : ArcaneUiModuleSettings.Raid,
                Mathf.Max(1f, height - 4f), mana);
            float hpHeight = height - manaHeight;
            _previewFrame.sizeDelta = new Vector2(width, height);
            float previewScale = Mathf.Min(3.5f,
                735f / Mathf.Max(1f, width), 310f / Mathf.Max(1f, height));
            _previewFrame.localScale = Vector3.one * previewScale;
            _previewFrame.anchoredPosition = new Vector2(
                (814f - width * previewScale) * .5f,
                -(640f - height * previewScale) * .5f);
            ArcaneUiFrameStyle appliedStyle = ArcaneUiStyleService.FrameStyle(
                party ? ArcaneUiModuleSettings.Party : ArcaneUiModuleSettings.Raid);
            _previewFrame.GetComponent<Image>().color = appliedStyle.Inherit
                ? appliedStyle.MissingHealth : UnitFrameAppearance.ParseColorOrDefault(missing, Theme.Background);
            bool target = party ? PartyFramesSettings.ShowTargetHighlight : RaidFramesSettings.ShowTargetHighlight;
            bool aggro = party ? PartyFramesSettings.ShowAggroHighlight : RaidFramesSettings.ShowAggroHighlight;
            float highlightThickness = party ? PartyFramesSettings.TargetHighlightThickness : RaidFramesSettings.TargetHighlightThickness;
            var outline = _previewFrame.GetComponent<Outline>();
            outline.enabled = target || aggro;
            outline.effectColor = aggro ? new Color(.94f, .34f, .27f, 1f) : Theme.Accent;
            outline.effectDistance = new Vector2(highlightThickness, highlightThickness);
            _previewHealth.sizeDelta = new Vector2(width * .82f, hpHeight);
            _previewHealth.anchoredPosition = Vector2.zero;
            var hpImage = _previewHealth.GetComponent<Image>();
            hpImage.color = ArcaneUiStyleService.ResolveHealthColor(
                party ? ArcaneUiModuleSettings.Party : ArcaneUiModuleSettings.Raid,
                new Color(.32f, .63f, .45f, 1f),
                UnitFrameAppearance.ParseColorOrDefault(hex, new Color(.17f, .51f, .27f, 1f)),
                party ? PartyFramesSettings.UseClassColors : RaidFramesSettings.UseClassColors);
            // The same shared renderer resolves font and bar texture for
            // both this preview and the real Party/Raid unit frames.
            ArcaneUiFrameRenderer.Apply(appliedStyle,
                null, null, null, hpImage, _previewMana.GetComponent<Image>(),
                _previewName, _previewPercent, _previewRawHealth,
                texture, font, UnitFrameAppearance.ParseColorOrDefault(missing, Theme.Background),
                party ? ArcaneUiModuleSettings.Party : ArcaneUiModuleSettings.Raid);
            ArcaneUiFrameFineTuning finePreview = ArcaneUiFrameFineTuning.For(
                party ? ArcaneUiModuleSettings.Party : ArcaneUiModuleSettings.Raid);
            ArcaneUiFrameFineTuning.ApplyAlpha(_previewFrame.gameObject, finePreview.FrameOpacity);
            Color previewBorder;
            if (ColorUtility.TryParseHtmlString(finePreview.BorderColour, out previewBorder))
                _previewFrame.GetComponent<Image>().color = previewBorder;
            ArcaneUiFrameFineTuning.ApplyAlpha(_previewHealth.gameObject, finePreview.HealthOpacity);
            _previewMana.gameObject.SetActive(mana);
            _previewMana.anchoredPosition = new Vector2(0f, -hpHeight);
            _previewMana.sizeDelta = new Vector2(width * .69f, manaHeight);
            _previewPercent.gameObject.SetActive(hpPercent);
            _previewRawHealth.gameObject.SetActive(rawHealth);
            _previewName.fontSize = nameSize;
            _previewPercent.fontSize = percentSize;
            _previewRawHealth.fontSize = rawSize;
            string textModule = party ? ArcaneUiModuleSettings.Party : ArcaneUiModuleSettings.Raid;
            _previewName.rectTransform.sizeDelta = new Vector2(Mathf.Max(38f, width * .65f),
                Mathf.Min(23f, hpHeight));
            _previewPercent.rectTransform.anchoredPosition = new Vector2(Mathf.Max(0f, width - 72f), -1f);
            _previewPercent.rectTransform.sizeDelta = new Vector2(Mathf.Min(68f, width),
                Mathf.Min(23f, hpHeight));
            _previewRawHealth.rectTransform.anchoredPosition = new Vector2(5f, -Mathf.Max(1f, hpHeight - 19f));
            _previewRawHealth.rectTransform.sizeDelta = new Vector2(Mathf.Max(42f, width - 11f),
                Mathf.Min(19f, hpHeight));
            ArcaneUiTextTagStyle.Apply(_previewName, textModule, "name", nameSize);
            ArcaneUiTextTagStyle.Apply(_previewPercent, textModule, "percent", percentSize);
            ArcaneUiTextTagStyle.Apply(_previewRawHealth, textModule, "health", rawSize);
            _previewDetails.text = width.ToString("0") + " × " + height.ToString("0") + " px";
            _previewStyle.text = "Member spacing: " + spacing.ToString("0") + " px" +
                (party ? "" : "    •    Group spacing: " + RaidFramesSettings.GroupSpacing.ToString("0") + " px") +
                "\n" + ArcaneUiStyleService.EffectiveTexture(
                    party ? ArcaneUiModuleSettings.Party : ArcaneUiModuleSettings.Raid, texture) + " bars    •    " +
                ArcaneUiStyleService.EffectiveFont(
                    party ? ArcaneUiModuleSettings.Party : ArcaneUiModuleSettings.Raid, font) + " font" + (mana ? "    •    Mana on" : "    •    Mana off") +
                (target ? "    •    Target highlight" : "");
        }

        private void RenderClickCastingBindings(bool party)
        {
            Section((party ? "PARTY" : "RAID") + " CLICK CASTING",
                "Choose from healing and absorb spells learned by your current character and permitted for its class and level.");
            foreach (ClickCastBindingSlot slot in Enum.GetValues(typeof(ClickCastBindingSlot)))
            {
                var selectedSlot = slot;
                string display = ClickCastingManager.GetBindingDisplayName(
                    ClickCastingManager.GetBinding(slot, party));
                ActionButton(GetBindingLabel(slot), "Current binding: " + display,
                    "CHOOSE ACTION", () => OpenBindingPicker(selectedSlot, party));
            }
            Section("BINDING UTILITIES");
            ActionButton("Reset Click Casting", "Restore default Target/Inspect/None assignments for this module.",
                "RESET BINDINGS", () =>
                {
                    if (party) PartyFramesSettings.ResetClickBindings();
                    else RaidFramesSettings.ResetClickBindings();
                    RenderPage();
                });
            ActionButton("Healing Spell Diagnostics", "Dump live healing spells and Lightbringer absorb wards to the log for troubleshooting.",
                "DUMP SPELLS", () => HealingSpellCatalog.DumpAllHealingSpellsToLog());
        }

        private static string GetBindingLabel(ClickCastBindingSlot slot)
        {
            switch (slot)
            {
                case ClickCastBindingSlot.LeftClick: return "Left Click";
                case ClickCastBindingSlot.RightClick: return "Right Click";
                case ClickCastBindingSlot.MiddleClick: return "Middle Click";
                case ClickCastBindingSlot.ShiftLeft: return "Shift + Left";
                case ClickCastBindingSlot.ShiftRight: return "Shift + Right";
                case ClickCastBindingSlot.CtrlLeft: return "Ctrl + Left";
                case ClickCastBindingSlot.CtrlRight: return "Ctrl + Right";
                case ClickCastBindingSlot.AltLeft: return "Alt + Left";
                case ClickCastBindingSlot.AltRight: return "Alt + Right";
                default: return slot.ToString();
            }
        }

        private void CreateBindingPicker()
        {
            _bindingPicker = ArcaneUiControls.Panel(_window, "ArcaneUI_BindingPicker",
                BodyX, 211, BodyWidth, 416, Theme.Background);
            _bindingPicker.gameObject.SetActive(false);
        }

        private void OpenBindingPicker(ClickCastBindingSlot slot, bool party)
        {
            if (_bindingPicker == null) return;
            ArcaneUiControls.ClearChildren(_bindingPicker);
            ArcaneUiControls.Label(_bindingPicker, "Title", "ASSIGN  /  " + GetBindingLabel(slot),
                18, 8, 680, 32, 16, Theme.Text, TextAnchor.MiddleLeft, FontStyle.Bold);
            ArcaneUiControls.Button(_bindingPicker, "ClosePicker", "BACK", 688, 9, 100, 29,
                Card, Theme.Text, () => _bindingPicker.gameObject.SetActive(false), 12);
            var choices = ArcaneUiControls.ScrollContent(_bindingPicker, "BindingChoices",
                12, 48, BodyWidth - 24, 356, Card);
            AddBindingChoice(choices, slot, party, "None", ClickCastingManager.ActionNone);
            AddBindingChoice(choices, slot, party, "Target", ClickCastingManager.ActionTarget);
            AddBindingChoice(choices, slot, party, "Inspect", ClickCastingManager.ActionInspect);
            foreach (Spell spell in HealingSpellCatalog.GetPlayerHealingSpells())
            {
                var captured = spell;
                AddBindingChoice(choices, slot, party,
                    HealingSpellCatalog.GetDisplayName(captured),
                    ClickCastingManager.MakeSpellBinding(captured));
            }
            ArcaneUiControls.RefreshScrollLayout(choices, true);
            _bindingPicker.gameObject.SetActive(true);
            _bindingPicker.SetAsLastSibling();
        }

        private void AddBindingChoice(Transform parent, ClickCastBindingSlot slot,
            bool party, string label, string binding)
        {
            var row = ArcaneUiControls.Row(parent, "BindingChoice", 37, Card);
            ArcaneUiControls.Button(row, "Select", label, 5, 2, 762, 33,
                Theme.Panel, Theme.Text, () =>
                {
                    ClickCastingManager.SetBinding(slot, binding, party);
                    _bindingPicker.gameObject.SetActive(false);
                    RenderPage();
                }, 12);
        }

        private void RenderManager()
        {
            Section("MODULE", "This switch does not disable Erenshor's native raid management.");
            ModuleToggle(ArcaneUiModuleSettings.Manager, "Enable ArcaneUI Raid Manager",
                "OFF hides ArcaneUI raid controls and their popups; the native manager remains available.");
            Section("APPEARANCE");
            ToggleSetting("Use Global Theme", "Restyle ArcaneUI's raid panel and buttons without changing native raid controls.",
                () => ArcaneUiStyleService.Inherits(ArcaneUiModuleSettings.Manager),
                value => ArcaneUiStyleService.SetInherits(ArcaneUiModuleSettings.Manager, value));
            Section("RAID MANAGER CONTROLS");
            Info("Gameplay behaviours unchanged",
                "Assignments, raid roles, healing, resurrection and management continue through Arcane's Group Frames' existing backend.");
            ActionButton("Raid Manager Hotkey",
                _capturingRaidManagerHotkey ? "Press a key to bind. Escape clears the binding." :
                (RaidFramesSettings.RaidManagerToggleKey == KeyCode.None ? "No custom key assigned." :
                "Current key: " + RaidFramesSettings.RaidManagerToggleKey),
                _capturingRaidManagerHotkey ? "PRESS A KEY" : "SET HOTKEY", () =>
                {
                    _capturingRaidManagerHotkey = true;
                    RenderPage();
                });
            Info("Raid Manager visibility",
                "Use the configured hotkey during gameplay to show or hide only the raid manager. " +
                "Raid frame visibility is handled independently.");
        }

        private void RenderInteractions()
        {
            Section("CLICK CASTING & MOUSEOVER");
            Info("Existing Group Frames interactions",
                "Party and raid bindings remain separately configurable and retain their existing PlayerPrefs keys.");
            ActionButton("Party Frame Click Casting", "Set actions for all nine mouse/modifier combinations.",
                "PARTY BINDINGS", () => NavigateToBindings(true));
            ActionButton("Raid Frame Click Casting", "Set actions for all nine mouse/modifier combinations.",
                "RAID BINDINGS", () => NavigateToBindings(false));
        }

        private void RenderTargetDiscovery()
        {
            Section("NATIVE TARGET UI DISCOVERY",
                "Read-only scanner. Select a target in-game before scanning; nothing is hidden or modified.");
            Info("Candidate finder",
                "Finds visible and inactive UI objects with target/enemy/mob names. Review the exact full paths " +
                "before ArcaneUI replaces any native target visuals.");
            ActionButton("Scan Native Target UI", "Search loaded scene UI objects, including inactive panels.",
                "SCAN UI", () => { ScanTargetCandidates(); RenderPage(); });
            if (_hudCandidates.Count == 0)
            {
                Info("No candidates cached", "Click SCAN UI while in gameplay with an NPC targeted, then check the results.");
                return;
            }
            Section("CANDIDATES FOUND: " + _hudCandidates.Count,
                "Cycle to inspect the full hierarchy path and copy it from the game log.");
            _hudCandidateIndex = Mathf.Clamp(_hudCandidateIndex, 0, _hudCandidates.Count - 1);
            Info("Candidate " + (_hudCandidateIndex + 1) + " / " + _hudCandidates.Count,
                _hudCandidates[_hudCandidateIndex]);
            ActionButton("Previous Target Candidate", "Display the previous UI candidate.",
                "PREVIOUS", () => { _hudCandidateIndex = (_hudCandidateIndex - 1 + _hudCandidates.Count) % _hudCandidates.Count; RenderPage(); });
            ActionButton("Next Target Candidate", "Display the next UI candidate.",
                "NEXT", () => { _hudCandidateIndex = (_hudCandidateIndex + 1) % _hudCandidates.Count; RenderPage(); });
            ActionButton("Log Target Candidates", "Writes all candidate paths to the mod log, including the currently selected path.",
                "WRITE LOG", LogTargetCandidates);
            Info("Native fallbacks protected", "This scan does NOT enable the target module, disable a native object, or change targeting/casting behaviour.");
        }

        private static string HierarchyPath(Transform transform)
        {
            var parts = new List<string>();
            for (Transform t = transform; t != null; t = t.parent)
                parts.Add(t.name);
            parts.Reverse();
            return transform.gameObject.scene.name + ":" + string.Join("/", parts.ToArray());
        }

        private void ScanTargetCandidates()
        {
            _hudCandidates.Clear();
            _hudCandidateIndex = 0;
            RectTransform[] all = Resources.FindObjectsOfTypeAll<RectTransform>();
            var found = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (RectTransform rect in all)
            {
                if (rect == null || !rect.gameObject.scene.IsValid()) continue;
                string path = HierarchyPath(rect);
                string name = rect.name.ToLowerInvariant();
                if (path.IndexOf("ArcaneUI", StringComparison.OrdinalIgnoreCase) >= 0) continue;
                string lowerPath = path.ToLowerInvariant();
                if (!(name.Contains("target") || name.Contains("enemy") ||
                      name.Contains("monster") || name.Contains("mob") ||
                      name.Contains("npc") || name.Contains("focus") ||
                      (name.Contains("health") && lowerPath.Contains("uielements") &&
                       !lowerPath.Contains("player")))) continue;
                if (!(lowerPath.Contains("ui") || lowerPath.Contains("hud") ||
                      rect.GetComponent<CanvasRenderer>() != null)) continue;
                found.Add(path + (rect.gameObject.activeInHierarchy ? " [ACTIVE]" : " [INACTIVE]"));
            }
            foreach (string entry in found)
            {
                if (_hudCandidates.Count >= 100) break;
                _hudCandidates.Add(entry);
            }
            Plugin.LogInfo("ArcaneUI target discovery: " + _hudCandidates.Count + " possible UI nodes found (first 100). No native objects modified.");
            if (_hudCandidates.Count > 0) LogTargetCandidates();
        }

        private void LogTargetCandidates()
        {
            for (int i = 0; i < _hudCandidates.Count; i++)
                Plugin.LogInfo("ArcaneUI target candidate " + (i + 1) + "/" + _hudCandidates.Count + ": " + _hudCandidates[i]);
        }

        private void RenderNameplates()
        {
            if (_tab == "Appearance")
            {
                Section("NAMEPLATE SIZING", "Size and screen-space placement of custom overhead nameplates.");
                SliderSetting("Nameplate Width", 85f, 280f,
                    () => NameplateSettings.Width, NameplateSettings.SetWidth, " px");
                SliderSetting("Nameplate Health Height", 8f, 32f,
                    () => NameplateSettings.Height, NameplateSettings.SetHeight, " px");
                SliderSetting("Nameplate Vertical Offset", -10f, 20f,
                    () => NameplateSettings.HeightOffset * 10f, v => NameplateSettings.SetHeightOffset(v / 10f), " ×0.1 m");
                ChoiceSetting("Health Fill Direction", "Left to Right / Right to Left / Bottom to Top / Top to Bottom.",
                    NameplateSettings.HealthDirectionName, NameplateSettings.CycleHealthDirection);
                SliderSetting("Background Opacity", 0f, 100f,
                    () => NameplateSettings.BackgroundOpacity, NameplateSettings.SetBackgroundOpacity, " %");
                Section("HEALTH BAR COLOURS", "Use class colours for SimPlayers or custom NPC colours.");
                ToggleSetting("SimPlayer Class Colours", "Use the SimPlayer's native/FOTF class colour rather than the NPC health colour.",
                    () => NameplateSettings.SimClassColours, NameplateSettings.SetSimClassColours);
                HexSetting("Hostile NPC Health Colour", "Used for hostile and unknown/neutral NPCs.",
                    () => NameplateSettings.HostileColour, NameplateSettings.SetHostileColour);
                HexSetting("Friendly NPC Health Colour", "Used for positively identified friendly NPCs.",
                    () => NameplateSettings.FriendlyColour, NameplateSettings.SetFriendlyColour);
                HexSetting("SimPlayer Health Colour", "Used when SimPlayer Class Colours is disabled.",
                    () => NameplateSettings.SimColour, NameplateSettings.SetSimColour);
                Section("TARGET HIGHLIGHT", "Apply an accent border and optional size boost to the selected target.");
                ToggleSetting("Show Target Outline", "Highlight the health bar on the currently selected target.",
                    () => NameplateSettings.ShowTargetOutline, NameplateSettings.SetShowTargetOutline);
                HexSetting("Target Highlight Colour", "Outline colour for the selected target.",
                    () => NameplateSettings.TargetColour, NameplateSettings.SetTargetColour);
                SliderSetting("Target Outline Thickness", 1f, 5f,
                    () => NameplateSettings.TargetOutlineSize, NameplateSettings.SetTargetOutlineSize, " px");
                SliderSetting("Target Scale", 100f, 150f,
                    () => NameplateSettings.TargetScale, NameplateSettings.SetTargetScale, " %");
                return;
            }
            if (_tab == "Text")
            {
                Section("NAME LABEL", "Configure name text independently from health bar rendering.");
                ToggleSetting("Show Character Names", "Show or hide the text above each nameplate health bar.",
                    () => NameplateSettings.ShowNames, NameplateSettings.SetShowNames);
                SliderSetting("Name Font Size", 8f, 24f,
                    () => NameplateSettings.NameFontSize, NameplateSettings.SetNameFontSize, " px");
                SliderSetting("Name Character Limit", 8f, 60f,
                    () => NameplateSettings.NameCharacterLimit, NameplateSettings.SetNameCharacterLimit, " chars");
                HexSetting("Name Text Colour", "Name colour on NPCs and non-class-coloured SimPlayers.",
                    () => NameplateSettings.NameColour, NameplateSettings.SetNameColour);
                Section("NPC ROLE TAGS", "Display service tags such as <Vendor> or <Banker> for NPCs whose roles are confirmed by the game.");
                ToggleSetting("Show NPC Role Tags", "Display confirmed NPC service roles below their name. Unknown roles remain untagged; SimPlayers are excluded.",
                    () => NameplateSettings.ShowNpcTags, NameplateSettings.SetShowNpcTags);
                SliderSetting("NPC Tag Font Size", 8f, 22f,
                    () => NameplateSettings.NpcTagFontSize, NameplateSettings.SetNpcTagFontSize, " px");
                HexSetting("NPC Tag Colour", "Text colour for service tags.",
                    () => NameplateSettings.NpcTagColour, NameplateSettings.SetNpcTagColour);
                Section("HEALTH LABEL", "Optional percentage printed directly on the nameplate health bar.");
                ToggleSetting("Show Health Percentage", "Show current health percentage on the health bar.",
                    () => NameplateSettings.ShowHealthPercent, NameplateSettings.SetShowHealthPercent);
                SliderSetting("Health Percentage Font Size", 8f, 24f,
                    () => NameplateSettings.PercentFontSize, NameplateSettings.SetPercentFontSize, " px");
                return;
            }
            if (_tab == "Native")
            {
                Section("NATIVE OVERHEAD DISCOVERY", "Choose an exact native text/UI child from a targeted NPC before suppression.");
                ToggleSetting("Hide Selected Native Nameplate",
                    "Opt-in. Hides only the selected native child text/UI; never disables NPC objects, meshes or gameplay scripts.",
                    () => NameplateSettings.HideNative, v => { if (!v) NativeOverheadNameplateBridge.Restore(); NameplateSettings.SetHideNative(v); });
                Info("Suppression status", NativeOverheadNameplateBridge.Status);
                Info("NamePlate(Clone) discovery", NativeOverheadNameplateBridge.CloneDiagnostics);
                ActionButton("Scan Target Nameplate", "Target an NPC/SimPlayer and inspect its native nameplate children.",
                    "SCAN TARGET", () => { NativeOverheadNameplateBridge.ScanTarget(); RenderPage(); });
                Info("Nameplate candidate", NativeOverheadNameplateBridge.SelectedCandidate);
                ActionButton("Select Native Nameplate Candidate", "Cycle through " + NativeOverheadNameplateBridge.CandidateCount + " verified nameplate candidates.",
                    "NEXT", () => { NativeOverheadNameplateBridge.NextCandidate(); RenderPage(); });
                ActionButton("Use Native Nameplate Candidate", "Save the selected exact path. Native hiding remains independently toggleable.",
                    "USE THIS", () => { NativeOverheadNameplateBridge.UseCandidate(); RenderPage(); });
                ActionButton("Reset Native Nameplate Selection", "Restore existing native visuals and clear the chosen suppression path.",
                    "RESTORE", () => { NativeOverheadNameplateBridge.Restore(); NameplateSettings.SetNativeRelativePath(""); RenderPage(); });
                Info("Selected child path", string.IsNullOrEmpty(NameplateSettings.NativeRelativePath)
                    ? "None. The native overhead UI is untouched." : NameplateSettings.NativeRelativePath);
                return;
            }
            Section("OVERHEAD NAMEPLATES", "Enable the custom nameplate layer and choose which characters appear.");
            ToggleSetting("Enable ArcaneUI Nameplates", "Display ArcaneUI nameplates above nearby characters.",
                () => NameplateSettings.Enabled, NameplateSettings.SetEnabled);
            ToggleSetting("Hide Original Nameplates", "Hide original overhead visuals only where an ArcaneUI nameplate is available.",
                () => NameplateSettings.HideNative,
                v => { if (!v) NativeOverheadNameplateBridge.Restore(); NameplateSettings.SetHideNative(v); });
            ToggleSetting("Show NPC Nameplates", "Master toggle for all non-SimPlayer NPCs.",
                () => NameplateSettings.ShowNpcs, NameplateSettings.SetShowNpcs);
            ToggleSetting("Show Friendly NPC Nameplates", "Show friendly NPCs. Hostile and unknown/neutral NPCs are unaffected.",
                () => NameplateSettings.ShowFriendly, NameplateSettings.SetShowFriendly);
            ToggleSetting("Show SimPlayer Nameplates", "Show ArcaneUI SimPlayer health bars. When off, Erenshor's native SimPlayer name and guild overhead text remain visible.",
                () => NameplateSettings.ShowSims, NameplateSettings.SetShowSims);
            SliderSetting("Nameplate View Distance", 10f, 80f,
                () => NameplateSettings.MaxDistance, NameplateSettings.SetMaxDistance, " m");
            Info("Performance", "Up to 24 closest custom nameplates; bounded pool with 1.25-second discovery updates.");
            Info("Native compatibility", "Native overhead hiding applies to ArcaneUI-covered characters and filtered NPCs. With SimPlayer nameplates off, native SimPlayer names and guilds are preserved even when Hide Native is enabled.");
        }

        // The sidebar Cast Bars page is a real settings entry, not a placeholder.
        // All entry points use the same PlayerPrefs-backed NativeCastBarBridge setting.
        private void RenderCastBars()
        {
            Section("NATIVE CAST BAR", "Hide Erenshor's original screen-space player cast bar.");
            NativeCastVisibilityToggle();
            Info("How visibility works", "The native cast bar is hidden only while Player Frame and Show Player Cast Bar are enabled. Turn either off to restore it.");
            Section("PLAYER & TARGET CAST BARS", "Both replacements use the compact mana overlay by default.");
            ToggleSetting("Show Player Cast Bar", "Enable ArcaneUI's custom player cast display.",
                () => PlayerFrameSettings.ShowCastBar, v => PlayerFrameSettings.SetShowCastBar(v));
            ToggleSetting("Show Target Cast Bar", "Enable ArcaneUI's target cast display.",
                () => TargetFrameSettings.ShowCastBar, v => TargetFrameSettings.SetShowCastBar(v));
            ActionButton("Player Cast Bar Settings", "Appearance, offsets, progress, time and diagnostics.",
                "OPEN PLAYER", () => JumpToSetting("player", "Casting", "Show Player Cast Bar"));
            ActionButton("Target Cast Bar Settings", "Appearance, offsets, progress and diagnostics.",
                "OPEN TARGET", () => JumpToSetting("target", "Casting", "Show Cast Bar"));
            // Native cast-bar discovery remains internal and is not part of the player menu.
        }

        private void NativeCastVisibilityToggle()
        {
            ToggleSetting("Hide Native Cast Bar", "Hide the original Erenshor player cast visual while the ArcaneUI Player Cast Bar is enabled; OFF restores it.",
                () => NativeCastBarBridge.HideNative, v => NativeCastBarBridge.SetHideNative(v));
        }

        private void NativeCastDiscoveryControls()
        {
            Info("Native visual selection", NativeCastBarBridge.SelectedPath);
            ActionButton("Scan Native Cast Bar", "Find native screen-space casting visuals. Scan while casting; candidates are logged.",
                "SCAN CAST UI", () => { NativeCastBarBridge.ScanAndLog(); RenderPage(); });
            ActionButton("Select Native Cast Bar", "If automatic detection fails, cycle scanned candidates (including Automatic).",
                "NEXT MATCH", () => { NativeCastBarBridge.SelectNextCandidate(); RenderPage(); });
        }

        private void RenderComingSoon()
        {
            Section("MODULE ROADMAP");
            Info("Planned module — not yet enabled",
                "This settings category is a placeholder only. Erenshor's native interface remains untouched until the replacement is implemented and validated.");
            ActionButton("Return to active modules", "Configure the working party and raid frame integrations.",
                "PARTY FRAMES", () => SelectPage("party"));
        }
    }
}
