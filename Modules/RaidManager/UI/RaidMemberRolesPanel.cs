using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ArcanesGroupFrames
{
    internal static class RaidMemberRolesPanel
    {
        private const int MaxRows =
            6;

        private const float Width =
            520f;

        private const float Height =
            245f;

        private const float HeaderHeight =
            28f;

        private const float RowHeight =
            31f;

        private const float Gap =
            3f;

        private const float Margin =
            5f;


        private static readonly Color PanelColor =
            new Color(
                0.018f,
                0.035f,
                0.045f,
                0.99f);

        private static readonly Color HeaderColor =
            new Color(
                0.025f,
                0.075f,
                0.09f,
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

        private static readonly Color ActiveColor =
            new Color(
                0.10f,
                0.34f,
                0.25f,
                1f);

        private static readonly Color DisabledColor =
            new Color(
                0.04f,
                0.06f,
                0.07f,
                1f);


        private static GameObject _panelObject;

        private static RectTransform _panelRect;

        private static Text _title;

        private static int _group;

        private static bool _visible;

        private static float _nextRefresh;


        private sealed class MemberRow
        {
            internal GameObject Root;
            internal Text Name;
            internal Button MtMa;
            internal Button Pull;
            internal Button Heal;
            internal Button Cc;
            internal Button Matrix;
            internal RaidMemberSlot Slot;
            internal bool Player;
        }


        private static readonly MemberRow[] Rows =
            new MemberRow[MaxRows];


        internal static bool IsVisible
        {
            get
            {
                return _visible;
            }
        }


        internal static int CurrentGroup
        {
            get
            {
                return _group;
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


        internal static void Toggle(
            int group)
        {
            if (group < 1 ||
                group > 3)
            {
                return;
            }


            if (_visible &&
                _group == group)
            {
                Close();
                return;
            }


            Show(
                group);
        }


        internal static void Show(
            int group)
        {
            if (group < 1 ||
                group > 3)
            {
                return;
            }


            Initialize();


            NativeRaidRoleBackend
                .EnsureNativeManagementUiHidden();


            RaidHealingMatrixPanel
                .Close();


            _group =
                group;

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
        }


        internal static void Update()
        {
            NativeRaidRoleBackend
                .EnsureNativeManagementUiHidden();


            if (!_visible)
            {
                return;
            }


            if (!GameData.RaidActive ||
                GameData.RaidManager == null)
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
                0.15f;


            RefreshContent();
        }


        internal static void Shutdown()
        {
            _visible =
                false;


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

            _group =
                0;


            for (int i = 0;
                 i < Rows.Length;
                 i++)
            {
                Rows[i] =
                    null;
            }
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
                    "ArcanesGroupFrames_RaidMemberRolesPanel");


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
                    "Group Roles",
                    14,
                    TextAnchor.MiddleLeft,
                    8f);


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


            float y =
                -(HeaderHeight +
                  Gap);


            for (int i = 0;
                 i < MaxRows;
                 i++)
            {
                Rows[i] =
                    CreateMemberRow(
                        i,
                        y);


                y -=
                    RowHeight +
                    Gap;
            }
        }


        private static MemberRow CreateMemberRow(
            int index,
            float y)
        {
            MemberRow row =
                new MemberRow();


            row.Root =
                CreateBox(
                    _panelRect,
                    "MemberRow_" +
                    index,
                    Margin,
                    y,
                    Width -
                        (Margin * 2f),
                    RowHeight,
                    RowColor);


            row.Name =
                CreateText(
                    row.Root.transform,
                    "",
                    12,
                    TextAnchor.MiddleLeft,
                    6f);


            RectTransform nameRect =
                row.Name.rectTransform;


            nameRect.anchorMin =
                new Vector2(
                    0f,
                    0f);

            nameRect.anchorMax =
                new Vector2(
                    0f,
                    1f);

            nameRect.pivot =
                new Vector2(
                    0f,
                    0.5f);

            nameRect.anchoredPosition =
                new Vector2(
                    6f,
                    0f);

            nameRect.sizeDelta =
                new Vector2(
                    144f,
                    0f);


            float x =
                154f;

            const float roleWidth =
                58f;


            row.MtMa =
                CreateButton(
                    row.Root.transform,
                    "MT/MA",
                    x,
                    -3f,
                    roleWidth,
                    RowHeight -
                        6f,
                    delegate
                    {
                        OnRolePressed(
                            index,
                            RaidRoleKind.MainAssistMainTank);
                    });


            x +=
                roleWidth +
                Gap;


            row.Pull =
                CreateButton(
                    row.Root.transform,
                    "Pull",
                    x,
                    -3f,
                    roleWidth,
                    RowHeight -
                        6f,
                    delegate
                    {
                        OnRolePressed(
                            index,
                            RaidRoleKind.Puller);
                    });


            x +=
                roleWidth +
                Gap;


            row.Heal =
                CreateButton(
                    row.Root.transform,
                    "Heal",
                    x,
                    -3f,
                    roleWidth,
                    RowHeight -
                        6f,
                    delegate
                    {
                        OnRolePressed(
                            index,
                            RaidRoleKind.Healer);
                    });


            x +=
                roleWidth +
                Gap;


            row.Cc =
                CreateButton(
                    row.Root.transform,
                    "CC",
                    x,
                    -3f,
                    48f,
                    RowHeight -
                        6f,
                    delegate
                    {
                        OnRolePressed(
                            index,
                            RaidRoleKind.CrowdControl);
                    });


            x +=
                48f +
                Gap;


            row.Matrix =
                CreateButton(
                    row.Root.transform,
                    "Matrix",
                    x,
                    -3f,
                    70f,
                    RowHeight -
                        6f,
                    delegate
                    {
                        OnMatrixPressed(
                            index);
                    });


            return row;
        }


        private static void RefreshContent()
        {
            if (_title == null)
            {
                return;
            }


            _title.text =
                "Group " +
                _group +
                " Member Roles";


            List<RaidMemberSlot> members =
                NativeRaidRoleBackend
                    .GetGroupMembers(
                        _group);


            int rowIndex =
                0;


            if (_group == 1 &&
                rowIndex <
                    Rows.Length)
            {
                ConfigurePlayerRow(
                    Rows[rowIndex]);


                rowIndex++;
            }


            for (int i = 0;
                 i < members.Count &&
                 rowIndex <
                    Rows.Length;
                 i++)
            {
                ConfigureMemberRow(
                    Rows[rowIndex],
                    members[i]);


                rowIndex++;
            }


            for (int i = rowIndex;
                 i < Rows.Length;
                 i++)
            {
                ClearRow(
                    Rows[i]);
            }
        }


        private static void ConfigurePlayerRow(
            MemberRow row)
        {
            if (row == null)
            {
                return;
            }


            row.Player =
                true;

            row.Slot =
                null;


            row.Root.SetActive(
                true);


            row.Name.text =
                NativeRaidRoleBackend.GetPlayerName() +
                " (You)";


            SetButtonState(
                row.MtMa,
                NativeRaidRoleBackend.IsPlayerRoleActive(
                    RaidRoleKind.MainAssistMainTank),
                true);

            SetButtonState(
                row.Pull,
                NativeRaidRoleBackend.IsPlayerRoleActive(
                    RaidRoleKind.Puller),
                true);

            SetButtonState(
                row.Heal,
                NativeRaidRoleBackend.IsPlayerRoleActive(
                    RaidRoleKind.Healer),
                true);

            SetButtonState(
                row.Cc,
                NativeRaidRoleBackend.IsPlayerRoleActive(
                    RaidRoleKind.CrowdControl),
                true);


            SetButtonState(
                row.Matrix,
                false,
                false);
        }


        private static void ConfigureMemberRow(
            MemberRow row,
            RaidMemberSlot slot)
        {
            if (row == null)
            {
                return;
            }


            row.Player =
                false;

            row.Slot =
                slot;


            row.Root.SetActive(
                true);


            row.Name.text =
                NativeRaidRoleBackend
                    .GetMemberName(
                        slot);


            SetButtonState(
                row.MtMa,
                NativeRaidRoleBackend.IsRoleActive(
                    slot,
                    RaidRoleKind.MainAssistMainTank),
                true);

            SetButtonState(
                row.Pull,
                NativeRaidRoleBackend.IsRoleActive(
                    slot,
                    RaidRoleKind.Puller),
                true);

            SetButtonState(
                row.Heal,
                NativeRaidRoleBackend.IsRoleActive(
                    slot,
                    RaidRoleKind.Healer),
                true);

            SetButtonState(
                row.Cc,
                NativeRaidRoleBackend.IsRoleActive(
                    slot,
                    RaidRoleKind.CrowdControl),
                true);


            bool matrixEnabled =
                NativeRaidHealingBackend
                    .CanConfigure(
                        slot);


            SetButtonState(
                row.Matrix,
                matrixEnabled,
                matrixEnabled);
        }


        private static void ClearRow(
            MemberRow row)
        {
            if (row == null)
            {
                return;
            }


            row.Slot =
                null;

            row.Player =
                false;


            row.Root.SetActive(
                false);
        }


        private static void OnRolePressed(
            int rowIndex,
            RaidRoleKind role)
        {
            if (rowIndex < 0 ||
                rowIndex >=
                    Rows.Length)
            {
                return;
            }


            MemberRow row =
                Rows[rowIndex];


            if (row == null)
            {
                return;
            }


            if (row.Player)
            {
                NativeRaidRoleBackend
                    .TogglePlayerRole(
                        role);
            }
            else
            {
                NativeRaidRoleBackend
                    .ToggleRole(
                        row.Slot,
                        _group,
                        role);
            }


            RefreshContent();
        }


        private static void OnMatrixPressed(
            int rowIndex)
        {
            if (rowIndex < 0 ||
                rowIndex >=
                    Rows.Length)
            {
                return;
            }


            MemberRow row =
                Rows[rowIndex];


            if (row == null ||
                row.Player ||
                !NativeRaidHealingBackend.CanConfigure(
                    row.Slot))
            {
                return;
            }


            RaidMemberSlot healer =
                row.Slot;


            int returnGroup =
                _group;


            Close();


            RaidHealingMatrixPanel.Open(
                healer,
                returnGroup);
        }


        private static void SetButtonState(
            Button button,
            bool active,
            bool interactable)
        {
            if (button == null)
            {
                return;
            }


            button.interactable =
                interactable;


            Image image =
                button.targetGraphic as Image;


            if (image != null)
            {
                image.color =
                    !interactable
                        ? DisabledColor
                        : (active
                            ? ActiveColor
                            : ButtonColor);
            }
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
