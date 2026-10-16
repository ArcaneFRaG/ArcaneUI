using UnityEngine;

namespace ArcanesGroupFrames.Modules.CharacterSheet.Configuration
{
    // Presentation-only preferences. The native equipment, inventory, tooltip,
    // and character stat components remain owned by Erenshor.
    internal static class CharacterSheetSettings
    {
        private const string Key = "ArcaneUI.CharacterSheet.";

        internal static readonly string[] LayoutPresets = { "Classic Split", "Wide Stats", "Equipment Focus" };
        internal static readonly string[] VisualThemes = { "Arcane Dark", "Refined Fantasy", "Minimal Clean" };
        internal static readonly string[] HeaderAlignments = { "Centre", "Left" };
        internal static readonly string[] WardrobeLayouts = { "Compact Grid", "Gallery", "Minimal Slots" };
        internal static string WardrobeLayout { get; private set; }
        internal static float AscensionNodeSize { get; private set; }
        internal static float AscensionLinkWidth { get; private set; }
        internal static bool AscensionShowLabels { get; private set; }
        internal static bool AscensionHoverInspect { get; private set; }
        internal static string LayoutPreset { get; private set; }
        internal static string VisualTheme { get; private set; }
        internal static string HeaderAlignment { get; private set; }
        internal static float StatFontSize { get; private set; }
        internal static bool SectionDividers { get; private set; }
        internal static bool UseCustomPalette { get; private set; }
        internal static bool InheritTheme { get; private set; }
        internal static bool ShowOverview { get; private set; }
        internal static bool SafeNativeMode { get; private set; }
        internal static bool ShowModelPreview { get; private set; }
        internal static bool ReflowNativeEquipment { get; private set; }
        internal static bool ReflowNativeTabs { get; private set; }
        internal static float OverviewWidth { get; private set; }
        internal static float OverviewScale { get; private set; }
        internal static bool DrawFrame { get; private set; }
        internal static bool DrawHeader { get; private set; }
        internal static bool FrameEquipment { get; private set; }
        internal static bool TintNativeBackground { get; private set; }
        internal static float BackgroundOpacity { get; private set; }
        internal static float NativeTintStrength { get; private set; }
        internal static float BorderThickness { get; private set; }
        internal static float SlotThickness { get; private set; }
        internal static float HeaderHeight { get; private set; }
        internal static string AccentHex { get; private set; }
        internal static string BackgroundHex { get; private set; }
        internal static string TextHex { get; private set; }
        internal static string NativePath { get; private set; }

        internal static void Load()
        {
            LayoutPreset = SafeChoice(PlayerPrefs.GetString(Key + "LayoutPreset", "Wide Stats"), LayoutPresets);
            VisualTheme = SafeChoice(PlayerPrefs.GetString(Key + "VisualTheme", "Arcane Dark"), VisualThemes);
            HeaderAlignment = SafeChoice(PlayerPrefs.GetString(Key + "HeaderAlignment", "Centre"), HeaderAlignments);
            WardrobeLayout = SafeChoice(PlayerPrefs.GetString(Key + "WardrobeLayout", "Gallery"), WardrobeLayouts);
            AscensionNodeSize = Mathf.Clamp(PlayerPrefs.GetFloat(Key + "AscensionNodeSize", 30f), 24f, 38f);
            AscensionLinkWidth = Mathf.Clamp(PlayerPrefs.GetFloat(Key + "AscensionLinkWidth", 1.2f), .6f, 3f);
            AscensionShowLabels = PlayerPrefs.GetInt(Key + "AscensionShowLabels", 1) != 0;
            AscensionHoverInspect = PlayerPrefs.GetInt(Key + "AscensionHoverInspect", 1) != 0;
            StatFontSize = Mathf.Clamp(PlayerPrefs.GetFloat(Key + "StatFontSize", 13f), 11f, 15f);
            SectionDividers = PlayerPrefs.GetInt(Key + "SectionDividers", 1) != 0;
            UseCustomPalette = PlayerPrefs.GetInt(Key + "UseCustomPalette", 0) != 0;
            InheritTheme = PlayerPrefs.GetInt(Key + "InheritTheme", 0) != 0;
            // The old companion covered native equipment and sometimes appeared
            // above dialogs. Migrate the unsafe default off once; users can
            // still explicitly opt into it later for diagnostics.
            if (PlayerPrefs.GetInt(Key + "NativeSafeDisplayMigration", 0) == 0)
            {
                PlayerPrefs.SetInt(Key + "ShowOverview", 0);
                PlayerPrefs.SetInt(Key + "NativeSafeDisplayMigration", 1);
                PlayerPrefs.Save();
            }
            // 10.5L intentionally disabled the only ArcaneUI character panel.
            // Restore the optional native-safe character information display.
            // Keep the user in control after this one-time upgrade.
            if (PlayerPrefs.GetInt(Key + "RestoreVisibleCompanionMigration", 0) == 0)
            {
                PlayerPrefs.SetInt(Key + "ShowOverview", 1);
                PlayerPrefs.SetInt(Key + "RestoreVisibleCompanionMigration", 1);
                PlayerPrefs.Save();
            }
            ShowOverview = PlayerPrefs.GetInt(Key + "ShowOverview", 1) != 0;
            // Force native-first behaviour on upgrading older, broken visual-only
            // layouts. An explicit opt-in is required to test experimental UI.
            if (PlayerPrefs.GetInt(Key + "NativeBoundsMigration", 0) == 0)
            {
                PlayerPrefs.SetInt(Key + "SafeNativeMode", 0);
                PlayerPrefs.SetInt(Key + "ShowOverview", 1);
                PlayerPrefs.SetInt(Key + "NativeBoundsMigration", 1);
                PlayerPrefs.Save();
            }
            SafeNativeMode = PlayerPrefs.GetInt(Key + "SafeNativeMode", 0) != 0;
            ShowModelPreview = PlayerPrefs.GetInt(Key + "ShowModelPreview", 1) != 0;
            ReflowNativeEquipment = PlayerPrefs.GetInt(Key + "ReflowNativeEquipment", 1) != 0;
            ReflowNativeTabs = PlayerPrefs.GetInt(Key + "ReflowNativeTabs", 1) != 0;
            OverviewWidth = Mathf.Clamp(PlayerPrefs.GetFloat(Key + "OverviewWidth", 420f), 360f, 530f);
            OverviewScale = Mathf.Clamp(PlayerPrefs.GetFloat(Key + "OverviewScale", 1f), .65f, 1.25f);
            DrawFrame = PlayerPrefs.GetInt(Key + "DrawFrame", 1) != 0;
            DrawHeader = PlayerPrefs.GetInt(Key + "DrawHeader", 0) != 0;
            FrameEquipment = PlayerPrefs.GetInt(Key + "FrameEquipment", 1) != 0;
            TintNativeBackground = PlayerPrefs.GetInt(Key + "TintNativeBackground", 0) != 0;
            BackgroundOpacity = Mathf.Clamp(PlayerPrefs.GetFloat(Key + "BackgroundOpacity", 65f), 0f, 100f);
            NativeTintStrength = Mathf.Clamp(PlayerPrefs.GetFloat(Key + "NativeTintStrength", 35f), 0f, 100f);
            BorderThickness = Mathf.Clamp(PlayerPrefs.GetFloat(Key + "BorderThickness", 2f), 0f, 8f);
            SlotThickness = Mathf.Clamp(PlayerPrefs.GetFloat(Key + "SlotThickness", 2f), 0f, 6f);
            HeaderHeight = Mathf.Clamp(PlayerPrefs.GetFloat(Key + "HeaderHeight", 30f), 12f, 65f);
            AccentHex = PlayerPrefs.GetString(Key + "AccentHex", "#CEAC6B");
            BackgroundHex = PlayerPrefs.GetString(Key + "BackgroundHex", "#1D2231");
            TextHex = PlayerPrefs.GetString(Key + "TextHex", "#EDE6D5");
            NativePath = PlayerPrefs.GetString(Key + "NativePath", "");
        }

        internal static void SetBool(string name, bool value)
        {
            switch (name)
            {
                case "SectionDividers": SectionDividers = value; break;
                case "AscensionShowLabels": AscensionShowLabels = value; break;
                case "AscensionHoverInspect": AscensionHoverInspect = value; break;
                case "UseCustomPalette": UseCustomPalette = value; break;
                case "InheritTheme": InheritTheme = value; break;
                case "ShowOverview": ShowOverview = value; break;
                case "SafeNativeMode": SafeNativeMode = value; break;
                case "ShowModelPreview": ShowModelPreview = value; break;
                case "ReflowNativeEquipment": ReflowNativeEquipment = value; break;
                case "ReflowNativeTabs": ReflowNativeTabs = value; break;
                case "DrawFrame": DrawFrame = value; break;
                case "DrawHeader": DrawHeader = value; break;
                case "FrameEquipment": FrameEquipment = value; break;
                case "TintNativeBackground": TintNativeBackground = value; break;
                default: return;
            }
            PlayerPrefs.SetInt(Key + name, value ? 1 : 0);
            PlayerPrefs.Save();
        }

        internal static void SetFloat(string name, float value)
        {
            switch (name)
            {
                case "StatFontSize": StatFontSize = Mathf.Clamp(value, 11f, 15f); value = StatFontSize; break;
                case "AscensionNodeSize": AscensionNodeSize = Mathf.Clamp(value, 24f, 38f); value = AscensionNodeSize; break;
                case "AscensionLinkWidth": AscensionLinkWidth = Mathf.Clamp(value, .6f, 3f); value = AscensionLinkWidth; break;
                case "OverviewWidth": OverviewWidth = Mathf.Clamp(value, 360f, 530f); value = OverviewWidth; break;
                case "OverviewScale": OverviewScale = Mathf.Clamp(value, .65f, 1.25f); value = OverviewScale; break;
                case "BackgroundOpacity": BackgroundOpacity = Mathf.Clamp(value, 0f, 100f); value = BackgroundOpacity; break;
                case "NativeTintStrength": NativeTintStrength = Mathf.Clamp(value, 0f, 100f); value = NativeTintStrength; break;
                case "BorderThickness": BorderThickness = Mathf.Clamp(value, 0f, 8f); value = BorderThickness; break;
                case "SlotThickness": SlotThickness = Mathf.Clamp(value, 0f, 6f); value = SlotThickness; break;
                case "HeaderHeight": HeaderHeight = Mathf.Clamp(value, 12f, 65f); value = HeaderHeight; break;
                default: return;
            }
            PlayerPrefs.SetFloat(Key + name, value);
            PlayerPrefs.Save();
        }

        private static string SafeChoice(string proposed, string[] choices)
        {
            foreach (string candidate in choices)
                if (string.Equals(candidate, proposed, System.StringComparison.Ordinal)) return candidate;
            return choices[0];
        }

        internal static void SetChoice(string name, string selected)
        {
            switch (name)
            {
                case "LayoutPreset": LayoutPreset = SafeChoice(selected, LayoutPresets); selected = LayoutPreset; break;
                case "VisualTheme": VisualTheme = SafeChoice(selected, VisualThemes); selected = VisualTheme; break;
                case "HeaderAlignment": HeaderAlignment = SafeChoice(selected, HeaderAlignments); selected = HeaderAlignment; break;
                case "WardrobeLayout": WardrobeLayout = SafeChoice(selected, WardrobeLayouts); selected = WardrobeLayout; break;
                default: return;
            }
            PlayerPrefs.SetString(Key + name, selected);
            PlayerPrefs.Save();
        }

        internal static float EquipmentWidth
        {
            get
            {
                if (LayoutPreset == "Classic Split") return .42f;
                if (LayoutPreset == "Equipment Focus") return .46f;
                return .37f; // Wide Stats: roomy stats + genuine 64-pixel native slots
            }
        }

        internal static void SetHex(string name, string hex)
        {
            Color color;
            if (!ColorUtility.TryParseHtmlString(hex, out color)) return;
            string normalized = "#" + ColorUtility.ToHtmlStringRGB(color);
            switch (name)
            {
                case "AccentHex": AccentHex = normalized; break;
                case "BackgroundHex": BackgroundHex = normalized; break;
                case "TextHex": TextHex = normalized; break;
                default: return;
            }
            PlayerPrefs.SetString(Key + name, normalized);
            PlayerPrefs.Save();
        }

        internal static void SetNativePath(string path)
        {
            NativePath = path ?? "";
            PlayerPrefs.SetString(Key + "NativePath", NativePath);
            PlayerPrefs.Save();
        }

        internal static Color Parse(string hex, Color fallback)
        {
            Color parsed;
            return ColorUtility.TryParseHtmlString(hex, out parsed) ? parsed : fallback;
        }
    }
}
