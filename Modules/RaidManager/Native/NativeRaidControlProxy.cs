using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ArcanesGroupFrames
{
    /// <summary>
    /// Bridges our replacement raid-control UI to the native
    /// RaidManager UI callbacks.
    ///
    /// The native RaidManager can be visually hidden while remaining
    /// active. Invoking its Button.onClick event therefore preserves
    /// Erenshor's original behavior without duplicating raid logic.
    /// </summary>
    internal static class NativeRaidControlProxy
    {
        // ============================================================
        // PUBLIC COMMANDS
        // ============================================================

        internal static void TanksEngage()
        {
            InvokeNativeButton(
                "Tanks Engage",
                "Tank Engage",
                "Tanks");
        }


        internal static void BurnTarget()
        {
            InvokeNativeButton(
                "Burn Target",
                "Burn");
        }


        internal static void InviteRaider()
        {
            InvokeNativeButton(
                "Invite Raider",
                "Invite");
        }


        internal static void SaveRaid()
        {
            InvokeNativeButton(
                "Save",
                "Save Raid");


            // The native raid save owns roster persistence. Our replacement
            // raid frames additionally persist the exact GroupNumber +
            // SlotIndex arrangement so Load can reconstruct the same visual
            // and functional formation.
            RaidLayoutPersistence
                .CaptureCurrentLayout();
        }


        internal static void LoadRaid()
        {
            if (!InvokeNativeButton(
                    "Load",
                    "Load Raid"))
                return;

            // The native callback may continue repopulating raid slots after
            // this click. Retry and verify the saved arrangement over several
            // subsequent frames, not just the initial (possibly stale) roster.
            RaidLayoutPersistence.BeginRestoreAfterNativeLoad();
        }


        internal static void LootDistribution()
        {
            InvokeNativeButton(
                "Loot Dist.",
                "Loot Dist",
                "Loot Distribution");
        }


        // ============================================================
        // NATIVE BUTTON LOOKUP
        // ============================================================

        private static bool InvokeNativeButton(
            params string[] possibleLabels)
        {
            RaidManager manager =
                GameData.RaidManager;


            if (manager == null)
            {
                manager =
                    Object.FindObjectOfType<RaidManager>(
                        true);
            }


            if (manager == null)
            {
                Plugin.LogWarning(
                    "Native RaidManager could not be found.");

                return false;
            }


            Button[] buttons =
                manager.GetComponentsInChildren<Button>(
                    true);


            if (buttons == null ||
                buttons.Length == 0)
            {
                Plugin.LogWarning(
                    "Native RaidManager contains no UI Buttons.");

                return false;
            }


            foreach (string requestedLabel
                     in possibleLabels)
            {
                string requested =
                    Normalize(
                        requestedLabel);


                // ----------------------------------------------------
                // PASS 1:
                // Exact normalized text match.
                // ----------------------------------------------------

                foreach (Button button
                         in buttons)
                {
                    if (button == null)
                    {
                        continue;
                    }


                    string label =
                        GetButtonLabel(
                            button);


                    if (string.IsNullOrEmpty(
                        label))
                    {
                        continue;
                    }


                    if (Normalize(label) !=
                        requested)
                    {
                        continue;
                    }


                    Invoke(
                        button,
                        label);

                    return true;
                }


                // ----------------------------------------------------
                // PASS 2:
                // Fuzzy containment.
                //
                // Useful for things such as:
                // "Loot dist." vs "Loot Distribution"
                // ----------------------------------------------------

                foreach (Button button
                         in buttons)
                {
                    if (button == null)
                    {
                        continue;
                    }


                    string label =
                        GetButtonLabel(
                            button);


                    if (string.IsNullOrEmpty(
                        label))
                    {
                        continue;
                    }


                    string native =
                        Normalize(
                            label);


                    if (!native.Contains(
                            requested) &&
                        !requested.Contains(
                            native))
                    {
                        continue;
                    }


                    Invoke(
                        button,
                        label);

                    return true;
                }
            }


            Plugin.LogWarning(
                "Could not locate native RaidManager button: " +
                string.Join(
                    " / ",
                    possibleLabels));


            return false;
        }


        // ============================================================
        // INVOKE
        // ============================================================

        private static void Invoke(
            Button button,
            string label)
        {
            try
            {
                button.onClick.Invoke();


}
            catch (System.Exception exception)
            {
                Plugin.LogError(
                    $"Failed invoking native raid control " +
                    $"'{label}': {exception}");
            }
        }


        // ============================================================
        // BUTTON TEXT
        // ============================================================

        private static string GetButtonLabel(
            Button button)
        {
            if (button == null)
            {
                return null;
            }


            // --------------------------------------------------------
            // Legacy Unity UI Text
            // --------------------------------------------------------

            Text legacy =
                button.GetComponentInChildren<Text>(
                    true);


            if (legacy != null &&
                !string.IsNullOrWhiteSpace(
                    legacy.text))
            {
                return legacy.text;
            }


            // --------------------------------------------------------
            // TextMeshPro
            // --------------------------------------------------------

            TMP_Text tmp =
                button.GetComponentInChildren<TMP_Text>(
                    true);


            if (tmp != null &&
                !string.IsNullOrWhiteSpace(
                    tmp.text))
            {
                return tmp.text;
            }


            return null;
        }


        // ============================================================
        // NORMALIZE
        // ============================================================

        private static string Normalize(
            string value)
        {
            if (string.IsNullOrEmpty(
                value))
            {
                return string.Empty;
            }


            StringBuilder builder =
                new StringBuilder();


            foreach (char c in value)
            {
                if (!char.IsLetterOrDigit(
                    c))
                {
                    continue;
                }


                builder.Append(
                    char.ToLowerInvariant(
                        c));
            }


            return builder.ToString();
        }
    }
}