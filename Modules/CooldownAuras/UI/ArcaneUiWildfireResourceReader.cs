using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace ArcanesGroupFrames.Modules.CooldownAuras.UI
{
    /// <summary>
    /// Target-specific, read-only FOTF Wildfire reservoir. The gameplay runtime
    /// decides whether the current target still has a valid Wildfire state.
    /// No approximate DoT sampling, target cache, or FOTF compile reference.
    /// </summary>
    internal static class ArcaneUiWildfireResourceReader
    {
        private static readonly List<MethodInfo> Providers = new List<MethodInfo>();
        private static readonly object[] Arguments = { 0f, 0f, 0 };
        private static float _nextDiscovery;

        internal static bool TryRead(bool projected, out float damage)
        {
            damage = 0f;
            if (GameData.PlayerControl == null ||
                GameData.PlayerControl.CurrentTarget == null) return false;
            if (Providers.Count == 0 && Time.unscaledTime >= _nextDiscovery)
            {
                _nextDiscovery = Time.unscaledTime + 5f;
                const BindingFlags flags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
                foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    try
                    {
                        Type runtime = assembly.GetType("FateOfTheFallen.EmberweaverPassiveRuntime", false);
                        if (runtime == null) continue;
                        MethodInfo method = runtime.GetMethod("TryGetCurrentTargetWildfire", flags,
                            null, new[] { typeof(float).MakeByRefType(), typeof(float).MakeByRefType(),
                                typeof(int).MakeByRefType() }, null);
                        if (method != null && method.ReturnType == typeof(bool) && !Providers.Contains(method))
                            Providers.Add(method);
                    }
                    catch (Exception) { /* FOTF is an optional plugin. */ }
                }
            }
            foreach (MethodInfo method in Providers)
            {
                try
                {
                    Arguments[0] = 0f;
                    Arguments[1] = 0f;
                    Arguments[2] = 0;
                    if (!Convert.ToBoolean(method.Invoke(null, Arguments))) continue;
                    float stored = Mathf.Max(0f, Convert.ToSingle(Arguments[0]));
                    int projectedDamage = Mathf.Max(0, Convert.ToInt32(Arguments[2]));
                    damage = projected ? projectedDamage : stored;
                    return true;
                }
                catch (Exception) { /* Skip incompatible/stale FOTF assembly. */ }
            }
            return false;
        }
    }
}
