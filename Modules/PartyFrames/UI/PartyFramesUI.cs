using ArcanesGroupFrames.Framework.Theming;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using ArcanesGroupFrames.Framework.Modules;

namespace ArcanesGroupFrames
{
    internal static class PartyFramesUI
    {
        internal static RectTransform PanelRect { get { return _panelRect; } }

        private const int MaximumPartyMembers = 5;
        private const float GroupHeaderHeight = 22f;
        private const float KickButtonGutter = 22f;

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
        private static GameObject _headerObject;
        private static RectTransform _memberContent;

        private static readonly List<RaidMemberFrame> MemberFrames =
            new List<RaidMemberFrame>();

        private static string _lastRosterSignature = "";
        private static bool _lastVisible;
        private static float _rosterRefreshTimer;
        private static float _healthRefreshTimer;
        private static Vector2 _savedPosition = Vector2.zero;

        private static readonly Color GroupHeaderColor =
            new Color(0.055f, 0.135f, 0.16f, 0.98f);

        private static readonly Color EditBackgroundColor =
            new Color(0.01f, 0.02f, 0.025f, 0.42f);

        internal static void Initialize()
        {
            if (_canvasObject != null)
            {
                return;
            }

            CreateCanvas();
            CreatePanel();

            _lastVisible = ShouldShowPartyFrames();
            SetVisible(_lastVisible);
            ApplyLockState();

            if (_lastVisible)
            {
                RebuildRoster();
            }

}

        private static void CreateCanvas()
        {
            _canvasObject =
                new GameObject("ArcanesGroupFrames_PartyFramesCanvas");

            Object.DontDestroyOnLoad(_canvasObject);

            Canvas canvas = _canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder =
                NativeUiLayering.GameplayCanvasSortingOrder;

            CanvasScaler scaler = _canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            _canvasObject.AddComponent<GraphicRaycaster>();
        }

        private static void CreatePanel()
        {
            _panelObject = new GameObject("PartyFramesPanel");
            _panelObject.transform.SetParent(_canvasObject.transform, false);

            _panelRect = _panelObject.AddComponent<RectTransform>();
            _panelRect.anchorMin = new Vector2(0f, 1f);
            _panelRect.anchorMax = new Vector2(0f, 1f);
            _panelRect.pivot = new Vector2(0f, 1f);

            _savedPosition = PartyFramesSettings.GetPosition();
            _panelRect.anchoredPosition = _savedPosition;

            Image background = _panelObject.AddComponent<Image>();
            background.color = EditBackgroundColor;

            BuildContainer();
            ApplyTheme();
        }

        private static void BuildContainer()
        {
            UpdateLayoutGeometry(1);

            CreateHeader();
            CreateMemberContainer();
        }

        private static void ConfigureMemberLayout(GameObject obj)
        {
            HorizontalOrVerticalLayoutGroup layout = PartyFramesSettings.GrowMembersHorizontally
                ? (HorizontalOrVerticalLayoutGroup)obj.AddComponent<HorizontalLayoutGroup>()
                : obj.AddComponent<VerticalLayoutGroup>();
            layout.spacing = PartyFramesSettings.MemberSpacing;
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
        }

        private static void UpdateLayoutGeometry(int members)
        {
            if (_panelRect == null) return;
            int n = Mathf.Max(1, members);
            bool horizontal = PartyFramesSettings.GrowMembersHorizontally;
            float w = PartyFramesSettings.FrameWidth;
            float h = PartyFramesSettings.FrameHeight;
            float spacing = PartyFramesSettings.MemberSpacing;
            _panelRect.sizeDelta = new Vector2(
                KickButtonGutter + (horizontal ? n * w + (n - 1) * spacing : w),
                GroupHeaderHeight + (horizontal ? h : n * h + (n - 1) * spacing));
            if (_memberContent != null)
                _memberContent.sizeDelta = new Vector2(
                    horizontal ? n * w + (n - 1) * spacing : w,
                    horizontal ? h : n * h + (n - 1) * spacing);
        }

        private static void CreateHeader()
        {
            _headerObject = new GameObject("Party_Header");
            _headerObject.transform.SetParent(_panelRect, false);

            RectTransform rect = _headerObject.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(0f, GroupHeaderHeight);

            Image image = _headerObject.AddComponent<Image>();
            image.color = GroupHeaderColor;

            Text text = CreateText(
                _headerObject.transform,
                "GROUP",
                11,
                TextAnchor.MiddleLeft);

            text.rectTransform.offsetMin = new Vector2(5f, 0f);
            text.rectTransform.offsetMax = new Vector2(-3f, 0f);

            PartyFramesDragHandler drag =
                _headerObject.AddComponent<PartyFramesDragHandler>();

            drag.Initialize(_panelRect);
        }

        private static void CreateMemberContainer()
        {
            GameObject obj = new GameObject("Members");
            obj.transform.SetParent(_panelRect, false);

            _memberContent = obj.AddComponent<RectTransform>();
            _memberContent.anchorMin = new Vector2(0f, 1f);
            _memberContent.anchorMax = new Vector2(0f, 1f);
            _memberContent.pivot = new Vector2(0f, 1f);
            _memberContent.anchoredPosition =
                new Vector2(
                    KickButtonGutter,
                    -GroupHeaderHeight);
            _memberContent.sizeDelta =
                new Vector2(
                    PartyFramesSettings.FrameWidth,
                    0f);

            ConfigureMemberLayout(obj);
        }



        internal static void Update()
        {
            if (_panelObject == null)
            {
                return;
            }

            bool visible = ShouldShowPartyFrames();

            if (visible != _lastVisible)
            {
                _lastVisible = visible;
                SetVisible(visible);

                if (visible)
                {
                    RebuildRoster();
                }
                else
                {
                    ClearMemberFrames();
                    _lastRosterSignature = "";
                }
            }

            if (!visible)
            {
                return;
            }

            _healthRefreshTimer -= Time.unscaledDeltaTime;
            if (_healthRefreshTimer <= 0f)
            {
                _healthRefreshTimer = 0.10f;
                RefreshMemberFrames();
            }

            _rosterRefreshTimer -= Time.unscaledDeltaTime;
            if (_rosterRefreshTimer <= 0f)
            {
                _rosterRefreshTimer = 0.35f;

                string signature = BuildRosterSignature();
                if (signature != _lastRosterSignature)
                {
                    RebuildRoster();
                }
            }
        }

        internal static bool IsReplacementActive()
        {
            return ArcaneUiModuleSettings.PartyFramesEnabled &&
                   _panelObject != null &&
                   !GameData.RaidActive &&
                   HasGroupMember();
        }

        private static bool ShouldShowPartyFrames()
        {
            return ArcaneUiModuleSettings.PartyFramesEnabled &&
                   !GameData.RaidActive &&
                   NativeUiLayering.CanShowGameplayUi &&
                   HasGroupMember();
        }

        private static bool HasGroupMember()
        {
            if (GameData.GroupMembers == null)
            {
                return false;
            }

            foreach (SimPlayerTracking member in GameData.GroupMembers)
            {
                if (member != null)
                {
                    return true;
                }
            }

            return false;
        }

        private static void RebuildRoster()
        {
            ClearMemberFrames();

            if (!ShouldShowPartyFrames() || _memberContent == null)
            {
                return;
            }

            if (GameData.PlayerStats != null)
            {
                MemberFrames.Add(
                    new RaidMemberFrame(_memberContent, true));
            }

            if (GameData.GroupMembers != null)
            {
                for (int i = 0; i < GameData.GroupMembers.Length; i++)
                {
                    SimPlayerTracking member =
                        GameData.GroupMembers[i];

                    if (member == null)
                    {
                        continue;
                    }

                    MemberFrames.Add(
                        new RaidMemberFrame(
                            _memberContent,
                            member,
                            i));
                }
            }

            UpdateLayoutGeometry(MemberFrames.Count);
            _lastRosterSignature = BuildRosterSignature();
            RefreshMemberFrames();
        }

        private static string BuildRosterSignature()
        {
            if (!ShouldShowPartyFrames())
            {
                return "";
            }

            StringBuilder builder = new StringBuilder();

            if (GameData.GroupMembers != null)
            {
                int index = 0;
                foreach (SimPlayerTracking member in GameData.GroupMembers)
                {
                    builder.Append(index++);
                    builder.Append(':');

                    if (member != null)
                    {
                        builder.Append(member.SimName);
                        builder.Append(':');
                        builder.Append(member.MyAvatar != null ? "A" : "-");
                    }

                    builder.Append(';');
                }
            }

            return builder.ToString();
        }

        private static void RefreshMemberFrames()
        {
            for (int i = 0; i < MemberFrames.Count; i++)
            {
                MemberFrames[i]?.Refresh();
            }
        }

        private static void ClearMemberFrames()
        {
            for (int i = 0; i < MemberFrames.Count; i++)
            {
                MemberFrames[i]?.Destroy();
            }

            MemberFrames.Clear();
        }

        internal static void SetLocked(bool locked)
        {
            PartyFramesSettings.SetLocked(locked);
            ApplyLockState();
        }

        internal static void ToggleLocked()
        {
            SetLocked(!PartyFramesSettings.Locked);
        }


        internal static void LockStateChanged()
        {
            ApplyLockState();
        }

        private static void ApplyLockState()
        {
            bool unlocked = !PartyFramesSettings.Locked;

            if (_headerObject != null)
            {
                _headerObject.SetActive(unlocked);
            }

            if (_panelObject != null)
            {
                Image background = _panelObject.GetComponent<Image>();
                if (background != null)
                {
                    background.enabled = unlocked;
                }
            }

            if (_memberContent != null)
            {
                _memberContent.anchoredPosition =
                    new Vector2(
                        KickButtonGutter,
                        unlocked ? -GroupHeaderHeight : 0f);
            }
        }

        internal static void ApplyTheme()
        {
            bool inherit = ArcaneUiStyleService.UsesPresetPalette(ArcaneUiModuleSettings.Party);
            if (_headerObject != null)
            {
                Image header = _headerObject.GetComponent<Image>();
                if (header != null) header.color = inherit
                    ? ArcaneUiStyleService.WithOpacity(ArcaneUiStyleService.Theme.Panel) : GroupHeaderColor;
            }
            if (_panelObject != null)
            {
                Image background = _panelObject.GetComponent<Image>();
                if (background != null) background.color = inherit
                    ? ArcaneUiStyleService.WithOpacity(ArcaneUiStyleService.Theme.Background) : EditBackgroundColor;
            }
        }

        internal static void RefreshTheme()
        {
            ApplyTheme();
            RefreshMemberFrames();
        }

        internal static void SettingsChanged()
        {
            RecreateUi();
        }

        private static void RecreateUi()
        {
            ClearMemberFrames();

            _savedPosition = PartyFramesSettings.GetPosition();
            if (_panelRect != null)
            {
                _panelRect.anchoredPosition = _savedPosition;
            }

            if (_headerObject != null)
            {
                Object.Destroy(_headerObject);
                _headerObject = null;
            }

            if (_memberContent != null)
            {
                Object.Destroy(_memberContent.gameObject);
                _memberContent = null;
            }

            BuildContainer();
            ApplyTheme();
            ApplyLockState();

            if (ShouldShowPartyFrames())
            {
                RebuildRoster();
            }
        }

        private static void SetVisible(bool visible)
        {
            if (_panelObject != null)
            {
                _panelObject.SetActive(visible);
            }
        }

        internal static void StoreCurrentPosition()
        {
            if (_panelRect == null)
            {
                return;
            }

            _savedPosition = _panelRect.anchoredPosition;
            PartyFramesSettings.SetPosition(_savedPosition);

}

        private static Text CreateText(
            Transform parent,
            string value,
            int fontSize,
            TextAnchor alignment)
        {
            GameObject obj = new GameObject("Text");
            obj.transform.SetParent(parent, false);

            RectTransform rect = obj.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            Text text = obj.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = Color.white;
            text.raycastTarget = false;
            text.text = value;

            return text;
        }

        internal static void Shutdown()
        {
            ClearMemberFrames();

            if (_canvasObject != null)
            {
                Object.Destroy(_canvasObject);
            }

            _canvasObject = null;
            _panelObject = null;
            _panelRect = null;
            _headerObject = null;
            _memberContent = null;
            _lastVisible = false;
            _lastRosterSignature = "";
        }
    }
}
