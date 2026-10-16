using System;
using System.Collections.Generic;

namespace ArcanesGroupFrames.Modules.CooldownAuras.Editor
{
    // Preset catalogue. These are configuration recipes, NOT invented gameplay
    // resources. Every preset uses an existing native aura/cooldown or a
    // read-only FOTF resource already supported by the Phase 9.4 HUD.
    internal sealed class CustomAuraTemplate
    {
        internal readonly string Id, Folder, Name, Description, Display, Trigger, Match, Colour;
        internal readonly bool NeedsSetup;
        internal CustomAuraTemplate(string id, string folder, string name, string description,
            string display, string trigger, string match, string colour, bool needsSetup = false)
        {
            Id = id; Folder = folder; Name = name; Description = description;
            Display = display; Trigger = trigger; Match = match; Colour = colour;
            NeedsSetup = needsSetup;
        }
    }

    /// <summary>Verified resource types plus optional spell/effect examples.
    /// Only examples using an actual native status/cooldown are event-driven;
    /// the decorative class icon is always visible regardless of hotbar assignment.</summary>
    internal sealed class CustomAuraHudRecipe
    {
        internal readonly string TemplateId, ClassName, ResourceTrigger, ResourceLabel;
        internal readonly string IconSpell, AlertTrigger, AlertName, AlertLabel, Colour;
        internal CustomAuraHudRecipe(string id, string className, string resource, string label,
            string iconSpell, string alertTrigger, string alertName, string alertLabel, string colour)
        {
            TemplateId = id; ClassName = className; ResourceTrigger = resource; ResourceLabel = label;
            IconSpell = iconSpell; AlertTrigger = alertTrigger; AlertName = alertName;
            AlertLabel = alertLabel; Colour = colour;
        }
    }

    internal static class CustomAuraTemplates
    {
        private static readonly CustomAuraHudRecipe[] HudRecipes =
        {
            new CustomAuraHudRecipe("arc_hud", "Arcanist", "Player Mana", "MANA", "Dazzle", "Target Debuff", "Dazzle", "DAZZLE ON TARGET", "#9679DA"),
            new CustomAuraHudRecipe("dru_hud", "Druid", "Player Mana", "MANA", "Regrowth", "Player Buff", "Regrowth", "REGROWTH ACTIVE", "#6BBB72"),
            new CustomAuraHudRecipe("pal_hud", "Paladin", "Player Health", "HEALTH", "Moon Bolt", "Cooldown Ready", "Moon Bolt", "MOON BOLT READY", "#E2CB83"),
            new CustomAuraHudRecipe("rea_hud", "Reaver", "Player Health", "HEALTH", "Waste", "Target Debuff", "Waste", "WASTE ON TARGET", "#B94E5B"),
            new CustomAuraHudRecipe("sto_hud", "Stormcaller", "Player Mana", "MANA", "Imbued Arrow", "Cooldown Ready", "Imbued Arrow", "IMBUED ARROW READY", "#67B9DA"),
            new CustomAuraHudRecipe("win_hud", "Windblade", "Player Health", "HEALTH", "Expose Weakness", "Target Debuff", "Expose Weakness", "EXPOSE WEAKNESS", "#69C7B4"),
            new CustomAuraHudRecipe("bli_hud", "Blightcaller", "Player Mana", "MANA", "Withering Touch", "Target Debuff", "Withering Touch", "WITHERING TOUCH", "#9362CD"),
            new CustomAuraHudRecipe("emb_hud", "Emberweaver", "Emberweaver Heat", "HEAT", "Pyroclasm", "Emberweaver Flashfire", "", "FLASHFIRE READY", "#FF923B"),
            new CustomAuraHudRecipe("lig_hud", "Lightbringer", "Player Mana", "MANA", "Flicker Ward", "Player Buff", "Flicker Ward", "FLICKER WARD ACTIVE", "#FFFFFF")
        };

        internal static CustomAuraHudRecipe FindHudRecipe(string id)
        {
            foreach (var item in HudRecipes) if (item.TemplateId == id) return item;
            return null;
        }

        private static readonly CustomAuraTemplate[] Templates =
        {
            // General utilities applicable to any class.
            T("g_health", "General", "Health Percentage", "Live player health progress.", "Progress Bar", "Player Health", "", "#C95755"),
            T("g_mana", "General", "Mana Reserve", "Live player mana progress.", "Progress Bar", "Player Mana", "", "#478AD8"),
            T("g_buff", "General", "Track a Self Buff", "Choose a buff name in Trigger before enabling.", "Icon", "Player Buff", "", "#63B97A", true),
            T("g_debuff", "General", "Track a Target Debuff", "Choose an effect in Trigger before enabling.", "Icon", "Target Debuff", "", "#CA736D", true),
            T("g_ready", "General", "Spell Ready Alert", "Choose an action assigned to a hotkey.", "Icon", "Cooldown Ready", "", "#E0B869", true),

            T("g_targethealth", "General", "Target Health", "Current target health, directly from native Stats.", "Progress Bar", "Target Health", "", "#D67272"),
            T("g_targetmana", "General", "Target Mana", "Current target mana, directly from native Stats.", "Progress Bar", "Target Mana", "", "#6499DD"),
            T("g_absorb", "General", "Shield Remaining", "Shows native remaining absorption, not estimated original capacity.", "Text", "Player Absorb", "", "#B9DBF2"),
            T("g_healthring", "General", "Health Radial Gauge", "Sweeping circular health gauge with adjustable thickness, direction and text.", "Shape", "Player Health", "", "#D96D66"),
            T("g_icontext", "General", "Spell Icon + Label", "Creates a two-element compact group to customise an icon and text independently.", "Icon", "Always", "", "#E0B869"),

            // Six native Erenshor classes. Names below are registered ability
            // examples: detection still depends on the ability being learned,
            // equipped or assigned, and on native effect visibility.
            T("arc_hud", "Arcanist", "Arcanist HUD Example", "Class icon, live mana bar, numeric mana and Dazzle alert in a grouped HUD.", "Progress Bar", "Player Mana", "", "#9679DA"),
            T("arc_mana", "Arcanist", "Mana Focus", "Track the Arcanist's mana pool.", "Progress Bar", "Player Mana", "", "#9679DA"),
            T("arc_dazzle", "Arcanist", "Dazzle Target", "Watch for Dazzle on the current target.", "Icon", "Target Debuff", "Dazzle", "#9E83EE"),
            T("arc_skin", "Arcanist", "Hardened Skin", "Show Hardened Skin if exposed as a player buff.", "Icon", "Player Buff", "Hardened Skin", "#759AE2"),
            T("arc_targetmana", "Arcanist", "Target Mana", "Read target mana for spellcasting decisions.", "Progress Bar", "Target Mana", "", "#A5A2EE"),
            T("arc_bolt", "Arcanist", "Magic Bolt Ready", "Watch an assigned Magic Bolt hotkey.", "Icon", "Cooldown Ready", "Magic Bolt", "#A7A0F0"),

            T("dru_hud", "Druid", "Druid HUD Example", "Group a class icon, live mana bar, numerical readout and Regrowth alert.", "Progress Bar", "Player Mana", "", "#6BBB72"),
            T("dru_mana", "Druid", "Nature Mana", "Track mana used by heals and nature spells.", "Progress Bar", "Player Mana", "", "#6BBB72"),
            T("dru_poison", "Druid", "Weak Poison", "Track Weak Poison on the current target.", "Icon", "Target Debuff", "Weak Poison", "#74B95E"),
            T("dru_regrowth", "Druid", "Regrowth", "Show Regrowth when visible as a player buff.", "Icon", "Player Buff", "Regrowth", "#A3C85F"),
            T("dru_target", "Druid", "Healing Target Health", "Read the selected target's native health.", "Progress Bar", "Target Health", "", "#88CF91"),
            T("dru_heal", "Druid", "Healing Ready", "Watch an assigned Minor Healing cooldown.", "Icon", "Cooldown Ready", "Minor Healing", "#93D1B4"),

            T("pal_hud", "Paladin", "Paladin HUD Example", "Group a class icon, live health bar, numerical readout and Flicker Ward alert.", "Progress Bar", "Player Health", "", "#E2CB83"),
            T("pal_health", "Paladin", "Tank Health", "Large defensive health indicator.", "Progress Bar", "Player Health", "", "#E2CB83"),
            T("pal_lunar", "Paladin", "Lunar Madness Ready", "Watch an assigned Lunar Madness action.", "Icon", "Cooldown Ready", "Lunar Madness", "#E7BE78"),
            T("pal_moon", "Paladin", "Moon Bolt Ready", "Watch an assigned Moon Bolt action.", "Icon", "Cooldown Ready", "Moon Bolt", "#D1D9E9"),
            T("pal_absorb", "Paladin", "Active Shield Remaining", "Show the current native shield amount (not a percentage).", "Text", "Player Absorb", "", "#E6D3A4"),
            T("pal_buff", "Paladin", "Defensive Buff", "Choose your defensive buff name in Trigger.", "Icon", "Player Buff", "", "#E8D5A8", true),

            T("rea_hud", "Reaver", "Reaver HUD Example", "Group a class icon, live health bar, numerical readout and Waste alert.", "Progress Bar", "Player Health", "", "#B94E5B"),
            T("rea_health", "Reaver", "Bloodied Health", "Track health for low-health combat play.", "Progress Bar", "Player Health", "", "#B94E5B"),
            T("rea_stance", "Reaver", "Aggressive Stance", "Watch the assigned Aggressive Stance action.", "Icon", "Cooldown Ready", "Aggressive Stance", "#C96860"),
            T("rea_waste", "Reaver", "Waste Debuff", "Track Waste on the current target.", "Icon", "Target Debuff", "Waste", "#BD658C"),
            T("rea_target", "Reaver", "Enemy Health", "Selected enemy's native health.", "Progress Bar", "Target Health", "", "#D56472"),
            T("rea_void", "Reaver", "Void Bolt Ready", "Watch an assigned Void Bolt cooldown.", "Icon", "Cooldown Ready", "Void Bolt", "#B47ED4"),

            T("sto_hud", "Stormcaller", "Stormcaller HUD Example", "Group a class icon, live mana bar, numerical readout and Imbued Arrow alert.", "Progress Bar", "Player Mana", "", "#67B9DA"),
            T("sto_mana", "Stormcaller", "Lightning Mana", "Track spell-bow mana.", "Progress Bar", "Player Mana", "", "#67B9DA"),
            T("sto_imbue", "Stormcaller", "Imbued Arrow Ready", "Track your assigned Imbued Arrow skill.", "Icon", "Cooldown Ready", "Imbued Arrow", "#5DD6E9"),
            T("sto_bolt", "Stormcaller", "Storm Bolt Ready", "Watch an assigned Storm Bolt action.", "Icon", "Cooldown Ready", "Storm Bolt", "#89CDF2"),
            T("sto_target", "Stormcaller", "Target Health", "Selected enemy's native health.", "Progress Bar", "Target Health", "", "#8AC9DC"),
            T("sto_arc", "Stormcaller", "Arcstorm Cooldown", "Track an assigned Arcstorm cooldown.", "Icon", "Cooldown Active", "Arcstorm", "#9ADCE5"),

            T("win_hud", "Windblade", "Windblade HUD Example", "Group a class icon, live health bar, numerical readout and Expose Weakness alert.", "Progress Bar", "Player Health", "", "#69C7B4"),
            T("win_health", "Windblade", "Lifesteal Health", "Track survivability while attacking.", "Progress Bar", "Player Health", "", "#69C7B4"),
            T("win_expose", "Windblade", "Expose Weakness", "Track Expose Weakness on your target.", "Icon", "Target Debuff", "Expose Weakness", "#87C6A3"),
            T("win_breeze", "Windblade", "Vithean Breeze Ready", "Watch an assigned Vithean Breeze action.", "Icon", "Cooldown Ready", "Vithean Breeze", "#8AC6CB"),
            T("win_target", "Windblade", "Enemy Health", "Selected enemy's health, useful for finishing windows.", "Progress Bar", "Target Health", "", "#94CABA"),
            T("win_embrace", "Windblade", "Death's Embrace Ready", "Watch an assigned Death's Embrace action.", "Icon", "Cooldown Ready", "Death's Embrace", "#A7B2DC"),

            // FOTF custom classes. Heat/Flashfire use the read-only FOTF bridge.
            T("bli_hud", "Blightcaller", "Blightcaller HUD Example", "Group a class icon, live mana bar, numerical readout and Withering Touch alert.", "Progress Bar", "Player Mana", "", "#9362CD"),
            T("bli_mana", "Blightcaller", "Blight Mana", "Track mana available for afflictions.", "Progress Bar", "Player Mana", "", "#9362CD"),
            T("bli_wither", "Blightcaller", "Withering Touch", "Watch Withering Touch on your target.", "Icon", "Target Debuff", "Withering Touch", "#AE77DA"),
            T("bli_shadow", "Blightcaller", "Shadow Rend", "Watch Shadow Rend on your target.", "Icon", "Target Debuff", "Shadow Rend", "#9163C9"),
            T("bli_target", "Blightcaller", "Blighted Target Health", "Selected enemy's native health.", "Progress Bar", "Target Health", "", "#A47ACF"),
            T("bli_blight", "Blightcaller", "Blight Ready", "Watch the assigned Blight action.", "Icon", "Cooldown Ready", "Blight", "#BC93E0"),

            T("emb_heat", "Emberweaver", "Heat (10 Charges)", "Live FOTF Heat segmented-charge display.", "Charge Segments", "Emberweaver Heat", "", "#FF923B"),
            T("emb_heatring", "Emberweaver", "Heat Radial Gauge", "Ring gauge filled by the confirmed live FOTF Heat fraction.", "Shape", "Emberweaver Heat", "", "#FF923B"),
            T("emb_hud", "Emberweaver", "Emberweaver HUD Example", "Creates a grouped Emberweaver mini-HUD with linked Heat displays and a Flashfire alert.", "Charge Segments", "Emberweaver Heat", "", "#FF923B"),
            T("emb_flash", "Emberweaver", "Flashfire Proc", "Show the live FOTF Flashfire proc.", "Icon", "Emberweaver Flashfire", "Flashfire", "#FFAF63"),
            T("emb_pyro", "Emberweaver", "Pyroclasm Ready", "Show ready state including instant cooldown resets.", "Icon", "Cooldown Ready", "Pyroclasm", "#F08742"),
            T("emb_heatfull", "Emberweaver", "Full Heat Alert", "A ten-charge Heat display with an at-max conditional glow.", "Charge Segments", "Emberweaver Heat", "", "#FF8430"),
            T("emb_mana", "Emberweaver", "Fire Mana", "Track mana while building Heat.", "Progress Bar", "Player Mana", "", "#F5A45E"),
            T("emb_wildfire", "Emberweaver", "Wildfire Stored Damage (Target)", "Live outstanding Wildfire stored on the current target, including pending Scorch pulses. Uncapped damage number from FOTF.", "Text", "Emberweaver Wildfire Stored", "", "#FF693D"),
            T("emb_combust", "Emberweaver", "Projected Combust (Target)", "Optional projection for the current target at current Heat, read directly from FOTF. Zero if Combust is not ready.", "Text", "Emberweaver Combust Projected", "", "#FFC56E"),

            T("lig_hud", "Lightbringer", "Lightbringer HUD Example", "Group a class icon, live health bar, numerical readout and Flicker Ward alert.", "Progress Bar", "Player Health", "", "#FFFFFF"),
            T("lig_health", "Lightbringer", "Guardian Health", "Track defensive health for a Lightbringer.", "Progress Bar", "Player Health", "", "#FFFFFF"),
            T("lig_ward", "Lightbringer", "Flicker Ward", "Show the native Flicker Ward buff, if active.", "Icon", "Player Buff", "Flicker Ward", "#C6DEFF"),
            T("lig_dawn", "Lightbringer", "Dawn's Sanctuary Ready", "Track an assigned Dawn's Sanctuary cooldown.", "Icon", "Cooldown Ready", "Dawn's Sanctuary", "#FFE0A0"),
            T("lig_absorb", "Lightbringer", "Ward Absorb Remaining", "Native remaining shield as a number, avoiding false percentage.", "Text", "Player Absorb", "", "#DFECFF"),
            T("lig_mana", "Lightbringer", "Holy Mana", "Track mana available for wards and heals.", "Progress Bar", "Player Mana", "", "#E4F0FF"),

        };

        private static CustomAuraTemplate T(string id, string folder, string name, string description,
            string display, string trigger, string match, string colour, bool needsSetup = false)
        { return new CustomAuraTemplate(id, folder, name, description, display, trigger, match, colour, needsSetup); }

        internal static int Count { get { return Templates.Length; } }
        internal static CustomAuraTemplate Find(string id)
        {
            foreach (var item in Templates)
                if (item.Id == id) return item;
            return null;
        }
        internal static CustomAuraTemplate[] ForFolder(string folder)
        {
            var found = new List<CustomAuraTemplate>();
            foreach (var item in Templates) if (item.Folder == folder) found.Add(item);
            return found.ToArray();
        }
    }
}
