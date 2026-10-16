using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace ArcanesGroupFrames
{
    // ================================================================
    // MOUSEOVER SPELL TARGETING
    // ================================================================
    //
    // Erenshor has several CastSpell.StartSpell overloads:
    //
    //     StartSpell(Spell, Stats)
    //     StartSpell(Spell, Stats, float)
    //     StartSpell(Spell, Stats, float, bool)
    //     StartSpell(Spell, Stats, float, bool, float)
    //
    // The previous implementation patched only the two-argument overload.
    // Hotbar casting can enter through one of the longer overloads instead,
    // which means the target override never runs.
    //
    // This patch targets every StartSpell overload whose first two arguments
    // are Spell and Stats, then replaces only the Stats argument.
    // ================================================================

    [HarmonyPatch]
    internal static class UnitFrameMouseoverCastingPatch
    {
        // ============================================================
        // TARGET METHODS
        // ============================================================

        private static IEnumerable<MethodBase> TargetMethods()
        {
            MethodInfo[] methods =
                typeof(CastSpell)
                    .GetMethods(
                        BindingFlags.Instance |
                        BindingFlags.Public |
                        BindingFlags.NonPublic);

            for (int i = 0;
                 i < methods.Length;
                 i++)
            {
                MethodInfo method =
                    methods[i];

                if (method == null ||
                    method.Name != "StartSpell")
                {
                    continue;
                }

                ParameterInfo[] parameters =
                    method.GetParameters();

                if (parameters == null ||
                    parameters.Length < 2)
                {
                    continue;
                }

                if (parameters[0].ParameterType != typeof(Spell) ||
                    parameters[1].ParameterType != typeof(Stats))
                {
                    continue;
                }

                yield return method;
            }
        }


        // ============================================================
        // PREFIX
        // ============================================================

        private static void Prefix(
            CastSpell __instance,
            Spell _spell,
            ref Stats _target)
        {
            bool mouseoverEnabled =
                UnitFrameMouseoverState.IsPartyFrame
                    ? PartyFramesSettings.EnableMouseoverCasting
                    : RaidFramesSettings.EnableMouseoverCasting;

            if (!mouseoverEnabled)
            {
                return;
            }

            if (__instance == null ||
                _spell == null)
            {
                return;
            }

            // --------------------------------------------------------
            // PLAYER CASTS ONLY
            //
            // Do not rely on Stats.MySpells here. In Erenshor that field
            // is private in the native Stats class. The player's CastSpell
            // component lives on the same GameObject as PlayerControl, so
            // this is a reliable native distinction between the player and
            // SimPlayer/NPC casters.
            // --------------------------------------------------------

            PlayerControl playerControl =
                __instance.GetComponent<PlayerControl>();

            if (playerControl == null)
            {
                return;
            }

            if (GameData.PlayerControl != null &&
                playerControl != GameData.PlayerControl)
            {
                return;
            }

            // --------------------------------------------------------
            // FRIENDLY SPELL TYPES ONLY
            // --------------------------------------------------------

            if (_spell.Type != Spell.SpellType.Heal &&
                _spell.Type != Spell.SpellType.Beneficial)
            {
                return;
            }

            Character hovered =
                UnitFrameMouseoverState.Character;

            if (hovered == null ||
                hovered.MyStats == null)
            {
                return;
            }

            // --------------------------------------------------------
            // OVERRIDE ONLY THE CAST TARGET
            //
            // This intentionally does not change CurrentTarget and does
            // not call TargetMe(). The selected combat target therefore
            // remains untouched.
            // --------------------------------------------------------

            _target =
                hovered.MyStats;

}


        // ============================================================
        // HELPERS
        // ============================================================

        private static string GetTargetName(
            Character target)
        {
            if (target == null)
            {
                return "<null>";
            }

            if (target.MyStats != null &&
                !string.IsNullOrWhiteSpace(
                    target.MyStats.MyName))
            {
                return target.MyStats.MyName;
            }

            if (target.MyNPC != null &&
                !string.IsNullOrWhiteSpace(
                    target.MyNPC.NPCName))
            {
                return target.MyNPC.NPCName;
            }

            return target.name;
        }
    }
}
