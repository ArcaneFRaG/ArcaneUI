using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;
using ArcanesGroupFrames.Framework.NativeUI;
using ArcanesGroupFrames.Modules.ActionBars.Configuration;

namespace ArcanesGroupFrames.Modules.ActionBars.Native
{
    /// <summary>
    /// ArcaneUI-owned REAL Erenshor Hotkeys components. Unlike the 7.3E
    /// borrowed-slot dispatcher, each extra slot retains its own native action,
    /// type and cooldown. Primary Hotkeys are never patched or modified.
    ///
    /// The UI owns the GameObject lifetime; assignments are persisted separately
    /// using the existing character-scoped v1 identities. No game assets are copied.
    /// </summary>
    internal static class NativeExtraHotkeySlots
    {
        private sealed class Entry
        {
            internal int Bar, Slot;
            internal Hotkeys Native;
            internal Stats Owner;
            internal string LoadedIdentity;
            internal bool Attempted;
            internal int CreatedFrame;
            internal int LastTaskFrame = -1;
            internal float PreviousCooldownUnits = -1f;
            internal float PreviousCooldownTime;
        }

        private static readonly Dictionary<int, Entry> Slots = new Dictionary<int, Entry>();
        private static readonly Dictionary<Hotkeys, Entry> Owned = new Dictionary<Hotkeys, Entry>();
        private sealed class CooldownCarry
        {
            internal Stats Owner;
            internal float EndsAt;
        }
        // Rebuilding bar artwork must not reset a real native cooldown.
        private static readonly Dictionary<string, CooldownCarry> RebuildCooldowns =
            new Dictionary<string, CooldownCarry>(StringComparer.Ordinal);
        private static readonly BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private static MethodInfo _doTask;
        private static FieldInfo _spellSource;
        private static FieldInfo _cooldownMultiplier;
        private static FieldInfo _damageScale;
        private static bool _restoring;
        private static float _nextWarning;

        private static int Key(int bar, int slot) { return bar * 32 + slot; }
        private static string Fingerprint(ActionBarIndependentAssignments.Assignment a)
        {
            return a == null ? "" : a.Kind + ":" + a.Identity;
        }
        internal static void Register(int bar, int slot, GameObject root, Image icon, int size)
        {
            if (bar <= 0 || root == null || icon == null || GameData.PlayerControl == null ||
                GameData.PlayerControl.Myself == null) return;
            Unregister(bar, slot);
            // Erenshor's spellbook/inventory native drag targeting uses the Hotkey
            // tag and 2D collider, not just the Unity EventSystem drop interface.
            try { root.tag = "Hotkey"; } catch (UnityException) { Warn("Native Hotkey tag was unavailable."); }
            int uiLayer = LayerMask.NameToLayer("UI");
            if (uiLayer >= 0) root.layer = uiLayer;
            BoxCollider2D hit = root.AddComponent<BoxCollider2D>();
            hit.size = new Vector2(size, size);
            hit.isTrigger = true;
            Rigidbody2D body = root.AddComponent<Rigidbody2D>();
            body.isKinematic = true;
            body.gravityScale = 0f;

            // Native Hotkeys.Start expects an optional text and stance indicator.
            // Both are non-raycastable, keeping ArcaneUI's own visual layout.
            var textRoot = new GameObject("Text", typeof(RectTransform), typeof(Text));
            textRoot.transform.SetParent(root.transform, false);
            Text nativeText = textRoot.GetComponent<Text>();
            nativeText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            nativeText.raycastTarget = false;
            nativeText.enabled = false; // ArcaneUI draws its own cooldown numbers.
            var indicator = new GameObject("StanceIndicator", typeof(RectTransform), typeof(Image));
            indicator.transform.SetParent(root.transform, false);
            indicator.GetComponent<Image>().raycastTarget = false;
            indicator.SetActive(false);

            Hotkeys hotkey = root.AddComponent<Hotkeys>();
            hotkey.ActivateKey = 0;
            hotkey.ActivateKey2 = 0;
            hotkey.MyImage = icon;
            hotkey.PlayerSkills = GameData.PlayerControl.Myself.MySkills;
            hotkey.PlayerSpells = GameData.PlayerControl.Myself.MySpells;
            hotkey.ActiveStanceIndicator = indicator;
            var entry = new Entry { Bar = bar, Slot = slot, Native = hotkey, Owner = GameData.PlayerStats, CreatedFrame = Time.frameCount };
            Slots[Key(bar, slot)] = entry;
            Owned[hotkey] = entry;
        }

        internal static void Unregister(int bar, int slot)
        {
            Entry old;
            if (!Slots.TryGetValue(Key(bar, slot), out old)) return;
            Slots.Remove(Key(bar, slot));
            RememberCooldown(old);
            if (old.Native != null) Owned.Remove(old.Native);
        }
        internal static void UnregisterBar(int bar)
        {
            for (int i = 1; i <= ActionBarPlanSettings.MaxSlots; ++i) Unregister(bar, i);
        }
        internal static void Shutdown()
        {
            Owned.Clear();
            Slots.Clear();
            RebuildCooldowns.Clear();
        }

        private static string NativeActionKey(Hotkeys hk)
        {
            if (hk == null) return null;
            if (hk.AssignedSpell != null) return "Spell:" + hk.AssignedSpell.Id;
            if (hk.AssignedSkill != null) return "Skill:" + hk.AssignedSkill.Id;
            if (hk.AssignedItem != null)
            {
                string id = NativeActionBarDiscovery.ItemIdentity(hk.AssignedItem);
                if (!string.IsNullOrEmpty(id)) return "Item:" + id;
                return hk.InvSlotIndex >= 0 ? "Item:InvSlot:" + hk.InvSlotIndex : null;
            }
            return null;
        }
        private static void RememberCooldown(Entry e)
        {
            if (e == null || e.Native == null || e.Owner == null || e.Native.Cooldown <= 0f) return;
            string key = NativeActionKey(e.Native);
            if (string.IsNullOrEmpty(key)) return;
            float ends = Time.time + e.Native.Cooldown / 60f;
            CooldownCarry old;
            if (!RebuildCooldowns.TryGetValue(key, out old) ||
                !object.ReferenceEquals(old.Owner, e.Owner) || ends > old.EndsAt)
                RebuildCooldowns[key] = new CooldownCarry { Owner = e.Owner, EndsAt = ends };
        }
        private static void RestoreCooldown(Entry e)
        {
            string key = NativeActionKey(e.Native);
            CooldownCarry state;
            if (!string.IsNullOrEmpty(key) && RebuildCooldowns.TryGetValue(key, out state) &&
                object.ReferenceEquals(e.Owner, state.Owner))
                e.Native.Cooldown = Mathf.Max(e.Native.Cooldown,
                    Mathf.Max(0f, state.EndsAt - Time.time) * 60f);
        }
        internal static bool IsOwned(Hotkeys hk) { return hk != null && Owned.ContainsKey(hk); }

        /// <summary>Synchronise stored action identity only when it changes or a player changes.</summary>
        internal static void Sync(int bar, int slot)
        {
            Entry e;
            if (!Slots.TryGetValue(Key(bar, slot), out e) || e.Native == null) return;
            Stats owner = GameData.PlayerStats;
            // Allow Hotkeys.Start to initialise its own native fields before
            // restoring character-scoped assignments.
            if (owner == null || Time.frameCount <= e.CreatedFrame) return;
            if (!object.ReferenceEquals(e.Owner, owner))
            {
                e.Owner = owner;
                e.PreviousCooldownUnits = -1f;
                e.PreviousCooldownTime = Time.time;
                e.LoadedIdentity = null;
                e.Attempted = false;
                e.Native.PlayerSkills = GameData.PlayerControl.Myself.MySkills;
                e.Native.PlayerSpells = GameData.PlayerControl.Myself.MySpells;
                try { _restoring = true; e.Native.ClearMe(); }
                catch (Exception ex) { Warn("Cannot reset extra slot on character change: " + ex.GetType().Name); }
                finally { _restoring = false; }
            }
            var assignment = ActionBarIndependentAssignments.Get(bar, slot);
            string key = Fingerprint(assignment);
            if (e.Attempted && e.LoadedIdentity == key) return;
            e.LoadedIdentity = key;
            e.Attempted = true;
            try
            {
                _restoring = true;
                if (assignment == null)
                {
                    e.Native.ClearMe();
                    return;
                }
                // A cached session's item/skill pointer may no longer be valid;
                // the saved identity is resolved against the active character.
                object value;
                if (!ActionBarIndependentAssignments.TryResolve(assignment, out value))
                {
                    e.Attempted = false; // deferred while player/inventory loads
                    return;
                }
                AssignNative(e.Native, assignment.Kind, value);
                if (!Matches(e.Native, assignment.Kind, value))
                    throw new InvalidOperationException("Native hotkey did not accept the saved action.");
                RestoreCooldown(e);
            }
            catch (Exception ex)
            {
                e.Attempted = false;
                Warn("Unable to restore Bar" + (bar + 1) + " Slot" + slot + ": " + ex.GetType().Name);
            }
            finally { _restoring = false; }
        }

        private static void AssignNative(Hotkeys hk, string kind, object value)
        {
            if (kind == "Spell" && value is Spell) hk.AssignSpellFromBook((Spell)value);
            else if (kind == "Skill" && value is Skill) hk.AssignSkillFromBook((Skill)value);
            else if (kind == "Item" && value is ItemIcon) hk.AssignItemFrominv((ItemIcon)value);
            else throw new InvalidOperationException("The native action is not a valid " + kind + " object.");
        }
        private static bool Matches(Hotkeys hk, string kind, object value)
        {
            return kind == "Spell" ? object.ReferenceEquals(hk.AssignedSpell, value) :
                   kind == "Skill" ? object.ReferenceEquals(hk.AssignedSkill, value) :
                   kind == "Item" && object.ReferenceEquals(hk.AssignedItem, value);
        }
        internal static bool Activate(int bar, int slot)
        {
            Entry e;
            if (!Slots.TryGetValue(Key(bar, slot), out e) || e.Native == null ||
                !object.ReferenceEquals(e.Owner, GameData.PlayerStats)) return false;
            Sync(bar, slot);
            if (e.Native.AssignedSpell == null && e.Native.AssignedSkill == null &&
                e.Native.AssignedItem == null) return false;
            if (e.Native.Cooldown > 0f) return false;
            try
            {
                if (_doTask == null) _doTask = typeof(Hotkeys).GetMethod("DoHotkeyTask", Flags, null, Type.EmptyTypes, null);
                if (_doTask == null) { Warn("Hotkeys.DoHotkeyTask was not found."); return false; }
                _doTask.Invoke(e.Native, null);
                // Native skill cooldowns are slot-local; share among duplicate
                // extra slots just as game hotkey completion shares spell CD.
                if (e.Native.AssignedSkill != null && e.Native.Cooldown > 0f)
                    foreach (Entry other in Slots.Values)
                        if (other.Native != null && other.Native.AssignedSkill == e.Native.AssignedSkill)
                            other.Native.Cooldown = Mathf.Max(other.Native.Cooldown, e.Native.Cooldown);
                return true;
            }
            catch (Exception ex)
            {
                Warn("Native extra-slot action failed: " + (ex.InnerException ?? ex).GetType().Name);
                return false;
            }
        }
        internal static void Clear(int bar, int slot)
        {
            Entry e;
            if (Slots.TryGetValue(Key(bar, slot), out e) && e.Native != null)
            {
                try { _restoring = true; e.Native.ClearMe(); e.Native.Cooldown = 0f; }
                catch (Exception ex) { Warn("Native clear failed: " + ex.GetType().Name); }
                finally { _restoring = false; }
                e.LoadedIdentity = "";
                e.Attempted = true;
            }
            ActionBarIndependentAssignments.Clear(bar, slot);
            ActionBarPlanSettings.SetSourceSlot(bar, slot, 0);
        }
        internal static bool AssignFromDrag(int bar, int slot, string kind, object action)
        {
            Entry e;
            if (!Slots.TryGetValue(Key(bar, slot), out e) || e.Native == null ||
                !object.ReferenceEquals(e.Owner, GameData.PlayerStats) || action == null) return false;
            try
            {
                // Calls the same native AssignXFromBook / AssignItemFrominv
                // methods used by Erenshor and ExtendedHotbars. Harmony postfix
                // saves the accepted real native assignment.
                AssignNative(e.Native, kind, action);
                if (!Matches(e.Native, kind, action)) return false;
                NativeAssignmentChanged(e.Native);
                return true;
            }
            catch (Exception ex)
            {
                Warn("Native drag assignment failed: " + ex.GetType().Name);
                return false;
            }
        }
        internal static bool HasAssignment(int bar, int slot)
        {
            Entry e;
            return Slots.TryGetValue(Key(bar, slot), out e) && e.Native != null &&
                object.ReferenceEquals(e.Owner, GameData.PlayerStats) &&
                (e.Native.AssignedSpell != null || e.Native.AssignedSkill != null || e.Native.AssignedItem != null);
        }
        internal static Sprite ActionIcon(int bar, int slot)
        {
            Entry e;
            return Slots.TryGetValue(Key(bar, slot), out e) && e.Native != null &&
                e.Native.MyImage != null ? e.Native.MyImage.sprite : null;
        }
        /// <summary>Read-only snapshot for the Phase 9 tracker. No native tasks run.</summary>
        internal static bool TryReadTrackerCooldown(int bar, int slot, out string key,
            out string name, out Sprite icon, out float remaining)
        {
            key = name = null; icon = null; remaining = 0f;
            Entry e;
            if (!Slots.TryGetValue(Key(bar, slot), out e) || e.Native == null ||
                !object.ReferenceEquals(e.Owner, GameData.PlayerStats)) return false;
            Hotkeys hk = e.Native;
            key = NativeActionKey(hk);
            if (string.IsNullOrEmpty(key)) return false;
            if (hk.AssignedSpell != null) name = hk.AssignedSpell.SpellName;
            else if (hk.AssignedSkill != null) name = hk.AssignedSkill.SkillName;
            else if (hk.AssignedItem != null) name = "Assigned Item";
            icon = hk.MyImage != null ? hk.MyImage.sprite : null;
            remaining = Mathf.Max(0f, hk.Cooldown / 60f);
            return true;
        }

        internal static float CooldownSeconds(int bar, int slot)
        {
            Entry e;
            return Slots.TryGetValue(Key(bar, slot), out e) && e.Native != null &&
                object.ReferenceEquals(e.Owner, GameData.PlayerStats)
                ? Mathf.Max(0f, e.Native.Cooldown / 60f) : 0f;
        }

        // Native assignment UI (book, inventory, skills) can call these methods
        // itself via the 2D collider. Persist changes ONLY on ArcaneUI-owned HKs.
        internal static void NativeAssignmentChanged(Hotkeys hk)
        {
            if (_restoring) return;
            Entry e;
            if (hk == null || !Owned.TryGetValue(hk, out e) ||
                !object.ReferenceEquals(e.Owner, GameData.PlayerStats) ||
                Time.frameCount <= e.CreatedFrame + 1) return;
            string kind = null, id = null, name = null;
            object value = null;
            if (hk.AssignedSpell != null && !string.IsNullOrEmpty(hk.AssignedSpell.Id))
            {
                kind = "Spell"; id = hk.AssignedSpell.Id;
                name = hk.AssignedSpell.SpellName; value = hk.AssignedSpell;
            }
            else if (hk.AssignedSkill != null)
            {
                kind = "Skill"; id = hk.AssignedSkill.Id;
                name = hk.AssignedSkill.SkillName; value = hk.AssignedSkill;
            }
            else if (hk.AssignedItem != null)
            {
                kind = "Item";
                value = hk.AssignedItem;
                id = NativeActionBarDiscovery.ItemIdentity(value);
                // An inventory slot index remains a safe fallback when a game
                // build does not expose a persistent item type ID.
                if (string.IsNullOrEmpty(id) && hk.InvSlotIndex >= 0)
                    id = "InvSlot:" + hk.InvSlotIndex;
                object itemName = hk.AssignedItem.MyItem == null ? null :
                    (object)hk.AssignedItem.MyItem.GetType().GetField("ItemName", Flags)?.GetValue(hk.AssignedItem.MyItem);
                name = itemName as string ?? "Assigned item";
            }
            if (string.IsNullOrEmpty(id))
            {
                // Native ClearMe can also be invoked by the game after a
                // consumable stack runs out. Do not retain a dead assignment.
                Clear(e.Bar, e.Slot);
                return;
            }
            ActionBarIndependentAssignments.StoreLive(e.Bar, e.Slot, kind, id, name, value);
            e.LoadedIdentity = kind + ":" + id;
            e.Attempted = true;
        }

        // Our custom keybind handler drives these components. Suppress their
        // automatic native Update ONLY; native primary slots remain untouched.
        internal static bool AllowNativeUpdate(Hotkeys hk) { return !IsOwned(hk); }
        internal static bool AllowNativeTask(Hotkeys hk)
        {
            Entry e;
            if (hk == null || !Owned.TryGetValue(hk, out e)) return true;
            // A native OnMouseDown handler and the ArcaneUI Button may fire in
            // the same frame. Execute the actual native action exactly once.
            if (e.LastTaskFrame == Time.frameCount) return false;
            e.LastTaskFrame = Time.frameCount;
            return true;
        }
        /// <summary>
        /// Native cooldown reset can happen at any time, including externally
        /// from a class proc. If one live hotkey snaps from a substantial CD to
        /// ready too soon, all of ArcaneUI's duplicate instances and estimates
        /// must forget that cooldown. Never write to native primary hotkeys.
        /// </summary>
        internal static void ResetVisualCooldown(string kind, string identity)
        {
            if (string.IsNullOrEmpty(kind) || string.IsNullOrEmpty(identity)) return;
            string key = kind + ":" + identity;
            RebuildCooldowns.Remove(key);
            if (kind == "Spell")
            {
                NativeActionBarDiscovery.ForgetEstimatedSpellCooldown(identity);
                ClickCastingManager.ResetCooldown(identity);
            }
            foreach (Entry e in Slots.Values)
            {
                if (e.Native == null || !object.ReferenceEquals(e.Owner, GameData.PlayerStats) ||
                    NativeActionKey(e.Native) != key) continue;
                e.Native.Cooldown = 0f;
                e.PreviousCooldownUnits = 0f;
                e.PreviousCooldownTime = Time.time;
            }
        }

        internal static void Tick(float delta)
        {
            ArcaneUiOwnedHotkeyHoverPatch.Ensure();
            if (delta <= 0f) return;
            string resetKey = null;
            foreach (Entry e in Slots.Values)
            {
                if (e.Native == null || !object.ReferenceEquals(e.Owner, GameData.PlayerStats)) continue;
                float now = Time.time;
                float live = Mathf.Max(0f, e.Native.Cooldown);
                // A normal last tick reaches zero at the predicted time; an
                // instant reset leaps ahead significantly. Two-frame sampling
                // catches resets even when no native primary HK holds the spell.
                float expected = Mathf.Max(0f, e.PreviousCooldownUnits -
                    (now - e.PreviousCooldownTime) * 60f);
                if (e.PreviousCooldownUnits > 90f && live <= 0.01f && expected > 30f)
                    resetKey = NativeActionKey(e.Native);
                if (live > 0f) e.Native.Cooldown = Mathf.Max(0f, live - 60f * delta);
                e.PreviousCooldownUnits = Mathf.Max(0f, e.Native.Cooldown);
                e.PreviousCooldownTime = now;
            }
            if (!string.IsNullOrEmpty(resetKey))
            {
                int divider = resetKey.IndexOf(':');
                if (divider > 0)
                    ResetVisualCooldown(resetKey.Substring(0, divider), resetKey.Substring(divider + 1));
            }
        }

        internal static void SpellCompleted(SpellVessel vessel, bool noCooldown)
        {
            if (vessel == null || vessel.isProc || vessel.spell == null) return;
            try
            {
                if (_spellSource == null) _spellSource = typeof(SpellVessel).GetField("SpellSource", Flags);
                CastSpell source = _spellSource == null ? null : _spellSource.GetValue(vessel) as CastSpell;
                if (source == null || !source.isPlayer || source.MyChar == null ||
                    GameData.PlayerControl == null || source.MyChar != GameData.PlayerControl.Myself) return;
                float units = noCooldown ? 20f : Mathf.Max(0f, vessel.spell.Cooldown * 60f);
                if (!noCooldown)
                {
                    if (_cooldownMultiplier == null) _cooldownMultiplier = typeof(SpellVessel).GetField("CDMult", Flags);
                    if (_damageScale == null) _damageScale = typeof(SpellVessel).GetField("scaleDmg", Flags);
                    if (_cooldownMultiplier != null) units *= Convert.ToSingle(_cooldownMultiplier.GetValue(vessel));
                    if (_damageScale != null) units *= Convert.ToSingle(_damageScale.GetValue(vessel));
                }
                ClickCastingManager.SpellCompleted(vessel.spell, units / 60f, noCooldown);
                foreach (Entry e in Slots.Values)
                    if (e.Native != null && object.ReferenceEquals(e.Owner, GameData.PlayerStats) &&
                        ((e.Native.AssignedSpell != null &&
                          e.Native.AssignedSpell.Id == vessel.spell.Id) ||
                         (e.Native.AssignedItem != null && e.Native.AssignedItem.MyItem != null &&
                          e.Native.AssignedItem.MyItem.ItemEffectOnClick == vessel.spell)))
                        e.Native.Cooldown = Mathf.Max(e.Native.Cooldown, units);
            }
            catch (Exception ex) { Warn("Native completion cooldown unavailable: " + ex.GetType().Name); }
        }

        private static void Warn(string message)
        {
            if (Time.unscaledTime < _nextWarning) return;
            _nextWarning = Time.unscaledTime + 4f;
            Plugin.LogWarning("ArcaneUI NativeExtraHotkeySlots: " + message);
        }
    }

    // Existing native hotkeys are not intercepted. Only ArcaneUI-owned extra
    // Hotkeys components are excluded from automatic native input processing.
    [HarmonyPatch(typeof(Hotkeys), "Update")]
    internal static class ArcaneUiExtraHotkeyUpdatePatch
    {
        private static bool Prefix(Hotkeys __instance)
        {
            return NativeExtraHotkeySlots.AllowNativeUpdate(__instance);
        }
    }
    [HarmonyPatch(typeof(Hotkeys), "DoHotkeyTask")]
    internal static class ArcaneUiExtraHotkeyTaskPatch
    {
        private static bool Prefix(Hotkeys __instance)
        {
            return NativeExtraHotkeySlots.AllowNativeTask(__instance);
        }
    }
    [HarmonyPatch(typeof(Hotkeys), "AssignSpellFromBook")]
    internal static class ArcaneUiExtraSpellAssignmentPatch
    {
        private static void Postfix(Hotkeys __instance) { NativeExtraHotkeySlots.NativeAssignmentChanged(__instance); }
    }
    [HarmonyPatch(typeof(Hotkeys), "AssignSkillFromBook")]
    internal static class ArcaneUiExtraSkillAssignmentPatch
    {
        private static void Postfix(Hotkeys __instance) { NativeExtraHotkeySlots.NativeAssignmentChanged(__instance); }
    }
    [HarmonyPatch(typeof(Hotkeys), "AssignItemFrominv")]
    internal static class ArcaneUiExtraItemAssignmentPatch
    {
        private static void Postfix(Hotkeys __instance) { NativeExtraHotkeySlots.NativeAssignmentChanged(__instance); }
    }
    [HarmonyPatch(typeof(Hotkeys), "ClearMe")]
    internal static class ArcaneUiExtraClearPatch
    {
        private static void Postfix(Hotkeys __instance) { NativeExtraHotkeySlots.NativeAssignmentChanged(__instance); }
    }
    // Optional hover interception lives outside PatchAll, since some Erenshor
    // versions do not expose these native tooltip entry points. Never fail
    // ArcaneUI initialization just because a tooltip method was renamed.
    internal static class ArcaneUiOwnedHotkeyHoverPatch
    {
        private static bool _attempted;
        internal static void Ensure()
        {
            if (_attempted) return;
            _attempted = true;
            var harmony = new Harmony("arcanes.groupframes.owned-tooltip");
            var callback = typeof(ArcaneUiOwnedHotkeyHoverPatch).GetMethod("Prefix",
                BindingFlags.Static | BindingFlags.NonPublic);
            if (callback == null) return;
            string[] names = { "OnMouseEnter", "OnPointerEnter", "ShowTooltip", "ShowToolTip",
                "DisplayTooltip", "DisplayToolTip" };
            BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            foreach (MethodInfo native in typeof(Hotkeys).GetMethods(flags))
            {
                bool match = false;
                foreach (string name in names)
                    if (native.Name == name) { match = true; break; }
                if (!match || native.IsAbstract || native.IsGenericMethod) continue;
                try { harmony.Patch(native, prefix: new HarmonyMethod(callback)); }
                catch (Exception ex)
                {
                    Plugin.LogWarning("ArcaneUI extra hotkey tooltip guard: " +
                        native.Name + " unavailable (" + ex.GetType().Name + ").");
                }
            }
        }
        private static bool Prefix(Hotkeys __instance)
        {
            return !NativeExtraHotkeySlots.IsOwned(__instance);
        }
    }
    [HarmonyPatch(typeof(SpellVessel), "EndSpell")]
    internal static class ArcaneUiExtraSpellCompletionPatch
    {
        private static void Postfix(SpellVessel __instance) { NativeExtraHotkeySlots.SpellCompleted(__instance, false); }
    }
    [HarmonyPatch(typeof(SpellVessel), "EndSpellNoCD")]
    internal static class ArcaneUiExtraSpellNoCooldownPatch
    {
        private static void Postfix(SpellVessel __instance) { NativeExtraHotkeySlots.SpellCompleted(__instance, true); }
    }
}
