using System;
using System.Reflection;
using UnityEngine;

namespace ArcanesGroupFrames.Modules.CooldownAuras.UI
{
    /// <summary>Read-only resource providers. FOTF is optional: no hard dependency and
    /// reflection lookups are cached; unavailable providers never invent resource values.</summary>
    internal static class ArcaneUiAuraResourceReader
    {
        // Keep the FOTF integration reflection-only and read-only. The preferred
        // r107C telemetry API lives on EmberweaverPassiveRuntime, alongside
        // the authoritative _heat field; the FOTF HUD is optional and can be
        // invisible while ArcaneUI's own settings window is open.
        private sealed class EmberweaverProvider
        {
            internal Assembly Assembly;
            internal Type Runtime;
            internal PropertyInfo Heat;
            internal FieldInfo HeatField;
            internal PropertyInfo Flashfire;
            internal FieldInfo FlashfireField;
            internal MethodInfo IsClass;
            internal FieldInfo HudBehaviour;
            internal FieldInfo HudCachedHeat;
            internal FieldInfo HudCachedFlashfire;
            internal FieldInfo HudHasHeat;
            internal FieldInfo HudCanRender;
            internal PropertyInfo InteropHasSample;
            internal PropertyInfo InteropHeat;
            internal PropertyInfo InteropFlashfire;
            internal PropertyInfo InteropUpdatedAt;
            internal PropertyInfo InteropRevision;
            internal string InteropOwner = "unavailable";
            internal string InteropStatus = "not checked";
            internal string Revision = "unknown";
            internal string Identity;

            // 9.11F: distinguish "old FOTF DLL/no telemetry API" from
            // "API present but no live sample". The writer now runs from
            // the FOTF gameplay Update, regardless of HUD visibility.
            internal bool TryReadInteropHud(out int heat, out bool flashfire)
            {
                heat = 0;
                flashfire = false;
                if (InteropHasSample == null || InteropHeat == null ||
                    InteropFlashfire == null || InteropUpdatedAt == null)
                {
                    InteropStatus = "API MISSING on FOTF runtime and legacy HUD; " +
                        "install FOTF r107C and ArcaneUI 9.11G";
                    return false;
                }
                try
                {
                    if (!Convert.ToBoolean(InteropHasSample.GetValue(null, null)))
                    {
                        InteropStatus = "API present on " + InteropOwner + " (" + Revision +
                            ") but publisher not running";
                        return false;
                    }
                    float stamp = Convert.ToSingle(InteropUpdatedAt.GetValue(null, null));
                    float age = Time.unscaledTime - stamp;
                    if (age < -0.5f || age > 0.5f)
                    {
                        InteropStatus = "API on " + InteropOwner + " (" + Revision + ") stale, age=" +
                            age.ToString("F2") + "s";
                        return false;
                    }
                    heat = Mathf.Clamp(Convert.ToInt32(InteropHeat.GetValue(null, null)), 0, 10);
                    flashfire = Convert.ToBoolean(InteropFlashfire.GetValue(null, null));
                    InteropStatus = "LIVE " + InteropOwner + " (" + Revision + ")";
                    return true;
                }
                catch (Exception exception)
                {
                    InteropStatus = "API read threw " + exception.GetType().Name;
                    return false;
                }
            }

            internal bool TryReadRuntimeHeat(out int heat)
            {
                heat = 0;
                try
                {
                    object raw = Heat != null ? Heat.GetValue(null, null) :
                        HeatField != null ? HeatField.GetValue(null) : null;
                    if (raw == null) return false;
                    heat = Mathf.Clamp(Convert.ToInt32(raw), 0, 10);
                    return true;
                }
                catch (Exception) { return false; }
            }

            internal bool TryReadRuntimeFlashfire(out bool ready)
            {
                ready = false;
                try
                {
                    object raw = Flashfire != null ? Flashfire.GetValue(null, null) :
                        FlashfireField != null ? FlashfireField.GetValue(null) : null;
                    if (raw == null) return false;
                    ready = Convert.ToBoolean(raw);
                    return true;
                }
                catch (Exception) { return false; }
            }

            // The actual HUD's cached value is refreshed by FOTF every 0.1s.
            // It is read-only and only used if the *active, rendering* HUD
            // disagrees with an otherwise-zero runtime read.
            internal bool TryReadLiveHud(out int heat, out bool flashfire)
            {
                heat = 0;
                flashfire = false;
                try
                {
                    object behaviour = HudBehaviour != null ? HudBehaviour.GetValue(null) : null;
                    MonoBehaviour component = behaviour as MonoBehaviour;
                    if (component == null || !component.isActiveAndEnabled ||
                        HudCachedHeat == null) return false;
                    if (HudCanRender != null && !Convert.ToBoolean(
                        HudCanRender.GetValue(behaviour))) return false;
                    if (HudHasHeat != null && !Convert.ToBoolean(
                        HudHasHeat.GetValue(behaviour))) return false;
                    heat = Mathf.Clamp(Convert.ToInt32(HudCachedHeat.GetValue(behaviour)), 0, 10);
                    if (HudCachedFlashfire != null)
                        flashfire = Convert.ToBoolean(HudCachedFlashfire.GetValue(behaviour));
                    return true;
                }
                catch (Exception) { return false; }
            }
        }

        private static readonly System.Collections.Generic.List<EmberweaverProvider> _providers =
            new System.Collections.Generic.List<EmberweaverProvider>();
        private static EmberweaverProvider _activeProvider;
        private static Type _emberRuntime;
        private static MethodInfo _isEmberweaver;
        private static float _nextResolveAttempt;
        private static float _nextResourceSample;
        private static bool _resourceSampleValid;
        private static int _sampleHeat;
        private static bool _sampleFlashfire;
        private static bool _sampleHudIsLive;
        private static int _sampleRawHeat;
        private static int _sampleHudHeat;
        private static bool _sampleInteropLive;
        private static int _sampleInteropHeat;
        private static string _emberStatus = "FOTF Emberweaver provider not resolved";
        private static string _lastInteropState;
        private static string _sampleInteropState = "not sampled";
        private static string _lastWarning;
        private static bool _loggedFirstHeatSample;
        private static float _nextHeatSampleLog;
        private static int _loggedHeatValue = -1;
        private static bool _loggedInteropLive;
        private static string _loggedAuraBlocker;

        // FOTF registers Heat as a real, learned native Skill. A saved class
        // can temporarily look like its native Arcanist backing class; checking
        // the unique skill ID is a reliable second source of class identity.
        internal static bool HasLearnedHeat()
        {
            try
            {
                Character actor = GameData.PlayerStats != null ? GameData.PlayerStats.Myself : null;
                UseSkill skillbook = actor != null ? actor.MySkills : null;
                if (skillbook == null && GameData.PlayerControl != null)
                    skillbook = GameData.PlayerControl.GetComponent<UseSkill>();
                if (skillbook == null || skillbook.KnownSkills == null) return false;
                foreach (Skill known in skillbook.KnownSkills)
                    if (known != null && string.Equals(known.Id, "FOTF_EW_SKILL_HEAT",
                        StringComparison.OrdinalIgnoreCase)) return true;
            }
            catch (Exception exception)
            {
                ReportBridgeFailure("Heat skill inspection failed: " + exception.GetType().Name);
            }
            return false;
        }

        private static bool NativeOrSavedEmberweaver()
        {
            Stats stats = GameData.PlayerStats;
            return stats != null && string.Equals(
                RaidClassColors.ResolveClassNameForActor(stats, stats.MyName),
                "Emberweaver", StringComparison.OrdinalIgnoreCase);
        }

        // Log on genuine changes, including a change from 0 to the first stack.
        // Avoid dumping one line per UI frame or per tracked aura.
        private static void TraceHeatSample(int heat)
        {
            if (_loggedFirstHeatSample && _loggedHeatValue == heat &&
                _loggedInteropLive == _sampleInteropLive) return;
            float now = Time.unscaledTime;
            if (_loggedFirstHeatSample && now < _nextHeatSampleLog) return;
            _loggedFirstHeatSample = true;
            _loggedHeatValue = heat;
            _loggedInteropLive = _sampleInteropLive;
            _nextHeatSampleLog = now + 0.5f;
            Plugin.LogInfo("[ArcaneUI Heat] 9.11G live Heat=" + heat + "/10" +
                " | runtime=" + _sampleRawHeat + " | FOTF sample=" +
                (_sampleInteropLive ? _sampleInteropHeat.ToString() : "unavailable") +
                " | bridge=" + _sampleInteropState +
                " | legacy HUD=" +
                (_sampleHudIsLive ? _sampleHudHeat.ToString() : "not active") +
                " | source=" + (_activeProvider != null ? _activeProvider.Identity : "unavailable") +
                " | candidates=" + _providers.Count);
        }

        internal static void TraceAuraSuppressed(string reason)
        {
            if (string.Equals(_loggedAuraBlocker, reason, StringComparison.Ordinal)) return;
            _loggedAuraBlocker = reason;
            Plugin.LogDiagnosticWarning("[ArcaneUI Heat] 9.11C aura not evaluated: " + reason);
        }

        internal static void ForceHeatDiagnostic()
        {
            _nextResourceSample = 0f; // The Refresh button must not report a stale cached sample.
            float value, maximum;
            bool live = TryRead("Emberweaver Heat", out value, out maximum);
            Plugin.LogDiagnosticWarning("[ArcaneUI Heat] 9.11G REFRESH live=" + live +
                " value=" + value + "/" + maximum +
                " runtime=" + _sampleRawHeat +
                " FOTF published=" + (_sampleInteropLive ? _sampleInteropHeat.ToString() : "unavailable") +
                " bridge=" + _sampleInteropState +
                " native HUD=" + (_sampleHudIsLive ? _sampleHudHeat.ToString() : "not active") +
                " learned Heat=" + HasLearnedHeat() +
                " FOTF class=" + IsFOTFClass() +
                " Saved class=" + NativeOrSavedEmberweaver() +
                " Selected=" + (_activeProvider != null ? _activeProvider.Identity : "none") +
                " Candidates=" + _providers.Count + " Provider=" + _emberStatus);
            foreach (EmberweaverProvider candidate in _providers)
            {
                int runtimeHeat, hudHeat;
                bool hudFlash;
                bool raw = candidate.TryReadRuntimeHeat(out runtimeHeat);
                bool hud = candidate.TryReadLiveHud(out hudHeat, out hudFlash);
                int publishedHeat;
                bool publishedFlashfire;
                bool published = candidate.TryReadInteropHud(out publishedHeat, out publishedFlashfire);
                Plugin.LogInfo("[ArcaneUI Heat] 9.11G candidate " + candidate.Identity +
                    " runtime=" + (raw ? runtimeHeat.ToString() : "unreadable") +
                    " FOTF published=" + (published ? publishedHeat.ToString() : "unavailable") +
                    " bridge=" + candidate.InteropStatus +
                    " legacy HUD=" + (hud ? hudHeat.ToString() : "not active") +
                    " runtimeType=" + candidate.Runtime.FullName +
                    " interopOwner=" + candidate.InteropOwner);
            }
        }

        // A missing FOTF module should not log a warning every 100 ms.
        private static void ReportBridgeFailure(string reason)
        {
            _emberStatus = reason;
            if (reason == _lastWarning) return;
            _lastWarning = reason;
            Plugin.LogDiagnosticWarning("[ArcaneUI Heat] " + reason);
        }

        // Refresh candidates only every few seconds; static reflection reads
        // use cached PropertyInfo/FieldInfo and never scan the scene.
        private static bool ResolveEmberweaver()
        {
            if (Time.unscaledTime < _nextResolveAttempt)
                return _providers.Count > 0;
            _nextResolveAttempt = Time.unscaledTime + 3f;
            var discovered = new System.Collections.Generic.List<EmberweaverProvider>();
            const BindingFlags flags = BindingFlags.Static | BindingFlags.Instance |
                                       BindingFlags.Public | BindingFlags.NonPublic;
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                // Match by the specific runtime type, not merely by assembly
                // filename: some plugin loaders use alternative assembly names.
                Type runtime;
                try { runtime = assembly.GetType("FateOfTheFallen.EmberweaverPassiveRuntime", false); }
                catch (Exception) { continue; } // optional plugin or dynamic assembly
                if (runtime == null) continue;
                var candidate = new EmberweaverProvider();
                candidate.Assembly = assembly;
                candidate.Runtime = runtime;
                candidate.Heat = runtime.GetProperty("HeatStacks", flags);
                candidate.HeatField = runtime.GetField("_heat", flags);
                candidate.Flashfire = runtime.GetProperty("FlashfireReady", flags);
                candidate.FlashfireField = runtime.GetField("_flashfireReady", flags);
                if (candidate.Heat == null && candidate.HeatField == null) continue;
                Type catalog = assembly.GetType("FateOfTheFallen.EmberweaverCatalog", false);
                candidate.IsClass = catalog != null ? catalog.GetMethod("IsCurrentPlayerEmberweaver", flags) : null;
                Type hudRuntime = assembly.GetType("FateOfTheFallen.EmberweaverHudRuntime", false);
                Type hudBehaviour = assembly.GetType("FateOfTheFallen.EmberweaverHudBehaviour", false);
                candidate.HudBehaviour = hudRuntime != null ? hudRuntime.GetField("_behaviour", flags) : null;

                // 9.11G: First bind all telemetry members on the same runtime
                // type as HeatStacks. This works even when the HUD is never
                // constructed or its IMGUI drawing is temporarily suppressed.
                PropertyInfo apiHas = runtime.GetProperty("ArcaneUiHeatApiHasSample", flags);
                PropertyInfo apiHeat = runtime.GetProperty("ArcaneUiHeatApiValue", flags);
                PropertyInfo apiFlash = runtime.GetProperty("ArcaneUiHeatApiFlashfire", flags);
                PropertyInfo apiTime = runtime.GetProperty("ArcaneUiHeatApiUpdatedAt", flags);
                if (apiHas != null && apiHeat != null && apiFlash != null && apiTime != null)
                {
                    candidate.InteropHasSample = apiHas;
                    candidate.InteropHeat = apiHeat;
                    candidate.InteropFlashfire = apiFlash;
                    candidate.InteropUpdatedAt = apiTime;
                    candidate.InteropRevision = runtime.GetProperty("ArcaneUiHeatApiRevision", flags);
                    candidate.InteropOwner = "PassiveRuntime";
                }
                else if (hudRuntime != null)
                {
                    // Prior r107B handoff: optional backwards compatibility.
                    candidate.InteropHasSample = hudRuntime.GetProperty("InteropHasSample", flags);
                    candidate.InteropHeat = hudRuntime.GetProperty("InteropHeat", flags);
                    candidate.InteropFlashfire = hudRuntime.GetProperty("InteropFlashfireReady", flags);
                    candidate.InteropUpdatedAt = hudRuntime.GetProperty("InteropUpdatedAt", flags);
                    candidate.InteropRevision = hudRuntime.GetProperty("InteropRevision", flags);
                    candidate.InteropOwner = "HudRuntime (legacy)";
                }
                if (candidate.InteropRevision != null)
                {
                    try
                    {
                        object revision = candidate.InteropRevision.GetValue(null, null);
                        if (revision != null) candidate.Revision = revision.ToString();
                    }
                    catch (Exception) { /* optional diagnostic metadata */ }
                }
                if (hudBehaviour != null)
                {
                    candidate.HudCachedHeat = hudBehaviour.GetField("_cachedHeat", flags);
                    candidate.HudCachedFlashfire = hudBehaviour.GetField("_cachedFlashfireReady", flags);
                    candidate.HudHasHeat = hudBehaviour.GetField("_cachedHasHeat", flags);
                    candidate.HudCanRender = hudBehaviour.GetField("_canRender", flags);
                }
                candidate.Identity = assembly.GetName().Name + "#" +
                    System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(assembly).ToString("X");
                discovered.Add(candidate);
            }
            _providers.Clear();
            _providers.AddRange(discovered);
            // A rescan produces new provider wrappers; don't continue reading
            // a candidate that no longer belongs to the live assembly set.
            _activeProvider = null;
            _nextResourceSample = 0f;
            if (_providers.Count == 0)
            {
                _emberRuntime = null;
                _isEmberweaver = null;
                ReportBridgeFailure("FOTF loaded runtime not found; Heat/Flashfire values are unavailable.");
                return false;
            }
            _emberStatus = "FOTF provider available (" + _providers.Count + " assembly instance(s))";
            _lastWarning = null;
            return true;
        }

        // Select the assembly that owns the active FOTF HUD; on fallback,
        // prefer the provider with nonzero real Heat. If its runtime property
        // disagrees with that active HUD, use FOTF's already-sampled displayed
        // value until the next polling cycle. No casts, timers or guessed stacks.
        private static bool SampleEmberweaver()
        {
            if (!ResolveEmberweaver()) return false;
            if (_resourceSampleValid && Time.unscaledTime < _nextResourceSample)
                return true;
            _nextResourceSample = Time.unscaledTime + .045f;
            EmberweaverProvider chosen = null;
            int bestScore = int.MinValue;
            int bestRaw = 0, bestHud = 0, bestInterop = 0;
            bool bestHudLive = false, bestInteropLive = false;
            bool bestReady = false;
            foreach (EmberweaverProvider provider in _providers)
            {
                int runtimeHeat;
                if (!provider.TryReadRuntimeHeat(out runtimeHeat)) continue;
                int hudHeat;
                bool hudReady;
                bool hudLive = provider.TryReadLiveHud(out hudHeat, out hudReady);
                int interopHeat;
                bool interopFlashfire;
                bool interopLive = provider.TryReadInteropHud(out interopHeat, out interopFlashfire);
                bool runtimeReady;
                provider.TryReadRuntimeFlashfire(out runtimeReady);
                // A published, fresh FOTF HUD sample is the primary source.
                // Older FOTF installations use the legacy HUD/runtime path.
                int score = (interopLive ? 10000 : 0) +
                    (hudLive ? 1000 : 0) +
                    (runtimeHeat > 0 ? 10 : 0) +
                    (provider == _activeProvider ? 1 : 0);
                if (score <= bestScore) continue;
                chosen = provider; bestScore = score;
                bestRaw = runtimeHeat;
                bestHud = hudHeat;
                bestHudLive = hudLive;
                bestInterop = interopHeat;
                bestInteropLive = interopLive;
                bestReady = interopLive ? interopFlashfire :
                    (runtimeReady || (hudLive && hudReady));
            }
            _activeProvider = chosen;
            _resourceSampleValid = chosen != null;
            _sampleRawHeat = bestRaw;
            _sampleHudHeat = bestHud;
            _sampleHudIsLive = bestHudLive;
            _sampleInteropLive = bestInteropLive;
            _sampleInteropHeat = bestInterop;
            _sampleInteropState = chosen != null ? chosen.InteropStatus : "no provider";
            if (chosen != null && _sampleInteropState != _lastInteropState)
            {
                _lastInteropState = _sampleInteropState;
                if (!_sampleInteropLive)
                    Plugin.LogDiagnosticWarning("[ArcaneUI Heat] 9.11G bridge status: " + _sampleInteropState);
                else
                    Plugin.LogInfo("[ArcaneUI Heat] 9.11G bridge connected: " + _sampleInteropState);
            }
            if (chosen == null)
            {
                ReportBridgeFailure("FOTF Heat provider detected but no readable runtime found.");
                return false;
            }
            _emberRuntime = chosen.Runtime;
            _isEmberweaver = chosen.IsClass;
            _sampleHeat = bestInteropLive ? bestInterop :
                ((bestHudLive && bestHud > bestRaw) ? bestHud : bestRaw);
            _sampleFlashfire = bestReady;
            _emberStatus = bestInteropLive
                ? "Connected to FOTF gameplay Heat/Flashfire snapshot (" + chosen.Identity + ")"
                : (bestHudLive
                    ? "Connected to legacy FOTF HUD (" + chosen.Identity + ")"
                    : "Connected to FOTF runtime (" + chosen.Identity + ")");
            return true;
        }

        private static bool IsFOTFClass()
        {
            if (!SampleEmberweaver() || _isEmberweaver == null) return false;
            try { return Convert.ToBoolean(_isEmberweaver.Invoke(null, null)); }
            catch (Exception exception)
            {
                ReportBridgeFailure("FOTF class check threw " + exception.GetType().Name);
                return false;
            }
        }

        internal static bool IsCurrentPlayerEmberweaver()
        {
            // The unique native Heat skill survives cases where both mod UI
            // resolvers temporarily see the Arcanist backing class.
            return GameData.PlayerStats != null &&
                (IsFOTFClass() || NativeOrSavedEmberweaver() || HasLearnedHeat());
        }

        internal static string DescribeEmberweaver(string trigger)
        {
            float current, maximum;
            bool live = TryRead(trigger, out current, out maximum);
            float flashfireCurrent, flashfireMaximum;
            bool flashfireLive = TryRead("Emberweaver Flashfire", out flashfireCurrent, out flashfireMaximum);
            return "Heat provider: " + _emberStatus +
                " | Live read: " + (live ? Mathf.RoundToInt(current) + " / " + Mathf.RoundToInt(maximum) : "unavailable") +
                " | Flashfire: " + (flashfireLive ? (flashfireCurrent > 0.5f ? "READY" : "not ready") : "unavailable") +
                " | Heat passive learned: " + (HasLearnedHeat() ? "YES" : "NO") +
                " | Emberweaver identity: " + (IsCurrentPlayerEmberweaver() ? "YES" : "NOT DETECTED") +
                ". Check that the native FOTF HUD also gains charges when a fire hit lands.";
        }

        private static Type _shieldStatsType;
        private static FieldInfo _shieldField;
        private static PropertyInfo _shieldProperty;
        private static bool TryReadNativeShield(Stats stats, out float amount)
        {
            amount = 0f;
            if (stats == null) return false;
            Type type = stats.GetType();
            if (_shieldStatsType != type)
            {
                _shieldStatsType = type;
                const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
                _shieldField = type.GetField("SpellShield", flags);
                _shieldProperty = type.GetProperty("SpellShield", flags);
            }
            try
            {
                object read = _shieldField != null ? _shieldField.GetValue(stats) :
                    (_shieldProperty != null && _shieldProperty.CanRead ? _shieldProperty.GetValue(stats,null) : null);
                if (read == null) return false;
                amount = Mathf.Max(0f,Convert.ToSingle(read));
                return true;
            }
            catch (Exception) { return false; }
        }
        internal static bool IsResource(string trigger)
        {
            return trigger == "Emberweaver Wildfire Stored" || trigger == "Emberweaver Combust Projected" ||
                   trigger == "Emberweaver Heat" || trigger == "Emberweaver Flashfire" ||
                   trigger == "Player Health" || trigger == "Player Mana" ||
                   trigger == "Target Health" || trigger == "Target Mana" ||
                   trigger == "Player Absorb";
        }
        internal static bool TryRead(string trigger, out float value, out float maximum)
        {
            value = 0f; maximum = 0f;
            try
            {
                Stats stats = GameData.PlayerStats;
                if (stats == null) return false;
                if (trigger == "Target Health" || trigger == "Target Mana")
                {
                    Stats target = GameData.PlayerControl != null &&
                        GameData.PlayerControl.CurrentTarget != null ?
                        GameData.PlayerControl.CurrentTarget.MyStats : null;
                    if (target == null || target.CurrentHP <= 0f) return false;
                    if (trigger == "Target Health")
                    {
                        value = Mathf.Max(0f,target.CurrentHP);
                        maximum = Mathf.Max(1f,target.CurrentMaxHP);
                    }
                    else
                    {
                        value = Mathf.Max(0f,target.CurrentMana);
                        maximum = Mathf.Max(1f,target.GetCurrentMaxMana());
                    }
                    return true;
                }
                if (trigger == "Player Absorb")
                {
                    // Native Stats.SpellShield is the gameplay-authoritative
                    // remaining shield, also used by FOTF Lightbringer wards.
                    if (!TryReadNativeShield(stats, out value)) return false;
                    maximum = Mathf.Max(1f, value); // Unknown original capacity.
                    return true;
                }
                if (trigger == "Player Health")
                {
                    value = Mathf.Max(0f, stats.CurrentHP);
                    maximum = Mathf.Max(1f, stats.CurrentMaxHP);
                    return true;
                }
                if (trigger == "Player Mana")
                {
                    value = Mathf.Max(0f, stats.CurrentMana);
                    maximum = Mathf.Max(1f, stats.GetCurrentMaxMana());
                    return true;
                }
                if (trigger == "Emberweaver Wildfire Stored" ||
                    trigger == "Emberweaver Combust Projected")
                {
                    bool projected = trigger == "Emberweaver Combust Projected";
                    if (!ArcaneUiWildfireResourceReader.TryRead(projected, out value)) return false;
                    // Wildfire has no fixed cap. No invented 100%-full maximum.
                    maximum = 0f;
                    return true;
                }
                if (trigger != "Emberweaver Heat" && trigger != "Emberweaver Flashfire") return false;
                // HeatStacks is FOTF's authoritative, player-only state. Do
                // not veto a valid read based on a separate class-name lookup;
                // the Aura load filter handles class selection independently.
                if (!SampleEmberweaver()) return false;
                if (trigger == "Emberweaver Heat")
                {
                    value = _sampleHeat;
                    maximum = 10f;
                    TraceHeatSample(_sampleHeat);
                    return true;
                }
                value = _sampleFlashfire ? 1f : 0f;
                maximum = 1f;
                return true;
            }
            catch (Exception exception)
            {
                ReportBridgeFailure("Failed to read live Emberweaver resource: " +
                    exception.GetType().Name + " - " + exception.Message);
                return false;
            }
        }
    }
}
