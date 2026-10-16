using UnityEngine;

namespace ArcanesGroupFrames.Framework.Theming
{
    public sealed class UiTheme
    {
        public string Id;
        public string DisplayName;
        // The Blank/Custom preset does not force any of its palette or
        // typography values onto gameplay frames. It leaves the original
        // per-module appearance settings authoritative.
        public bool IsBlank;
        public Color Background;
        public Color Panel;
        public Color Border;
        public Color Text;
        public Color Accent;
        // Explicit bar palette: class colour is an optional module override,
        // not the default when using a shared theme.
        public Color Health;
        public Color MissingHealth;
        public Color Mana;
        public float BorderSize = 1f;
        public string BarTexture = "Flat";
        public string FontName = "Default";

        // Widget skinning for the settings UI and shared preview surfaces.
        public Color HeaderPanel;
        public Color HeaderAccent;
        public Color TabInactive;
        public Color TabActive;
        public Color TabIcon;
        public Color WidgetPanel;
        public Color WidgetBorder;
        public Color InputBackground;
        public Color InputText;
        public Color ButtonFill;
        public Color ButtonActiveFill;
        public Color ButtonText;
        public Color ToggleOn;
        public Color ToggleOff;
        public Color DropdownFill;
        public Color SliderTrack;
        public Color SliderFill;
        public Color SliderHandle;
        public Color Ornament;
        public Color SecondaryAccent;
        public Color Danger;
        public Color Success;

    }
}
