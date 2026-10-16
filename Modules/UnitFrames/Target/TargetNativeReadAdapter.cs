using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace ArcanesGroupFrames.Modules.UnitFrames.Target
{
    /// <summary>
    /// Read-only fallback for version-dependent Erenshor target data. We NEVER
    /// guess cast duration from incomplete native values. Missing native
    /// members simply leave the corresponding custom display hidden.
    /// </summary>
    internal static class TargetNativeReadAdapter
    {
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private static object Member(object owner, params string[] names)
        {
            if (owner == null) return null;
            Type type = owner.GetType();
            foreach (string name in names)
            {
                try
                {
                    var f = type.GetField(name, Flags);
                    if (f != null) return f.GetValue(owner);
                    var prop = type.GetProperty(name, Flags);
                    if (prop != null && prop.GetIndexParameters().Length == 0 && prop.CanRead)
                        return prop.GetValue(owner, null);
                }
                catch (Exception) { }
            }
            return null;
        }
        private static bool Number(object obj, out float n)
        {
            n = 0f;
            if (obj == null) return false;
            try { n = Convert.ToSingle(obj); return !float.IsNaN(n) && !float.IsInfinity(n); }
            catch (Exception) { return false; }
        }
        internal static void LogCandidateMembers(Character target)
        {
            if (target == null) { Plugin.LogDiagnosticWarning("ArcaneUI cast diagnostics: select a target first."); return; }
            object[] sources = { target, target.MyNPC, target.MyStats };
            foreach (object source in sources)
            {
                if (source == null) continue;
                var type = source.GetType();
                foreach (var field in type.GetFields(Flags))
                {
                    string n = field.Name.ToLowerInvariant();
                    if (n.Contains("cast") || n.Contains("spell") || n.Contains("target"))
                        Plugin.LogDiagnosticWarning("ArcaneUI target source " + type.Name + "." + field.Name +
                            " : " + field.FieldType.Name);
                }
                foreach (var prop in type.GetProperties(Flags))
                {
                    string n = prop.Name.ToLowerInvariant();
                    if (prop.GetIndexParameters().Length == 0 &&
                        (n.Contains("cast") || n.Contains("spell") || n.Contains("target")))
                        Plugin.LogDiagnosticWarning("ArcaneUI target source " + type.Name + "." + prop.Name +
                            " : " + prop.PropertyType.Name);
                }
            }
            // The character and stats expose MySpells : CastSpell. The first
            // diagnostic only printed the type of that field, never the fields
            // INSIDE CastSpell that could supply spell identity and cast timing.
            // This is a read-only, one-shot inspection driven by the existing
            // Cast Diagnostics button; it does not poll or modify the game.
            object characterCaster = Member(target, "MySpells");
            object statsCaster = Member(target.MyStats, "MySpells");
            object npcCaster = Member(target.MyNPC, "MySpells");
            LogCaster("Character.MySpells", characterCaster);
            if (!ReferenceEquals(statsCaster, characterCaster))
                LogCaster("Stats.MySpells", statsCaster);
            else
                Plugin.LogDiagnosticWarning("ArcaneUI cast diagnostics: Stats.MySpells shares Character.MySpells.");
            if (npcCaster != null && !ReferenceEquals(npcCaster, characterCaster) &&
                !ReferenceEquals(npcCaster, statsCaster))
                LogCaster("NPC.MySpells", npcCaster);
            if (characterCaster == null && statsCaster == null && npcCaster == null)
                Plugin.LogDiagnosticWarning("ArcaneUI cast diagnostics: no MySpells component was available for this target.");
            ArmCastTimingProbe(target);
        }

        private static bool IsCastRelated(string name)
        {
            string n = name.ToLowerInvariant();
            return n.Contains("cast") || n.Contains("spell") || n.Contains("time") ||
                n.Contains("timer") || n.Contains("progress") || n.Contains("interrupt") ||
                n.Contains("channel") || n.Contains("charge") || n.Contains("duration") ||
                n.Contains("current") || n.Contains("active") || n.Contains("target") ||
                n.Contains("ability") || n.Contains("skill") || n.Contains("delay") ||
                n.Contains("start");
        }

        private static string DiagnosticValue(object value)
        {
            if (value == null) return "null";
            if (value is Spell)
            {
                Spell spell = (Spell)value;
                return "Spell: " + (string.IsNullOrEmpty(spell.SpellName) ? "<unnamed>" : spell.SpellName);
            }
            Type valueType = value.GetType();
            if (valueType.IsEnum || valueType.IsPrimitive || value is decimal || value is string)
            {
                string result = Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture);
                return result == null ? "null" : (result.Length > 80 ? result.Substring(0, 80) + "..." : result);
            }
            // Avoid invoking game logic, enumerating collections or dumping
            // Unity objects: only the member type is needed for those.
            return "<" + valueType.Name + ">";
        }

        private static void LogCaster(string sourceName, object caster)
        {
            if (caster == null)
            {
                Plugin.LogDiagnosticWarning("ArcaneUI cast diagnostics: " + sourceName + " = null");
                return;
            }
            Type casterType = caster.GetType();
            Plugin.LogDiagnosticWarning("ArcaneUI cast diagnostics: " + sourceName + " -> " + casterType.FullName);
            CastSpell nativeCaster = caster as CastSpell;
            if (nativeCaster != null)
            {
                bool nativeActive;
                if (TryGetNativeCasting(nativeCaster, out nativeActive))
                    Plugin.LogDiagnosticWarning("ArcaneUI CastSpell NATIVE " + sourceName +
                        ".isCasting() = " + nativeActive);
                string observed;
                if (TargetCastStartObserver.TryGetRecentName(nativeCaster, out observed))
                    Plugin.LogDiagnosticWarning("ArcaneUI CastSpell OBSERVED " + sourceName +
                        ".StartSpell = " + observed);
                float progress, remaining;
                Spell spell = ReadActiveSpell(nativeCaster) ??
                    TargetCastStartObserver.GetRecentSpell(nativeCaster);
                object spellChargeTime = Member(spell, "SpellChargeTime");
                object legacyCastTime = Member(spell, "CastTime", "CastingTime", "castTime",
                    "CastDuration", "BaseCastTime", "TimeToCast", "CastingDelay");
                Plugin.LogDiagnosticWarning("ArcaneUI CastSpell DURATION " + sourceName +
                    " Spell.SpellChargeTime(raw)=" + DiagnosticValue(spellChargeTime) +
                    " Spell.CastTime(legacy)=" + DiagnosticValue(legacyCastTime) +
                    " CastSpell.castTime(raw)=" + DiagnosticValue(Member(nativeCaster, "castTime")) +
                    " (native charge units are NOT assumed to be seconds)");
                if (spell != null)
                {
                    foreach (FieldInfo field in spell.GetType().GetFields(Flags))
                    {
                        if (!IsCastRelated(field.Name)) continue;
                        object value;
                        try { value = field.GetValue(spell); }
                        catch (Exception) { continue; }
                        if (value is float || value is int || value is double)
                            Plugin.LogDiagnosticWarning("ArcaneUI Spell TIMING FIELD " + field.Name + "=" + DiagnosticValue(value));
                    }
                }
                string displaySource;
                float displayProgress, displayRemaining;
                if (nativeActive && TryGetCastTimeline(nativeCaster, spell,
                    out displayProgress, out displayRemaining, out displaySource))
                    Plugin.LogDiagnosticWarning("ArcaneUI CastSpell DISPLAY " + sourceName +
                        " source=" + displaySource +
                        " progress=" + displayProgress.ToString("0.000") +
                        " remaining=" + displayRemaining.ToString("0.000"));
                string timingSource;
                if (nativeActive && TryGetNativeCastTiming(nativeCaster, spell, out progress,
                    out remaining, out timingSource))
                    Plugin.LogDiagnosticWarning("ArcaneUI CastSpell TIMING " + sourceName +
                        " source=" + timingSource +
                        " progress=" + progress.ToString("0.000") +
                        " remaining=" + remaining.ToString("0.000"));
                else
                    Plugin.LogDiagnosticWarning("ArcaneUI CastSpell TIMING " + sourceName +
                        (nativeActive
                            ? " no matching valid cast duration/counter; activity fallback."
                            : " inactive (cast bar hidden)."));
            }
            int found = 0;
            foreach (FieldInfo field in casterType.GetFields(Flags))
            {
                if (!IsCastRelated(field.Name)) continue;
                string value;
                try { value = DiagnosticValue(field.GetValue(caster)); }
                catch (Exception) { value = "<unavailable>"; }
                Plugin.LogDiagnosticWarning("ArcaneUI CastSpell FIELD " + casterType.Name + "." +
                    field.Name + " : " + field.FieldType.Name + " = " + value);
                if (++found >= 90) break;
            }
            // Properties are listed by name/type only: getters can have side
            // effects, and inspecting their values is unnecessary at this stage.
            if (found < 90)
                foreach (PropertyInfo prop in casterType.GetProperties(Flags))
                {
                    if (!IsCastRelated(prop.Name) || prop.GetIndexParameters().Length != 0)
                        continue;
                    Plugin.LogDiagnosticWarning("ArcaneUI CastSpell PROPERTY " + casterType.Name + "." +
                        prop.Name + " : " + prop.PropertyType.Name);
                    if (++found >= 90) break;
                }
            Plugin.LogDiagnosticWarning("ArcaneUI cast diagnostics: " + sourceName + " inspected " +
                found + " matching fields/properties.");
        }
        // Confirmed Erenshor native API: CastSpell.isCasting(). We look it
        // up once rather than depending on the access modifier/version of the
        // method in the player's Assembly-CSharp.dll.
        private static readonly MethodInfo NativeIsCastingMethod =
            typeof(CastSpell).GetMethod("isCasting", Flags, null, Type.EmptyTypes, null) ??
            typeof(CastSpell).GetMethod("IsCasting", Flags, null, Type.EmptyTypes, null);
        private static bool _warnedMissingNativeState;
        private static bool _warnedNativeStateException;

        private static bool TryGetNativeCasting(CastSpell caster, out bool isCasting)
        {
            isCasting = false;
            if (caster == null) return false;
            if (NativeIsCastingMethod == null)
            {
                if (!_warnedMissingNativeState)
                {
                    _warnedMissingNativeState = true;
                    Plugin.LogWarning("ArcaneUI: CastSpell.isCasting() is unavailable on this Erenshor build; cast overlay disabled until native state can be verified.");
                }
                return false;
            }
            try
            {
                object result = NativeIsCastingMethod.Invoke(caster, null);
                if (!(result is bool)) return false;
                isCasting = (bool)result;
                return true;
            }
            catch (Exception ex)
            {
                if (!_warnedNativeStateException)
                {
                    _warnedNativeStateException = true;
                    Plugin.LogWarning("ArcaneUI: Unable to read CastSpell.isCasting(): " + ex.GetType().Name);
                }
                return false;
            }
        }

        private static CastSpell GetCaster(Character character)
        {
            if (character == null) return null;
            return Member(character, "MySpells") as CastSpell ??
                Member(character.MyStats, "MySpells") as CastSpell ??
                Member(character.MyNPC, "MySpells") as CastSpell;
        }

        private static Spell ReadActiveSpell(CastSpell caster)
        {
            // Confirmed member from in-game Cast Diagnostics. This is null
            // outside a cast; the StartSpell observer is only a fallback.
            return Member(caster, "CurrentlyCasting", "CastingSpell", "CurrentCastingSpell",
                "SpellBeingCast", "CurrentCastSpell", "CurrentSpell", "ActiveSpell",
                "SpellToCast", "castingSpell", "currentSpell", "spellBeingCast") as Spell;
        }

        // Native diagnostics confirm Spell.SpellChargeTime is the authored cast
        // charge, whereas Spell.CastTime is not present in this Erenshor build.
        // CastSpell.castTime remains set even while idle (e.g. 60 for Flicker
        // Ward). Therefore a *single* counter sample tells us neither cast
        // progress nor seconds. We require consecutive samples while native
        // isCasting() is true and never treat charge units as seconds.
        private sealed class CastCounterState
        {
            internal Spell Spell;
            internal int StartSequence;
            internal bool Active;
            internal float FirstCounter;
            internal float MaxCounter;
            internal float LastCounter;
            internal float LastAt;
            internal float UnitsPerSecond;
            internal bool RateMeasured;
            internal int Direction; // 1 = up, -1 = down, 0 = unknown
            internal int Samples;
        }

        private static readonly ConditionalWeakTable<CastSpell, CastCounterState> CastCounters =
            new ConditionalWeakTable<CastSpell, CastCounterState>();

        private static CastCounterState ObserveCounter(CastSpell caster, Spell spell, float counter)
        {
            CastCounterState state = CastCounters.GetValue(caster, _ => new CastCounterState());
            int sequence = TargetCastStartObserver.GetSequence(caster);
            // The on-screen countdown is wall-clock seconds, so rate samples
            // must use the same unscaled clock even under Time.timeScale mods.
            float now = Time.unscaledTime;
            bool restarted = !state.Active || (sequence > 0 && sequence != state.StartSequence) ||
                (spell != null && state.Spell != null && spell != state.Spell);
            if (restarted)
            {
                state.Active = true;
                state.Spell = spell;
                state.StartSequence = sequence;
                state.FirstCounter = counter;
                state.MaxCounter = counter;
                state.LastCounter = counter;
                state.LastAt = now;
                state.UnitsPerSecond = 0f;
                state.RateMeasured = false;
                state.Direction = 0;
                state.Samples = 1;
                return state;
            }

            float deltaTime = now - state.LastAt;
            float deltaCounter = counter - state.LastCounter;
            if (deltaTime >= .02f && deltaTime <= .6f && Mathf.Abs(deltaCounter) > .0001f)
            {
                float rate = Mathf.Abs(deltaCounter / deltaTime);
                // Reject stalls and giant changes consistent with a reset.
                // We also reject any change that flips the observed direction;
                // those must not create a false timer for a new cast.
                if (rate >= .1f && rate <= 4000f)
                {
                    int direction = deltaCounter > 0f ? 1 : -1;
                    if (state.Direction != 0 && direction != state.Direction)
                    {
                        state.RateMeasured = false;
                        state.UnitsPerSecond = 0f;
                        state.Direction = 0;
                        state.Samples = 1;
                    }
                    else
                    {
                        state.UnitsPerSecond = state.Direction == 0
                            ? rate : Mathf.Lerp(state.UnitsPerSecond, rate, .25f);
                        state.Direction = direction;
                        state.Samples++;
                        state.RateMeasured = state.Samples >= 3;
                    }
                }
            }
            state.MaxCounter = Mathf.Max(state.MaxCounter, counter);
            state.LastCounter = counter;
            state.LastAt = now;
            if (state.Spell == null) state.Spell = spell;
            return state;
        }

        private static void ResetCounter(CastSpell caster)
        {
            if (caster == null) return;
            CastCounterState state;
            if (CastCounters.TryGetValue(caster, out state)) state.Active = false;
        }

        private static bool TryGetNativeCastTiming(CastSpell caster, Spell spell,
            out float progress, out float remaining, out string source)
        {
            progress = -1f;
            remaining = -1f;
            source = "unresolved";
            if (caster == null) return false;

            // An explicit native effective duration + elapsed/remaining pair
            // is authoritative when a game version exposes one. These values
            // are expected in seconds; the fallbacks below are *charge units*.
            float total, elapsed, timeLeft;
            bool haveTotal = Number(Member(caster, "modifiedCastTime", "ModifiedCastTime",
                "TotalCastTime", "CastDuration", "CastingDuration", "totalCastTime"), out total);
            bool haveElapsed = Number(Member(caster, "CastElapsed", "CurrentCastTime",
                "CastProgressSeconds", "elapsedCastTime", "castElapsed"), out elapsed);
            bool haveRemaining = Number(Member(caster, "CastTimeRemaining", "RemainingCastTime",
                "castTimeRemaining", "remainingCastTime"), out timeLeft);
            if (haveTotal && total > .05f && total <= 120f &&
                (haveElapsed || haveRemaining))
            {
                float left = haveRemaining ? timeLeft : total - elapsed;
                if (left >= -.1f && left <= total + .1f)
                {
                    remaining = Mathf.Clamp(left, 0f, total);
                    progress = Mathf.Clamp01(1f - remaining / total);
                    source = "native effective duration + elapsed/remaining seconds";
                    return true;
                }
            }

            float counter;
            if (!Number(Member(caster, "castTime"), out counter) || counter < 0f)
                return false;
            CastCounterState state = ObserveCounter(caster, spell, counter);
            if (!state.RateMeasured || state.Direction == 0 || state.UnitsPerSecond <= 0f)
            {
                source = "sampling live native castTime direction/rate";
                return false;
            }

            // The spell data observed in-game has SpellChargeTime=60 while
            // Spell.CastTime is null. This is a native CHARGE UNIT amount, not
            // proof of 60 seconds. Compare units directly for the bar's ratio.
            float chargeUnits;
            bool haveCharge = Number(Member(spell, "SpellChargeTime"), out chargeUnits) &&
                chargeUnits > .01f && chargeUnits <= 72000f;
            if (!haveCharge)
                haveCharge = Number(Member(spell, "ChargeTime", "ChargeDuration"), out chargeUnits) &&
                    chargeUnits > .01f && chargeUnits <= 72000f;
            if (!haveCharge && haveTotal && total > .05f && total <= 120f)
            {
                chargeUnits = total * state.UnitsPerSecond;
                haveCharge = true;
            }

            if (state.Direction < 0)
            {
                // Observed decreasing native counter: its remaining real
                // seconds follow directly from measured units per second.
                remaining = counter / state.UnitsPerSecond;
                if (remaining < 0f || remaining > 120f)
                {
                    remaining = -1f;
                    return false;
                }
                source = "native decreasing castTime / measured units/sec";
                if (haveCharge)
                {
                    // For slower casts the native starting counter may exceed
                    // the spell's baseline charge. Never draw negative fill.
                    float actualTotal = Mathf.Max(chargeUnits, state.MaxCounter);
                    if (actualTotal > 0f && actualTotal <= 72000f &&
                        counter <= actualTotal * 1.05f)
                    {
                        progress = Mathf.Clamp01(1f - counter / actualTotal);
                        source += " + SpellChargeTime native-unit ratio";
                    }
                }
                return true;
            }

            // Other Erenshor versions/mods might count upwards. Progress is
            // only meaningful with a matching native charge-unit total.
            if (!haveCharge || counter > chargeUnits * 1.1f) return false;
            progress = Mathf.Clamp01(counter / chargeUnits);
            remaining = (chargeUnits - counter) / state.UnitsPerSecond;
            if (remaining < 0f || remaining > 120f)
            {
                progress = -1f;
                remaining = -1f;
                return false;
            }
            source = "native increasing castTime + SpellChargeTime / measured units/sec";
            return true;
        }

        // Native Erenshor charge values use 60 charge units per nominal
        // second. These are the units of Spell.SpellChargeTime, not the native
        // caster's castTime field (which may be an accumulator or a cached
        // duration, and may remain constant while casting).
        //
        // For each cast, a native StartSpell observer supplies the start
        // timestamp; subsequent UI updates use elapsed real time. This is a
        // deterministic display fallback even when the game exposes no usable
        // per-frame cast counter. Native isCasting() still controls visibility.
        // Modified cast speed will need further native-data validation.
        private const float NominalChargeUnitsPerSecond = 60f;
        private sealed class CastTimelineState
        {
            internal bool Active;
            internal int Sequence;
            internal Spell Spell;
            internal float StartedAt;
            internal float DurationSeconds;
            internal string Source;
            internal bool Announced;
        }

        private static readonly ConditionalWeakTable<CastSpell, CastTimelineState> CastTimelines =
            new ConditionalWeakTable<CastSpell, CastTimelineState>();

        private static void ResetTimeline(CastSpell caster)
        {
            if (caster == null) return;
            CastTimelineState state;
            if (CastTimelines.TryGetValue(caster, out state))
                state.Active = false;
        }

        private static bool TryGetCastTimeline(CastSpell caster, Spell currentSpell,
            out float progress, out float remaining, out string source)
        {
            progress = -1f;
            remaining = -1f;
            source = "native cast timeline unavailable";
            if (caster == null) return false;

            float observedAt;
            int observedSequence;
            Spell observedSpell;
            bool haveObservedStart = TargetCastStartObserver.TryGetStart(caster,
                out observedAt, out observedSequence, out observedSpell);
            Spell spell = currentSpell ?? observedSpell;
            if (spell == null) return false;

            CastTimelineState state = CastTimelines.GetValue(caster,
                _ => new CastTimelineState());
            float now = Time.unscaledTime;
            bool newCast = !state.Active ||
                (haveObservedStart && observedSequence != state.Sequence) ||
                (currentSpell != null && state.Spell != null &&
                 !ReferenceEquals(currentSpell, state.Spell));

            if (newCast)
            {
                state.Active = true;
                state.Sequence = haveObservedStart ? observedSequence : 0;
                state.Spell = spell;
                // On a normal cast, StartSpell has already recorded the
                // beginning. For a mod-loaded/missed event, first live sighting
                // is safer than a stale timestamp from a previous cast.
                float age = haveObservedStart ? now - observedAt : -1f;
                state.StartedAt = age >= 0f && age < 15f ? observedAt : now;
                state.Source = age >= 0f && age < 15f
                    ? "native StartSpell timestamp" : "first observed active cast";
                state.Announced = false;
                state.DurationSeconds = -1f;
                float charge;
                if (Number(Member(spell, "SpellChargeTime"), out charge) &&
                    charge > .01f && charge <= 7200f)
                {
                    state.DurationSeconds = charge / NominalChargeUnitsPerSecond;
                }
            }
            if (state.DurationSeconds <= 0f || state.DurationSeconds > 120f)
                return false;

            float elapsed = Mathf.Max(0f, now - state.StartedAt);
            float duration = state.DurationSeconds;
            progress = Mathf.Clamp01(elapsed / duration);
            remaining = Mathf.Max(0f, duration - elapsed);
            source = state.Source + " + SpellChargeTime / 60 (nominal; haste unverified)";
            if (!state.Announced)
            {
                state.Announced = true;
                Plugin.LogDiagnosticWarning("ArcaneUI CastTiming LIVE " +
                    (spell.SpellName ?? "<unnamed>") +
                    " charge=" + DiagnosticValue(Member(spell, "SpellChargeTime")) +
                    " nominalSeconds=" + duration.ToString("0.00",
                        System.Globalization.CultureInfo.InvariantCulture) +
                    " source=" + state.Source);
            }
            return true;
        }

        // Both native Player Stats and a Character target own a CastSpell
        // component. The reader is shared so the two cast bars never infer
        // different timelines or manufacture elapsed seconds.
        private static CastSpell GetPlayerCaster(Stats stats)
        {
            // Player builds can keep the caster on Stats or on the local
            // control component. Only inspect the local player here; never
            // accidentally show the selected NPC's casting data.
            CastSpell caster = Member(stats, "MySpells") as CastSpell;
            if (caster != null) return caster;
            object control = GameData.PlayerControl;
            caster = Member(control, "MySpells") as CastSpell;
            if (caster != null) return caster;
            Component localControl = control as Component;
            if (localControl == null) return null;
            try { return localControl.GetComponent<CastSpell>(); }
            catch (Exception) { return null; }
        }

        internal static bool TryReadCast(Stats stats, out string spellName,
            out float normalizedProgress, out float remaining)
        {
            return TryReadCaster(GetPlayerCaster(stats),
                out spellName, out normalizedProgress, out remaining);
        }

        internal static bool TryReadCast(Character character, out string spellName,
            out float normalizedProgress, out float remaining)
        {
            return TryReadCaster(GetCaster(character),
                out spellName, out normalizedProgress, out remaining);
        }

        private static bool TryReadCaster(CastSpell caster, out string spellName,
            out float normalizedProgress, out float remaining)
        {
            spellName = null;
            normalizedProgress = -1f;
            remaining = -1f;
            bool nativeIsCasting;
            if (!TryGetNativeCasting(caster, out nativeIsCasting) || !nativeIsCasting)
            {
                ResetCounter(caster);
                ResetTimeline(caster);
                return false;
            }

            // This is now a confirmed active native cast. Find its name on the
            // caster itself first; use the read-only StartSpell observer as a
            // fallback. Unknown name is explicit, never the previous target.
            Spell spell = ReadActiveSpell(caster);
            if (spell != null && !string.IsNullOrEmpty(spell.SpellName))
                spellName = spell.SpellName;
            if (string.IsNullOrEmpty(spellName))
                TargetCastStartObserver.TryGetRecentName(caster, out spellName);
            if (string.IsNullOrEmpty(spellName)) spellName = "Casting...";

            string timingSource;
            Spell timingSpell = spell ?? TargetCastStartObserver.GetRecentSpell(caster);
            // The previous implementation required three changing castTime
            // samples before rendering any progress; for many native casts the
            // counter does not change until completion. Use the observed
            // StartSpell clock and the real SpellChargeTime field first.
            // The explicitly measured native counter path remains available
            // for diagnostic comparison, not as a gate for the visible timer.
            TryGetCastTimeline(caster, timingSpell,
                out normalizedProgress, out remaining, out timingSource);
            return true;
        }

        internal static void LogPlayerCastMembers(Stats player)
        {
            if (player == null)
            {
                Plugin.LogDiagnosticWarning("ArcaneUI player casting diagnostics: no PlayerStats available.");
                return;
            }
            // The same CastSpell fields are available on the player's Stats.
            // Snapshot only: never alter casting or invoke spell commands.
            LogCaster("PlayerStats.MySpells", Member(player, "MySpells"));
            object controlCaster = Member(GameData.PlayerControl, "MySpells");
            if (controlCaster != null && !ReferenceEquals(controlCaster, Member(player, "MySpells")))
                LogCaster("PlayerControl.MySpells", controlCaster);
            if (GetPlayerCaster(player) == null)
                Plugin.LogDiagnosticWarning("ArcaneUI player casting diagnostics: no local CastSpell component resolved.");
            Plugin.LogDiagnosticWarning("ArcaneUI player cast progress: the display uses StartSpell start time " +
                "and SpellChargeTime/60 for its nominal countdown; haste-modified effective " +
                "duration is not yet confirmed. CastSpell.castTime is logged separately for diagnosis.");
        }

        // Opt-in diagnostic sampler. A single idle snapshot cannot tell us
        // what CastSpell.castTime represents. Sampling multiple points DURING
        // a native-confirmed cast distinguishes a timer from a duration.
        private static Character _probeTarget;
        private static bool _probeArmed;
        private static bool _probeWasCasting;
        private static float _probeExpires;
        private static float _probeNextSample;
        private static int _probeSamples;

        private static void ArmCastTimingProbe(Character character)
        {
            if (character == null || GetCaster(character) == null) return;
            _probeTarget = character;
            _probeArmed = true;
            _probeWasCasting = false;
            _probeSamples = 0;
            _probeNextSample = 0f;
            _probeExpires = Time.unscaledTime + 45f;
            Plugin.LogDiagnosticWarning("ArcaneUI CastProbe ARMED: keep this target selected and let it cast within 45 seconds. " +
                "The probe will log raw native cast values and stop after one cast.");
        }

        internal static void TickCastTimingProbe(Character target)
        {
            if (!_probeArmed) return;
            if (Time.unscaledTime >= _probeExpires)
            {
                _probeArmed = false;
                Plugin.LogDiagnosticWarning("ArcaneUI CastProbe TIMEOUT: no complete native cast captured.");
                return;
            }
            if (target != _probeTarget) return; // Never report a different unit.
            CastSpell caster = GetCaster(target);
            bool active;
            if (!TryGetNativeCasting(caster, out active))
            {
                _probeArmed = false;
                Plugin.LogDiagnosticWarning("ArcaneUI CastProbe STOP: native isCasting() unavailable.");
                return;
            }
            if (active && (!_probeWasCasting ||
                           (Time.unscaledTime >= _probeNextSample && _probeSamples < 16)))
            {
                if (!_probeWasCasting)
                    Plugin.LogDiagnosticWarning("ArcaneUI CastProbe START: native isCasting() became true.");
                LogCastProbeSample(caster, _probeSamples);
                _probeSamples++;
                _probeNextSample = Time.unscaledTime + 0.25f;
            }
            if (_probeWasCasting && !active)
            {
                LogCastProbeSample(caster, _probeSamples);
                Plugin.LogDiagnosticWarning("ArcaneUI CastProbe END: native isCasting() became false.");
                _probeArmed = false;
            }
            _probeWasCasting = active;
        }

        private static void LogCastProbeSample(CastSpell caster, int sample)
        {
            object rawTimer = Member(caster, "castTime");
            object rawSpell = Member(caster, "CurrentlyCasting");
            object vessel = Member(caster, "CurrentVessel");
            float progress, remaining;
            string timingSource;
            bool timed = TryGetNativeCastTiming(caster,
                ReadActiveSpell(caster) ?? TargetCastStartObserver.GetRecentSpell(caster),
                out progress, out remaining, out timingSource);
            Plugin.LogDiagnosticWarning("ArcaneUI CastProbe SAMPLE " + sample +
                " at=" + Time.unscaledTime.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture) +
                " castTime(raw)=" + DiagnosticValue(rawTimer) +
                " SpellChargeTime(raw)=" + DiagnosticValue(Member(rawSpell ?? TargetCastStartObserver.GetRecentSpell(caster), "SpellChargeTime")) +
                " CurrentlyCasting=" + DiagnosticValue(rawSpell) +
                " CurrentVessel=" + DiagnosticValue(vessel) +
                " timingSource=" + timingSource +
                (timed ? " progress=" + progress.ToString("0.000") +
                    " remaining=" + remaining.ToString("0.000") : " timer=unresolved"));
            if (sample != 0 || vessel == null) return;
            // One-time shallow inspection of the vessel. Read only scalar
            // data; do not invoke native methods or enumerate collections.
            int count = 0;
            foreach (FieldInfo field in vessel.GetType().GetFields(Flags))
            {
                if (!IsCastRelated(field.Name)) continue;
                object value;
                try { value = field.GetValue(vessel); }
                catch (Exception) { continue; }
                Plugin.LogDiagnosticWarning("ArcaneUI CastProbe VESSEL " + field.Name +
                    " : " + field.FieldType.Name + " = " + DiagnosticValue(value));
                if (++count >= 25) break;
            }
        }

        internal static bool WasInterrupted(Character character)
        {
            if (character == null) return false;
            return WasInterruptedSources(character, character.MyNPC, character.MyStats, GetCaster(character));
        }
        internal static bool WasInterrupted(Stats stats)
        {
            if (stats == null) return false;
            return WasInterruptedSources(stats, GetPlayerCaster(stats), GameData.PlayerControl);
        }
        private static bool WasInterruptedSources(params object[] sources)
        {
            foreach (object source in sources)
            {
                object result = Member(source, "CastInterrupted", "WasCastInterrupted", "CastingInterrupted");
                if (result is bool && (bool)result) return true;
            }
            return false;
        }
        internal static bool TryReadCombat(object unit, out bool combat)
        {
            combat = false;
            object value = Member(unit, "InCombat", "IsInCombat", "CurrentlyInCombat");
            if (value is bool) { combat = (bool)value; return true; }
            return false;
        }
        internal static int ReadStacks(StatusEffect status)
        {
            float value;
            if (Number(Member(status, "Stacks", "StackCount", "NumStacks"), out value))
                return Mathf.Clamp(Mathf.RoundToInt(value), 1, 999);
            return 1;
        }
        internal static Sprite ReadSpellIcon(Spell spell)
        {
            return Member(spell, "Icon", "SpellIcon", "IconSprite", "SpellSprite", "MyIcon") as Sprite;
        }
    }
}
