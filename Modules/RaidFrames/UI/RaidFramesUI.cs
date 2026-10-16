using ArcanesGroupFrames.Framework.Theming;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using ArcanesGroupFrames.Framework.Modules;

namespace ArcanesGroupFrames
{
    internal static class RaidFramesUI
    {
        internal static RectTransform PanelRect { get { return _panelRect; } }

        // ============================================================
        // CONSTANTS
        // ============================================================

        private const int RaidGroupCount =
            3;


        private const float GroupHeaderHeight =
            22f;


        private const int MaximumMembersPerGroup =
            6;


        // ============================================================
        // ROOT
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


        // ============================================================
        // GROUPS
        // ============================================================

        private static readonly GameObject[]
            GroupObjects =
                new GameObject[RaidGroupCount + 1];


        private static readonly GameObject[]
            GroupHeaders =
                new GameObject[RaidGroupCount + 1];


        private static readonly RectTransform[]
            GroupContent =
                new RectTransform[RaidGroupCount + 1];


        // ============================================================
        // MEMBER FRAMES
        // ============================================================

        private static readonly List<RaidMemberFrame>
            MemberFrames =
                new List<RaidMemberFrame>();


        // ============================================================
        // STATE
        // ============================================================

        private static bool _lastRaidActive;


        private static string _lastRosterSignature =
            "";


        private static float _rosterRefreshTimer;

        private static float _healthRefreshTimer;


        private static Vector2 _savedPosition =
            Vector2.zero;


        // ============================================================
        // COLORS
        // ============================================================

        private static readonly Color GroupHeaderColor =
            new Color(
                0.055f,
                0.135f,
                0.16f,
                0.98f);


        private static readonly Color EditBackgroundColor =
            new Color(
                0.01f,
                0.02f,
                0.025f,
                0.42f);


        // ============================================================
        // INITIALIZE
        // ============================================================

        internal static void RefreshTheme()
        {
            ApplyTheme();
            RefreshMemberFrames();
        }

        internal static void ApplyTheme()
        {
            bool inherits = ArcaneUiStyleService.UsesPresetPalette(ArcaneUiModuleSettings.Raid);
            Color headerColor = inherits ? ArcaneUiStyleService.WithOpacity(ArcaneUiStyleService.Theme.Panel) : GroupHeaderColor;
            Color editColor = inherits ? ArcaneUiStyleService.WithOpacity(ArcaneUiStyleService.Theme.Background) : EditBackgroundColor;
            for (int group = 1; group <= RaidGroupCount; group++)
            {
                if (GroupHeaders[group] != null)
                {
                    Image header = GroupHeaders[group].GetComponent<Image>();
                    if (header != null) header.color = headerColor;
                }
                if (GroupObjects[group] != null)
                {
                    Image backing = GroupObjects[group].GetComponent<Image>();
                    if (backing != null) backing.color = editColor;
                }
            }
        }

        internal static void Initialize()
        {
            if (_canvasObject != null)
            {
                return;
            }


            CreateCanvas();

            CreatePanel();
            ApplyTheme();


            _lastRaidActive =
                GameData.RaidActive && ArcaneUiModuleSettings.RaidFramesEnabled;


            SetVisible(
                _lastRaidActive);


            ApplyLockState();


            if (_lastRaidActive)
            {
                RebuildRoster();
            }


}


        // ============================================================
        // CANVAS
        // ============================================================

        private static void CreateCanvas()
        {
            _canvasObject =
                new GameObject(
                    "ArcanesGroupFrames_RaidFramesCanvas");


            UnityEngine.Object.DontDestroyOnLoad(
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
                    "RaidFramesPanel");


            _panelObject.transform.SetParent(
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


            _savedPosition =
                RaidFramesSettings.GetPosition();


            _panelRect.anchoredPosition =
                _savedPosition;


            BuildGroupContainers();
            ApplyTheme();
        }


        // ============================================================
        // GROUP CONTAINERS
        // ============================================================

        // Resize the occupied group panels after every roster update.
        // Vertical member growth: each group is a column; groups 1-3 sit side by side.
        // Horizontal member growth: each group is a row; groups 1-3 stack downward.
        // Group/slot assignments do not change when the visual orientation changes.
        private static void RefreshGroupLayoutGeometry()
        {
            if (_panelRect == null) return;

            bool horizontal = RaidFramesSettings.GrowMembersHorizontally;
            int[] counts = new int[RaidGroupCount + 1];
            if (GameData.PlayerStats != null) counts[1]++;
            if (GameData.RaidManager != null && GameData.RaidManager.Raiders != null)
            {
                foreach (RaidMemberSlot member in GameData.RaidManager.Raiders)
                {
                    if (member != null &&
                        member.GroupNumber >= 1 && member.GroupNumber <= RaidGroupCount &&
                        (member.AssignedSimTracking != null || member.AssignedAvatar != null) &&
                        !(member.AssignedAvatar != null && GameData.PlayerStats != null &&
                          member.AssignedAvatar.MyStats == GameData.PlayerStats))
                    {
                        counts[member.GroupNumber]++;
                    }
                }
            }

            float offset = 0f;
            float widest = 0f;
            float tallest = 0f;
            for (int group = 1; group <= RaidGroupCount; group++)
            {
                // Keep at least one member-sized drop region for empty groups.
                int count = Mathf.Max(1, counts[group]);
                float width = horizontal
                    ? count * RaidFramesSettings.FrameWidth +
                      (count - 1) * RaidFramesSettings.MemberSpacing
                    : RaidFramesSettings.FrameWidth;
                float height = GroupHeaderHeight + (horizontal
                    ? RaidFramesSettings.FrameHeight
                    : count * RaidFramesSettings.FrameHeight +
                      (count - 1) * RaidFramesSettings.MemberSpacing);

                if (GroupObjects[group] != null)
                {
                    RectTransform container = GroupObjects[group].GetComponent<RectTransform>();
                    if (container != null)
                    {
                        // Top-left anchored, so a negative Y moves the next
                        // horizontal row below the preceding group.
                        container.anchoredPosition = horizontal
                            ? new Vector2(0f, -offset)
                            : new Vector2(offset, 0f);
                        container.sizeDelta = new Vector2(width, height);
                    }
                }
                if (GroupContent[group] != null)
                {
                    // The member container stretches to match the group width.
                    GroupContent[group].sizeDelta = new Vector2(0f,
                        Mathf.Max(0f, height - GroupHeaderHeight));
                }

                offset += (horizontal ? height : width) + RaidFramesSettings.GroupSpacing;
                widest = Mathf.Max(widest, width);
                tallest = Mathf.Max(tallest, height);
            }

            _panelRect.sizeDelta = horizontal
                ? new Vector2(widest, Mathf.Max(0f, offset - RaidFramesSettings.GroupSpacing))
                : new Vector2(Mathf.Max(0f, offset - RaidFramesSettings.GroupSpacing), tallest);
        }

        // ============================================================

        private static void BuildGroupContainers()
        {
            float width =
                RaidFramesSettings.FrameWidth;


            float groupHeight =
                GroupHeaderHeight +
                (MaximumMembersPerGroup *
                 RaidFramesSettings.FrameHeight) +
                ((MaximumMembersPerGroup - 1) *
                 RaidFramesSettings.MemberSpacing);


            float panelWidth =
                (RaidGroupCount * width) +
                ((RaidGroupCount - 1) *
                 RaidFramesSettings.GroupSpacing);


            _panelRect.sizeDelta =
                new Vector2(
                    panelWidth,
                    groupHeight);


            for (int group = 1;
                 group <= RaidGroupCount;
                 group++)
            {
                float x =
                    (group - 1) *
                    (width +
                     RaidFramesSettings.GroupSpacing);


                GameObject groupObject =
                    new GameObject(
                        $"RaidGroup_{group}");


                groupObject.transform.SetParent(
                    _panelRect,
                    false);


                RectTransform groupRect =
                    groupObject
                        .AddComponent<RectTransform>();


                groupRect.anchorMin =
                    new Vector2(
                        0f,
                        1f);


                groupRect.anchorMax =
                    new Vector2(
                        0f,
                        1f);


                groupRect.pivot =
                    new Vector2(
                        0f,
                        1f);


                groupRect.anchoredPosition =
                    new Vector2(
                        x,
                        0f);


                groupRect.sizeDelta =
                    new Vector2(
                        width,
                        groupHeight);


                Image editBackground =
                    groupObject
                        .AddComponent<Image>();


                editBackground.color =
                    EditBackgroundColor;


                GroupObjects[group] =
                    groupObject;


                CreateGroupHeader(
                    group,
                    groupRect);


                CreateMemberContainer(
                    group,
                    groupRect);
            }

            RefreshGroupLayoutGeometry();
        }


        // ============================================================
        // GROUP HEADER
        // ============================================================

        private static void CreateGroupHeader(
            int group,
            RectTransform parent)
        {
            GameObject header =
                new GameObject(
                    $"Group{group}_Header");


            header.transform.SetParent(
                parent,
                false);


            RectTransform rect =
                header.AddComponent<RectTransform>();


            rect.anchorMin =
                new Vector2(
                    0f,
                    1f);


            rect.anchorMax =
                new Vector2(
                    1f,
                    1f);


            rect.pivot =
                new Vector2(
                    0.5f,
                    1f);


            rect.anchoredPosition =
                Vector2.zero;


            rect.sizeDelta =
                new Vector2(
                    0f,
                    GroupHeaderHeight);


            Image image =
                header.AddComponent<Image>();


            image.color =
                GroupHeaderColor;


            Text text =
                CreateText(
                    header.transform,
                    $"GROUP {group}",
                    11,
                    TextAnchor.MiddleLeft);


            RectTransform textRect =
                text.rectTransform;


            textRect.offsetMin =
                new Vector2(
                    5f,
                    0f);


            textRect.offsetMax =
                new Vector2(
                    -3f,
                    0f);


            RaidFramesDragHandler drag =
                header.AddComponent<RaidFramesDragHandler>();


            drag.Initialize(
                _panelRect);


            GroupHeaders[group] =
                header;
        }


        // ============================================================
        // MEMBER CONTAINER
        // ============================================================

        private static void CreateMemberContainer(
            int group,
            RectTransform parent)
        {
            GameObject obj =
                new GameObject(
                    "Members");


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
                    1f,
                    1f);


            rect.pivot =
                new Vector2(
                    0.5f,
                    1f);


            rect.anchoredPosition =
                new Vector2(
                    0f,
                    -GroupHeaderHeight);


            rect.sizeDelta =
                new Vector2(
                    0f,
                    0f);


            HorizontalOrVerticalLayoutGroup layout =
                RaidFramesSettings.GrowMembersHorizontally
                    ? (HorizontalOrVerticalLayoutGroup)obj.AddComponent<HorizontalLayoutGroup>()
                    : obj.AddComponent<VerticalLayoutGroup>();


            layout.spacing =
                RaidFramesSettings.MemberSpacing;


            layout.childAlignment =
                TextAnchor.UpperCenter;


            layout.childControlWidth =
                false;


            layout.childControlHeight =
                false;


            layout.childForceExpandWidth =
                false;


            layout.childForceExpandHeight =
                false;


            GroupContent[group] =
                rect;
        }


        // ============================================================
        // RAID MEMBER DRAG / DROP
        // ============================================================

        internal static GameObject GetDropGroupObject(
            int group)
        {
            if (group < 1 ||
                group > RaidGroupCount)
            {
                return null;
            }


            return GroupObjects[group];
        }


        internal static bool CanDropRaidMemberIntoGroup(
            RaidMemberSlot slot,
            int targetGroup)
        {
            if (!GameData.RaidActive ||
                slot == null ||
                targetGroup < 1 ||
                targetGroup > RaidGroupCount)
            {
                return false;
            }


            // Staying in the same group is not an empty-space move. Occupied
            // member frames are handled separately as slot swaps.
            if (slot.GroupNumber == targetGroup)
            {
                return false;
            }


            return GetDisplayedMemberCount(
                       targetGroup) <
                   MaximumMembersPerGroup;
        }


        internal static int GetDropGroup(
            Vector2 screenPosition)
        {
            if (!GameData.RaidActive)
            {
                return 0;
            }


            for (int group = 1;
                 group <= RaidGroupCount;
                 group++)
            {
                GameObject groupObject =
                    GroupObjects[group];


                if (groupObject == null)
                {
                    continue;
                }


                RectTransform rect =
                    groupObject.GetComponent<RectTransform>();


                if (rect != null &&
                    RectTransformUtility.RectangleContainsScreenPoint(
                        rect,
                        screenPosition,
                        null))
                {
                    return group;
                }
            }


            return 0;
        }


        internal static bool TrySwapRaidMembers(
            RaidMemberSlot first,
            RaidMemberSlot second)
        {
            if (!GameData.RaidActive ||
                GameData.RaidManager == null ||
                first == null ||
                second == null ||
                first == second)
            {
                return false;
            }


            int firstGroup =
                first.GroupNumber;

            int secondGroup =
                second.GroupNumber;


            if (firstGroup < 1 ||
                firstGroup > RaidGroupCount ||
                secondGroup < 1 ||
                secondGroup > RaidGroupCount)
            {
                return false;
            }


            if (!TrySynchronizeNativeGroupListsForSwap(
                    first,
                    second,
                    firstGroup,
                    secondGroup))
            {
                Plugin.LogWarning(
                    "Could not synchronize native RaidManager group lists; " +
                    "raid member swap cancelled.");

                return false;
            }


            int firstSlotIndex =
                first.SlotIndex;

            int secondSlotIndex =
                second.SlotIndex;


            // Exchange both group ownership and raid slot index. This keeps
            // the destination group at the same population and makes the two
            // frames occupy each other's actual positions rather than
            // appending the dragged member to the end of the group.
            first.GroupNumber =
                secondGroup;

            second.GroupNumber =
                firstGroup;


            first.SlotIndex =
                secondSlotIndex;

            second.SlotIndex =
                firstSlotIndex;


            SynchronizeAvatarRaidIndex(
                first);

            SynchronizeAvatarRaidIndex(
                second);


            _lastRosterSignature =
                "";


            RebuildRoster();


return true;
        }


        internal static bool TryMoveRaidMemberToGroup(
            RaidMemberSlot slot,
            int targetGroup)
        {
            if (!GameData.RaidActive ||
                GameData.RaidManager == null ||
                slot == null ||
                targetGroup < 1 ||
                targetGroup > RaidGroupCount)
            {
                return false;
            }


            if (slot.GroupNumber == targetGroup)
            {
                return false;
            }


            if (GetDisplayedMemberCount(targetGroup) >=
                MaximumMembersPerGroup)
            {
                Plugin.LogWarning(
                    "Raid Group " +
                    targetGroup +
                    " is full; drag/drop move cancelled.");

                return false;
            }


            if (!TrySynchronizeNativeGroupLists(
                    slot,
                    targetGroup))
            {
                Plugin.LogWarning(
                    "Could not synchronize native RaidManager group lists; " +
                    "drag/drop move cancelled.");

                return false;
            }


            int oldGroup =
                slot.GroupNumber;


            slot.GroupNumber =
                targetGroup;


            _lastRosterSignature =
                "";


            RebuildRoster();


return true;
        }


        private static int GetDisplayedMemberCount(
            int group)
        {
            int count =
                group == 1 && GameData.PlayerStats != null
                    ? 1
                    : 0;


            if (GameData.RaidManager == null ||
                GameData.RaidManager.Raiders == null)
            {
                return count;
            }


            foreach (RaidMemberSlot slot
                     in GameData.RaidManager.Raiders)
            {
                if (slot != null &&
                    slot.GroupNumber == group &&
                    (slot.AssignedSimTracking != null ||
                     slot.AssignedAvatar != null))
                {
                    count++;
                }
            }


            return count;
        }


        private static bool TrySynchronizeNativeGroupListsForSwap(
            RaidMemberSlot first,
            RaidMemberSlot second,
            int firstGroup,
            int secondGroup)
        {
            RaidManager manager =
                GameData.RaidManager;


            if (manager == null ||
                first == null ||
                second == null)
            {
                return false;
            }


            IList group1 =
                ResolveNativeGroupList(
                    manager,
                    "Group1");

            IList group2 =
                ResolveNativeGroupList(
                    manager,
                    "Group2");

            IList group3 =
                ResolveNativeGroupList(
                    manager,
                    "Group3");


            if (group1 == null ||
                group2 == null ||
                group3 == null)
            {
                return false;
            }


            IList firstDestination =
                secondGroup == 1
                    ? group1
                    : (secondGroup == 2
                        ? group2
                        : group3);


            IList secondDestination =
                firstGroup == 1
                    ? group1
                    : (firstGroup == 2
                        ? group2
                        : group3);


            RemoveSlotFromList(
                group1,
                first);

            RemoveSlotFromList(
                group2,
                first);

            RemoveSlotFromList(
                group3,
                first);


            RemoveSlotFromList(
                group1,
                second);

            RemoveSlotFromList(
                group2,
                second);

            RemoveSlotFromList(
                group3,
                second);


            if (!ContainsSlot(
                    firstDestination,
                    first))
            {
                firstDestination.Add(
                    first);
            }


            if (!ContainsSlot(
                    secondDestination,
                    second))
            {
                secondDestination.Add(
                    second);
            }


            return true;
        }


        private static void SynchronizeAvatarRaidIndex(
            RaidMemberSlot slot)
        {
            if (slot == null ||
                slot.AssignedAvatar == null)
            {
                return;
            }


            NPC npc =
                slot.AssignedAvatar.GetThisNPC();


            if (npc == null)
            {
                return;
            }


            npc.indexInRaid =
                slot.SlotIndex;
        }


        private static bool TrySynchronizeNativeGroupLists(
            RaidMemberSlot slot,
            int targetGroup)
        {
            RaidManager manager =
                GameData.RaidManager;


            if (manager == null ||
                slot == null)
            {
                return false;
            }


            IList group1 =
                ResolveNativeGroupList(
                    manager,
                    "Group1");

            IList group2 =
                ResolveNativeGroupList(
                    manager,
                    "Group2");

            IList group3 =
                ResolveNativeGroupList(
                    manager,
                    "Group3");


            if (group1 == null ||
                group2 == null ||
                group3 == null)
            {
                return false;
            }


            IList destination =
                targetGroup == 1
                    ? group1
                    : (targetGroup == 2
                        ? group2
                        : group3);


            RemoveSlotFromList(
                group1,
                slot);

            RemoveSlotFromList(
                group2,
                slot);

            RemoveSlotFromList(
                group3,
                slot);


            if (!ContainsSlot(
                    destination,
                    slot))
            {
                destination.Add(
                    slot);
            }


            return true;
        }


        private static IList ResolveNativeGroupList(
            RaidManager manager,
            string name)
        {
            Type type =
                manager.GetType();


            FieldInfo field =
                type.GetField(
                    name,
                    BindingFlags.Instance |
                    BindingFlags.Public |
                    BindingFlags.NonPublic);


            if (field != null)
            {
                return field.GetValue(
                    manager) as IList;
            }


            PropertyInfo property =
                type.GetProperty(
                    name,
                    BindingFlags.Instance |
                    BindingFlags.Public |
                    BindingFlags.NonPublic);


            if (property != null &&
                property.CanRead)
            {
                return property.GetValue(
                    manager,
                    null) as IList;
            }


            return null;
        }


        private static void RemoveSlotFromList(
            IList list,
            RaidMemberSlot slot)
        {
            if (list == null ||
                slot == null)
            {
                return;
            }


            for (int i = list.Count - 1;
                 i >= 0;
                 i--)
            {
                if (object.ReferenceEquals(
                        list[i],
                        slot))
                {
                    list.RemoveAt(
                        i);
                }
            }
        }


        private static bool ContainsSlot(
            IList list,
            RaidMemberSlot slot)
        {
            if (list == null ||
                slot == null)
            {
                return false;
            }


            for (int i = 0;
                 i < list.Count;
                 i++)
            {
                if (object.ReferenceEquals(
                        list[i],
                        slot))
                {
                    return true;
                }
            }


            return false;
        }


        // ============================================================
        // LOCK STATE
        // ============================================================

        internal static void SetLocked(
            bool locked)
        {
            RaidFramesSettings.SetLocked(
                locked);


            ApplyLockState();

        }


        internal static void ToggleLocked()
        {
            SetLocked(
                !RaidFramesSettings.Locked);
        }


        private static void ApplyLockState()
        {
            bool unlocked =
                !RaidFramesSettings.Locked;


            for (int group = 1;
                 group <= RaidGroupCount;
                 group++)
            {
                if (GroupHeaders[group] != null)
                {
                    GroupHeaders[group]
                        .SetActive(
                            unlocked);
                }


                if (GroupObjects[group] != null)
                {
                    Image background =
                        GroupObjects[group]
                            .GetComponent<Image>();


                    if (background != null)
                    {
                        background.enabled =
                            unlocked;
                    }
                }


                if (GroupContent[group] != null)
                {
                    GroupContent[group]
                        .anchoredPosition =
                            new Vector2(
                                0f,
                                unlocked
                                    ? -GroupHeaderHeight
                                    : 0f);
                }
            }
        }


        // ============================================================
        // SETTINGS CHANGED
        // ============================================================

        internal static void SettingsChanged()
        {
            RecreateUi();
        }


        private static void RecreateUi()
        {
            ClearMemberFrames();


            _savedPosition =
                RaidFramesSettings.GetPosition();


            if (_panelRect != null)
            {
                _panelRect.anchoredPosition =
                    _savedPosition;
            }


            for (int group = 1;
                 group <= RaidGroupCount;
                 group++)
            {
                if (GroupObjects[group] != null)
                {
                    UnityEngine.Object.Destroy(
                        GroupObjects[group]);
                }


                GroupObjects[group] =
                    null;


                GroupHeaders[group] =
                    null;


                GroupContent[group] =
                    null;
            }


            BuildGroupContainers();
            ApplyTheme();


            ApplyLockState();


            RebuildRoster();
        }


        // ============================================================
        // UPDATE
        // ============================================================

        internal static void Update()
        {
            if (_panelObject == null)
            {
                return;
            }


            bool raidActive =
                GameData.RaidActive && ArcaneUiModuleSettings.RaidFramesEnabled;


            if (raidActive !=
                _lastRaidActive)
            {
                _lastRaidActive =
                    raidActive;


                SetVisible(
                    raidActive);


                if (raidActive)
                {
                    RebuildRoster();
                }
                else
                {
                    ClearMemberFrames();

                    _lastRosterSignature =
                        "";
                }
            }


            SetVisible(
                raidActive &&
                NativeUiLayering
                    .CanShowGameplayUi);


            if (!raidActive ||
                !NativeUiLayering
                    .CanShowGameplayUi)
            {
                return;
            }


            _healthRefreshTimer -=
                Time.unscaledDeltaTime;


            if (_healthRefreshTimer <= 0f)
            {
                _healthRefreshTimer =
                    0.10f;


                RefreshMemberFrames();
            }


            _rosterRefreshTimer -=
                Time.unscaledDeltaTime;


            if (_rosterRefreshTimer <= 0f)
            {
                _rosterRefreshTimer =
                    0.50f;


                string signature =
                    BuildRosterSignature();


                if (signature !=
                    _lastRosterSignature)
                {
                    RebuildRoster();
                }
            }
        }


        // ============================================================
        // ROSTER
        // ============================================================



        internal static void ForceRosterRefresh()
        {
            _lastRosterSignature =
                string.Empty;

            RebuildRoster();
        }


        private static void RebuildRoster()
        {
            ClearMemberFrames();


            if (!GameData.RaidActive ||
                GameData.RaidManager == null)
            {
                return;
            }


            // --------------------------------------------------------
            // PLAYER -> GROUP 1
            // --------------------------------------------------------

            if (GameData.PlayerStats != null &&
                GroupContent[1] != null)
            {
                MemberFrames.Add(
                    new RaidMemberFrame(
                        GroupContent[1]));
            }


            // --------------------------------------------------------
            // RAID MEMBERS
            // --------------------------------------------------------

            List<RaidMemberSlot> slots =
                new List<RaidMemberSlot>();


            foreach (
                RaidMemberSlot slot
                in GameData.RaidManager.Raiders)
            {
                if (slot == null)
                {
                    continue;
                }


                if (slot.GroupNumber < 1 ||
                    slot.GroupNumber > RaidGroupCount)
                {
                    continue;
                }


                if (slot.AssignedSimTracking == null &&
                    slot.AssignedAvatar == null)
                {
                    continue;
                }


                slots.Add(
                    slot);
            }


            slots.Sort(
                (a, b) =>
                    a.SlotIndex.CompareTo(
                        b.SlotIndex));


            foreach (
                RaidMemberSlot slot
                in slots)
            {
                if (slot.AssignedAvatar != null &&
                    GameData.PlayerStats != null &&
                    slot.AssignedAvatar.MyStats ==
                    GameData.PlayerStats)
                {
                    continue;
                }


                RectTransform parent =
                    GroupContent[
                        slot.GroupNumber];


                if (parent == null)
                {
                    continue;
                }


                MemberFrames.Add(
                    new RaidMemberFrame(
                        parent,
                        slot));
            }


            RefreshGroupLayoutGeometry();
            _lastRosterSignature =
                BuildRosterSignature();


            RefreshMemberFrames();
        }


        // ============================================================
        // SIGNATURE
        // ============================================================

        private static string BuildRosterSignature()
        {
            if (!GameData.RaidActive ||
                GameData.RaidManager == null)
            {
                return "";
            }


            StringBuilder builder =
                new StringBuilder();


            foreach (
                RaidMemberSlot slot
                in GameData.RaidManager.Raiders)
            {
                if (slot == null)
                {
                    continue;
                }


                builder.Append(
                    slot.SlotIndex);


                builder.Append(
                    ':');


                builder.Append(
                    slot.GroupNumber);


                builder.Append(
                    ':');


                if (slot.AssignedSimTracking != null)
                {
                    builder.Append(
                        slot.AssignedSimTracking
                            .SimName);
                }


                builder.Append(
                    ';');
            }


            return builder.ToString();
        }


        // ============================================================
        // MEMBER REFRESH
        // ============================================================

        private static void RefreshMemberFrames()
        {
            for (int i = 0;
                 i < MemberFrames.Count;
                 i++)
            {
                MemberFrames[i]
                    ?.Refresh();
            }
        }


        private static void ClearMemberFrames()
        {
            for (int i = 0;
                 i < MemberFrames.Count;
                 i++)
            {
                MemberFrames[i]
                    ?.Destroy();
            }


            MemberFrames.Clear();
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

        internal static void StoreCurrentPosition()
        {
            if (_panelRect == null)
            {
                return;
            }


            _savedPosition =
                _panelRect
                    .anchoredPosition;


            RaidFramesSettings
                .SetPosition(
                    _savedPosition);


}


        // ============================================================
        // TEXT FACTORY
        // ============================================================

        private static Text CreateText(
            Transform parent,
            string value,
            int fontSize,
            TextAnchor alignment)
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
                Vector2.zero;


            rect.offsetMax =
                Vector2.zero;


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


        // ============================================================
        // SHUTDOWN
        // ============================================================

        internal static void Shutdown()
        {
            ClearMemberFrames();


            if (_canvasObject != null)
            {
                UnityEngine.Object.Destroy(
                    _canvasObject);
            }


            _canvasObject =
                null;


            _panelObject =
                null;


            _panelRect =
                null;


            _lastRaidActive =
                false;


            _lastRosterSignature =
                "";
        }
    }
}