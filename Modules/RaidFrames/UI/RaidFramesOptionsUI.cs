using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ArcanesGroupFrames
{
    internal static class RaidFramesOptionsUI
    {
        private const float PanelWidth = 520f;
        private const float PanelHeight = 900f;
        private const float Margin = 12f;

        private static GameObject _canvasObject;
        private static GameObject _panelObject;
        private static RectTransform _panelRect;
        private static GameObject _framesTab;
        private static GameObject _clickCastingTab;
        private static GameObject _appearanceTab;
        private static GameObject _pickerOverlay;
        private static Transform _pickerContent;

        private static Text _lockText;
        private static Text _hpPercentText;
        private static Text _rawHealthText;
        private static Text _classColorText;
        private static Text _mouseoverCastingText;
        private static Text _manaBarsText;
        private static Text _targetHighlightText;
        private static Text _aggroHighlightText;
        private static Text _statusIconsText;
        private static Text _raidManagerHotkeyText;
        private static bool _capturingRaidManagerHotkey;

        private static Text _barTextureText;
        private static Text _healthColorModeText;
        private static Text _fontText;
        private static InputField _staticHealthColorInput;
        private static InputField _missingHealthColorInput;

        private static Text _nameFontSizeLabel;
        private static Text _hpPercentFontSizeLabel;
        private static Text _rawHpFontSizeLabel;
        private static Slider _nameFontSizeSlider;
        private static Slider _hpPercentFontSizeSlider;
        private static Slider _rawHpFontSizeSlider;

        private static Text _frameWidthLabel;
        private static Text _frameHeightLabel;
        private static Text _memberSpacingLabel;
        private static Text _targetHighlightThicknessLabel;
        private static Text _aggroHighlightThicknessLabel;
        private static Text _groupSpacingLabel;

        private static Slider _frameWidthSlider;
        private static Slider _frameHeightSlider;
        private static Slider _memberSpacingSlider;
        private static Slider _targetHighlightThicknessSlider;
        private static Slider _aggroHighlightThicknessSlider;
        private static Slider _groupSpacingSlider;

        private static readonly Dictionary<ClickCastBindingSlot, Text>
            BindingTexts = new Dictionary<ClickCastBindingSlot, Text>();

        private static bool _catalogueDumped;

        internal static void Toggle()
        {
            EnsureCreated();

            if (_panelObject == null)
            {
                return;
            }

            bool show = !_panelObject.activeSelf;
            _panelObject.SetActive(show);

            if (show)
            {
                ShowFramesTab();
                RefreshAllControls();
            }
        }

        private static void EnsureCreated()
        {
            if (_canvasObject != null && _panelObject != null)
            {
                return;
            }

            CreateCanvas();
            CreatePanel();
        }

        private static void CreateCanvas()
        {
            _canvasObject = new GameObject("ArcanesGroupFrames_OptionsCanvas");
            UnityEngine.Object.DontDestroyOnLoad(_canvasObject);

            Canvas canvas = _canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 700;

            CanvasScaler scaler = _canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            _canvasObject.AddComponent<GraphicRaycaster>();
        }

        private static void CreatePanel()
        {
            _panelObject = new GameObject("RaidFramesOptionsPanel");
            _panelObject.transform.SetParent(_canvasObject.transform, false);

            _panelRect = _panelObject.AddComponent<RectTransform>();
            _panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            _panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            _panelRect.pivot = new Vector2(0.5f, 0.5f);
            _panelRect.anchoredPosition = Vector2.zero;
            _panelRect.sizeDelta = new Vector2(PanelWidth, PanelHeight);

            Image background = _panelObject.AddComponent<Image>();
            background.color = new Color(0.02f, 0.035f, 0.045f, 0.985f);

            CreateHeader();
            CreateTabs();
            CreateFramesTab();
            CreateAppearanceTab();
            CreateClickCastingTab();
            CreatePickerOverlay();

            _panelObject.SetActive(false);
        }

        private static void CreateHeader()
        {
            GameObject title = CreateObject(
                _panelObject.transform,
                "Title",
                new Vector2(Margin, -10f),
                new Vector2(PanelWidth - 110f, 34f),
                new Vector2(0f, 1f));

            Text titleText = title.AddComponent<Text>();
            titleText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            titleText.fontSize = 18;
            titleText.fontStyle = FontStyle.Bold;
            titleText.alignment = TextAnchor.MiddleLeft;
            titleText.color = Color.white;
            titleText.text = "ArcaneUI - Raid Frames";
            titleText.raycastTarget = false;

            CreateButton(
                _panelObject.transform,
                "Close",
                new Vector2(-12f, -11f),
                new Vector2(76f, 30f),
                new Vector2(1f, 1f),
                Toggle);
        }

        private static void CreateTabs()
        {
            CreateButton(
                _panelObject.transform,
                "Frames",
                new Vector2(Margin, -52f),
                new Vector2(150f, 32f),
                new Vector2(0f, 1f),
                ShowFramesTab);

            CreateButton(
                _panelObject.transform,
                "Appearance",
                new Vector2(Margin + 156f, -52f),
                new Vector2(150f, 32f),
                new Vector2(0f, 1f),
                ShowAppearanceTab);

            CreateButton(
                _panelObject.transform,
                "Click Casting",
                new Vector2(Margin + 312f, -52f),
                new Vector2(150f, 32f),
                new Vector2(0f, 1f),
                ShowClickCastingTab);
        }

        private static void CreateFramesTab()
        {
            _framesTab = new GameObject("FramesTab");
            _framesTab.transform.SetParent(_panelObject.transform, false);
            StretchBelowTabs(_framesTab.AddComponent<RectTransform>());

            float y = -10f;

            CreateSectionLabel(_framesTab.transform, "General", ref y);
            _lockText = CreateToggleRow(_framesTab.transform, ref y, ToggleLock);
            _hpPercentText = CreateToggleRow(_framesTab.transform, ref y, ToggleHpPercent);
            _rawHealthText = CreateToggleRow(_framesTab.transform, ref y, ToggleRawHp);
            _classColorText = CreateToggleRow(_framesTab.transform, ref y, ToggleClassColors);
            _mouseoverCastingText = CreateToggleRow(_framesTab.transform, ref y, ToggleMouseoverCasting);
            _manaBarsText = CreateToggleRow(_framesTab.transform, ref y, ToggleManaBars);
            _targetHighlightText = CreateToggleRow(_framesTab.transform, ref y, ToggleTargetHighlight);
            _aggroHighlightText = CreateToggleRow(_framesTab.transform, ref y, ToggleAggroHighlight);
            _statusIconsText = CreateToggleRow(_framesTab.transform, ref y, ToggleStatusIcons);

            _raidManagerHotkeyText =
                CreateHotkeyRow(
                    _framesTab.transform,
                    ref y,
                    BeginRaidManagerHotkeyCapture);

            CreateSectionLabel(_framesTab.transform, "Layout", ref y);
            CreateSliderRow(_framesTab.transform, ref y, 90f, 240f,
                value => { RaidFramesSettings.SetFrameWidth(value); RaidFramesUI.SettingsChanged(); RefreshAllControls(); },
                out _frameWidthSlider, out _frameWidthLabel);
            CreateSliderRow(_framesTab.transform, ref y, 24f, 70f,
                value => { RaidFramesSettings.SetFrameHeight(value); RaidFramesUI.SettingsChanged(); RefreshAllControls(); },
                out _frameHeightSlider, out _frameHeightLabel);
            CreateSliderRow(_framesTab.transform, ref y, 0f, 10f,
                value => { RaidFramesSettings.SetMemberSpacing(value); RaidFramesUI.SettingsChanged(); RefreshAllControls(); },
                out _memberSpacingSlider, out _memberSpacingLabel);
            CreateSliderRow(_framesTab.transform, ref y, 1f, 6f,
                value => { RaidFramesSettings.SetTargetHighlightThickness(value); RefreshAllControls(); },
                out _targetHighlightThicknessSlider, out _targetHighlightThicknessLabel);
            CreateSliderRow(_framesTab.transform, ref y, 1f, 6f,
                value => { RaidFramesSettings.SetAggroHighlightThickness(value); RefreshAllControls(); },
                out _aggroHighlightThicknessSlider, out _aggroHighlightThicknessLabel);
            CreateSliderRow(_framesTab.transform, ref y, 0f, 25f,
                value => { RaidFramesSettings.SetGroupSpacing(value); RaidFramesUI.SettingsChanged(); RefreshAllControls(); },
                out _groupSpacingSlider, out _groupSpacingLabel);

            CreateButton(
                _framesTab.transform,
                "Reset All Defaults",
                new Vector2(Margin, y - 4f),
                new Vector2(160f, 32f),
                new Vector2(0f, 1f),
                () =>
                {
                    RaidFramesSettings.ResetDefaults();
                    RaidFramesUI.SettingsChanged();
                    RefreshAllControls();
                });
        }

        private static void CreateAppearanceTab()
        {
            _appearanceTab = new GameObject("AppearanceTab");
            _appearanceTab.transform.SetParent(_panelObject.transform, false);
            StretchBelowTabs(_appearanceTab.AddComponent<RectTransform>());

            float y = -10f;

            CreateSectionLabel(_appearanceTab.transform, "Bars", ref y);

            Button textureButton = CreateButton(
                _appearanceTab.transform,
                "Bar Texture",
                new Vector2(Margin, y),
                new Vector2(300f, 32f),
                new Vector2(0f, 1f),
                () =>
                {
                    RaidFramesSettings.SetBarTexture(
                        UnitFrameAppearance.NextTexture(RaidFramesSettings.BarTexture));
                    RefreshAllControls();
                });
            _barTextureText = textureButton.GetComponentInChildren<Text>();
            y -= 38f;

            Button colorModeButton = CreateButton(
                _appearanceTab.transform,
                "Health Color",
                new Vector2(Margin, y),
                new Vector2(300f, 32f),
                new Vector2(0f, 1f),
                () =>
                {
                    RaidFramesSettings.SetUseClassColors(!RaidFramesSettings.UseClassColors);
                    RefreshAllControls();
                });
            _healthColorModeText = colorModeButton.GetComponentInChildren<Text>();
            y -= 44f;

            _staticHealthColorInput = CreateInputRow(
                _appearanceTab.transform,
                "Static Health Color",
                ref y,
                value =>
                {
                    RaidFramesSettings.SetStaticHealthColorHex(value);
                    RefreshAllControls();
                });

            _missingHealthColorInput = CreateInputRow(
                _appearanceTab.transform,
                "Missing HP / Background",
                ref y,
                value =>
                {
                    RaidFramesSettings.SetMissingHealthColorHex(value);
                    RefreshAllControls();
                });

            CreateSectionLabel(_appearanceTab.transform, "Text", ref y);

            Button fontButton = CreateButton(
                _appearanceTab.transform,
                "Font",
                new Vector2(Margin, y),
                new Vector2(300f, 32f),
                new Vector2(0f, 1f),
                () =>
                {
                    RaidFramesSettings.SetFontName(
                        UnitFrameAppearance.NextFont(RaidFramesSettings.FontName));
                    RefreshAllControls();
                });
            _fontText = fontButton.GetComponentInChildren<Text>();
            y -= 44f;

            CreateSliderRow(_appearanceTab.transform, ref y, 8f, 24f,
                value => { RaidFramesSettings.SetNameFontSize(value); RefreshAllControls(); },
                out _nameFontSizeSlider, out _nameFontSizeLabel);

            CreateSliderRow(_appearanceTab.transform, ref y, 8f, 24f,
                value => { RaidFramesSettings.SetHpPercentFontSize(value); RefreshAllControls(); },
                out _hpPercentFontSizeSlider, out _hpPercentFontSizeLabel);

            CreateSliderRow(_appearanceTab.transform, ref y, 8f, 24f,
                value => { RaidFramesSettings.SetRawHpFontSize(value); RefreshAllControls(); },
                out _rawHpFontSizeSlider, out _rawHpFontSizeLabel);

            CreateInfoText(
                _appearanceTab.transform,
                "Color fields accept #RRGGBB hex values. Class color mode ignores the static health color.",
                ref y,
                42f);
        }


        private static void CreateClickCastingTab()
        {
            _clickCastingTab = new GameObject("ClickCastingTab");
            _clickCastingTab.transform.SetParent(_panelObject.transform, false);
            StretchBelowTabs(_clickCastingTab.AddComponent<RectTransform>());

            float y = -10f;

            CreateSectionLabel(_clickCastingTab.transform, "Click Casting", ref y);

            CreateInfoText(
                _clickCastingTab.transform,
                "Select a binding, then choose Target, Inspect, None, or a healing spell. " +
                "Healing spells are read from Erenshor's live SpellDB and filtered to your current class and level.",
                ref y,
                58f);

            CreateBindingRow(_clickCastingTab.transform, "Left Click", ClickCastBindingSlot.LeftClick, ref y);
            CreateBindingRow(_clickCastingTab.transform, "Right Click", ClickCastBindingSlot.RightClick, ref y);
            CreateBindingRow(_clickCastingTab.transform, "Middle Click", ClickCastBindingSlot.MiddleClick, ref y);
            CreateBindingRow(_clickCastingTab.transform, "Shift + Left", ClickCastBindingSlot.ShiftLeft, ref y);
            CreateBindingRow(_clickCastingTab.transform, "Shift + Right", ClickCastBindingSlot.ShiftRight, ref y);
            CreateBindingRow(_clickCastingTab.transform, "Ctrl + Left", ClickCastBindingSlot.CtrlLeft, ref y);
            CreateBindingRow(_clickCastingTab.transform, "Ctrl + Right", ClickCastBindingSlot.CtrlRight, ref y);
            CreateBindingRow(_clickCastingTab.transform, "Alt + Left", ClickCastBindingSlot.AltLeft, ref y);
            CreateBindingRow(_clickCastingTab.transform, "Alt + Right", ClickCastBindingSlot.AltRight, ref y);

            CreateButton(
                _clickCastingTab.transform,
                "Reset Bindings",
                new Vector2(Margin, -486f),
                new Vector2(145f, 32f),
                new Vector2(0f, 1f),
                () =>
                {
                    RaidFramesSettings.ResetClickBindings();
                    RefreshBindingTexts();
                });

            CreateButton(
                _clickCastingTab.transform,
                "Dump Healing Spells",
                new Vector2(Margin + 153f, -486f),
                new Vector2(170f, 32f),
                new Vector2(0f, 1f),
                () => HealingSpellCatalog.DumpAllHealingSpellsToLog());
        }

        private static void CreatePickerOverlay()
        {
            _pickerOverlay = new GameObject("BindingPickerOverlay");
            _pickerOverlay.transform.SetParent(_panelObject.transform, false);

            // The picker occupies the complete tab-content area.
            // Keep the main title/tabs visible, but fully cover the
            // click-casting rows and bottom buttons while the picker is open.
            RectTransform overlayRect = _pickerOverlay.AddComponent<RectTransform>();
            overlayRect.anchorMin = Vector2.zero;
            overlayRect.anchorMax = Vector2.one;
            overlayRect.offsetMin = Vector2.zero;
            overlayRect.offsetMax = new Vector2(0f, -92f);

            Image background = _pickerOverlay.AddComponent<Image>();
            background.color = new Color(0.015f, 0.025f, 0.035f, 0.995f);

            CreateText(
                _pickerOverlay.transform,
                "Choose Binding",
                new Vector2(14f, -10f),
                new Vector2(330f, 32f),
                15,
                TextAnchor.MiddleLeft,
                new Vector2(0f, 1f));

            CreateButton(
                _pickerOverlay.transform,
                "Close",
                new Vector2(-12f, -10f),
                new Vector2(70f, 28f),
                new Vector2(1f, 1f),
                () => _pickerOverlay.SetActive(false));

            // IMPORTANT: this must be a true stretched viewport.
            // The previous implementation created it with a top-left pivot
            // and a negative height, which collapsed/inverted the ScrollRect
            // and left the choice list effectively invisible.
            GameObject scrollObject = new GameObject("Scroll");
            scrollObject.transform.SetParent(_pickerOverlay.transform, false);

            RectTransform scrollRectTransform =
                scrollObject.AddComponent<RectTransform>();

            scrollRectTransform.anchorMin = Vector2.zero;
            scrollRectTransform.anchorMax = Vector2.one;
            scrollRectTransform.pivot = new Vector2(0.5f, 0.5f);
            scrollRectTransform.offsetMin = new Vector2(12f, 12f);
            scrollRectTransform.offsetMax = new Vector2(-12f, -52f);

            Image viewportImage = scrollObject.AddComponent<Image>();
            viewportImage.color = new Color(0.025f, 0.045f, 0.055f, 1f);

            Mask mask = scrollObject.AddComponent<Mask>();
            mask.showMaskGraphic = false;

            ScrollRect scrollRect = scrollObject.AddComponent<ScrollRect>();

            GameObject content = new GameObject("Content");
            content.transform.SetParent(scrollObject.transform, false);

            RectTransform contentRect = content.AddComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.anchoredPosition = Vector2.zero;
            contentRect.sizeDelta = Vector2.zero;

            VerticalLayoutGroup layout = content.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(6, 6, 6, 6);
            layout.spacing = 4f;
            layout.childControlHeight = true;
            layout.childForceExpandHeight = false;
            layout.childControlWidth = true;
            layout.childForceExpandWidth = true;

            ContentSizeFitter fitter = content.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scrollRect.viewport = scrollRectTransform;
            scrollRect.content = contentRect;
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
            scrollRect.scrollSensitivity = 24f;

            _pickerContent = content.transform;
            _pickerOverlay.SetActive(false);
        }

        private static void OpenBindingPicker(ClickCastBindingSlot slot)
        {
            if (_pickerOverlay == null || _pickerContent == null)
            {
                return;
            }

            for (int i = _pickerContent.childCount - 1; i >= 0; i--)
            {
                UnityEngine.Object.Destroy(_pickerContent.GetChild(i).gameObject);
            }

            AddPickerChoice(slot, "None", ClickCastingManager.ActionNone);
            AddPickerChoice(slot, "Target", ClickCastingManager.ActionTarget);
            AddPickerChoice(slot, "Inspect", ClickCastingManager.ActionInspect);

            List<Spell> spells = HealingSpellCatalog.GetPlayerHealingSpells();

            foreach (Spell spell in spells)
            {
                Spell captured = spell;
                AddPickerChoice(
                    slot,
                    HealingSpellCatalog.GetDisplayName(captured),
                    ClickCastingManager.MakeSpellBinding(captured));
            }

            _pickerOverlay.SetActive(true);
        }

        private static void AddPickerChoice(
            ClickCastBindingSlot slot,
            string label,
            string binding)
        {
            GameObject obj = new GameObject("Choice");
            obj.transform.SetParent(_pickerContent, false);

            RectTransform rect = obj.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(0f, 30f);

            LayoutElement element = obj.AddComponent<LayoutElement>();
            element.preferredHeight = 30f;
            element.minHeight = 30f;

            Image image = obj.AddComponent<Image>();
            image.color = new Color(0.055f, 0.12f, 0.14f, 1f);

            Button button = obj.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(() =>
            {
                ClickCastingManager.SetBinding(slot, binding);
                RefreshBindingTexts();
                _pickerOverlay.SetActive(false);
            });

            Text text = CreateText(obj.transform, label, Vector2.zero, Vector2.zero, 11, TextAnchor.MiddleLeft, Vector2.zero);
            RectTransform tr = text.rectTransform;
            tr.anchorMin = Vector2.zero;
            tr.anchorMax = Vector2.one;
            tr.offsetMin = new Vector2(8f, 0f);
            tr.offsetMax = new Vector2(-8f, 0f);
        }

        internal static bool IsCapturingRaidManagerHotkey
        {
            get { return _capturingRaidManagerHotkey; }
        }

        internal static void Update()
        {
            if (!_capturingRaidManagerHotkey)
            {
                return;
            }

            if (_panelObject == null ||
                !_panelObject.activeInHierarchy)
            {
                _capturingRaidManagerHotkey = false;
                RefreshAllControls();
                return;
            }

            if (!Input.anyKeyDown)
            {
                return;
            }

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                RaidFramesSettings.SetRaidManagerToggleKey(KeyCode.None);
                _capturingRaidManagerHotkey = false;
                RefreshAllControls();
                return;
            }

            Array values = Enum.GetValues(typeof(KeyCode));

            for (int i = 0; i < values.Length; i++)
            {
                KeyCode key = (KeyCode)values.GetValue(i);

                if (key == KeyCode.None ||
                    IsMouseKey(key))
                {
                    continue;
                }

                if (!Input.GetKeyDown(key))
                {
                    continue;
                }

                RaidFramesSettings.SetRaidManagerToggleKey(key);
                _capturingRaidManagerHotkey = false;
                RefreshAllControls();
                return;
            }
        }

        private static bool IsMouseKey(KeyCode key)
        {
            return key >= KeyCode.Mouse0 &&
                   key <= KeyCode.Mouse6;
        }

        private static void BeginRaidManagerHotkeyCapture()
        {
            _capturingRaidManagerHotkey = true;

            if (_raidManagerHotkeyText != null)
            {
                _raidManagerHotkeyText.text =
                    "Raid Manager Toggle: PRESS A KEY (Esc clears)";
            }
        }

        private static Text CreateHotkeyRow(
            Transform parent,
            ref float y,
            Action action)
        {
            Button button =
                CreateButton(
                    parent,
                    "Set Hotkey",
                    new Vector2(Margin, y),
                    new Vector2(460f, 32f),
                    new Vector2(0f, 1f),
                    action);

            Text text = button.GetComponentInChildren<Text>();
            y -= 38f;
            return text;
        }

        private static void ShowFramesTab()
        {
            if (_framesTab != null) _framesTab.SetActive(true);
            if (_appearanceTab != null) _appearanceTab.SetActive(false);
            if (_clickCastingTab != null) _clickCastingTab.SetActive(false);
            if (_pickerOverlay != null) _pickerOverlay.SetActive(false);
        }

        private static void ShowAppearanceTab()
        {
            if (_framesTab != null) _framesTab.SetActive(false);
            if (_appearanceTab != null) _appearanceTab.SetActive(true);
            if (_clickCastingTab != null) _clickCastingTab.SetActive(false);
            if (_pickerOverlay != null) _pickerOverlay.SetActive(false);
            RefreshAllControls();
        }

        private static void ShowClickCastingTab()
        {
            if (_framesTab != null) _framesTab.SetActive(false);
            if (_appearanceTab != null) _appearanceTab.SetActive(false);
            if (_clickCastingTab != null) _clickCastingTab.SetActive(true);
            if (_pickerOverlay != null) _pickerOverlay.SetActive(false);

            RefreshBindingTexts();

            if (!_catalogueDumped)
            {
                HealingSpellCatalog.DumpAllHealingSpellsToLog();
                _catalogueDumped = true;
            }
        }

        private static void CreateBindingRow(
            Transform parent,
            string label,
            ClickCastBindingSlot slot,
            ref float y)
        {
            CreateText(parent, label, new Vector2(Margin, y), new Vector2(135f, 32f), 11,
                TextAnchor.MiddleLeft, new Vector2(0f, 1f));

            Button button = CreateButton(
                parent,
                "Select",
                new Vector2(Margin + 145f, y),
                new Vector2(320f, 32f),
                new Vector2(0f, 1f),
                () => OpenBindingPicker(slot));

            Text text = button.GetComponentInChildren<Text>();
            BindingTexts[slot] = text;
            y -= 38f;
        }

        private static void CreateSectionLabel(Transform parent, string value, ref float y)
        {
            CreateText(parent, value, new Vector2(Margin, y), new Vector2(460f, 26f), 13,
                TextAnchor.MiddleLeft, new Vector2(0f, 1f)).fontStyle = FontStyle.Bold;
            y -= 30f;
        }

        private static void CreateInfoText(Transform parent, string value, ref float y, float height)
        {
            Text text = CreateText(parent, value, new Vector2(Margin, y), new Vector2(468f, height), 10,
                TextAnchor.UpperLeft, new Vector2(0f, 1f));
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            y -= height + 6f;
        }

        private static Text CreateToggleRow(Transform parent, ref float y, Action action)
        {
            Button button = CreateButton(parent, "Toggle", new Vector2(Margin, y), new Vector2(300f, 32f),
                new Vector2(0f, 1f), action);
            Text text = button.GetComponentInChildren<Text>();
            y -= 38f;
            return text;
        }

        private static InputField CreateInputRow(
            Transform parent,
            string label,
            ref float y,
            Action<string> changed)
        {
            CreateText(
                parent,
                label,
                new Vector2(Margin, y),
                new Vector2(180f, 30f),
                11,
                TextAnchor.MiddleLeft,
                new Vector2(0f, 1f));

            GameObject fieldObject = CreateObject(
                parent,
                "Input",
                new Vector2(Margin + 190f, y),
                new Vector2(270f, 30f),
                new Vector2(0f, 1f));

            Image background = fieldObject.AddComponent<Image>();
            background.color = new Color(0.08f, 0.11f, 0.13f, 1f);

            InputField input = fieldObject.AddComponent<InputField>();

            Text text = CreateText(
                fieldObject.transform,
                string.Empty,
                new Vector2(6f, -2f),
                new Vector2(258f, 26f),
                11,
                TextAnchor.MiddleLeft,
                new Vector2(0f, 1f));

            text.color = Color.white;
            input.textComponent = text;
            input.targetGraphic = background;
            input.characterLimit = 9;
            input.onEndEdit.AddListener(value => changed(value));

            y -= 38f;
            return input;
        }


        private static void CreateSliderRow(
            Transform parent,
            ref float y,
            float min,
            float max,
            Action<float> changed,
            out Slider slider,
            out Text label)
        {
            label = CreateText(parent, string.Empty, new Vector2(Margin, y), new Vector2(460f, 22f), 11,
                TextAnchor.MiddleLeft, new Vector2(0f, 1f));

            GameObject sliderObject = CreateObject(parent, "Slider", new Vector2(Margin, y - 24f),
                new Vector2(460f, 20f), new Vector2(0f, 1f));

            slider = sliderObject.AddComponent<Slider>();
            slider.minValue = min;
            slider.maxValue = max;
            slider.wholeNumbers = true;
            CreateSliderGraphics(sliderObject, slider);
            slider.onValueChanged.AddListener(value => changed(value));

            y -= 54f;
        }

        private static void CreateSliderGraphics(GameObject sliderObject, Slider slider)
        {
            GameObject track = new GameObject("Track");
            track.transform.SetParent(sliderObject.transform, false);
            RectTransform trackRect = track.AddComponent<RectTransform>();
            trackRect.anchorMin = new Vector2(0f, 0.5f);
            trackRect.anchorMax = new Vector2(1f, 0.5f);
            trackRect.sizeDelta = new Vector2(-10f, 4f);
            Image trackImage = track.AddComponent<Image>();
            trackImage.color = new Color(0.10f, 0.15f, 0.17f, 1f);

            GameObject handleArea = new GameObject("Handle Area");
            handleArea.transform.SetParent(sliderObject.transform, false);
            RectTransform areaRect = handleArea.AddComponent<RectTransform>();
            areaRect.anchorMin = Vector2.zero;
            areaRect.anchorMax = Vector2.one;
            areaRect.offsetMin = new Vector2(6f, 0f);
            areaRect.offsetMax = new Vector2(-6f, 0f);

            GameObject handle = new GameObject("Handle");
            handle.transform.SetParent(handleArea.transform, false);
            RectTransform handleRect = handle.AddComponent<RectTransform>();
            handleRect.anchorMin = new Vector2(0f, 0.5f);
            handleRect.anchorMax = new Vector2(0f, 0.5f);
            handleRect.pivot = new Vector2(0.5f, 0.5f);
            handleRect.sizeDelta = new Vector2(9f, 14f);

            Image handleImage = handle.AddComponent<Image>();
            handleImage.color = Color.white;

            slider.targetGraphic = handleImage;
            slider.handleRect = handleRect;
            slider.fillRect = null;
        }

        private static void ToggleLock() { RaidFramesUI.ToggleLocked(); RefreshAllControls(); }
        private static void ToggleHpPercent() { RaidFramesSettings.SetShowHpPercent(!RaidFramesSettings.ShowHpPercent); RefreshAllControls(); }
        private static void ToggleRawHp() { RaidFramesSettings.SetShowRawHealth(!RaidFramesSettings.ShowRawHealth); RefreshAllControls(); }
        private static void ToggleClassColors() { RaidFramesSettings.SetUseClassColors(!RaidFramesSettings.UseClassColors); RefreshAllControls(); }
        private static void ToggleMouseoverCasting() { RaidFramesSettings.SetEnableMouseoverCasting(!RaidFramesSettings.EnableMouseoverCasting); RefreshAllControls(); }
        private static void ToggleManaBars() { RaidFramesSettings.SetShowManaBars(!RaidFramesSettings.ShowManaBars); RaidFramesUI.SettingsChanged(); RefreshAllControls(); }
        private static void ToggleTargetHighlight() { RaidFramesSettings.SetShowTargetHighlight(!RaidFramesSettings.ShowTargetHighlight); RefreshAllControls(); }
        private static void ToggleAggroHighlight() { RaidFramesSettings.SetShowAggroHighlight(!RaidFramesSettings.ShowAggroHighlight); RefreshAllControls(); }
        private static void ToggleStatusIcons() { RaidFramesSettings.SetShowStatusIcons(!RaidFramesSettings.ShowStatusIcons); RefreshAllControls(); }

        private static void RefreshAllControls()
        {
            if (_lockText != null) _lockText.text = RaidFramesSettings.Locked ? "Frames: LOCKED" : "Frames: UNLOCKED";
            if (_hpPercentText != null) _hpPercentText.text = "HP %: " + (RaidFramesSettings.ShowHpPercent ? "ON" : "OFF");
            if (_rawHealthText != null) _rawHealthText.text = "Raw HP: " + (RaidFramesSettings.ShowRawHealth ? "ON" : "OFF");
            if (_classColorText != null) _classColorText.text = "Class Colors: " + (RaidFramesSettings.UseClassColors ? "ON" : "OFF");
            if (_mouseoverCastingText != null) _mouseoverCastingText.text = "Mouseover Casting: " + (RaidFramesSettings.EnableMouseoverCasting ? "ON" : "OFF");
            if (_manaBarsText != null) _manaBarsText.text = "Mana Bars: " + (RaidFramesSettings.ShowManaBars ? "ON" : "OFF");
            if (_targetHighlightText != null) _targetHighlightText.text = "Target Highlight: " + (RaidFramesSettings.ShowTargetHighlight ? "ON" : "OFF");
            if (_aggroHighlightText != null) _aggroHighlightText.text = "Aggro Highlight: " + (RaidFramesSettings.ShowAggroHighlight ? "ON" : "OFF");
            if (_statusIconsText != null) _statusIconsText.text = "Status Icons: " + (RaidFramesSettings.ShowStatusIcons ? "ON" : "OFF");
            if (_raidManagerHotkeyText != null && !_capturingRaidManagerHotkey)
            {
                _raidManagerHotkeyText.text =
                    "Raid Manager Toggle: " +
                    (RaidFramesSettings.RaidManagerToggleKey == KeyCode.None
                        ? "NOT SET"
                        : RaidFramesSettings.RaidManagerToggleKey.ToString());
            }

            if (_barTextureText != null) _barTextureText.text = "Bar Texture: " + RaidFramesSettings.BarTexture;
            if (_healthColorModeText != null) _healthColorModeText.text = "Health Color: " + (RaidFramesSettings.UseClassColors ? "CLASS" : "STATIC");
            if (_fontText != null) _fontText.text = "Font: " + RaidFramesSettings.FontName;
            if (_staticHealthColorInput != null) _staticHealthColorInput.text = RaidFramesSettings.StaticHealthColorHex;
            if (_missingHealthColorInput != null) _missingHealthColorInput.text = RaidFramesSettings.MissingHealthColorHex;

            SetSlider(_nameFontSizeSlider, RaidFramesSettings.NameFontSize);
            SetSlider(_hpPercentFontSizeSlider, RaidFramesSettings.HpPercentFontSize);
            SetSlider(_rawHpFontSizeSlider, RaidFramesSettings.RawHpFontSize);

            if (_nameFontSizeLabel != null) _nameFontSizeLabel.text = "Name Font Size: " + RaidFramesSettings.NameFontSize;
            if (_hpPercentFontSizeLabel != null) _hpPercentFontSizeLabel.text = "HP % Font Size: " + RaidFramesSettings.HpPercentFontSize;
            if (_rawHpFontSizeLabel != null) _rawHpFontSizeLabel.text = "Raw HP Font Size: " + RaidFramesSettings.RawHpFontSize;

            SetSlider(_frameWidthSlider, RaidFramesSettings.FrameWidth);
            SetSlider(_frameHeightSlider, RaidFramesSettings.FrameHeight);
            SetSlider(_memberSpacingSlider, RaidFramesSettings.MemberSpacing);
            SetSlider(_targetHighlightThicknessSlider, RaidFramesSettings.TargetHighlightThickness);
            SetSlider(_aggroHighlightThicknessSlider, RaidFramesSettings.AggroHighlightThickness);
            SetSlider(_groupSpacingSlider, RaidFramesSettings.GroupSpacing);

            if (_frameWidthLabel != null) _frameWidthLabel.text = "Frame Width: " + RaidFramesSettings.FrameWidth.ToString("F0");
            if (_frameHeightLabel != null) _frameHeightLabel.text = "Frame Height: " + RaidFramesSettings.FrameHeight.ToString("F0");
            if (_memberSpacingLabel != null) _memberSpacingLabel.text = "Member Spacing: " + RaidFramesSettings.MemberSpacing.ToString("F0");
            if (_targetHighlightThicknessLabel != null) _targetHighlightThicknessLabel.text = "Target Border: " + RaidFramesSettings.TargetHighlightThickness.ToString("F0") + " px";
            if (_aggroHighlightThicknessLabel != null) _aggroHighlightThicknessLabel.text = "Aggro Border: " + RaidFramesSettings.AggroHighlightThickness.ToString("F0") + " px";
            if (_groupSpacingLabel != null) _groupSpacingLabel.text = "Group Spacing: " + RaidFramesSettings.GroupSpacing.ToString("F0");

            RefreshBindingTexts();
        }

        private static void RefreshBindingTexts()
        {
            foreach (KeyValuePair<ClickCastBindingSlot, Text> pair in BindingTexts)
            {
                if (pair.Value != null)
                {
                    pair.Value.text = ClickCastingManager.GetBindingDisplayName(
                        ClickCastingManager.GetBinding(pair.Key));
                }
            }
        }

        private static void SetSlider(Slider slider, float value)
        {
            if (slider != null) slider.SetValueWithoutNotify(value);
        }

        private static Button CreateButton(
            Transform parent,
            string label,
            Vector2 anchoredPosition,
            Vector2 size,
            Vector2 anchor,
            Action action)
        {
            GameObject obj = CreateObject(parent, label + "Button", anchoredPosition, size, anchor);

            Image image = obj.AddComponent<Image>();
            image.color = new Color(0.055f, 0.135f, 0.16f, 1f);

            Button button = obj.AddComponent<Button>();
            button.targetGraphic = image;
            if (action != null) button.onClick.AddListener(() => action());

            Text text = CreateText(obj.transform, label, Vector2.zero, Vector2.zero, 11,
                TextAnchor.MiddleCenter, Vector2.zero);
            RectTransform textRect = text.rectTransform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(4f, 0f);
            textRect.offsetMax = new Vector2(-4f, 0f);

            return button;
        }

        private static Text CreateText(
            Transform parent,
            string value,
            Vector2 anchoredPosition,
            Vector2 size,
            int fontSize,
            TextAnchor alignment,
            Vector2 anchor)
        {
            GameObject obj = CreateObject(parent, "Text", anchoredPosition, size, anchor);
            Text text = obj.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = Color.white;
            text.text = value;
            text.raycastTarget = false;
            return text;
        }

        private static GameObject CreateObject(
            Transform parent,
            string name,
            Vector2 anchoredPosition,
            Vector2 size,
            Vector2 anchor)
        {
            GameObject obj = new GameObject(name);
            obj.transform.SetParent(parent, false);

            RectTransform rect = obj.AddComponent<RectTransform>();
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(anchor.x == 1f ? 1f : 0f, anchor.y == 1f ? 1f : 0f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
            return obj;
        }

        private static void StretchBelowTabs(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = new Vector2(0f, -92f);
        }

        internal static void Shutdown()
        {
            if (_canvasObject != null)
            {
                UnityEngine.Object.Destroy(_canvasObject);
            }

            BindingTexts.Clear();
            _canvasObject = null;
            _panelObject = null;
            _panelRect = null;
            _framesTab = null;
            _clickCastingTab = null;
            _appearanceTab = null;
            _pickerOverlay = null;
            _pickerContent = null;
            _lockText = null;
            _hpPercentText = null;
            _rawHealthText = null;
            _classColorText = null;
            _mouseoverCastingText = null;
            _manaBarsText = null;
            _targetHighlightText = null;
            _aggroHighlightText = null;
            _statusIconsText = null;
            _raidManagerHotkeyText = null;
            _capturingRaidManagerHotkey = false;
            _barTextureText = null;
            _healthColorModeText = null;
            _fontText = null;
            _staticHealthColorInput = null;
            _missingHealthColorInput = null;
            _nameFontSizeLabel = null;
            _hpPercentFontSizeLabel = null;
            _rawHpFontSizeLabel = null;
            _nameFontSizeSlider = null;
            _hpPercentFontSizeSlider = null;
            _rawHpFontSizeSlider = null;
            _frameWidthLabel = null;
            _frameHeightLabel = null;
            _memberSpacingLabel = null;
            _targetHighlightThicknessLabel = null;
            _aggroHighlightThicknessLabel = null;
            _groupSpacingLabel = null;
            _frameWidthSlider = null;
            _frameHeightSlider = null;
            _memberSpacingSlider = null;
            _targetHighlightThicknessSlider = null;
            _aggroHighlightThicknessSlider = null;
            _groupSpacingSlider = null;
            _catalogueDumped = false;
        }
    }
}
