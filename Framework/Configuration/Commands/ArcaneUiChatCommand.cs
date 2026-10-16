using System;
using System.Reflection;
using HarmonyLib;

namespace ArcanesGroupFrames.Framework.Configuration.Commands
{
    /// <summary>
    /// Dispatches the ArcaneUI slash command through Erenshor's normal
    /// command-processing path. No keyboard polling or custom chat input.
    /// </summary>
    internal static class ArcaneUiChatCommand
    {
        internal static void Install(Harmony harmony)
        {
            if (harmony == null) return;

            // Keep the patch optional: if Erenshor changes its command API,
            // other ArcaneUI features should still load.
            MethodInfo commandMethod = AccessTools.Method(typeof(TypeText), "CheckCommands");
            MethodInfo prefix = AccessTools.Method(typeof(ArcaneUiChatCommand), nameof(Prefix));
            if (commandMethod == null || prefix == null)
            {
                Plugin.LogWarning("ArcaneUI: TypeText.CheckCommands is unavailable. /arcaneui was not registered.");
                return;
            }

            try
            {
                harmony.Patch(commandMethod, prefix: new HarmonyMethod(prefix));
                Plugin.LogInfo("ArcaneUI slash command registered: /arcaneui.");
            }
            catch (Exception exception)
            {
                Plugin.LogWarning("ArcaneUI: failed to register the chat command: " + exception);
            }
        }

        // Only intercept our own slash command; every other line continues
        // unmodified to native Erenshor and other mods.
        private static bool Prefix(TypeText __instance)
        {
            if (__instance == null || __instance.typed == null) return true;

            string input = __instance.typed.text;
            if (string.IsNullOrWhiteSpace(input)) return true;
            // One command only. Subcommands and aliases are not consumed.
            // Leave all other chat input entirely to Erenshor / other mods.
            if (!string.Equals(input.Trim(), "/arcaneui", StringComparison.OrdinalIgnoreCase))
                return true;

            var runtime = Plugin.Instance != null ? Plugin.Instance.UiRuntime : null;
            if (runtime == null) return true;

            // Consume just our slash command, without touching native submit lifecycle.
            __instance.typed.text = string.Empty;
            runtime.QueueSettingsToggle();
            return false;
        }
    }
}
