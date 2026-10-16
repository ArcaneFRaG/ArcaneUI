using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;

namespace ArcanesGroupFrames.Modules.ActionBars.Native
{
    /// <summary>
    /// Observe successful *player* StartSpell calls from either the native bar
    /// or ArcaneUI. The game owns real cooldown enforcement; the display may
    /// show an estimated authored recast interval if no live timer is exposed.
    /// </summary>
    [HarmonyPatch]
    internal static class NativeSpellCooldownObserver
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            foreach (MethodInfo method in typeof(CastSpell).GetMethods(
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            {
                if (method.Name != "StartSpell" || method.ReturnType != typeof(bool)) continue;
                ParameterInfo[] args = method.GetParameters();
                if (args.Length >= 2 && args[0].ParameterType == typeof(Spell) &&
                    args[1].ParameterType == typeof(Stats)) yield return method;
            }
        }
        private static void Postfix(CastSpell __instance, Spell _spell, bool __result)
        {
            if (!__result || _spell == null || __instance == null ||
                GameData.PlayerControl == null ||
                __instance.GetComponent<PlayerControl>() != GameData.PlayerControl) return;
            NativeActionBarDiscovery.ObserveAcceptedSpell(_spell);
        }
    }
}
