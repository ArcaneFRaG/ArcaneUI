using UnityEngine;

namespace ArcanesGroupFrames.Modules.EnemyTracker.Configuration
{
    // The in-game tracker and the right-side visual-only preview use these exact
    // colour/opacity rules to avoid style drift. Never sends combat commands.
    internal static class EnemyTrackerVisualStyle
    {
        internal static Color Parse(string hex, Color fallback)
        {
            Color c;
            return ColorUtility.TryParseHtmlString(hex, out c) ? c : fallback;
        }
        internal static Color Alpha(Color c, float alpha)
        {
            return new Color(c.r, c.g, c.b, Mathf.Clamp01(alpha));
        }
        internal static float PanelAlpha
        {
            get
            {
                if (EnemyTrackerSettings.BackgroundStyle == "Clear") return 0f;
                if (EnemyTrackerSettings.BackgroundStyle == "Soft") return EnemyTrackerSettings.Opacity * .40f;
                return EnemyTrackerSettings.Opacity;
            }
        }
        internal static Color RowColor(bool selected)
        {
            Color bg = Parse(EnemyTrackerSettings.BackgroundHex, new Color(.055f, .075f, .10f));
            Color target = Parse(EnemyTrackerSettings.TargetHex, new Color(.85f, .72f, .42f));
            bool active = selected && EnemyTrackerSettings.HighlightTarget;
            return Alpha(active ? Color.Lerp(bg, target, .37f) : Color.Lerp(bg, Color.white, .06f),
                EnemyTrackerSettings.Opacity * .98f);
        }
        // command: 1 party, 2/3/4 raid groups, 5 urgent.
        internal static Color CommandColor(int command, bool assigned)
        {
            string activeHex = EnemyTrackerSettings.ActiveCommandHex;
            if (command == 1) activeHex = EnemyTrackerSettings.PartyActiveHex;
            else if (command == 2) activeHex = EnemyTrackerSettings.G1ActiveHex;
            else if (command == 3) activeHex = EnemyTrackerSettings.G2ActiveHex;
            else if (command == 4) activeHex = EnemyTrackerSettings.G3ActiveHex;
            else if (command == 5) activeHex = EnemyTrackerSettings.UrgentActiveHex;
            string hex = assigned ? activeHex : command == 5 ?
                EnemyTrackerSettings.UrgentIdleHex : EnemyTrackerSettings.CommandHex;
            return Alpha(Parse(hex, assigned ? new Color(.13f, .56f, .51f) :
                new Color(.32f, .23f, .16f)), EnemyTrackerSettings.CommandOpacity);
        }
        internal static bool Highlight(bool selected)
        {
            return selected && EnemyTrackerSettings.HighlightTarget;
        }
    }
}
