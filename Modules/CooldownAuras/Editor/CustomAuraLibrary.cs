using System;
using System.Collections.Generic;
using UnityEngine;

namespace ArcanesGroupFrames.Modules.CooldownAuras.Editor
{
    /// <summary>Data-only, v1 custom displays. No game components are serialized.
    /// Phase 9.2 keeps displays separate from the fixed Phase 9.1 tracker.</summary>
    [Serializable]
    internal sealed class CustomAuraDefinition
    {
        public string Id;
        public string Name;
        // UI organisation only. Existing v1 JSON without this field migrates to General.
        public string Folder;
        public bool Enabled;
        public string Display; // Icon, Progress Bar, Text
        public string Trigger; // Always, Player Buff, Player Debuff, Target Buff, Target Debuff, Cooldown Active, Cooldown Ready
        public string MatchName;
        // Phase 11.4C: optional sandboxed numeric and boolean expressions.
        // Serialized on the existing profile; never interpreted as C# code.
        public string ExpressionValue;
        public string ExpressionMaximum;
        public string ExpressionCondition;
        public string Colour;
        public int X, Y, Width, Height;
        public bool ShowTimer;
        // Phase 9.4: non-breaking optional visual and resource controls.
        public string Shape; // Rectangle, Circle, Diamond, Hexagon, Triangle
        public int SegmentCount; // 1..20 (Heat defaults to 10)
        public int SegmentSpacing;
        public bool ShowValue;
        public bool ShowWhenEmpty;
        public int MinimumValue; // Inclusive lower threshold
        public bool ReverseFill;
        // Visuals v2: optional fields on existing v1 JSON; migrate once without
        // changing spell triggers, names, locations or the underlying HUD state.
        public int VisualVersion;
        public string ArtSource; // Automatic, Matched Spell, Trigger, None
        public string ArtSpell;  // explicit spell name or ID, independent of trigger
        public string BackgroundColour, BorderColour, InactiveColour, TextColour;
        public int Opacity, BackgroundOpacity, BorderSize, IconPadding;
        public int TextSize, TimerSize;
        public bool ShowLabel;
        public string LabelPattern; // %n name; %v current; %m max; %p percentage; %t time
        public string TextAlignment; // Left, Centre, Right
        public string FillDirection; // Left to Right / Right to Left / Bottom to Top / Top to Bottom
        public int SegmentsPerRow;
        // Trigger 1 remains Trigger/MatchName for v1 compatibility.
        public string TriggerLogic; // AND or OR, across Trigger 1 and additional rules.
        public List<CustomAuraTriggerRule> AdditionalTriggers;
        public List<CustomAuraConditionRule> Conditions;
        public string LoadClass; // Any or a concrete playable class.
        public string LoadCombat; // Any / In Combat / Out of Combat.
        public string LoadGroup; // Any / Solo / Party / Raid.
        public bool LoadRequiresTarget;
        public string LoadZone; // Optional exact Unity scene name.
        public string LoadCharacter; // Optional exact player name.
        // Phase 9.7: animation fields are optional in v1 JSON. Old auras default
        // to static rendering and never receive unsolicited motion or glow.
        public int AnimationVersion;
        public string LoopAnimation;   // None / Pulse / Breathe / Float / Shake
        public int LoopSpeed;          // 25..300 percent of normal speed
        public int LoopIntensity;      // 0..100 percent
        public string EnterAnimation;  // None / Fade / Pop / Slide Up / Slide Down
        public string ExitAnimation;   // None / Fade / Shrink / Slide Up / Slide Down
        public int TransitionMs;       // 100..1200 ms
        public string GlowStyle;       // None / Solid / Pulse / Flash
        public string GlowColour;
        public int GlowWidth;          // 1..16px
        public int GlowOpacity;        // 0..100 percent
        // Phase 9.8: optional parent group and a read-only shared data source.
        public string GroupId;
        public int GroupOrder;
        public string LinkedAuraId;
        // Phase 9.10: independently configured vector accent and background tint.
        public string GradientColour;
        public bool UseGradient;
        public int RingThickness;
        public string BackgroundArtSpell;
        public int TextureOpacity;
        public bool TexturePreserveAspect;
        // 9.13: filled ring/arc gauge (legacy rings remain decorative by default).
        public bool RadialProgress;
        public bool RadialReverse;
        public int ProgressSmoothingMs; // 0 = no smoothing, preserves pre-9.13 behavior.

    }

    // Phase 9.6: all new rule fields are optional in legacy JsonUtility saves.
    [Serializable]
    internal sealed class CustomAuraTriggerRule
    {
        public string Type = "Player Buff";
        public string MatchName = "";
        public bool Not;
        public int MinimumValue;
        public bool ShowWhenEmpty;
    }

    [Serializable]
    internal sealed class CustomAuraConditionRule
    {
        public string Metric = "Value Percentage";
        public string Comparison = "At least";
        public int Threshold = 80;
        public bool ChangeColour = true;
        public string Colour = "#FF5533";
        public bool ChangeOpacity;
        public int Opacity = 100;
        public bool ChangeScale;
        public int Scale = 100;
        public string GlowAction = "Keep"; // Keep / Enable / Disable. First matched condition wins.
    }

    [Serializable]
    internal sealed class CustomAuraGroupDefinition
    {
        public string Id;
        public string Name = "New Group";
        public bool Enabled = true;
        public string Layout = "Horizontal";
        public string Order = "Manual";
        public bool HideInactive = true;
        public bool Reverse;
        public int Columns = 4;
        public int Spacing = 8;
        public int X = 650, Y = 320;
        public int Scale = 100;
        public bool SnapToGrid;
        public int SnapSize = 16;
    }

    [Serializable]
    internal sealed class CustomAuraProfileIndex
    {
        public List<string> Names = new List<string>();
    }

    [Serializable]
    internal sealed class CustomAuraDocument
    {
        public int Version = 1;
        public List<CustomAuraDefinition> Auras = new List<CustomAuraDefinition>();
        public List<CustomAuraGroupDefinition> Groups = new List<CustomAuraGroupDefinition>();
    }

    internal static class CustomAuraLibrary
    {
        private const string Storage = "ArcaneUI.CustomAuras.Document.v1";
        private const string ProfileIndexKey = "ArcaneUI.CustomAuras.ProfileNames.v1";
        private const string ActiveProfileKey = "ArcaneUI.CustomAuras.ActiveProfile.v1";
        private const string ProfilePrefix = "ArcaneUI.CustomAuras.Profile.v1.";
        internal const int MaximumGroups = 32;
        internal static readonly string[] GroupLayouts = { "Horizontal", "Vertical", "Grid", "HUD Panel", "Stacked Cards", "Compact Row" };
        internal static readonly string[] GroupOrders = { "Manual", "Name", "Remaining" };
        internal static string ActiveProfile { get; private set; } = "Default";

        internal const int Maximum = 200;
        internal static readonly string[] Folders = { "General", "Arcanist", "Druid", "Paladin", "Reaver", "Stormcaller", "Windblade", "Blightcaller", "Emberweaver", "Lightbringer" };
        private static readonly string[] Displays = { "Icon", "Progress Bar", "Text", "Shape", "Charge Segments" };
        internal static readonly string[] ShapeStyles = { "Rectangle", "Circle", "Diamond", "Hexagon", "Triangle", "Ring", "Arc" };
        internal static readonly string[] ArtSources = { "Automatic", "Matched Spell", "Trigger", "None" };
        internal static readonly string[] FillDirections = { "Left to Right", "Right to Left", "Bottom to Top", "Top to Bottom" };
        internal static readonly string[] TextAlignments = { "Left", "Centre", "Right" };
        internal static readonly string[] TriggerLogicOptions = { "AND", "OR" };
        internal static readonly string[] LoadCombatOptions = { "Any", "In Combat", "Out of Combat" };
        internal static readonly string[] LoadGroupOptions = { "Any", "Solo", "Party", "Raid" };
        internal static readonly string[] ConditionMetrics = { "Value Percentage", "Resource Value", "Remaining Seconds", "Stack Count" };
        internal static readonly string[] Comparisons = { "At least", "Above", "At most", "Below", "Equals" };
        internal static readonly string[] LoopAnimations = { "None", "Pulse", "Breathe", "Float", "Shake" };
        internal static readonly string[] EnterAnimations = { "None", "Fade", "Pop", "Slide Up", "Slide Down" };
        internal static readonly string[] ExitAnimations = { "None", "Fade", "Shrink", "Slide Up", "Slide Down" };
        internal static readonly string[] GlowStyles = { "None", "Solid", "Pulse", "Flash" };
        internal static readonly string[] GlowActions = { "Keep", "Enable", "Disable" };
        internal const int MaxExtraTriggers = 3;
        internal const int MaxConditions = 3;

        private static readonly string[] Triggers = { "Always", "Player Buff", "Player Debuff", "Target Buff", "Target Debuff", "Cooldown Active", "Cooldown Ready", "Emberweaver Heat", "Emberweaver Flashfire", "Emberweaver Wildfire Stored", "Emberweaver Combust Projected", "Player Health", "Player Mana", "Target Health", "Target Mana", "Player Absorb", "Custom Expression" };
        private static CustomAuraDocument _document;
        internal static int Revision { get; private set; }
        internal static IList<CustomAuraDefinition> All { get { Ensure(); return _document.Auras.AsReadOnly(); } }
        internal static IList<CustomAuraGroupDefinition> Groups { get { Ensure(); return _document.Groups.AsReadOnly(); } }
        internal static string[] ProfileNames
        {
            get
            {
                var names = new List<string> { "Default" };
                try
                {
                    var index = UnityEngine.JsonUtility.FromJson<CustomAuraProfileIndex>(PlayerPrefs.GetString(ProfileIndexKey, ""));
                    if (index != null && index.Names != null)
                        foreach (string name in index.Names)
                            if (ValidProfileName(name) && !names.Contains(name)) names.Add(name);
                }
                catch (Exception) { }
                return names.ToArray();
            }
        }
        internal static string SelectedId { get; private set; }
        internal static bool EditPositions { get; private set; }

        internal static void Load()
        {
            _document = new CustomAuraDocument();
            ActiveProfile = PlayerPrefs.GetString(ActiveProfileKey, "Default");
            if (Array.IndexOf(ProfileNames, ActiveProfile) < 0) ActiveProfile = "Default";
            try
            {
                string text = PlayerPrefs.GetString(ProfileStorage(ActiveProfile), "");
                if (!string.IsNullOrEmpty(text))
                {
                    var restored = UnityEngine.JsonUtility.FromJson<CustomAuraDocument>(text);
                    if (restored != null && restored.Version == 1 && restored.Auras != null)
                        _document = restored;
                }
            }
            catch (Exception e) { Debug.LogWarning("[ArcaneUI Auras] Failed to read saved displays: " + e.Message); }
            // Bound, validate, and discard corrupt/duplicate records before any HUD objects are created.
            var clean = new List<CustomAuraDefinition>();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (CustomAuraDefinition def in _document.Auras)
            {
                if (clean.Count >= Maximum) break;
                if (def == null || string.IsNullOrEmpty(def.Id) || !ids.Add(def.Id)) continue;
                Sanitize(def);
                clean.Add(def);
            }
            _document.Auras = clean;
            if (_document.Groups == null) _document.Groups = new List<CustomAuraGroupDefinition>();
            var groups = new List<CustomAuraGroupDefinition>();
            var groupIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var group in _document.Groups)
            {
                if (groups.Count >= MaximumGroups) break;
                if (group == null || string.IsNullOrEmpty(group.Id) || !groupIds.Add(group.Id)) continue;
                SanitizeGroup(group); groups.Add(group);
            }
            _document.Groups = groups;
            foreach (var def in clean)
            {
                if (!string.IsNullOrEmpty(def.GroupId) && !groupIds.Contains(def.GroupId)) def.GroupId = "";
                if (!string.IsNullOrEmpty(def.LinkedAuraId) &&
                    (def.LinkedAuraId == def.Id || !ids.Contains(def.LinkedAuraId))) def.LinkedAuraId = "";
            }
            SelectedId = null;
            foreach (CustomAuraDefinition aura in clean)
                if (Array.IndexOf(Folders, aura.Folder) >= 0) { SelectedId = aura.Id; break; }
            EditPositions = false;
            Revision++;
        }
        private static void Ensure() { if (_document == null) Load(); }
        private static void Sanitize(CustomAuraDefinition def)
        {
            def.Name = Clip(def.Name, 48, "New Aura");
            if (string.IsNullOrWhiteSpace(def.Folder)) def.Folder = "General"; // Keep out-of-roster folders in saved profiles without exposing them.
            def.MatchName = Clip(def.MatchName, 100, "");
            def.ExpressionValue = Clip(def.ExpressionValue, 240, "");
            def.ExpressionMaximum = Clip(def.ExpressionMaximum, 240, "");
            def.ExpressionCondition = Clip(def.ExpressionCondition, 240, "");
            def.Display = Array.IndexOf(Displays, def.Display) >= 0 ? def.Display : Displays[0];
            def.Trigger = Array.IndexOf(Triggers, def.Trigger) >= 0 ? def.Trigger : Triggers[0];
            if (Array.IndexOf(ShapeStyles, def.Shape) < 0) def.Shape = "Rectangle";
            def.SegmentCount = Mathf.Clamp(def.SegmentCount <= 0 ? 10 : def.SegmentCount, 1, 20);
            def.SegmentSpacing = Mathf.Clamp(def.SegmentSpacing, 0, 16);
            def.MinimumValue = Mathf.Clamp(def.MinimumValue, 0, 100000);
            Color colour;
            if (!ColorUtility.TryParseHtmlString(def.Colour, out colour)) def.Colour = "#D78543";
            if (def.VisualVersion < 2)
            {
                def.ArtSource = "Automatic";
                def.BackgroundColour = "#101319";
                def.BorderColour = "#000000";
                def.InactiveColour = "#493A31";
                def.TextColour = "#FFFFFF";
                def.Opacity = 100;
                def.BackgroundOpacity = def.Display == "Shape" || def.Display == "Charge Segments" ? 0 : 78;
                def.TextSize = 14; def.TimerSize = 11;
                def.TextAlignment = "Centre"; def.FillDirection = "Left to Right";
                def.SegmentsPerRow = def.SegmentCount;
                def.LabelPattern = "%n";
                def.VisualVersion = 2;
            }
            def.ArtSpell = Clip(def.ArtSpell, 100, "");
            if (Array.IndexOf(ArtSources, def.ArtSource) < 0) def.ArtSource = "Automatic";
            if (Array.IndexOf(FillDirections, def.FillDirection) < 0) def.FillDirection = FillDirections[0];
            if (Array.IndexOf(TextAlignments, def.TextAlignment) < 0) def.TextAlignment = "Centre";
            if (!ColorUtility.TryParseHtmlString(def.BackgroundColour, out colour)) def.BackgroundColour = "#101319";
            if (!ColorUtility.TryParseHtmlString(def.BorderColour, out colour)) def.BorderColour = "#000000";
            if (!ColorUtility.TryParseHtmlString(def.InactiveColour, out colour)) def.InactiveColour = "#493A31";
            if (!ColorUtility.TryParseHtmlString(def.TextColour, out colour)) def.TextColour = "#FFFFFF";
            def.Opacity = Mathf.Clamp(def.Opacity, 0, 100);
            def.BackgroundOpacity = Mathf.Clamp(def.BackgroundOpacity, 0, 100);
            def.BorderSize = Mathf.Clamp(def.BorderSize, 0, 12);
            def.IconPadding = Mathf.Clamp(def.IconPadding, 0, 24);
            def.TextSize = Mathf.Clamp(def.TextSize, 8, 36);
            def.TimerSize = Mathf.Clamp(def.TimerSize, 8, 32);
            def.SegmentsPerRow = Mathf.Clamp(def.SegmentsPerRow < 1 ? def.SegmentCount : def.SegmentsPerRow, 1, 20);
            def.LabelPattern = Clip(def.LabelPattern, 120, "%n");
            if (Array.IndexOf(TriggerLogicOptions, def.TriggerLogic) < 0) def.TriggerLogic = "AND";
            if (def.AdditionalTriggers == null) def.AdditionalTriggers = new List<CustomAuraTriggerRule>();
            if (def.Conditions == null) def.Conditions = new List<CustomAuraConditionRule>();
            if (def.AdditionalTriggers.Count > MaxExtraTriggers)
                def.AdditionalTriggers.RemoveRange(MaxExtraTriggers, def.AdditionalTriggers.Count - MaxExtraTriggers);
            if (def.Conditions.Count > MaxConditions)
                def.Conditions.RemoveRange(MaxConditions, def.Conditions.Count - MaxConditions);
            for (int i = def.AdditionalTriggers.Count - 1; i >= 0; --i)
            {
                CustomAuraTriggerRule rule = def.AdditionalTriggers[i];
                if (rule == null) { def.AdditionalTriggers.RemoveAt(i); continue; }
                if (Array.IndexOf(Triggers, rule.Type) < 0) rule.Type = "Player Buff";
                rule.MatchName = Clip(rule.MatchName, 100, "");
                rule.MinimumValue = Mathf.Clamp(rule.MinimumValue, 0, 100000);
            }
            for (int i = def.Conditions.Count - 1; i >= 0; --i)
            {
                CustomAuraConditionRule rule = def.Conditions[i];
                if (rule == null) { def.Conditions.RemoveAt(i); continue; }
                if (Array.IndexOf(ConditionMetrics, rule.Metric) < 0) rule.Metric = "Value Percentage";
                if (Array.IndexOf(Comparisons, rule.Comparison) < 0) rule.Comparison = "At least";
                rule.Threshold = Mathf.Clamp(rule.Threshold, 0, 100000);
                rule.Opacity = Mathf.Clamp(rule.Opacity, 0, 100);
                rule.Scale = Mathf.Clamp(rule.Scale <= 0 ? 100 : rule.Scale, 25, 200);
                if (!ColorUtility.TryParseHtmlString(rule.Colour, out colour)) rule.Colour = "#FF5533";
                if (Array.IndexOf(GlowActions, rule.GlowAction) < 0) rule.GlowAction = "Keep";
            }
            if (string.IsNullOrWhiteSpace(def.LoadClass)) def.LoadClass = "General"; // Do not rewrite future class rules into unrestricted General tracking.
            if (Array.IndexOf(LoadCombatOptions, def.LoadCombat) < 0) def.LoadCombat = "Any";
            if (Array.IndexOf(LoadGroupOptions, def.LoadGroup) < 0) def.LoadGroup = "Any";
            def.LoadZone = Clip(def.LoadZone, 100, "");
            def.LoadCharacter = Clip(def.LoadCharacter, 100, "");

            if (def.AnimationVersion < 1)
            {
                def.LoopAnimation = "None"; def.LoopSpeed = 100; def.LoopIntensity = 30;
                def.EnterAnimation = "None"; def.ExitAnimation = "None"; def.TransitionMs = 250;
                def.GlowStyle = "None"; def.GlowColour = "#FF9C39";
                def.GlowWidth = 6; def.GlowOpacity = 80;
                def.AnimationVersion = 1;
            }
            if (Array.IndexOf(LoopAnimations, def.LoopAnimation) < 0) def.LoopAnimation = "None";
            if (Array.IndexOf(EnterAnimations, def.EnterAnimation) < 0) def.EnterAnimation = "None";
            if (Array.IndexOf(ExitAnimations, def.ExitAnimation) < 0) def.ExitAnimation = "None";
            if (Array.IndexOf(GlowStyles, def.GlowStyle) < 0) def.GlowStyle = "None";
            if (!ColorUtility.TryParseHtmlString(def.GlowColour, out colour)) def.GlowColour = "#FF9C39";
            def.LoopSpeed = Mathf.Clamp(def.LoopSpeed <= 0 ? 100 : def.LoopSpeed, 25, 300);
            def.LoopIntensity = Mathf.Clamp(def.LoopIntensity, 0, 100);
            def.TransitionMs = Mathf.Clamp(def.TransitionMs <= 0 ? 250 : def.TransitionMs, 100, 1200);
            def.GlowWidth = Mathf.Clamp(def.GlowWidth <= 0 ? 6 : def.GlowWidth, 1, 16);
            def.GlowOpacity = Mathf.Clamp(def.GlowOpacity, 0, 100);

            def.BackgroundArtSpell = Clip(def.BackgroundArtSpell, 100, "");
            def.TextureOpacity = Mathf.Clamp(def.TextureOpacity, 0, 100);
            def.GroupId = Clip(def.GroupId, 64, "");
            def.LinkedAuraId = Clip(def.LinkedAuraId, 64, "");
            def.GroupOrder = Mathf.Clamp(def.GroupOrder, -1000, 1000);
            if (!ColorUtility.TryParseHtmlString(def.GradientColour, out colour)) def.GradientColour = "#FFDC86";
            def.RingThickness = Mathf.Clamp(def.RingThickness == 0 ? 24 : def.RingThickness, 2, 48);
            def.ProgressSmoothingMs = Mathf.Clamp(def.ProgressSmoothingMs, 0, 1000);
            def.X = Mathf.Clamp(def.X, 0, 1820); def.Y = Mathf.Clamp(def.Y, 0, 980);
            def.Width = Mathf.Clamp(def.Width, 32, 480); def.Height = Mathf.Clamp(def.Height, 22, 140);
        }
        private static void SanitizeGroup(CustomAuraGroupDefinition group)
        {
            group.Name = Clip(group.Name, 40, "New Group");
            if (Array.IndexOf(GroupLayouts, group.Layout) < 0) group.Layout = "Horizontal";
            if (Array.IndexOf(GroupOrders, group.Order) < 0) group.Order = "Manual";
            group.Columns = Mathf.Clamp(group.Columns, 1, 12);
            group.Spacing = Mathf.Clamp(group.Spacing, 0, 48);
            group.X = Mathf.Clamp(group.X, 0, 1820);
            group.Y = Mathf.Clamp(group.Y, 0, 980);
            group.Scale = Mathf.Clamp(group.Scale <= 0 ? 100 : group.Scale, 25, 200);
            group.SnapSize = Mathf.Clamp(group.SnapSize <= 0 ? 16 : group.SnapSize, 4, 64);
        }
        private static bool ValidProfileName(string name)
        {
            if (string.IsNullOrWhiteSpace(name) || name.Length > 32 || name == "Default") return false;
            foreach (char c in name)
                if (!char.IsLetterOrDigit(c) && c != '_' && c != '-' && c != ' ') return false;
            return true;
        }
        private static string ProfileStorage(string name)
        { return name == "Default" ? Storage : ProfilePrefix + name; }
        private static string Clip(string value, int length, string fallback)
        {
            return string.IsNullOrEmpty(value) ? fallback : (value.Length > length ? value.Substring(0, length) : value);
        }
        internal static CustomAuraDefinition Selected
        {
            get
            {
                Ensure();
                foreach (var aura in _document.Auras) if (aura.Id == SelectedId) return aura;
                return null;
            }
        }
        internal static void Select(string id) { Ensure(); SelectedId = id; }
        internal static CustomAuraDefinition Create()
        {
            Ensure(); if (_document.Auras.Count >= Maximum) return null;
            var result = NewDefaultAura(_document.Auras.Count);
            _document.Auras.Add(result); SelectedId = result.Id; Save(); return result;
        }
        private static CustomAuraDefinition NewDefaultAura(int index)
        {
            var result = new CustomAuraDefinition
            {
                Id = Guid.NewGuid().ToString("N"), Name = "New Aura", Folder = "General", Enabled = true,
                Display = "Icon", Trigger = "Always", MatchName = "", Colour = "#D78543",
                X = 650, Y = 400, Width = 52, Height = 52, ShowTimer = true,
                Shape = "Rectangle", SegmentCount = 10, SegmentSpacing = 4,
                ShowValue = false, ShowWhenEmpty = true, MinimumValue = 0,
                VisualVersion = 2, ArtSource = "Automatic", ArtSpell = "",
                BackgroundColour = "#101319", BorderColour = "#000000",
                InactiveColour = "#493A31", TextColour = "#FFFFFF",
                Opacity = 100, BackgroundOpacity = 78, BorderSize = 0, IconPadding = 0,
                TextSize = 14, TimerSize = 11, ShowLabel = false, LabelPattern = "%n",
                TextAlignment = "Centre", FillDirection = "Left to Right", SegmentsPerRow = 10,
                TriggerLogic = "AND", AdditionalTriggers = new List<CustomAuraTriggerRule>(),
                Conditions = new List<CustomAuraConditionRule>(), LoadClass = "General", LoadCombat = "Any", LoadGroup = "Any",
                AnimationVersion = 1, LoopAnimation = "None", LoopSpeed = 100, LoopIntensity = 30,
                EnterAnimation = "None", ExitAnimation = "None", TransitionMs = 250,
                GlowStyle = "None", GlowColour = "#FF9C39", GlowWidth = 6, GlowOpacity = 80
            };
            // Offset newly created displays to prevent every icon piling up at the same coordinates.
            result.X += (index % 6) * 60;
            result.Y += (index / 6) * 65;
            return result;
        }
        // Templates are recipes, never silently added to a character. Only a
        // click in the editor creates an instance, and every instance remains
        // editable and independently saved in the existing v1 document.
        internal static CustomAuraDefinition CreateTemplate(string templateId)
        {
            Ensure();
            if (string.Equals(templateId, "g_icontext", StringComparison.Ordinal))
                return CreateIconTextExample();
            CustomAuraHudRecipe hudRecipe = CustomAuraTemplates.FindHudRecipe(templateId);
            if (hudRecipe != null) return CreateClassHudExample(hudRecipe);
            CustomAuraTemplate template = CustomAuraTemplates.Find(templateId);
            if (template == null || _document.Auras.Count >= Maximum) return null;
            CustomAuraDefinition def = Create();
            if (def == null) return null;
            def.Name = template.Name;
            def.Folder = template.Folder;
            def.LoadClass = template.Folder;
            def.Display = template.Display;
            def.Trigger = template.Trigger;
            def.MatchName = template.Match;
            def.ArtSpell = template.Match;
            def.Colour = template.Colour;
            def.Enabled = !template.NeedsSetup;
            def.ShowLabel = template.Display != "Icon";
            def.ShowTimer = template.Trigger == "Player Buff" || template.Trigger == "Target Debuff" ||
                template.Trigger == "Cooldown Active";
            if (template.Display == "Progress Bar")
            {
                def.Width = 240; def.Height = 30;
                def.LabelPattern = "%n  %p";
                def.BackgroundOpacity = 75;
            }
            else if (template.Display == "Charge Segments")
            {
                def.Width = 310; def.Height = 36;
                def.SegmentCount = 10; def.SegmentsPerRow = 10;
                def.SegmentSpacing = 5; def.ShowValue = true;
                def.ShowWhenEmpty = true; def.InactiveColour = "#493A31";
                def.BackgroundOpacity = 0; def.ShowTimer = false;
            }
            else if (template.Display == "Shape")
            {
                def.Width = 96; def.Height = 96;
                def.Shape = "Ring"; def.RingThickness = 20;
                def.RadialProgress = true;
                def.RadialReverse = false;
                def.ArtSource = "None";
                def.ShowLabel = true;
                def.ShowValue = true;
                def.LabelPattern = "%p";
                def.TextSize = 18;
                def.BackgroundOpacity = 0;
                def.ShowWhenEmpty = true;
                def.ShowTimer = false;
            }
            else if (template.Display == "Text")
            {
                def.Width = 168; def.Height = 36;
                def.ArtSource = "None"; def.ShowValue = true; def.ShowLabel = true;
                def.LabelPattern = "%v"; def.ShowTimer = false;
                def.ShowWhenEmpty = true;
            }
            else
            {
                def.Width = 56; def.Height = 56;
                def.ArtSource = "Matched Spell";
                def.IconPadding = 1;
                def.BackgroundOpacity = 55;
            }
            if (template.Id == "emb_wildfire" || template.Id == "emb_combust")
            {
                // These are uncapped target damage numbers, not charges or percent.
                def.ShowWhenEmpty = false;
                def.MinimumValue = 1;
                def.ShowTimer = false;
                def.LabelPattern = template.Id == "emb_wildfire" ? "WILDFIRE %v" : "COMBUST %v";
                def.Width = 190; def.Height = 36;
                def.TextSize = 15;
            }
            if (template.Id == "emb_heatfull")
            {
                def.GlowStyle = "Pulse"; def.GlowColour = "#FF7630";
                def.Conditions.Add(new CustomAuraConditionRule {
                    Metric = "Value Percentage", Comparison = "At least", Threshold = 100,
                    ChangeColour = true, Colour = "#FF3825", GlowAction = "Enable"
                });
            }
            Sanitize(def);
            Save();
            return def;
        }
        internal static CustomAuraDefinition CreateHeatPreset()
        { return CreateTemplate("emb_heat"); }

        private static CustomAuraDefinition CreateIconTextExample()
        {
            Ensure();
            if (_document.Auras.Count + 2 > Maximum ||
                _document.Groups.Count >= MaximumGroups) return null;
            var group = new CustomAuraGroupDefinition {
                Id = Guid.NewGuid().ToString("N"), Name = "Spell Icon + Label",
                Enabled = true, Layout = "Compact Row", Order = "Manual",
                HideInactive = false, Spacing = 6, X = 740, Y = 350, Scale = 100
            };
            SanitizeGroup(group);
            var icon = NewDefaultAura(_document.Auras.Count);
            var label = NewDefaultAura(_document.Auras.Count + 1);
            icon.Name = "Spell Artwork (edit spell)";
            icon.Display = "Icon"; icon.Trigger = "Always";
            icon.ArtSource = "Matched Spell"; icon.ArtSpell = "Pyroclasm";
            icon.MatchName = "Pyroclasm"; icon.Width = 54; icon.Height = 54;
            icon.GroupId = group.Id; icon.GroupOrder = 0;
            icon.ShowTimer = false; icon.BackgroundOpacity = 75;
            label.Name = "Custom Spell Label";
            label.Display = "Text"; label.Trigger = "Always";
            label.ArtSource = "None"; label.LabelPattern = "CUSTOM SPELL";
            label.TextAlignment = "Centre"; label.TextSize = 16;
            label.BackgroundColour = "#201B22"; label.TextColour = "#FFFFFF";
            label.Width = 208; label.Height = 54; label.ShowTimer = false;
            label.GroupId = group.Id; label.GroupOrder = 1; label.ShowLabel = true;
            Sanitize(icon); Sanitize(label);
            _document.Groups.Add(group);
            _document.Auras.Add(icon); _document.Auras.Add(label);
            SelectedId = icon.Id;
            Save();
            return icon;
        }

        // Compound templates are a single transaction: check both limits before
        // inserting anything, and persist once. No half-created HUD on failure.
        private static CustomAuraDefinition CreateClassHudExample(CustomAuraHudRecipe recipe)
        {
            Ensure();
            if (recipe == null || _document.Auras.Count + 4 > Maximum ||
                _document.Groups.Count >= MaximumGroups) return null;

            var group = new CustomAuraGroupDefinition {
                Id = Guid.NewGuid().ToString("N"), Name = recipe.ClassName + " HUD",
                Enabled = true, Layout = "HUD Panel", Order = "Manual", HideInactive = true,
                Columns = 2, Spacing = 8, X = 740, Y = 350, Scale = 100
            };
            SanitizeGroup(group);

            var icon = NewDefaultAura(_document.Auras.Count);
            var readout = NewDefaultAura(_document.Auras.Count + 1);
            var gauge = NewDefaultAura(_document.Auras.Count + 2);
            var alert = NewDefaultAura(_document.Auras.Count + 3);
            CustomAuraDefinition[] members = { icon, readout, gauge, alert };
            for (int i = 0; i < members.Length; i++)
            {
                members[i].Folder = recipe.ClassName;
                members[i].LoadClass = recipe.ClassName;
                members[i].GroupId = group.Id;
                members[i].GroupOrder = i;
                members[i].Enabled = true;
                members[i].ShowTimer = false;
                members[i].Colour = recipe.Colour;
                members[i].TextColour = "#FFF4DE";
                members[i].BorderColour = recipe.Colour;
                members[i].BorderSize = 1;
            }

            // The icon is a persistent class/ability ornament. It does not
            // falsely promise spell readiness, and falls back to an initial if
            // the exact artwork is not registered.
            icon.Name = recipe.ClassName + " - Spell Icon";
            icon.Display = "Icon";
            icon.Trigger = "Always";
            icon.ArtSource = "Matched Spell";
            icon.ArtSpell = recipe.IconSpell;
            icon.MatchName = recipe.IconSpell;
            icon.Width = 70; icon.Height = 70;
            icon.BackgroundColour = "#302018";
            icon.BackgroundOpacity = 90;
            icon.ShowLabel = false;

            readout.Name = recipe.ClassName + " - " + recipe.ResourceLabel + " Readout";
            readout.Display = "Text"; readout.ArtSource = "None";
            readout.Trigger = recipe.ResourceTrigger;
            readout.Width = 264; readout.Height = 70;
            readout.BackgroundColour = "#201B22";
            readout.BackgroundOpacity = 85;
            readout.TextAlignment = "Centre"; readout.TextSize = 17;
            readout.LabelPattern = recipe.ResourceLabel + "  %v / %m";
            readout.ShowLabel = true; readout.ShowValue = true;
            readout.ShowWhenEmpty = true; readout.MinimumValue = 0;

            gauge.Name = recipe.ClassName + " - " + recipe.ResourceLabel + " Gauge";
            gauge.Trigger = recipe.ResourceTrigger;
            gauge.LinkedAuraId = readout.Id; // One source read shared with numeric text.
            gauge.Width = 342; gauge.Height = 32;
            gauge.ShowWhenEmpty = true; gauge.MinimumValue = 0;
            gauge.BackgroundColour = "#1D1820";
            gauge.BackgroundOpacity = 70;
            gauge.ShowLabel = false; gauge.ShowValue = false;
            if (recipe.ResourceTrigger == "Emberweaver Heat")
            {
                gauge.Display = "Charge Segments";
                gauge.Shape = "Rectangle";
                gauge.SegmentCount = 10; gauge.SegmentsPerRow = 10;
                gauge.SegmentSpacing = 5; gauge.BackgroundOpacity = 0;
                gauge.InactiveColour = "#49352B";
            }
            else
            {
                gauge.Display = "Progress Bar";
                gauge.FillDirection = "Left to Right";
            }

            alert.Name = recipe.ClassName + " - " + recipe.AlertLabel;
            alert.Display = "Text"; alert.ArtSource = "None";
            alert.Trigger = recipe.AlertTrigger;
            alert.MatchName = recipe.AlertName;
            alert.LabelPattern = recipe.AlertLabel;
            alert.Width = 342; alert.Height = 34;
            alert.TextAlignment = "Centre"; alert.TextSize = 14;
            alert.BackgroundColour = "#382318";
            alert.BackgroundOpacity = 88;
            alert.ShowLabel = true; alert.ShowValue = false;
            alert.ShowWhenEmpty = false; alert.MinimumValue = 1;
            alert.EnterAnimation = "Pop"; alert.ExitAnimation = "Fade";
            alert.TransitionMs = 220;
            alert.GlowStyle = "Pulse"; alert.GlowColour = recipe.Colour;
            alert.GlowWidth = 7; alert.GlowOpacity = 75;
            // Ready spells require a user-assigned hotkey; buffs and debuffs
            // require the corresponding native status to be present.

            foreach (var member in members) Sanitize(member);
            _document.Groups.Add(group);
            _document.Auras.AddRange(members);
            SelectedId = gauge.Id;
            Save();
            return gauge;
        }

        internal static CustomAuraDefinition Duplicate()
        {
            var source = Selected;
            if (source == null || _document.Auras.Count >= Maximum) return null;
            var clone = UnityEngine.JsonUtility.FromJson<CustomAuraDefinition>(UnityEngine.JsonUtility.ToJson(source));
            clone.Id = Guid.NewGuid().ToString("N"); clone.Name = Clip(source.Name + " Copy", 48, "New Aura");
            clone.X = Mathf.Clamp(source.X + 28, 0, 1820);
            clone.Y = Mathf.Clamp(source.Y + 28, 0, 980);
            clone.Enabled = false;
            _document.Auras.Add(clone); SelectedId = clone.Id; Save(); return clone;
        }
        internal static void DeleteSelected()
        {
            Ensure();
            string removedId = SelectedId;
            _document.Auras.RemoveAll(a => a.Id == SelectedId);
            foreach (var aura in _document.Auras)
                if (aura.LinkedAuraId == removedId) aura.LinkedAuraId = "";
            SelectedId = _document.Auras.Count > 0 ? _document.Auras[0].Id : null;
            Save();
        }
        internal static void Change(Action<CustomAuraDefinition> change)
        {
            var def = Selected;
            if (def == null || change == null) return;
            change(def); Sanitize(def); Save();
        }
        internal static void Move(string id, float x, float y)
        {
            Ensure();
            foreach (var def in _document.Auras)
                if (def.Id == id)
                {
                    def.X = Mathf.Clamp(Mathf.RoundToInt(x), 0, 1820);
                    def.Y = Mathf.Clamp(Mathf.RoundToInt(y), 0, 980);
                    Save(); return;
                }
        }
        internal static void SetDisplay(string display)
        {
            if (Array.IndexOf(Displays, display) < 0) return;
            Change(def =>
            {
                if (def.Display == display) return;
                def.Display = display;
                if (display == "Progress Bar" && def.Width < 120) { def.Width = 210; def.Height = 28; }
                else if (display == "Icon") { def.Width = 52; def.Height = 52; }
                else if (display == "Text") { def.Width = 180; def.Height = 32; }
                else if (display == "Shape") { def.Width = 64; def.Height = 64; }
                else if (display == "Charge Segments") { def.Width = 310; def.Height = 36; }
            });
        }
        internal static void SetTrigger(string trigger)
        {
            if (Array.IndexOf(Triggers, trigger) < 0) return;
            Change(def =>
            {
                def.Trigger = trigger;
                if (trigger == "Custom Expression" && string.IsNullOrWhiteSpace(def.ExpressionValue))
                {
                    def.ExpressionValue = "player_hp_pct";
                    def.ExpressionMaximum = "100";
                    def.ExpressionCondition = "player_hp > 0";
                    def.Display = "Text";
                    def.Width = 210;
                    def.ShowValue = true;
                    def.ShowLabel = true;
                    def.LabelPattern = "%n: %v";
                }
            });
        }
        internal static void CycleDisplay() { Cycle(Displays, true); }
        internal static void CycleTrigger() { Cycle(Triggers, false); }
        private static void Cycle(string[] choices, bool display)
        {
            Change(def =>
            {
                int idx = Array.IndexOf(choices, display ? def.Display : def.Trigger);
                if (display) def.Display = choices[(idx + 1) % choices.Length];
                else def.Trigger = choices[(idx + 1) % choices.Length];
                if (display && def.Display == "Progress Bar" && def.Width < 120) { def.Width = 210; def.Height = 28; }
                else if (display && def.Display == "Icon") { def.Width = 52; def.Height = 52; }
                else if (display && def.Display == "Text") { def.Width = 180; def.Height = 32; }
            });
        }
        internal static void AddTriggerRule()
        {
            Change(def =>
            {
                if (def.AdditionalTriggers.Count < MaxExtraTriggers)
                    def.AdditionalTriggers.Add(new CustomAuraTriggerRule());
            });
        }
        internal static void ChangeTriggerRule(int index, Action<CustomAuraTriggerRule> edit)
        {
            Change(def =>
            {
                if (index >= 0 && index < def.AdditionalTriggers.Count && edit != null)
                    edit(def.AdditionalTriggers[index]);
            });
        }
        internal static void RemoveTriggerRule(int index)
        {
            Change(def =>
            {
                if (index >= 0 && index < def.AdditionalTriggers.Count)
                    def.AdditionalTriggers.RemoveAt(index);
            });
        }
        internal static void AddCondition()
        {
            Change(def =>
            {
                if (def.Conditions.Count < MaxConditions)
                    def.Conditions.Add(new CustomAuraConditionRule());
            });
        }
        internal static void ChangeCondition(int index, Action<CustomAuraConditionRule> edit)
        {
            Change(def =>
            {
                if (index >= 0 && index < def.Conditions.Count && edit != null)
                    edit(def.Conditions[index]);
            });
        }
        internal static void RemoveCondition(int index)
        {
            Change(def =>
            {
                if (index >= 0 && index < def.Conditions.Count)
                    def.Conditions.RemoveAt(index);
            });
        }
        internal static CustomAuraGroupDefinition FindGroup(string id)
        {
            Ensure();
            foreach (var group in _document.Groups) if (group.Id == id) return group;
            return null;
        }
        internal static CustomAuraGroupDefinition CreateGroup()
        {
            Ensure();
            if (_document.Groups.Count >= MaximumGroups) return null;
            var group = new CustomAuraGroupDefinition { Id = Guid.NewGuid().ToString("N"),
                Name = "Aura Group " + (_document.Groups.Count + 1), Y = 340 + (_document.Groups.Count % 5) * 60 };
            _document.Groups.Add(group); Save(); return group;
        }
        internal static void ChangeGroup(string id, Action<CustomAuraGroupDefinition> action)
        {
            var group = FindGroup(id);
            if (group == null || action == null) return;
            action(group); SanitizeGroup(group); Save();
        }
        internal static void DeleteGroup(string id)
        {
            Ensure();
            _document.Groups.RemoveAll(g => g.Id == id);
            foreach (var def in _document.Auras) if (def.GroupId == id) def.GroupId = "";
            Save();
        }
        internal static void MoveGroup(string id, float x, float y)
        {
            ChangeGroup(id, g =>
            {
                if (g.SnapToGrid)
                {
                    int step = Mathf.Max(4, g.SnapSize);
                    x = Mathf.Round(x / step) * step;
                    y = Mathf.Round(y / step) * step;
                }
                g.X = Mathf.RoundToInt(x); g.Y = Mathf.RoundToInt(y);
            });
        }
        internal static bool MoveSelectedInGroup(int offset)
        {
            Ensure();
            var current = Selected;
            if (current == null || string.IsNullOrEmpty(current.GroupId) || offset == 0) return false;
            var members = new List<CustomAuraDefinition>();
            foreach (var candidate in _document.Auras)
                if (candidate.GroupId == current.GroupId) members.Add(candidate);
            members.Sort((a,b) =>
            {
                int order = a.GroupOrder.CompareTo(b.GroupOrder);
                return order != 0 ? order : string.Compare(a.Id,b.Id,StringComparison.Ordinal);
            });
            int index = members.FindIndex(a => a.Id == current.Id);
            int target = Mathf.Clamp(index + offset, 0, members.Count - 1);
            if (index < 0 || target == index) return false;
            var moved = members[index];
            members.RemoveAt(index); members.Insert(target, moved);
            for (int i = 0; i < members.Count; i++) members[i].GroupOrder = i;
            Save();
            return true;
        }
        internal static void ScaleGroupMembers(string groupId, float factor)
        {
            Ensure();
            if (FindGroup(groupId) == null) return;
            bool changed = false;
            foreach (var aura in _document.Auras)
            {
                if (aura.GroupId != groupId) continue;
                aura.Width = Mathf.Clamp(Mathf.RoundToInt(aura.Width * factor), 32, 480);
                aura.Height = Mathf.Clamp(Mathf.RoundToInt(aura.Height * factor), 22, 140);
                changed = true;
            }
            if (changed) Save();
        }

        internal static void LinkSelected(string id)
        {
            var current = Selected;
            if (current == null) return;
            if (string.IsNullOrEmpty(id)) { Change(def => def.LinkedAuraId = ""); return; }
            var seen = new HashSet<string>(StringComparer.Ordinal) { current.Id };
            string cursor = id;
            while (!string.IsNullOrEmpty(cursor))
            {
                if (!seen.Add(cursor)) return; // Prevent cycles at edit-time.
                CustomAuraDefinition candidate = null;
                foreach (var other in All) if (other.Id == cursor) { candidate = other; break; }
                if (candidate == null) return; // Do not create dangling references.
                cursor = candidate.LinkedAuraId;
            }
            Change(def => def.LinkedAuraId = id);
        }
        internal static bool CreateProfile(string name)
        {
            Ensure(); name = (name ?? "").Trim();
            if (!ValidProfileName(name) || Array.IndexOf(ProfileNames, name) >= 0 || ProfileNames.Length >= 16) return false;
            try
            {
                var index = new CustomAuraProfileIndex();
                foreach (string n in ProfileNames) if (n != "Default") index.Names.Add(n);
                index.Names.Add(name);
                PlayerPrefs.SetString(ProfileIndexKey, UnityEngine.JsonUtility.ToJson(index));
                PlayerPrefs.SetString(ProfileStorage(name), UnityEngine.JsonUtility.ToJson(_document));
                PlayerPrefs.Save();
                return true;
            }
            catch (Exception e) { Debug.LogWarning("[ArcaneUI Auras] Create profile failed: " + e.Message); return false; }
        }
        internal static bool SwitchProfile(string name)
        {
            if (Array.IndexOf(ProfileNames, name) < 0) return false;
            if (name == ActiveProfile) return true;
            Save();
            PlayerPrefs.SetString(ActiveProfileKey, name);
            PlayerPrefs.Save();
            Load();
            return true;
        }
        internal static bool DeleteProfile(string name)
        {
            if (name == "Default" || Array.IndexOf(ProfileNames, name) < 0) return false;
            if (ActiveProfile == name) SwitchProfile("Default");
            var index = new CustomAuraProfileIndex();
            foreach (string n in ProfileNames) if (n != name && n != "Default") index.Names.Add(n);
            PlayerPrefs.SetString(ProfileIndexKey, UnityEngine.JsonUtility.ToJson(index));
            PlayerPrefs.DeleteKey(ProfileStorage(name)); PlayerPrefs.Save();
            return true;
        }
        // Explicit clipboard export / import. Imports merge without overwriting
        // existing auras; remap group/auras IDs to protect saved links.
        internal static string ExportJson()
        { Ensure(); return UnityEngine.JsonUtility.ToJson(_document, true); }
        internal static bool PreviewImportJson(string json, out string message)
        {
            Ensure();
            message = "No valid profile data in clipboard.";
            if (string.IsNullOrEmpty(json) || json.Length > 1500000 || !json.Contains("\"Auras\"")) return false;
            try
            {
                var doc = UnityEngine.JsonUtility.FromJson<CustomAuraDocument>(json);
                if (doc == null || doc.Version != 1 || doc.Auras == null) return false;
                int groupCount = doc.Groups != null ? doc.Groups.Count : 0;
                if (_document.Auras.Count + doc.Auras.Count > Maximum ||
                    _document.Groups.Count + groupCount > MaximumGroups)
                { message = "Import exceeds 200-aura or 32-group capacity."; return false; }
                var auraIds = new HashSet<string>(StringComparer.Ordinal);
                foreach (var aura in doc.Auras)
                    if (aura == null || string.IsNullOrEmpty(aura.Id) || !auraIds.Add(aura.Id))
                    { message = "Invalid or duplicate imported aura ID."; return false; }
                var groupIds = new HashSet<string>(StringComparer.Ordinal);
                if (doc.Groups != null)
                    foreach (var group in doc.Groups)
                        if (group == null || string.IsNullOrEmpty(group.Id) || !groupIds.Add(group.Id))
                        { message = "Invalid or duplicate imported group ID."; return false; }
                message = "Ready to add " + doc.Auras.Count + " aura(s) and " + groupCount +
                    " group(s) to '" + ActiveProfile + "'. Existing entries are preserved. Press CONFIRM to import.";
                return true;
            }
            catch (Exception e)
            { message = "Invalid profile JSON: " + e.GetType().Name; return false; }
        }
        internal static bool ImportJson(string json, out string message)
        {
            Ensure();
            message = "Invalid import data.";
            if (string.IsNullOrEmpty(json) || json.Length > 1500000 ||
                !json.Contains("\"Auras\"")) return false;
            CustomAuraDocument imported;
            try { imported = UnityEngine.JsonUtility.FromJson<CustomAuraDocument>(json); }
            catch (Exception e) { message = "JSON parsing failed: " + e.Message; return false; }
            if (imported == null || imported.Version != 1 || imported.Auras == null) return false;
            if (imported.Auras.Count > Maximum || _document.Auras.Count + imported.Auras.Count > Maximum)
            { message = "Too many auras (200 maximum)."; return false; }
            if (imported.Groups == null) imported.Groups = new List<CustomAuraGroupDefinition>();
            if (_document.Groups.Count + imported.Groups.Count > MaximumGroups)
            { message = "Too many groups (32 maximum)."; return false; }
            var idMap = new Dictionary<string, string>(StringComparer.Ordinal);
            var groupMap = new Dictionary<string, string>(StringComparer.Ordinal);
            var groups = new List<CustomAuraGroupDefinition>();
            foreach (var original in imported.Groups)
            {
                if (original == null || string.IsNullOrEmpty(original.Id) || groupMap.ContainsKey(original.Id))
                { message = "Invalid or duplicate group ID in import."; return false; }
                var clone = UnityEngine.JsonUtility.FromJson<CustomAuraGroupDefinition>(UnityEngine.JsonUtility.ToJson(original));
                clone.Id = Guid.NewGuid().ToString("N"); groupMap.Add(original.Id, clone.Id);
                SanitizeGroup(clone); groups.Add(clone);
            }
            var auras = new List<CustomAuraDefinition>();
            foreach (var original in imported.Auras)
            {
                if (original == null || string.IsNullOrEmpty(original.Id) || idMap.ContainsKey(original.Id))
                { message = "Invalid or duplicate aura ID in import."; return false; }
                var clone = UnityEngine.JsonUtility.FromJson<CustomAuraDefinition>(UnityEngine.JsonUtility.ToJson(original));
                clone.Id = Guid.NewGuid().ToString("N"); idMap.Add(original.Id, clone.Id);
                Sanitize(clone); auras.Add(clone);
            }
            foreach (var clone in auras)
            {
                string mapped;
                if (!string.IsNullOrEmpty(clone.GroupId))
                    clone.GroupId = groupMap.TryGetValue(clone.GroupId, out mapped) ? mapped : "";
                if (!string.IsNullOrEmpty(clone.LinkedAuraId))
                    clone.LinkedAuraId = idMap.TryGetValue(clone.LinkedAuraId, out mapped) ? mapped : "";
            }
            _document.Groups.AddRange(groups); _document.Auras.AddRange(auras);
            if (auras.Count > 0) SelectedId = auras[0].Id;
            Save(); message = "Imported " + auras.Count + " auras and " + groups.Count + " groups.";
            return true;
        }
        internal static void TogglePositionEditing() { EditPositions = !EditPositions; Revision++; }
        internal static void SetEditing(bool enabled) { if (EditPositions == enabled) return; EditPositions = enabled; Revision++; }
        private static void Save()
        {
            Ensure();
            try
            {
                PlayerPrefs.SetString(ProfileStorage(ActiveProfile), UnityEngine.JsonUtility.ToJson(_document));
                PlayerPrefs.Save(); Revision++;
            }
            catch (Exception e) { Debug.LogWarning("[ArcaneUI Auras] Could not save displays: " + e.Message); }
        }
    }
}
