using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using UnityEngine;

namespace ArcanesGroupFrames.Modules.UnitFrames.Target
{
    /// <summary>
    /// Records the identity of a spell when Erenshor starts casting it.
    /// This does not alter spell parameters or game behaviour. Live cast state
    /// still comes from CastSpell.isCasting(), not from a timer we invented.
    /// </summary>
    [HarmonyPatch]
    internal static class TargetCastStartObserver
    {
        private sealed class ObservedCast
        {
            internal string SpellName;
            internal Spell Spell;
            internal float StartedAt;
            internal float StartedAtUnscaled;
            internal int Sequence;
        }

        private static readonly ConditionalWeakTable<CastSpell, ObservedCast> Observations =
            new ConditionalWeakTable<CastSpell, ObservedCast>();

        private static IEnumerable<MethodBase> TargetMethods()
        {
            foreach (MethodInfo method in typeof(CastSpell).GetMethods(
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                if (method.Name != "StartSpell") continue;
                ParameterInfo[] parameters = method.GetParameters();
                if (parameters.Length < 2 ||
                    parameters[0].ParameterType != typeof(Spell) ||
                    parameters[1].ParameterType != typeof(Stats)) continue;
                yield return method;
            }
        }

        // A postfix means the native implementation has had the opportunity to
        // accept/reject the cast. The live isCasting() check remains authoritative.
        private static void Postfix(CastSpell __instance, Spell _spell)
        {
            if (__instance == null || _spell == null ||
                string.IsNullOrEmpty(_spell.SpellName)) return;

            ObservedCast state = Observations.GetValue(__instance, _ => new ObservedCast());
            float now = Time.unscaledTime;
            // One native spell attempt may travel through several StartSpell
            // overloads. Their Harmony postfixes are not separate casts.
            if (ReferenceEquals(state.Spell, _spell) && state.Sequence > 0 &&
                now - state.StartedAtUnscaled >= 0f &&
                now - state.StartedAtUnscaled < .10f)
                return;
            state.SpellName = _spell.SpellName;
            state.Spell = _spell;
            state.StartedAt = Time.time;
            state.StartedAtUnscaled = now;
            state.Sequence++;
        }

        // Preserve the native Spell reference as well as the name. If
        // CurrentlyCasting is momentarily null, the observer can still supply
        // the spell's authored cast duration for the same active caster.
        internal static int GetSequence(CastSpell caster)
        {
            if (caster == null) return 0;
            ObservedCast cast;
            return Observations.TryGetValue(caster, out cast) && cast != null ? cast.Sequence : 0;
        }

        // Wall-clock timestamp recorded by the native StartSpell Harmony
        // observer. Unlike CastSpell.castTime, it changes predictably as
        // Time.unscaledTime advances. Valid only for this exact caster instance.
        internal static bool TryGetStart(CastSpell caster, out float startedAt,
            out int sequence, out Spell spell)
        {
            startedAt = -1f;
            sequence = 0;
            spell = null;
            ObservedCast observed;
            if (caster == null || !Observations.TryGetValue(caster, out observed) ||
                observed == null || observed.Sequence <= 0) return false;
            startedAt = observed.StartedAtUnscaled;
            sequence = observed.Sequence;
            spell = observed.Spell;
            return true;
        }

        internal static Spell GetRecentSpell(CastSpell caster)
        {
            if (caster == null) return null;
            ObservedCast observed;
            if (!Observations.TryGetValue(caster, out observed) || observed == null)
                return null;
            float age = Time.time - observed.StartedAt;
            return age >= 0f && age < 120f ? observed.Spell : null;
        }

        internal static bool TryGetRecentName(CastSpell caster, out string spellName)
        {
            spellName = null;
            if (caster == null) return false;
            ObservedCast cast;
            if (!Observations.TryGetValue(caster, out cast) || cast == null ||
                string.IsNullOrEmpty(cast.SpellName)) return false;
            float age = Time.time - cast.StartedAt;
            if (age < 0f || age > 120f) return false;
            spellName = cast.SpellName;
            return true;
        }
    }
}
