using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ArcanesGroupFrames
{
    internal static class RaidHealingMatrixPanel
    {
        private const int RaidGroupCount =
            3;

        private const int MaxRowsPerGroup =
            5;

        private const float Width =
            660f;

        private const float Height =
            270f;

        private const float HeaderHeight =
            28f;

        private const float GroupHeaderHeight =
            22f;

        private const float RowHeight =
            39f;

        private const float Gap =
            4f;

        private const float Margin =
            5f;


        private static readonly Color PanelColor =
            new Color(
                0.018f,
                0.035f,
                0.045f,
                0.995f);

        private static readonly Color HeaderColor =
            new Color(
                0.025f,
                0.075f,
                0.09f,
                1f);

        private static readonly Color GroupColor =
            new Color(
                0.07f,
                0.17f,
                0.20f,
                1f);

        private static readonly Color RowColor =
            new Color(
                0.035f,
                0.09f,
                0.105f,
                1f);

        private static readonly Color ButtonColor =
            new Color(
                0.055f,
                0.135f,
                0.16f,
                1f);

        private static readonly Color SliderColor =
            new Color(
                0.12f,
                0.18f,
                0.20f,
                1f);


        private static GameObject _panelObject;

        private static RectTransform _panelRect;

        private static Text _title;

        private static RaidMemberSlot _healer;

        private static int _returnGroup;

        private static bool _visible;

        private static bool _refreshing;

        private static float _nextRefresh;


        private sealed class MatrixRow
        {
            internal GameObject Root;
            internal Text Name;
            internal Text Percent;
            internal Slider Slider;
            internal RaidMemberSlot Target;
            internal bool PlayerTarget;
        }


        private static readonly MatrixRow[,]
            Rows =
                new MatrixRow[
                    RaidGroupCount,
                    MaxRowsPerGroup];


        internal static bool IsVisible
        {
            get
            {
                return _visible;
            }
        }


        internal static void Initialize()
        {
            if (_panelObject != null)
            {
                return;
            }


            CreatePanel();

            Close();
        }


        internal static void Open(
            RaidMemberSlot healer,
            int returnGroup)
        {
            if (!NativeRaidHealingBackend.CanConfigure(
                    healer))
            {
                return;
            }


            Initialize();


            NativeRaidRoleBackend
                .EnsureNativeManagementUiHidden();


            RaidMemberRolesPanel
                .Close();


            _healer =
                healer;

            _returnGroup =
                returnGroup;

            _visible =
                true;


            _panelObject.SetActive(
                true);


            RefreshContent();


            RaidManagerPopupLayout
                .Apply(
                    _panelRect);
        }


        internal static void Close()
        {
            _visible =
                false;


            if (_panelObject != null)
            {
                _panelObject.SetActive(
                    false);
            }


            _healer =
                null;
        }


        internal static void Update()
        {
            if (!_visible)
            {
                return;
            }


            if (!GameData.RaidActive ||
                GameData.RaidManager == null ||
                !NativeRaidHealingBackend.IsStillInRaid(
                    _healer) ||
                !NativeRaidHealingBackend.CanConfigure(
                    _healer))
            {
                Close();
                return;
            }


            RaidManagerPopupLayout
                .Apply(
                    _panelRect);


            if (Time.unscaledTime <
                _nextRefresh)
            {
                return;
            }


            _nextRefresh =
                Time.unscaledTime +
                0.20f;


            RefreshContent();
        }


        internal static void Shutdown()
        {
            _visible =
                false;

            _healer =
                null;


            if (_panelObject != null)
            {
                UnityEngine.Object.Destroy(
                    _panelObject);
            }


            _panelObject =
                null;

            _panelRect =
                null;

            _title =
                null;
        }


        private static void CreatePanel()
        {
            RectTransform parent =
                RaidControlPanel.PanelRect;


            if (parent == null)
            {
                return;
            }


            _panelObject =
                new GameObject(
                    "ArcanesGroupFrames_RaidHealingMatrixPanel");


            _panelObject.transform.SetParent(
                parent,
                false);


            _panelRect =
                _panelObject.AddComponent<RectTransform>();


            _panelRect.sizeDelta =
                new Vector2(
                    Width,
                    Height);


            Image background =
                _panelObject.AddComponent<Image>();


            background.color =
                PanelColor;


            GameObject header =
                CreateBox(
                    _panelRect,
                    "Header",
                    0f,
                    0f,
                    Width,
                    HeaderHeight,
                    HeaderColor);


            _title =
                CreateText(
                    header.transform,
                    "Raid Healing Matrix",
                    14,
                    TextAnchor.MiddleLeft,
                    76f);


            CreateButton(
                header.transform,
                "Back",
                4f,
                -2f,
                66f,
                HeaderHeight -
                    4f,
                BackToRoles);


            CreateButton(
                header.transform,
                "X",
                Width -
                    30f,
                -2f,
                26f,
                HeaderHeight -
                    4f,
                Close);


            float contentY =
                -(HeaderHeight +
                  Gap);


            float columnWidth =
                (Width -
                 (Margin * 2f) -
                 (Gap * 2f)) /
                RaidGroupCount;


            for (int groupIndex = 0;
                 groupIndex < RaidGroupCount;
                 groupIndex++)
            {
                float x =
                    Margin +
                    (groupIndex *
                     (columnWidth +
                      Gap));


                GameObject groupHeader =
                    CreateBox(
                        _panelRect,
                        "GroupHeader_" +
                        (groupIndex + 1),
                        x,
                        contentY,
                        columnWidth,
                        GroupHeaderHeight,
                        GroupColor);


                CreateText(
                    groupHeader.transform,
                    "GROUP " +
                    (groupIndex + 1),
                    12,
                    TextAnchor.MiddleCenter,
                    0f);


                float rowY =
                    contentY -
                    GroupHeaderHeight -
                    Gap;


                for (int rowIndex = 0;
                     rowIndex < MaxRowsPerGroup;
                     rowIndex++)
                {
                    Rows[groupIndex, rowIndex] =
                        CreateMatrixRow(
                            groupIndex,
                            rowIndex,
                            x,
                            rowY,
                            columnWidth);


                    rowY -=
                        RowHeight +
                        Gap;
                }
            }
        }


        private static MatrixRow CreateMatrixRow(
            int groupIndex,
            int rowIndex,
            float x,
            float y,
            float width)
        {
            MatrixRow row =
                new MatrixRow();


            row.Root =
                CreateBox(
                    _panelRect,
                    "MatrixRow_" +
                    groupIndex +
                    "_" +
                    rowIndex,
                    x,
                    y,
                    width,
                    RowHeight,
                    RowColor);


            row.Name =
                CreateText(
                    row.Root.transform,
                    "",
                    11,
                    TextAnchor.UpperLeft,
                    5f);


            row.Name.rectTransform.offsetMin =
                new Vector2(
                    5f,
                    17f);


            row.Percent =
                CreateText(
                    row.Root.transform,
                    "50%",
                    11,
                    TextAnchor.UpperRight,
                    0f);


            row.Percent.rectTransform.offsetMin =
                new Vector2(
                    0f,
                    17f);

            row.Percent.rectTransform.offsetMax =
                new Vector2(
                    -5f,
                    0f);


            row.Slider =
                CreateSlider(
                    row.Root.transform,
                    5f,
                    -22f,
                    width -
                        10f,
                    14f);


            MatrixRow captured =
                row;


            row.Slider.onValueChanged.AddListener(
                delegate(float value)
                {
                    OnSliderChanged(
                        captured,
                        value);
                });


            return row;
        }


        private static void RefreshContent()
        {
            if (_title == null ||
                _healer == null)
            {
                return;
            }


            _title.text =
                "Raid Healing Matrix — " +
                NativeRaidRoleBackend.GetMemberName(
                    _healer);


            _refreshing =
                true;


            try
            {
                for (int group = 1;
                     group <= RaidGroupCount;
                     group++)
                {
                    List<RaidMemberSlot> members =
                        NativeRaidRoleBackend.GetGroupMembers(
                            group);


                    int memberIndex =
                        0;


                    for (int rowIndex = 0;
                         rowIndex < MaxRowsPerGroup;
                         rowIndex++)
                    {
                        MatrixRow row =
                            Rows[group - 1, rowIndex];


                        // Player is not a RaidMemberSlot. Native Erenshor
                        // represents the player's heal threshold as assignment
                        // index 14, and visually the player belongs to Group 1.
                        if (group == 1 &&
                            rowIndex == 0)
                        {
                            ConfigurePlayerRow(
                                row);

                            continue;
                        }


                        if (memberIndex >=
                            members.Count)
                        {
                            ClearRow(
                                row);

                            continue;
                        }


                        RaidMemberSlot target =
                            members[memberIndex];


                        memberIndex++;


                        ConfigureRow(
                            row,
                            target);
                    }
                }
            }
            finally
            {
                _refreshing =
                    false;
            }
        }


        private static void ConfigurePlayerRow(
            MatrixRow row)
        {
            if (row == null)
            {
                return;
            }


            row.Target =
                null;

            row.PlayerTarget =
                true;


            row.Root.SetActive(
                true);


            row.Name.text =
                NativeRaidRoleBackend.GetPlayerName() +
                " (You)";


            float value =
                NativeRaidHealingBackend.GetPlayerHealAt(
                    _healer);


            row.Slider.SetValueWithoutNotify(
                value);


            row.Percent.text =
                Mathf.RoundToInt(
                    value *
                    100f) +
                "%";
        }


        private static void ConfigureRow(
            MatrixRow row,
            RaidMemberSlot target)
        {
            if (row == null ||
                target == null)
            {
                return;
            }


            row.Target =
                target;

            row.PlayerTarget =
                false;


            row.Root.SetActive(
                true);


            row.Name.text =
                NativeRaidRoleBackend.GetMemberName(
                    target);


            float value =
                NativeRaidHealingBackend.GetHealAt(
                    _healer,
                    target);


            row.Slider.SetValueWithoutNotify(
                value);


            row.Percent.text =
                Mathf.RoundToInt(
                    value *
                    100f) +
                "%";
        }


        private static void ClearRow(
            MatrixRow row)
        {
            if (row == null)
            {
                return;
            }


            row.Target =
                null;

            row.PlayerTarget =
                false;


            row.Root.SetActive(
                false);
        }


        private static void OnSliderChanged(
            MatrixRow row,
            float value)
        {
            if (_refreshing ||
                row == null ||
                _healer == null)
            {
                return;
            }


            if (row.PlayerTarget)
            {
                NativeRaidHealingBackend.SetPlayerHealAt(
                    _healer,
                    value);
            }
            else
            {
                if (row.Target == null)
                {
                    return;
                }


                NativeRaidHealingBackend.SetHealAt(
                    _healer,
                    row.Target,
                    value);
            }


            row.Percent.text =
                Mathf.RoundToInt(
                    value *
                    100f) +
                "%";
        }


        private static void BackToRoles()
        {
            int group =
                _returnGroup;


            Close();


            RaidMemberRolesPanel.Show(
                group);
        }


        private static Slider CreateSlider(
            Transform parent,
            float x,
            float y,
            float width,
            float height)
        {
            GameObject root =
                new GameObject(
                    "Slider");


            root.transform.SetParent(
                parent,
                false);


            RectTransform rect =
                root.AddComponent<RectTransform>();


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


            Image track =
                root.AddComponent<Image>();


            track.color =
                SliderColor;


            Slider slider =
                root.AddComponent<Slider>();


            slider.minValue =
                0f;

            slider.maxValue =
                1f;

            slider.wholeNumbers =
                false;

            slider.direction =
                Slider.Direction.LeftToRight;


            GameObject handleObject =
                new GameObject(
                    "Handle");


            handleObject.transform.SetParent(
                root.transform,
                false);


            RectTransform handleRect =
                handleObject.AddComponent<RectTransform>();


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

            handleRect.sizeDelta =
                new Vector2(
                    10f,
                    height +
                        2f);


            Image handle =
                handleObject.AddComponent<Image>();


            handle.color =
                Color.white;


            slider.targetGraphic =
                handle;

            slider.handleRect =
                handleRect;

            slider.fillRect =
                null;


            return slider;
        }


        private static GameObject CreateBox(
            Transform parent,
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


            obj.transform.SetParent(
                parent,
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


            return obj;
        }


        private static Button CreateButton(
            Transform parent,
            string label,
            float x,
            float y,
            float width,
            float height,
            UnityEngine.Events.UnityAction action)
        {
            GameObject obj =
                CreateBox(
                    parent,
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
                button.onClick.AddListener(
                    action);
            }


            CreateText(
                obj.transform,
                label,
                11,
                TextAnchor.MiddleCenter,
                0f);


            return button;
        }


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


            obj.transform.SetParent(
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
                Resources.GetBuiltinResource<Font>(
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
    }
}
