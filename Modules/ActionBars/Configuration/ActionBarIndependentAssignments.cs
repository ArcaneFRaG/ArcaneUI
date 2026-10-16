using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using ArcanesGroupFrames.Modules.ActionBars.Native;

namespace ArcanesGroupFrames.Modules.ActionBars.Configuration
{
    /// <summary>
    /// Phase 7.3: character-scoped, independently saved action identities.
    /// Import requires an actual native Hotkeys assignment; we never grant or
    /// infer spells/skills/items from the entire game database.
    /// No Unity object references are serialized to PlayerPrefs.
    /// </summary>
    internal static class ActionBarIndependentAssignments
    {
        internal sealed class Assignment
        {
            internal string Kind;
            internal string Identity;
            internal string Name;
            internal bool IsValid { get { return !string.IsNullOrEmpty(Kind) && !string.IsNullOrEmpty(Identity); } }
        }
        private sealed class CachedAction
        {
            internal Stats Owner;
            internal object Value;
        }
        private const string Prefix = "ArcaneUI.ActionBars.Independent.v1.";
        private const string OwnersListKey = Prefix + "KnownCharacters";
        private static readonly Dictionary<string, CachedAction> RuntimeActions = new Dictionary<string, CachedAction>();
        private static Stats _lastOwner;
        private static Stats _cachedOwnerStats;
        private static string _cachedOwnerName;
        private static string _cachedOwnerKey;
        private static readonly Dictionary<int, Assignment> AssignmentCache = new Dictionary<int, Assignment>();
        private static float _nextDiagnostic;
        // Gameplay execution always delegates to Erenshor's native Hotkeys
        // action handler; ArcaneUI stores only extra bar assignments/bindings.
        private static string OwnerKey
        {
            get
            {
                Stats stats = GameData.PlayerStats;
                string name = stats == null ? null : stats.MyName;
                if (!object.ReferenceEquals(_cachedOwnerStats, stats) ||
                    !string.Equals(_cachedOwnerName, name, StringComparison.Ordinal))
                {
                    _cachedOwnerStats = stats;
                    _cachedOwnerName = name;
                    _cachedOwnerKey = string.IsNullOrEmpty(name) ? null : Uri.EscapeDataString(name.Trim());
                    AssignmentCache.Clear();
                }
                return _cachedOwnerKey;
            }
        }
        private static string Key(int bar, int slot)
        {
            string owner = OwnerKey;
            return owner == null ? null : OwnerSlotKey(owner, bar, slot);
        }
        private static string OwnerSlotKey(string owner, int bar, int slot)
        {
            return Prefix + owner + ".Bar" + bar + ".Slot" + slot + ".";
        }
        private static string[] KnownOwners()
        {
            string stored = PlayerPrefs.GetString(OwnersListKey, "");
            string owner = OwnerKey;
            if (string.IsNullOrEmpty(stored)) return owner == null ? new string[0] : new [] { owner };
            var owners = new List<string>(stored.Split(new [] { '|' }, StringSplitOptions.RemoveEmptyEntries));
            if (owner != null && !owners.Contains(owner)) owners.Add(owner);
            return owners.ToArray();
        }
        private static void RegisterOwner(string owner)
        {
            if (string.IsNullOrEmpty(owner)) return;
            string stored = PlayerPrefs.GetString(OwnersListKey, "");
            var owners = new List<string>(stored.Split(new [] { '|' }, StringSplitOptions.RemoveEmptyEntries));
            if (owners.Contains(owner)) return;
            owners.Add(owner);
            PlayerPrefs.SetString(OwnersListKey, string.Join("|", owners.ToArray()));
        }
        private static Assignment ReadForOwner(string owner, int bar, int slot)
        {
            int cacheSlot = bar * 32 + slot;
            bool current = string.Equals(owner, OwnerKey, StringComparison.Ordinal);
            Assignment cached;
            if (current && AssignmentCache.TryGetValue(cacheSlot, out cached)) return cached;
            string key = OwnerSlotKey(owner, bar, slot);
            var action = new Assignment
            {
                Kind = PlayerPrefs.GetString(key + "Kind", ""),
                Identity = PlayerPrefs.GetString(key + "Identity", ""),
                Name = PlayerPrefs.GetString(key + "Name", "")
            };
            Assignment result = action.IsValid ? action : null;
            if (current) AssignmentCache[cacheSlot] = result;
            return result;
        }
        private static void WriteForOwner(string owner, int bar, int slot, Assignment action)
        {
            if (string.Equals(owner, OwnerKey, StringComparison.Ordinal))
                AssignmentCache[bar * 32 + slot] = action != null && action.IsValid ? action : null;
            string key = OwnerSlotKey(owner, bar, slot);
            if (action == null || !action.IsValid)
            {
                PlayerPrefs.DeleteKey(key + "Kind");
                PlayerPrefs.DeleteKey(key + "Identity");
                PlayerPrefs.DeleteKey(key + "Name");
            }
            else
            {
                RegisterOwner(owner);
                PlayerPrefs.SetString(key + "Kind", action.Kind);
                PlayerPrefs.SetString(key + "Identity", action.Identity);
                PlayerPrefs.SetString(key + "Name", action.Name ?? "");
            }
        }
        private static bool IsUsableSlot(int bar, int slot)
        {
            return bar > 0 && bar < ActionBarPlanSettings.Count &&
                slot >= 1 && slot <= ActionBarPlanSettings.SlotCount(bar) && OwnerKey != null;
        }
        private static string CacheKey(Assignment assignment)
        {
            return assignment.Kind + ":" + assignment.Identity;
        }
        private static void RefreshOwner()
        {
            if (object.ReferenceEquals(_lastOwner, GameData.PlayerStats)) return;
            _lastOwner = GameData.PlayerStats;
            RuntimeActions.Clear();
        }
        internal static Assignment Get(int bar, int slot)
        {
            if (!IsUsableSlot(bar, slot)) return null;
            return ReadForOwner(OwnerKey, bar, slot);
        }
        private static void Write(int bar, int slot, Assignment action, bool save)
        {
            // Internal lifecycle operations may clear a bar before Count is incremented
            // or after its index has been shifted. Do not require a live bar here.
            if (bar <= 0 || bar >= ActionBarPlanSettings.MaxBars || slot < 1 ||
                slot > ActionBarPlanSettings.MaxSlots || OwnerKey == null) return;
            WriteForOwner(OwnerKey, bar, slot, action);
            if (save) PlayerPrefs.Save();
        }
        internal static void Clear(int bar, int slot)
        {
            Write(bar, slot, null, true);
        }
        // Deliberately does not query a global game database for learned spells.
        // Called after a REAL native extra Hotkeys component accepts an action
        // from Erenshor's spellbook, skillbook or inventory UI.
        internal static void StoreLive(int bar, int slot, string kind, string identity,
            string name, object value)
        {
            if (!IsUsableSlot(bar, slot) || string.IsNullOrEmpty(identity) ||
                (kind != "Spell" && kind != "Skill" && kind != "Item")) return;
            var action = new Assignment { Kind = kind, Identity = identity, Name = name ?? identity };
            RefreshOwner();
            if (value != null)
                RuntimeActions[CacheKey(action)] = new CachedAction { Owner = GameData.PlayerStats, Value = value };
            Write(bar, slot, action, true);
            ActionBarPlanSettings.SetSourceSlot(bar, slot, 0);
        }
        internal static bool CaptureNative(int bar, int slot, int nativeSlot, out string message)
        {
            message = "Unable to capture this native action.";
            if (!IsUsableSlot(bar, slot)) { message = "Select an additional bar and a valid slot while logged in."; return false; }
            object value;
            string kind, identity, name;
            if (!NativeActionBarDiscovery.TryReadNativeAssignment(nativeSlot, out kind, out identity, out name, out value))
            {
                message = "HK" + nativeSlot + " is empty or the assignment has no verifiable persistent identity.";
                return false;
            }
            if (kind != "Spell" && kind != "Skill" && kind != "Item")
            {
                message = "Unsupported native action kind: " + kind;
                return false;
            }
            var assignment = new Assignment { Kind = kind, Identity = identity, Name = name };
            RefreshOwner();
            RuntimeActions[CacheKey(assignment)] = new CachedAction { Owner = GameData.PlayerStats, Value = value };
            Write(bar, slot, assignment, true);
            ActionBarPlanSettings.SetSourceSlot(bar, slot, 0);
            message = "Copied " + kind + " '" + name + "' from HK" + nativeSlot + " into its own saved slot.";
            return true;
        }
        internal static bool CaptureSpellbookSpell(int bar, int slot, Spell spell, out string message)
        {
            message = "Spellbook entry was not recognised.";
            if (!IsUsableSlot(bar, slot) || spell == null || string.IsNullOrEmpty(spell.Id)) return false;
            // Caller accepts only live native spellbook UI entries. Never expose
            // this as a global SpellDB picker or arbitrary spell-ID editor.
            var assignment = new Assignment { Kind = "Spell", Identity = spell.Id,
                Name = spell.SpellName ?? spell.Id };
            RefreshOwner();
            RuntimeActions[CacheKey(assignment)] = new CachedAction { Owner = GameData.PlayerStats, Value = spell };
            Write(bar, slot, assignment, true);
            ActionBarPlanSettings.SetSourceSlot(bar, slot, 0);
            message = "Assigned spellbook spell '" + assignment.Name + "' to the extra slot.";
            return true;
        }

        internal static bool Move(int fromBar, int fromSlot, int toBar, int toSlot, out string message)
        {
            message = "Only independently assigned additional-bar slots can be moved.";
            if (!IsUsableSlot(fromBar, fromSlot) || !IsUsableSlot(toBar, toSlot)) return false;
            if (fromBar == toBar && fromSlot == toSlot) return false;
            Assignment action = Get(fromBar, fromSlot);
            if (action == null) return false;
            if (ActionBarPlanSettings.SourceSlot(toBar, toSlot) > 0 && Get(toBar, toSlot) == null)
            {
                message = "Clear the destination's legacy native link before moving an independent action there.";
                return false;
            }
            Assignment replaced = Get(toBar, toSlot);
            Write(toBar, toSlot, action, false);
            Write(fromBar, fromSlot, replaced, false); // swap; never silently lose the target assignment
            ActionBarPlanSettings.SetSourceSlot(toBar, toSlot, 0);
            ActionBarPlanSettings.SetSourceSlot(fromBar, fromSlot, 0);
            PlayerPrefs.Save();
            message = "Swapped independent action assignments.";
            return true;
        }
        // The source survives. Copy into an empty additional slot only; never
        // silently overwrite an existing action or erase a native linked slot.
        internal static bool Copy(int fromBar, int fromSlot, int toBar, int toSlot, out string message)
        {
            message = "Only saved additional-bar assignments can be copied.";
            if (!IsUsableSlot(fromBar, fromSlot) || !IsUsableSlot(toBar, toSlot) ||
                (fromBar == toBar && fromSlot == toSlot)) return false;
            Assignment original = Get(fromBar, fromSlot);
            if (original == null) return false;
            if (Get(toBar, toSlot) != null || ActionBarPlanSettings.SourceSlot(toBar, toSlot) > 0)
            {
                message = "Destination is occupied. Clear it first or drag without Ctrl to swap.";
                return false;
            }
            Write(toBar, toSlot, original, true);
            message = "Copied " + original.Name + ". Original action retained.";
            return true;
        }

        // Layout restoration is limited to the CURRENT character. Never delete
        // assignments belonging to other characters, even when bar counts differ.
        internal static void RestoreCurrentOwner(ActionBarLayoutPresets.BarRecord[] bars)
        {
            string owner = OwnerKey;
            if (owner == null || bars == null) return;
            for (int bar = 1; bar < ActionBarPlanSettings.MaxBars; ++bar)
                for (int slot = 1; slot <= ActionBarPlanSettings.MaxSlots; ++slot)
                    WriteForOwner(owner, bar, slot, null);
            for (int bar = 1; bar < bars.Length; ++bar)
                for (int slot = 1; slot <= bars[bar].Entries.Length; ++slot)
                {
                    var entry = bars[bar].Entries[slot - 1];
                    if (entry == null || string.IsNullOrEmpty(entry.Identity)) continue;
                    WriteForOwner(owner, bar, slot, new Assignment {
                        Kind = entry.Kind, Identity = entry.Identity, Name = entry.Name ?? entry.Identity });
                }
        }
        internal static void RemoveStoredSlot(int bar, int slot)
        {
            if (bar <= 0 || bar >= ActionBarPlanSettings.MaxBars ||
                slot < 1 || slot > ActionBarPlanSettings.MaxSlots) return;
            // Bar geometry is global, but assignments are character-scoped:
            // clearing/deleting a bar must not leave orphaned actions belonging
            // to another character that will later inherit the same bar index.
            foreach (string owner in KnownOwners()) WriteForOwner(owner, bar, slot, null);
        }
        internal static void CopyStoredSlot(int sourceBar, int destinationBar, int slot)
        {
            if (sourceBar <= 0 || destinationBar <= 0) return;
            // Reindex every known character's action identities when a bar is removed.
            foreach (string owner in KnownOwners())
                WriteForOwner(owner, destinationBar, slot, ReadForOwner(owner, sourceBar, slot));
        }
        internal static bool TryResolve(Assignment action, out object value)
        {
            value = null;
            if (action == null || !action.IsValid || GameData.PlayerStats == null) return false;
            RefreshOwner();
            CachedAction cached;
            if (action.Kind != "Item" && RuntimeActions.TryGetValue(CacheKey(action), out cached) &&
                object.ReferenceEquals(cached.Owner, GameData.PlayerStats) &&
                cached.Value != null && (!(cached.Value is UnityEngine.Object) || (UnityEngine.Object)cached.Value != null))
            {
                value = cached.Value;
                return true;
            }
            if (action.Kind == "Spell")
            {
                // Stable Spell.Id is resolved anew after zoning / character changes.
                if (GameData.SpellDatabase == null || GameData.SpellDatabase.SpellDatabase == null) return false;
                foreach (Spell spell in GameData.SpellDatabase.SpellDatabase)
                    if (spell != null && string.Equals(spell.Id, action.Identity, StringComparison.Ordinal))
                    {
                        value = spell;
                        RuntimeActions[CacheKey(action)] = new CachedAction { Owner = GameData.PlayerStats, Value = spell };
                        return true;
                    }
                return false;
            }
            if (action.Kind == "Skill" && GameData.SkillDatabase != null)
            {
                // GetSkillByID is the same native lookup used by ExtendedHotbars
                // to restore skill assignments. Only previously captured IDs
                // from a live skill/hotbar are stored in this class.
                var skill = GameData.SkillDatabase.GetSkillByID(action.Identity);
                if (skill != null)
                {
                    value = skill;
                    RuntimeActions[CacheKey(action)] = new CachedAction { Owner = GameData.PlayerStats, Value = skill };
                    return true;
                }
            }
            if (action.Kind == "Item" && GameData.PlayerInv != null && GameData.PlayerInv.ALLSLOTS != null)
            {
                if (action.Identity.StartsWith("InvSlot:", StringComparison.Ordinal))
                {
                    int index;
                    if (int.TryParse(action.Identity.Substring(8), out index) && index >= 0)
                    {
                        ItemIcon current = null;
                        int n = 0;
                        foreach (ItemIcon slotIcon in GameData.PlayerInv.ALLSLOTS)
                        { if (n++ == index) { current = slotIcon; break; } }
                        if (current != null && current.MyItem != null)
                        { value = current; return true; }
                    }
                    return false;
                }
                // Resolve the CURRENT inventory icon, never a stale ItemIcon
                // component from a previous scene. An item moved to another
                // inventory slot should still work when the ID is unchanged.
                foreach (ItemIcon icon in GameData.PlayerInv.ALLSLOTS)
                    if (icon != null && icon.MyItem != null &&
                        NativeActionBarDiscovery.ItemIdentity(icon) == action.Identity)
                    {
                        value = icon;
                        return true;
                    }
                return false;
            }
            // Skills and item instances cannot safely be reconstructed from
            // a name alone. Require the same current player and a live native
            // assignment, or the exactly captured runtime object this session.
            for (int i = 1; i <= 10; i++)
            {
                string kind, identity, name;
                object native;
                if (NativeActionBarDiscovery.TryReadNativeAssignment(i, out kind, out identity, out name, out native) &&
                    kind == action.Kind && identity == action.Identity)
                {
                    value = native;
                    RuntimeActions[CacheKey(action)] = new CachedAction { Owner = GameData.PlayerStats, Value = native };
                    return true;
                }
            }
            return false;
        }
        // Legacy API retained for existing callers; NEVER borrow or mutate
        // one of Erenshor's original ten native Hotkeys components.
        internal static bool Activate(int bar, int slot)
        {
            return NativeExtraHotkeySlots.Activate(bar, slot);
        }
        private static void Diagnostic(string message)
        {
            if (Time.unscaledTime < _nextDiagnostic) return;
            _nextDiagnostic = Time.unscaledTime + 3f;
            Plugin.LogWarning("ArcaneUI Phase 7.3: " + message);
        }
    }
}
