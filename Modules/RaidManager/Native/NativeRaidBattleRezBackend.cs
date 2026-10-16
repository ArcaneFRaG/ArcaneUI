using UnityEngine;

namespace ArcanesGroupFrames
{
    internal static class NativeRaidBattleRezBackend
    {
        internal const int MaximumBattleRezzes =
            3;


        internal static int GetAvailableBattleRezzes()
        {
            RaidManager manager =
                GameData.RaidManager;


            if (manager == null)
            {
                return 0;
            }


            return Mathf.Clamp(
                manager.RaidLevel,
                0,
                MaximumBattleRezzes);
        }


        internal static int GetCurrentRaidXP()
        {
            RaidManager manager =
                GameData.RaidManager;


            return manager != null
                ? Mathf.Max(
                    0,
                    manager.CurrentRaidXP)
                : 0;
        }


        internal static int GetRaidXPForNextBattleRez()
        {
            RaidManager manager =
                GameData.RaidManager;


            return manager != null
                ? Mathf.Max(
                    1,
                    manager.RaidXPForLevel)
                : 1;
        }


        internal static float GetRaidXPProgress()
        {
            if (GetAvailableBattleRezzes() >=
                MaximumBattleRezzes)
            {
                return 1f;
            }


            return Mathf.Clamp01(
                (float)GetCurrentRaidXP() /
                GetRaidXPForNextBattleRez());
        }


        internal static bool CanBattleRez(
            RaidMemberSlot slot)
        {
            if (slot == null ||
                slot.AssignedAvatar == null ||
                slot.AssignedAvatar.MyStats == null ||
                GetAvailableBattleRezzes() <= 0)
            {
                return false;
            }


            NPC npc =
                slot.AssignedAvatar.GetThisNPC();


            Character character =
                npc != null
                    ? npc.GetChar()
                    : null;


            return
                slot.AssignedAvatar.MyStats.CurrentHP <= 0 ||
                (character != null &&
                 !character.Alive);
        }


        internal static bool CanBattleRezPlayer()
        {
            if (GetAvailableBattleRezzes() <= 0 ||
                GameData.PlayerControl == null ||
                GameData.PlayerControl.Myself == null ||
                GameData.PlayerStats == null)
            {
                return false;
            }


            return
                GameData.PlayerStats.CurrentHP <= 0 ||
                !GameData.PlayerControl.Myself.Alive;
        }


        internal static void BattleRez(
            RaidMemberSlot slot)
        {
            if (!CanBattleRez(
                    slot))
            {
                return;
            }


            // Native Erenshor implementation:
            // validates that a raider is still alive, consumes one RaidLevel,
            // revives the SimPlayer, restores HP and spawns ReviveFX.
            slot.DoBattleRez();
        }


        internal static void BattleRezPlayer()
        {
            if (!CanBattleRezPlayer())
            {
                return;
            }


            RaidManager manager =
                GameData.RaidManager;


            if (manager == null ||
                manager.PlayerNameCard == null)
            {
                return;
            }


            // Native Erenshor player-card path. It performs the same
            // validation, UsedBattleRez(), RaidRevive(), HP restoration
            // and ReviveFX as the original raid UI.
            manager.PlayerNameCard
                .DoBattleRez();
        }
    }
}
