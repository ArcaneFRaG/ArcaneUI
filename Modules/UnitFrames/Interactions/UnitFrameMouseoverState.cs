using UnityEngine;

namespace ArcanesGroupFrames
{
    internal static class UnitFrameMouseoverState
    {
        // ============================================================
        // CURRENT HOVERED UNIT
        // ============================================================

        internal static Character Character
        {
            get;
            private set;
        }

        internal static SimPlayer SimPlayer
        {
            get;
            private set;
        }

        internal static SimPlayerTracking Tracking
        {
            get;
            private set;
        }

        internal static RaidMemberSlot RaidSlot
        {
            get;
            private set;
        }

        internal static bool IsPlayer
        {
            get;
            private set;
        }

        internal static bool IsPartyFrame
        {
            get;
            private set;
        }

        internal static bool HasUnit =>
            Character != null;


        // ============================================================
        // OWNER
        //
        // Used so one frame cannot accidentally clear the hover state
        // after the pointer has already entered a different frame.
        // ============================================================

        private static Object _owner;


        // ============================================================
        // SET
        // ============================================================

        internal static void Set(
            Object owner,
            Character character,
            SimPlayer simPlayer,
            SimPlayerTracking tracking,
            RaidMemberSlot raidSlot,
            bool isPlayer,
            bool isPartyFrame)
        {
            _owner =
                owner;

            Character =
                character;

            SimPlayer =
                simPlayer;

            Tracking =
                tracking;

            RaidSlot =
                raidSlot;

            IsPlayer =
                isPlayer;

            IsPartyFrame =
                isPartyFrame;
        }


        // ============================================================
        // CLEAR
        // ============================================================

        internal static void Clear(
            Object owner)
        {
            if (_owner != owner)
            {
                return;
            }

            Reset();
        }


        internal static void Reset()
        {
            _owner =
                null;

            Character =
                null;

            SimPlayer =
                null;

            Tracking =
                null;

            RaidSlot =
                null;

            IsPlayer =
                false;

            IsPartyFrame =
                false;
        }
    }
}
