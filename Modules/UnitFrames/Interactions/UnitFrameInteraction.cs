using UnityEngine;
using UnityEngine.EventSystems;

namespace ArcanesGroupFrames
{
    internal sealed class UnitFrameInteraction :
        MonoBehaviour,
        IPointerClickHandler,
        IPointerEnterHandler,
        IPointerExitHandler
    {
        // ============================================================
        // BINDING
        // ============================================================

        private bool _isPlayer;

        private RaidMemberSlot _raidSlot;

        private SimPlayerTracking _groupMember;

        private bool _partyContext;


        // ============================================================
        // INITIALIZE - PLAYER
        // ============================================================

        internal void InitializeForPlayer(
            bool partyContext = false)
        {
            _isPlayer =
                true;

            _raidSlot =
                null;

            _groupMember =
                null;

            _partyContext =
                partyContext;
        }


        // ============================================================
        // INITIALIZE - RAID MEMBER
        // ============================================================

        internal void InitializeForRaidMember(
            RaidMemberSlot slot,
            bool partyContext = false)
        {
            _isPlayer =
                false;

            _raidSlot =
                slot;

            _groupMember =
                null;

            _partyContext =
                partyContext;
        }


        // ============================================================
        // INITIALIZE - NORMAL GROUP MEMBER
        //
        // This is intentionally included now so the same interaction
        // component can be reused when we replace the native one-group
        // party frames later.
        // ============================================================

        internal void InitializeForGroupMember(
            SimPlayerTracking member,
            bool partyContext = true)
        {
            _isPlayer =
                false;

            _raidSlot =
                null;

            _groupMember =
                member;

            _partyContext =
                partyContext;
        }


        // ============================================================
        // CLICK
        // ============================================================

        public void OnPointerClick(
            PointerEventData eventData)
        {
            if (eventData == null)
            {
                return;
            }

            ClickCastBindingSlot slot =
                ClickCastingManager.ResolveSlot(eventData.button);

            string binding =
                ClickCastingManager.GetBinding(
                    slot,
                    _partyContext);

            if (binding == ClickCastingManager.ActionNone)
            {
                return;
            }

            if (binding == ClickCastingManager.ActionTarget)
            {
                TargetBoundUnit();
                return;
            }

            if (binding == ClickCastingManager.ActionInspect)
            {
                InspectBoundSimPlayer();
                return;
            }

            Character target =
                ResolveCharacter();

            ClickCastingManager.TryCastBoundSpell(
                binding,
                target);
        }

        // ============================================================
        // HOVER
        // ============================================================

        public void OnPointerEnter(
            PointerEventData eventData)
        {
            Character character =
                ResolveCharacter();

            SimPlayer simPlayer =
                ResolveSimPlayer();

            SimPlayerTracking tracking =
                ResolveTracking();


            UnitFrameMouseoverState.Set(
                this,
                character,
                simPlayer,
                tracking,
                _raidSlot,
                _isPlayer,
                _partyContext);
        }


        public void OnPointerExit(
            PointerEventData eventData)
        {
            UnitFrameMouseoverState.Clear(
                this);
        }


        private void OnDisable()
        {
            UnitFrameMouseoverState.Clear(
                this);
        }


        private void OnDestroy()
        {
            UnitFrameMouseoverState.Clear(
                this);
        }


        // ============================================================
        // TARGETING
        // ============================================================

        private void TargetBoundUnit()
        {
            Character target =
                ResolveCharacter();


            if (target == null ||
                GameData.PlayerControl == null)
            {
                return;
            }


            Character current =
                GameData.PlayerControl.CurrentTarget;


            if (current != null &&
                current != target)
            {
                current.UntargetMe();
            }


            GameData.PlayerControl.CurrentTarget =
                target;


            target.TargetMe();
        }


        // ============================================================
        // INSPECTION
        // ============================================================

        private void InspectBoundSimPlayer()
        {
            if (_isPlayer)
            {
                return;
            }


            SimPlayer simPlayer =
                ResolveSimPlayer();


            if (simPlayer == null ||
                GameData.InspectSim == null)
            {
                return;
            }


            GameData.InspectSim.InspectSim(
                simPlayer);
        }


        // ============================================================
        // RESOLUTION
        // ============================================================

        private Character ResolveCharacter()
        {
            // --------------------------------------------------------
            // PLAYER
            // --------------------------------------------------------

            if (_isPlayer)
            {
                if (GameData.PlayerControl != null &&
                    GameData.PlayerControl.Myself != null)
                {
                    return GameData.PlayerControl.Myself;
                }


                if (GameData.PlayerStats != null)
                {
                    return GameData.PlayerStats.Myself;
                }


                return null;
            }


            // --------------------------------------------------------
            // RAID MEMBER
            // --------------------------------------------------------

            if (_raidSlot != null)
            {
                if (_raidSlot.AssignedAvatar != null &&
                    _raidSlot.AssignedAvatar.MyStats != null &&
                    _raidSlot.AssignedAvatar.MyStats.Myself != null)
                {
                    return _raidSlot
                        .AssignedAvatar
                        .MyStats
                        .Myself;
                }


                if (_raidSlot.AssignedSimTracking != null &&
                    _raidSlot.AssignedSimTracking.MyStats != null &&
                    _raidSlot.AssignedSimTracking.MyStats.Myself != null)
                {
                    return _raidSlot
                        .AssignedSimTracking
                        .MyStats
                        .Myself;
                }
            }


            // --------------------------------------------------------
            // NORMAL GROUP MEMBER
            // --------------------------------------------------------

            if (_groupMember != null)
            {
                if (_groupMember.MyAvatar != null &&
                    _groupMember.MyAvatar.MyStats != null &&
                    _groupMember.MyAvatar.MyStats.Myself != null)
                {
                    return _groupMember
                        .MyAvatar
                        .MyStats
                        .Myself;
                }


                if (_groupMember.MyStats != null &&
                    _groupMember.MyStats.Myself != null)
                {
                    return _groupMember
                        .MyStats
                        .Myself;
                }
            }


            return null;
        }


        private SimPlayer ResolveSimPlayer()
        {
            if (_isPlayer)
            {
                return null;
            }


            if (_raidSlot != null &&
                _raidSlot.AssignedAvatar != null)
            {
                return _raidSlot.AssignedAvatar;
            }


            if (_groupMember != null &&
                _groupMember.MyAvatar != null)
            {
                return _groupMember.MyAvatar;
            }


            return null;
        }


        private SimPlayerTracking ResolveTracking()
        {
            if (_raidSlot != null)
            {
                return _raidSlot.AssignedSimTracking;
            }


            return _groupMember;
        }
    }
}
