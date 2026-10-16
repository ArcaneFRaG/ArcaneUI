using UnityEngine;

namespace ArcanesGroupFrames.Modules.CooldownAuras.Configuration
{
    /// <summary>Independent, presentation-only Phase 9 tracker options.
    /// Native auras, casts and hotkeys remain authoritative.</summary>
    internal static class CooldownAuraSettings
    {
        private const string Prefix = "ArcaneUI.CooldownAuras.";
        internal static bool Enabled { get; private set; }
        internal static bool PlayerBuffs { get; private set; }
        internal static bool PlayerDebuffs { get; private set; }
        internal static bool TargetBuffs { get; private set; }
        internal static bool TargetDebuffs { get; private set; }
        internal static bool Cooldowns { get; private set; }
        internal static bool Durations { get; private set; }
        internal static bool Stacks { get; private set; }
        internal static int IconSize { get; private set; }
        internal static int Spacing { get; private set; }
        internal static int MaxIcons { get; private set; }
        internal static float MinimumCooldown { get; private set; }
        internal static int X { get; private set; }
        internal static int Y { get; private set; }
        internal static bool Vertical { get; private set; }

        // Independent HUD containers. These are ArcaneUI-only presentation
        // settings; Erenshor owns the actual effects and cooldown state.
        internal sealed class AuraFrame
        {
            internal int X, Y, IconSize, Spacing, MaxIcons, Columns, Growth, Sort, Style, Palette;
            internal float Opacity;
            internal bool ShowTitle, ShowDuration, ShowStacks;
            internal Vector2 Position { get { return new Vector2(X, -Y); } }
        }
        private static readonly AuraFrame[] FrameValues = new AuraFrame[5];
        internal static readonly string[] FrameNames = {
            "Player Buffs", "Player Debuffs", "Target Buffs", "Target Debuffs", "Cooldowns"
        };
        internal static AuraFrame Frame(int id)
        {
            id = Mathf.Clamp(id, 0, 4);
            if (FrameValues[id] == null) FrameValues[id] = ReadFrame(id);
            return FrameValues[id];
        }
        internal static bool FrameVisible(int id)
        {
            switch (id)
            {
                case 0: return PlayerBuffs;
                case 1: return PlayerDebuffs;
                case 2: return TargetBuffs;
                case 3: return TargetDebuffs;
                default: return Cooldowns;
            }
        }
        private static string FrameKey(int id, string field)
        { return Prefix + "Frame" + id + "." + field; }
        private static AuraFrame ReadFrame(int id)
        {
            bool migrated = PlayerPrefs.GetInt(FrameKey(id, "Version"), 0) >= 1;
            int oldSize = IconSize > 0 ? IconSize : 34;
            int fallbackX = IconSize > 0 ? X : 420;
            int fallbackY = (IconSize > 0 ? Y : 145) + id * (oldSize + 23);
            var f = new AuraFrame();
            f.X = Mathf.Clamp(PlayerPrefs.GetInt(FrameKey(id, "X"), fallbackX), 0, 1920);
            f.Y = Mathf.Clamp(PlayerPrefs.GetInt(FrameKey(id, "Y"), fallbackY), 0, 1080);
            f.IconSize = Mathf.Clamp(PlayerPrefs.GetInt(FrameKey(id, "Size"), oldSize), 20, 64);
            f.Spacing = Mathf.Clamp(PlayerPrefs.GetInt(FrameKey(id, "Spacing"), Spacing), 0, 24);
            f.MaxIcons = Mathf.Clamp(PlayerPrefs.GetInt(FrameKey(id, "Max"), MaxIcons), 1, 12);
            f.Columns = Mathf.Clamp(PlayerPrefs.GetInt(FrameKey(id, "Columns"), migrated ? 8 : (Vertical ? 1 : 8)), 1, 12);
            f.Growth = Mathf.Clamp(PlayerPrefs.GetInt(FrameKey(id, "Growth"), Vertical ? 1 : 0), 0, 3);
            f.Sort = Mathf.Clamp(PlayerPrefs.GetInt(FrameKey(id, "Sort"), 0), 0, 3);
            f.Style = Mathf.Clamp(PlayerPrefs.GetInt(FrameKey(id, "Style"), 0), 0, 3);
            f.Palette = Mathf.Clamp(PlayerPrefs.GetInt(FrameKey(id, "Palette"), id == 1 || id == 3 ? 2 : 0), 0, 4);
            f.Opacity = Mathf.Clamp(PlayerPrefs.GetFloat(FrameKey(id, "Opacity"), .92f), .2f, 1f);
            f.ShowTitle = PlayerPrefs.GetInt(FrameKey(id, "Title"), 1) != 0;
            f.ShowDuration = PlayerPrefs.GetInt(FrameKey(id, "Duration"), Durations ? 1 : 0) != 0;
            f.ShowStacks = PlayerPrefs.GetInt(FrameKey(id, "Stacks"), Stacks ? 1 : 0) != 0;
            return f;
        }
        private static void Store(int id, string field, int v)
        { PlayerPrefs.SetInt(FrameKey(id, "Version"), 1); PlayerPrefs.SetInt(FrameKey(id, field), v); PlayerPrefs.Save(); }
        private static void Store(int id, string field, float v)
        { PlayerPrefs.SetInt(FrameKey(id, "Version"), 1); PlayerPrefs.SetFloat(FrameKey(id, field), v); PlayerPrefs.Save(); }
        internal static void SetFrameDragPosition(int id, Vector2 p)
        {
            // Runtime-only during mouse movement; saved only when the drag ends.
            AuraFrame f = Frame(id);
            f.X = Mathf.Clamp(Mathf.RoundToInt(p.x), 0, 1920);
            f.Y = Mathf.Clamp(Mathf.RoundToInt(-p.y), 0, 1080);
        }
        internal static void SetFramePosition(int id, Vector2 p)
        {
            AuraFrame f = Frame(id);
            f.X = Mathf.Clamp(Mathf.RoundToInt(p.x), 0, 1920);
            f.Y = Mathf.Clamp(Mathf.RoundToInt(-p.y), 0, 1080);
            PlayerPrefs.SetInt(FrameKey(id, "Version"), 1);
            PlayerPrefs.SetInt(FrameKey(id, "X"), f.X);
            PlayerPrefs.SetInt(FrameKey(id, "Y"), f.Y);
            PlayerPrefs.Save();
        }
        internal static void SetFrameSize(int id, float v) { var f = Frame(id); f.IconSize = Mathf.Clamp(Mathf.RoundToInt(v), 20, 64); Store(id, "Size", f.IconSize); }
        internal static void SetFrameSpacing(int id, float v) { var f = Frame(id); f.Spacing = Mathf.Clamp(Mathf.RoundToInt(v), 0, 24); Store(id, "Spacing", f.Spacing); }
        internal static void SetFrameMax(int id, float v) { var f = Frame(id); f.MaxIcons = Mathf.Clamp(Mathf.RoundToInt(v), 1, 12); Store(id, "Max", f.MaxIcons); }
        internal static void SetFrameColumns(int id, float v) { var f = Frame(id); f.Columns = Mathf.Clamp(Mathf.RoundToInt(v), 1, 12); Store(id, "Columns", f.Columns); }
        internal static void CycleFrameGrowth(int id) { var f = Frame(id); f.Growth = (f.Growth + 1) % 4; Store(id, "Growth", f.Growth); }
        internal static void CycleFrameSort(int id) { var f = Frame(id); f.Sort = (f.Sort + 1) % 4; Store(id, "Sort", f.Sort); }
        internal static void CycleFrameStyle(int id) { var f = Frame(id); f.Style = (f.Style + 1) % 4; Store(id, "Style", f.Style); }
        internal static void CycleFramePalette(int id) { var f = Frame(id); f.Palette = (f.Palette + 1) % 5; Store(id, "Palette", f.Palette); }
        internal static void SetFrameOpacity(int id, float v) { var f = Frame(id); f.Opacity = Mathf.Clamp(v / 100f, .2f, 1f); Store(id, "Opacity", f.Opacity); }
        internal static void SetFrameTitle(int id, bool v) { Frame(id).ShowTitle = v; Store(id, "Title", v ? 1 : 0); }
        internal static void SetFrameDuration(int id, bool v) { Frame(id).ShowDuration = v; Store(id, "Duration", v ? 1 : 0); }
        internal static void SetFrameStacks(int id, bool v) { Frame(id).ShowStacks = v; Store(id, "Stacks", v ? 1 : 0); }
        internal static readonly string[] GrowthNames = { "Right", "Down", "Left", "Up" };
        internal static readonly string[] SortNames = { "Shortest First", "Longest First", "Name A-Z", "Native Order" };
        internal static readonly string[] StyleNames = { "Classic", "Minimal", "Arcane", "Obsidian" };
        internal static readonly string[] PaletteNames = { "Cyan", "Gold", "Crimson", "Violet", "Green" };
        internal static Color Accent(int id)
        {
            switch (Frame(id).Palette)
            {
                case 1: return new Color(.94f, .72f, .31f);
                case 2: return new Color(.90f, .31f, .37f);
                case 3: return new Color(.64f, .46f, .96f);
                case 4: return new Color(.33f, .79f, .53f);
                default: return new Color(.33f, .74f, .88f);
            }
        }

        internal static void Load()
        {
            Enabled = ReadBool("Enabled", false);
            PlayerBuffs = ReadBool("PlayerBuffs", true);
            PlayerDebuffs = ReadBool("PlayerDebuffs", true);
            TargetBuffs = ReadBool("TargetBuffs", true);
            TargetDebuffs = ReadBool("TargetDebuffs", true);
            Cooldowns = ReadBool("Cooldowns", true);
            Durations = ReadBool("Durations", true);
            Stacks = ReadBool("Stacks", true);
            IconSize = Mathf.Clamp(PlayerPrefs.GetInt(Prefix + "IconSize", 34), 20, 56);
            Spacing = Mathf.Clamp(PlayerPrefs.GetInt(Prefix + "Spacing", 4), 0, 16);
            MaxIcons = Mathf.Clamp(PlayerPrefs.GetInt(Prefix + "MaxIcons", 8), 1, 12);
            MinimumCooldown = Mathf.Clamp(PlayerPrefs.GetFloat(Prefix + "MinimumCooldown", .5f), 0f, 30f);
            X = Mathf.Clamp(PlayerPrefs.GetInt(Prefix + "X", 420), 0, 1600);
            Y = Mathf.Clamp(PlayerPrefs.GetInt(Prefix + "Y", 145), 0, 900);
            Vertical = ReadBool("Vertical", false);
            for (int i = 0; i < FrameValues.Length; ++i) FrameValues[i] = ReadFrame(i);
        }
        private static bool ReadBool(string name, bool fallback)
        { return PlayerPrefs.GetInt(Prefix + name, fallback ? 1 : 0) != 0; }
        private static void Save(string name, bool v)
        { PlayerPrefs.SetInt(Prefix + name, v ? 1 : 0); PlayerPrefs.Save(); }
        private static void Save(string name, int v)
        { PlayerPrefs.SetInt(Prefix + name, v); PlayerPrefs.Save(); }
        internal static void SetEnabled(bool v) { Enabled = v; Save("Enabled", v); }
        internal static void SetPlayerBuffs(bool v) { PlayerBuffs = v; Save("PlayerBuffs", v); }
        internal static void SetPlayerDebuffs(bool v) { PlayerDebuffs = v; Save("PlayerDebuffs", v); }
        internal static void SetTargetBuffs(bool v) { TargetBuffs = v; Save("TargetBuffs", v); }
        internal static void SetTargetDebuffs(bool v) { TargetDebuffs = v; Save("TargetDebuffs", v); }
        internal static void SetCooldowns(bool v) { Cooldowns = v; Save("Cooldowns", v); }
        internal static void SetDurations(bool v) { Durations = v; Save("Durations", v); }
        internal static void SetStacks(bool v) { Stacks = v; Save("Stacks", v); }
        internal static void SetIconSize(float v) { IconSize = Mathf.Clamp(Mathf.RoundToInt(v), 20, 56); Save("IconSize", IconSize); }
        internal static void SetSpacing(float v) { Spacing = Mathf.Clamp(Mathf.RoundToInt(v), 0, 16); Save("Spacing", Spacing); }
        internal static void SetMaxIcons(float v) { MaxIcons = Mathf.Clamp(Mathf.RoundToInt(v), 1, 12); Save("MaxIcons", MaxIcons); }
        internal static void SetMinimumCooldown(float v) {
            MinimumCooldown = Mathf.Clamp(v, 0f, 30f);
            PlayerPrefs.SetFloat(Prefix + "MinimumCooldown", MinimumCooldown); PlayerPrefs.Save();
        }
        internal static void SetX(float v) { X = Mathf.Clamp(Mathf.RoundToInt(v), 0, 1600); Save("X", X); }
        internal static void SetY(float v) { Y = Mathf.Clamp(Mathf.RoundToInt(v), 0, 900); Save("Y", Y); }
        internal static void CycleDirection() { Vertical = !Vertical; Save("Vertical", Vertical); }
    }
}
