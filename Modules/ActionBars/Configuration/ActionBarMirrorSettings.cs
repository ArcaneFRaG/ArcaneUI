using UnityEngine;

namespace ArcanesGroupFrames.Modules.ActionBars.Configuration
{
    /// <summary>
    /// Explicit opt-in, read-only display of scanned native primary hotkeys.
    /// This does not replace native game controls or create actionable slots.
    /// </summary>
    internal static class ActionBarMirrorSettings
    {
        private const string Prefix = "ArcaneUI.ActionBars.Mirror.";
        internal static bool Enabled { get { return PlayerPrefs.GetInt(Prefix + "Enabled", 0) != 0; } }
        internal static void SetEnabled(bool enabled)
        {
            PlayerPrefs.SetInt(Prefix + "Enabled", enabled ? 1 : 0);
            PlayerPrefs.Save();
        }
        // Primary native hotbar stays fully operational even when its original
        // art is faded behind the replacement. Hiding is opt-in and reversible.
        internal static bool HideNativeBar { get { return PlayerPrefs.GetInt(Prefix + "HideNative", 0) != 0; } }
        internal static void SetHideNativeBar(bool enabled)
        {
            PlayerPrefs.SetInt(Prefix + "HideNative", enabled ? 1 : 0);
            PlayerPrefs.Save();
        }
        internal static int X { get { return Mathf.Clamp(PlayerPrefs.GetInt(Prefix + "X", 0), -800, 800); } }
        internal static int Y { get { return Mathf.Clamp(PlayerPrefs.GetInt(Prefix + "Y", 155), 0, 900); } }
        internal static void SetPosition(float x, float y)
        {
            PlayerPrefs.SetInt(Prefix + "X", Mathf.Clamp(Mathf.RoundToInt(x), -800, 800));
            PlayerPrefs.SetInt(Prefix + "Y", Mathf.Clamp(Mathf.RoundToInt(y), 0, 900));
            PlayerPrefs.Save();
        }
        internal static void SetX(float x)
        {
            PlayerPrefs.SetInt(Prefix + "X", Mathf.Clamp(Mathf.RoundToInt(x), -800, 800));
            PlayerPrefs.Save();
        }
        internal static void SetY(float y)
        {
            PlayerPrefs.SetInt(Prefix + "Y", Mathf.Clamp(Mathf.RoundToInt(y), 0, 900));
            PlayerPrefs.Save();
        }
    }
}
