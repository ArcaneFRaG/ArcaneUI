using UnityEngine;
namespace ArcanesGroupFrames.Modules.Experience.Configuration
{
    internal static class ExperienceBarSettings
    {
        private const string Key = "ArcaneUI.XPBar.";
        internal static bool Enabled { get; private set; }
        internal static bool ShowText { get; private set; }
        internal static bool ShowPercent { get; private set; }
        internal static bool ShowNumbers { get; private set; }
        internal static bool ShowAtMaxLevel { get; private set; }
        internal static float Width { get; private set; }
        internal static float Height { get; private set; }
        internal static float OffsetX { get; private set; }
        internal static float OffsetY { get; private set; }
        internal static int FontSize { get; private set; }
        internal static string NormalHex { get; private set; }
        internal static string AscensionHex { get; private set; }
        internal static string BackgroundHex { get; private set; }
        internal static void Load()
        {
            Enabled = PlayerPrefs.GetInt(Key + "Enabled", 0) != 0;
            ShowText = PlayerPrefs.GetInt(Key + "ShowText", 1) != 0;
            ShowPercent = PlayerPrefs.GetInt(Key + "ShowPercent", 1) != 0;
            ShowNumbers = PlayerPrefs.GetInt(Key + "ShowNumbers", 1) != 0;
            ShowAtMaxLevel = PlayerPrefs.GetInt(Key + "ShowAtMaxLevel", 1) != 0;
            Width = Mathf.Clamp(PlayerPrefs.GetFloat(Key + "Width", 500), 180, 950);
            Height = Mathf.Clamp(PlayerPrefs.GetFloat(Key + "Height", 20), 8, 48);
            OffsetX = Mathf.Clamp(PlayerPrefs.GetFloat(Key + "OffsetX", 0), -900, 900);
            OffsetY = Mathf.Clamp(PlayerPrefs.GetFloat(Key + "OffsetY", 108), 0, 750);
            FontSize = Mathf.Clamp(PlayerPrefs.GetInt(Key + "FontSize", 13), 9, 22);
            NormalHex = ReadHex("NormalHex", "#B99B49");
            AscensionHex = ReadHex("AscensionHex", "#8A58C2");
            BackgroundHex = ReadHex("BackgroundHex", "#151A27");
        }
        private static string ReadHex(string name, string fallback)
        {
            string hex = PlayerPrefs.GetString(Key + name, fallback);
            Color c;
            return ColorUtility.TryParseHtmlString(hex, out c) ? hex : fallback;
        }
        private static void Bool(string name, bool v) { PlayerPrefs.SetInt(Key + name, v ? 1 : 0); PlayerPrefs.Save(); }
        private static void Number(string name, float v) { PlayerPrefs.SetFloat(Key + name, v); PlayerPrefs.Save(); }
        internal static void SetEnabled(bool v) { Enabled = v; Bool("Enabled",v); }
        internal static void SetShowText(bool v) { ShowText = v; Bool("ShowText",v); }
        internal static void SetShowPercent(bool v) { ShowPercent = v; Bool("ShowPercent",v); }
        internal static void SetShowNumbers(bool v) { ShowNumbers = v; Bool("ShowNumbers",v); }
        internal static void SetShowAtMaxLevel(bool v) { ShowAtMaxLevel = v; Bool("ShowAtMaxLevel",v); }
        internal static void SetWidth(float v) { Width = Mathf.Clamp(v,180,950); Number("Width",Width); }
        internal static void SetHeight(float v) { Height = Mathf.Clamp(v,8,48); Number("Height",Height); }
        internal static void SetOffsetX(float v) { OffsetX = Mathf.Clamp(v,-900,900); Number("OffsetX",OffsetX); }
        internal static void SetOffsetY(float v) { OffsetY = Mathf.Clamp(v,0,750); Number("OffsetY",OffsetY); }
        internal static void SetFontSize(float v) { FontSize = Mathf.Clamp(Mathf.RoundToInt(v),9,22); PlayerPrefs.SetInt(Key+"FontSize",FontSize); PlayerPrefs.Save(); }
        internal static void SetColour(string name, string hex)
        {
            Color c;
            if (!ColorUtility.TryParseHtmlString(hex, out c)) return;
            hex = "#" + ColorUtility.ToHtmlStringRGB(c);
            switch(name)
            {
                case "NormalHex": NormalHex = hex; break;
                case "AscensionHex": AscensionHex = hex; break;
                case "BackgroundHex": BackgroundHex = hex; break;
                default: return;
            }
            PlayerPrefs.SetString(Key+name,hex); PlayerPrefs.Save();
        }
    }
}
