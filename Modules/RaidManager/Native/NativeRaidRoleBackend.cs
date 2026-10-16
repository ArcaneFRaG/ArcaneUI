using System.Collections.Generic;

namespace ArcanesGroupFrames
{
    internal enum RaidRoleKind
    {
        MainAssistMainTank,
        Puller,
        Healer,
        CrowdControl
    }


    internal static class NativeRaidRoleBackend
    {
        private static RaidManager _cleanedManager;


        internal static void EnsureNativeManagementUiHidden()
        {
            RaidManager manager =
                GameData.RaidManager;


            if (manager == null ||
                manager == _cleanedManager)
            {
                return;
            }


            _cleanedManager =
                manager;


            SetActive(
                manager.G1Roles,
                false);

            SetActive(
                manager.G2Roles,
                false);

            SetActive(
                manager.G3Roles,
                false);

            SetActive(
                manager.Group1RolesUI,
                false);

            SetActive(
                manager.Group2RolesUI,
                false);

            SetActive(
                manager.Group3RolesUI,
                false);

            SetActive(
                manager.Group1HealSliders,
                false);

            SetActive(
                manager.Group2HealSliders,
                false);

            SetActive(
                manager.Group3HealSliders,
                false);


            manager.ActiveHealerSettingIndex =
                -1;
        }


        internal static void Reset()
        {
            RaidManager manager =
                GameData.RaidManager;


            if (manager != null)
            {
                SetActive(
                    manager.G1Roles,
                    false);

                SetActive(
                    manager.G2Roles,
                    false);

                SetActive(
                    manager.G3Roles,
                    false);

                SetActive(
                    manager.Group1HealSliders,
                    false);

                SetActive(
                    manager.Group2HealSliders,
                    false);

                SetActive(
                    manager.Group3HealSliders,
                    false);

                SetActive(
                    manager.Group1RolesUI,
                    true);

                SetActive(
                    manager.Group2RolesUI,
                    true);

                SetActive(
                    manager.Group3RolesUI,
                    true);


                manager.ActiveHealerSettingIndex =
                    -1;
            }


            _cleanedManager =
                null;
        }


        internal static List<RaidMemberSlot> GetGroupMembers(
            int group)
        {
            List<RaidMemberSlot> result =
                new List<RaidMemberSlot>();


            RaidManager manager =
                GameData.RaidManager;


            if (manager == null ||
                manager.Raiders == null)
            {
                return result;
            }


            for (int i = 0;
                 i < manager.Raiders.Count;
                 i++)
            {
                RaidMemberSlot slot =
                    manager.Raiders[i];


                if (slot == null ||
                    slot.GroupNumber != group ||
                    slot.AssignedSimTracking == null)
                {
                    continue;
                }


                result.Add(
                    slot);
            }


            result.Sort(
                delegate(
                    RaidMemberSlot left,
                    RaidMemberSlot right)
                {
                    return left.SlotIndex.CompareTo(
                        right.SlotIndex);
                });


            return result;
        }


        internal static string GetMemberName(
            RaidMemberSlot slot)
        {
            if (slot == null)
            {
                return "<empty>";
            }


            if (slot.AssignedSimTracking != null &&
                !string.IsNullOrEmpty(
                    slot.AssignedSimTracking.SimName))
            {
                return slot.AssignedSimTracking.SimName;
            }


            if (slot.AssignedAvatar != null)
            {
                NPC npc =
                    slot.AssignedAvatar.GetThisNPC();


                if (npc != null &&
                    !string.IsNullOrEmpty(
                        npc.NPCName))
                {
                    return npc.NPCName;
                }
            }


            return "<empty>";
        }


        internal static string GetPlayerName()
        {
            if (GameData.PlayerStats != null &&
                !string.IsNullOrEmpty(
                    GameData.PlayerStats.MyName))
            {
                return GameData.PlayerStats.MyName;
            }


            return "Player";
        }


        internal static bool IsRoleActive(
            RaidMemberSlot slot,
            RaidRoleKind role)
        {
            if (slot == null)
            {
                return false;
            }


            switch (role)
            {
                case RaidRoleKind.MainAssistMainTank:
                    return slot.MainAssistMainTank;

                case RaidRoleKind.Puller:
                    return slot.Puller;

                case RaidRoleKind.Healer:
                    return slot.Healer;

                case RaidRoleKind.CrowdControl:
                    return slot.CrowdControl;
            }


            return false;
        }


        internal static bool IsPlayerRoleActive(
            RaidRoleKind role)
        {
            RaidManager manager =
                GameData.RaidManager;


            if (manager == null)
            {
                return false;
            }


            switch (role)
            {
                case RaidRoleKind.MainAssistMainTank:
                    return manager.PlayerIsG1MA;

                case RaidRoleKind.Puller:
                    return manager.PlayerIsG1Puller;

                case RaidRoleKind.Healer:
                    return manager.PlayerIsFlaggedHeals;

                case RaidRoleKind.CrowdControl:
                    return manager.PlayerIsFlaggedCC;
            }


            return false;
        }


        internal static void ToggleRole(
            RaidMemberSlot slot,
            int group,
            RaidRoleKind role)
        {
            RaidManager manager =
                GameData.RaidManager;


            if (manager == null ||
                slot == null ||
                slot.AssignedAvatar == null ||
                slot.GroupNumber != group)
            {
                return;
            }


            switch (role)
            {
                case RaidRoleKind.MainAssistMainTank:
                    SelectMainAssistMainTank(
                        manager,
                        slot,
                        group);
                    break;

                case RaidRoleKind.Puller:
                    SelectPuller(
                        manager,
                        slot,
                        group);
                    break;

                case RaidRoleKind.Healer:
                    slot.Healer =
                        !slot.Healer;

                    SyncListRole(
                        GetHealerList(
                            manager,
                            group),
                        slot.AssignedAvatar,
                        slot.Healer);
                    break;

                case RaidRoleKind.CrowdControl:
                    slot.CrowdControl =
                        !slot.CrowdControl;

                    SyncListRole(
                        GetCrowdControlList(
                            manager,
                            group),
                        slot.AssignedAvatar,
                        slot.CrowdControl);
                    break;
            }
        }


        internal static void TogglePlayerRole(
            RaidRoleKind role)
        {
            RaidManager manager =
                GameData.RaidManager;


            if (manager == null)
            {
                return;
            }


            switch (role)
            {
                case RaidRoleKind.MainAssistMainTank:
                    manager.PlayerIsG1MA =
                        true;

                    manager.G1MT =
                        null;

                    ClearExclusiveRole(
                        manager,
                        1,
                        true);
                    break;

                case RaidRoleKind.Puller:
                    manager.PlayerIsG1Puller =
                        true;

                    manager.G1Pull =
                        null;

                    ClearExclusiveRole(
                        manager,
                        1,
                        false);
                    break;

                case RaidRoleKind.Healer:
                    manager.PlayerIsFlaggedHeals =
                        !manager.PlayerIsFlaggedHeals;
                    break;

                case RaidRoleKind.CrowdControl:
                    manager.PlayerIsFlaggedCC =
                        !manager.PlayerIsFlaggedCC;
                    break;
            }
        }


        private static void SelectMainAssistMainTank(
            RaidManager manager,
            RaidMemberSlot slot,
            int group)
        {
            // Native RaidRoleButton deliberately refuses to toggle the
            // currently assigned MT/MA off. Selecting another member moves
            // the role instead.
            if (slot.MainAssistMainTank)
            {
                return;
            }


            ClearExclusiveRole(
                manager,
                group,
                true);


            slot.MainAssistMainTank =
                true;


            SetMainTank(
                manager,
                group,
                slot.AssignedAvatar);


            if (group == 1)
            {
                manager.PlayerIsG1MA =
                    false;
            }
        }


        private static void SelectPuller(
            RaidManager manager,
            RaidMemberSlot slot,
            int group)
        {
            if (slot.Puller)
            {
                return;
            }


            ClearExclusiveRole(
                manager,
                group,
                false);


            slot.Puller =
                true;


            SetPuller(
                manager,
                group,
                slot.AssignedAvatar);


            if (group == 1)
            {
                manager.PlayerIsG1Puller =
                    false;
            }
        }


        private static void ClearExclusiveRole(
            RaidManager manager,
            int group,
            bool mainAssistMainTank)
        {
            if (manager == null ||
                manager.Raiders == null)
            {
                return;
            }


            for (int i = 0;
                 i < manager.Raiders.Count;
                 i++)
            {
                RaidMemberSlot candidate =
                    manager.Raiders[i];


                if (candidate == null ||
                    candidate.GroupNumber != group)
                {
                    continue;
                }


                if (mainAssistMainTank)
                {
                    candidate.MainAssistMainTank =
                        false;
                }
                else
                {
                    candidate.Puller =
                        false;
                }
            }
        }


        private static void SetMainTank(
            RaidManager manager,
            int group,
            SimPlayer avatar)
        {
            if (group == 1)
            {
                manager.G1MT =
                    avatar;
            }
            else if (group == 2)
            {
                manager.G2MT =
                    avatar;
            }
            else if (group == 3)
            {
                manager.G3MT =
                    avatar;
            }
        }


        private static void SetPuller(
            RaidManager manager,
            int group,
            SimPlayer avatar)
        {
            if (group == 1)
            {
                manager.G1Pull =
                    avatar;
            }
            else if (group == 2)
            {
                manager.G2Pull =
                    avatar;
            }
            else if (group == 3)
            {
                manager.G3Pull =
                    avatar;
            }
        }


        private static List<SimPlayer> GetHealerList(
            RaidManager manager,
            int group)
        {
            if (group == 1)
            {
                return manager.G1Heals;
            }


            if (group == 2)
            {
                return manager.G2Heals;
            }


            return manager.G3Heals;
        }


        private static List<SimPlayer> GetCrowdControlList(
            RaidManager manager,
            int group)
        {
            if (group == 1)
            {
                return manager.G1CC;
            }


            if (group == 2)
            {
                return manager.G2CC;
            }


            return manager.G3CC;
        }


        private static void SyncListRole(
            List<SimPlayer> list,
            SimPlayer avatar,
            bool enabled)
        {
            if (list == null ||
                avatar == null)
            {
                return;
            }


            if (enabled)
            {
                if (!list.Contains(
                        avatar))
                {
                    list.Add(
                        avatar);
                }
            }
            else
            {
                while (list.Remove(
                    avatar))
                {
                }
            }
        }


        private static void SetActive(
            UnityEngine.GameObject obj,
            bool active)
        {
            if (obj != null &&
                obj.activeSelf != active)
            {
                obj.SetActive(
                    active);
            }
        }
    }
}
