using UnityEngine;

namespace ArcanesGroupFrames
{
    internal static class RaidGroupCommands
    {
        // ============================================================
        // CONSTANTS
        // ============================================================

        private const int MinGroupNumber =
            1;

        private const int MaxGroupNumber =
            3;


        // ============================================================
        // ASSIGNED TARGET CACHE
        //
        // Index 0 is unused.
        // Groups 1-3 use indexes 1-3.
        // ============================================================

        private static readonly Character[]
            AssignedTargets =
                new Character[MaxGroupNumber + 1];


        // ============================================================
        // ASSIGN TARGET
        // ============================================================

        internal static void AssignTarget(
            int groupNumber)
        {
            if (!ValidateRaid(
                groupNumber))
            {
                return;
            }


            Character target =
                GameData.PlayerControl?
                    .CurrentTarget;


            if (!IsValidHostileTarget(
                target))
            {
                Plugin.LogWarning(
                    $"Group {groupNumber}: " +
                    "no valid target selected.");

                return;
            }


            AssignedTargets[groupNumber] =
                target;


            GameData.RaidManager
                .AssignTargetToGroup(
                    groupNumber,
                    target);


}


        // ============================================================
        // ATTACK
        // ============================================================

        internal static void Attack(
            int groupNumber)
        {
            if (!ValidateRaid(
                groupNumber))
            {
                return;
            }


            Character target =
                ResolveTarget(
                    groupNumber);


            if (!IsValidHostileTarget(
                target))
            {
                Plugin.LogWarning(
                    $"Group {groupNumber}: " +
                    "no assigned or selected target.");

                return;
            }


            GameData.RaidManager
                .SimPlayerPullerOrderAttack(
                    groupNumber,
                    target);


}


        // ============================================================
        // PULL
        // ============================================================

        internal static void Pull(
            int groupNumber)
        {
            if (!ValidateRaid(
                groupNumber))
            {
                return;
            }


            Character target =
                ResolveTarget(
                    groupNumber);


            if (!IsValidHostileTarget(
                target))
            {
                Plugin.LogWarning(
                    $"Group {groupNumber}: " +
                    "no assigned or selected target to pull.");

                return;
            }


            RaidMemberSlot puller =
                FindPuller(
                    groupNumber);


            if (puller == null ||
                puller.AssignedAvatar == null)
            {
                Plugin.LogWarning(
                    $"Group {groupNumber}: " +
                    "could not find an active puller.");

                return;
            }


            foreach (
                RaidMemberSlot slot
                in GameData.RaidManager.Raiders)
            {
                if (slot == null ||
                    slot.GroupNumber != groupNumber)
                {
                    continue;
                }


                bool isPuller =
                    slot == puller;


                slot.Puller =
                    isPuller;


                if (slot.AssignedAvatar == null)
                {
                    continue;
                }


                SimPlayer avatar =
                    slot.AssignedAvatar;


                if (isPuller)
                {
                    avatar.FreeFollow();

                    avatar.PullTarget =
                        target;

                    avatar.IgnoreAllCombat =
                        true;
                }
                else
                {
                    avatar.ClearPullTarget();

                    avatar.IgnoreAllCombat =
                        false;
                }
            }


}


        // ============================================================
        // FOLLOW
        // ============================================================

        internal static void Follow(
            int groupNumber)
        {
            if (!ValidateRaid(
                groupNumber))
            {
                return;
            }


            foreach (
                RaidMemberSlot slot
                in GameData.RaidManager.Raiders)
            {
                if (slot == null ||
                    slot.GroupNumber != groupNumber ||
                    slot.AssignedAvatar == null)
                {
                    continue;
                }


                SimPlayer avatar =
                    slot.AssignedAvatar;


                avatar.ClearPullTarget();

                avatar.IgnoreAllCombat =
                    false;


                avatar.FreeFollow();


                NPC npc =
                    avatar.GetThisNPC();


                if (npc != null)
                {
                    npc.CurrentAggroTarget =
                        null;
                }


                slot.Puller =
                    false;
            }


}


        // ============================================================
        // HERE
        // ============================================================

        internal static void Here(
            int groupNumber)
        {
            if (!ValidateRaid(
                groupNumber))
            {
                return;
            }


            if (GameData.PlayerControl == null)
            {
                return;
            }


            Vector3 position =
                GameData.PlayerControl
                    .transform
                    .position;


            foreach (
                RaidMemberSlot slot
                in GameData.RaidManager.Raiders)
            {
                if (slot == null ||
                    slot.GroupNumber != groupNumber ||
                    slot.AssignedAvatar == null)
                {
                    continue;
                }


                SimPlayer avatar =
                    slot.AssignedAvatar;


                avatar.ClearPullTarget();

                avatar.IgnoreAllCombat =
                    false;


                avatar.AssignGuardSpot(
                    position);


                NPC npc =
                    avatar.GetThisNPC();


                if (npc != null)
                {
                    npc.CurrentAggroTarget =
                        null;
                }


                slot.Puller =
                    false;
            }


}


        // ============================================================
        // GLOBAL RAID COMMANDS
        // ============================================================

        internal static void AllAttack()
        {
            if (!GameData.RaidActive ||
                GameData.RaidManager == null)
            {
                return;
            }


            GameData.RaidManager
                .OrderAttack();


}


        internal static void AllPull()
        {
            if (!GameData.RaidActive ||
                GameData.RaidManager == null)
            {
                return;
            }


            GameData.RaidManager
                .OrderRaiderToPullTarget();


}


        // ============================================================
        // RAID DPS
        // ============================================================

        internal static void SetRaidDps(
            float value)
        {
            if (!GameData.RaidActive ||
                GameData.RaidManager == null)
            {
                return;
            }


            value =
                Mathf.Clamp(
                    Mathf.Round(value),
                    0f,
                    100f);


            foreach (
                RaidMemberSlot slot
                in GameData.RaidManager.Raiders)
            {
                if (slot == null)
                {
                    continue;
                }


                // ----------------------------------------------------
                // Persistent SimPlayer value.
                // ----------------------------------------------------

                if (slot.AssignedSimTracking != null)
                {
                    slot.AssignedSimTracking
                        .HoldDPSNum =
                            value;
                }


                // ----------------------------------------------------
                // Live spawned value.
                // ----------------------------------------------------

                if (slot.AssignedAvatar != null)
                {
                    NPC npc =
                        slot.AssignedAvatar
                            .GetThisNPC();


                    if (npc != null)
                    {
                        npc.HoldDPS =
                            value;
                    }
                }
            }
        }


        // ============================================================
        // RAID SPREAD
        // ============================================================

        internal static void SetRaidSpread(
            float value)
        {
            if (!GameData.RaidActive ||
                GameData.RaidManager == null)
            {
                return;
            }


            value =
                Mathf.Clamp(
                    Mathf.Round(value),
                    0f,
                    20f);


            GameData.RaidManager
                .RaidSpreadAddition =
                    value;
        }


        // ============================================================
        // ASSIGNED TARGET ACCESS
        // ============================================================

        internal static Character GetAssignedTarget(
            int groupNumber)
        {
            if (groupNumber < MinGroupNumber ||
                groupNumber > MaxGroupNumber)
            {
                return null;
            }


            Character target =
                AssignedTargets[groupNumber];


            // --------------------------------------------------------
            // Clear destroyed/dead/invalid targets automatically.
            // --------------------------------------------------------

            if (target != null &&
                !IsValidHostileTarget(target))
            {
                AssignedTargets[groupNumber] =
                    null;

                return null;
            }


            return target;
        }


        internal static string GetAssignedTargetName(
            int groupNumber)
        {
            Character target =
                GetAssignedTarget(
                    groupNumber);


            if (target == null)
            {
                return "None";
            }


            return GetCharacterName(
                target);
        }


        // ============================================================
        // TARGET RESOLUTION
        // ============================================================

        private static Character ResolveTarget(
            int groupNumber)
        {
            Character target =
                GetAssignedTarget(
                    groupNumber);


            if (IsValidHostileTarget(
                target))
            {
                return target;
            }


            // --------------------------------------------------------
            // Attack/Pull can still fall back to current player target.
            //
            // This does NOT mark it as the group's assigned target.
            // Only Assign Targ. does that.
            // --------------------------------------------------------

            target =
                GameData.PlayerControl?
                    .CurrentTarget;


            if (IsValidHostileTarget(
                target))
            {
                return target;
            }


            return null;
        }


        // ============================================================
        // PULLER
        // ============================================================

        private static RaidMemberSlot FindPuller(
            int groupNumber)
        {
            RaidMemberSlot fallback =
                null;


            foreach (
                RaidMemberSlot slot
                in GameData.RaidManager.Raiders)
            {
                if (slot == null ||
                    slot.GroupNumber != groupNumber ||
                    slot.AssignedAvatar == null)
                {
                    continue;
                }


                if (fallback == null)
                {
                    fallback =
                        slot;
                }


                if (slot.Puller)
                {
                    return slot;
                }
            }


            return fallback;
        }


        // ============================================================
        // TARGET VALIDATION
        // ============================================================

        private static bool IsValidHostileTarget(
            Character target)
        {
            if (target == null)
            {
                return false;
            }


            if (!target.Alive)
            {
                return false;
            }


            if (!target.isNPC)
            {
                return false;
            }


            if (target.MyNPC == null)
            {
                return false;
            }


            if (target.MyStats != null &&
                target.MyStats.Charmed)
            {
                return false;
            }


            if (target.MyNPC.SimPlayer)
            {
                return false;
            }


            return true;
        }


        // ============================================================
        // CHARACTER NAME
        // ============================================================

        private static string GetCharacterName(
            Character target)
        {
            if (target == null)
            {
                return "None";
            }


            if (target.MyNPC != null &&
                !string.IsNullOrWhiteSpace(
                    target.MyNPC.NPCName))
            {
                return target.MyNPC.NPCName;
            }


            if (!string.IsNullOrWhiteSpace(
                    target.name))
            {
                return target.name;
            }


            if (target.transform != null &&
                !string.IsNullOrWhiteSpace(
                    target.transform.name))
            {
                return target.transform.name;
            }


            return "Unknown";
        }


        // ============================================================
        // RAID VALIDATION
        // ============================================================

        private static bool ValidateRaid(
            int groupNumber)
        {
            if (!GameData.RaidActive)
            {
                return false;
            }


            if (GameData.RaidManager == null)
            {
                Plugin.LogWarning(
                    "RaidManager is unavailable.");

                return false;
            }


            if (groupNumber < MinGroupNumber ||
                groupNumber > MaxGroupNumber)
            {
                Plugin.LogWarning(
                    $"Invalid raid group: {groupNumber}");

                return false;
            }


            return true;
        }


        // ============================================================
        // RESET
        // ============================================================

        internal static void Reset()
        {
            for (int i = 0;
                 i < AssignedTargets.Length;
                 i++)
            {
                AssignedTargets[i] =
                    null;
            }
        }
    }
}