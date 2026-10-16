using UnityEngine;

namespace ArcanesGroupFrames
{
    internal static class PartyFramesSettings
    {
        private const string Prefix = "ArcanesGroupFrames.Party.";

        internal const float DefaultFrameWidth = 145f;
        internal const float DefaultFrameHeight = 38f;
        internal const float DefaultMemberSpacing = 2f;
        internal const float DefaultPositionX = 8f;
        internal const float DefaultPositionY = -220f;

        internal static bool Locked { get; private set; } = true;
        internal static bool ShowHpPercent { get; private set; } = true;
        internal static bool ShowRawHealth { get; private set; } = false;
        internal static bool UseClassColors { get; private set; } = true;
        internal static bool EnableMouseoverCasting { get; private set; } = true;
        internal static bool ShowManaBars { get; private set; } = true;
        internal static bool ShowTargetHighlight { get; private set; } = true;
        internal static bool ShowAggroHighlight { get; private set; } = true;
        internal static bool ShowStatusIcons { get; private set; } = true;
        // False = down; true = to the right as new members appear.
        internal static bool GrowMembersHorizontally { get; private set; } = false;

        internal static string BarTexture { get; private set; } = "Flat";
        internal static string StaticHealthColorHex { get; private set; } = "#2E8538";
        internal static string MissingHealthColorHex { get; private set; } = "#121213";
        internal static string FontName { get; private set; } = "Default";
        internal static int NameFontSize { get; private set; } = 12;
        internal static int HpPercentFontSize { get; private set; } = 11;
        internal static int RawHpFontSize { get; private set; } = 10;

        internal static float FrameWidth { get; private set; } = DefaultFrameWidth;
        internal static float FrameHeight { get; private set; } = DefaultFrameHeight;
        internal static float MemberSpacing { get; private set; } = DefaultMemberSpacing;
        internal static float TargetHighlightThickness { get; private set; } = 2f;
        internal static float AggroHighlightThickness { get; private set; } = 2f;
        internal static float PositionX { get; private set; } = DefaultPositionX;
        internal static float PositionY { get; private set; } = DefaultPositionY;

        internal static string ClickLeft { get; private set; } = ClickCastingManager.ActionTarget;
        internal static string ClickRight { get; private set; } = ClickCastingManager.ActionInspect;
        internal static string ClickMiddle { get; private set; } = ClickCastingManager.ActionNone;
        internal static string ClickShiftLeft { get; private set; } = ClickCastingManager.ActionNone;
        internal static string ClickShiftRight { get; private set; } = ClickCastingManager.ActionNone;
        internal static string ClickCtrlLeft { get; private set; } = ClickCastingManager.ActionNone;
        internal static string ClickCtrlRight { get; private set; } = ClickCastingManager.ActionNone;
        internal static string ClickAltLeft { get; private set; } = ClickCastingManager.ActionNone;
        internal static string ClickAltRight { get; private set; } = ClickCastingManager.ActionNone;

        internal static void Load()
        {
            Locked = PlayerPrefs.GetInt(Prefix + "Locked", 1) != 0;
            ShowHpPercent = PlayerPrefs.GetInt(Prefix + "ShowHpPercent", 1) != 0;
            ShowRawHealth = PlayerPrefs.GetInt(Prefix + "ShowRawHealth", 0) != 0;
            UseClassColors = PlayerPrefs.GetInt(Prefix + "UseClassColors", 1) != 0;
            EnableMouseoverCasting = PlayerPrefs.GetInt(Prefix + "EnableMouseoverCasting", 1) != 0;
            ShowManaBars = PlayerPrefs.GetInt(Prefix + "ShowManaBars", 1) != 0;
            ShowTargetHighlight = PlayerPrefs.GetInt(Prefix + "ShowTargetHighlight", 1) != 0;
            ShowAggroHighlight = PlayerPrefs.GetInt(Prefix + "ShowAggroHighlight", 1) != 0;
            ShowStatusIcons = PlayerPrefs.GetInt(Prefix + "ShowStatusIcons", 1) != 0;
            GrowMembersHorizontally = PlayerPrefs.GetInt(Prefix + "GrowMembersHorizontally", 0) != 0;
            BarTexture = PlayerPrefs.GetString(Prefix + "BarTexture", "Flat");
            StaticHealthColorHex = PlayerPrefs.GetString(Prefix + "StaticHealthColorHex", "#2E8538");
            MissingHealthColorHex = PlayerPrefs.GetString(Prefix + "MissingHealthColorHex", "#121213");
            FontName = PlayerPrefs.GetString(Prefix + "FontName", "Default");
            NameFontSize = PlayerPrefs.GetInt(Prefix + "NameFontSize", 12);
            HpPercentFontSize = PlayerPrefs.GetInt(Prefix + "HpPercentFontSize", 11);
            RawHpFontSize = PlayerPrefs.GetInt(Prefix + "RawHpFontSize", 10);

            FrameWidth = PlayerPrefs.GetFloat(Prefix + "FrameWidth", DefaultFrameWidth);
            FrameHeight = PlayerPrefs.GetFloat(Prefix + "FrameHeight", DefaultFrameHeight);
            MemberSpacing = PlayerPrefs.GetFloat(Prefix + "MemberSpacing", DefaultMemberSpacing);
            TargetHighlightThickness = PlayerPrefs.GetFloat(Prefix + "TargetHighlightThickness", 2f);
            AggroHighlightThickness = PlayerPrefs.GetFloat(Prefix + "AggroHighlightThickness", 2f);
            PositionX = PlayerPrefs.GetFloat(Prefix + "PositionX", DefaultPositionX);
            PositionY = PlayerPrefs.GetFloat(Prefix + "PositionY", DefaultPositionY);

            ClickLeft = PlayerPrefs.GetString(Prefix + "Click.Left", ClickCastingManager.ActionTarget);
            ClickRight = PlayerPrefs.GetString(Prefix + "Click.Right", ClickCastingManager.ActionInspect);
            ClickMiddle = PlayerPrefs.GetString(Prefix + "Click.Middle", ClickCastingManager.ActionNone);
            ClickShiftLeft = PlayerPrefs.GetString(Prefix + "Click.ShiftLeft", ClickCastingManager.ActionNone);
            ClickShiftRight = PlayerPrefs.GetString(Prefix + "Click.ShiftRight", ClickCastingManager.ActionNone);
            ClickCtrlLeft = PlayerPrefs.GetString(Prefix + "Click.CtrlLeft", ClickCastingManager.ActionNone);
            ClickCtrlRight = PlayerPrefs.GetString(Prefix + "Click.CtrlRight", ClickCastingManager.ActionNone);
            ClickAltLeft = PlayerPrefs.GetString(Prefix + "Click.AltLeft", ClickCastingManager.ActionNone);
            ClickAltRight = PlayerPrefs.GetString(Prefix + "Click.AltRight", ClickCastingManager.ActionNone);

            ClampValues();
        }

        internal static void SetLocked(bool value) { Locked = value; PlayerPrefs.SetInt(Prefix + "Locked", value ? 1 : 0); Save(); }
        internal static void SetShowHpPercent(bool value) { ShowHpPercent = value; PlayerPrefs.SetInt(Prefix + "ShowHpPercent", value ? 1 : 0); Save(); }
        internal static void SetShowRawHealth(bool value) { ShowRawHealth = value; PlayerPrefs.SetInt(Prefix + "ShowRawHealth", value ? 1 : 0); Save(); }
        internal static void SetUseClassColors(bool value) { UseClassColors = value; PlayerPrefs.SetInt(Prefix + "UseClassColors", value ? 1 : 0); Save(); }
        internal static void SetEnableMouseoverCasting(bool value) { EnableMouseoverCasting = value; PlayerPrefs.SetInt(Prefix + "EnableMouseoverCasting", value ? 1 : 0); Save(); }
        internal static void SetShowManaBars(bool value) { ShowManaBars = value; PlayerPrefs.SetInt(Prefix + "ShowManaBars", value ? 1 : 0); Save(); }
        internal static void SetShowTargetHighlight(bool value) { ShowTargetHighlight = value; PlayerPrefs.SetInt(Prefix + "ShowTargetHighlight", value ? 1 : 0); Save(); }
        internal static void SetShowAggroHighlight(bool value) { ShowAggroHighlight = value; PlayerPrefs.SetInt(Prefix + "ShowAggroHighlight", value ? 1 : 0); Save(); }
        internal static void SetShowStatusIcons(bool value) { ShowStatusIcons = value; PlayerPrefs.SetInt(Prefix + "ShowStatusIcons", value ? 1 : 0); Save(); }
        internal static void SetGrowMembersHorizontally(bool value)
        {
            GrowMembersHorizontally = value;
            PlayerPrefs.SetInt(Prefix + "GrowMembersHorizontally", value ? 1 : 0);
            Save();
        }
        internal static void SetBarTexture(string value)
        {
            BarTexture = string.IsNullOrWhiteSpace(value) ? "Flat" : value;
            PlayerPrefs.SetString(Prefix + "BarTexture", BarTexture);
            Save();
        }
        internal static void SetStaticHealthColorHex(string value)
        {
            StaticHealthColorHex = UnitFrameAppearance.NormalizeColorHex(value, StaticHealthColorHex);
            PlayerPrefs.SetString(Prefix + "StaticHealthColorHex", StaticHealthColorHex);
            Save();
        }
        internal static void SetMissingHealthColorHex(string value)
        {
            MissingHealthColorHex = UnitFrameAppearance.NormalizeColorHex(value, MissingHealthColorHex);
            PlayerPrefs.SetString(Prefix + "MissingHealthColorHex", MissingHealthColorHex);
            Save();
        }
        internal static void SetFontName(string value)
        {
            FontName = string.IsNullOrWhiteSpace(value) ? "Default" : value;
            PlayerPrefs.SetString(Prefix + "FontName", FontName);
            Save();
        }
        internal static void SetNameFontSize(float value) { NameFontSize = Mathf.Clamp(Mathf.RoundToInt(value), 8, 24); PlayerPrefs.SetInt(Prefix + "NameFontSize", NameFontSize); Save(); }
        internal static void SetHpPercentFontSize(float value) { HpPercentFontSize = Mathf.Clamp(Mathf.RoundToInt(value), 8, 24); PlayerPrefs.SetInt(Prefix + "HpPercentFontSize", HpPercentFontSize); Save(); }
        internal static void SetRawHpFontSize(float value) { RawHpFontSize = Mathf.Clamp(Mathf.RoundToInt(value), 8, 24); PlayerPrefs.SetInt(Prefix + "RawHpFontSize", RawHpFontSize); Save(); }
        internal static void SetFrameWidth(float value) { FrameWidth = Mathf.Clamp(value, 90f, 240f); PlayerPrefs.SetFloat(Prefix + "FrameWidth", FrameWidth); Save(); }
        internal static void SetFrameHeight(float value) { FrameHeight = Mathf.Clamp(value, 24f, 70f); PlayerPrefs.SetFloat(Prefix + "FrameHeight", FrameHeight); Save(); }
        internal static void SetMemberSpacing(float value) { MemberSpacing = Mathf.Clamp(value, 0f, 10f); PlayerPrefs.SetFloat(Prefix + "MemberSpacing", MemberSpacing); Save(); }
        internal static void SetTargetHighlightThickness(float value) { TargetHighlightThickness = Mathf.Clamp(value, 1f, 6f); PlayerPrefs.SetFloat(Prefix + "TargetHighlightThickness", TargetHighlightThickness); Save(); }
        internal static void SetAggroHighlightThickness(float value) { AggroHighlightThickness = Mathf.Clamp(value, 1f, 6f); PlayerPrefs.SetFloat(Prefix + "AggroHighlightThickness", AggroHighlightThickness); Save(); }

        internal static Vector2 GetPosition() { return new Vector2(PositionX, PositionY); }

        internal static void SetPosition(Vector2 position)
        {
            PositionX = position.x;
            PositionY = position.y;
            PlayerPrefs.SetFloat(Prefix + "PositionX", PositionX);
            PlayerPrefs.SetFloat(Prefix + "PositionY", PositionY);
            Save();
        }

        internal static void SetClickBinding(ClickCastBindingSlot slot, string value)
        {
            if (string.IsNullOrEmpty(value)) value = ClickCastingManager.ActionNone;

            switch (slot)
            {
                case ClickCastBindingSlot.LeftClick: ClickLeft = value; PlayerPrefs.SetString(Prefix + "Click.Left", value); break;
                case ClickCastBindingSlot.RightClick: ClickRight = value; PlayerPrefs.SetString(Prefix + "Click.Right", value); break;
                case ClickCastBindingSlot.MiddleClick: ClickMiddle = value; PlayerPrefs.SetString(Prefix + "Click.Middle", value); break;
                case ClickCastBindingSlot.ShiftLeft: ClickShiftLeft = value; PlayerPrefs.SetString(Prefix + "Click.ShiftLeft", value); break;
                case ClickCastBindingSlot.ShiftRight: ClickShiftRight = value; PlayerPrefs.SetString(Prefix + "Click.ShiftRight", value); break;
                case ClickCastBindingSlot.CtrlLeft: ClickCtrlLeft = value; PlayerPrefs.SetString(Prefix + "Click.CtrlLeft", value); break;
                case ClickCastBindingSlot.CtrlRight: ClickCtrlRight = value; PlayerPrefs.SetString(Prefix + "Click.CtrlRight", value); break;
                case ClickCastBindingSlot.AltLeft: ClickAltLeft = value; PlayerPrefs.SetString(Prefix + "Click.AltLeft", value); break;
                case ClickCastBindingSlot.AltRight: ClickAltRight = value; PlayerPrefs.SetString(Prefix + "Click.AltRight", value); break;
            }

            Save();
        }

        internal static void ResetClickBindings()
        {
            SetClickBinding(ClickCastBindingSlot.LeftClick, ClickCastingManager.ActionTarget);
            SetClickBinding(ClickCastBindingSlot.RightClick, ClickCastingManager.ActionInspect);
            SetClickBinding(ClickCastBindingSlot.MiddleClick, ClickCastingManager.ActionNone);
            SetClickBinding(ClickCastBindingSlot.ShiftLeft, ClickCastingManager.ActionNone);
            SetClickBinding(ClickCastBindingSlot.ShiftRight, ClickCastingManager.ActionNone);
            SetClickBinding(ClickCastBindingSlot.CtrlLeft, ClickCastingManager.ActionNone);
            SetClickBinding(ClickCastBindingSlot.CtrlRight, ClickCastingManager.ActionNone);
            SetClickBinding(ClickCastBindingSlot.AltLeft, ClickCastingManager.ActionNone);
            SetClickBinding(ClickCastBindingSlot.AltRight, ClickCastingManager.ActionNone);
        }

        internal static void ResetDefaults()
        {
            Locked = true;
            ShowHpPercent = true;
            ShowRawHealth = false;
            UseClassColors = true;
            EnableMouseoverCasting = true;
            ShowManaBars = true;
            ShowTargetHighlight = true;
            ShowAggroHighlight = true;
            ShowStatusIcons = true;
            GrowMembersHorizontally = false;
            BarTexture = "Flat";
            StaticHealthColorHex = "#2E8538";
            MissingHealthColorHex = "#121213";
            FontName = "Default";
            NameFontSize = 12;
            HpPercentFontSize = 11;
            RawHpFontSize = 10;
            FrameWidth = DefaultFrameWidth;
            FrameHeight = DefaultFrameHeight;
            MemberSpacing = DefaultMemberSpacing;
            TargetHighlightThickness = 2f;
            AggroHighlightThickness = 2f;
            PositionX = DefaultPositionX;
            PositionY = DefaultPositionY;

            PlayerPrefs.SetInt(Prefix + "Locked", 1);
            PlayerPrefs.SetInt(Prefix + "ShowHpPercent", 1);
            PlayerPrefs.SetInt(Prefix + "ShowRawHealth", 0);
            PlayerPrefs.SetInt(Prefix + "UseClassColors", 1);
            PlayerPrefs.SetInt(Prefix + "EnableMouseoverCasting", 1);
            PlayerPrefs.SetInt(Prefix + "ShowManaBars", 1);
            PlayerPrefs.SetInt(Prefix + "ShowTargetHighlight", 1);
            PlayerPrefs.SetInt(Prefix + "ShowAggroHighlight", 1);
            PlayerPrefs.SetInt(Prefix + "ShowStatusIcons", 1);
            PlayerPrefs.SetInt(Prefix + "GrowMembersHorizontally", 0);
            PlayerPrefs.SetString(Prefix + "BarTexture", BarTexture);
            PlayerPrefs.SetString(Prefix + "StaticHealthColorHex", StaticHealthColorHex);
            PlayerPrefs.SetString(Prefix + "MissingHealthColorHex", MissingHealthColorHex);
            PlayerPrefs.SetString(Prefix + "FontName", FontName);
            PlayerPrefs.SetInt(Prefix + "NameFontSize", NameFontSize);
            PlayerPrefs.SetInt(Prefix + "HpPercentFontSize", HpPercentFontSize);
            PlayerPrefs.SetInt(Prefix + "RawHpFontSize", RawHpFontSize);
            PlayerPrefs.SetFloat(Prefix + "FrameWidth", FrameWidth);
            PlayerPrefs.SetFloat(Prefix + "FrameHeight", FrameHeight);
            PlayerPrefs.SetFloat(Prefix + "MemberSpacing", MemberSpacing);
            PlayerPrefs.SetFloat(Prefix + "TargetHighlightThickness", TargetHighlightThickness);
            PlayerPrefs.SetFloat(Prefix + "AggroHighlightThickness", AggroHighlightThickness);
            PlayerPrefs.SetFloat(Prefix + "PositionX", PositionX);
            PlayerPrefs.SetFloat(Prefix + "PositionY", PositionY);

            ResetClickBindings();
            Save();
        }

        private static void ClampValues()
        {
            FrameWidth = Mathf.Clamp(FrameWidth, 90f, 240f);
            FrameHeight = Mathf.Clamp(FrameHeight, 24f, 70f);
            MemberSpacing = Mathf.Clamp(MemberSpacing, 0f, 10f);
            TargetHighlightThickness = Mathf.Clamp(TargetHighlightThickness, 1f, 6f);
            AggroHighlightThickness = Mathf.Clamp(AggroHighlightThickness, 1f, 6f);
            NameFontSize = Mathf.Clamp(NameFontSize, 8, 24);
            HpPercentFontSize = Mathf.Clamp(HpPercentFontSize, 8, 24);
            RawHpFontSize = Mathf.Clamp(RawHpFontSize, 8, 24);
            StaticHealthColorHex = UnitFrameAppearance.NormalizeColorHex(StaticHealthColorHex, "#2E8538");
            MissingHealthColorHex = UnitFrameAppearance.NormalizeColorHex(MissingHealthColorHex, "#121213");
        }

        private static void Save()
        {
            PlayerPrefs.Save();
        }
    }
}
