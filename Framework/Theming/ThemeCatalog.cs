using System;
using System.Collections.Generic;
using UnityEngine;

namespace ArcanesGroupFrames.Framework.Theming
{
    public sealed class ThemeCatalog
    {
        private readonly Dictionary<string, UiTheme> _themes = new Dictionary<string, UiTheme>(StringComparer.OrdinalIgnoreCase);
        public ThemeCatalog()
        {
            Register(CreateBlankTheme());
            Register(CreateDarkTheme());
            Register(CreateFantasyTheme());
            Register(CreateTraditionalRpgTheme());
            Register(CreateArcaneTheme());
            Register(CreateNordicTheme());
            Register(CreateCrimsonTheme());
            Register(CreateVerdantTheme());
            Register(CreateMinimalTheme());
        }

        private static UiTheme CreateBlankTheme()
        {
            UiTheme theme = WithWidgetDefaults(new UiTheme
            {
                Id = "blank", DisplayName = "Blank / Custom", IsBlank = true,
                Background = new Color(.055f,.062f,.078f,.98f),
                Panel = new Color(.095f,.108f,.13f,1f),
                Border = new Color(.24f,.27f,.31f,1f),
                Text = Color.white,
                Accent = new Color(.22f,.65f,.85f,1f),
                Health = new Color(.16f,.69f,.64f,1f),
                MissingHealth = new Color(.055f,.135f,.15f,1f),
                Mana = new Color(.28f,.52f,.89f,1f),
                BarTexture = "Flat", FontName = "Default"
            });
            theme.SecondaryAccent = theme.WidgetPanel;
            theme.Danger = new Color(.74f, .24f, .22f, 1f);
            theme.Success = new Color(.25f, .68f, .46f, 1f);
            return theme;
        }

        private static UiTheme CreateDarkTheme()
        {
            UiTheme theme = WithWidgetDefaults(new UiTheme
            {
                Id = "dark", DisplayName = "Dark Minimal",
                Background = new Color(.055f,.062f,.078f,.95f),
                Panel = new Color(.095f,.108f,.13f,.98f),
                Border = new Color(.24f,.27f,.31f,1f),
                Text = Color.white,
                Accent = new Color(.22f,.65f,.85f,1f),
                Health = new Color(.16f,.69f,.64f,1f),
                MissingHealth = new Color(.055f,.135f,.15f,1f),
                Mana = new Color(.28f,.52f,.89f,1f),
                BarTexture = "Flat", FontName = "Default"
            });
            theme.SecondaryAccent = new Color(.13f, .19f, .25f, 1f);
            theme.Danger = new Color(.78f, .29f, .26f, 1f);
            theme.Success = new Color(.20f, .66f, .50f, 1f);
            return theme;
        }

        private static UiTheme CreateFantasyTheme()
        {
            UiTheme theme = WithWidgetDefaults(new UiTheme
            {
                Id = "fantasy", DisplayName = "Refined Fantasy",
                Background = new Color(.09f,.078f,.065f,.95f),
                Panel = new Color(.16f,.13f,.10f,.98f),
                Border = new Color(.58f,.45f,.26f,1f),
                Text = new Color(.96f,.91f,.81f,1f),
                Accent = new Color(.89f,.69f,.35f,1f),
                Health = new Color(.67f,.57f,.29f,1f),
                MissingHealth = new Color(.19f,.155f,.105f,1f),
                Mana = new Color(.52f,.42f,.72f,1f),
                BarTexture = "Bevel", FontName = "Georgia"
            });
            theme.HeaderPanel = new Color(.18f, .14f, .10f, 1f);
            theme.HeaderAccent = new Color(.83f, .67f, .34f, 1f);
            theme.TabInactive = new Color(.25f, .19f, .14f, 1f);
            theme.TabActive = new Color(.47f, .35f, .20f, 1f);
            theme.TabIcon = new Color(.95f, .81f, .54f, 1f);
            theme.WidgetPanel = new Color(.22f, .18f, .12f, .98f);
            theme.WidgetBorder = new Color(.64f, .49f, .27f, 1f);
            theme.InputBackground = new Color(.18f, .14f, .11f, 1f);
            theme.ButtonFill = new Color(.33f, .24f, .16f, 1f);
            theme.ButtonActiveFill = new Color(.68f, .50f, .24f, 1f);
            theme.ToggleOn = new Color(.69f, .53f, .24f, 1f);
            theme.ToggleOff = new Color(.41f, .29f, .19f, 1f);
            theme.DropdownFill = new Color(.21f, .16f, .11f, 1f);
            theme.SliderTrack = new Color(.44f, .32f, .19f, 1f);
            theme.SliderFill = new Color(.86f, .67f, .31f, 1f);
            theme.SliderHandle = new Color(.98f, .92f, .74f, 1f);
            theme.Ornament = new Color(.89f, .79f, .57f, 1f);
            theme.SecondaryAccent = new Color(.46f, .31f, .15f, 1f);
            theme.Danger = new Color(.75f, .25f, .22f, 1f);
            theme.Success = new Color(.38f, .69f, .34f, 1f);
            return theme;
        }

        private static UiTheme CreateTraditionalRpgTheme()
        {
            UiTheme theme = WithWidgetDefaults(new UiTheme
            {
                Id = "traditional-rpg", DisplayName = "Traditional RPG",
                Background = new Color(.11f, .083f, .057f, .97f),
                Panel = new Color(.18f, .13f, .09f, .99f),
                Border = new Color(.67f, .53f, .31f, 1f),
                Text = new Color(.97f, .92f, .81f, 1f),
                Accent = new Color(.85f, .63f, .24f, 1f),
                Health = new Color(.62f, .22f, .19f, 1f),
                MissingHealth = new Color(.19f, .09f, .08f, 1f),
                Mana = new Color(.32f, .41f, .73f, 1f),
                BarTexture = "Bevel", FontName = "Georgia"
            });
            theme.HeaderPanel = new Color(.22f, .16f, .11f, 1f);
            theme.HeaderAccent = new Color(.84f, .67f, .30f, 1f);
            theme.TabInactive = new Color(.29f, .20f, .12f, 1f);
            theme.TabActive = new Color(.53f, .36f, .17f, 1f);
            theme.TabIcon = new Color(.96f, .81f, .42f, 1f);
            theme.WidgetPanel = new Color(.24f, .17f, .11f, 1f);
            theme.WidgetBorder = new Color(.72f, .57f, .29f, 1f);
            theme.InputBackground = new Color(.17f, .12f, .08f, 1f);
            theme.InputText = new Color(.98f, .94f, .84f, 1f);
            theme.ButtonFill = new Color(.35f, .25f, .15f, 1f);
            theme.ButtonActiveFill = new Color(.72f, .52f, .19f, 1f);
            theme.ButtonText = new Color(.99f, .95f, .86f, 1f);
            theme.ToggleOn = new Color(.73f, .53f, .20f, 1f);
            theme.ToggleOff = new Color(.42f, .29f, .18f, 1f);
            theme.DropdownFill = new Color(.20f, .14f, .09f, 1f);
            theme.SliderTrack = new Color(.47f, .33f, .16f, 1f);
            theme.SliderFill = new Color(.84f, .66f, .27f, 1f);
            theme.SliderHandle = new Color(.99f, .94f, .79f, 1f);
            theme.Ornament = new Color(.88f, .73f, .46f, 1f);
            theme.SecondaryAccent = new Color(.49f, .33f, .18f, 1f);
            theme.Danger = new Color(.73f, .26f, .21f, 1f);
            theme.Success = new Color(.40f, .63f, .27f, 1f);
            return theme;
        }

        private static UiTheme CreateArcaneTheme()
        {
            UiTheme theme = WithWidgetDefaults(new UiTheme
            {
                Id = "arcane", DisplayName = "Arcane Relic",
                Background = new Color(.07f, .06f, .10f, .97f),
                Panel = new Color(.12f, .09f, .18f, .98f),
                Border = new Color(.44f, .34f, .71f, 1f),
                Text = new Color(.94f, .92f, .99f, 1f),
                Accent = new Color(.63f, .52f, .98f, 1f),
                Health = new Color(.43f, .56f, .94f, 1f),
                MissingHealth = new Color(.11f, .08f, .15f, 1f),
                Mana = new Color(.34f, .64f, .94f, 1f),
                BarTexture = "Bevel", FontName = "Georgia"
            });
            theme.HeaderPanel = new Color(.13f, .10f, .20f, 1f);
            theme.HeaderAccent = new Color(.71f, .61f, .98f, 1f);
            theme.TabInactive = new Color(.18f, .13f, .28f, 1f);
            theme.TabActive = new Color(.40f, .29f, .68f, 1f);
            theme.TabIcon = new Color(.86f, .80f, .99f, 1f);
            theme.WidgetPanel = new Color(.15f, .11f, .23f, .98f);
            theme.WidgetBorder = new Color(.49f, .40f, .81f, 1f);
            theme.InputBackground = new Color(.11f, .09f, .16f, 1f);
            theme.ButtonFill = new Color(.22f, .16f, .33f, 1f);
            theme.ButtonActiveFill = new Color(.52f, .38f, .87f, 1f);
            theme.Ornament = new Color(.78f, .72f, .99f, 1f);
            theme.SecondaryAccent = new Color(.21f, .50f, .80f, 1f);
            theme.Danger = new Color(.82f, .34f, .50f, 1f);
            theme.Success = new Color(.32f, .74f, .64f, 1f);
            return theme;
        }

        private static UiTheme CreateNordicTheme()
        {
            UiTheme theme = WithWidgetDefaults(new UiTheme
            {
                Id = "nordic", DisplayName = "Nordic Stone",
                Background = new Color(.08f, .10f, .12f, .97f),
                Panel = new Color(.15f, .18f, .20f, .98f),
                Border = new Color(.58f, .69f, .78f, 1f),
                Text = new Color(.92f, .95f, .98f, 1f),
                Accent = new Color(.51f, .74f, .90f, 1f),
                Health = new Color(.56f, .68f, .76f, 1f),
                MissingHealth = new Color(.12f, .14f, .16f, 1f),
                Mana = new Color(.36f, .59f, .86f, 1f),
                BarTexture = "Smooth", FontName = "Default"
            });
            theme.HeaderPanel = new Color(.16f, .19f, .22f, 1f);
            theme.HeaderAccent = new Color(.73f, .88f, .97f, 1f);
            theme.TabInactive = new Color(.22f, .25f, .29f, 1f);
            theme.TabActive = new Color(.36f, .49f, .58f, 1f);
            theme.TabIcon = new Color(.88f, .96f, 1f, 1f);
            theme.WidgetPanel = new Color(.20f, .23f, .26f, 1f);
            theme.WidgetBorder = new Color(.64f, .72f, .80f, 1f);
            theme.InputBackground = new Color(.13f, .15f, .18f, 1f);
            theme.ButtonFill = new Color(.26f, .31f, .35f, 1f);
            theme.ButtonActiveFill = new Color(.44f, .58f, .67f, 1f);
            theme.Ornament = new Color(.82f, .90f, .96f, 1f);
            theme.SecondaryAccent = new Color(.34f, .44f, .51f, 1f);
            theme.Danger = new Color(.73f, .40f, .38f, 1f);
            theme.Success = new Color(.42f, .70f, .60f, 1f);
            return theme;
        }

        private static UiTheme CreateCrimsonTheme()
        {
            UiTheme theme = WithWidgetDefaults(new UiTheme
            {
                Id = "crimson", DisplayName = "Crimson Warlord",
                Background = new Color(.10f, .05f, .06f, .97f),
                Panel = new Color(.17f, .08f, .10f, .99f),
                Border = new Color(.68f, .30f, .30f, 1f),
                Text = new Color(.98f, .92f, .90f, 1f),
                Accent = new Color(.88f, .34f, .28f, 1f),
                Health = new Color(.78f, .22f, .22f, 1f),
                MissingHealth = new Color(.19f, .08f, .09f, 1f),
                Mana = new Color(.46f, .38f, .78f, 1f),
                BarTexture = "Bevel", FontName = "Georgia"
            });
            theme.HeaderPanel = new Color(.19f, .09f, .10f, 1f);
            theme.HeaderAccent = new Color(.93f, .38f, .29f, 1f);
            theme.TabInactive = new Color(.25f, .13f, .13f, 1f);
            theme.TabActive = new Color(.50f, .20f, .18f, 1f);
            theme.TabIcon = new Color(.98f, .79f, .69f, 1f);
            theme.WidgetPanel = new Color(.22f, .11f, .12f, 1f);
            theme.WidgetBorder = new Color(.76f, .38f, .34f, 1f);
            theme.InputBackground = new Color(.14f, .07f, .08f, 1f);
            theme.ButtonFill = new Color(.31f, .13f, .13f, 1f);
            theme.ButtonActiveFill = new Color(.73f, .24f, .20f, 1f);
            theme.Ornament = new Color(.95f, .73f, .56f, 1f);
            theme.SecondaryAccent = new Color(.48f, .19f, .15f, 1f);
            theme.Danger = new Color(.92f, .30f, .28f, 1f);
            theme.Success = new Color(.50f, .65f, .35f, 1f);
            return theme;
        }

        private static UiTheme CreateVerdantTheme()
        {
            UiTheme theme = WithWidgetDefaults(new UiTheme
            {
                Id = "verdant", DisplayName = "Verdant Grove",
                Background = new Color(.06f, .10f, .07f, .97f),
                Panel = new Color(.10f, .17f, .11f, .99f),
                Border = new Color(.48f, .66f, .38f, 1f),
                Text = new Color(.94f, .97f, .89f, 1f),
                Accent = new Color(.61f, .82f, .35f, 1f),
                Health = new Color(.45f, .72f, .31f, 1f),
                MissingHealth = new Color(.10f, .15f, .09f, 1f),
                Mana = new Color(.37f, .62f, .82f, 1f),
                BarTexture = "Smooth", FontName = "Georgia"
            });
            theme.HeaderPanel = new Color(.13f, .19f, .13f, 1f);
            theme.HeaderAccent = new Color(.78f, .93f, .44f, 1f);
            theme.TabInactive = new Color(.17f, .25f, .16f, 1f);
            theme.TabActive = new Color(.31f, .47f, .23f, 1f);
            theme.TabIcon = new Color(.92f, .98f, .79f, 1f);
            theme.WidgetPanel = new Color(.14f, .22f, .14f, 1f);
            theme.WidgetBorder = new Color(.57f, .73f, .43f, 1f);
            theme.InputBackground = new Color(.10f, .15f, .10f, 1f);
            theme.ButtonFill = new Color(.19f, .31f, .18f, 1f);
            theme.ButtonActiveFill = new Color(.41f, .62f, .27f, 1f);
            theme.Ornament = new Color(.84f, .95f, .68f, 1f);
            theme.SecondaryAccent = new Color(.31f, .46f, .20f, 1f);
            theme.Danger = new Color(.80f, .34f, .22f, 1f);
            theme.Success = new Color(.49f, .77f, .34f, 1f);
            return theme;
        }

        private static UiTheme CreateMinimalTheme()
        {
            UiTheme theme = WithWidgetDefaults(new UiTheme
            {
                Id = "minimal", DisplayName = "Minimal",
                Background = new Color(.075f,.085f,.10f,.96f),
                Panel = new Color(.115f,.125f,.15f,.98f),
                Border = new Color(.24f,.28f,.32f,1f),
                Text = new Color(.91f,.93f,.96f,1f),
                Accent = new Color(.39f,.62f,.70f,1f),
                Health = new Color(.42f,.65f,.73f,1f),
                MissingHealth = new Color(.12f,.17f,.20f,1f),
                Mana = new Color(.43f,.56f,.77f,1f),
                BarTexture = "Smooth", FontName = "Default"
            });
            theme.TabInactive = new Color(.14f, .16f, .19f, 1f);
            theme.TabActive = new Color(.26f, .34f, .39f, 1f);
            theme.WidgetPanel = new Color(.14f, .16f, .18f, 1f);
            theme.WidgetBorder = new Color(.31f, .36f, .40f, 1f);
            theme.InputBackground = new Color(.11f, .12f, .14f, 1f);
            theme.ButtonFill = new Color(.18f, .22f, .26f, 1f);
            theme.ButtonActiveFill = new Color(.30f, .45f, .53f, 1f);
            theme.ToggleOn = new Color(.34f, .54f, .61f, 1f);
            theme.ToggleOff = new Color(.23f, .27f, .31f, 1f);
            theme.DropdownFill = new Color(.14f, .16f, .19f, 1f);
            theme.SliderTrack = new Color(.26f, .30f, .35f, 1f);
            theme.SliderFill = new Color(.40f, .61f, .70f, 1f);
            theme.SliderHandle = new Color(.95f, .97f, 1f, 1f);
            theme.SecondaryAccent = new Color(.18f, .22f, .26f, 1f);
            theme.Danger = new Color(.80f, .34f, .29f, 1f);
            theme.Success = new Color(.33f, .67f, .59f, 1f);
            return theme;
        }

        private static UiTheme WithWidgetDefaults(UiTheme theme)
        {
            theme.HeaderPanel = theme.Panel;
            theme.HeaderAccent = theme.Accent;
            theme.TabInactive = Color.Lerp(theme.Panel, theme.Border, .18f);
            theme.TabActive = theme.Accent;
            theme.TabIcon = theme.Accent;
            theme.WidgetPanel = Color.Lerp(theme.Panel, theme.Background, .25f);
            theme.WidgetBorder = theme.Border;
            theme.InputBackground = Color.Lerp(theme.Background, theme.Panel, .5f);
            theme.InputText = theme.Text;
            theme.ButtonFill = Color.Lerp(theme.Panel, theme.Accent, .18f);
            theme.ButtonActiveFill = theme.Accent;
            theme.ButtonText = theme.Text;
            theme.ToggleOn = theme.Accent;
            theme.ToggleOff = theme.Border;
            theme.DropdownFill = theme.Panel;
            theme.SliderTrack = theme.Border;
            theme.SliderFill = theme.Accent;
            theme.SliderHandle = theme.Text;
            theme.Ornament = Color.Lerp(theme.Accent, theme.Text, .35f);
            theme.SecondaryAccent = Color.Lerp(theme.Panel, theme.Accent, .30f);
            theme.Danger = new Color(.77f, .28f, .24f, 1f);
            theme.Success = new Color(.30f, .69f, .43f, 1f);
            return theme;
        }

        public void Register(UiTheme theme)
        {
            if (theme == null || string.IsNullOrEmpty(theme.Id)) throw new ArgumentException("Theme ID is required");
            _themes.Add(theme.Id, theme);
        }
        public bool TryGet(string id, out UiTheme theme)
        {
            theme = null;
            return !string.IsNullOrEmpty(id) && _themes.TryGetValue(id, out theme);
        }
        public UiTheme Get(string id)
        {
            UiTheme result;
            return id != null && _themes.TryGetValue(id, out result) ? result : _themes["dark"];
        }
    }
}
