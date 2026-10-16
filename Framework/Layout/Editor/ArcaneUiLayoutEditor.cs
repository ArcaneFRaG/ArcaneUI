using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using ArcanesGroupFrames.Framework.Canvas;
using ArcanesGroupFrames.Framework.Configuration.UI;
using ArcanesGroupFrames.Framework.Theming;
using ArcanesGroupFrames.Framework.Modules;
using ArcanesGroupFrames.Framework.NativeUI;
using ArcanesGroupFrames.Modules.UnitFrames.Player;
using ArcanesGroupFrames.Modules.UnitFrames.Target;
using ArcanesGroupFrames.Modules.ActionBars.Configuration;
using ArcanesGroupFrames.Modules.Experience.Configuration;
using ArcanesGroupFrames.Modules.Chat.Configuration;
using ArcanesGroupFrames.Modules.Chat.Native;
using ArcanesGroupFrames.Modules.Minimap.Configuration;
using ArcanesGroupFrames.Modules.Minimap.Native;
using ArcanesGroupFrames.Modules.EnemyTracker.Configuration;
using ArcanesGroupFrames.Modules.CooldownAuras.Configuration;

namespace ArcanesGroupFrames.Framework.Layout.Editor
{
    /// <summary>
    /// A reversible, screen-space mover for existing, native-bound Arcane UI
    /// modules. The source RectTransform and its existing settings own the
    /// actual position/size; the overlay is only an editing surface.
    /// </summary>
    internal sealed class ArcaneUiLayoutEditor
    {
        private const string ProfilePrefix = "ArcaneUI.Layout.Default.";
        private readonly ArcaneCanvasManager _canvas;
        private readonly ThemeManager _themes;
        private readonly Action<string> _onOpenSettings;
        private RectTransform _layer;
        private RectTransform _toolbar;
        private RectTransform _selectedXY;
        private Text _selectedXYTitle;
        private InputField _coordinateX;
        private InputField _coordinateY;
        private InputField _coordinateWidth;
        private InputField _coordinateHeight;
        private RectTransform _sizeControls;
        private RectTransform _guideVertical;
        private RectTransform _guideHorizontal;
        // Click a frame to select; Ctrl/Shift-click to add or remove eligible frames.
        // Native chat, minimap and FOTF detached strips retain their own safeguards.
        private readonly HashSet<string> _selectedIds = new HashSet<string>(StringComparer.Ordinal);
        private readonly Dictionary<string, Vector2> _groupOrigins =
            new Dictionary<string, Vector2>(StringComparer.Ordinal);
        private readonly List<float> _xSnapGuides = new List<float>(72);
        private readonly List<float> _ySnapGuides = new List<float>(72);
        private bool _groupDragging;
        private float _nextGroupRefresh;
        private RawImage _grid;
        private Texture2D _gridTexture;
        private int _gridTextureSize;
        private Vector2 _lastGridCanvasSize;
        private readonly Dictionary<string, RectTransform> _ghosts =
            new Dictionary<string, RectTransform>();
        private readonly List<LayoutSnapshot> _history = new List<LayoutSnapshot>();
        private int _historyIndex;
        private LayoutSnapshot _onOpen;
        private string _selected;
        private string _dragged;
        private bool _resizing;
        private Vector2 _dragOffset;
        private Vector2 _startNativePosition;
        private Vector2 _startGhostPosition;
        private Vector2 _startPoint;
        private Vector2 _startSize;
        private float _startMapScale;
        private float _startFrameWidth;
        private float _startFrameHeight;
        private float _nextRefresh;
        private bool _snap = true;
        private bool _showGrid = true;
        private int _gridSize = 20;
        private bool _changedDuringDrag;
        private float _lastResizeRebuild;
        private float _pendingResizeWidth;
        private float _pendingResizeHeight;

        internal bool IsOpen { get { return _layer != null && _layer.gameObject.activeSelf; } }

        internal ArcaneUiLayoutEditor(ArcaneCanvasManager canvas, ThemeManager themes,
            Action<string> openSettings)
        {
            _canvas = canvas;
            _themes = themes;
            _onOpenSettings = openSettings;
            // Only three grid steps are supported; gracefully recover from
            // old/out-of-range PlayerPrefs values.
            _gridSize = PlayerPrefs.GetInt("ArcaneUI.Layout.GridSize", 20);
            if (_gridSize != 10 && _gridSize != 20 && _gridSize != 40)
                _gridSize = 20;
            _snap = PlayerPrefs.GetInt("ArcaneUI.Layout.Snap", 1) == 1;
            _showGrid = PlayerPrefs.GetInt("ArcaneUI.Layout.ShowGrid", 1) == 1;
        }

        internal void Open()
        {
            if (!_canvas.IsReady || IsOpen) return;
            if (_layer == null) Build();
            if (_layer == null) return;
            _onOpen = LayoutSnapshot.Capture();
            // Selection is direct: click any visible frame outline to show its local X/Y editor.
            _selected = null;
            _selectedIds.Clear();
            _groupOrigins.Clear();
            _groupDragging = false;
            _history.Clear();
            _history.Add(_onOpen);
            _historyIndex = 0;
            _layer.gameObject.SetActive(true);
            ArcaneUiModalInputGuard.SetEditorOpen(true);
            // Refresh canvas rects before calculating the number of visible
            // grid tiles, including on the first editor opening.
            UnityEngine.Canvas.ForceUpdateCanvases();
            if (NativeChat != null) NativeChat.EnsureEditorBinding();
            // Force one full render/readiness pass before calculating the
            // replacement minimap ghost bounds. The binding-only call does
            // not refresh an already discovered native HUD.
            if (NativeMinimap != null) NativeMinimap.Refresh();
            // The replacement minimap may have been created after the layout editor layer.
            // Keep the editor's sorted canvas above the native map HUD and its controls.
            _layer.SetAsLastSibling();
            var editorCanvas = _layer.GetComponent<UnityEngine.Canvas>();
            if (editorCanvas != null) { editorCanvas.overrideSorting = true; editorCanvas.sortingOrder = 32767; }
            RefreshGrid();
            RefreshGhosts();
            UpdateInfo(null);
        }

        internal void Close(bool returnToSettings)
        {
            if (!IsOpen) return;
            // Last drag may have been interrupted by Escape or the slash command.
            if (_changedDuringDrag) FinishMutation();
            _dragged = null;
            _groupDragging = false;
            _groupOrigins.Clear();
            _changedDuringDrag = false;
            ClearGuides();
            FOTFResourceSettings.ClearDragPositions();
            FOTFResourceSettings.ClearMassDragPosition();
            // With the old SAVE/LOAD toolbar removed, locking the editor commits
            // the exact current layout (including native chat, minimap, and FOTF strips).
            LayoutSnapshot.Capture().Save(ProfilePrefix);
            _layer.gameObject.SetActive(false);
            ArcaneUiModalInputGuard.SetEditorOpen(false);
            if (returnToSettings && _onOpenSettings != null)
                _onOpenSettings(PageFor(_selected));
        }

        internal void Update()
        {
            if (!IsOpen) return;
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                Close(false);
                return;
            }
            // Keep the grid at the selected spacing if the game window
            // changes resolution while the editor is open.
            if (_layer != null && _layer.rect.size != _lastGridCanvasSize)
                RefreshGrid();
            if (_dragged == null && Time.unscaledTime >= _nextRefresh)
            {
                _nextRefresh = Time.unscaledTime + .25f;
                RefreshGhosts();
            }
            // Do not intercept typing in the exact coordinate/size editors or other UI fields.
            if (_dragged == null && !TextInputFocused())
            {
                bool ctrl = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
                if (ctrl && Input.GetKeyDown(KeyCode.Z)) Undo();
                else if (ctrl && Input.GetKeyDown(KeyCode.Y)) Redo();
                else if (Input.GetKeyDown(KeyCode.LeftArrow)) Nudge(-1, 0);
                else if (Input.GetKeyDown(KeyCode.RightArrow)) Nudge(1, 0);
                else if (Input.GetKeyDown(KeyCode.UpArrow)) Nudge(0, 1);
                else if (Input.GetKeyDown(KeyCode.DownArrow)) Nudge(0, -1);
            }
        }

        private static bool TextInputFocused()
        {
            var events = EventSystem.current;
            return events != null && events.currentSelectedGameObject != null &&
                events.currentSelectedGameObject.GetComponentInParent<InputField>() != null;
        }


        internal void Shutdown()
        {
            ArcaneUiModalInputGuard.SetEditorOpen(false);
            if (_layer != null) UnityEngine.Object.Destroy(_layer.gameObject);
            if (_gridTexture != null) UnityEngine.Object.Destroy(_gridTexture);
            _gridTexture = null;
            _gridTextureSize = 0;
            _lastGridCanvasSize = Vector2.zero;
            _grid = null;
            _coordinateX = null;
            _coordinateY = null;
            _coordinateWidth = null;
            _coordinateHeight = null;
            _sizeControls = null;
            _guideVertical = null;
            _guideHorizontal = null;
            _selectedIds.Clear();
            _groupOrigins.Clear();
            _selectedXY = null;
            _selectedXYTitle = null;
            _layer = null;
            _toolbar = null;
            _ghosts.Clear();
            _history.Clear();
        }

        private UiTheme Theme { get { return _themes.Current; } }
        private static bool TryBar(string id, out int bar)
        {
            bar = -1;
            if (string.IsNullOrEmpty(id) || !id.StartsWith("bar", StringComparison.Ordinal)) return false;
            int index;
            if (!int.TryParse(id.Substring(3), out index) || index < 0 || index >= ActionBarPlanSettings.MaxBars)
                return false;
            bar = index;
            return true;
        }

        private static bool TryAuraGroup(string id, out int group)
        {
            group = -1;
            if (string.IsNullOrEmpty(id) || !id.StartsWith("auraframe", StringComparison.Ordinal)) return false;
            int index;
            if (!int.TryParse(id.Substring(9), out index) || index < 0 || index > 4) return false;
            group = index;
            return true;
        }
        private static NativeChatPresentation NativeChat
        {
            get
            {
                return Plugin.Instance != null && Plugin.Instance.UiRuntime != null
                    ? Plugin.Instance.UiRuntime.Chat : null;
            }
        }
        private static NativeMinimapPresentation NativeMinimap
        {
            get { return Plugin.Instance != null && Plugin.Instance.UiRuntime != null ?
                Plugin.Instance.UiRuntime.Minimap : null; }
        }
        private static string PageFor(string id)
        {
            int bar;
            if (id == "native-chat") return "chat";
            int auraGroup;
            if (TryAuraGroup(id, out auraGroup)) return "auras";
            if (id == "minimap") return "minimap";
            if (id == "enemytracker") return "enemytracker";
            if (TryBar(id, out bar)) return "action";
            if (id == "xp") return "xp";
            if (id == "fotf-primary" || id == "fotf-secondary" || id == "fotf-mass") return "fotf";
            return id == "target" ? "target" : id == "player" ? "player" : id == "party" ? "party" : id == "raid" ? "raid" : "manager";
        }
        private static string TitleFor(string id)
        {
            int bar;
            if (id == "native-chat") return "Native Chat";
            int auraGroup;
            if (TryAuraGroup(id, out auraGroup)) return CooldownAuraSettings.FrameNames[auraGroup];
            if (id == "minimap") return "Minimap";
            if (id == "enemytracker") return "Enemy Tracker";
            if (TryBar(id, out bar)) return bar == 0 ? "Action Bar 1 (Primary)" : "Action Bar " + (bar + 1);
            if (id == "xp") return "XP / Ascension XP Bar";
            if (id == "fotf-primary") return "FOTF Primary Resource";
            if (id == "fotf-mass") return "FOTF Bone Mass";
            if (id == "fotf-secondary") return "FOTF Secondary Resource";
            return id == "target" ? "Target Frame" : id == "player" ? "Player Frame" : id == "party" ? "Party Frames" : id == "raid" ? "Raid Frames" : "Raid Manager";
        }

        private static bool CanEdit(string id)
        {
            int bar;
            int auraGroup;
            // The five aura rows are always editable, even with no buffs, no
            // target, or with the entire tracker toggled off. These are
            // editor-only outlines, not fake gameplay status effects.
            if (TryAuraGroup(id, out auraGroup)) return true;
            if (id == "native-chat") return NativeChat != null && NativeChat.CanSelectInEditor;
            if (id == "minimap") return NativeMinimap != null && NativeMinimap.CanEdit;
            if (id == "enemytracker") return EnemyTrackerSettings.Enabled && Plugin.Instance != null &&
                Plugin.Instance.UiRuntime != null && Plugin.Instance.UiRuntime.GetEnemyTrackerRect() != null;
            if (TryBar(id, out bar)) return ActionBarMirrorSettings.Enabled && bar < ActionBarPlanSettings.Count;
            if (id == "xp") return ExperienceBarSettings.Enabled;
            if (id == "fotf-primary") return ArcaneUiModuleSettings.PlayerFrameEnabled && FOTFResourceSettings.Enabled && FOTFResourceSettings.DetachedPrimary;
            if (id == "fotf-secondary") return ArcaneUiModuleSettings.PlayerFrameEnabled && FOTFResourceSettings.Enabled && FOTFResourceSettings.DetachedSecondary;
            if (id == "fotf-mass") return ArcaneUiModuleSettings.PlayerFrameEnabled && FOTFResourceSettings.Enabled && FOTFResourceSettings.Detached(FOTFResourcePart.Mass);
            if (id == "target") return ArcaneUiModuleSettings.TargetFrameEnabled;
            if (id == "player") return ArcaneUiModuleSettings.PlayerFrameEnabled;
            if (id == "party") return ArcaneUiModuleSettings.PartyFramesEnabled;
            if (id == "raid") return ArcaneUiModuleSettings.RaidFramesEnabled;
            return ArcaneUiModuleSettings.RaidManagerEnabled;
        }

        private static RectTransform Source(string id)
        {
            int bar;
            int auraGroup;
            if (TryAuraGroup(id, out auraGroup)) return Plugin.Instance != null && Plugin.Instance.UiRuntime != null
                ? Plugin.Instance.UiRuntime.GetAuraTrackerFrameRect(auraGroup) : null;
            if (id == "native-chat") return NativeChat != null ? NativeChat.NativeEditorRect : null;
            if (id == "minimap") return NativeMinimap != null ? NativeMinimap.EditorRect : null;
            if (id == "enemytracker") return Plugin.Instance != null && Plugin.Instance.UiRuntime != null
                ? Plugin.Instance.UiRuntime.GetEnemyTrackerRect() : null;
            if (TryBar(id, out bar))
                return Plugin.Instance != null && Plugin.Instance.UiRuntime != null
                    ? Plugin.Instance.UiRuntime.GetActionBarRect(bar) : null;
            if (id == "fotf-primary" || id == "fotf-secondary" || id == "fotf-mass")
            {
                RectTransform resource = Plugin.Instance != null && Plugin.Instance.UiRuntime != null && Plugin.Instance.UiRuntime.PlayerFrame != null
                    ? (id == "fotf-primary" ? Plugin.Instance.UiRuntime.PlayerFrame.FOTFPrimaryRect
                        : id == "fotf-secondary" ? Plugin.Instance.UiRuntime.PlayerFrame.FOTFSecondaryRect
                        : Plugin.Instance.UiRuntime.PlayerFrame.FOTFMassRect) : null;
                return resource != null && resource.gameObject.activeInHierarchy ? resource : null;
            }
            if (id == "xp") return Plugin.Instance != null && Plugin.Instance.UiRuntime != null
                ? Plugin.Instance.UiRuntime.GetExperienceBarRect() : null;
            if (id == "target") return Plugin.Instance != null && Plugin.Instance.UiRuntime != null && Plugin.Instance.UiRuntime.TargetFrame != null
                ? Plugin.Instance.UiRuntime.TargetFrame.Rect : null;
            if (id == "player") return Plugin.Instance != null && Plugin.Instance.UiRuntime != null && Plugin.Instance.UiRuntime.PlayerFrame != null
                ? Plugin.Instance.UiRuntime.PlayerFrame.Rect : null;
            if (id == "party") return PartyFramesUI.PanelRect;
            if (id == "raid") return RaidFramesUI.PanelRect;
            return RaidControlPanel.PanelRect;
        }

        private static Vector2 SavedPosition(string id)
        {
            int bar;
            int auraGroup;
            if (TryAuraGroup(id, out auraGroup)) return CooldownAuraSettings.Frame(auraGroup).Position;
            if (id == "native-chat") return new Vector2(ChatSettings.OffsetX, ChatSettings.OffsetY);
            if (id == "minimap") return new Vector2(MinimapSettings.OffsetX, MinimapSettings.OffsetY);
            if (id == "enemytracker") return EnemyTrackerSettings.Position;
            if (TryBar(id, out bar)) return new Vector2(ActionBarPlanSettings.BarX(bar), ActionBarPlanSettings.BarY(bar));
            if (id == "xp") return new Vector2(ExperienceBarSettings.OffsetX, ExperienceBarSettings.OffsetY);
            if (id == "fotf-primary") return FOTFResourceSettings.PrimaryPosition;
            if (id == "fotf-secondary") return FOTFResourceSettings.SecondaryPosition;
            if (id == "fotf-mass") return FOTFResourceSettings.MassPosition;
            if (id == "target") return TargetFrameSettings.Position;
            if (id == "player") return PlayerFrameSettings.Position;
            if (id == "party") return PartyFramesSettings.GetPosition();
            if (id == "raid") return RaidFramesSettings.GetPosition();
            return RaidControlPanel.GetPosition();
        }

        private static void PersistPosition(string id, Vector2 position)
        {
            int bar;
            int auraGroup;
            if (TryAuraGroup(id, out auraGroup))
            {
                CooldownAuraSettings.SetFramePosition(auraGroup, position);
                return;
            }
            if (id == "minimap")
            {
                if (NativeMinimap != null) NativeMinimap.TryMove(position);
                return;
            }
            if (id == "enemytracker")
            {
                EnemyTrackerSettings.SetPosition(position);
                var tracker = Plugin.Instance != null && Plugin.Instance.UiRuntime != null
                    ? Plugin.Instance.UiRuntime.EnemyTracker : null;
                if (tracker != null) tracker.ApplyLayout();
                return;
            }
            if (id == "native-chat")
            {
                if (NativeChat != null && NativeChat.CanMove)
                {
                    NativeChat.TryEditorMove(position);
                    PlayerPrefs.Save();
                }
                return;
            }
            if (TryBar(id, out bar))
            {
                ActionBarPlanSettings.SetBarPosition(bar, position);
                var rect = Source(id);
                if (rect != null) rect.anchoredPosition = SavedPosition(id);
                return;
            }
            if (id == "xp")
            {
                ExperienceBarSettings.SetOffsetX(position.x);
                ExperienceBarSettings.SetOffsetY(position.y);
                var xp = Source("xp");
                if (xp != null) xp.anchoredPosition = SavedPosition("xp");
                return;
            }
            if (id == "fotf-primary" || id == "fotf-secondary" || id == "fotf-mass")
            {
                if (id == "fotf-primary") FOTFResourceSettings.SetPrimaryPosition(position);
                else if (id == "fotf-secondary") FOTFResourceSettings.SetSecondaryPosition(position);
                else FOTFResourceSettings.SetMassPosition(position);
                if (Plugin.Instance != null && Plugin.Instance.UiRuntime != null && Plugin.Instance.UiRuntime.PlayerFrame != null)
                    Plugin.Instance.UiRuntime.PlayerFrame.RefreshLayout();
                return;
            }
            if (id == "target")
            {
                TargetFrameSettings.SetPosition(position);
                if (Plugin.Instance != null && Plugin.Instance.UiRuntime != null && Plugin.Instance.UiRuntime.TargetFrame != null)
                    Plugin.Instance.UiRuntime.TargetFrame.RefreshLayout();
                return;
            }
            if (id == "player")
            {
                PlayerFrameSettings.SetPosition(position);
                if (Plugin.Instance != null && Plugin.Instance.UiRuntime != null && Plugin.Instance.UiRuntime.PlayerFrame != null)
                    Plugin.Instance.UiRuntime.PlayerFrame.RefreshLayout();
            }
            else if (id == "party") PartyFramesSettings.SetPosition(position);
            else if (id == "raid") RaidFramesSettings.SetPosition(position);
            else RaidControlPanel.SetPosition(position);
        }

        private void Build()
        {
            _layer = _canvas.CreateLayer("ArcaneUI_LayoutEditor");
            if (_layer == null) return;
            var overlayCanvas = _layer.gameObject.AddComponent<UnityEngine.Canvas>();
            overlayCanvas.overrideSorting = true;
            overlayCanvas.sortingOrder = 32761;
            _layer.gameObject.AddComponent<GraphicRaycaster>();
            // Catch clicks on empty space before native UI can receive them.
            var shield = ArcaneUiControls.Panel(_layer, "EditInputShield", 0, 0, 0, 0,
                new Color(0f, 0f, 0f, .12f));
            Stretch(shield);

            var gridRoot = ArcaneUiControls.Rect(_layer, "Grid", 0, 0, 0, 0);
            Stretch(gridRoot);
            _grid = gridRoot.gameObject.AddComponent<RawImage>();
            _grid.raycastTarget = false;
            _grid.color = Color.white;
            // Grid textures are created on demand in RefreshGrid() at the
            // selected step (10, 20 or 40), not at a hard-coded 64 pixels.

            CreateGhost("player");
            CreateGhost("target");
            CreateGhost("party");
            CreateGhost("raid");
            CreateGhost("manager");
            CreateGhost("xp");
            CreateGhost("fotf-primary");
            CreateGhost("fotf-secondary");
            CreateGhost("fotf-mass");
            CreateGhost("native-chat");
            CreateGhost("minimap");
            CreateGhost("enemytracker");
            for (int aura = 0; aura < 5; ++aura) CreateGhost("auraframe" + aura);
            for (int bar = 0; bar < ActionBarPlanSettings.MaxBars; ++bar)
                CreateGhost("bar" + bar);

            // Small magnetic alignment guides, behind the hitboxes (non-interactive).
            _guideVertical = ArcaneUiControls.Panel(_layer, "AlignGuideV", 0, 0, 1, 1,
                new Color(1f, .73f, .27f, .8f), false);
            _guideHorizontal = ArcaneUiControls.Panel(_layer, "AlignGuideH", 0, 0, 1, 1,
                new Color(1f, .73f, .27f, .8f), false);
            ClearGuides();

            // Retain the single top-centre layout toolbar; do not introduce a
            // second large editor or a settings-side mirror panel.
            _toolbar = ArcaneUiControls.Panel(_layer, "LayoutControls", 0, 0, 554, 142,
                new Color(Theme.Background.r, Theme.Background.g, Theme.Background.b, .97f));
            _toolbar.anchorMin = _toolbar.anchorMax = new Vector2(.5f, 1f);
            _toolbar.pivot = new Vector2(.5f, 1f);
            _toolbar.anchoredPosition = new Vector2(0f, -12f);
            ArcaneUiControls.Label(_toolbar, "Title", "ARCANEUI / EDIT LAYOUT   -   CTRL/SHIFT-CLICK TO MULTI-SELECT", 12, 4, 530, 23,
                12, Theme.Text, TextAnchor.MiddleLeft, FontStyle.Bold);
            ArcaneUiControls.Panel(_toolbar, "Divider", 12, 29, 530, 2, Theme.Accent, false);
            AddButton("GRID 20", 12, 37, 98, 29, CycleGrid);
            AddButton("SNAP ON", 116, 37, 98, 29, ToggleSnap);
            AddButton("GRID ON", 220, 37, 98, 29, ToggleGrid);
            AddButton("LOCK UI", 424, 37, 118, 29, () => Close(false));
            AddButton("UNDO", 12, 72, 83, 29, Undo);
            AddButton("REDO", 101, 72, 83, 29, Redo);
            AddButton("LOCK FRAME", 190, 72, 119, 29, ToggleSelectionLock);
            AddButton("REVERT", 315, 72, 99, 29, RevertSelected);
            AddButton("SETTINGS", 420, 72, 122, 29, OpenSelectedSettings);
            ArcaneUiControls.Label(_toolbar, "Help", "Arrow keys: move  |  Shift: 10px  |  Ctrl+Z/Y: undo/redo  |  Revert: on-open layout", 14, 105, 527, 28,
                11, Theme.Text);

            // The floating inspector edits actual module settings, not only
            // the ghost. Unsupported widths/heights remain configured in their
            // own settings pages via SETTINGS.
            _selectedXY = ArcaneUiControls.Panel(_layer, "SelectedFrameXY", 0, 0, 536, 49,
                new Color(Theme.Background.r, Theme.Background.g, Theme.Background.b, .98f));
            _selectedXYTitle = ArcaneUiControls.Label(_selectedXY, "Element", "", 7, 0, 520, 16,
                11, Theme.Text, TextAnchor.MiddleLeft, FontStyle.Bold);
            ArcaneUiControls.Label(_selectedXY, "XLabel", "X", 9, 19, 14, 24, 12, Theme.Text);
            _coordinateX = ArcaneUiControls.Search(_selectedXY, "ExactX", 25, 18, 64, 25,
                "X", Theme.Panel, Theme.Text, null);
            ArcaneUiControls.Label(_selectedXY, "YLabel", "Y", 94, 19, 14, 24, 12, Theme.Text);
            _coordinateY = ArcaneUiControls.Search(_selectedXY, "ExactY", 110, 18, 64, 25,
                "Y", Theme.Panel, Theme.Text, null);
            ArcaneUiControls.Button(_selectedXY, "ApplyXY", "APPLY XY", 181, 17, 87, 27,
                Theme.Panel, Theme.Text, ApplyExactCoordinates, 11);
            _sizeControls = ArcaneUiControls.Rect(_selectedXY, "SizeControls", 276, 0, 260, 48);
            ArcaneUiControls.Label(_sizeControls, "WLabel", "W", 2, 19, 16, 24, 12, Theme.Text);
            _coordinateWidth = ArcaneUiControls.Search(_sizeControls, "ExactWidth", 20, 18, 59, 25,
                "Width", Theme.Panel, Theme.Text, null);
            ArcaneUiControls.Label(_sizeControls, "HLabel", "H", 83, 19, 16, 24, 12, Theme.Text);
            _coordinateHeight = ArcaneUiControls.Search(_sizeControls, "ExactHeight", 101, 18, 59, 25,
                "Height", Theme.Panel, Theme.Text, null);
            ArcaneUiControls.Button(_sizeControls, "ApplySize", "SIZE", 165, 17, 88, 27,
                Theme.Panel, Theme.Text, ApplyExactSize, 11);
            _selectedXY.gameObject.SetActive(false);
            _toolbar.SetAsLastSibling();
            _layer.gameObject.SetActive(false);
        }

        private void SelectMinimap()
        {
            if (NativeMinimap != null) NativeMinimap.EnsureBinding();
            if (NativeMinimap == null || !NativeMinimap.CanEdit)
            {
                UpdateInfo(NativeMinimap != null ? NativeMinimap.Status : "Native minimap module unavailable.");
                return;
            }
            Select("minimap");
            UpdateInfo("Native minimap selected. Drag, scale with corner grip, or use exact offsets.");
        }

        private void SelectNativeChat()
        {
            NativeChatPresentation chat = NativeChat;
            if (chat != null) chat.EnsureEditorBinding();
            if (chat == null || !chat.CanSelectInEditor)
            {
                UpdateInfo(chat != null ? chat.Status : "Native chat has not been discovered yet. Open chat and retry.");
                return;
            }
            // Select the actual native transcript even if movement fails its
            // hierarchy safety test. An outline alone is still useful for
            // diagnosing which native window ArcaneUI has discovered.
            if (chat.CanMove)
            {
                ChatSettings.SetBool("MoveNative", true);
                chat.Refresh();
            }
            Select("native-chat");
            UpdateInfo(chat.CanMove ? "Native chat selected. Drag its outline or use X/Y." :
                "Native chat selected, but movement is disabled: " + chat.LayoutStatus);
        }




        private void CycleBarSelection()
        {
            if (!ActionBarMirrorSettings.Enabled)
            {
                UpdateInfo("Enable ArcaneUI Action Bars in settings to edit their positions.");
                return;
            }
            int current;
            int next = TryBar(_selected, out current) ? (current + 1) % ActionBarPlanSettings.Count : 0;
            Select("bar" + next);
        }

        private void AddButton(string name, float x, float y, float w, float h, Action click)
        {
            ArcaneUiControls.Button(_toolbar, "Btn_" + name, name, x, y, w, h,
                Theme.Panel, Theme.Text, click, 11);
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private void CreateGhost(string id)
        {
            var r = ArcaneUiControls.Panel(_layer, "Mover_" + id, 0, 0, 160, 50,
                new Color(.10f, .48f, .65f, .34f));
            var mover = r.gameObject.AddComponent<ArcaneUiLayoutMover>();
            mover.Editor = this;
            mover.ElementId = id;
            ArcaneUiControls.Label(r, "Caption", TitleFor(id), 7, 0, 200, 24,
                13, Color.white, TextAnchor.MiddleLeft, FontStyle.Bold);
            // Edges visually describe the underlying frame footprint.
            ArcaneUiControls.Panel(r, "Top", 0, 0, 0, 2, Theme.Accent, false);
            ArcaneUiControls.Panel(r, "Bottom", 0, 0, 0, 2, Theme.Accent, false);
            ArcaneUiControls.Panel(r, "Left", 0, 0, 2, 0, Theme.Accent, false);
            ArcaneUiControls.Panel(r, "Right", 0, 0, 2, 0, Theme.Accent, false);
            int barIndex;
            int auraGroup;
            if (id != "enemytracker" && id != "manager" && id != "xp" && id != "fotf-primary" && id != "fotf-secondary" &&
                id != "fotf-mass" && !TryAuraGroup(id, out auraGroup) && !TryBar(id, out barIndex))
            {
                var handle = ArcaneUiControls.Panel(r, "Resize", 0, 0, 18, 18,
                    new Color(1, 1, 1, .95f));
                handle.anchorMin = handle.anchorMax = new Vector2(1, 0);
                handle.pivot = new Vector2(1, 0);
                handle.anchoredPosition = Vector2.zero;
                var resize = handle.gameObject.AddComponent<ArcaneUiLayoutMover>();
                resize.Editor = this;
                resize.ElementId = id;
                resize.Resize = true;
            }
            _ghosts.Add(id, r);
        }

        private void RefreshGrid()
        {
            if (_grid == null || _layer == null) return;
            EnsureGridTexture();
            _grid.enabled = _showGrid;
            _lastGridCanvasSize = _layer.rect.size;
            // One UV tile per grid step. Because the texture itself is now
            // gridSize x gridSize, each line stays one actual texture pixel
            // wide rather than shrinking below a pixel at 10/20 spacing.
            _grid.uvRect = new Rect(0f, 0f,
                _lastGridCanvasSize.x / _gridSize,
                _lastGridCanvasSize.y / _gridSize);
            UpdateButton("Btn_GRID 20", "GRID " + _gridSize);
            UpdateButton("Btn_SNAP ON", _snap ? "SNAP ON" : "SNAP OFF");
            UpdateButton("Btn_GRID ON", _showGrid ? "GRID ON" : "GRID OFF");
        }

        private void EnsureGridTexture()
        {
            if (_gridTexture != null && _gridTextureSize == _gridSize) return;

            int tileSize = _gridSize;
            var texture = new Texture2D(tileSize, tileSize, TextureFormat.RGBA32, false);
            texture.name = "ArcaneUI_Grid_" + tileSize;
            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Repeat;

            var pixels = new Color32[tileSize * tileSize];
            var line = new Color32(130, 199, 230, 160);
            var clear = new Color32(0, 0, 0, 0);
            for (int y = 0; y < tileSize; y++)
                for (int x = 0; x < tileSize; x++)
                    pixels[y * tileSize + x] = (x == 0 || y == 0) ? line : clear;

            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            var old = _gridTexture;
            _gridTexture = texture;
            _gridTextureSize = tileSize;
            _grid.texture = texture;
            if (old != null) UnityEngine.Object.Destroy(old);
        }

        private void UpdateButton(string path, string text)
        {
            if (_toolbar == null) return;
            var child = _toolbar.Find(path);
            if (child == null) return;
            var label = child.GetComponentInChildren<Text>();
            if (label != null) label.text = text;
        }

        private void UpdateBarButton()
        {
            int current;
            UpdateButton("Btn_BAR 1", TryBar(_selected, out current) ? "BAR " + (current + 1) : "BARS");
        }

        private void CycleGrid()
        {
            _gridSize = _gridSize == 10 ? 20 : _gridSize == 20 ? 40 : 10;
            PlayerPrefs.SetInt("ArcaneUI.Layout.GridSize", _gridSize);
            PlayerPrefs.Save();
            RefreshGrid();
        }

        private void ToggleSnap()
        {
            _snap = !_snap;
            PlayerPrefs.SetInt("ArcaneUI.Layout.Snap", _snap ? 1 : 0);
            PlayerPrefs.Save();
            RefreshGrid();
        }

        private void ToggleGrid()
        {
            _showGrid = !_showGrid;
            PlayerPrefs.SetInt("ArcaneUI.Layout.ShowGrid", _showGrid ? 1 : 0);
            PlayerPrefs.Save();
            RefreshGrid();
        }

        private static bool ModifierAdditive()
        {
            return Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl) ||
                   Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        }

        private static bool GroupEligible(string id)
        {
            // These controls have separate native coordinate systems or transient
            // FOTF drag lifetimes and must never be translated as a group.
            return id != "native-chat" && id != "minimap" &&
                id != "fotf-primary" && id != "fotf-secondary" && id != "fotf-mass";
        }

        private static bool IsLocked(string id)
        {
            return !string.IsNullOrEmpty(id) &&
                PlayerPrefs.GetInt("ArcaneUI.Layout.Lock." + id, 0) == 1;
        }

        internal void Select(string id)
        {
            Select(id, false);
        }

        internal void Select(string id, bool additive)
        {
            if (!_ghosts.ContainsKey(id) || !CanEdit(id)) return;
            if (!additive || !GroupEligible(id)) _selectedIds.Clear();
            if (additive && GroupEligible(id) && _selectedIds.Contains(id))
            {
                _selectedIds.Remove(id);
                if (_selectedIds.Count == 0) _selected = null;
                else if (_selected == id)
                {
                    _selected = null;
                    foreach (string other in _selectedIds) { _selected = other; break; }
                }
            }
            else
            {
                _selectedIds.Add(id);
                _selected = id;
            }
            RefreshGhosts();
        }

        private void ToggleSelectionLock()
        {
            if (string.IsNullOrEmpty(_selected) || !CanEdit(_selected)) return;
            PlayerPrefs.SetInt("ArcaneUI.Layout.Lock." + _selected, IsLocked(_selected) ? 0 : 1);
            PlayerPrefs.Save();
            RefreshGhosts();
        }

        private void OpenSelectedSettings()
        {
            if (string.IsNullOrEmpty(_selected)) return;
            Close(true);
        }

        private Vector2 ToOverlay(Vector3 world)
        {
            Vector2 screen = RectTransformUtility.WorldToScreenPoint(null, world);
            Vector2 local;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_layer, screen, null, out local);
            return new Vector2(local.x - _layer.rect.xMin, local.y - _layer.rect.yMax);
        }

        private Vector2 Pointer(PointerEventData evt)
        {
            Vector2 local;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _layer, evt.position, evt.pressEventCamera, out local)) return Vector2.zero;
            return new Vector2(local.x - _layer.rect.xMin, local.y - _layer.rect.yMax);
        }

        private Vector2 OverlayToSource(string id, Vector2 position)
        {
            var source = Source(id);
            int bar;
            int auraGroup;
            if (TryAuraGroup(id, out auraGroup))
                return SavedPosition(id) + position - _ghosts[id].anchoredPosition;
            if (id == "enemytracker")
            {
                if (source == null) return SavedPosition(id);
                Vector3[] bounds = new Vector3[4]; source.GetWorldCorners(bounds);
                Vector3 targetWorld = _layer.TransformPoint(new Vector3(
                    _layer.rect.xMin + position.x, _layer.rect.yMax + position.y, 0f));
                var parentRect = source.parent as RectTransform;
                if (parentRect == null) return SavedPosition(id);
                Vector3 diff = parentRect.InverseTransformVector(targetWorld - bounds[1]);
                return SavedPosition(id) + new Vector2(diff.x, diff.y);
            }
            if (id == "minimap")
            {
                var nativeMap = Source(id);
                if (nativeMap == null || !_ghosts.ContainsKey(id)) return SavedPosition(id);
                // Ghost and native map are on different canvases. Their world-space
                // top-left delta converts to a local offset via the native parent.
                Vector3[] corners = new Vector3[4]; nativeMap.GetWorldCorners(corners);
                Vector3 desired = _layer.TransformPoint(new Vector3(
                    _layer.rect.xMin + position.x, _layer.rect.yMax + position.y, 0f));
                RectTransform mapParent = nativeMap.parent as RectTransform;
                if (mapParent == null) return SavedPosition(id);
                Vector3 delta = mapParent.InverseTransformVector(desired - corners[1]);
                return SavedPosition(id) + new Vector2(delta.x, delta.y);
            }
            if (id == "native-chat")
            {
                // Convert the visible transcript's top-left through the
                // editor and chat canvases; save native offsets, not the root's
                // anchored position (which is a different coordinate space).
                NativeChatPresentation native = NativeChat;
                Vector3 topLeft, bottomRight;
                if (native == null || !native.TryGetEditorBounds(out topLeft, out bottomRight))
                    return SavedPosition(id);
                Vector3 desired = _layer.TransformPoint(new Vector3(
                    _layer.rect.xMin + position.x, _layer.rect.yMax + position.y, 0f));
                return native.EditorOffsetForWorldTopLeft(desired);
            }
            if (TryBar(id, out bar) || id == "xp")
            {
                // Action bars use bottom-centre anchors, unlike top-left
                // unit frames. Convert the ghost's top-left into the bar's
                // actual pivot, then into its parent's local coordinates.
                Vector2 size = _ghosts[id].sizeDelta;
                Vector3 barWorld = _layer.TransformPoint(new Vector3(
                    _layer.rect.xMin + position.x + size.x * .5f,
                    _layer.rect.yMax + position.y - size.y * (id == "xp" ? .5f : 1f), 0f));
                RectTransform barParent = source != null ? source.parent as RectTransform : null;
                if (barParent == null) return SavedPosition(id);
                Vector2 barLocal;
                Vector2 barScreen = RectTransformUtility.WorldToScreenPoint(null, barWorld);
                if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(barParent, barScreen, null, out barLocal))
                    return SavedPosition(id);
                Vector2 anchor = new Vector2(
                    Mathf.Lerp(barParent.rect.xMin, barParent.rect.xMax, source.anchorMin.x),
                    Mathf.Lerp(barParent.rect.yMin, barParent.rect.yMax, source.anchorMin.y));
                return barLocal - anchor;
            }
            if (source == null || source.parent == null)
                return SavedPosition(id) + position - _ghosts[id].anchoredPosition;
            var parent = source.parent as RectTransform;
            if (parent == null) return SavedPosition(id);
            Vector3 world = _layer.TransformPoint(new Vector3(
                _layer.rect.xMin + position.x, _layer.rect.yMax + position.y, 0f));
            Vector2 screen = RectTransformUtility.WorldToScreenPoint(null, world);
            Vector2 local;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, screen, null, out local))
                return SavedPosition(id);
            return new Vector2(local.x - parent.rect.xMin, local.y - parent.rect.yMax);
        }

        private void RefreshGhosts()
        {
            // No default selection: every enabled frame is immediately clickable.
            // Do not silently reselect Party Frames when the minimap is unavailable.
            if (!string.IsNullOrEmpty(_selected) && !CanEdit(_selected))
                _selected = null;
            _selectedIds.RemoveWhere(id => !CanEdit(id));
            if (string.IsNullOrEmpty(_selected)) _selectedIds.Clear();
            foreach (var pair in _ghosts)
            {
                var ghost = pair.Value;
                if (!CanEdit(pair.Key))
                {
                    ghost.gameObject.SetActive(false);
                    continue;
                }
                ghost.gameObject.SetActive(true);
                int auraId;
                // Always use configured capacity, not today's active effect count.
                // This keeps each editable outline stable even when spells expire.
                bool auraPlaceholder = TryAuraGroup(pair.Key, out auraId);
                var source = pair.Key == "fotf-primary" || pair.Key == "fotf-secondary" || pair.Key == "fotf-mass" || auraPlaceholder
                    ? null : Source(pair.Key);
                Vector2 pos;
                Vector2 dimensions;
                if (pair.Key == "native-chat" && NativeChat != null)
                {
                    Vector3 nativeTopLeft, nativeBottomRight;
                    if (NativeChat.TryGetEditorBounds(out nativeTopLeft, out nativeBottomRight))
                    {
                        pos = ToOverlay(nativeTopLeft);
                        Vector2 end = ToOverlay(nativeBottomRight);
                        dimensions = new Vector2(Mathf.Abs(end.x - pos.x), Mathf.Abs(end.y - pos.y));
                    }
                    else
                    {
                        ghost.gameObject.SetActive(false);
                        continue;
                    }
                }
                else if (source != null)
                {
                    var corners = new Vector3[4];
                    source.GetWorldCorners(corners);
                    pos = ToOverlay(corners[1]);
                    Vector2 bottomRight = ToOverlay(corners[3]);
                    dimensions = new Vector2(Mathf.Abs(bottomRight.x - pos.x),
                        Mathf.Abs(bottomRight.y - pos.y));
                }
                else
                {
                    int bar;
                            if (pair.Key == "xp")
                    {
                        dimensions = new Vector2(ExperienceBarSettings.Width, ExperienceBarSettings.Height);
                        Vector2 saved = SavedPosition(pair.Key);
                        pos = new Vector2(_layer.rect.width * .5f + saved.x - dimensions.x * .5f,
                            -_layer.rect.height + saved.y + dimensions.y * .5f);
                    }
                    else if (pair.Key == "fotf-primary" || pair.Key == "fotf-secondary" || pair.Key == "fotf-mass")
                    {
                        dimensions = new Vector2(PlayerFrameSettings.Width *
                            FOTFResourceSettings.WidthScale(pair.Key == "fotf-primary" ? FOTFResourcePart.Primary :
                                pair.Key == "fotf-secondary" ? FOTFResourcePart.Secondary : FOTFResourcePart.Mass) / 100f,
                            FOTFResourceSettings.PartHeight(pair.Key == "fotf-primary" ? FOTFResourcePart.Primary :
                                pair.Key == "fotf-secondary" ? FOTFResourcePart.Secondary : FOTFResourcePart.Mass));
                        pos = SavedPosition(pair.Key);
                    }
                    else if (TryAuraGroup(pair.Key, out auraId))
                    {
                        // Layout capacity represents the configured icon slots,
                        // even if there are zero live icons to render.
                        var f = CooldownAuraSettings.Frame(auraId);
                        int perLine = Mathf.Clamp(f.Columns, 1, f.MaxIcons);
                        int lines = Mathf.CeilToInt(f.MaxIcons / (float)perLine);
                        bool vertical = f.Growth == 1 || f.Growth == 3;
                        int horizontalSlots = vertical ? lines : perLine;
                        int verticalSlots = vertical ? perLine : lines;
                        int step = f.IconSize + f.Spacing;
                        dimensions = new Vector2(
                            Mathf.Max(80f, horizontalSlots * step - f.Spacing),
                            (f.ShowTitle ? 18f : 0f) + verticalSlots * step - f.Spacing);
                        pos = f.Position;
                    }
                    else if (TryBar(pair.Key, out bar))
                    {
                        int count = ActionBarPlanSettings.SlotCount(bar);
                        int columns = Mathf.Clamp(ActionBarPlanSettings.Columns(bar), 1, count);
                        int rows = Mathf.CeilToInt(count / (float)columns);
                        int iconSize = ActionBarStyleSettings.IconSize;
                        int spacing = ActionBarStyleSettings.Spacing;
                        dimensions = new Vector2(columns * iconSize + (columns - 1) * spacing + 12,
                            rows * iconSize + (rows - 1) * spacing + 12);
                        Vector2 saved = SavedPosition(pair.Key);
                        pos = new Vector2(_layer.rect.width * .5f + saved.x - dimensions.x * .5f,
                            -_layer.rect.height + saved.y + dimensions.y);
                    }
                    else
                    {
                        pos = SavedPosition(pair.Key);
                        dimensions = pair.Key == "target"
                            ? new Vector2(TargetFrameSettings.Width, TargetFrameSettings.Height)
                            : pair.Key == "player"
                            ? new Vector2(PlayerFrameSettings.Width, PlayerFrameSettings.Height)
                            : pair.Key == "party"
                            ? new Vector2(PartyFramesSettings.FrameWidth + 22,
                                22 + 5 * PartyFramesSettings.FrameHeight + 4 * PartyFramesSettings.MemberSpacing)
                            : pair.Key == "raid"
                                ? new Vector2(3 * RaidFramesSettings.FrameWidth + 2 * RaidFramesSettings.GroupSpacing,
                                    22 + 6 * RaidFramesSettings.FrameHeight + 5 * RaidFramesSettings.MemberSpacing)
                                : new Vector2(390, 300);
                    }
                }
                ghost.anchoredPosition = pos;
                ghost.sizeDelta = dimensions;
                ghost.GetComponent<Image>().color = _selectedIds.Contains(pair.Key)
                    ? (IsLocked(pair.Key) ? new Color(.68f, .34f, .13f, .25f)
                        : new Color(.11f, .48f, .64f, .24f))
                    : (IsLocked(pair.Key) ? new Color(.62f, .34f, .17f, .08f)
                        : new Color(.10f, .40f, .54f, .055f));
                var caption = ghost.Find("Caption");
                if (caption != null)
                {
                    var title = caption.GetComponent<Text>();
                    if (title != null) title.text = TitleFor(pair.Key) + (IsLocked(pair.Key) ? " [LOCKED]" : "");
                }
                var top = ghost.Find("Top") as RectTransform;
                var bottom = ghost.Find("Bottom") as RectTransform;
                var left = ghost.Find("Left") as RectTransform;
                var right = ghost.Find("Right") as RectTransform;
                if (top != null) top.sizeDelta = new Vector2(dimensions.x, 2);
                if (bottom != null) { bottom.anchoredPosition = new Vector2(0, -dimensions.y + 2); bottom.sizeDelta = new Vector2(dimensions.x, 2); }
                if (left != null) left.sizeDelta = new Vector2(2, dimensions.y);
                if (right != null) { right.anchoredPosition = new Vector2(dimensions.x - 2, 0); right.sizeDelta = new Vector2(2, dimensions.y); }
                Color borderColour = IsLocked(pair.Key)
                    ? new Color(.96f, .57f, .25f, _selectedIds.Contains(pair.Key) ? 1f : .6f)
                    : _selectedIds.Contains(pair.Key) ? Theme.Accent
                    : new Color(Theme.Accent.r, Theme.Accent.g, Theme.Accent.b, .46f);
                if (top != null) top.GetComponent<Image>().color = borderColour;
                if (bottom != null) bottom.GetComponent<Image>().color = borderColour;
                if (left != null) left.GetComponent<Image>().color = borderColour;
                if (right != null) right.GetComponent<Image>().color = borderColour;
                // Resize grips are hidden until the frame is selected. Do not
                // offer a grip for FOTF Bone Mass: its size is configured in
                // FOTF Resources, not by the native frame resize path.
                var handle = ghost.Find("Resize");
                if (handle != null) handle.gameObject.SetActive(pair.Key == _selected &&
                    !IsLocked(pair.Key) && _selectedIds.Count == 1 &&
                    (pair.Key != "native-chat" || NativeChat != null && NativeChat.CanResize));
                var label = ghost.Find("Caption") as RectTransform;
                if (label != null) label.sizeDelta = new Vector2(Mathf.Max(40, dimensions.x - 12), 24);
            }
            // Selected frame must win hit-testing over overlapping frames, while
            // the global toolbar and its coordinate bubble remain on top.
            RectTransform selectedGhost;
            if (!string.IsNullOrEmpty(_selected) && _ghosts.TryGetValue(_selected, out selectedGhost) &&
                selectedGhost != null && selectedGhost.gameObject.activeSelf)
                selectedGhost.SetAsLastSibling();
            UpdateSelectedXYPlacement();
            if (_selectedXY != null && _selectedXY.gameObject.activeSelf) _selectedXY.SetAsLastSibling();
            if (_toolbar != null) _toolbar.SetAsLastSibling();
            UpdateButton("Btn_LOCK FRAME", IsLocked(_selected) ? "UNLOCK FRAME" : "LOCK FRAME");
            UpdateInfo(null);
        }

        private void UpdateSelectedXYPlacement()
        {
            if (_selectedXY == null || _layer == null) return;
            RectTransform ghost;
            if (string.IsNullOrEmpty(_selected) || !_ghosts.TryGetValue(_selected, out ghost) ||
                ghost == null || !ghost.gameObject.activeSelf || !CanEdit(_selected))
            {
                _selectedXY.gameObject.SetActive(false);
                return;
            }
            _selectedXY.gameObject.SetActive(true);
            if (_selectedXYTitle != null) _selectedXYTitle.text = TitleFor(_selected) +
                (_selectedIds.Count > 1 ? "  (" + _selectedIds.Count + " selected)" : "") +
                (IsLocked(_selected) ? "  [LOCKED]" : "");
            if (_sizeControls != null) _sizeControls.gameObject.SetActive(_selectedIds.Count == 1 && CanResizeExact(_selected));
            // Keep the floating X/Y editor attached to the selected outline.
            // Prefer below the frame; near the screen bottom, dock just above it.
            float x = Mathf.Clamp(ghost.anchoredPosition.x, 0f,
                Mathf.Max(0f, _layer.rect.width - _selectedXY.rect.width));
            float belowY = ghost.anchoredPosition.y - ghost.rect.height - 5f;
            float aboveY = ghost.anchoredPosition.y + _selectedXY.rect.height + 5f;
            float y = belowY - _selectedXY.rect.height < -_layer.rect.height ? aboveY : belowY;
            y = Mathf.Clamp(y, -Mathf.Max(0f, _layer.rect.height - _selectedXY.rect.height), 0f);
            _selectedXY.anchoredPosition = new Vector2(x, y);
        }

        private void UpdateInfo(string message)
        {
            if (_coordinateX == null || _coordinateY == null || string.IsNullOrEmpty(_selected) ||
                !CanEdit(_selected)) return;
            Vector2 p = CurrentPosition(_selected);
            if (!_coordinateX.isFocused)
                _coordinateX.SetTextWithoutNotify(p.x.ToString("0.##", CultureInfo.InvariantCulture));
            if (!_coordinateY.isFocused)
                _coordinateY.SetTextWithoutNotify(p.y.ToString("0.##", CultureInfo.InvariantCulture));
            if (_sizeControls != null && _sizeControls.gameObject.activeSelf &&
                _coordinateWidth != null && _coordinateHeight != null)
            {
                Vector2 size = CurrentEditableSize(_selected);
                if (!_coordinateWidth.isFocused)
                    _coordinateWidth.SetTextWithoutNotify(size.x.ToString("0.##", CultureInfo.InvariantCulture));
                if (!_coordinateHeight.isFocused)
                    _coordinateHeight.SetTextWithoutNotify(size.y.ToString("0.##", CultureInfo.InvariantCulture));
            }
            // Detailed errors remain available in module diagnostics; don't add
            // another large console or developer-only row to the compact editor.
        }

        private static Vector2 CurrentPosition(string id)
        {
            int auraGroup;
            if (TryAuraGroup(id, out auraGroup)) return SavedPosition(id);
            if (id == "native-chat" || id == "minimap" || id == "enemytracker" || id == "fotf-primary" || id == "fotf-secondary" || id == "fotf-mass") return SavedPosition(id);
            var rect = Source(id);
            return rect != null ? rect.anchoredPosition : SavedPosition(id);
        }

        private void ApplyExactCoordinates()
        {
            if (string.IsNullOrEmpty(_selected) || !CanEdit(_selected) || IsLocked(_selected) ||
                _selectedIds.Count != 1) return;
            if (_selected == "native-chat" && (NativeChat == null || !NativeChat.CanMove))
            {
                UpdateInfo(NativeChat != null ? NativeChat.LayoutStatus : "Native chat is not available.");
                return;
            }
            if (_coordinateX == null || _coordinateY == null) return;
            float x, y;
            if (!float.TryParse(_coordinateX.text, NumberStyles.Float, CultureInfo.InvariantCulture, out x) ||
                !float.TryParse(_coordinateY.text, NumberStyles.Float, CultureInfo.InvariantCulture, out y) ||
                float.IsNaN(x) || float.IsNaN(y) || float.IsInfinity(x) || float.IsInfinity(y))
            {
                UpdateInfo("Enter valid numeric X/Y coordinates.");
                return;
            }
            var rect = Source(_selected);
            int bar;
            if (_selected == "minimap")
            {
                x = Mathf.Clamp(x, -1300f, 1300f); y = Mathf.Clamp(y, -1000f, 1000f);
            }
            else if (_selected == "enemytracker")
            {
                x = Mathf.Clamp(x, -1920f, 0f);
                y = Mathf.Clamp(y, -1100f, 0f);
            }
            else if (_selected == "native-chat")
            {
                x = Mathf.Clamp(x, -900f, 900f);
                y = Mathf.Clamp(y, -900f, 900f);
            }
            else if (_selected == "xp")
            {
                x = Mathf.Clamp(x, -900f, 900f);
                y = Mathf.Clamp(y, 0f, 750f);
            }
            else if (TryBar(_selected, out bar))
            {
                x = Mathf.Clamp(x, -800f, 800f);
                y = Mathf.Clamp(y, 0f, 900f);
            }
            else if (rect != null && rect.parent is RectTransform)
            {
                var parent = (RectTransform)rect.parent;
                x = Mathf.Clamp(x, 0f, Mathf.Max(0f, parent.rect.width - rect.rect.width));
                y = Mathf.Clamp(y, -Mathf.Max(0f, parent.rect.height - rect.rect.height), 0f);
            }
            PersistPosition(_selected, new Vector2(x, y));
            Record();
            RefreshGhosts();
            UpdateInfo("Applied precise coordinates.");
        }

        private static bool CanResizeExact(string id)
        {
            return id == "player" || id == "target" || id == "party" || id == "raid" ||
                (id == "native-chat" && NativeChat != null && NativeChat.CanResize);
        }

        private static Vector2 CurrentEditableSize(string id)
        {
            if (id == "player") return new Vector2(PlayerFrameSettings.Width, PlayerFrameSettings.Height);
            if (id == "target") return new Vector2(TargetFrameSettings.Width, TargetFrameSettings.Height);
            if (id == "party") return new Vector2(PartyFramesSettings.FrameWidth, PartyFramesSettings.FrameHeight);
            if (id == "raid") return new Vector2(RaidFramesSettings.FrameWidth, RaidFramesSettings.FrameHeight);
            return new Vector2(ChatSettings.Width, ChatSettings.Height);
        }

        private void ApplyExactSize()
        {
            if (string.IsNullOrEmpty(_selected) || !CanEdit(_selected) || !CanResizeExact(_selected) ||
                IsLocked(_selected) || _selectedIds.Count != 1 || _coordinateWidth == null ||
                _coordinateHeight == null) return;
            float width, height;
            if (!float.TryParse(_coordinateWidth.text, NumberStyles.Float, CultureInfo.InvariantCulture, out width) ||
                !float.TryParse(_coordinateHeight.text, NumberStyles.Float, CultureInfo.InvariantCulture, out height) ||
                float.IsNaN(width) || float.IsNaN(height) || float.IsInfinity(width) || float.IsInfinity(height)) return;
            if (_selected == "native-chat")
            {
                _pendingResizeWidth = Mathf.Clamp(Mathf.Round(width), 180f, 1000f);
                _pendingResizeHeight = Mathf.Clamp(Mathf.Round(height), 75f, 750f);
            }
            else
            {
                bool unit = _selected == "player" || _selected == "target";
                _pendingResizeWidth = Mathf.Clamp(Mathf.Round(width), unit ? 130f : 90f, unit ? 450f : 240f);
                _pendingResizeHeight = Mathf.Clamp(Mathf.Round(height), unit ? 42f : 24f, unit ? 130f : 70f);
            }
            CommitResize(_selected);
            Record();
            RefreshGhosts();
        }

        internal void BeginDrag(string id, bool resize, PointerEventData evt)
        {
            if (!IsOpen || !_ghosts.ContainsKey(id) || evt == null) return;
            // A drag on any already-selected member moves the selection as a
            // unit. A fresh drag starts a single selection unless Ctrl/Shift
            // is held. Resize always acts on one frame only.
            if (_selectedIds.Count > 1 && _selectedIds.Contains(id) && !resize)
                _selected = id;
            else if (!(_selectedIds.Count == 1 && _selectedIds.Contains(id)))
                Select(id, ModifierAdditive() && !resize);
            if (!CanEdit(id) || IsLocked(id) || (resize && _selectedIds.Count != 1)) return;
            if (id == "native-chat" && (NativeChat == null || !NativeChat.CanMove))
            {
                UpdateInfo(NativeChat != null ? NativeChat.LayoutStatus : "Native chat is not available.");
                return;
            }
            _dragged = id;
            _groupDragging = !resize && _selectedIds.Count > 1 && GroupEligible(id);
            _groupOrigins.Clear();
            if (_groupDragging)
            {
                foreach (string member in _selectedIds)
                {
                    RectTransform g;
                    if (CanEdit(member) && GroupEligible(member) && !IsLocked(member) &&
                        _ghosts.TryGetValue(member, out g) && g.gameObject.activeSelf)
                        _groupOrigins[member] = g.anchoredPosition;
                }
                if (_groupOrigins.Count < 2) _groupDragging = false;
            }
            int bar;
            int auraGroup;
            _resizing = resize && (id != "native-chat" || NativeChat != null && NativeChat.CanResize) &&
                id != "manager" && id != "xp" &&
                id != "fotf-primary" && id != "fotf-secondary" && id != "fotf-mass" &&
                !TryAuraGroup(id, out auraGroup) && !TryBar(id, out bar);
            _changedDuringDrag = false;
            _startPoint = Pointer(evt);
            var ghost = _ghosts[id];
            _dragOffset = ghost.anchoredPosition - _startPoint;
            _startGhostPosition = ghost.anchoredPosition;
            _startNativePosition = SavedPosition(id);
            _startSize = ghost.sizeDelta;
            if (id == "minimap")
            {
                _startMapScale = MinimapSettings.Scale;
                _startFrameWidth = _startSize.x;
                _startFrameHeight = _startSize.y;
                return;
            }
            if (id == "native-chat")
            {
                Vector2 nativeSize = NativeChat != null && NativeChat.NativeEditorRect != null
                    ? NativeChat.NativeEditorRect.rect.size : _startSize;
                _startFrameWidth = nativeSize.x;
                _startFrameHeight = nativeSize.y;
                return;
            }
            _startFrameWidth = id == "target" ? TargetFrameSettings.Width : id == "player" ? PlayerFrameSettings.Width : id == "party" ? PartyFramesSettings.FrameWidth : RaidFramesSettings.FrameWidth;
            _startFrameHeight = id == "target" ? TargetFrameSettings.Height : id == "player" ? PlayerFrameSettings.Height : id == "party" ? PartyFramesSettings.FrameHeight : RaidFramesSettings.FrameHeight;
        }

        internal void Drag(string id, PointerEventData evt)
        {
            if (_dragged != id || evt == null) return;
            Vector2 pointer = Pointer(evt);
            if (_resizing)
            {
                float dx = pointer.x - _startPoint.x;
                float dy = pointer.y - _startPoint.y;
                // Corner drags grow towards the right/bottom; native frame
                // settings are per-member dimensions, not group container size.
                    if (id == "minimap")
                {
                    float next = Mathf.Clamp(_startMapScale *
                        Mathf.Max(.5f, (_startFrameWidth + dx) / Mathf.Max(1f, _startFrameWidth)), 50f, 200f);
                    MinimapSettings.SetFloat("Scale", next);
                    if (NativeMinimap != null) NativeMinimap.Refresh();
                    _changedDuringDrag = true;
                    RefreshGhosts();
                    return;
                }
                if (id == "native-chat")
                {
                    if (NativeChat == null || !NativeChat.CanResize) return;
                    _pendingResizeWidth = Mathf.Clamp(Mathf.Round(_startFrameWidth + dx), 180f, 1000f);
                    _pendingResizeHeight = Mathf.Clamp(Mathf.Round(_startFrameHeight - dy), 75f, 750f);
                    _changedDuringDrag = true;
                    if (Time.unscaledTime - _lastResizeRebuild >= .08f)
                    {
                        CommitResize(id);
                        _lastResizeRebuild = Time.unscaledTime;
                        RefreshGhosts();
                    }
                    return;
                }
                float width = _startFrameWidth + dx / (id == "raid" ? 3f : 1f);
                float height = _startFrameHeight - dy / (id == "raid" ? 6f : id == "player" || id == "target" ? 1f : 5f);
                width = Mathf.Clamp(Mathf.Round(width), id == "player" || id == "target" ? 130f : 90f, id == "player" || id == "target" ? 450f : 240f);
                height = Mathf.Clamp(Mathf.Round(height), id == "player" || id == "target" ? 42f : 24f, id == "player" || id == "target" ? 130f : 70f);
                _pendingResizeWidth = width;
                _pendingResizeHeight = height;
                _changedDuringDrag = true;
                // Keep the resize grip responsive; expensive saves and native
                // frame rebuilds run at most ~12 times/second during drag.
                _ghosts[id].sizeDelta = new Vector2(
                    Mathf.Max(30f, _startSize.x + dx),
                    Mathf.Max(30f, _startSize.y - dy));
                if (Time.unscaledTime - _lastResizeRebuild >= .08f)
                {
                    CommitResize(id);
                    _lastResizeRebuild = Time.unscaledTime;
                    RefreshGhosts();
                }
            }
            else
            {
                if (_groupDragging)
                {
                    DragGroup(id, pointer);
                    return;
                }
                Vector2 pos = pointer + _dragOffset;
                if (_snap) pos = SnapWithGuides(id, pos);
                else ClearGuides();
                var ghost = _ghosts[id];
                pos.x = Mathf.Clamp(pos.x, 0f, Mathf.Max(0, _layer.rect.width - ghost.rect.width));
                pos.y = Mathf.Clamp(pos.y, -Mathf.Max(0, _layer.rect.height - ghost.rect.height), 0);
                var source = Source(id);
                Vector2 sourcePosition = source != null ? OverlayToSource(id, pos) : Vector2.zero;
                ghost.anchoredPosition = pos;
                UpdateSelectedXYPlacement();
                if (id == "fotf-primary" || id == "fotf-secondary" || id == "fotf-mass")
                {
                    // The live FOTF renderer refreshes every 50ms. Give it the
                    // editor's in-memory position instead of fighting a saved
                    // coordinate and snapping the strip back during dragging.
                    if (id == "fotf-mass") FOTFResourceSettings.SetMassDragPosition(
                        source != null ? sourcePosition : pos);
                    else FOTFResourceSettings.SetDragPosition(id == "fotf-secondary",
                        source != null ? sourcePosition : pos);
                }
                    int movingAura;
                if (TryAuraGroup(id, out movingAura))
                {
                    CooldownAuraSettings.SetFrameDragPosition(movingAura, sourcePosition);
                    if (source != null) source.anchoredPosition = sourcePosition;
                    _changedDuringDrag = true;
                    UpdateInfo(null);
                    return;
                }
                if (id == "enemytracker")
                {
                    // Save once at EndDrag; interim frame position follows pointer.
                    EnemyTrackerSettings.SetDragPosition(sourcePosition);
                    if (Plugin.Instance != null && Plugin.Instance.UiRuntime != null && Plugin.Instance.UiRuntime.EnemyTracker != null)
                        Plugin.Instance.UiRuntime.EnemyTracker.ApplyLayout();
                    _changedDuringDrag = true;
                    UpdateInfo(null);
                    return;
                }
                if (id == "minimap")
                {
                    if (NativeMinimap == null || !NativeMinimap.TryMove(sourcePosition))
                    {
                        RefreshGhosts(); return;
                    }
                    _changedDuringDrag = true;
                    UpdateInfo(null);
                    return;
                }
                if (id == "native-chat")
                {
                    // A native chat is not a centre-anchored mirror. Never
                    // assign offsets directly to its RectTransform.
                    if (NativeChat == null || !NativeChat.TryEditorMove(sourcePosition))
                    {
                        RefreshGhosts();
                        return;
                    }
                    _changedDuringDrag = true;
                    UpdateInfo(null);
                    return;
                }
                if (source != null) source.anchoredPosition = sourcePosition;
                _changedDuringDrag = true;
                UpdateInfo(null);
            }
        }

        private void DragGroup(string id, Vector2 pointer)
        {
            Vector2 leader;
            if (!_groupOrigins.TryGetValue(id, out leader)) return;
            Vector2 desired = leader + pointer - _startPoint;
            if (_snap) desired = SnapWithGuides(id, desired);
            else ClearGuides();
            Vector2 delta = desired - leader;
            // Keep the whole selection visible instead of clamping each member
            // separately (which would destroy spacing between frames).
            foreach (var pair in _groupOrigins)
            {
                RectTransform ghost = _ghosts[pair.Key];
                float left = pair.Value.x;
                float top = pair.Value.y;
                delta.x = Mathf.Max(delta.x, -left);
                delta.x = Mathf.Min(delta.x, _layer.rect.width - ghost.rect.width - left);
                delta.y = Mathf.Max(delta.y, -_layer.rect.height + ghost.rect.height - top);
                delta.y = Mathf.Min(delta.y, -top);
            }
            if (Time.unscaledTime < _nextGroupRefresh) return;
            _nextGroupRefresh = Time.unscaledTime + .033f;
            foreach (var pair in _groupOrigins)
            {
                string member = pair.Key;
                Vector2 target = pair.Value + delta;
                RectTransform ghost = _ghosts[member];
                if ((ghost.anchoredPosition - target).sqrMagnitude < .01f) continue;
                Vector2 sourcePosition = OverlayToSource(member, target);
                PersistPosition(member, sourcePosition);
                ghost.anchoredPosition = target;
                _changedDuringDrag = true;
            }
            UpdateSelectedXYPlacement();
        }

        private void ClearGuides()
        {
            if (_guideVertical != null) _guideVertical.gameObject.SetActive(false);
            if (_guideHorizontal != null) _guideHorizontal.gameObject.SetActive(false);
        }

        private void DisplayGuides(float? x, float? y)
        {
            if (_guideVertical != null)
            {
                _guideVertical.gameObject.SetActive(x.HasValue);
                if (x.HasValue)
                {
                    _guideVertical.anchoredPosition = new Vector2(x.Value, 0);
                    _guideVertical.sizeDelta = new Vector2(1f, _layer.rect.height);
                }
            }
            if (_guideHorizontal != null)
            {
                _guideHorizontal.gameObject.SetActive(y.HasValue);
                if (y.HasValue)
                {
                    _guideHorizontal.anchoredPosition = new Vector2(0, y.Value);
                    _guideHorizontal.sizeDelta = new Vector2(_layer.rect.width, 1f);
                }
            }
        }

        private Vector2 SnapWithGuides(string id, Vector2 point)
        {
            Vector2 snapped = ArcanesGroupFrames.Framework.Layout.GridSnap.Snap(point, _gridSize);
            const float tolerance = 9f;
            var rect = _ghosts[id];
            float width = rect.rect.width;
            float height = rect.rect.height;
            float? guideX = null, guideY = null;
            float bestX = tolerance, bestY = tolerance;
            // Candidate positions align left/centre/right and top/centre/bottom
            // edges to either screen centre or another visible non-selected frame.
            _xSnapGuides.Clear();
            _ySnapGuides.Clear();
            _xSnapGuides.Add(_layer.rect.width * .5f);
            _ySnapGuides.Add(-_layer.rect.height * .5f);
            foreach (var pair in _ghosts)
            {
                if (pair.Key == id || !pair.Value.gameObject.activeSelf ||
                    _groupOrigins.ContainsKey(pair.Key)) continue;
                var other = pair.Value;
                _xSnapGuides.Add(other.anchoredPosition.x);
                _xSnapGuides.Add(other.anchoredPosition.x + other.rect.width * .5f);
                _xSnapGuides.Add(other.anchoredPosition.x + other.rect.width);
                _ySnapGuides.Add(other.anchoredPosition.y);
                _ySnapGuides.Add(other.anchoredPosition.y - other.rect.height * .5f);
                _ySnapGuides.Add(other.anchoredPosition.y - other.rect.height);
            }
            foreach (float line in _xSnapGuides)
            {
                for (int anchor = 0; anchor < 3; ++anchor)
                {
                    float candidate = line - width * anchor * .5f;
                    float distance = Mathf.Abs(point.x - candidate);
                    if (distance < bestX)
                    {
                        bestX = distance; snapped.x = candidate; guideX = line;
                    }
                }
            }
            foreach (float line in _ySnapGuides)
            {
                for (int anchor = 0; anchor < 3; ++anchor)
                {
                    float candidate = line + height * anchor * .5f;
                    float distance = Mathf.Abs(point.y - candidate);
                    if (distance < bestY)
                    {
                        bestY = distance; snapped.y = candidate; guideY = line;
                    }
                }
            }
            DisplayGuides(guideX, guideY);
            return snapped;
        }

        internal void EndDrag(string id)
        {
            if (_dragged != id) return;
            FinishMutation();
            _dragged = null;
            _groupOrigins.Clear();
            _groupDragging = false;
            ClearGuides();
        }

        private void FinishMutation()
        {
            if (!_changedDuringDrag) return;
            if (_groupDragging)
            {
                _changedDuringDrag = false;
                Record();
                RefreshGhosts();
                return;
            }
            string id = _dragged ?? _selected;
            if (_resizing)
            {
                CommitResize(id);
                    if (id == "native-chat") PlayerPrefs.Save();
            }
            else
            {
                var source = Source(id);
                PersistPosition(id, id == "native-chat" || id == "minimap" || id == "enemytracker" ? SavedPosition(id) : id == "fotf-primary" || id == "fotf-secondary" || id == "fotf-mass"
                    ? SavedPosition(id)
                    : source == null
                        ? _startNativePosition + _ghosts[id].anchoredPosition - _startGhostPosition
                        : source.anchoredPosition);
            }
            _changedDuringDrag = false;
            Record();
            RefreshGhosts();
        }

        private void CommitResize(string id)
        {
            if (id == "minimap") return; // Resize grip updates Scale directly during Drag.
            if (id == "native-chat")
            {
                if (NativeChat != null && NativeChat.CanResize)
                {
                    NativeChat.TryEditorResize(new Vector2(_pendingResizeWidth, _pendingResizeHeight));
                }
                return;
            }
            bool change;
            if (id == "target")
            {
                change = Mathf.Abs(TargetFrameSettings.Width - _pendingResizeWidth) > .01f ||
                    Mathf.Abs(TargetFrameSettings.Height - _pendingResizeHeight) > .01f;
                if (change)
                {
                    TargetFrameSettings.SetSize(_pendingResizeWidth, _pendingResizeHeight);
                    if (Plugin.Instance != null && Plugin.Instance.UiRuntime != null && Plugin.Instance.UiRuntime.TargetFrame != null)
                        Plugin.Instance.UiRuntime.TargetFrame.RefreshLayout();
                }
                return;
            }
            if (id == "player")
            {
                change = Mathf.Abs(PlayerFrameSettings.Width - _pendingResizeWidth) > .01f ||
                    Mathf.Abs(PlayerFrameSettings.Height - _pendingResizeHeight) > .01f;
                if (change)
                {
                    PlayerFrameSettings.SetSize(_pendingResizeWidth, _pendingResizeHeight);
                    if (Plugin.Instance != null && Plugin.Instance.UiRuntime != null && Plugin.Instance.UiRuntime.PlayerFrame != null)
                        Plugin.Instance.UiRuntime.PlayerFrame.RefreshLayout();
                }
            }
            else if (id == "party")
            {
                change = Mathf.Abs(PartyFramesSettings.FrameWidth - _pendingResizeWidth) > .01f ||
                         Mathf.Abs(PartyFramesSettings.FrameHeight - _pendingResizeHeight) > .01f;
                if (change)
                {
                    PartyFramesSettings.SetFrameWidth(_pendingResizeWidth);
                    PartyFramesSettings.SetFrameHeight(_pendingResizeHeight);
                    PartyFramesUI.SettingsChanged();
                }
            }
            else if (id == "raid")
            {
                change = Mathf.Abs(RaidFramesSettings.FrameWidth - _pendingResizeWidth) > .01f ||
                         Mathf.Abs(RaidFramesSettings.FrameHeight - _pendingResizeHeight) > .01f;
                if (change)
                {
                    RaidFramesSettings.SetFrameWidth(_pendingResizeWidth);
                    RaidFramesSettings.SetFrameHeight(_pendingResizeHeight);
                    RaidFramesUI.SettingsChanged();
                }
            }
        }

        private void Nudge(int x, int y)
        {
            if (string.IsNullOrEmpty(_selected) || !CanEdit(_selected)) return;
            float step = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)
                ? 10f : (_snap ? _gridSize : 1f);
            bool moved = false;
            var members = new List<string>(_selectedIds);
            foreach (string id in members)
            {
                RectTransform ghost;
                if (IsLocked(id) || !CanEdit(id) || !_ghosts.TryGetValue(id, out ghost) ||
                    !ghost.gameObject.activeSelf) continue;
                Vector2 pos = ghost.anchoredPosition + new Vector2(x * step, y * step);
                pos.x = Mathf.Clamp(pos.x, 0, Mathf.Max(0, _layer.rect.width - ghost.rect.width));
                pos.y = Mathf.Clamp(pos.y, -Mathf.Max(0, _layer.rect.height - ghost.rect.height), 0);
                if ((pos - ghost.anchoredPosition).sqrMagnitude < .01f) continue;
                PersistPosition(id, OverlayToSource(id, pos));
                moved = true;
            }
            if (moved) { Record(); RefreshGhosts(); }
        }

        private void RevertSelected()
        {
            string id = _selected;
            if (string.IsNullOrEmpty(id) || IsLocked(id) || !CanEdit(id) || _selectedIds.Count != 1) return;
            // Revert this module only to the values captured when this editor
            // opened; this does not overwrite other modules or factory profiles.
            _onOpen.ApplySelected(id);
            Record();
            RefreshGhosts();
        }

        private void Record()
        {
            var next = LayoutSnapshot.Capture();
            if (next.Equals(_history[_historyIndex])) return;
            if (_historyIndex + 1 < _history.Count)
                _history.RemoveRange(_historyIndex + 1, _history.Count - _historyIndex - 1);
            _history.Add(next);
            _historyIndex = _history.Count - 1;
            if (_history.Count > 40) { _history.RemoveAt(0); _historyIndex--; }
        }

        private void Undo()
        {
            if (_historyIndex <= 0) return;
            _history[--_historyIndex].Apply();
            RefreshGhosts();
            UpdateInfo("Undo applied.");
        }

        private void Redo()
        {
            if (_historyIndex >= _history.Count - 1) return;
            _history[++_historyIndex].Apply();
            RefreshGhosts();
            UpdateInfo("Redo applied.");
        }

        private void ResetSession()
        {
            _onOpen.Apply();
            Record();
            RefreshGhosts();
            UpdateInfo("Restored layout from when editor opened.");
        }

        private void SaveLayout()
        {
            var s = LayoutSnapshot.Capture();
            s.Save(ProfilePrefix);
            UpdateInfo("Saved layout to the Default preset.");
        }

        private void LoadLayout()
        {
            if (!PlayerPrefs.HasKey(ProfilePrefix + "Valid"))
            {
                UpdateInfo("No saved layout yet. Press SAVE first.");
                return;
            }
            LayoutSnapshot.Read(ProfilePrefix).Apply();
            Record();
            RefreshGhosts();
            UpdateInfo("Loaded saved layout, including action bars.");
        }

        private struct LayoutSnapshot : IEquatable<LayoutSnapshot>
        {
            internal Vector2 Player, Target, Party, Raid, Manager, XP, FOTFPrimary, FOTFSecondary, FOTFMass;
            internal Vector2[] ActionBars;
            internal Vector2[] AuraFramePositions;
            internal Vector2 NativeChatOffset, NativeChatSize, MinimapOffset, EnemyTrackerOffset;
            internal float MinimapScale;
            internal bool MinimapMove;
            internal bool NativeChatMove, NativeChatResize;
            internal float PlayerWidth, PlayerHeight;
            internal float TargetWidth, TargetHeight;
            internal float PartyWidth, PartyHeight, RaidWidth, RaidHeight;
            internal float PartySpacing, RaidSpacing, RaidGroupSpacing;

            internal static LayoutSnapshot Capture()
            {
                var s = new LayoutSnapshot();
                s.NativeChatOffset = new Vector2(ChatSettings.OffsetX, ChatSettings.OffsetY);
                s.NativeChatSize = new Vector2(ChatSettings.Width, ChatSettings.Height);
                s.NativeChatMove = ChatSettings.MoveNative;
                s.MinimapOffset = new Vector2(MinimapSettings.OffsetX, MinimapSettings.OffsetY);
                s.EnemyTrackerOffset = EnemyTrackerSettings.Position;
                s.MinimapScale = MinimapSettings.Scale;
                s.MinimapMove = MinimapSettings.MoveNative;
                s.NativeChatResize = ChatSettings.ResizeNative;
                s.AuraFramePositions = new Vector2[5];
                for (int aura = 0; aura < 5; ++aura)
                    s.AuraFramePositions[aura] = CooldownAuraSettings.Frame(aura).Position;
                s.ActionBars = new Vector2[ActionBarPlanSettings.MaxBars];
                for (int i = 0; i < s.ActionBars.Length; ++i)
                    s.ActionBars[i] = new Vector2(ActionBarPlanSettings.BarX(i), ActionBarPlanSettings.BarY(i));
                s.XP = SavedPosition("xp");
                s.FOTFPrimary = FOTFResourceSettings.PrimaryPosition;
                s.FOTFSecondary = FOTFResourceSettings.SecondaryPosition;
                s.FOTFMass = FOTFResourceSettings.MassPosition;
                s.Target = TargetFrameSettings.Position;
                s.TargetWidth = TargetFrameSettings.Width;
                s.TargetHeight = TargetFrameSettings.Height;
                s.Player = PlayerFrameSettings.Position;
                s.PlayerWidth = PlayerFrameSettings.Width;
                s.PlayerHeight = PlayerFrameSettings.Height;
                s.Party = PartyFramesSettings.GetPosition();
                s.Raid = RaidFramesSettings.GetPosition();
                s.Manager = RaidControlPanel.GetPosition();
                s.PartyWidth = PartyFramesSettings.FrameWidth;
                s.PartyHeight = PartyFramesSettings.FrameHeight;
                s.RaidWidth = RaidFramesSettings.FrameWidth;
                s.RaidHeight = RaidFramesSettings.FrameHeight;
                s.PartySpacing = PartyFramesSettings.MemberSpacing;
                s.RaidSpacing = RaidFramesSettings.MemberSpacing;
                s.RaidGroupSpacing = RaidFramesSettings.GroupSpacing;
                return s;
            }

            internal Vector2 PositionFor(string id)
            {
                int bar, aura;
                if (TryBar(id, out bar)) return ActionBars[bar];
                if (TryAuraGroup(id, out aura)) return AuraFramePositions[aura];
                if (id == "player") return Player;
                if (id == "target") return Target;
                if (id == "party") return Party;
                if (id == "raid") return Raid;
                if (id == "manager") return Manager;
                if (id == "native-chat") return NativeChatOffset;
                if (id == "minimap") return MinimapOffset;
                if (id == "enemytracker") return EnemyTrackerOffset;
                if (id == "xp") return XP;
                if (id == "fotf-primary") return FOTFPrimary;
                if (id == "fotf-secondary") return FOTFSecondary;
                return FOTFMass;
            }

            internal void ApplySelected(string id)
            {
                if (id == "native-chat")
                {
                    ChatSettings.SetFloat("OffsetX", NativeChatOffset.x, false);
                    ChatSettings.SetFloat("OffsetY", NativeChatOffset.y, false);
                    ChatSettings.SetFloat("Width", NativeChatSize.x, false);
                    ChatSettings.SetFloat("Height", NativeChatSize.y, false);
                    if (NativeChat != null) NativeChat.Refresh();
                    PlayerPrefs.Save();
                    return;
                }
                if (id == "minimap")
                {
                    MinimapSettings.SetFloat("OffsetX", MinimapOffset.x);
                    MinimapSettings.SetFloat("OffsetY", MinimapOffset.y);
                    MinimapSettings.SetFloat("Scale", MinimapScale);
                    if (NativeMinimap != null) NativeMinimap.Refresh();
                    return;
                }
                PersistPosition(id, PositionFor(id));
                if (id == "target")
                {
                    TargetFrameSettings.SetSize(TargetWidth, TargetHeight);
                    if (Plugin.Instance != null && Plugin.Instance.UiRuntime != null && Plugin.Instance.UiRuntime.TargetFrame != null)
                        Plugin.Instance.UiRuntime.TargetFrame.RefreshLayout();
                }
                else if (id == "player")
                {
                    PlayerFrameSettings.SetSize(PlayerWidth, PlayerHeight);
                    if (Plugin.Instance != null && Plugin.Instance.UiRuntime != null && Plugin.Instance.UiRuntime.PlayerFrame != null)
                        Plugin.Instance.UiRuntime.PlayerFrame.RefreshLayout();
                }
                else if (id == "party")
                {
                    PartyFramesSettings.SetFrameWidth(PartyWidth);
                    PartyFramesSettings.SetFrameHeight(PartyHeight);
                    PartyFramesUI.SettingsChanged();
                }
                else if (id == "raid")
                {
                    RaidFramesSettings.SetFrameWidth(RaidWidth);
                    RaidFramesSettings.SetFrameHeight(RaidHeight);
                    RaidFramesUI.SettingsChanged();
                }
            }

            internal void Apply()
            {
                ChatSettings.SetFloat("OffsetX", NativeChatOffset.x, false);
                ChatSettings.SetFloat("OffsetY", NativeChatOffset.y, false);
                ChatSettings.SetFloat("Width", NativeChatSize.x, false);
                ChatSettings.SetFloat("Height", NativeChatSize.y, false);
                ChatSettings.SetBool("MoveNative", NativeChatMove);
                ChatSettings.SetBool("ResizeNative", NativeChatResize);
                if (NativeChat != null) NativeChat.Refresh();
                MinimapSettings.SetFloat("OffsetX", MinimapOffset.x);
                MinimapSettings.SetFloat("OffsetY", MinimapOffset.y);
                MinimapSettings.SetFloat("Scale", MinimapScale);
                MinimapSettings.SetBool("MoveNative", MinimapMove);
                if (NativeMinimap != null) NativeMinimap.Refresh();
                EnemyTrackerSettings.SetPosition(EnemyTrackerOffset);
                if (Plugin.Instance != null && Plugin.Instance.UiRuntime != null && Plugin.Instance.UiRuntime.EnemyTracker != null)
                    Plugin.Instance.UiRuntime.EnemyTracker.ApplyLayout();
                if (AuraFramePositions != null)
                    for (int i = 0; i < Mathf.Min(5, AuraFramePositions.Length); ++i)
                        CooldownAuraSettings.SetFramePosition(i, AuraFramePositions[i]);
                if (ActionBars != null)
                    for (int i = 0; i < Mathf.Min(ActionBars.Length, ActionBarPlanSettings.Count); ++i)
                        PersistPosition("bar" + i, ActionBars[i]);
                PersistPosition("xp", XP);
                FOTFResourceSettings.SetPrimaryPosition(FOTFPrimary);
                FOTFResourceSettings.SetSecondaryPosition(FOTFSecondary);
                FOTFResourceSettings.SetMassPosition(FOTFMass);
                TargetFrameSettings.SetPosition(Target);
                TargetFrameSettings.SetSize(TargetWidth, TargetHeight);
                if (Plugin.Instance != null && Plugin.Instance.UiRuntime != null && Plugin.Instance.UiRuntime.TargetFrame != null)
                    Plugin.Instance.UiRuntime.TargetFrame.RefreshLayout();
                PlayerFrameSettings.SetPosition(Player);
                PlayerFrameSettings.SetSize(PlayerWidth, PlayerHeight);
                if (Plugin.Instance != null && Plugin.Instance.UiRuntime != null && Plugin.Instance.UiRuntime.PlayerFrame != null)
                    Plugin.Instance.UiRuntime.PlayerFrame.RefreshLayout();
                PartyFramesSettings.SetPosition(Party);
                RaidFramesSettings.SetPosition(Raid);
                RaidControlPanel.SetPosition(Manager);
                PartyFramesSettings.SetFrameWidth(PartyWidth);
                PartyFramesSettings.SetFrameHeight(PartyHeight);
                RaidFramesSettings.SetFrameWidth(RaidWidth);
                RaidFramesSettings.SetFrameHeight(RaidHeight);
                PartyFramesSettings.SetMemberSpacing(PartySpacing);
                RaidFramesSettings.SetMemberSpacing(RaidSpacing);
                RaidFramesSettings.SetGroupSpacing(RaidGroupSpacing);
                PartyFramesUI.SettingsChanged();
                RaidFramesUI.SettingsChanged();
            }

            internal void Save(string prefix)
            {
                PlayerPrefs.SetInt(prefix + "Valid", 1);
                PlayerPrefs.SetInt(prefix + "NativeChatConfigured", 1);
                PlayerPrefs.SetInt(prefix + "MinimapConfigured", 1);
                PlayerPrefs.SetInt(prefix + "EnemyTrackerConfigured", 1);
                Write(prefix + "EnemyTrackerOffset", EnemyTrackerOffset);
                PlayerPrefs.SetInt(prefix + "AuraFramesConfigured", 1);
                if (AuraFramePositions != null)
                    for (int i = 0; i < AuraFramePositions.Length; ++i) Write(prefix + "AuraFrame" + i, AuraFramePositions[i]);
                Write(prefix + "MinimapOffset", MinimapOffset);
                PlayerPrefs.SetFloat(prefix + "MinimapScale", MinimapScale);
                PlayerPrefs.SetInt(prefix + "MinimapMove", MinimapMove ? 1 : 0);
                Write(prefix + "NativeChatOffset", NativeChatOffset);
                Write(prefix + "NativeChatSize", NativeChatSize);
                PlayerPrefs.SetInt(prefix + "NativeChatMove", NativeChatMove ? 1 : 0);
                PlayerPrefs.SetInt(prefix + "NativeChatResize", NativeChatResize ? 1 : 0);
                Write(prefix + "XP", XP);
                Write(prefix + "FOTFPrimary", FOTFPrimary);
                Write(prefix + "FOTFSecondary", FOTFSecondary);
                Write(prefix + "FOTFMass", FOTFMass);
                Write(prefix + "Target", Target);
                PlayerPrefs.SetFloat(prefix + "TargetWidth", TargetWidth);
                PlayerPrefs.SetFloat(prefix + "TargetHeight", TargetHeight);
                Write(prefix + "Player", Player);
                PlayerPrefs.SetFloat(prefix + "PlayerWidth", PlayerWidth);
                PlayerPrefs.SetFloat(prefix + "PlayerHeight", PlayerHeight);
                Write(prefix + "Party", Party);
                Write(prefix + "Raid", Raid);
                Write(prefix + "Manager", Manager);
                if (ActionBars != null)
                    for (int i = 0; i < ActionBars.Length; ++i)
                        Write(prefix + "Bar" + i, ActionBars[i]);
                PlayerPrefs.SetFloat(prefix + "PartyWidth", PartyWidth);
                PlayerPrefs.SetFloat(prefix + "PartyHeight", PartyHeight);
                PlayerPrefs.SetFloat(prefix + "RaidWidth", RaidWidth);
                PlayerPrefs.SetFloat(prefix + "RaidHeight", RaidHeight);
                PlayerPrefs.SetFloat(prefix + "PartySpacing", PartySpacing);
                PlayerPrefs.SetFloat(prefix + "RaidSpacing", RaidSpacing);
                PlayerPrefs.SetFloat(prefix + "RaidGroupSpacing", RaidGroupSpacing);
                PlayerPrefs.Save();
            }

            internal static LayoutSnapshot Read(string prefix)
            {
                var s = Capture();
                // Older profiles never contained the native-chat layout.
                if (PlayerPrefs.HasKey(prefix + "NativeChatConfigured"))
                {
                    s.NativeChatOffset = ReadPoint(prefix + "NativeChatOffset", s.NativeChatOffset);
                    s.NativeChatSize = ReadPoint(prefix + "NativeChatSize", s.NativeChatSize);
                    s.NativeChatMove = PlayerPrefs.GetInt(prefix + "NativeChatMove", s.NativeChatMove ? 1 : 0) != 0;
                    s.NativeChatResize = PlayerPrefs.GetInt(prefix + "NativeChatResize", s.NativeChatResize ? 1 : 0) != 0;
                }
                // Layouts saved before Phase 11.4D preserve current aura positions.
                if (PlayerPrefs.HasKey(prefix + "AuraFramesConfigured") && s.AuraFramePositions != null)
                    for (int i = 0; i < s.AuraFramePositions.Length; ++i)
                        s.AuraFramePositions[i] = ReadPoint(prefix + "AuraFrame" + i, s.AuraFramePositions[i]);
                if (PlayerPrefs.HasKey(prefix + "EnemyTrackerConfigured"))
                    s.EnemyTrackerOffset = ReadPoint(prefix + "EnemyTrackerOffset", s.EnemyTrackerOffset);
                // Old layouts intentionally preserve the current minimap settings.
                if (PlayerPrefs.HasKey(prefix + "MinimapConfigured"))
                {
                    s.MinimapOffset = ReadPoint(prefix + "MinimapOffset", s.MinimapOffset);
                    s.MinimapScale = PlayerPrefs.GetFloat(prefix + "MinimapScale", s.MinimapScale);
                    s.MinimapMove = PlayerPrefs.GetInt(prefix + "MinimapMove", s.MinimapMove ? 1 : 0) != 0;
                }
                s.XP = ReadPoint(prefix + "XP", s.XP);
                s.FOTFPrimary = ReadPoint(prefix + "FOTFPrimary", s.FOTFPrimary);
                s.FOTFSecondary = ReadPoint(prefix + "FOTFSecondary", s.FOTFSecondary);
                s.FOTFMass = ReadPoint(prefix + "FOTFMass", s.FOTFMass);
                s.Target = ReadPoint(prefix + "Target", s.Target);
                s.TargetWidth = PlayerPrefs.GetFloat(prefix + "TargetWidth", s.TargetWidth);
                s.TargetHeight = PlayerPrefs.GetFloat(prefix + "TargetHeight", s.TargetHeight);
                s.Player = ReadPoint(prefix + "Player", s.Player);
                s.PlayerWidth = PlayerPrefs.GetFloat(prefix + "PlayerWidth", s.PlayerWidth);
                s.PlayerHeight = PlayerPrefs.GetFloat(prefix + "PlayerHeight", s.PlayerHeight);
                s.Party = ReadPoint(prefix + "Party", s.Party);
                s.Raid = ReadPoint(prefix + "Raid", s.Raid);
                s.Manager = ReadPoint(prefix + "Manager", s.Manager);
                for (int i = 0; i < s.ActionBars.Length; ++i)
                    s.ActionBars[i] = ReadPoint(prefix + "Bar" + i, s.ActionBars[i]);
                s.PartyWidth = PlayerPrefs.GetFloat(prefix + "PartyWidth", s.PartyWidth);
                s.PartyHeight = PlayerPrefs.GetFloat(prefix + "PartyHeight", s.PartyHeight);
                s.RaidWidth = PlayerPrefs.GetFloat(prefix + "RaidWidth", s.RaidWidth);
                s.RaidHeight = PlayerPrefs.GetFloat(prefix + "RaidHeight", s.RaidHeight);
                s.PartySpacing = PlayerPrefs.GetFloat(prefix + "PartySpacing", s.PartySpacing);
                s.RaidSpacing = PlayerPrefs.GetFloat(prefix + "RaidSpacing", s.RaidSpacing);
                s.RaidGroupSpacing = PlayerPrefs.GetFloat(prefix + "RaidGroupSpacing", s.RaidGroupSpacing);
                return s;
            }

            private static void Write(string key, Vector2 p)
            {
                PlayerPrefs.SetFloat(key + "X", p.x);
                PlayerPrefs.SetFloat(key + "Y", p.y);
            }
            private static Vector2 ReadPoint(string key, Vector2 fallback)
            {
                return new Vector2(PlayerPrefs.GetFloat(key + "X", fallback.x),
                    PlayerPrefs.GetFloat(key + "Y", fallback.y));
            }
            public bool Equals(LayoutSnapshot other)
            {
                if (EnemyTrackerOffset != other.EnemyTrackerOffset) return false;
                if (MinimapOffset != other.MinimapOffset || MinimapScale != other.MinimapScale || MinimapMove != other.MinimapMove) return false;
                if (NativeChatOffset != other.NativeChatOffset || NativeChatSize != other.NativeChatSize ||
                    NativeChatMove != other.NativeChatMove || NativeChatResize != other.NativeChatResize) return false;
                for (int i = 0; i < 5; ++i)
                {
                    if (AuraFramePositions == null || other.AuraFramePositions == null ||
                        i >= AuraFramePositions.Length || i >= other.AuraFramePositions.Length ||
                        AuraFramePositions[i] != other.AuraFramePositions[i]) return false;
                }
                for (int i = 0; i < ActionBarPlanSettings.MaxBars; ++i)
                {
                    if (ActionBars == null || other.ActionBars == null ||
                        i >= ActionBars.Length || i >= other.ActionBars.Length || ActionBars[i] != other.ActionBars[i])
                        return false;
                }
                return XP == other.XP && FOTFPrimary == other.FOTFPrimary && FOTFSecondary == other.FOTFSecondary && FOTFMass == other.FOTFMass && Target == other.Target && TargetWidth == other.TargetWidth && TargetHeight == other.TargetHeight &&
                    Player == other.Player && PlayerWidth == other.PlayerWidth && PlayerHeight == other.PlayerHeight &&
                    Party == other.Party && Raid == other.Raid && Manager == other.Manager &&
                    PartyWidth == other.PartyWidth && PartyHeight == other.PartyHeight &&
                    RaidWidth == other.RaidWidth && RaidHeight == other.RaidHeight &&
                    PartySpacing == other.PartySpacing && RaidSpacing == other.RaidSpacing &&
                    RaidGroupSpacing == other.RaidGroupSpacing;
            }
        }
    }
}
