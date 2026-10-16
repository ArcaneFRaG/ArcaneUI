using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace ArcanesGroupFrames
{
    internal enum ClickCastBindingSlot
    {
        LeftClick,
        RightClick,
        MiddleClick,
        ShiftLeft,
        ShiftRight,
        CtrlLeft,
        CtrlRight,
        AltLeft,
        AltRight
    }

    internal static class ClickCastingManager
    {
        internal const string ActionNone = "ACTION:NONE";
        internal const string ActionTarget = "ACTION:TARGET";
        internal const string ActionInspect = "ACTION:INSPECT";
        private const string SpellPrefix = "SPELL:";

        // StartSpell accepts a spell without consulting Hotkeys.Cooldown.
        // Track completion-based cooldowns for click-casts and honour native
        // hotkey timers as well. State is scoped to the current character.
        private static Stats _cooldownOwner;
        private static readonly Dictionary<string, float> CooldownEnds =
            new Dictionary<string, float>(StringComparer.Ordinal);
        private static readonly Dictionary<string, float> PendingCast =
            new Dictionary<string, float>(StringComparer.Ordinal);

        private static void CheckOwner()
        {
            if (object.ReferenceEquals(_cooldownOwner, GameData.PlayerStats)) return;
            _cooldownOwner = GameData.PlayerStats;
            CooldownEnds.Clear();
            PendingCast.Clear();
        }

        private static bool IsCoolingDown(Spell spell)
        {
            CheckOwner();
            if (spell == null || string.IsNullOrEmpty(spell.Id)) return true;
            float end;
            if (CooldownEnds.TryGetValue(spell.Id, out end) && end > Time.time) return true;
            if (PendingCast.TryGetValue(spell.Id, out end) && end > Time.time) return true;
            // Native action bars are authoritative when they carry this spell.
            // Never reset or mutate their cooldowns here.
            foreach (Hotkeys hotkey in UnityEngine.Object.FindObjectsOfType<Hotkeys>())
            {
                if (hotkey == null || hotkey.AssignedSpell == null) continue;
                if (hotkey.AssignedSpell.Id == spell.Id && hotkey.Cooldown > .01f)
                    return true;
            }
            return false;
        }

        // Called from the existing SpellVessel completion observer. Unlike
        // StartSpell, this is the point at which the game's cooldown begins.
        internal static void SpellCompleted(Spell spell, float cooldownSeconds, bool noCooldown)
        {
            CheckOwner();
            if (spell == null || string.IsNullOrEmpty(spell.Id)) return;
            PendingCast.Remove(spell.Id);
            if (noCooldown)
            {
                CooldownEnds.Remove(spell.Id);
                return;
            }
            if (cooldownSeconds > 0f && !float.IsNaN(cooldownSeconds) &&
                !float.IsInfinity(cooldownSeconds))
                CooldownEnds[spell.Id] = Time.time + cooldownSeconds;
        }

        internal static void ResetCooldown(string spellId)
        {
            CheckOwner();
            if (string.IsNullOrEmpty(spellId)) return;
            CooldownEnds.Remove(spellId);
            PendingCast.Remove(spellId);
        }


        internal static string GetBinding(
            ClickCastBindingSlot slot,
            bool partyContext = false)
        {
            if (partyContext)
            {
                switch (slot)
                {
                    case ClickCastBindingSlot.LeftClick: return PartyFramesSettings.ClickLeft;
                    case ClickCastBindingSlot.RightClick: return PartyFramesSettings.ClickRight;
                    case ClickCastBindingSlot.MiddleClick: return PartyFramesSettings.ClickMiddle;
                    case ClickCastBindingSlot.ShiftLeft: return PartyFramesSettings.ClickShiftLeft;
                    case ClickCastBindingSlot.ShiftRight: return PartyFramesSettings.ClickShiftRight;
                    case ClickCastBindingSlot.CtrlLeft: return PartyFramesSettings.ClickCtrlLeft;
                    case ClickCastBindingSlot.CtrlRight: return PartyFramesSettings.ClickCtrlRight;
                    case ClickCastBindingSlot.AltLeft: return PartyFramesSettings.ClickAltLeft;
                    case ClickCastBindingSlot.AltRight: return PartyFramesSettings.ClickAltRight;
                    default: return ActionNone;
                }
            }

            switch (slot)
            {
                case ClickCastBindingSlot.LeftClick: return RaidFramesSettings.ClickLeft;
                case ClickCastBindingSlot.RightClick: return RaidFramesSettings.ClickRight;
                case ClickCastBindingSlot.MiddleClick: return RaidFramesSettings.ClickMiddle;
                case ClickCastBindingSlot.ShiftLeft: return RaidFramesSettings.ClickShiftLeft;
                case ClickCastBindingSlot.ShiftRight: return RaidFramesSettings.ClickShiftRight;
                case ClickCastBindingSlot.CtrlLeft: return RaidFramesSettings.ClickCtrlLeft;
                case ClickCastBindingSlot.CtrlRight: return RaidFramesSettings.ClickCtrlRight;
                case ClickCastBindingSlot.AltLeft: return RaidFramesSettings.ClickAltLeft;
                case ClickCastBindingSlot.AltRight: return RaidFramesSettings.ClickAltRight;
                default: return ActionNone;
            }
        }

        internal static void SetBinding(
            ClickCastBindingSlot slot,
            string value,
            bool partyContext = false)
        {
            if (partyContext)
            {
                PartyFramesSettings.SetClickBinding(slot, value);
                return;
            }

            RaidFramesSettings.SetClickBinding(slot, value);
        }

        internal static string MakeSpellBinding(Spell spell)
        {
            return spell == null
                ? ActionNone
                : SpellPrefix + spell.Id;
        }

        internal static Spell ResolveSpell(string binding)
        {
            if (string.IsNullOrEmpty(binding) ||
                !binding.StartsWith(SpellPrefix, StringComparison.Ordinal))
            {
                return null;
            }

            return HealingSpellCatalog.FindById(
                binding.Substring(SpellPrefix.Length));
        }

        internal static string GetBindingDisplayName(string binding)
        {
            if (string.IsNullOrEmpty(binding) || binding == ActionNone)
            {
                return "None";
            }

            if (binding == ActionTarget)
            {
                return "Target";
            }

            if (binding == ActionInspect)
            {
                return "Inspect";
            }

            Spell spell = ResolveSpell(binding);

            if (spell == null) return "Missing Spell";
            return HealingSpellCatalog.IsPlayerEligibleSpell(spell)
                ? spell.SpellName
                : "Unavailable: " + spell.SpellName;
        }

        internal static ClickCastBindingSlot ResolveSlot(
            PointerEventData.InputButton button)
        {
            bool shift =
                Input.GetKey(KeyCode.LeftShift) ||
                Input.GetKey(KeyCode.RightShift);

            bool ctrl =
                Input.GetKey(KeyCode.LeftControl) ||
                Input.GetKey(KeyCode.RightControl);

            // Alt is reserved from every in-game action, including click casting.
            // Existing Alt click-cast assignments are retained but inert.
            bool alt = false;

            if (shift)
            {
                return button == PointerEventData.InputButton.Right
                    ? ClickCastBindingSlot.ShiftRight
                    : ClickCastBindingSlot.ShiftLeft;
            }

            if (ctrl)
            {
                return button == PointerEventData.InputButton.Right
                    ? ClickCastBindingSlot.CtrlRight
                    : ClickCastBindingSlot.CtrlLeft;
            }

            if (alt)
            {
                return button == PointerEventData.InputButton.Right
                    ? ClickCastBindingSlot.AltRight
                    : ClickCastBindingSlot.AltLeft;
            }

            if (button == PointerEventData.InputButton.Right)
            {
                return ClickCastBindingSlot.RightClick;
            }

            if (button == PointerEventData.InputButton.Middle)
            {
                return ClickCastBindingSlot.MiddleClick;
            }

            return ClickCastBindingSlot.LeftClick;
        }

        internal static bool TryCastBoundSpell(
            string binding,
            Character target)
        {
            Spell spell = ResolveSpell(binding);

            if (spell == null ||
                !HealingSpellCatalog.IsPlayerEligibleSpell(spell) ||
                target == null ||
                target.MyStats == null ||
                GameData.PlayerControl == null)
            {
                return false;
            }

            CastSpell caster =
                GameData.PlayerControl.GetComponent<CastSpell>();

            if (caster == null)
            {
                return false;
            }

            if (IsCoolingDown(spell)) return false;
            bool started = caster.StartSpell(spell, target.MyStats);
            if (started && !string.IsNullOrEmpty(spell.Id))
            {
                // A short pending-cast guard prevents duplicate clicks before
                // a SpellVessel completion event can publish the real timer.
                PendingCast[spell.Id] = Time.time + .35f;
            }
            return started;
        }

        private static string GetCharacterName(Character character)
        {
            if (character == null)
            {
                return "<unknown>";
            }

            if (character.MyStats != null &&
                !string.IsNullOrEmpty(character.MyStats.MyName))
            {
                return character.MyStats.MyName;
            }

            return character.transform != null
                ? character.transform.name
                : "<unknown>";
        }
    }
}
