using ArcanesGroupFrames.Framework.Theming;
using ArcanesGroupFrames.Framework.Theming.Rendering;
using System;
using UnityEngine;
using UnityEngine.UI;
using ArcanesGroupFrames.Framework.Modules;

namespace ArcanesGroupFrames
{
    internal static class RaidControlPanel
    {
        // ============================================================
        // CONSTANTS
        // ============================================================

        private const int RaidGroupCount =
            3;


        private const float PanelWidth =
            382f;

        private const float PanelHeight =
            392f;

        private const float HeaderHeight =
            24f;

        private const float ButtonHeight =
            25f;

        private const float Gap =
            3f;

        private const float Margin =
            5f;


        private const float CombatRezTrackerHeight =
            40f;


        // ============================================================
        // ROOT OBJECTS
        // ============================================================

        private static GameObject _canvasObject;

        // The plugin owns the visibility of these persistent canvases during
        // scene transitions. Hiding the root also hides all nested popups.
        internal static void SetGameplayVisible(bool visible)
        {
            if (_canvasObject != null && _canvasObject.activeSelf != visible)
                _canvasObject.SetActive(visible);
        }


        private static GameObject _panelObject;

        private static RectTransform _panelRect;


        internal static RectTransform PanelRect
        {
            get
            {
                return _panelRect;
            }
        }


        // ============================================================
        // POPUPS
        // ============================================================

        private static GameObject _dpsPopup;

        private static GameObject _spreadPopup;


        private static Slider _dpsSlider;

        private static Slider _spreadSlider;


        private static Text _dpsValueText;

        private static Text _spreadValueText;


        // ============================================================
        // COMBAT REZ / RAID XP
        // ============================================================

        private static Text _combatRezCountText;

        private static Text _raidXpText;

        private static Image _raidXpFill;


        private static readonly Image[]
            CombatRezPips =
                new Image[
                    NativeRaidBattleRezBackend
                        .MaximumBattleRezzes];


        // ============================================================
        // GROUP TARGET TEXT
        //
        // Index 0 unused.
        // ============================================================

        private static readonly Text[]
            GroupTargetTexts =
                new Text[RaidGroupCount + 1];


        private static readonly string[]
            LastTargetNames =
                new string[RaidGroupCount + 1];


        // ============================================================
        // GROUP TASK DROPDOWNS
        // ============================================================

        private static readonly Dropdown[]
            GroupTaskDropdowns =
                new Dropdown[RaidGroupCount + 1];


        private static readonly string[]
            LastTaskOptionSignatures =
                new string[RaidGroupCount + 1];


        private static readonly int[]
            LastTaskValues =
                new int[RaidGroupCount + 1];


        private static bool _refreshingGroupTasks;


        // ============================================================
        // STATE
        // ============================================================

        private static bool _lastRaidActive;

        // User-controlled visibility for the Raid Manager only.
        // Raid member frames are intentionally unaffected.
        private static bool _userVisible = true;


        // ============================================================
        // POSITION
        // ============================================================

        private static Vector2 _savedPosition =
            new Vector2(
                8f,
                -8f);


        // ============================================================
        // COLORS
        // ============================================================

        private static readonly Color PanelColor =
            new Color(
                0.018f,
                0.035f,
                0.045f,
                0.97f);


        private static readonly Color HeaderColor =
            new Color(
                0.025f,
                0.075f,
                0.09f,
                1f);


        private static readonly Color ButtonColor =
            new Color(
                0.055f,
                0.135f,
                0.16f,
                1f);


        private static readonly Color GroupColor =
            new Color(
                0.07f,
                0.17f,
                0.20f,
                1f);


        private static readonly Color TargetColor =
            new Color(
                0.035f,
                0.09f,
                0.105f,
                1f);


        private static readonly Color PopupColor =
            new Color(
                0.025f,
                0.055f,
                0.07f,
                0.99f);


        private static readonly Color SliderTrackColor =
            new Color(
                0.12f,
                0.18f,
                0.20f,
                1f);


        private static ArcaneUiSurfaceRole PaletteRole(Color color)
        {
            if (color == PanelColor) return ArcaneUiSurfaceRole.Panel;
            if (color == HeaderColor) return ArcaneUiSurfaceRole.Header;
            if (color == ButtonColor) return ArcaneUiSurfaceRole.Button;
            if (color == GroupColor) return ArcaneUiSurfaceRole.Group;
            if (color == TargetColor) return ArcaneUiSurfaceRole.Target;
            if (color == PopupColor) return ArcaneUiSurfaceRole.Popup;
            if (color == SliderTrackColor) return ArcaneUiSurfaceRole.Track;
            return ArcaneUiSurfaceRole.None;
        }

        private static void StyleSurface(GameObject go, Image image, ArcaneUiSurfaceRole role, Color original)
        {
            if (go == null || image == null || role == ArcaneUiSurfaceRole.None) return;
            ArcaneUiStyledSurface surface = go.GetComponent<ArcaneUiStyledSurface>();
            if (surface == null) surface = go.AddComponent<ArcaneUiStyledSurface>();
            surface.Role = role;
            surface.Original = original;
            surface.Image = image;
            surface.Refresh();
        }

        internal static void ApplyTheme()
        {
            if (_canvasObject == null) return;
            foreach (ArcaneUiStyledSurface surface in
                _canvasObject.GetComponentsInChildren<ArcaneUiStyledSurface>(true))
                if (surface != null) surface.Refresh();
        }

        // ============================================================
        // INITIALIZE
        // ============================================================

        internal static void Initialize()
        {
            _savedPosition = new Vector2(
                PlayerPrefs.GetFloat("ArcaneUI.RaidManager.PositionX", 8f),
                PlayerPrefs.GetFloat("ArcaneUI.RaidManager.PositionY", -8f));
            if (_canvasObject != null)
            {
                return;
            }


            CreateCanvas();

            CreatePanel();


            _lastRaidActive =
                GameData.RaidActive;


            _userVisible = true;

            SetVisible(
                _lastRaidActive &&
                _userVisible);


            RefreshTargetLabels(
                true);


            RefreshGroupTaskDropdowns(
                true);


            RefreshCombatRezTracker();


}


        // ============================================================
        // CANVAS
        // ============================================================

        private static void CreateCanvas()
        {
            _canvasObject =
                new GameObject(
                    "ArcanesGroupFrames_RaidControlsCanvas");


            UnityEngine.Object
                .DontDestroyOnLoad(
                    _canvasObject);


            Canvas canvas =
                _canvasObject
                    .AddComponent<Canvas>();


            canvas.renderMode =
                RenderMode.ScreenSpaceOverlay;


            canvas.sortingOrder =
                NativeUiLayering
                    .GameplayCanvasSortingOrder;


            CanvasScaler scaler =
                _canvasObject
                    .AddComponent<CanvasScaler>();


            scaler.uiScaleMode =
                CanvasScaler.ScaleMode
                    .ScaleWithScreenSize;


            scaler.referenceResolution =
                new Vector2(
                    1920f,
                    1080f);


            scaler.matchWidthOrHeight =
                0.5f;


            _canvasObject
                .AddComponent<GraphicRaycaster>();
        }


        // ============================================================
        // PANEL
        // ============================================================

        private static void CreatePanel()
        {
            _panelObject =
                new GameObject(
                    "RaidControlPanel");


            _panelObject
                .transform
                .SetParent(
                    _canvasObject.transform,
                    false);


            _panelRect =
                _panelObject
                    .AddComponent<RectTransform>();


            _panelRect.anchorMin =
                new Vector2(
                    0f,
                    1f);


            _panelRect.anchorMax =
                new Vector2(
                    0f,
                    1f);


            _panelRect.pivot =
                new Vector2(
                    0f,
                    1f);


            _panelRect.anchoredPosition =
                _savedPosition;


            _panelRect.sizeDelta =
                new Vector2(
                    PanelWidth,
                    PanelHeight);


            Image background =
                _panelObject
                    .AddComponent<Image>();


            background.color =
                PanelColor;
            StyleSurface(_panelObject, background, ArcaneUiSurfaceRole.Panel, PanelColor);


            CreateHeader();

            CreateGlobalRows();

            CreateCombatRezTracker();

            CreateGroupRows();
        }


        // ============================================================
        // HEADER
        // ============================================================

        private static void CreateHeader()
        {
            GameObject header =
                CreateBox(
                    "Header",
                    0f,
                    0f,
                    PanelWidth,
                    HeaderHeight,
                    HeaderColor);


            // --------------------------------------------------------
            // TITLE
            // --------------------------------------------------------

            CreateText(
                header.transform,
                "Raid Controls",
                14,
                TextAnchor.MiddleLeft,
                7f);


            // --------------------------------------------------------
            // PANEL DRAGGING
            // --------------------------------------------------------

            RaidControlPanelDragHandler drag =
                header
                    .AddComponent<RaidControlPanelDragHandler>();


            drag.Initialize(
                _panelRect);
        }


        // ============================================================
        // GLOBAL ROWS
        // ============================================================

        private static void CreateGlobalRows()
        {
            float y =
                -(HeaderHeight + Gap);


            const float width =
                72f;


            // ========================================================
            // ROW 1
            // ========================================================

            CreateButton(
                "Tanks Engage",
                Margin,
                y,
                width,
                ButtonHeight,
                NativeRaidControlProxy.TanksEngage);


            CreateButton(
                "All Attack",
                Margin + (width + Gap),
                y,
                width,
                ButtonHeight,
                RaidGroupCommands.AllAttack);


            CreateButton(
                "All Pull",
                Margin + ((width + Gap) * 2f),
                y,
                width,
                ButtonHeight,
                RaidGroupCommands.AllPull);


            Button dpsButton =
                CreateButton(
                    "Raid DPS",
                    Margin + ((width + Gap) * 3f),
                    y,
                    width,
                    ButtonHeight,
                    ToggleDpsPopup);


            Button spreadButton =
                CreateButton(
                    "Spread",
                    Margin + ((width + Gap) * 4f),
                    y,
                    width,
                    ButtonHeight,
                    ToggleSpreadPopup);


            // ========================================================
            // SLIDER POPUPS
            // ========================================================

            _dpsPopup =
                CreateSliderPopup(
                    "RaidDpsPopup",
                    dpsButton.GetComponent<RectTransform>(),
                    "Raid DPS",
                    0f,
                    100f,
                    100f,
                    true,
                    true,
                    OnDpsChanged,
                    out _dpsSlider,
                    out _dpsValueText);


            _spreadPopup =
                CreateSliderPopup(
                    "RaidSpreadPopup",
                    spreadButton.GetComponent<RectTransform>(),
                    "Spread",
                    0f,
                    20f,
                    0f,
                    true,
                    false,
                    OnSpreadChanged,
                    out _spreadSlider,
                    out _spreadValueText);


            _dpsPopup.SetActive(
                false);


            _spreadPopup.SetActive(
                false);


            // ========================================================
            // ROW 2
            // ========================================================

            y -=
                ButtonHeight +
                Gap;


            CreateButton(
                "Burn Target",
                Margin,
                y,
                width,
                ButtonHeight,
                NativeRaidControlProxy.BurnTarget);


            CreateButton(
                "Invite Raider",
                Margin + (width + Gap),
                y,
                width,
                ButtonHeight,
                NativeRaidControlProxy.InviteRaider);


            CreateButton(
                "Save",
                Margin + ((width + Gap) * 2f),
                y,
                width,
                ButtonHeight,
                NativeRaidControlProxy.SaveRaid);


            CreateButton(
                "Load",
                Margin + ((width + Gap) * 3f),
                y,
                width,
                ButtonHeight,
                NativeRaidControlProxy.LoadRaid);


            CreateButton(
                "Loot Dist.",
                Margin + ((width + Gap) * 4f),
                y,
                width,
                ButtonHeight,
                NativeRaidControlProxy.LootDistribution);
        }


        // ============================================================
        // COMBAT REZ / RAID XP TRACKER
        // ============================================================

        private static void CreateCombatRezTracker()
        {
            float y =
                -(HeaderHeight +
                  Gap +
                  ButtonHeight +
                  Gap +
                  ButtonHeight +
                  Gap);


            GameObject tracker =
                CreateBox(
                    "CombatRezTracker",
                    Margin,
                    y,
                    PanelWidth -
                    (Margin * 2f),
                    CombatRezTrackerHeight,
                    TargetColor);


            GameObject titleObject =
                new GameObject(
                    "CombatRezTitle");


            titleObject.transform.SetParent(
                tracker.transform,
                false);


            RectTransform titleRect =
                titleObject.AddComponent<RectTransform>();


            titleRect.anchorMin =
                new Vector2(
                    0f,
                    1f);

            titleRect.anchorMax =
                new Vector2(
                    0f,
                    1f);

            titleRect.pivot =
                new Vector2(
                    0f,
                    1f);

            titleRect.anchoredPosition =
                new Vector2(
                    6f,
                    -2f);

            titleRect.sizeDelta =
                new Vector2(
                    155f,
                    17f);


            _combatRezCountText =
                titleObject.AddComponent<Text>();


            _combatRezCountText.font =
                Resources.GetBuiltinResource<Font>(
                    "Arial.ttf");

            _combatRezCountText.fontSize =
                11;

            _combatRezCountText.fontStyle =
                FontStyle.Bold;

            _combatRezCountText.alignment =
                TextAnchor.MiddleLeft;

            _combatRezCountText.color =
                Color.white;

            _combatRezCountText.raycastTarget =
                false;


            AddOutline(
                titleObject);


            const float pipSize =
                12f;

            const float pipGap =
                3f;


            for (int i = 0;
                 i < CombatRezPips.Length;
                 i++)
            {
                GameObject pip =
                    new GameObject(
                        "CombatRezPip_" +
                        (i + 1));


                pip.transform.SetParent(
                    tracker.transform,
                    false);


                RectTransform pipRect =
                    pip.AddComponent<RectTransform>();


                pipRect.anchorMin =
                    new Vector2(
                        1f,
                        1f);

                pipRect.anchorMax =
                    new Vector2(
                        1f,
                        1f);

                pipRect.pivot =
                    new Vector2(
                        1f,
                        1f);

                pipRect.anchoredPosition =
                    new Vector2(
                        -(6f +
                          (i *
                           (pipSize +
                            pipGap))),
                        -4f);

                pipRect.sizeDelta =
                    new Vector2(
                        pipSize,
                        pipSize);


                CombatRezPips[
                    CombatRezPips.Length -
                    1 -
                    i] =
                        pip.AddComponent<Image>();
            }


            GameObject track =
                new GameObject(
                    "RaidXPTrack");


            track.transform.SetParent(
                tracker.transform,
                false);


            RectTransform trackRect =
                track.AddComponent<RectTransform>();


            trackRect.anchorMin =
                new Vector2(
                    0f,
                    0f);

            trackRect.anchorMax =
                new Vector2(
                    1f,
                    0f);

            trackRect.pivot =
                new Vector2(
                    0.5f,
                    0f);

            trackRect.anchoredPosition =
                new Vector2(
                    0f,
                    4f);

            trackRect.sizeDelta =
                new Vector2(
                    -12f,
                    14f);


            Image trackImage =
                track.AddComponent<Image>();


            trackImage.color =
                SliderTrackColor;
            StyleSurface(track, trackImage, ArcaneUiSurfaceRole.Track, SliderTrackColor);


            GameObject fill =
                new GameObject(
                    "Fill");


            fill.transform.SetParent(
                track.transform,
                false);


            RectTransform fillRect =
                fill.AddComponent<RectTransform>();


            fillRect.anchorMin =
                Vector2.zero;

            fillRect.anchorMax =
                new Vector2(
                    0f,
                    1f);

            fillRect.pivot =
                new Vector2(
                    0f,
                    0.5f);

            fillRect.offsetMin =
                new Vector2(
                    1f,
                    1f);

            fillRect.offsetMax =
                new Vector2(
                    -1f,
                    -1f);


            _raidXpFill =
                fill.AddComponent<Image>();


            _raidXpFill.color =
                new Color(
                    0.12f,
                    0.68f,
                    0.72f,
                    1f);


            GameObject xpTextObject =
                new GameObject(
                    "RaidXPText");


            xpTextObject.transform.SetParent(
                track.transform,
                false);


            RectTransform xpTextRect =
                xpTextObject.AddComponent<RectTransform>();


            xpTextRect.anchorMin =
                Vector2.zero;

            xpTextRect.anchorMax =
                Vector2.one;

            xpTextRect.offsetMin =
                Vector2.zero;

            xpTextRect.offsetMax =
                Vector2.zero;


            _raidXpText =
                xpTextObject.AddComponent<Text>();


            _raidXpText.font =
                Resources.GetBuiltinResource<Font>(
                    "Arial.ttf");

            _raidXpText.fontSize =
                10;

            _raidXpText.fontStyle =
                FontStyle.Bold;

            _raidXpText.alignment =
                TextAnchor.MiddleCenter;

            _raidXpText.color =
                Color.white;

            _raidXpText.raycastTarget =
                false;


            AddOutline(
                xpTextObject);


            RefreshCombatRezTracker();
        }


        private static void AddOutline(
            GameObject obj)
        {
            if (obj == null)
            {
                return;
            }


            Outline outline =
                obj.AddComponent<Outline>();


            outline.effectColor =
                new Color(
                    0f,
                    0f,
                    0f,
                    0.85f);


            outline.effectDistance =
                new Vector2(
                    1f,
                    -1f);
        }


        private static void RefreshCombatRezTracker()
        {
            if (_combatRezCountText == null ||
                _raidXpText == null ||
                _raidXpFill == null)
            {
                return;
            }


            int charges =
                NativeRaidBattleRezBackend
                    .GetAvailableBattleRezzes();


            _combatRezCountText.text =
                "Combat Res: " +
                charges +
                " / " +
                NativeRaidBattleRezBackend
                    .MaximumBattleRezzes;


            for (int i = 0;
                 i < CombatRezPips.Length;
                 i++)
            {
                if (CombatRezPips[i] == null)
                {
                    continue;
                }


                CombatRezPips[i].color =
                    i < charges
                        ? new Color(
                            0.12f,
                            0.68f,
                            0.72f,
                            1f)
                        : new Color(
                            0.08f,
                            0.12f,
                            0.13f,
                            1f);
            }


            float progress =
                NativeRaidBattleRezBackend
                    .GetRaidXPProgress();


            _raidXpFill
                .gameObject
                .SetActive(
                    progress >
                    0.001f);


            _raidXpFill.rectTransform.anchorMax =
                new Vector2(
                    progress,
                    1f);


            if (charges >=
                NativeRaidBattleRezBackend
                    .MaximumBattleRezzes)
            {
                _raidXpText.text =
                    "Raid XP: MAX";
            }
            else
            {
                _raidXpText.text =
                    "Raid XP: " +
                    NativeRaidBattleRezBackend
                        .GetCurrentRaidXP() +
                    " / " +
                    NativeRaidBattleRezBackend
                        .GetRaidXPForNextBattleRez();
            }
        }


        // ============================================================
        // GROUP ROWS
        // ============================================================

        private static void CreateGroupRows()
        {
            float y =
                -(HeaderHeight +
                  Gap +
                  ButtonHeight +
                  Gap +
                  ButtonHeight +
                  Gap +
                  CombatRezTrackerHeight +
                  10f);


            for (int group = 1;
                 group <= RaidGroupCount;
                 group++)
            {
                CreateGroupBlock(
                    group,
                    y);


                y -=
                    84f;
            }
        }


        // ============================================================
        // GROUP BLOCK
        // ============================================================

        private static void CreateGroupBlock(
            int group,
            float y)
        {
            const float labelWidth =
                48f;

            const float assignWidth =
                95f;

            const float commandWidth =
                72f;


            // ========================================================
            // TOP ROW
            // ========================================================

            GameObject label =
                CreateBox(
                    $"Group{group}",
                    Margin,
                    y,
                    labelWidth,
                    ButtonHeight,
                    GroupColor);


            CreateText(
                label.transform,
                $"GRP {group}",
                13,
                TextAnchor.MiddleCenter,
                0f);


            float x =
                Margin +
                labelWidth +
                Gap;


            // --------------------------------------------------------
            // ASSIGN TARGET
            // --------------------------------------------------------

            CreateButton(
                "Assign Targ.",
                x,
                y,
                assignWidth,
                ButtonHeight,
                () =>
                {
                    RaidGroupCommands
                        .AssignTarget(
                            group);


                    RefreshTargetLabel(
                        group,
                        true);
                });


            x +=
                assignWidth +
                Gap;


            // --------------------------------------------------------
            // ATTACK
            // --------------------------------------------------------

            CreateButton(
                "Attack",
                x,
                y,
                commandWidth,
                ButtonHeight,
                () =>
                    RaidGroupCommands
                        .Attack(
                            group));


            x +=
                commandWidth +
                Gap;


            // --------------------------------------------------------
            // PULL
            // --------------------------------------------------------

            CreateButton(
                "Pull",
                x,
                y,
                commandWidth,
                ButtonHeight,
                () =>
                    RaidGroupCommands
                        .Pull(
                            group));


            x +=
                commandWidth +
                Gap;


            // --------------------------------------------------------
            // MEMBER ROLES
            //
            // Opens Arcane's custom per-group role-management panel.
            // Erenshor's raid state remains authoritative; only the
            // presentation is replaced.
            // --------------------------------------------------------

            float rolesWidth =
                PanelWidth -
                Margin -
                x;


            CreateButton(
                "Member Roles",
                x,
                y,
                rolesWidth,
                ButtonHeight,
                () =>
                    RaidMemberRolesPanel
                        .Toggle(
                            group));


            // ========================================================
            // SECOND ROW
            // ========================================================

            float row2 =
                y -
                ButtonHeight -
                Gap;


            float secondX =
                Margin +
                labelWidth +
                Gap;


            // --------------------------------------------------------
            // FOLLOW
            // --------------------------------------------------------

            CreateButton(
                "Follow",
                secondX,
                row2,
                assignWidth,
                ButtonHeight,
                () =>
                    RaidGroupCommands
                        .Follow(
                            group));


            secondX +=
                assignWidth +
                Gap;


            // --------------------------------------------------------
            // HERE
            // --------------------------------------------------------

            CreateButton(
                "Here!",
                secondX,
                row2,
                commandWidth,
                ButtonHeight,
                () =>
                    RaidGroupCommands
                        .Here(
                            group));


            secondX +=
                commandWidth +
                Gap;


            // ========================================================
            // ASSIGNED TARGET DISPLAY
            // ========================================================

            float remainingWidth =
                PanelWidth -
                Margin -
                secondX;


            GameObject targetBox =
                CreateBox(
                    $"Group{group}_Target",
                    secondX,
                    row2,
                    remainingWidth,
                    ButtonHeight,
                    TargetColor);


            Text targetText =
                CreateText(
                    targetBox.transform,
                    "Target: None",
                    11,
                    TextAnchor.MiddleLeft,
                    5f);


            targetText.horizontalOverflow =
                HorizontalWrapMode.Wrap;


            targetText.verticalOverflow =
                VerticalWrapMode.Truncate;


            GroupTargetTexts[group] =
                targetText;


            LastTargetNames[group] =
                null;


            // ========================================================
            // THIRD ROW — GROUP TASK
            // ========================================================

            float row3 =
                row2 -
                ButtonHeight -
                Gap;


            GameObject taskLabel =
                CreateBox(
                    $"Group{group}_TaskLabel",
                    Margin,
                    row3,
                    labelWidth,
                    ButtonHeight,
                    GroupColor);


            CreateText(
                taskLabel.transform,
                "Task",
                11,
                TextAnchor.MiddleCenter,
                0f);


            float taskX =
                Margin +
                labelWidth +
                Gap;


            float taskWidth =
                PanelWidth -
                Margin -
                taskX;


            GroupTaskDropdowns[group] =
                CreateTaskDropdown(
                    group,
                    taskX,
                    row3,
                    taskWidth,
                    ButtonHeight);
        }


        // ============================================================
        // GROUP TASK DROPDOWNS
        // ============================================================

        private static Dropdown CreateTaskDropdown(
            int group,
            float x,
            float y,
            float width,
            float height)
        {
            GameObject root =
                CreateBox(
                    $"Group{group}_TaskDropdown",
                    x,
                    y,
                    width,
                    height,
                    ButtonColor);


            Dropdown dropdown =
                root.AddComponent<Dropdown>();


            dropdown.targetGraphic =
                root.GetComponent<Image>();


            Text caption =
                CreateText(
                    root.transform,
                    "Group Task",
                    11,
                    TextAnchor.MiddleLeft,
                    7f);


            RectTransform captionRect =
                caption.rectTransform;


            captionRect.anchorMin =
                Vector2.zero;

            captionRect.anchorMax =
                Vector2.one;

            captionRect.offsetMin =
                Vector2.zero;

            captionRect.offsetMax =
                new Vector2(
                    -24f,
                    0f);


            dropdown.captionText =
                caption;


            Text arrow =
                CreateText(
                    root.transform,
                    "▼",
                    11,
                    TextAnchor.MiddleCenter,
                    0f);


            RectTransform arrowRect =
                arrow.rectTransform;


            arrowRect.anchorMin =
                new Vector2(
                    1f,
                    0f);

            arrowRect.anchorMax =
                new Vector2(
                    1f,
                    1f);

            arrowRect.pivot =
                new Vector2(
                    1f,
                    0.5f);

            arrowRect.anchoredPosition =
                new Vector2(
                    -3f,
                    0f);

            arrowRect.sizeDelta =
                new Vector2(
                    20f,
                    0f);


            GameObject templateObject =
                new GameObject(
                    "Template");


            templateObject.transform
                .SetParent(
                    root.transform,
                    false);


            RectTransform templateRect =
                templateObject
                    .AddComponent<RectTransform>();


            templateRect.anchorMin =
                new Vector2(
                    0f,
                    0f);

            templateRect.anchorMax =
                new Vector2(
                    1f,
                    0f);

            templateRect.pivot =
                new Vector2(
                    0.5f,
                    1f);

            templateRect.anchoredPosition =
                Vector2.zero;

            templateRect.sizeDelta =
                new Vector2(
                    0f,
                    140f);


            Image templateImage =
                templateObject
                    .AddComponent<Image>();


            templateImage.color =
                PopupColor;


            ScrollRect scrollRect =
                templateObject
                    .AddComponent<ScrollRect>();


            scrollRect.horizontal =
                false;

            scrollRect.vertical =
                true;


            GameObject viewportObject =
                new GameObject(
                    "Viewport");


            viewportObject.transform
                .SetParent(
                    templateObject.transform,
                    false);


            RectTransform viewportRect =
                viewportObject
                    .AddComponent<RectTransform>();


            viewportRect.anchorMin =
                Vector2.zero;

            viewportRect.anchorMax =
                Vector2.one;

            viewportRect.offsetMin =
                Vector2.zero;

            viewportRect.offsetMax =
                Vector2.zero;


            Image viewportImage =
                viewportObject
                    .AddComponent<Image>();


            viewportImage.color =
                new Color(
                    1f,
                    1f,
                    1f,
                    0.01f);


            Mask mask =
                viewportObject
                    .AddComponent<Mask>();


            mask.showMaskGraphic =
                false;


            scrollRect.viewport =
                viewportRect;


            GameObject contentObject =
                new GameObject(
                    "Content");


            contentObject.transform
                .SetParent(
                    viewportObject.transform,
                    false);


            RectTransform contentRect =
                contentObject
                    .AddComponent<RectTransform>();


            contentRect.anchorMin =
                new Vector2(
                    0f,
                    1f);

            contentRect.anchorMax =
                new Vector2(
                    1f,
                    1f);

            contentRect.pivot =
                new Vector2(
                    0.5f,
                    1f);

            contentRect.anchoredPosition =
                Vector2.zero;

            contentRect.sizeDelta =
                new Vector2(
                    0f,
                    25f);


            scrollRect.content =
                contentRect;


            GameObject itemObject =
                new GameObject(
                    "Item");


            itemObject.transform
                .SetParent(
                    contentObject.transform,
                    false);


            RectTransform itemRect =
                itemObject
                    .AddComponent<RectTransform>();


            itemRect.anchorMin =
                new Vector2(
                    0f,
                    0.5f);

            itemRect.anchorMax =
                new Vector2(
                    1f,
                    0.5f);

            itemRect.pivot =
                new Vector2(
                    0.5f,
                    0.5f);

            itemRect.sizeDelta =
                new Vector2(
                    0f,
                    25f);


            Image itemImage =
                itemObject
                    .AddComponent<Image>();


            itemImage.color =
                ButtonColor;


            Toggle itemToggle =
                itemObject
                    .AddComponent<Toggle>();


            itemToggle.targetGraphic =
                itemImage;


            Text itemText =
                CreateText(
                    itemObject.transform,
                    "Option",
                    11,
                    TextAnchor.MiddleLeft,
                    7f);


            RectTransform itemTextRect =
                itemText.rectTransform;


            itemTextRect.anchorMin =
                Vector2.zero;

            itemTextRect.anchorMax =
                Vector2.one;

            itemTextRect.offsetMin =
                Vector2.zero;

            itemTextRect.offsetMax =
                Vector2.zero;


            dropdown.template =
                templateRect;

            dropdown.itemText =
                itemText;


            templateObject.SetActive(
                false);


            dropdown.onValueChanged
                .AddListener(
                    value =>
                        OnGroupTaskChanged(
                            group,
                            value));


            return dropdown;
        }


        private static void OnGroupTaskChanged(
            int group,
            int value)
        {
            if (_refreshingGroupTasks)
            {
                return;
            }


            NativeRaidGroupTaskBackend
                .SetValue(
                    group,
                    value);


            LastTaskValues[group] =
                NativeRaidGroupTaskBackend
                    .GetValue(
                        group);
        }


        private static void RefreshGroupTaskDropdowns(
            bool force)
        {
            if (_refreshingGroupTasks)
            {
                return;
            }


            _refreshingGroupTasks =
                true;


            try
            {
                for (int group = 1;
                     group <= RaidGroupCount;
                     group++)
                {
                    Dropdown dropdown =
                        GroupTaskDropdowns[group];


                    if (dropdown == null)
                    {
                        continue;
                    }


                    System.Collections.Generic.List<string> options =
                        NativeRaidGroupTaskBackend
                            .GetOptions(
                                group);


                    string signature =
                        string.Join(
                            "\u001F",
                            options.ToArray());


                    if (force ||
                        LastTaskOptionSignatures[group] !=
                            signature)
                    {
                        LastTaskOptionSignatures[group] =
                            signature;


                        dropdown.ClearOptions();


                        if (options.Count > 0)
                        {
                            dropdown.AddOptions(
                                options);
                        }
                        else
                        {
                            dropdown.AddOptions(
                                new System.Collections.Generic.List<string>
                                {
                                    "Group Task"
                                });
                        }
                    }


                    int nativeValue =
                        NativeRaidGroupTaskBackend
                            .GetValue(
                                group);


                    int maxValue =
                        Mathf.Max(
                            0,
                            dropdown.options.Count -
                            1);


                    nativeValue =
                        Mathf.Clamp(
                            nativeValue,
                            0,
                            maxValue);


                    if (force ||
                        LastTaskValues[group] !=
                            nativeValue ||
                        dropdown.value !=
                            nativeValue)
                    {
                        LastTaskValues[group] =
                            nativeValue;


                        dropdown.SetValueWithoutNotify(
                            nativeValue);


                        dropdown.RefreshShownValue();
                    }


                    dropdown.interactable =
                        options.Count >
                        0;
                }
            }
            finally
            {
                _refreshingGroupTasks =
                    false;
            }
        }


        // ============================================================
        // TARGET LABEL REFRESH
        // ============================================================

        private static void RefreshTargetLabels(
            bool force)
        {
            for (int group = 1;
                 group <= RaidGroupCount;
                 group++)
            {
                RefreshTargetLabel(
                    group,
                    force);
            }
        }


        private static void RefreshTargetLabel(
            int group,
            bool force)
        {
            if (group < 1 ||
                group > RaidGroupCount)
            {
                return;
            }


            Text targetText =
                GroupTargetTexts[group];


            if (targetText == null)
            {
                return;
            }


            string name =
                RaidGroupCommands
                    .GetAssignedTargetName(
                        group);


            if (!force &&
                LastTargetNames[group] ==
                name)
            {
                return;
            }


            LastTargetNames[group] =
                name;


            targetText.text =
                "Target: " +
                name;
        }


        // ============================================================
        // DPS POPUP
        // ============================================================

        private static void ToggleDpsPopup()
        {
            if (_dpsPopup == null)
            {
                return;
            }


            bool show =
                !_dpsPopup.activeSelf;


            ClosePopups();


            if (show)
            {
                _dpsPopup.SetActive(
                    true);


                _dpsPopup.transform
                    .SetAsLastSibling();
            }
        }


        private static void OnDpsChanged(
            float value)
        {
            value =
                Mathf.Round(
                    value);


            if (_dpsValueText != null)
            {
                _dpsValueText.text =
                    $"{value:F0}%";
            }


            RaidGroupCommands
                .SetRaidDps(
                    value);


            NativeRaidSliderSync
                .SetRaidDps(
                    value);
        }


        // ============================================================
        // SPREAD POPUP
        // ============================================================

        private static void ToggleSpreadPopup()
        {
            if (_spreadPopup == null)
            {
                return;
            }


            bool show =
                !_spreadPopup.activeSelf;


            ClosePopups();


            if (show)
            {
                _spreadPopup.SetActive(
                    true);


                _spreadPopup.transform
                    .SetAsLastSibling();
            }
        }


        private static void OnSpreadChanged(
            float value)
        {
            value =
                Mathf.Round(
                    value);


            if (_spreadValueText != null)
            {
                _spreadValueText.text =
                    value.ToString(
                        "F0");
            }


            RaidGroupCommands
                .SetRaidSpread(
                    value);


            NativeRaidSliderSync
                .SetRaidSpread(
                    value);
        }


        // ============================================================
        // CLOSE POPUPS
        // ============================================================

        private static void ClosePopups()
        {
            if (_dpsPopup != null)
            {
                _dpsPopup.SetActive(
                    false);
            }


            if (_spreadPopup != null)
            {
                _spreadPopup.SetActive(
                    false);
            }
        }


        // ============================================================
        // SLIDER POPUP FACTORY
        // ============================================================

        private static GameObject CreateSliderPopup(
            string name,
            RectTransform sourceButton,
            string title,
            float min,
            float max,
            float initialValue,
            bool wholeNumbers,
            bool displayPercent,
            Action<float> changed,
            out Slider slider,
            out Text valueText)
        {
            // ========================================================
            // ROOT
            // ========================================================

            GameObject popup =
                new GameObject(
                    name);


            popup.transform
                .SetParent(
                    _panelRect,
                    false);


            RectTransform popupRect =
                popup
                    .AddComponent<RectTransform>();


            popupRect.anchorMin =
                new Vector2(
                    0f,
                    1f);


            popupRect.anchorMax =
                new Vector2(
                    0f,
                    1f);


            popupRect.pivot =
                new Vector2(
                    0f,
                    1f);


            Vector2 sourcePosition =
                sourceButton
                    .anchoredPosition;


            popupRect.anchoredPosition =
                new Vector2(
                    sourcePosition.x,
                    sourcePosition.y -
                    ButtonHeight -
                    2f);


            popupRect.sizeDelta =
                new Vector2(
                    135f,
                    48f);


            Image popupImage =
                popup
                    .AddComponent<Image>();


            popupImage.color =
                PopupColor;


            // ========================================================
            // TITLE
            // ========================================================

            GameObject titleObject =
                new GameObject(
                    "Title");


            titleObject.transform
                .SetParent(
                    popup.transform,
                    false);


            RectTransform titleRect =
                titleObject
                    .AddComponent<RectTransform>();


            titleRect.anchorMin =
                new Vector2(
                    0f,
                    1f);


            titleRect.anchorMax =
                new Vector2(
                    1f,
                    1f);


            titleRect.pivot =
                new Vector2(
                    0.5f,
                    1f);


            titleRect.anchoredPosition =
                Vector2.zero;


            titleRect.sizeDelta =
                new Vector2(
                    0f,
                    18f);


            titleRect.offsetMin =
                new Vector2(
                    5f,
                    titleRect.offsetMin.y);


            titleRect.offsetMax =
                new Vector2(
                    -42f,
                    titleRect.offsetMax.y);


            Text titleText =
                titleObject
                    .AddComponent<Text>();


            titleText.font =
                Resources
                    .GetBuiltinResource<Font>(
                        "Arial.ttf");


            titleText.fontSize =
                11;


            titleText.alignment =
                TextAnchor.MiddleLeft;


            titleText.color =
                Color.white;


            titleText.text =
                title;


            titleText.raycastTarget =
                false;


            // ========================================================
            // VALUE
            // ========================================================

            GameObject valueObject =
                new GameObject(
                    "Value");


            valueObject.transform
                .SetParent(
                    popup.transform,
                    false);


            RectTransform valueRect =
                valueObject
                    .AddComponent<RectTransform>();


            valueRect.anchorMin =
                new Vector2(
                    1f,
                    1f);


            valueRect.anchorMax =
                new Vector2(
                    1f,
                    1f);


            valueRect.pivot =
                new Vector2(
                    1f,
                    1f);


            valueRect.anchoredPosition =
                new Vector2(
                    -5f,
                    0f);


            valueRect.sizeDelta =
                new Vector2(
                    38f,
                    18f);


            valueText =
                valueObject
                    .AddComponent<Text>();


            valueText.font =
                Resources
                    .GetBuiltinResource<Font>(
                        "Arial.ttf");


            valueText.fontSize =
                11;


            valueText.alignment =
                TextAnchor.MiddleRight;


            valueText.color =
                Color.white;


            valueText.text =
                displayPercent
                    ? $"{initialValue:F0}%"
                    : initialValue.ToString(
                        "F0");


            valueText.raycastTarget =
                false;


            // ========================================================
            // SLIDER ROOT
            // ========================================================

            GameObject sliderObject =
                new GameObject(
                    "Slider");


            sliderObject.transform
                .SetParent(
                    popup.transform,
                    false);


            RectTransform sliderRect =
                sliderObject
                    .AddComponent<RectTransform>();


            sliderRect.anchorMin =
                new Vector2(
                    0f,
                    0f);


            sliderRect.anchorMax =
                new Vector2(
                    1f,
                    0f);


            sliderRect.pivot =
                new Vector2(
                    0.5f,
                    0f);


            sliderRect.anchoredPosition =
                new Vector2(
                    0f,
                    6f);


            sliderRect.sizeDelta =
                new Vector2(
                    -12f,
                    20f);


            slider =
                sliderObject
                    .AddComponent<Slider>();


            slider.direction =
                Slider.Direction.LeftToRight;


            slider.minValue =
                min;


            slider.maxValue =
                max;


            slider.wholeNumbers =
                wholeNumbers;


            // ========================================================
            // TRACK
            // ========================================================

            GameObject trackObject =
                new GameObject(
                    "Track");


            trackObject.transform
                .SetParent(
                    sliderObject.transform,
                    false);


            RectTransform trackRect =
                trackObject
                    .AddComponent<RectTransform>();


            trackRect.anchorMin =
                new Vector2(
                    0f,
                    0.5f);


            trackRect.anchorMax =
                new Vector2(
                    1f,
                    0.5f);


            trackRect.pivot =
                new Vector2(
                    0.5f,
                    0.5f);


            trackRect.anchoredPosition =
                Vector2.zero;


            trackRect.sizeDelta =
                new Vector2(
                    -8f,
                    4f);


            Image trackImage =
                trackObject
                    .AddComponent<Image>();


            trackImage.color =
                SliderTrackColor;


            // ========================================================
            // HANDLE AREA
            // ========================================================

            GameObject handleAreaObject =
                new GameObject(
                    "Handle Slide Area");


            handleAreaObject.transform
                .SetParent(
                    sliderObject.transform,
                    false);


            RectTransform handleArea =
                handleAreaObject
                    .AddComponent<RectTransform>();


            handleArea.anchorMin =
                Vector2.zero;


            handleArea.anchorMax =
                Vector2.one;


            handleArea.offsetMin =
                new Vector2(
                    6f,
                    0f);


            handleArea.offsetMax =
                new Vector2(
                    -6f,
                    0f);


            // ========================================================
            // HANDLE
            // ========================================================

            GameObject handleObject =
                new GameObject(
                    "Handle");


            handleObject.transform
                .SetParent(
                    handleAreaObject.transform,
                    false);


            RectTransform handleRect =
                handleObject
                    .AddComponent<RectTransform>();


            handleRect.anchorMin =
                new Vector2(
                    0f,
                    0.5f);


            handleRect.anchorMax =
                new Vector2(
                    0f,
                    0.5f);


            handleRect.pivot =
                new Vector2(
                    0.5f,
                    0.5f);


            handleRect.anchoredPosition =
                Vector2.zero;


            // --------------------------------------------------------
            // SMALLER HANDLE
            // --------------------------------------------------------

            handleRect.sizeDelta =
                new Vector2(
                    10f,
                    14f);


            Image handleImage =
                handleObject
                    .AddComponent<Image>();


            handleImage.color =
                Color.white;


            // ========================================================
            // SLIDER HOOKUP
            // ========================================================

            slider.targetGraphic =
                handleImage;


            slider.handleRect =
                handleRect;


            // --------------------------------------------------------
            // Deliberately no fill rect.
            //
            // This prevents the previous giant cyan rectangle bug.
            // --------------------------------------------------------

            slider.fillRect =
                null;


            slider.SetValueWithoutNotify(
                initialValue);


            slider.onValueChanged
                .AddListener(
                    value =>
                    {
                        changed?.Invoke(
                            value);
                    });


            return popup;
        }


        // ============================================================
        // BUTTON FACTORY
        // ============================================================

        private static Button CreateButton(
            string label,
            float x,
            float y,
            float width,
            float height,
            Action action)
        {
            GameObject obj =
                CreateBox(
                    "Button_" +
                    label.Replace(
                        " ",
                        "_"),
                    x,
                    y,
                    width,
                    height,
                    ButtonColor);


            Button button =
                obj.AddComponent<Button>();


            button.targetGraphic =
                obj.GetComponent<Image>();


            if (action != null)
            {
                button.onClick
                    .AddListener(
                        () =>
                            action());
            }


            CreateText(
                obj.transform,
                label,
                12,
                TextAnchor.MiddleCenter,
                0f);


            return button;
        }


        // ============================================================
        // BOX FACTORY
        // ============================================================

        private static GameObject CreateBox(
            string name,
            float x,
            float y,
            float width,
            float height,
            Color color)
        {
            GameObject obj =
                new GameObject(
                    name);


            obj.transform
                .SetParent(
                    _panelRect,
                    false);


            RectTransform rect =
                obj.AddComponent<RectTransform>();


            rect.anchorMin =
                new Vector2(
                    0f,
                    1f);


            rect.anchorMax =
                new Vector2(
                    0f,
                    1f);


            rect.pivot =
                new Vector2(
                    0f,
                    1f);


            rect.anchoredPosition =
                new Vector2(
                    x,
                    y);


            rect.sizeDelta =
                new Vector2(
                    width,
                    height);


            Image image =
                obj.AddComponent<Image>();


            image.color =
                color;
            StyleSurface(obj, image, PaletteRole(color), color);


            return obj;
        }


        // ============================================================
        // TEXT FACTORY
        // ============================================================

        private static Text CreateText(
            Transform parent,
            string value,
            int fontSize,
            TextAnchor alignment,
            float leftPadding)
        {
            GameObject obj =
                new GameObject(
                    "Text");


            obj.transform
                .SetParent(
                    parent,
                    false);


            RectTransform rect =
                obj.AddComponent<RectTransform>();


            rect.anchorMin =
                Vector2.zero;


            rect.anchorMax =
                Vector2.one;


            rect.offsetMin =
                new Vector2(
                    leftPadding,
                    0f);


            rect.offsetMax =
                new Vector2(
                    -3f,
                    0f);


            Text text =
                obj.AddComponent<Text>();


            text.font =
                Resources
                    .GetBuiltinResource<Font>(
                        "Arial.ttf");


            text.fontSize =
                fontSize;


            text.alignment =
                alignment;


            text.color =
                Color.white;


            text.raycastTarget =
                false;


            text.text =
                value;


            return text;
        }


        // ============================================================
        // UPDATE
        // ============================================================

        internal static void ApplyModuleSetting()
        {
            if (!ArcaneUiModuleSettings.RaidManagerEnabled)
            {
                SetVisible(false);
                ClosePopups();
                RaidMemberRolesPanel.Close();
                RaidHealingMatrixPanel.Close();
            }
            else
            {
                // Normal raid state, user hotkey and scene-suppression still
                // determine visibility. No native raid controls are hidden.
                Update();
            }
        }

        internal static void Update()
        {
            if (!ArcaneUiModuleSettings.RaidManagerEnabled)
            {
                if (_panelObject != null && _panelObject.activeSelf)
                    ApplyModuleSetting();
                return;
            }
            if (_panelObject == null)
            {
                return;
            }


            KeyCode toggleKey =
                RaidFramesSettings.RaidManagerToggleKey;

            if (toggleKey != KeyCode.None &&
                !(Plugin.Instance != null && Plugin.Instance.UiRuntime != null &&
                  (Plugin.Instance.UiRuntime.IsCapturingRaidManagerHotkey ||
                   Plugin.Instance.UiRuntime.IsBlockingGameplayInput)) &&
                Input.GetKeyDown(toggleKey))
            {
                _userVisible = !_userVisible;

                if (!_userVisible)
                {
                    ClosePopups();
                }
            }


            bool raidActive =
                GameData.RaidActive;


            // ========================================================
            // RAID START / END
            // ========================================================

            if (raidActive !=
                _lastRaidActive)
            {
                _lastRaidActive =
                    raidActive;


                if (raidActive)
                {
                    _userVisible = true;
                }

                SetVisible(
                    raidActive &&
                    _userVisible &&
                    NativeUiLayering.CanShowGameplayUi);


                if (!raidActive)
                {
                    ClosePopups();


                    RaidGroupCommands
                        .Reset();


                    RefreshTargetLabels(
                        true);
                }
                else
                {
                    RefreshGroupTaskDropdowns(
                        true);


                    RefreshCombatRezTracker();
                }
            }


            SetVisible(
                raidActive &&
                _userVisible &&
                NativeUiLayering
                    .CanShowGameplayUi);


            // ========================================================
            // LIVE TARGET DISPLAY
            // ========================================================

            if (raidActive &&
                NativeUiLayering
                    .CanShowGameplayUi)
            {
                RefreshTargetLabels(
                    false);


                RefreshGroupTaskDropdowns(
                    false);


                RefreshCombatRezTracker();
            }
        }


        // ============================================================
        // VISIBILITY
        // ============================================================

        private static void SetVisible(
            bool visible)
        {
            if (_panelObject != null)
            {
                _panelObject.SetActive(
                    visible);
            }
        }


        // ============================================================
        // POSITION
        // ============================================================

        internal static Vector2 GetPosition() { return _savedPosition; }

        internal static void SetPosition(Vector2 position)
        {
            _savedPosition = position;
            if (_panelRect != null) _panelRect.anchoredPosition = position;
            PlayerPrefs.SetFloat("ArcaneUI.RaidManager.PositionX", position.x);
            PlayerPrefs.SetFloat("ArcaneUI.RaidManager.PositionY", position.y);
            PlayerPrefs.Save();
        }

        internal static void StoreCurrentPosition()
        {
            if (_panelRect != null) SetPosition(_panelRect.anchoredPosition);
        }


        // ============================================================
        // SHUTDOWN
        // ============================================================

        internal static void Shutdown()
        {
            if (_canvasObject != null)
            {
                UnityEngine.Object
                    .Destroy(
                        _canvasObject);
            }


            RaidGroupCommands
                .Reset();


            for (int group = 1;
                 group <= RaidGroupCount;
                 group++)
            {
                GroupTargetTexts[group] =
                    null;


                LastTargetNames[group] =
                    null;


                GroupTaskDropdowns[group] =
                    null;


                LastTaskOptionSignatures[group] =
                    null;


                LastTaskValues[group] =
                    0;
            }


            _canvasObject =
                null;


            _panelObject =
                null;


            _panelRect =
                null;


            _dpsPopup =
                null;


            _spreadPopup =
                null;


            _dpsSlider =
                null;


            _spreadSlider =
                null;


            _dpsValueText =
                null;


            _spreadValueText =
                null;


            _lastRaidActive =
                false;
        }
    }
}