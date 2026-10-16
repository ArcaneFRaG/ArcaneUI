using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ArcanesGroupFrames
{
    /// <summary>
    /// Persists the exact custom raid-frame layout alongside Erenshor's
    /// native raid Save / Load flow.
    ///
    /// Erenshor remains authoritative for which SimPlayers belong to the
    /// saved raid. We persist only the presentation/formation information
    /// that the native save does not reliably preserve for the replacement
    /// raid frames: GroupNumber and SlotIndex for each SimPlayer.
    /// </summary>
    internal static class RaidLayoutPersistence
    {
        private const string Prefix =
            "ArcanesGroupFrames.RaidLayout.";

        private const string CountKey =
            Prefix + "Count";

        private const float RestoreTimeoutSeconds =
            8f;
        // Erenshor can rewrite raid slots a frame or more after its Load
        // callback. Keep observing for a short settle window after the first
        // successful restoration rather than accepting the old roster instantly.
        private const float RestoreSettleSeconds = 2.5f;

        private static bool _restorePending;
        private static float _restoreDeadline;
        private static float _nextRestoreAttempt;
        private static float _restoreStartedAt;
        private static float _lastNativeOverwrite;
        private static bool _hasRestored;


        // ============================================================
        // SAVE
        // ============================================================

        internal static void CaptureCurrentLayout()
        {
            RaidManager manager =
                GameData.RaidManager;

            if (!GameData.RaidActive ||
                manager == null ||
                manager.Raiders == null)
            {
                Plugin.LogWarning(
                    "Raid layout was not saved because no active raid roster is available.");

                return;
            }


            List<RaidMemberSlot> occupied =
                GetOccupiedRaidSlots(
                    manager);


            // Remove stale entries from a previous, larger saved layout.
            int previousCount =
                Mathf.Max(
                    0,
                    PlayerPrefs.GetInt(
                        CountKey,
                        0));

            for (int i = 0;
                 i < previousCount;
                 i++)
            {
                DeleteEntry(
                    i);
            }


            int saved =
                0;

            occupied.Sort(
                delegate(
                    RaidMemberSlot left,
                    RaidMemberSlot right)
                {
                    int groupCompare =
                        left.GroupNumber.CompareTo(
                            right.GroupNumber);

                    return groupCompare != 0
                        ? groupCompare
                        : left.SlotIndex.CompareTo(
                            right.SlotIndex);
                });


            for (int i = 0;
                 i < occupied.Count;
                 i++)
            {
                RaidMemberSlot slot =
                    occupied[i];

                string simName =
                    GetStableSimName(
                        slot);

                if (string.IsNullOrEmpty(
                        simName))
                {
                    continue;
                }

                PlayerPrefs.SetString(
                    EntryKey(saved, "Name"),
                    simName);

                PlayerPrefs.SetInt(
                    EntryKey(saved, "Group"),
                    Mathf.Clamp(
                        slot.GroupNumber,
                        1,
                        3));

                PlayerPrefs.SetInt(
                    EntryKey(saved, "Slot"),
                    slot.SlotIndex);

                saved++;
            }


            PlayerPrefs.SetInt(
                CountKey,
                saved);

            PlayerPrefs.Save();


            Plugin.LogInfo(
                "Saved exact raid layout for " +
                saved +
                " SimPlayer(s).");
        }


        // ============================================================
        // LOAD / RETRY
        // ============================================================

        internal static void BeginRestoreAfterNativeLoad()
        {
            if (!HasSavedLayout())
            {
                Plugin.LogInfo(
                    "No custom raid layout has been saved yet; native raid load will be used unchanged.");

                return;
            }


            _restorePending =
                true;
            _hasRestored = false;
            _restoreStartedAt = Time.unscaledTime;
            _lastNativeOverwrite = Time.unscaledTime;

            _restoreDeadline =
                Time.unscaledTime +
                RestoreTimeoutSeconds;

            _nextRestoreAttempt =
                0f;


            // Do NOT restore immediately: this can match the previous roster
            // before native Load has rebuilt its SimPlayer groups. That was
            // the cause of having to press LOAD twice. Update observes the
            // new roster across frames and reapplies if native code overwrites.

        }


        internal static void Update()
        {
            if (!_restorePending ||
                Time.unscaledTime <
                _nextRestoreAttempt)
            {
                return;
            }


            bool finalAttempt =
                Time.unscaledTime >=
                _restoreDeadline;


            if (TryRestoreSavedLayout(finalAttempt))
            {
                _hasRestored = true;
                // A successful first attempt does not end the restore: native
                // load can still change GroupNumber/SlotIndex later that frame.
                if (finalAttempt ||
                    (Time.unscaledTime >= _restoreStartedAt + RestoreSettleSeconds &&
                     Time.unscaledTime >= _lastNativeOverwrite + 0.6f))
                {
                    _restorePending = false;
                    Plugin.LogInfo("Raid layout restored from one Load action; native roster settled.");
                    return;
                }
            }
            if (finalAttempt)
            {
                _restorePending = false;
                if (!_hasRestored)
                    Plugin.LogWarning("Raid layout restore timed out; not all saved Sims were available.");
                return;
            }

            _nextRestoreAttempt =
                Time.unscaledTime +
                0.10f;
        }


        private static bool TryRestoreSavedLayout(
            bool finalAttempt)
        {
            RaidManager manager =
                GameData.RaidManager;

            if (!GameData.RaidActive ||
                manager == null ||
                manager.Raiders == null)
            {
                return false;
            }


            List<SavedRaidMember> saved =
                ReadSavedLayout();

            if (saved.Count == 0)
            {
                return true;
            }


            List<RaidMemberSlot> occupied =
                GetOccupiedRaidSlots(
                    manager);

            if (occupied.Count == 0)
            {
                return false;
            }


            Dictionary<string, RaidMemberSlot> currentByName =
                new Dictionary<string, RaidMemberSlot>(
                    StringComparer.OrdinalIgnoreCase);

            for (int i = 0;
                 i < occupied.Count;
                 i++)
            {
                RaidMemberSlot slot =
                    occupied[i];

                string name =
                    GetStableSimName(
                        slot);

                if (!string.IsNullOrEmpty(name) &&
                    !currentByName.ContainsKey(name))
                {
                    currentByName.Add(
                        name,
                        slot);
                }
            }


            int matched =
                0;

            HashSet<RaidMemberSlot> matchedSlots =
                new HashSet<RaidMemberSlot>();

            HashSet<int> claimedSlotIndexes =
                new HashSet<int>();

            Dictionary<RaidMemberSlot, SavedRaidMember> assignments =
                new Dictionary<RaidMemberSlot, SavedRaidMember>();


            for (int i = 0;
                 i < saved.Count;
                 i++)
            {
                SavedRaidMember entry =
                    saved[i];

                RaidMemberSlot slot;

                if (!currentByName.TryGetValue(
                        entry.SimName,
                        out slot) ||
                    slot == null ||
                    matchedSlots.Contains(slot) ||
                    claimedSlotIndexes.Contains(
                        entry.SlotIndex))
                {
                    continue;
                }


                matchedSlots.Add(
                    slot);

                claimedSlotIndexes.Add(
                    entry.SlotIndex);

                assignments.Add(
                    slot,
                    entry);

                matched++;
            }


            bool hasChanges = false;
            foreach (KeyValuePair<RaidMemberSlot, SavedRaidMember> pair in assignments)
                if (pair.Key.GroupNumber != pair.Value.GroupNumber ||
                    pair.Key.SlotIndex != pair.Value.SlotIndex)
                { hasChanges = true; break; }
            bool groupListsMatch = true;
            IList first = manager.Group1 as IList;
            IList second = manager.Group2 as IList;
            IList third = manager.Group3 as IList;
            foreach (RaidMemberSlot slot in occupied)
            {
                IList list = slot.GroupNumber == 1 ? first :
                    (slot.GroupNumber == 2 ? second : third);
                if (list == null || !list.Contains(slot))
                {
                    groupListsMatch = false;
                    break;
                }
            }
            if (hasChanges || !groupListsMatch)
                _lastNativeOverwrite = Time.unscaledTime;
            // Avoid tearing down/recreating raid frames 10 times per second
            // during the native-load settle window if nothing has changed.
            if (matched == saved.Count && !hasChanges && groupListsMatch)
                return true;

            // During the normal native load path every saved Sim should become
            // available. Wait briefly before finalizing a partial layout so a
            // late-spawning Sim does not cause the rest of the formation to be
            // compacted into the wrong slots.
            if (matched < saved.Count &&
                !finalAttempt)
            {
                return false;
            }


            HashSet<int> availableIndexes =
                new HashSet<int>();

            foreach (RaidMemberSlot nativeSlot
                     in manager.Raiders)
            {
                if (nativeSlot != null)
                {
                    availableIndexes.Add(
                        nativeSlot.SlotIndex);
                }
            }

            // Saved indexes came from this native slot space. Include them in
            // case Erenshor changed the occupied slot mapping while loading.
            for (int i = 0;
                 i < saved.Count;
                 i++)
            {
                availableIndexes.Add(
                    saved[i].SlotIndex);
            }


            // Apply all saved members first.
            foreach (KeyValuePair<RaidMemberSlot, SavedRaidMember> pair
                     in assignments)
            {
                RaidMemberSlot slot =
                    pair.Key;

                SavedRaidMember entry =
                    pair.Value;

                slot.GroupNumber =
                    Mathf.Clamp(
                        entry.GroupNumber,
                        1,
                        3);

                slot.SlotIndex =
                    entry.SlotIndex;
            }


            // Keep unsaved/new raid members without allowing them to steal a
            // saved member's exact SlotIndex. Prefer their current position,
            // then the first unclaimed native slot.
            List<int> freeIndexes =
                new List<int>();

            foreach (int index in availableIndexes)
            {
                if (!claimedSlotIndexes.Contains(index))
                {
                    freeIndexes.Add(index);
                }
            }

            freeIndexes.Sort();


            occupied.Sort(
                delegate(
                    RaidMemberSlot left,
                    RaidMemberSlot right)
                {
                    return left.SlotIndex.CompareTo(
                        right.SlotIndex);
                });


            int freeCursor =
                0;

            for (int i = 0;
                 i < occupied.Count;
                 i++)
            {
                RaidMemberSlot slot =
                    occupied[i];

                if (matchedSlots.Contains(slot))
                {
                    continue;
                }


                int chosen =
                    -1;

                if (!claimedSlotIndexes.Contains(
                        slot.SlotIndex) &&
                    availableIndexes.Contains(
                        slot.SlotIndex))
                {
                    chosen =
                        slot.SlotIndex;
                }
                else
                {
                    while (freeCursor < freeIndexes.Count &&
                           claimedSlotIndexes.Contains(
                               freeIndexes[freeCursor]))
                    {
                        freeCursor++;
                    }

                    if (freeCursor < freeIndexes.Count)
                    {
                        chosen =
                            freeIndexes[freeCursor++];
                    }
                }


                if (chosen >= 0)
                {
                    slot.SlotIndex =
                        chosen;

                    claimedSlotIndexes.Add(
                        chosen);
                }


                if (slot.GroupNumber < 1 ||
                    slot.GroupNumber > 3)
                {
                    slot.GroupNumber =
                        1;
                }
            }


            RebuildNativeGroupLists(
                manager,
                occupied);


            for (int i = 0;
                 i < occupied.Count;
                 i++)
            {
                SynchronizeAvatarRaidIndex(
                    occupied[i]);
            }


            RaidFramesUI
                .ForceRosterRefresh();


            if (matched == saved.Count)
                return true;


            if (finalAttempt)
            {
                Plugin.LogWarning(
                    "Restored saved raid layout for " +
                    matched +
                    " of " +
                    saved.Count +
                    " SimPlayer(s). Missing saved members were left absent and current unsaved members kept available slots.");

                return true;
            }


            return false;
        }


        // ============================================================
        // NATIVE RAID GROUP SYNCHRONIZATION
        // ============================================================

        private static void RebuildNativeGroupLists(
            RaidManager manager,
            List<RaidMemberSlot> occupied)
        {
            if (manager == null)
            {
                return;
            }


            IList group1 =
                manager.Group1 as IList;

            IList group2 =
                manager.Group2 as IList;

            IList group3 =
                manager.Group3 as IList;


            if (group1 == null ||
                group2 == null ||
                group3 == null)
            {
                Plugin.LogWarning(
                    "Could not synchronize native raid group lists while restoring saved layout.");

                return;
            }


            group1.Clear();
            group2.Clear();
            group3.Clear();


            occupied.Sort(
                delegate(
                    RaidMemberSlot left,
                    RaidMemberSlot right)
                {
                    return left.SlotIndex.CompareTo(
                        right.SlotIndex);
                });


            for (int i = 0;
                 i < occupied.Count;
                 i++)
            {
                RaidMemberSlot slot =
                    occupied[i];

                IList destination =
                    slot.GroupNumber == 1
                        ? group1
                        : (slot.GroupNumber == 2
                            ? group2
                            : group3);

                destination.Add(
                    slot);
            }
        }


        private static void SynchronizeAvatarRaidIndex(
            RaidMemberSlot slot)
        {
            if (slot == null ||
                slot.AssignedAvatar == null)
            {
                return;
            }


            NPC npc =
                slot.AssignedAvatar.GetThisNPC();

            if (npc != null)
            {
                npc.indexInRaid =
                    slot.SlotIndex;

                npc.MyRaidSlot =
                    slot;
            }
        }


        // ============================================================
        // PERSISTENCE HELPERS
        // ============================================================

        private static bool HasSavedLayout()
        {
            return PlayerPrefs.GetInt(
                       CountKey,
                       0) > 0;
        }


        private static List<SavedRaidMember> ReadSavedLayout()
        {
            List<SavedRaidMember> result =
                new List<SavedRaidMember>();

            int count =
                Mathf.Max(
                    0,
                    PlayerPrefs.GetInt(
                        CountKey,
                        0));


            for (int i = 0;
                 i < count;
                 i++)
            {
                string name =
                    PlayerPrefs.GetString(
                        EntryKey(i, "Name"),
                        string.Empty);

                int group =
                    PlayerPrefs.GetInt(
                        EntryKey(i, "Group"),
                        0);

                int slot =
                    PlayerPrefs.GetInt(
                        EntryKey(i, "Slot"),
                        -1);


                if (string.IsNullOrEmpty(name) ||
                    group < 1 ||
                    group > 3 ||
                    slot < 0)
                {
                    continue;
                }


                SavedRaidMember entry =
                    new SavedRaidMember();

                entry.SimName =
                    name;

                entry.GroupNumber =
                    group;

                entry.SlotIndex =
                    slot;

                result.Add(
                    entry);
            }


            return result;
        }


        private static void DeleteEntry(
            int index)
        {
            PlayerPrefs.DeleteKey(
                EntryKey(index, "Name"));

            PlayerPrefs.DeleteKey(
                EntryKey(index, "Group"));

            PlayerPrefs.DeleteKey(
                EntryKey(index, "Slot"));
        }


        private static string EntryKey(
            int index,
            string field)
        {
            return Prefix +
                   "Member." +
                   index +
                   "." +
                   field;
        }


        private static List<RaidMemberSlot> GetOccupiedRaidSlots(
            RaidManager manager)
        {
            List<RaidMemberSlot> result =
                new List<RaidMemberSlot>();


            if (manager == null ||
                manager.Raiders == null)
            {
                return result;
            }


            foreach (RaidMemberSlot slot
                     in manager.Raiders)
            {
                if (slot == null ||
                    (slot.AssignedSimTracking == null &&
                     slot.AssignedAvatar == null))
                {
                    continue;
                }


                string name =
                    GetStableSimName(
                        slot);

                if (string.IsNullOrEmpty(name))
                {
                    continue;
                }


                result.Add(
                    slot);
            }


            return result;
        }


        private static string GetStableSimName(
            RaidMemberSlot slot)
        {
            if (slot == null)
            {
                return null;
            }


            if (slot.AssignedSimTracking != null &&
                !string.IsNullOrEmpty(
                    slot.AssignedSimTracking.SimName))
            {
                return slot.AssignedSimTracking.SimName;
            }


            if (slot.AssignedAvatar != null)
            {
                NPC npc =
                    slot.AssignedAvatar.GetThisNPC();

                if (npc != null &&
                    !string.IsNullOrEmpty(
                        npc.NPCName))
                {
                    return npc.NPCName;
                }
            }


            return null;
        }


        private sealed class SavedRaidMember
        {
            internal string SimName;
            internal int GroupNumber;
            internal int SlotIndex;
        }
    }
}
