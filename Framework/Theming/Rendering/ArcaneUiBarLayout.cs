using UnityEngine;

namespace ArcanesGroupFrames.Framework.Theming.Rendering
{
    /// <summary>Independent persisted health/mana proportions for each UI module.</summary>
    internal static class ArcaneUiBarLayout
    {
        private const string KeyPrefix = "ArcaneUI.Bars.";
        internal static float ManaPercent(string module)
        {
            return Mathf.Clamp(PlayerPrefs.GetFloat(KeyPrefix + module + ".ManaPercent", 20f), 10f, 65f);
        }

        internal static void SetManaPercent(string module, float value)
        {
            PlayerPrefs.SetFloat(KeyPrefix + module + ".ManaPercent", Mathf.Clamp(value, 10f, 65f));
            PlayerPrefs.Save();
        }

        internal static float ManaHeight(string module, float availableHeight, bool shown)
        {
            if (!shown) return 0f;
            return Mathf.Max(0f, availableHeight) * ManaPercent(module) / 100f;
        }
    }
}
