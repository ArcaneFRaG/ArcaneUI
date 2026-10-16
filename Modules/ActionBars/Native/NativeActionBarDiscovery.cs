using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace ArcanesGroupFrames.Modules.ActionBars.Native
{
    /// <summary>
    /// Strictly read-only, opt-in investigation. Never disables an object, calls
    /// a button, assigns a hotkey, modifies a spell slot, or runs per frame.
    /// </summary>
    internal static class NativeActionBarDiscovery
    {
        private static readonly string[] CandidateNames =
        {
            "hotbar", "hot bar", "actionbar", "action bar", "spellbar", "spell bar",
            "skillbar", "skill bar", "quickbar", "quick bar", "quickslot",
            "actionslot", "spellslot", "spell slot", "spellbutton", "hotkeybar",
            "memorizedspell", "memorisedspell", "abilitybar"
        };
        private static readonly string[] BindingNames =
        {
            "slot", "spell", "skill", "item", "hotkey", "keybind", "binding",
            "cooldown", "charge", "shortcut", "ability", "memorize", "hotbar", "cast", "button", "quick", "activate"
        };

        internal static string Status { get; private set; } = "Not scanned. Native action bars are unchanged.";

        // Captured only when the user presses SCAN & LOG. No expensive search
        // or gameplay member invocation occurs from the UI drawing path.
        internal sealed class NativeSlotSnapshot
        {
            internal int Slot;
            internal bool Alternate;
            internal string AbilityName;
            internal string AbilityKind;
            internal Sprite AbilityIcon;
            internal KeyCode PrimaryKey;
            internal KeyCode SecondaryKey;
            internal float RawCooldown;
            internal MonoBehaviour Source;
            internal bool HasAssignment { get { return !string.IsNullOrEmpty(AbilityName); } }
        }

        private static readonly NativeSlotSnapshot[] PrimarySlots = new NativeSlotSnapshot[10];
        private static readonly NativeSlotSnapshot[] AlternateSlots = new NativeSlotSnapshot[10];
        internal static int ActiveSlotCount { get; private set; }
        internal static int AlternateSlotCount { get; private set; }
        internal static bool HasSlotSnapshot { get; private set; }
        private static Stats SnapshotPlayer;
        internal static bool HasValidLiveSnapshot
        {
            get { return HasSlotSnapshot && SnapshotPlayer != null &&
                object.ReferenceEquals(SnapshotPlayer, GameData.PlayerStats); }
        }

        internal static bool TryGetActiveSlot(int slot, out NativeSlotSnapshot snapshot)
        {
            snapshot = slot >= 1 && slot <= PrimarySlots.Length ? PrimarySlots[slot - 1] : null;
            return snapshot != null;
        }

        internal static string NativeBindingConflict(string plannedKey, int bar, int slot)
        {
            if (string.IsNullOrEmpty(plannedKey)) return null;
            if (!HasValidLiveSnapshot) return "Native bindings are not loaded yet. Capture the key in-game once native hotkeys have been discovered.";
            // Native Hotkeys receives KeyCode even while Ctrl/Alt/Shift is
            // pressed. Block colliding keys regardless of modifier to prevent
            // casting twice from custom+native input on the same keypress.
            int lastPlus = plannedKey.LastIndexOf('+');
            if (lastPlus >= 0) plannedKey = plannedKey.Substring(lastPlus + 1);
            for (int i = 0; i < PrimarySlots.Length; ++i)
            {
                var native = PrimarySlots[i];
                var alt = AlternateSlots[i];
                if (alt != null && (SameKey(plannedKey, alt.PrimaryKey) || SameKey(plannedKey, alt.SecondaryKey)))
                    return "Native alternate hotkey " + (i + 1) + " already uses " + plannedKey + ".";
                if (native == null) continue;
                if (SameKey(plannedKey, native.PrimaryKey) || SameKey(plannedKey, native.SecondaryKey))
                    return bar == 0 && slot == native.Slot
                        ? "This is also the current native shortcut for primary slot " + native.Slot + "."
                        : "Native primary slot " + native.Slot + " already uses " + plannedKey +
                          ". ArcaneUI will NOT override that native binding in this build.";
            }
            return null;
        }

        internal static bool IsNativeKey(KeyCode key)
        {
            if (key == KeyCode.None || !HasValidLiveSnapshot) return false;
            for (int i = 0; i < 10; ++i)
            {
                var p = PrimarySlots[i]; var a = AlternateSlots[i];
                if ((p != null && (p.PrimaryKey == key || p.SecondaryKey == key)) ||
                    (a != null && (a.PrimaryKey == key || a.SecondaryKey == key))) return true;
            }
            return false;
        }

        private static MethodInfo _taskMethod;
        internal static bool ActivatePrimarySlot(int number)
        {
            NativeSlotSnapshot slot;
            if (!HasValidLiveSnapshot || !TryGetActiveSlot(number, out slot) ||
                slot == null || slot.Source == null || !slot.HasAssignment ||
                !slot.Source.gameObject.activeInHierarchy) return false;
            try
            {
                if (_taskMethod == null || _taskMethod.DeclaringType != slot.Source.GetType())
                    _taskMethod = slot.Source.GetType().GetMethod("DoHotkeyTask",
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                        null, Type.EmptyTypes, null);
                if (_taskMethod == null) return false;
                _taskMethod.Invoke(slot.Source, null);
                return true;
            }
            catch (Exception error)
            {
                Plugin.LogWarning("ArcaneUI ActionBar native action failed (slot " + number + "): " +
                    error.GetType().Name);
                return false;
            }
        }

        // 7.3: capture only a real, live native action. The identity comes
        // from the assigned object rather than a text box or a global list.
        internal static bool TryReadNativeAssignment(int number, out string kind,
            out string identity, out string displayName, out object value)
        {
            kind = identity = displayName = null;
            value = null;
            NativeSlotSnapshot snapshot;
            if (!HasValidLiveSnapshot || !TryGetActiveSlot(number, out snapshot) ||
                snapshot == null || snapshot.Source == null) return false;
            var spell = ReadField(snapshot.Source, "AssignedSpell") as Spell;
            if (spell != null && !string.IsNullOrEmpty(spell.Id))
            {
                kind = "Spell"; identity = spell.Id; displayName = spell.SpellName; value = spell;
                return true;
            }
            object skill = ReadField(snapshot.Source, "AssignedSkill");
            if (skill != null)
            {
                string name = ReadNamedField(skill, "SkillName") as string;
                string id = ReadNamedField(skill, "Id") as string;
                if (!string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(id))
                {
                    kind = "Skill"; identity = id; displayName = name; value = skill;
                    return true;
                }
            }
            object item = ReadField(snapshot.Source, "AssignedItem");
            if (item != null)
            {
                // An ItemIcon without a stable item ID is NOT persisted.
                // The game's item/UI object layout varies by build.
                string id = ReadStableItemIdentity(item, 0);
                if (!string.IsNullOrEmpty(id))
                {
                    kind = "Item"; identity = id;
                    displayName = ReadNamedField(item, "ItemName") as string ?? "Native item " + id;
                    value = item;
                    return true;
                }
            }
            return false;
        }

        internal static string ItemIdentity(object item)
        {
            return ReadStableItemIdentity(item, 0);
        }

        private static string ReadStableItemIdentity(object item, int depth)
        {
            if (item == null || depth > 2) return null;
            foreach (string field in depth == 0
                ? new [] { "ItemID", "ItemId", "itemID", "itemId" }
                : new [] { "ItemID", "ItemId", "itemID", "itemId", "ID", "Id" })
            {
                object id = ReadNamedField(item, field);
                if (id is int || id is long || id is string)
                {
                    string text = Convert.ToString(id, System.Globalization.CultureInfo.InvariantCulture);
                    if (!string.IsNullOrEmpty(text) && text != "0") return text;
                }
            }
            foreach (string field in new [] { "MyItem", "Item", "ThisItem", "ItemData", "item" })
            {
                object nested = ReadNamedField(item, field);
                if (nested == null || object.ReferenceEquals(nested, item)) continue;
                string id = ReadStableItemIdentity(nested, depth + 1);
                if (!string.IsNullOrEmpty(id)) return id;
            }
            return null;
        }

        // Native spellcasting is already used by ArcaneUI click casting. Unlike
        // borrowing a Hotkeys UI button, this path works with no empty HK slot.
        // StartSpell owns target, resource, cooldown and cast legality checks.
        private sealed class ObservedSpellCooldown
        {
            internal float EndsAt;
            internal float Duration;
        }
        private static readonly Dictionary<string, ObservedSpellCooldown> ObservedSpellCooldowns =
            new Dictionary<string, ObservedSpellCooldown>(StringComparer.Ordinal);
        private static Stats _cooldownOwner;
        private static readonly string[] SpellCooldownNames =
        {
            "RecastTime", "RecastDelay", "CooldownTime", "CoolDownTime",
            "ReuseTime", "SpellCooldown", "Cooldown", "CoolDown"
        };
        private static float NumericMember(object instance, string name)
        {
            if (instance == null) return 0f;
            try
            {
                const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
                Type type = instance.GetType();
                FieldInfo field = type.GetField(name, flags);
                object value = field != null ? field.GetValue(instance) : null;
                if (field == null)
                {
                    PropertyInfo property = type.GetProperty(name, flags);
                    if (property == null || property.GetIndexParameters().Length != 0) return 0f;
                    value = property.GetValue(instance, null);
                }
                if (value == null || value is bool || value is string) return 0f;
                float number = Convert.ToSingle(value, System.Globalization.CultureInfo.InvariantCulture);
                return float.IsNaN(number) || float.IsInfinity(number) ? 0f : number;
            }
            catch { return 0f; }
        }
        internal static bool TryInvokeIndependentSpell(Spell spell)
        {
            if (spell == null || GameData.PlayerControl == null || GameData.PlayerStats == null) return false;
            try
            {
                CastSpell caster = GameData.PlayerControl.GetComponent<CastSpell>();
                if (caster == null) return false;
                Character target = GameData.PlayerControl.CurrentTarget;
                // The native cast handler decides whether this target is legal.
                Stats targetStats = target != null && target.MyStats != null ? target.MyStats : GameData.PlayerStats;
                bool started = caster.StartSpell(spell, targetStats);
                if (!started) return false;
                ObserveAcceptedSpell(spell);
                return true;
            }
            catch (Exception error)
            {
                Plugin.LogWarning("ArcaneUI independent StartSpell failed: " + error.GetType().Name);
                return false;
            }
        }
        internal static void ObserveAcceptedSpell(Spell spell)
        {
            if (spell == null || string.IsNullOrEmpty(spell.Id) || GameData.PlayerStats == null) return;
            if (!object.ReferenceEquals(_cooldownOwner, GameData.PlayerStats))
            {
                _cooldownOwner = GameData.PlayerStats;
                ObservedSpellCooldowns.Clear();
            }
            float duration = 0f;
            foreach (string name in SpellCooldownNames)
            {
                float configured = NumericMember(spell, name);
                if (configured > .1f && configured <= 600f) { duration = configured; break; }
            }
            // This estimate is made only when the native Spell defines a valid
            // configured cooldown. Cast acceptance, mana and legality are all
            // still controlled by Erenshor. Never impose our own recast lock.
            if (duration > 0f)
                ObservedSpellCooldowns[spell.Id] = new ObservedSpellCooldown
                    { Duration = duration, EndsAt = Time.time + duration };
        }

        // Called only after a live native hotkey timer unexpectedly snaps to
        // ready. A previously accepted cast's estimated countdown must NEVER
        // override the authoritative reset (e.g. Emberweaver Pyroclasm procs).
        internal static void ForgetEstimatedSpellCooldown(string spellId)
        {
            if (!string.IsNullOrEmpty(spellId))
                ObservedSpellCooldowns.Remove(spellId);
        }

        internal static bool TryGetIndependentSpellCooldown(string spellId, out float left, out float total)
        {
            left = total = 0f;
            if (!object.ReferenceEquals(_cooldownOwner, GameData.PlayerStats))
            {
                _cooldownOwner = GameData.PlayerStats;
                ObservedSpellCooldowns.Clear();
            }
            ObservedSpellCooldown data;
            if (string.IsNullOrEmpty(spellId) || !ObservedSpellCooldowns.TryGetValue(spellId, out data)) return false;
            left = Mathf.Max(0f, data.EndsAt - Time.time);
            total = data.Duration;
            if (left > 0f) return true;
            ObservedSpellCooldowns.Remove(spellId);
            return false;
        }

        // Phase 7.3F: removed the native-slot borrowing dispatcher. Each extra
        // bar slot now owns a real Hotkeys component (NativeExtraHotkeySlots).

        internal static bool IsLivePrimarySlotObject(GameObject candidate, out int slotNumber)
        {
            slotNumber = 0;
            if (!HasValidLiveSnapshot || candidate == null) return false;
            for (Transform current = candidate.transform; current != null; current = current.parent)
                for (int i = 0; i < PrimarySlots.Length; i++)
                    if (PrimarySlots[i] != null && PrimarySlots[i].Source != null &&
                        PrimarySlots[i].Source.transform == current)
                    {
                        slotNumber = i + 1;
                        return true;
                    }
            return false;
        }

        /// <summary>
        /// Clear all three native action references. Do not touch key settings,
        /// slot cooldown or other UI/gameplay fields. Native save persistence
        /// must be checked against the user's current Erenshor build.
        /// </summary>
        internal static bool TryClearPrimarySlot(int number, out string message)
        {
            message = "No live native hotkey found.";
            NativeSlotSnapshot entry;
            if (!HasValidLiveSnapshot || !TryGetActiveSlot(number, out entry) ||
                entry == null || entry.Source == null) return false;
            const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
            var type = entry.Source.GetType();
            var spell = type.GetField("AssignedSpell", flags);
            var skill = type.GetField("AssignedSkill", flags);
            var item = type.GetField("AssignedItem", flags);
            if (spell == null || skill == null || item == null)
            {
                message = "Native assignment members have changed; primary slot was left untouched.";
                return false;
            }
            object oldSpell = null, oldSkill = null, oldItem = null;
            try
            {
                oldSpell = spell.GetValue(entry.Source);
                oldSkill = skill.GetValue(entry.Source);
                oldItem = item.GetValue(entry.Source);
                spell.SetValue(entry.Source, null);
                skill.SetValue(entry.Source, null);
                item.SetValue(entry.Source, null);
                PrimarySlots[number - 1] = SnapshotSlot(entry.Source, number, false);
                message = "Cleared HK" + number + ". Check native persistence after logging out.";
                return true;
            }
            catch (Exception ex)
            {
                try
                {
                    spell.SetValue(entry.Source, oldSpell);
                    skill.SetValue(entry.Source, oldSkill);
                    item.SetValue(entry.Source, oldItem);
                }
                catch (Exception) { }
                message = "Native clear failed; original assignment restored where possible (" + ex.GetType().Name + ").";
                return false;
            }
        }

        /// <summary>
        /// Resolve a Spell from an *actual visible spellbook UI entry* only.
        /// Inspect component data; never infer an unowned spell from a text label.
        /// Game versions that don't expose an explicit Spell reference fail closed.
        /// </summary>
        internal static bool TryGetSkillbookEntry(GameObject dragSource, out Skill skill)
        {
            skill = null;
            if (dragSource == null || !dragSource.activeInHierarchy) return false;
            bool skillbook = false;
            for (Transform t = dragSource.transform; t != null; t = t.parent)
            {
                string path = t.name.ToLowerInvariant();
                if (path.Contains("skillbook") || path.Contains("skill book") ||
                    path.Contains("skillswindow") || path.Contains("skills window") ||
                    path.Contains("skillpanel")) { skillbook = true; break; }
            }
            if (!skillbook) return false;
            for (Transform t = dragSource.transform; t != null; t = t.parent)
            {
                foreach (MonoBehaviour component in t.GetComponents<MonoBehaviour>())
                {
                    if (component == null) continue;
                    foreach (string field in new [] { "AssignedSkill", "ThisSkill", "MySkill", "Skill", "skill", "skillData", "SkillData" })
                    {
                        Skill found = ReadNamedField(component, field) as Skill;
                        if (found == null || string.IsNullOrEmpty(found.Id)) continue;
                        var button = t.GetComponent<UnityEngine.UI.Button>();
                        if (button != null && !button.interactable) return false;
                        skill = found;
                        return true;
                    }
                }
                string n = t.name.ToLowerInvariant();
                if (n.Contains("skillbook") || n.Contains("skill book")) break;
            }
            return false;
        }
        internal static bool TryGetInventoryDrag(GameObject dragSource, out ItemIcon item)
        {
            item = null;
            if (dragSource == null || !dragSource.activeInHierarchy) return false;
            item = dragSource.GetComponentInParent<ItemIcon>();
            return item != null && item.MyItem != null &&
                GameData.PlayerInv != null && GameData.PlayerInv.ALLSLOTS != null;
        }

        internal static bool TryGetSpellbookEntry(GameObject dragSource, out Spell spell)
        {
            spell = null;
            if (dragSource == null || !dragSource.activeInHierarchy) return false;
            bool spellbook = false;
            for (Transform t = dragSource.transform; t != null; t = t.parent)
            {
                string n = t.name.ToLowerInvariant();
                if (n.Contains("spellbook") || n.Contains("spell book") ||
                    n.Contains("learnedspell") || n.Contains("knownspell") ||
                    n.Contains("spell list") || n.Contains("spelllist") ||
                    n.Contains("spellwindow") || n.Contains("spell window") ||
                    n.Contains("spellpanel") || n.Contains("spell page") ||
                    n.Contains("spellswindow")) { spellbook = true; break; }
            }
            if (!spellbook) return false;
            // Drag source can be a child icon; stop at the first candidate entry
            // rather than searching the owning spellbook's entire spell list.
            for (Transform t = dragSource.transform; t != null; t = t.parent)
            {
                foreach (MonoBehaviour component in t.GetComponents<MonoBehaviour>())
                {
                    if (component == null) continue;
                    foreach (string name in new [] { "ThisSpell", "AssignedSpell", "MySpell", "Spell", "spell", "spellData", "SpellData", "selectedSpell", "TheSpell", "ThisAbility" })
                    {
                        Spell found = ReadNamedField(component, name) as Spell;
                        if (found != null && !string.IsNullOrEmpty(found.Id))
                        {
                            // Disabled/locked entries must never become assignments.
                            var button = t.GetComponent<UnityEngine.UI.Button>();
                            if (button != null && !button.interactable) return false;
                            spell = found;
                            return true;
                        }
                    }
                    // Some Erenshor spellbook entries store only a SpellId
                    // rather than a Spell pointer. Resolve that exact ID only
                    // when it comes from the active, visible native spellbook.
                    foreach (string idField in new [] { "SpellID", "SpellId", "spellID", "spellId" })
                    {
                        object rawId = ReadNamedField(component, idField);
                        string id = rawId == null ? null : Convert.ToString(rawId,
                            System.Globalization.CultureInfo.InvariantCulture);
                        if (string.IsNullOrEmpty(id) || GameData.SpellDatabase == null ||
                            GameData.SpellDatabase.SpellDatabase == null) continue;
                        foreach (Spell candidate in GameData.SpellDatabase.SpellDatabase)
                        {
                            if (candidate != null && string.Equals(candidate.Id, id, StringComparison.Ordinal))
                            {
                                var button = t.GetComponent<UnityEngine.UI.Button>();
                                if (button != null && !button.interactable) return false;
                                spell = candidate;
                                return true;
                            }
                        }
                    }
                }
                string n = t.name.ToLowerInvariant();
                if (n.Contains("spellbook") || n.Contains("spell book")) break;
            }
            return false;
        }

        internal static List<Spell> GetVisibleSpellbookEntries()
        {
            var result = new List<Spell>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            // Only when opening the assignment picker, never during render.
            // Limit to active, visible UI entries rather than the master SpellDB.
            foreach (UnityEngine.UI.Button button in Resources.FindObjectsOfTypeAll<UnityEngine.UI.Button>())
            {
                if (button == null || !button.gameObject.scene.IsValid() ||
                    !button.gameObject.activeInHierarchy || !button.interactable) continue;
                Spell spell;
                if (TryGetSpellbookEntry(button.gameObject, out spell) && seen.Add(spell.Id))
                    result.Add(spell);
            }
            result.Sort((a, b) => string.Compare(a.SpellName, b.SpellName, StringComparison.OrdinalIgnoreCase));
            return result;
        }

        private static bool SameKey(string planned, KeyCode native)
        {
            return native != KeyCode.None &&
                   string.Equals(planned, ArcanesGroupFrames.Modules.ActionBars.Configuration.ActionBarPlanSettings.Describe(
                       native, false, false, false), StringComparison.OrdinalIgnoreCase);
        }

        private static object ReadField(MonoBehaviour component, string name)
        {
            if (component == null) return null;
            try
            {
                FieldInfo field = component.GetType().GetField(name,
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                return field == null ? null : field.GetValue(component);
            }
            catch { return null; }
        }

        private static object ReadNamedField(object obj, string name)
        {
            if (obj == null) return null;
            try
            {
                FieldInfo field = obj.GetType().GetField(name,
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (field != null) return field.GetValue(obj);
                PropertyInfo property = obj.GetType().GetProperty(name,
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                return property != null && property.GetIndexParameters().Length == 0 ?
                    property.GetValue(obj, null) : null;
            }
            catch { return null; }
        }

        private static NativeSlotSnapshot SnapshotSlot(MonoBehaviour component, int slot, bool alternate)
        {
            var entry = new NativeSlotSnapshot { Slot = slot, Alternate = alternate,
                PrimaryKey = KeyCode.None, SecondaryKey = KeyCode.None, AbilityKind = "Empty", Source = component };
            object spell = ReadField(component, "AssignedSpell");
            object skill = ReadField(component, "AssignedSkill");
            object item = ReadField(component, "AssignedItem");
            if (spell != null)
            {
                entry.AbilityKind = "Spell";
                entry.AbilityName = ReadNamedField(spell, "SpellName") as string;
                entry.AbilityIcon = ReadNamedField(spell, "SpellIcon") as Sprite;
            }
            else if (skill != null)
            {
                entry.AbilityKind = "Skill";
                entry.AbilityName = ReadNamedField(skill, "SkillName") as string;
                entry.AbilityIcon = ReadNamedField(skill, "SkillIcon") as Sprite;
            }
            else if (item != null)
            {
                entry.AbilityKind = "Item";
                // ItemIcon's underlying item and sprite paths are not verified.
                // Label the assignment without guessing its inventory identity.
                entry.AbilityName = "Assigned Item";
            }
            object key1 = ReadField(component, "ActivateKey");
            object key2 = ReadField(component, "ActivateKey2");
            if (key1 is KeyCode) entry.PrimaryKey = (KeyCode)key1;
            if (key2 is KeyCode) entry.SecondaryKey = (KeyCode)key2;
            // Erenshor builds may use an int/double timer or a differently
            // named remaining-time member; read the native slot only.
            foreach (string timerName in new [] { "CooldownRemaining", "RemainingCooldown",
                "cooldownRemaining", "cooldownTimer", "CooldownTimer", "Cooldown", "cooldown" })
            {
                float timer = NumericMember(component, timerName);
                if (timer > 0f && timer <= 3600f)
                {
                    entry.RawCooldown = timer;
                    break;
                }
            }
            return entry;
        }

        /// <summary>Re-read only the ten cached native components at 5 Hz. No scene traversal.</summary>
        internal static void RefreshPrimarySnapshots()
        {
            if (!HasValidLiveSnapshot) return;
            for (int i = 0; i < PrimarySlots.Length; ++i)
            {
                var old = PrimarySlots[i];
                if (old == null || old.Source == null) continue;
                var next = SnapshotSlot(old.Source, i + 1, false);
                PrimarySlots[i] = next;
            }
        }

        internal static string DescribeSlot(NativeSlotSnapshot slot)
        {
            if (slot == null) return "Unresolved";
            string keys = slot.PrimaryKey == KeyCode.None ? "Unbound" :
                ArcanesGroupFrames.Modules.ActionBars.Configuration.ActionBarPlanSettings.Describe(
                    slot.PrimaryKey, false, false, false);
            if (slot.SecondaryKey != KeyCode.None)
                keys += " / " + slot.SecondaryKey;
            return slot.Slot + ": " + (slot.HasAssignment ? slot.AbilityName + " [" + slot.AbilityKind + "]" : "Empty") +
                   "  (" + keys + ")";
        }


        private static Stats _lastRuntimePlayer;
        private static float _nextRuntimeProbe;
        /// <summary>
        /// One narrowly filtered discovery on player change. A missing native UI
        /// is retried at most every 15 seconds; never walk hierarchies per frame.
        /// No action methods are invoked and no UI elements are changed.
        /// </summary>
        internal static void TryBindRuntime()
        {
            Stats player = GameData.PlayerStats;
            if (player == null) return;
            if (HasValidLiveSnapshot) return;
            if (!object.ReferenceEquals(_lastRuntimePlayer, player))
            {
                _lastRuntimePlayer = player;
                _nextRuntimeProbe = 0f;
            }
            if (Time.unscaledTime < _nextRuntimeProbe) return;
            _nextRuntimeProbe = Time.unscaledTime + 15f;
            Array.Clear(PrimarySlots, 0, PrimarySlots.Length);
            Array.Clear(AlternateSlots, 0, AlternateSlots.Length);
            ActiveSlotCount = AlternateSlotCount = 0;
            try
            {
                RectTransform[] all = Resources.FindObjectsOfTypeAll<RectTransform>();
                foreach (RectTransform rect in all)
                {
                    if (rect == null || !rect.gameObject.scene.IsValid()) continue;
                    if (!rect.name.StartsWith("HK", StringComparison.Ordinal)) continue;
                    Transform parent = rect.parent;
                    if (parent == null || parent.name != "Hotkeys" ||
                        parent.parent == null || parent.parent.name != "HotbarPar") continue;
                    bool alternate = rect.name.EndsWith(" (1)", StringComparison.Ordinal);
                    string shortName = alternate ? rect.name.Substring(0, rect.name.Length - 4) : rect.name;
                    int number;
                    if (!int.TryParse(shortName.Substring(2), out number) || number < 1 || number > 10 ||
                        shortName != "HK" + number) continue;
                    MonoBehaviour[] components = rect.GetComponents<MonoBehaviour>();
                    foreach (MonoBehaviour component in components)
                    {
                        if (component == null || component.GetType().Name != "Hotkeys") continue;
                        var slot = SnapshotSlot(component, number, alternate);
                        if (alternate)
                        {
                            if (AlternateSlots[number - 1] == null) { AlternateSlots[number - 1] = slot; AlternateSlotCount++; }
                        }
                        else if (PrimarySlots[number - 1] == null)
                        {
                            PrimarySlots[number - 1] = slot;
                            ActiveSlotCount++;
                        }
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                // Missing hotkey roots during scene transitions are expected; the
                // native bar remains visible until a valid binding is found.
                Plugin.LogDiagnosticWarning("ArcaneUI ActionBar runtime bind deferred: " + ex.GetType().Name);
            }
            HasSlotSnapshot = ActiveSlotCount > 0;
            SnapshotPlayer = HasSlotSnapshot ? player : null;
        }

        internal static void ScanAndLog()
        {
            var candidates = new SortedDictionary<string, RectTransform>(StringComparer.OrdinalIgnoreCase);
            int visited = 0;
            try
            {
                foreach (RectTransform rect in Resources.FindObjectsOfTypeAll<RectTransform>())
                {
                    if (rect == null || !rect.gameObject.scene.IsValid()) continue;
                    if (++visited > 20000) break;
                    if (HasArcaneUiAncestor(rect)) continue;
                    string path = GetPath(rect);
                    if (!ContainsAny(path, CandidateNames)) continue;
                    if (!(path.IndexOf("UI", StringComparison.OrdinalIgnoreCase) >= 0 ||
                          rect.GetComponent<CanvasRenderer>() != null)) continue;
                    candidates[path] = rect;
                }
            }
            catch (Exception ex)
            {
                Plugin.LogDiagnosticWarning("ArcaneUI ActionBar DISCOVERY UI scan error: " + ex.GetType().Name);
            }
            Plugin.LogDiagnosticWarning("ArcaneUI ActionBar SCAN UI: " + candidates.Count +
                " candidate paths; read-only, no UI modified.");
            int n = 0;
            foreach (var pair in candidates)
            {
                if (n++ >= 55) break;
                var rect = pair.Value;
                Plugin.LogDiagnosticWarning("ArcaneUI ActionBar UI [" + n + "]: " + pair.Key +
                    (rect.gameObject.activeInHierarchy ? " [ACTIVE]" : " [INACTIVE]") +
                    " children=" + rect.childCount);
                try
                {
                    foreach (MonoBehaviour component in rect.GetComponents<MonoBehaviour>())
                    {
                        if (component == null) continue;
                        Plugin.LogDiagnosticWarning("ArcaneUI ActionBar COMPONENT " + pair.Key + " : " +
                            component.GetType().FullName);
                    }
                }
                catch (Exception ex)
                {
                    Plugin.LogDiagnosticWarning("ArcaneUI ActionBar COMPONENT inspection skipped: " + ex.GetType().Name);
                }
            }
            if (n == 0) Plugin.LogDiagnosticWarning("ArcaneUI ActionBar SCAN: no matching native RectTransform names. Try while a spellbar is visible.");
            ScanNativeHotkeySlots(candidates);
            ScanNativeTypes();
            Status = "Scanned " + candidates.Count + " UI candidates and the native Hotkeys slots; check ArcaneUI ActionBar lines in log.";
        }

        /// <summary>
        /// Inspect actual Hotkeys components on the native UI slots, rather than
        /// logging hundreds of Dropdown/Scrollbar children. The user triggers
        /// this explicitly; it NEVER invokes an assignment or activation method.
        /// </summary>
        private static void ScanNativeHotkeySlots(SortedDictionary<string, RectTransform> candidates)
        {
            Array.Clear(PrimarySlots, 0, PrimarySlots.Length);
            Array.Clear(AlternateSlots, 0, AlternateSlots.Length);
            ActiveSlotCount = 0;
            AlternateSlotCount = 0;
            HasSlotSnapshot = false;
            SnapshotPlayer = null;
            int found = 0;
            int inspected = 0;
            foreach (KeyValuePair<string, RectTransform> pair in candidates)
            {
                // The scan confirms this hierarchy exists in Erenshor:
                // UIElements/Canvas/HotbarPar/Hotkeys/HK1 ...
                if (pair.Key.IndexOf("/HotbarPar/Hotkeys/", StringComparison.OrdinalIgnoreCase) < 0)
                    continue;
                RectTransform rect = pair.Value;
                if (rect == null) continue;
                MonoBehaviour[] behaviours;
                try { behaviours = rect.GetComponents<MonoBehaviour>(); }
                catch { continue; }
                foreach (MonoBehaviour behaviour in behaviours)
                {
                    if (behaviour == null || behaviour.GetType().Name != "Hotkeys") continue;
                    found++;
                    // Confirm exact HK1..HK10 names; exclude GhostItem and
                    // other child objects. The (1) variants are inactive
                    // alternate slots, not 10 extra simultaneous hotkeys.
                    bool alternate = rect.name.EndsWith(" (1)", StringComparison.Ordinal);
                    string slotName = alternate ? rect.name.Substring(0, rect.name.Length - 4) : rect.name;
                    int number;
                    if (slotName.StartsWith("HK", StringComparison.Ordinal) &&
                        int.TryParse(slotName.Substring(2), out number) && number >= 1 && number <= 10 &&
                        (rect.name == "HK" + number || rect.name == "HK" + number + " (1)"))
                    {
                        var entry = SnapshotSlot(behaviour, number, alternate);
                        if (alternate)
                        {
                            if (AlternateSlots[number - 1] == null) { AlternateSlots[number - 1] = entry; AlternateSlotCount++; }
                        }
                        else if (PrimarySlots[number - 1] == null)
                        {
                            PrimarySlots[number - 1] = entry;
                            ActiveSlotCount++;
                        }
                    }
                    if (inspected >= 20) continue;
                    inspected++;
                    Plugin.LogDiagnosticWarning("ArcaneUI ActionBar SLOT [" + inspected + "] " + pair.Key +
                        (rect.gameObject.activeInHierarchy ? " ACTIVE" : " INACTIVE"));
                    int fields = 0;
                    foreach (FieldInfo field in behaviour.GetType().GetFields(
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                    {
                        if (!ContainsAny(field.Name, BindingNames)) continue;
                        try
                        {
                            object value = field.GetValue(behaviour);
                            Plugin.LogDiagnosticWarning("ArcaneUI ActionBar SLOT FIELD " + field.Name +
                                " : " + field.FieldType.Name + " = " + SafeValue(value));
                        }
                        catch (Exception ex)
                        {
                            Plugin.LogDiagnosticWarning("ArcaneUI ActionBar SLOT FIELD " + field.Name +
                                " : unreadable (" + ex.GetType().Name + ")");
                        }
                        if (++fields >= 14) break;
                    }
                    if (fields == 0)
                        Plugin.LogDiagnosticWarning("ArcaneUI ActionBar SLOT: no matching instance fields; inspect Hotkeys type signatures below.");
                }
            }
            HasSlotSnapshot = ActiveSlotCount > 0;
            SnapshotPlayer = GameData.PlayerStats;
            Plugin.LogDiagnosticWarning("ArcaneUI ActionBar SLOTS: found=" + found +
                " inspected=" + inspected + " (read-only; UI and bindings unchanged).");
            Plugin.LogDiagnosticWarning("ArcaneUI ActionBar SNAPSHOT: " + ActiveSlotCount +
                " primary slots, " + AlternateSlotCount + " alternate slots. No game input or slots modified.");
            for (int i = 0; i < PrimarySlots.Length; i++)
                if (PrimarySlots[i] != null)
                    Plugin.LogDiagnosticWarning("ArcaneUI ActionBar SNAPSHOT PRIMARY " + DescribeSlot(PrimarySlots[i]));
        }

        private static string SafeValue(object value)
        {
            if (value == null) return "null";
            // Never enumerate native collections or call gameplay methods.
            if (value is string || value is bool || value is char ||
                value is Enum || value is byte || value is short || value is int ||
                value is long || value is float || value is double || value is decimal)
                return Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture);
            Spell spell = value as Spell;
            if (spell != null) return "Spell: " + spell.SpellName;
            ICollection collection = value as ICollection;
            if (collection != null) return "<" + value.GetType().Name + " Count=" + collection.Count + ">";
            UnityEngine.Object nativeObject = value as UnityEngine.Object;
            if (nativeObject != null) return "<" + value.GetType().Name + ": " + nativeObject.name + ">";
            return "<" + value.GetType().Name + ">";
        }

        private static void ScanNativeTypes()
        {
            Type[] types;
            try { types = typeof(Spell).Assembly.GetTypes(); }
            catch (ReflectionTypeLoadException ex) { types = ex.Types; }
            catch (Exception ex)
            {
                Plugin.LogDiagnosticWarning("ArcaneUI ActionBar TYPE scan unavailable: " + ex.GetType().Name);
                return;
            }
            int count = 0;
            foreach (Type type in types)
            {
                if (type == null || !(ContainsAny(type.Name, CandidateNames) || IsCoreActionType(type.Name))) continue;
                if (++count > 50) break;
                Plugin.LogDiagnosticWarning("ArcaneUI ActionBar TYPE: " + type.FullName);
                try
                {
                    int members = 0;
                    foreach (FieldInfo field in type.GetFields(BindingFlags.Public | BindingFlags.NonPublic |
                        BindingFlags.Instance | BindingFlags.Static))
                    {
                        if (!ContainsAny(field.Name, BindingNames)) continue;
                        Plugin.LogDiagnosticWarning("ArcaneUI ActionBar FIELD " + type.Name + "." + field.Name + " : " + field.FieldType.Name);
                        if (++members == 20) break;
                    }
                    foreach (PropertyInfo property in type.GetProperties(BindingFlags.Public | BindingFlags.NonPublic |
                        BindingFlags.Instance | BindingFlags.Static))
                    {
                        if (!ContainsAny(property.Name, BindingNames)) continue;
                        Plugin.LogDiagnosticWarning("ArcaneUI ActionBar PROPERTY " + type.Name + "." + property.Name + " : " + property.PropertyType.Name);
                        if (++members >= 32) break;
                    }
                    int actionMethods = 0;
                    foreach (MethodInfo method in type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic |
                        BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
                    {
                        if (method.IsSpecialName || !ContainsAny(method.Name, BindingNames)) continue;
                        var parameters = method.GetParameters();
                        string[] names = new string[parameters.Length];
                        for (int p = 0; p < parameters.Length; p++)
                            names[p] = parameters[p].ParameterType.Name;
                        Plugin.LogDiagnosticWarning("ArcaneUI ActionBar METHOD " + type.Name + "." + method.Name +
                            "(" + string.Join(", ", names) + ") : " + method.ReturnType.Name);
                        if (++actionMethods >= 14) break;
                    }
                }
                catch (Exception) { /* Native reflection is optional. */ }
            }
            Plugin.LogDiagnosticWarning("ArcaneUI ActionBar SCAN TYPES: " + count +
                " likely/native related types (up to 50). Signatures only: no methods invoked or slots modified.");
        }

        private static bool IsCoreActionType(string name)
        {
            switch (name)
            {
                case "GameData": case "PlayerControl": case "CastSpell": case "Spell":
                case "Stats": case "UseSkill": case "AAScreen": case "SpellDB":
                case "Hotkeys": case "Skill": case "SkillBook": case "SpellBook": return true;
                default: return false;
            }
        }
        private static bool ContainsAny(string text, string[] names)
        {
            if (string.IsNullOrEmpty(text)) return false;
            foreach (string token in names)
                if (text.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            return false;
        }
        private static bool HasArcaneUiAncestor(Transform t)
        {
            for (Transform current = t; current != null; current = current.parent)
                if (current.name.StartsWith("ArcaneUI", StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }
        private static string GetPath(Transform t)
        {
            var parts = new List<string>();
            for (Transform current = t; current != null; current = current.parent)
                parts.Add(current.name);
            parts.Reverse();
            return t.gameObject.scene.name + ":" + string.Join("/", parts.ToArray());
        }
    }
}
