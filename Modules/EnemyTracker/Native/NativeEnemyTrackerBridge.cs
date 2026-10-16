using System;
using System.Collections.Generic;
using UnityEngine;

namespace ArcanesGroupFrames.Modules.EnemyTracker.Native
{
    /// <summary>
    /// Binds to the REAL Erenshor TrackEngagedTargets/TrackedTarget objects.
    /// No UI-name guessing, NPC creation, fake aggro lists or substitute Sim AI.
    /// See decompiled TrackEngagedTargets.cs and TrackedTarget.cs.
    /// </summary>
    internal sealed class NativeEnemyTrackerBridge
    {
        internal sealed class Entry
        {
            internal TrackedTarget Row;
            internal Character Enemy;
            internal string Name;
            internal int Level;
            internal int Health, MaxHealth;
            internal int NativeOrder;
            internal float HealthFraction;
            internal bool Selected;
            // Raid target assignments are authoritative game-owned state. Party
            // activity is derived from each companion's CURRENT aggro target.
            internal int RaidGroupMask;
            internal bool Urgent;
            internal int PartyAttackers, PartyMembers;
        }

        private TrackEngagedTargets _tracker;
        private readonly List<Entry> _entries = new List<Entry>(16);
        private float _nextProbe;
        private sealed class NativeRowState
        {
            internal CanvasGroup Group;
            internal bool Added;
            internal float Alpha;
            internal bool Interactable, BlocksRaycasts;
        }
        private readonly Dictionary<TrackedTarget, NativeRowState> _suppressedRows =
            new Dictionary<TrackedTarget, NativeRowState>();
        internal bool NativeHidden { get { return _suppressedRows.Count > 0; } }
        internal int ActiveNativeEnemyCount { get; private set; }
        internal IList<Entry> Entries { get { return _entries; } }
        internal bool Bound { get { return _tracker != null && _tracker.TrackedTargetBars != null; } }
        internal bool RaidActive { get { return GameData.RaidActive; } }
        internal string Path { get { return _tracker == null ? "Unbound" : Hierarchy(_tracker.transform); } }
        internal string Status { get; private set; } = "Not inspected.";

        private static string Hierarchy(Transform transform)
        {
            string path = "";
            for (int i = 0; transform != null && i < 18; i++, transform = transform.parent)
                path = "/" + transform.name + path;
            return path;
        }

        private bool ValidTracker(TrackEngagedTargets tracker)
        {
            return tracker != null && tracker.gameObject != null && tracker.gameObject.scene.IsValid() &&
                tracker.gameObject.activeInHierarchy && tracker.TrackedTargetBars != null &&
                tracker.TrackedTargetBars.Count > 0;
        }

        private void EnsureTracker(bool force)
        {
            if (ValidTracker(_tracker)) return;
            RestoreNative();
            _tracker = null;
            if (!force && Time.unscaledTime < _nextProbe) return;
            _nextProbe = Time.unscaledTime + 3f;
            TrackEngagedTargets[] controllers = UnityEngine.Object.FindObjectsOfType<TrackEngagedTargets>();
            foreach (TrackEngagedTargets candidate in controllers)
                if (ValidTracker(candidate)) { _tracker = candidate; break; }
        }

        internal void Update(bool force = false)
        {
            _entries.Clear();
            ActiveNativeEnemyCount = 0;
            if (GameData.InCharSelect) { Status = "Unavailable at character select; original interface preserved."; return; }
            EnsureTracker(force);
            if (!Bound) { Status = "Native TrackEngagedTargets not found; original interface preserved."; return; }
            var bars = _tracker.TrackedTargetBars;
            for (int index = 0; index < bars.Count; ++index)
            {
                TrackedTarget bar = bars[index];
                if (bar == null || !bar.gameObject.activeInHierarchy) continue;
                Character enemy = bar.TrackedChar;
                if (enemy == null || !enemy.Alive || enemy.MyStats == null || enemy.MyStats.CurrentHP <= 0) continue;
                ActiveNativeEnemyCount++;
                // If native tracks more enemies than the custom list can display,
                // continue counting all entries before deciding whether to hide.
                if (_entries.Count >= 20) continue;
                int max = Mathf.Max(1, enemy.MyStats.CurrentMaxHP);
                string name = bar.TargetName != null ? bar.TargetName.text : null;
                if (string.IsNullOrEmpty(name)) name = enemy.transform != null ? enemy.transform.name : "Enemy";
                int groups = 0;
                bool urgent = false;
                if (GameData.RaidActive && GameData.RaidManager != null)
                {
                    RaidManager raid = GameData.RaidManager;
                    if (raid.Group1Target == enemy) groups |= 1;
                    if (raid.Group2Target == enemy) groups |= 2;
                    if (raid.Group3Target == enemy) groups |= 4;
                    urgent = raid.UrgentTarget == enemy;
                }
                int partyMembers = 0, attackers = 0;
                if (!GameData.RaidActive && GameData.GroupMembers != null)
                {
                    foreach (SimPlayerTracking member in GameData.GroupMembers)
                    {
                        if (member == null || member.MyAvatar == null) continue;
                        partyMembers++;
                        NPC npc = member.MyAvatar.GetThisNPC();
                        if (npc != null && npc.CurrentAggroTarget == enemy) attackers++;
                    }
                }
                _entries.Add(new Entry {
                    Row = bar, Enemy = enemy, Name = name, Level = enemy.MyStats.Level,
                    NativeOrder = index,
                    Health = enemy.MyStats.CurrentHP, MaxHealth = max,
                    HealthFraction = Mathf.Clamp01((float)enemy.MyStats.CurrentHP / max),
                    Selected = GameData.PlayerControl != null && GameData.PlayerControl.CurrentTarget == enemy,
                    RaidGroupMask = groups, Urgent = urgent,
                    PartyAttackers = attackers, PartyMembers = partyMembers
                });
            }
            Status = "Native TrackEngagedTargets bound; " + ActiveNativeEnemyCount + " active enemy row(s), " +
                (RaidActive ? "raid group orders" : "party attack orders") + ".";
        }

        private bool IsCurrent(Entry entry)
        {
            if (!Bound || entry == null || entry.Row == null || entry.Enemy == null ||
                !entry.Enemy.Alive || entry.Row.TrackedChar != entry.Enemy ||
                !entry.Row.gameObject.activeInHierarchy || GameData.PlayerControl == null) return false;
            // The native coroutine can reuse rows between our sampling and a click.
            // Never send orders to an enemy unless that native row still belongs
            // to the bound TrackEngagedTargets instance.
            return _tracker.TrackedTargetBars.Contains(entry.Row);
        }

        internal bool Command(Entry entry, int command)
        {
            if (!IsCurrent(entry)) return false;
            try
            {
                if (command == 0) entry.Row.TargetTracked();
                else if (command == 1 && !RaidActive) entry.Row.GroupAttack();
                else if (command >= 2 && command <= 5 && RaidActive) entry.Row.RaidGroupAttack(command - 1);
                else return false;
                return true;
            }
            catch (Exception ex)
            {
                Plugin.LogWarning("[ArcaneUI Enemy Tracker] Native command failed: " + ex.Message);
                return false;
            }
        }

        // Suppress only the actual native TrackedTarget row objects, NEVER the
        // TrackEngagedTargets controller or shared HUD parent. Disabling their
        // GameObjects would stop the native coroutine and invalidate our binding.
        // CanvasGroup is reversible and does not affect activeInHierarchy.
        internal void SetNativeHidden(bool hide)
        {
            if (!hide || !Bound) { RestoreNative(); return; }
            var bars = _tracker.TrackedTargetBars;
            foreach (TrackedTarget bar in bars)
            {
                if (bar == null || bar.gameObject == null) continue;
                NativeRowState state;
                if (!_suppressedRows.TryGetValue(bar, out state))
                {
                    CanvasGroup group = bar.GetComponent<CanvasGroup>();
                    bool added = group == null;
                    if (added) group = bar.gameObject.AddComponent<CanvasGroup>();
                    state = new NativeRowState {
                        Group = group, Added = added, Alpha = group.alpha,
                        Interactable = group.interactable, BlocksRaycasts = group.blocksRaycasts
                    };
                    _suppressedRows[bar] = state;
                }
                if (state.Group == null) continue;
                state.Group.alpha = 0f;
                state.Group.interactable = false;
                state.Group.blocksRaycasts = false;
            }
        }

        internal void RestoreNative()
        {
            if (_suppressedRows.Count == 0) return;
            foreach (KeyValuePair<TrackedTarget, NativeRowState> pair in _suppressedRows)
            {
                NativeRowState state = pair.Value;
                if (state.Group == null) continue;
                // If we created the group, remove only our component. For
                // preexisting groups, restore the exact captured game values.
                if (state.Added) UnityEngine.Object.Destroy(state.Group);
                else
                {
                    state.Group.alpha = state.Alpha;
                    state.Group.interactable = state.Interactable;
                    state.Group.blocksRaycasts = state.BlocksRaycasts;
                }
            }
            _suppressedRows.Clear();
        }

        internal void Inspect()
        {
            _tracker = null;
            _nextProbe = 0f;
            Update(true);
            Plugin.LogInfo("[ArcaneUI Enemy Tracker] BIND " + Status + " path=" + Path);
            if (Bound)
            {
                Plugin.LogInfo("[ArcaneUI Enemy Tracker] Native bars=" + _tracker.TrackedTargetBars.Count +
                    " tracked characters=" + (_tracker.TrackedCharacters != null ? _tracker.TrackedCharacters.Count : 0));
                foreach (Entry row in _entries)
                    Plugin.LogInfo("[ArcaneUI Enemy Tracker] ROW '" + row.Name + "' level=" + row.Level +
                        " hp=" + row.Health + "/" + row.MaxHealth + " native=" + Hierarchy(row.Row.transform));
            }
        }

        internal void Reset() { RestoreNative(); _tracker = null; _entries.Clear(); ActiveNativeEnemyCount = 0; _nextProbe = 0f; Status = "Not inspected."; }
    }
}
