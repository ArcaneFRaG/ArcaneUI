using UnityEngine;
using ArcanesGroupFrames;

namespace ArcanesGroupFrames.Modules.UnitFrames.Player
{
    /// <summary>New player-frame preferences; no old Group Frames keys are touched.</summary>
    internal static class PlayerFrameSettings
    {
        private const string Key = "ArcaneUI.PlayerFrame.";
        internal static float Width { get; private set; }
        internal static float Height { get; private set; }
        internal static bool ShowMana { get; private set; }
        internal static bool ShowOnlyInCombat { get; private set; }
        internal static bool ShowCastBar { get; private set; }
        internal static string CastPlacement { get; private set; }
        internal static string CastColourHex { get; private set; }
        internal static float CastOffsetX { get; private set; }
        internal static float CastOffsetY { get; private set; }
        internal static bool ShowNumbers { get; private set; }
        internal static bool ShowPercent { get; private set; }
        internal static bool ShowManaPercent { get; private set; }
        internal static bool ShowLevel { get; private set; }
        internal static bool ShowClass { get; private set; }
        internal static bool UseClassColours { get; private set; }
        internal static bool InheritTheme { get; private set; }
        internal static string Texture { get; private set; }
        internal static string Font { get; private set; }
        internal static string HealthHex { get; private set; }
        internal static string ManaHex { get; private set; }
        internal static string BackgroundHex { get; private set; }
        internal static Vector2 Position { get; private set; }

        internal static void Load()
        {
            Width = Mathf.Clamp(PlayerPrefs.GetFloat(Key + "Width", 250f), 130f, 450f);
            Height = Mathf.Clamp(PlayerPrefs.GetFloat(Key + "Height", 70f), 42f, 130f);
            ShowMana = PlayerPrefs.GetInt(Key + "ShowMana", 1) != 0;
            ShowOnlyInCombat = PlayerPrefs.GetInt(Key + "OnlyCombat", 0) != 0;
            ShowCastBar = PlayerPrefs.GetInt(Key + "ShowCastBar", 1) != 0;
            CastPlacement = PlayerPrefs.GetString(Key + "CastPlacement", "Overlay Mana");
            CastColourHex = PlayerPrefs.GetString(Key + "CastColour", "");
            CastOffsetX = Mathf.Clamp(PlayerPrefs.GetFloat(Key + "CastOffsetX", 0f), -250f, 250f);
            CastOffsetY = Mathf.Clamp(PlayerPrefs.GetFloat(Key + "CastOffsetY", 0f), -250f, 250f);
            ShowNumbers = PlayerPrefs.GetInt(Key + "ShowNumbers", 1) != 0;
            ShowPercent = PlayerPrefs.GetInt(Key + "ShowPercent", 1) != 0;
            ShowManaPercent = PlayerPrefs.GetInt(Key + "ShowManaPercent", 1) != 0;
            ShowLevel = PlayerPrefs.GetInt(Key + "ShowLevel", 1) != 0;
            ShowClass = PlayerPrefs.GetInt(Key + "ShowClass", 1) != 0;
            UseClassColours = PlayerPrefs.GetInt(Key + "UseClassColours", 0) != 0;
            InheritTheme = PlayerPrefs.GetInt(Key + "InheritTheme", 1) != 0;
            Texture = PlayerPrefs.GetString(Key + "Texture", "");
            Font = PlayerPrefs.GetString(Key + "Font", "");
            HealthHex = PlayerPrefs.GetString(Key + "Health", "#5B9B84");
            ManaHex = PlayerPrefs.GetString(Key + "Mana", "#517DB5");
            BackgroundHex = PlayerPrefs.GetString(Key + "Background", "#192329");
            Position = new Vector2(PlayerPrefs.GetFloat(Key + "X", 35f), PlayerPrefs.GetFloat(Key + "Y", -260f));
        }
        internal static void SetSize(float w, float h)
        {
            Width = Mathf.Clamp(w, 130f, 450f);
            Height = Mathf.Clamp(h, 42f, 130f);
            PlayerPrefs.SetFloat(Key + "Width", Width);
            PlayerPrefs.SetFloat(Key + "Height", Height);
            PlayerPrefs.Save();
        }
        internal static void SetPosition(Vector2 pos)
        {
            Position = pos;
            PlayerPrefs.SetFloat(Key + "X", pos.x);
            PlayerPrefs.SetFloat(Key + "Y", pos.y);
            PlayerPrefs.Save();
        }
        internal static void SetShowOnlyInCombat(bool v) { ShowOnlyInCombat = v; SetBool("OnlyCombat", v); }
        internal static void SetShowCastBar(bool v) { ShowCastBar = v; SetBool("ShowCastBar", v); }
        internal static void CycleCastPlacement()
        {
            CastPlacement = CastPlacement == "Overlay Mana" ? "Above Health" :
                CastPlacement == "Above Health" ? "Below Frame" :
                CastPlacement == "Below Frame" ? "Detached" : "Overlay Mana";
            PlayerPrefs.SetString(Key + "CastPlacement", CastPlacement);
            PlayerPrefs.Save();
        }
        internal static void SetCastColour(string value)
        {
            Color colour;
            if (!UnitFrameAppearance.TryParseColor(value, out colour)) return;
            CastColourHex = "#" + ColorUtility.ToHtmlStringRGB(colour);
            PlayerPrefs.SetString(Key + "CastColour", CastColourHex);
            PlayerPrefs.Save();
        }
        internal static void ResetCastColour()
        {
            CastColourHex = "";
            PlayerPrefs.DeleteKey(Key + "CastColour");
            PlayerPrefs.Save();
        }
        internal static void SetCastOffsetX(float v)
        {
            CastOffsetX = Mathf.Clamp(v, -250f, 250f);
            PlayerPrefs.SetFloat(Key + "CastOffsetX", CastOffsetX);
            PlayerPrefs.Save();
        }
        internal static void SetCastOffsetY(float v)
        {
            CastOffsetY = Mathf.Clamp(v, -250f, 250f);
            PlayerPrefs.SetFloat(Key + "CastOffsetY", CastOffsetY);
            PlayerPrefs.Save();
        }
        internal static void SetShowMana(bool value) { ShowMana = value; SetBool("ShowMana", value); }
        internal static void SetShowNumbers(bool value) { ShowNumbers = value; SetBool("ShowNumbers", value); }
        internal static void SetShowPercent(bool value) { ShowPercent = value; SetBool("ShowPercent", value); }
        internal static void SetShowManaPercent(bool value) { ShowManaPercent = value; SetBool("ShowManaPercent", value); }
        internal static void SetShowLevel(bool value) { ShowLevel = value; SetBool("ShowLevel", value); }
        internal static void SetShowClass(bool value) { ShowClass = value; SetBool("ShowClass", value); }
        internal static void SetUseClassColours(bool value) { UseClassColours = value; SetBool("UseClassColours", value); }
        internal static void SetInheritTheme(bool value) { InheritTheme = value; SetBool("InheritTheme", value); }
        internal static void SetTexture(string value)
        {
            Texture = value ?? "";
            PlayerPrefs.SetString(Key + "Texture", Texture);
            PlayerPrefs.Save();
        }
        internal static void SetFont(string value)
        {
            Font = value ?? "";
            PlayerPrefs.SetString(Key + "Font", Font);
            PlayerPrefs.Save();
        }
        internal static void SetHealthHex(string value) { SetHex("Health", value); }
        internal static void SetManaHex(string value) { SetHex("Mana", value); }
        internal static void SetBackgroundHex(string value) { SetHex("Background", value); }
        private static void SetHex(string channel, string value)
        {
            Color parsed;
            if (!UnitFrameAppearance.TryParseColor(value, out parsed)) return;
            string normalized = "#" + ColorUtility.ToHtmlStringRGB(parsed);
            if (channel == "Health") HealthHex = normalized;
            else if (channel == "Mana") ManaHex = normalized;
            else if (channel == "Background") BackgroundHex = normalized;
            else return;
            PlayerPrefs.SetString(Key + channel, normalized);
            PlayerPrefs.Save();
        }
        private static void SetBool(string key, bool value)
        {
            PlayerPrefs.SetInt(Key + key, value ? 1 : 0);
            PlayerPrefs.Save();
        }
    }
}
