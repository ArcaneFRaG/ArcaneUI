using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using ArcanesGroupFrames.Modules.UnitFrames.Target;

namespace ArcanesGroupFrames.Modules.CooldownAuras.UI
{
    /// <summary>
    /// Resolves registered native/FOTF spells without requiring a current buff,
    /// learned spell, assigned hotkey or an active cooldown. Read-only. An ambiguous
    /// partial name is deliberately rejected instead of selecting random artwork.
    /// Queries are cached; the spell registry is not searched on every HUD frame.
    /// </summary>
    internal static class ArcaneUiAuraSpellArtwork
    {
        private sealed class Hit
        {
            internal Spell Spell;
            internal bool Ambiguous;
            internal float RetryAfter;
        }
        private sealed class IconHit
        {
            internal Sprite Art;
            internal float RetryAfter;
        }
        private static readonly Dictionary<string, Hit> Cache = new Dictionary<string, Hit>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, IconHit> Icons = new Dictionary<string, IconHit>(StringComparer.OrdinalIgnoreCase);
        private static object _database;
        private static int _databaseCount = -1;

        internal static Spell Find(string query)
        {
            if (string.IsNullOrWhiteSpace(query)) return null;
            query = query.Trim();
            // Players often copy the native spellbook/scroll label verbatim.
            if (query.StartsWith("Spell:", StringComparison.OrdinalIgnoreCase))
                query = query.Substring(6).Trim();
            if (query.Length == 0) return null;
            var owner = GameData.SpellDatabase;
            var spells = owner == null ? null : owner.SpellDatabase;
            if (spells == null) { Cache.Clear(); Icons.Clear(); _database = null; _databaseCount = -1; return null; }
            int count = spells is System.Collections.ICollection ? ((System.Collections.ICollection)spells).Count : -1;
            if (!ReferenceEquals(_database, spells) || _databaseCount != count)
            {
                Cache.Clear(); Icons.Clear(); _database = spells; _databaseCount = count;
            }
            Hit hit;
            if (Cache.TryGetValue(query, out hit) && Time.unscaledTime < hit.RetryAfter)
                return hit.Spell;
            // Refresh a cached hit occasionally: FOTF can re-register a spell
            // without changing the count or identity of the SpellDB list.
            Spell byId = null, byName = null, partial = null;
            int partials = 0;
            foreach (Spell candidate in spells)
            {
                if (candidate == null) continue;
                if (string.Equals(candidate.Id, query, StringComparison.OrdinalIgnoreCase))
                { byId = candidate; break; }
                if (string.Equals(candidate.SpellName, query, StringComparison.OrdinalIgnoreCase))
                { byName = candidate; }
                else if (!string.IsNullOrEmpty(candidate.SpellName) &&
                    candidate.SpellName.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
                { partial = candidate; partials++; }
            }
            Spell found = byId ?? byName ?? (partials == 1 ? partial : null);
            Cache[query] = new Hit { Spell = found, Ambiguous = found == null && partials > 1,
                RetryAfter = Time.unscaledTime + 3f };
            return found;
        }

        internal static Sprite Icon(string query)
        {
            Spell spell = Find(query);
            if (spell == null) return null;
            string key = query == null ? "" : query.Trim();
            IconHit cache;
            if (Icons.TryGetValue(key, out cache) && Time.unscaledTime < cache.RetryAfter)
                return cache.Art;
            Sprite sprite = null;
            // "SpellIcon" is the actual Sprite field read by native Hotkeys.
            // Try typed members separately: the generic target adapter may see
            // an unrelated non-Sprite "Icon" member before "SpellIcon".
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            foreach (string name in new [] { "SpellIcon", "IconSprite", "SpellSprite", "MyIcon", "Icon" })
            {
                try
                {
                    Type type = spell.GetType();
                    FieldInfo field = type.GetField(name, flags);
                    if (field != null) sprite = field.GetValue(spell) as Sprite;
                    if (sprite == null)
                    {
                        PropertyInfo prop = type.GetProperty(name, flags);
                        if (prop != null && prop.GetIndexParameters().Length == 0)
                            sprite = prop.GetValue(spell, null) as Sprite;
                    }
                }
                catch (Exception) { }
                if (sprite != null) break;
            }
            if (sprite == null) sprite = TargetNativeReadAdapter.ReadSpellIcon(spell);
            Icons[key] = new IconHit { Art = sprite, RetryAfter = Time.unscaledTime + 3f };
            return sprite;
        }

        internal static string ResolvedName(string query)
        {
            Spell spell = Find(query);
            return spell == null ? null : spell.SpellName;
        }

        internal static string[] Suggestions(string filter, int max = 28)
        {
            var matches = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var db = GameData.SpellDatabase;
            if (db == null || db.SpellDatabase == null) return matches.ToArray();
            string text = (filter ?? "").Trim();
            foreach (Spell spell in db.SpellDatabase)
            {
                if (spell == null || string.IsNullOrWhiteSpace(spell.SpellName)) continue;
                if (text.Length > 0 && spell.SpellName.IndexOf(text, StringComparison.OrdinalIgnoreCase) < 0 &&
                    (spell.Id == null || spell.Id.IndexOf(text, StringComparison.OrdinalIgnoreCase) < 0)) continue;
                if (seen.Add(spell.SpellName)) matches.Add(spell.SpellName);
            }
            matches.Sort(StringComparer.OrdinalIgnoreCase);
            if (matches.Count > max) matches.RemoveRange(max, matches.Count - max);
            return matches.ToArray();
        }

        internal static Sprite Select(CustomAurasArtSelection selection, Sprite trigger, string overrideSpell, string match, string title)
        {
            if (selection == CustomAurasArtSelection.NoArtwork) return null;
            if (selection == CustomAurasArtSelection.TriggerOnly) return trigger;
            Sprite selected = Icon(overrideSpell);
            if (selected == null) selected = Icon(match);
            if (selected == null) selected = Icon(title);
            return selected != null ? selected : (selection == CustomAurasArtSelection.Auto ? trigger : null);
        }
    }

    internal enum CustomAurasArtSelection { Auto, SpellOnly, TriggerOnly, NoArtwork }
}
