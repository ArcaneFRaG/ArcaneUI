using System;
using System.Reflection;
using UnityEngine;

namespace ArcanesGroupFrames
{
    /// <summary>
    /// Optional, read-only integration with FOTF's public Lightbringer shield API.
    /// The plugin must also implement and register real wards; ArcaneUI only draws them.
    /// No compile-time FOTF reference and no per-frame reflection on the success path.
    /// </summary>
    internal static class FOTFAbsorbInterop
    {
        private const string ApiTypeName = "FateOfTheFallen.LightbringerAbsorbApi";
        private const string ApiMethodName = "GetRemainingAbsorb";
        private static Func<Stats, int> _read;
        private static float _retryAt;
        private static string _status = "Not checked";

        internal static int GetRemaining(Stats stats)
        {
            if (stats == null || stats.CurrentHP <= 0) return 0;
            if (_read == null && Time.unscaledTime >= _retryAt) Resolve();
            if (_read == null) return 0;
            try { return Math.Max(0, _read(stats)); }
            catch (Exception ex)
            {
                _status = "API invocation failed: " + ex.GetType().Name + " - " + ex.Message;
                _read = null;
                _retryAt = Time.unscaledTime + 5f;
                return 0;
            }
        }

        private static Type FindApiType()
        {
            Type api = Type.GetType(ApiTypeName + ", FateOfTheFallen", false);
            if (api != null) return api;
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            foreach (Assembly assembly in assemblies)
            {
                try
                {
                    api = assembly.GetType(ApiTypeName, false);
                    if (api != null) return api;
                }
                catch { /* A separately loaded optional mod must not break the HUD. */ }
            }
            // FOTF may change its assembly name or move the API namespace.
            // Only inspect assemblies associated with FOTF, never the entire Unity domain.
            foreach (Assembly assembly in assemblies)
            {
                string name;
                try { name = assembly.GetName().Name ?? ""; }
                catch { continue; }
                if (name.IndexOf("Fate", StringComparison.OrdinalIgnoreCase) < 0 &&
                    name.IndexOf("FOTF", StringComparison.OrdinalIgnoreCase) < 0) continue;
                try
                {
                    foreach (Type type in assembly.GetTypes())
                        if (type != null && type.Name == "LightbringerAbsorbApi") return type;
                }
                catch (ReflectionTypeLoadException ex)
                {
                    if (ex.Types == null) continue;
                    foreach (Type type in ex.Types)
                        if (type != null && type.Name == "LightbringerAbsorbApi") return type;
                }
                catch { }
            }
            return null;
        }

        private static void Resolve()
        {
            _retryAt = Time.unscaledTime + 5f;
            Type api = FindApiType();
            if (api == null)
            {
                _status = "API type not found in loaded assemblies: " + ApiTypeName;
                return;
            }
            MethodInfo method = api.GetMethod(ApiMethodName,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static,
                null, new[] { typeof(Stats) }, null);
            if (method == null)
            {
                _status = "Found " + api.FullName + " in " + api.Assembly.GetName().Name +
                    ", but no static GetRemainingAbsorb(Stats) method. FOTF API may have changed.";
                return;
            }
            try
            {
                // Support common numeric-return versions without reflection per frame.
                if (method.ReturnType == typeof(int))
                    _read = (Func<Stats, int>)Delegate.CreateDelegate(typeof(Func<Stats, int>), method);
                else if (method.ReturnType == typeof(float))
                {
                    var fn = (Func<Stats, float>)Delegate.CreateDelegate(typeof(Func<Stats, float>), method);
                    _read = s => SafeToInt(fn(s));
                }
                else if (method.ReturnType == typeof(double))
                {
                    var fn = (Func<Stats, double>)Delegate.CreateDelegate(typeof(Func<Stats, double>), method);
                    _read = s => SafeToInt(fn(s));
                }
                else if (method.ReturnType == typeof(long))
                {
                    var fn = (Func<Stats, long>)Delegate.CreateDelegate(typeof(Func<Stats, long>), method);
                    _read = s => (int)Math.Max(0L, Math.Min((long)int.MaxValue, fn(s)));
                }
                else
                {
                    _status = "Unsupported FOTF absorb return type: " + method.ReturnType.FullName;
                    return;
                }
                _status = "Connected: " + api.FullName + "." + ApiMethodName +
                    "(Stats) -> " + method.ReturnType.Name + " in " + api.Assembly.GetName().Name;
            }
            catch (Exception ex)
            {
                _read = null;
                _status = "API bind failed: " + ex.GetType().Name + " - " + ex.Message;
            }
        }

        private static int SafeToInt(double number)
        {
            if (double.IsNaN(number) || number <= 0d) return 0;
            return number >= int.MaxValue ? int.MaxValue : (int)Math.Round(number);
        }

        /// <summary>Explicitly invoked by /arcaneui diagnostics; no combat spam.</summary>
        internal static void LogDiagnostics(Stats player, Stats target)
        {
            if (_read == null) Resolve();
            Plugin.LogDiagnosticWarning("ArcaneUI Absorb DIAGNOSTIC: " + _status);
            Plugin.LogDiagnosticWarning("ArcaneUI Absorb DIAGNOSTIC: overlay enabled=" +
                Framework.Theming.Rendering.FOTFAbsorbOverlay.Enabled +
                ", visual test=" + Framework.Theming.Rendering.FOTFAbsorbOverlay.VisualTestActive +
                ", opacity=" + Framework.Theming.Rendering.FOTFAbsorbOverlay.Opacity +
                "%, minimum visible width=" + Framework.Theming.Rendering.FOTFAbsorbOverlay.MinimumVisibleWidth + "%.");
            if (player != null)
                Plugin.LogDiagnosticWarning("ArcaneUI Absorb PLAYER: " + (player.MyName ?? "Unknown") +
                    " HP=" + player.CurrentHP + "/" + player.CurrentMaxHP +
                    " raw API remaining=" + GetRemaining(player));
            else
                Plugin.LogDiagnosticWarning("ArcaneUI Absorb PLAYER: native GameData.PlayerStats unavailable.");
            if (target != null)
                Plugin.LogDiagnosticWarning("ArcaneUI Absorb TARGET: " + (target.MyName ?? "Unknown") +
                    " HP=" + target.CurrentHP + "/" + target.CurrentMaxHP +
                    " raw API remaining=" + GetRemaining(target));
            else
                Plugin.LogDiagnosticWarning("ArcaneUI Absorb TARGET: no selected target Stats.");
            if (_read == null)
                Plugin.LogDiagnosticWarning("ArcaneUI Absorb DIAGNOSTIC: no real shields can display until the FOTF API is present and callable.");
            else
                Plugin.LogDiagnosticWarning("ArcaneUI Absorb DIAGNOSTIC: zero raw API value means FOTF has not reported a shield for that Stats instance; visual width settings cannot change it.");
        }
    }
}
