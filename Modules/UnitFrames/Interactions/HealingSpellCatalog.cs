using System;
using System.Collections.Generic;
using System.Linq;

namespace ArcanesGroupFrames
{
    internal static class HealingSpellCatalog
    {
        internal static List<Spell> GetAllHealingSpells()
        {
            List<Spell> result = new List<Spell>();

            if (GameData.SpellDatabase == null ||
                GameData.SpellDatabase.SpellDatabase == null)
            {
                return result;
            }

            foreach (Spell spell in GameData.SpellDatabase.SpellDatabase)
            {
                if (!IsHealingSpell(spell))
                {
                    continue;
                }

                result.Add(spell);
            }

            return result
                .OrderBy(GetClassSortName)
                .ThenBy(spell => spell.RequiredLevel)
                .ThenBy(spell => spell.SpellName)
                .ToList();
        }

        // This catalog is for real player click-casts, never every spell in the
        // global SpellDB. Native spells and optional FOTF wards use the same
        // three checks: current class, level, and the player's spellbook.
        internal static List<Spell> GetPlayerHealingSpells()
        {
            if (!HasPlayerSpellbook()) return new List<Spell>();

            return GetAllHealingSpells()
                .Where(IsPlayerEligibleSpell)
                .ToList();
        }

        private static bool HasPlayerSpellbook()
        {
            return GameData.PlayerStats != null &&
                   GameData.PlayerStats.CharacterClass != null &&
                   GameData.GM != null &&
                   GameData.GM.PlayerSpells != null &&
                   GameData.GM.PlayerSpells.KnownSpells != null;
        }

        // Also called immediately before StartSpell. A saved binding must not
        // become a free cast after changing character, class, or learned spells.
        internal static bool IsPlayerEligibleSpell(Spell spell)
        {
            if (!IsHealingSpell(spell) || !HasPlayerSpellbook() ||
                string.IsNullOrEmpty(spell.Id) ||
                spell.RequiredLevel > GameData.PlayerStats.Level ||
                !IsAllowedForCurrentClass(spell))
                return false;

            foreach (Spell known in GameData.GM.PlayerSpells.KnownSpells)
            {
                if (known != null &&
                    string.Equals(known.Id, spell.Id, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        private static bool IsAllowedForCurrentClass(Spell spell)
        {
            if (spell == null || spell.UsedBy == null || spell.UsedBy.Count == 0)
                return false;

            // Resolve FOTF's persisted class identity first. In particular a
            // Lightbringer backed by a native class must not inherit that
            // backing class's unrelated healing spells.
            Stats player = GameData.PlayerStats;
            string selected = RaidClassColors.ResolveClassNameForActor(player, player.MyName);
            if (string.IsNullOrWhiteSpace(selected)) return false;

            // FOTF ward records are uniquely Lightbringer-owned regardless
            // of any accidental native template class left in UsedBy.
            if (!string.IsNullOrEmpty(spell.Id) &&
                spell.Id.StartsWith("FOTF_LB_SPELL_", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(selected, "Lightbringer", StringComparison.OrdinalIgnoreCase))
                return false;

            foreach (Class allowed in spell.UsedBy)
            {
                if (allowed == null) continue;
                if (string.Equals(selected, allowed.ClassName, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(selected, allowed.DisplayName, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        internal static Spell FindById(string id)
        {
            if (string.IsNullOrEmpty(id) ||
                GameData.SpellDatabase == null ||
                GameData.SpellDatabase.SpellDatabase == null)
            {
                return null;
            }

            foreach (Spell spell in GameData.SpellDatabase.SpellDatabase)
            {
                if (spell != null &&
                    string.Equals(spell.Id, id, StringComparison.OrdinalIgnoreCase))
                {
                    return spell;
                }
            }

            return null;
        }

        internal static bool IsHealingSpell(Spell spell)
        {
            if (spell == null)
            {
                return false;
            }

            // Native heals and FOTF hybrid heal+absorb spells are Heal.
            // FOTF Lightbringer PURE wards are deliberately Beneficial/Shield
            // clones (of Hardened Skin), so filtering only Heal loses them.
            // Never include damage or arbitrary Beneficial spells.
            return spell.Type == Spell.SpellType.Heal ||
                   IsLightbringerPureAbsorb(spell);
        }


        // Only registered Lightbringer shield spells from the live SpellDB:
        // this never changes spell type/targeting and requires no FOTF DLL.
        internal static bool IsLightbringerPureAbsorb(Spell spell)
        {
            return IsLightbringerSupportSpell(spell) &&
                   spell.Type == Spell.SpellType.Beneficial &&
                   string.Equals(spell.Line.ToString(), "Shield",
                       StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsLightbringerSupportSpell(Spell spell)
        {
            return spell != null &&
                   !string.IsNullOrEmpty(spell.Id) &&
                   spell.Id.StartsWith("FOTF_LB_SPELL_", StringComparison.OrdinalIgnoreCase) &&
                   (spell.Type == Spell.SpellType.Heal ||
                    (spell.Type == Spell.SpellType.Beneficial &&
                     string.Equals(spell.Line.ToString(), "Shield",
                         StringComparison.OrdinalIgnoreCase)));
        }

        internal static string GetDisplayName(Spell spell)
        {
            if (spell == null)
            {
                return "None";
            }

            string className = GetClassSortName(spell);
            string prefix = string.IsNullOrEmpty(className)
                ? string.Empty
                : className + " - ";

            string category = IsLightbringerPureAbsorb(spell) ? "Absorb Ward, " :
                (IsLightbringerSupportSpell(spell) ? "Heal + Absorb, " : "");
            return prefix + spell.SpellName + " (" + category + "Lv " + spell.RequiredLevel + ")";
        }

        internal static void DumpAllHealingSpellsToLog()
        {
            List<Spell> spells = GetAllHealingSpells();
            int shields = 0;
            Plugin.LogInfo("[ArcaneUI ClickCast] SpellDB available friendly spells: " + spells.Count);
            foreach (Spell spell in spells)
            {
                if (IsLightbringerPureAbsorb(spell)) shields++;
                Plugin.LogInfo("[ArcaneUI ClickCast] " + spell.Id + " | " +
                    GetDisplayName(spell) + " | Type=" + spell.Type +
                    " | Line=" + spell.Line);
            }
            Plugin.LogInfo("[ArcaneUI ClickCast] FOTF Lightbringer pure absorb wards: " +
                shields + ". Currently learned, class/level-eligible spells: " + GetPlayerHealingSpells().Count);

        }

        private static bool IsHot(Spell spell)
        {
            return spell != null &&
                   spell.StatusEffectToApply != null &&
                   spell.StatusEffectToApply.TargetHealing > 0 &&
                   spell.StatusEffectToApply.SpellDurationInTicks > 0;
        }

        private static string GetClassSortName(Spell spell)
        {
            if (spell == null || spell.UsedBy == null || spell.UsedBy.Count == 0)
            {
                return string.Empty;
            }

            Class first = spell.UsedBy[0];

            if (first == null)
            {
                return string.Empty;
            }

            if (!string.IsNullOrEmpty(first.DisplayName))
            {
                return first.DisplayName;
            }

            return first.ClassName ?? string.Empty;
        }
    }
}
