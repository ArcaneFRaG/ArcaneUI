using UnityEngine;

namespace ArcanesGroupFrames.Framework.Configuration
{
    // Shared ArcaneUI UI preferences, persisted individually through PlayerPrefs.
    // These settings are independent of any cross-module profile mechanism.
    internal static class ArcaneUiPreferences
    {
        private const string Prefix = "ArcaneUI.Config.";

        internal static string ThemeId
        {
            get { return PlayerPrefs.GetString(Prefix + "Theme", "dark"); }
            set { PlayerPrefs.SetString(Prefix + "Theme", value); PlayerPrefs.Save(); }
        }

        internal static string LastPage
        {
            get { return PlayerPrefs.GetString(Prefix + "LastPage", "global"); }
            set { PlayerPrefs.SetString(Prefix + "LastPage", value); PlayerPrefs.Save(); }
        }
    }
}
