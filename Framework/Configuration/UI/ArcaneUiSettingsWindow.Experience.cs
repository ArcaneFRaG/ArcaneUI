using ArcanesGroupFrames.Modules.Experience.Configuration;
using ArcanesGroupFrames.Modules.Experience.UI;

namespace ArcanesGroupFrames.Framework.Configuration.UI
{
    internal sealed partial class ArcaneUiSettingsWindow
    {
        private void RenderExperienceBar()
        {
            switch (_tab)
            {
                case "Appearance":
                    Section("EXPERIENCE BAR APPEARANCE", "Independent colours for normal and Ascension XP.");
                    SliderSetting("XP Bar Width", 180f, 950f,
                        () => ExperienceBarSettings.Width, ExperienceBarSettings.SetWidth, " px");
                    SliderSetting("XP Bar Height", 8f, 48f,
                        () => ExperienceBarSettings.Height, ExperienceBarSettings.SetHeight, " px");
                    SliderSetting("XP Bar Font Size", 9f, 22f,
                        () => ExperienceBarSettings.FontSize, ExperienceBarSettings.SetFontSize, " px");
                    HexSetting("Normal XP Colour", "Bar colour before the level cap.",
                        () => ExperienceBarSettings.NormalHex, v => ExperienceBarSettings.SetColour("NormalHex", v));
                    HexSetting("Ascension XP Colour", "Bar colour at maximum level.",
                        () => ExperienceBarSettings.AscensionHex, v => ExperienceBarSettings.SetColour("AscensionHex", v));
                    HexSetting("XP Background Colour", "Background bar colour.",
                        () => ExperienceBarSettings.BackgroundHex, v => ExperienceBarSettings.SetColour("BackgroundHex", v));
                    return;
                case "Position":
                    Section("EXPERIENCE BAR POSITION", "Screen-centred anchor. Positive Y moves the bar upwards.");
                    SliderSetting("XP Bar X Position", -900f, 900f,
                        () => ExperienceBarSettings.OffsetX, ExperienceBarSettings.SetOffsetX, " px");
                    SliderSetting("XP Bar Y Position", 0f, 750f,
                        () => ExperienceBarSettings.OffsetY, ExperienceBarSettings.SetOffsetY, " px");
                    return;
                default:
                    Section("PLAYER EXPERIENCE", "Normal XP automatically switches to Ascension XP at max level.");
                    ToggleSetting("Enable XP Bar", "Independent HUD bar, unaffected by additional action-bar visibility.",
                        () => ExperienceBarSettings.Enabled, ExperienceBarSettings.SetEnabled);
                    ToggleSetting("Show At Maximum Level", "Keep the bar visible after reaching the level cap to track Ascension XP.",
                        () => ExperienceBarSettings.ShowAtMaxLevel, ExperienceBarSettings.SetShowAtMaxLevel);
                    ToggleSetting("Show XP Label", "Identify whether the displayed progress is normal or Ascension XP.",
                        () => ExperienceBarSettings.ShowText, ExperienceBarSettings.SetShowText);
                    ToggleSetting("Show XP Percentage", "Display percentage when native XP progress data is available.",
                        () => ExperienceBarSettings.ShowPercent, ExperienceBarSettings.SetShowPercent);
                    ToggleSetting("Show XP Numbers", "Display progress numbers when the game provides current and required XP.",
                        () => ExperienceBarSettings.ShowNumbers, ExperienceBarSettings.SetShowNumbers);
                    Info("Ascension XP", "The XP bar remains visible at max level; unsupported game XP values are shown as unavailable, never fabricated.");
                    return;
            }
        }
    }
}
