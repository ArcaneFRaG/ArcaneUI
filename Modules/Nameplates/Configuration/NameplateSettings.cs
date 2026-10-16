using UnityEngine;

namespace ArcanesGroupFrames.Modules.Nameplates.Configuration
{
    /// <summary>Saved presentation-only controls for ArcaneUI's overhead bars.</summary>
    internal static class NameplateSettings
    {
        private const string Key = "ArcaneUI.Nameplates.";
        private static bool _enabled, _hideNative;
        private static bool _showNpcs = true, _showSims = true, _showFriendly = true;
        private static bool _showNames = true, _showHealthPercent, _simClassColours = true, _targetOutline = true, _showNpcTags = true;
        private static float _distance = 35f, _width = 150f, _height = 13f, _offset;
        private static int _direction, _nameFontSize = 12, _npcTagFontSize = 11, _percentFontSize = 11, _maxNameChars = 32;
        private static int _backgroundOpacity = 88, _targetScale = 110, _targetOutlineSize = 2;
        private static string _nativeRelativePath = "";
        private static string _hostileColour = "#B54848", _friendlyColour = "#31C252", _simColour = "#448BC4";
        private static string _targetColour = "#F3C45A", _nameColour = "#EFE6D1", _npcTagColour = "#D6C296";

        internal static bool Enabled { get { return _enabled; } }
        internal static bool HideNative { get { return _hideNative; } }
        internal static bool ShowNpcs { get { return _showNpcs; } }
        internal static bool ShowSims { get { return _showSims; } }
        internal static bool ShowFriendly { get { return _showFriendly; } }
        internal static bool ShowNames { get { return _showNames; } }
        internal static bool ShowNpcTags { get { return _showNpcTags; } }
        internal static int NpcTagFontSize { get { return _npcTagFontSize; } }
        internal static string NpcTagColour { get { return _npcTagColour; } }
        internal static bool ShowHealthPercent { get { return _showHealthPercent; } }
        internal static bool SimClassColours { get { return _simClassColours; } }
        internal static bool ShowTargetOutline { get { return _targetOutline; } }
        internal static float MaxDistance { get { return _distance; } }
        internal static float Width { get { return _width; } }
        internal static float Height { get { return _height; } }
        internal static float HeightOffset { get { return _offset; } }
        internal static int HealthDirection { get { return _direction; } }
        internal static int NameFontSize { get { return _nameFontSize; } }
        internal static int PercentFontSize { get { return _percentFontSize; } }
        internal static int NameCharacterLimit { get { return _maxNameChars; } }
        internal static int BackgroundOpacity { get { return _backgroundOpacity; } }
        internal static int TargetScale { get { return _targetScale; } }
        internal static int TargetOutlineSize { get { return _targetOutlineSize; } }
        internal static string HostileColour { get { return _hostileColour; } }
        internal static string FriendlyColour { get { return _friendlyColour; } }
        internal static string SimColour { get { return _simColour; } }
        internal static string TargetColour { get { return _targetColour; } }
        internal static string NameColour { get { return _nameColour; } }
        internal static string NativeRelativePath { get { return _nativeRelativePath; } }
        internal static string HealthDirectionName
        {
            get
            {
                switch (_direction)
                {
                    case 1: return "Right to Left";
                    case 2: return "Bottom to Top";
                    case 3: return "Top to Bottom";
                    default: return "Left to Right";
                }
            }
        }

        internal static void Load()
        {
            _enabled = PlayerPrefs.GetInt(Key + "Enabled", 0) != 0;
            // Upgrade the old opt-in exact-child suppressor to automatic
            // owner-scoped hiding. Users may still disable this in settings.
            if (PlayerPrefs.GetInt(Key + "OwnedOverheadMigration", 0) == 0)
            {
                PlayerPrefs.SetInt(Key + "HideNative", 1);
                PlayerPrefs.SetInt(Key + "OwnedOverheadMigration", 1);
                PlayerPrefs.Save();
            }
            _hideNative = PlayerPrefs.GetInt(Key + "HideNative", 1) != 0;
            _showNpcs = PlayerPrefs.GetInt(Key + "Npcs", 1) != 0;
            _showSims = PlayerPrefs.GetInt(Key + "Sims", 1) != 0;
            _showFriendly = PlayerPrefs.GetInt(Key + "Friendly", 1) != 0;
            _showNames = PlayerPrefs.GetInt(Key + "Names", 1) != 0;
            _showNpcTags = PlayerPrefs.GetInt(Key + "NpcTags", 1) != 0;
            _npcTagFontSize = Mathf.Clamp(PlayerPrefs.GetInt(Key + "NpcTagFontSize", 11), 8, 22);
            _showHealthPercent = PlayerPrefs.GetInt(Key + "HealthPercent", 0) != 0;
            _simClassColours = PlayerPrefs.GetInt(Key + "SimClassColours", 1) != 0;
            _targetOutline = PlayerPrefs.GetInt(Key + "TargetOutline", 1) != 0;
            _distance = Mathf.Clamp(PlayerPrefs.GetFloat(Key + "Distance", 35f), 10f, 80f);
            _width = Mathf.Clamp(PlayerPrefs.GetFloat(Key + "Width", 150f), 85f, 280f);
            _height = Mathf.Clamp(PlayerPrefs.GetFloat(Key + "Height", 13f), 8f, 32f);
            _offset = Mathf.Clamp(PlayerPrefs.GetFloat(Key + "Offset", 0f), -1f, 2f);
            _direction = Mathf.Clamp(PlayerPrefs.GetInt(Key + "HealthDirection", 0), 0, 3);
            _nameFontSize = Mathf.Clamp(PlayerPrefs.GetInt(Key + "NameFontSize", 12), 8, 24);
            _percentFontSize = Mathf.Clamp(PlayerPrefs.GetInt(Key + "PercentFontSize", 11), 8, 24);
            _maxNameChars = Mathf.Clamp(PlayerPrefs.GetInt(Key + "NameLimit", 32), 8, 60);
            _backgroundOpacity = Mathf.Clamp(PlayerPrefs.GetInt(Key + "BackgroundOpacity", 88), 0, 100);
            _targetScale = Mathf.Clamp(PlayerPrefs.GetInt(Key + "TargetScale", 110), 100, 150);
            _targetOutlineSize = Mathf.Clamp(PlayerPrefs.GetInt(Key + "TargetOutlineSize", 2), 1, 5);
            _hostileColour = Colour(Key + "HostileColour", "#B54848");
            _friendlyColour = Colour(Key + "FriendlyColour", "#31C252");
            _simColour = Colour(Key + "SimColour", "#448BC4");
            _targetColour = Colour(Key + "TargetColour", "#F3C45A");
            _nameColour = Colour(Key + "NameColour", "#EFE6D1");
            _npcTagColour = Colour(Key + "NpcTagColour", "#D6C296");
            _nativeRelativePath = PlayerPrefs.GetString(Key + "NativePath", "");
        }
        private static string Colour(string name, string fallback)
        {
            string v = PlayerPrefs.GetString(name, fallback);
            Color parsed;
            return ColorUtility.TryParseHtmlString(v, out parsed) ? v : fallback;
        }
        private static void SaveBool(string name, bool v) { PlayerPrefs.SetInt(Key + name, v ? 1 : 0); PlayerPrefs.Save(); }
        private static void SaveInt(string name, int v) { PlayerPrefs.SetInt(Key + name, v); PlayerPrefs.Save(); }
        private static void SaveFloat(string name, float v) { PlayerPrefs.SetFloat(Key + name, v); PlayerPrefs.Save(); }
        private static void SaveColour(string name, string value, ref string field)
        {
            Color parsed;
            if (!ColorUtility.TryParseHtmlString(value, out parsed)) return;
            field = value;
            PlayerPrefs.SetString(Key + name, value);
            PlayerPrefs.Save();
        }
        internal static void SetEnabled(bool v) { _enabled = v; SaveBool("Enabled", v); }
        internal static void SetHideNative(bool v) { _hideNative = v; SaveBool("HideNative", v); }
        internal static void SetShowNpcs(bool v) { _showNpcs = v; SaveBool("Npcs", v); }
        internal static void SetShowSims(bool v) { _showSims = v; SaveBool("Sims", v); }
        internal static void SetShowFriendly(bool v) { _showFriendly = v; SaveBool("Friendly", v); }
        internal static void SetShowNames(bool v) { _showNames = v; SaveBool("Names", v); }
        internal static void SetShowNpcTags(bool v) { _showNpcTags = v; SaveBool("NpcTags", v); }
        internal static void SetNpcTagFontSize(float v) { _npcTagFontSize = Mathf.Clamp(Mathf.RoundToInt(v), 8, 22); SaveInt("NpcTagFontSize", _npcTagFontSize); }
        internal static void SetNpcTagColour(string v) { SaveColour("NpcTagColour", v, ref _npcTagColour); }
        internal static void SetShowHealthPercent(bool v) { _showHealthPercent = v; SaveBool("HealthPercent", v); }
        internal static void SetSimClassColours(bool v) { _simClassColours = v; SaveBool("SimClassColours", v); }
        internal static void SetShowTargetOutline(bool v) { _targetOutline = v; SaveBool("TargetOutline", v); }
        internal static void SetMaxDistance(float v) { _distance = Mathf.Clamp(v, 10f, 80f); SaveFloat("Distance", _distance); }
        internal static void SetWidth(float v) { _width = Mathf.Clamp(v, 85f, 280f); SaveFloat("Width", _width); }
        internal static void SetHeight(float v) { _height = Mathf.Clamp(v, 8f, 32f); SaveFloat("Height", _height); }
        internal static void SetHeightOffset(float v) { _offset = Mathf.Clamp(v, -1f, 2f); SaveFloat("Offset", _offset); }
        internal static void CycleHealthDirection() { _direction = (_direction + 1) % 4; SaveInt("HealthDirection", _direction); }
        internal static void SetNameFontSize(float v) { _nameFontSize = Mathf.Clamp(Mathf.RoundToInt(v), 8, 24); SaveInt("NameFontSize", _nameFontSize); }
        internal static void SetPercentFontSize(float v) { _percentFontSize = Mathf.Clamp(Mathf.RoundToInt(v), 8, 24); SaveInt("PercentFontSize", _percentFontSize); }
        internal static void SetNameCharacterLimit(float v) { _maxNameChars = Mathf.Clamp(Mathf.RoundToInt(v), 8, 60); SaveInt("NameLimit", _maxNameChars); }
        internal static void SetBackgroundOpacity(float v) { _backgroundOpacity = Mathf.Clamp(Mathf.RoundToInt(v), 0, 100); SaveInt("BackgroundOpacity", _backgroundOpacity); }
        internal static void SetTargetScale(float v) { _targetScale = Mathf.Clamp(Mathf.RoundToInt(v), 100, 150); SaveInt("TargetScale", _targetScale); }
        internal static void SetTargetOutlineSize(float v) { _targetOutlineSize = Mathf.Clamp(Mathf.RoundToInt(v), 1, 5); SaveInt("TargetOutlineSize", _targetOutlineSize); }
        internal static void SetHostileColour(string v) { SaveColour("HostileColour", v, ref _hostileColour); }
        internal static void SetFriendlyColour(string v) { SaveColour("FriendlyColour", v, ref _friendlyColour); }
        internal static void SetSimColour(string v) { SaveColour("SimColour", v, ref _simColour); }
        internal static void SetTargetColour(string v) { SaveColour("TargetColour", v, ref _targetColour); }
        internal static void SetNameColour(string v) { SaveColour("NameColour", v, ref _nameColour); }
        internal static void SetNativeRelativePath(string value)
        {
            _nativeRelativePath = value ?? "";
            PlayerPrefs.SetString(Key + "NativePath", _nativeRelativePath);
            PlayerPrefs.Save();
        }
    }
}
