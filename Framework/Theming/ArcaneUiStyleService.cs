using System;
using UnityEngine;
using ArcanesGroupFrames.Framework.Modules;
using ArcanesGroupFrames;

namespace ArcanesGroupFrames.Framework.Theming
{
    // Shared presentation contract. Native data, click casting, and raid
    // assignment logic never pass through this layer.
    internal struct ArcaneUiFrameStyle
    {
        internal bool Inherit;
        internal Color Border;
        internal Color Background;
        internal Color ResourceBackground;
        internal Color Text;
        internal Color Health;
        internal Color MissingHealth;
        internal Color Mana;
        internal string BarTexture;
        internal string FontName;
        internal int BorderSize;
    }

    internal static class ArcaneUiStyleService
    {
        private const string Prefix = "ArcaneUI.Style.";
        private static ThemeManager _themes;
        private static bool _partyInherit;
        private static bool _raidInherit;
        private static bool _managerInherit;
        private static bool _partyThemeClassColors;
        private static bool _raidThemeClassColors;
        private static bool _blankPaletteEnabled;
        // Explicit module selections win over global textures/fonts. Empty
        // means follow the selected global preset (or local settings in Blank).
        private static string _partyTextureOverride;
        private static string _raidTextureOverride;
        private static string _partyFontOverride;
        private static string _raidFontOverride;
        private static string _barTexture;
        private static string _fontName;
        private static int _borderSize;
        private static float _opacity;
        internal static event Action Changed;

        internal static void Initialize(ThemeManager themes)
        {
            if (_themes != null) _themes.ThemeChanged -= OnThemeChanged;
            _themes = themes;
            // Opt in to inheritance by default. Existing per-frame settings are
            // never overwritten and are restored simply by disabling inheritance.
            _partyInherit = PlayerPrefs.GetInt(Prefix + "Party.Inherit", 1) != 0;
            _raidInherit = PlayerPrefs.GetInt(Prefix + "Raid.Inherit", 1) != 0;
            _managerInherit = PlayerPrefs.GetInt(Prefix + "Manager.Inherit", 1) != 0;
            // Opt-in only: enabling a theme must replace the legacy class
            // health colours, including for upgraded installations.
            _partyThemeClassColors = PlayerPrefs.GetInt(Prefix + "Party.ThemeClassColors", 0) != 0;
            _raidThemeClassColors = PlayerPrefs.GetInt(Prefix + "Raid.ThemeClassColors", 0) != 0;
            _blankPaletteEnabled = PlayerPrefs.GetInt(Prefix + "Custom.ApplyPalette", 0) != 0;
            LoadCustomPalette();
            _partyTextureOverride = PlayerPrefs.GetString(Prefix + "Party.TextureOverride", "");
            _raidTextureOverride = PlayerPrefs.GetString(Prefix + "Raid.TextureOverride", "");
            _partyFontOverride = PlayerPrefs.GetString(Prefix + "Party.FontOverride", "");
            _raidFontOverride = PlayerPrefs.GetString(Prefix + "Raid.FontOverride", "");
            _barTexture = PlayerPrefs.GetString(Prefix + "Global.BarTexture", "Theme Default");
            _fontName = PlayerPrefs.GetString(Prefix + "Global.Font", "Theme Default");
            _borderSize = Mathf.Clamp(PlayerPrefs.GetInt(Prefix + "Global.BorderSize", 1), 1, 3);
            _opacity = Mathf.Clamp(PlayerPrefs.GetFloat(Prefix + "Global.Opacity", 1f), .3f, 1f);
            if (_themes != null) _themes.ThemeChanged += OnThemeChanged;
        }

        internal static void Shutdown()
        {
            if (_themes != null) _themes.ThemeChanged -= OnThemeChanged;
            _themes = null;
            Changed = null;
        }

        private static void OnThemeChanged(UiTheme ignored) { RaiseChanged(); }
        private static void RaiseChanged() { var h = Changed; if (h != null) h(); }
        internal static void NotifyVisualChanged() { RaiseChanged(); }
        internal static bool Inherits(string module)
        {
            if (module == ArcaneUiModuleSettings.Party) return _partyInherit;
            if (module == ArcaneUiModuleSettings.Raid) return _raidInherit;
            if (module == ArcaneUiModuleSettings.Manager) return _managerInherit;
            if (module == ArcaneUiModuleSettings.Player) return ArcanesGroupFrames.Modules.UnitFrames.Player.PlayerFrameSettings.InheritTheme;
            if (module == ArcaneUiModuleSettings.Target) return ArcanesGroupFrames.Modules.UnitFrames.Target.TargetFrameSettings.InheritTheme;
            return false;
        }
        internal static void SetInherits(string module, bool value)
        {
            if (Inherits(module) == value) return;
            string key;
            if (module == ArcaneUiModuleSettings.Party) { _partyInherit = value; key = "Party.Inherit"; }
            else if (module == ArcaneUiModuleSettings.Raid) { _raidInherit = value; key = "Raid.Inherit"; }
            else if (module == ArcaneUiModuleSettings.Manager) { _managerInherit = value; key = "Manager.Inherit"; }
            else return;
            PlayerPrefs.SetInt(Prefix + key, value ? 1 : 0);
            PlayerPrefs.Save();
            RaiseChanged();
        }
        internal static bool ThemeClassColors(string module)
        {
            if (module == ArcaneUiModuleSettings.Party) return _partyThemeClassColors;
            if (module == ArcaneUiModuleSettings.Raid) return _raidThemeClassColors;
            return false;
        }
        internal static void SetThemeClassColors(string module, bool value)
        {
            string key;
            if (module == ArcaneUiModuleSettings.Party)
            {
                if (_partyThemeClassColors == value) return;
                _partyThemeClassColors = value;
                key = "Party.ThemeClassColors";
            }
            else if (module == ArcaneUiModuleSettings.Raid)
            {
                if (_raidThemeClassColors == value) return;
                _raidThemeClassColors = value;
                key = "Raid.ThemeClassColors";
            }
            else return;
            PlayerPrefs.SetInt(Prefix + key, value ? 1 : 0);
            PlayerPrefs.Save();
            RaiseChanged();
        }
        // Effective setting: themed mode defaults to theme health, while a
        // legacy module continues using its original class-colour preference.
        internal static bool UsesPresetPalette(string module)
        {
            return Inherits(module) && (!Theme.IsBlank || _blankPaletteEnabled);
        }
        internal static bool EffectiveClassColors(string module, bool legacyPreference)
        {
            return UsesPresetPalette(module) ? ThemeClassColors(module) : legacyPreference;
        }
        internal static Color ResolveHealthColor(string module, Color classColor,
            Color legacyStaticColor, bool legacyClassColors)
        {
            if (UsesPresetPalette(module))
                return ThemeClassColors(module) ? classColor : Theme.Health;
            return legacyClassColors ? classColor : legacyStaticColor;
        }
        internal static bool IsBlankTheme { get { return Theme.IsBlank; } }
        internal static bool BlankPaletteEnabled { get { return _blankPaletteEnabled; } }
        internal static void SetBlankPaletteEnabled(bool enabled)
        {
            if (_blankPaletteEnabled == enabled) return;
            _blankPaletteEnabled = enabled;
            PlayerPrefs.SetInt(Prefix + "Custom.ApplyPalette", enabled ? 1 : 0);
            PlayerPrefs.Save();
            RaiseChanged();
        }
        // Custom colour values are separate from the built-in presets and
        // survive both theme switches and full game restarts.
        private static void LoadCustomPalette()
        {
            if (_themes == null) return;
            UiTheme custom = _themes.GetTheme("blank");
            custom.Health = ReadCustomColor("Health", new Color(.48f, .58f, .58f, 1f));
            custom.MissingHealth = ReadCustomColor("MissingHealth", new Color(.10f, .12f, .13f, 1f));
            custom.Mana = ReadCustomColor("Mana", new Color(.31f, .44f, .70f, 1f));
            custom.Background = ReadCustomColor("Background", new Color(.07f, .08f, .09f, .98f));
            custom.Panel = ReadCustomColor("Panel", new Color(.11f, .12f, .14f, .98f));
            custom.Border = ReadCustomColor("Border", new Color(.25f, .27f, .30f, 1f));
            custom.Accent = ReadCustomColor("Accent", new Color(.22f, .65f, .85f, 1f));
        }
        private static Color ReadCustomColor(string channel, Color fallback)
        {
            string key = Prefix + "Custom." + channel;
            Color parsed = UnitFrameAppearance.ParseColorOrDefault(
                PlayerPrefs.GetString(key, ""), fallback);
            parsed.a = fallback.a;
            return parsed;
        }
        internal static string GetCustomColorHex(string channel)
        {
            Color color;
            if (!TryGetCustomColor(channel, out color)) return "#FFFFFF";
            return "#" + ColorUtility.ToHtmlStringRGB(color);
        }
        internal static void SetCustomColorHex(string channel, string input)
        {
            if (_themes == null) return;
            Color previous;
            if (!TryGetCustomColor(channel, out previous)) return;
            Color parsed;
            if (!UnitFrameAppearance.TryParseColor(input, out parsed)) return;
            parsed.a = previous.a;
            UiTheme custom = _themes.GetTheme("blank");
            switch (channel)
            {
                case "Health": custom.Health = parsed; break;
                case "MissingHealth": custom.MissingHealth = parsed; break;
                case "Mana": custom.Mana = parsed; break;
                case "Background": custom.Background = parsed; break;
                case "Panel": custom.Panel = parsed; break;
                case "Border": custom.Border = parsed; break;
                case "Accent": custom.Accent = parsed; break;
                default: return;
            }
            PlayerPrefs.SetString(Prefix + "Custom." + channel, "#" + ColorUtility.ToHtmlStringRGB(parsed));
            PlayerPrefs.Save();
            RaiseChanged();
        }
        private static bool TryGetCustomColor(string channel, out Color color)
        {
            color = Color.white;
            if (_themes == null) return false;
            UiTheme custom = _themes.GetTheme("blank");
            switch (channel)
            {
                case "Health": color = custom.Health; break;
                case "MissingHealth": color = custom.MissingHealth; break;
                case "Mana": color = custom.Mana; break;
                case "Background": color = custom.Background; break;
                case "Panel": color = custom.Panel; break;
                case "Border": color = custom.Border; break;
                case "Accent": color = custom.Accent; break;
                default: return false;
            }
            return true;
        }
        internal static string ModuleTextureOverride(string module)
        {
            if (module == ArcaneUiModuleSettings.Party) return _partyTextureOverride;
            if (module == ArcaneUiModuleSettings.Raid) return _raidTextureOverride;
            return "";
        }
        internal static string ModuleFontOverride(string module)
        {
            if (module == ArcaneUiModuleSettings.Party) return _partyFontOverride;
            if (module == ArcaneUiModuleSettings.Raid) return _raidFontOverride;
            return "";
        }
        internal static void SetModuleTextureOverride(string module, string texture)
        {
            texture = texture ?? "";
            if (module == ArcaneUiModuleSettings.Party)
            {
                _partyTextureOverride = texture;
                PlayerPrefs.SetString(Prefix + "Party.TextureOverride", texture);
            }
            else if (module == ArcaneUiModuleSettings.Raid)
            {
                _raidTextureOverride = texture;
                PlayerPrefs.SetString(Prefix + "Raid.TextureOverride", texture);
            }
            else return;
            PlayerPrefs.Save();
            RaiseChanged();
        }
        internal static void SetModuleFontOverride(string module, string font)
        {
            font = font ?? "";
            if (module == ArcaneUiModuleSettings.Party)
            {
                _partyFontOverride = font;
                PlayerPrefs.SetString(Prefix + "Party.FontOverride", font);
            }
            else if (module == ArcaneUiModuleSettings.Raid)
            {
                _raidFontOverride = font;
                PlayerPrefs.SetString(Prefix + "Raid.FontOverride", font);
            }
            else return;
            PlayerPrefs.Save();
            RaiseChanged();
        }
        internal static string EffectiveTexture(string module, string legacyTexture)
        {
            if (!Inherits(module)) return legacyTexture;
            string local = ModuleTextureOverride(module);
            if (!string.IsNullOrEmpty(local)) return local;
            if (_barTexture != "Theme Default") return _barTexture;
            return Theme.IsBlank ? legacyTexture : Theme.BarTexture;
        }
        internal static string EffectiveFont(string module, string legacyFont)
        {
            if (!Inherits(module)) return legacyFont;
            string local = ModuleFontOverride(module);
            if (!string.IsNullOrEmpty(local)) return local;
            if (_fontName != "Theme Default") return _fontName;
            return Theme.IsBlank ? legacyFont : Theme.FontName;
        }
        internal static string GlobalBarTexture { get { return _barTexture; } }
        internal static string GlobalFont { get { return _fontName; } }
        internal static int GlobalBorderSize { get { return _borderSize; } }
        internal static float GlobalOpacity { get { return _opacity; } }
        internal static void SetGlobalBarTexture(string value)
        {
            _barTexture = string.IsNullOrEmpty(value) ? "Theme Default" : value;
            PlayerPrefs.SetString(Prefix + "Global.BarTexture", _barTexture);
            PlayerPrefs.Save(); RaiseChanged();
        }
        internal static void SetGlobalFont(string value)
        {
            _fontName = string.IsNullOrEmpty(value) ? "Theme Default" : value;
            PlayerPrefs.SetString(Prefix + "Global.Font", _fontName);
            PlayerPrefs.Save(); RaiseChanged();
        }
        internal static void SetGlobalBorderSize(int value)
        {
            _borderSize = Mathf.Clamp(value, 1, 3);
            PlayerPrefs.SetInt(Prefix + "Global.BorderSize", _borderSize);
            PlayerPrefs.Save(); RaiseChanged();
        }
        internal static void SetGlobalOpacity(float value)
        {
            _opacity = Mathf.Clamp(value, .3f, 1f);
            PlayerPrefs.SetFloat(Prefix + "Global.Opacity", _opacity);
            PlayerPrefs.Save(); RaiseChanged();
        }
        internal static string NextTexture()
        {
            if (_barTexture == "Theme Default") return UnitFrameAppearance.TextureNames[0];
            string next = UnitFrameAppearance.NextTexture(_barTexture);
            return next == UnitFrameAppearance.TextureNames[0] ? "Theme Default" : next;
        }
        internal static string NextFont()
        {
            if (_fontName == "Theme Default") return UnitFrameAppearance.FontNames[0];
            string next = UnitFrameAppearance.NextFont(_fontName);
            return next == UnitFrameAppearance.FontNames[0] ? "Theme Default" : next;
        }
        internal static UiTheme Theme { get { return _themes != null ? _themes.Current : new ThemeCatalog().Get("dark"); } }
        internal static Color WithOpacity(Color color)
        {
            color.a *= _opacity;
            return color;
        }
        internal static ArcaneUiFrameStyle FrameStyle(string module)
        {
            UiTheme theme = Theme;
            bool inherit = UsesPresetPalette(module);
            // Blank preserves local colours until the user explicitly enables
            // their custom global palette; textures/fonts remain independent.
            Color border = theme.Border;
            border.a *= _opacity;
            Color background = theme.Background;
            background.a *= _opacity;
            Color resource = theme.Panel;
            resource.a *= _opacity;
            Color missingHealth = theme.MissingHealth;
            missingHealth.a *= _opacity;
            return new ArcaneUiFrameStyle
            {
                Inherit = inherit,
                Border = border,
                Background = background,
                ResourceBackground = resource,
                Text = theme.Text,
                Health = theme.Health,
                MissingHealth = missingHealth,
                Mana = theme.Mana,
                BarTexture = Inherits(module) ? EffectiveTexture(module, "") : "",
                FontName = Inherits(module) ? EffectiveFont(module, "") : "",
                BorderSize = _borderSize
            };
        }
        internal static Color ManagerColor(ArcaneUiSurfaceRole role, Color original)
        {
            if (!UsesPresetPalette(ArcaneUiModuleSettings.Manager)) return original;
            UiTheme theme = Theme;
            Color selected;
            switch (role)
            {
                case ArcaneUiSurfaceRole.Panel: selected = theme.Background; break;
                case ArcaneUiSurfaceRole.Header: selected = theme.Panel; break;
                case ArcaneUiSurfaceRole.Button: selected = Color.Lerp(theme.Panel, theme.Accent, .19f); break;
                case ArcaneUiSurfaceRole.Group: selected = Color.Lerp(theme.Panel, theme.Border, .32f); break;
                case ArcaneUiSurfaceRole.Target: selected = Color.Lerp(theme.Background, theme.Panel, .5f); break;
                case ArcaneUiSurfaceRole.Popup: selected = theme.Background; break;
                case ArcaneUiSurfaceRole.Track: selected = theme.Border; break;
                default: return original;
            }
            selected.a = original.a * _opacity;
            return selected;
        }
    }

    internal enum ArcaneUiSurfaceRole { None, Panel, Header, Button, Group, Target, Popup, Track }
}
