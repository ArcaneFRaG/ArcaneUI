using System;
using System.Collections.Generic;
using UnityEngine;
using ArcanesGroupFrames.Framework.NativeUI;

namespace ArcanesGroupFrames.Framework.NativeUI.Status
{
    /// <summary>
    /// Visual-only native status icon suppression. The Erenshor source binds the
    /// player's actual tray through Stats.StatusIcons (StatusEffectIcon) and the
    /// target's through ShowTargetEffects.AllIcons (TargetStatusIcon). Do not guess
    /// from hierarchy names: native icons need not live under PlayerLifePar and
    /// parent suppression risks hiding unrelated native HUD controls.
    /// </summary>
    internal static class NativeBuffDebuffVisibility
    {
        private const string Prefix = "ArcaneUI.NativeEffects.";

        internal enum Channel
        {
            PlayerBuffs = 0,
            PlayerDebuffs = 1,
            TargetBuffs = 2,
            TargetDebuffs = 3
        }

        private sealed class TrackedIcon
        {
            internal Transform Transform;
            internal CanvasGroup Group;
            internal bool Added;
            internal float OriginalAlpha;
            internal bool OriginalInteractable;
            internal bool OriginalBlocksRaycasts;
        }

        private static readonly bool[] Suppressed = new bool[4];
        // Only components we suppressed are touched. Existing CanvasGroups are
        // restored to their exact original state, including input settings.
        private static readonly Dictionary<int, TrackedIcon> Hidden =
            new Dictionary<int, TrackedIcon>();
        private static readonly HashSet<int> Visited = new HashSet<int>();
        private static readonly List<int> Remove = new List<int>();
        private static readonly int[] NativeCounts = new int[4];
        private static readonly int[] HiddenCounts = new int[4];
        private static string _status = "Waiting for native effect icons.";
        private static float _nextDiagnosticTime;
        private static float _nextFailureLog;

        internal static string Diagnostics
        {
            get
            {
                return _status + " | Player buffs " + HiddenCounts[0] + "/" + NativeCounts[0] +
                    ", player debuffs " + HiddenCounts[1] + "/" + NativeCounts[1] +
                    ", target buffs " + HiddenCounts[2] + "/" + NativeCounts[2] +
                    ", target debuffs " + HiddenCounts[3] + "/" + NativeCounts[3] +
                    ". Hide switches P+/P-/T+/T-: " + Bit(0) + Bit(1) + Bit(2) + Bit(3) + ".";
            }
        }

        private static string Bit(int index) { return Suppressed[index] ? "1" : "0"; }

        internal static void Load()
        {
            for (int i = 0; i < Suppressed.Length; ++i)
                Suppressed[i] = PlayerPrefs.GetInt(Prefix + ((Channel)i) + ".Suppress", 0) == 1;
        }

        internal static bool GetSuppress(Channel channel)
        {
            return Suppressed[(int)channel];
        }

        internal static void SetSuppress(Channel channel, bool value)
        {
            int id = (int)channel;
            Suppressed[id] = value;
            PlayerPrefs.SetInt(Prefix + channel + ".Suppress", value ? 1 : 0);
            PlayerPrefs.Save();
            // Refresh is immediate and independent of whether ArcaneUI's effect
            // tracker happens to be enabled or currently displaying any buffs.
            Update();
        }

        /// <summary>Called only while the ArcaneUI gameplay UI is visible.</summary>
        internal static void Update()
        {
            try { Refresh(); }
            catch (Exception e)
            {
                RestoreAllVisuals();
                _status = "Native status binding unavailable; original effects restored.";
                if (Time.unscaledTime >= _nextFailureLog)
                {
                    _nextFailureLog = Time.unscaledTime + 5f;
                    Plugin.LogWarning("[ArcaneUI Native Effects] Native icons restored after binding failure: " + e.Message);
                }
            }
        }

        private static void Refresh()
        {
            Visited.Clear();
            for (int i = 0; i < 4; ++i) { NativeCounts[i] = 0; HiddenCounts[i] = 0; }
            if (!NativeUiLayering.CanShowGameplayUi ||
                (!Suppressed[0] && !Suppressed[1] && !Suppressed[2] && !Suppressed[3]))
            {
                RestoreAllVisuals();
                _status = "Native effect icons visible (hide switches disabled or gameplay UI unavailable).";
                return;
            }

            // GameData.PlayerStats.StatusIcons is the authoritative native list.
            // Native StatusEffectIcon.SetSlot can re-enable its Image every time
            // effects change. A CanvasGroup on that exact icon reliably survives
            // those native updates without changing status effect gameplay.
            Stats player = GameData.PlayerStats;
            if (player != null && player.StatusIcons != null)
            {
                bool hideAllPlayerSlots = Suppressed[0] && Suppressed[1];
                if (hideAllPlayerSlots) TryHideExclusiveTray(player.StatusIcons);
                foreach (StatusEffectIcon icon in player.StatusIcons)
                {
                    if (icon == null) continue;
                    StatusEffect status = null;
                    if (player.StatusEffects != null && icon.SlotIndex >= 0 &&
                        icon.SlotIndex < player.StatusEffects.Length)
                        status = player.StatusEffects[icon.SlotIndex];
                    Spell spell = status != null ? status.Effect : null;
                    if (hideAllPlayerSlots) Hide(icon.transform); // includes blank native slot frames
                    if (spell == null) continue;
                    int channel = IsBeneficial(spell) ? 0 : 1;
                    ++NativeCounts[channel];
                    if (Suppressed[channel])
                    {
                        Hide(icon.transform);
                        ++HiddenCounts[channel];
                    }
                }
            }

            // GameData.NPCEffects owns the native target effect icons. Never
            // disable the ShowTargetEffects component or manipulate its data.
            ShowTargetEffects target = GameData.NPCEffects;
            if (target != null && target.AllIcons != null)
            {
                bool hideAllTargetSlots = Suppressed[2] && Suppressed[3];
                if (hideAllTargetSlots) TryHideExclusiveTray(target.AllIcons);
                foreach (TargetStatusIcon icon in target.AllIcons)
                {
                    if (icon == null) continue;
                    if (hideAllTargetSlots) Hide(icon.transform);
                    if (icon.Effect == null) continue;
                    int channel = IsBeneficial(icon.Effect) ? 2 : 3;
                    ++NativeCounts[channel];
                    if (Suppressed[channel])
                    {
                        Hide(icon.transform);
                        ++HiddenCounts[channel];
                    }
                }
            }

            RestoreUnvisited();
            _status = player != null && player.StatusIcons != null
                ? "Bound to native Stats.StatusIcons and ShowTargetEffects.AllIcons."
                : "Waiting for GameData.PlayerStats.StatusIcons (native effects not yet populated).";
        }

        // Match the classification used by ArcaneUI's own native-backed
        // CooldownAuraTracker so the native and replacement trays agree.
        private static bool IsBeneficial(Spell spell)
        {
            return spell.Type == Spell.SpellType.Beneficial ||
                   spell.TargetHealing > 0 || spell.ShieldingAmt > 0;
        }

        // If and only if the direct parent contains exclusively native status
        // icons, it is safe to hide its backing graphic too. The status-icon
        // list is authoritative; never hide a shared HUD ancestor containing
        // HP, inventory, action bar, or other native functionality.
        private static void TryHideExclusiveTray<T>(IList<T> icons) where T : Component
        {
            Transform parent = null;
            int valid = 0;
            foreach (T icon in icons)
            {
                if (icon == null || icon.transform.parent == null) continue;
                Transform candidate = icon.transform.parent;
                if (parent != null && parent != candidate) return;
                parent = candidate;
                ++valid;
            }
            if (parent == null || valid < 3 || parent.childCount != valid ||
                parent.GetComponent<UnityEngine.Canvas>() != null) return;
            for (int i = 0; i < parent.childCount; ++i)
            {
                if (parent.GetChild(i).GetComponent<T>() == null) return;
            }
            Hide(parent);
        }

        private static void Hide(Transform transform)
        {
            if (transform == null) return;
            int id = transform.GetInstanceID();
            Visited.Add(id);
            TrackedIcon icon;
            if (!Hidden.TryGetValue(id, out icon) || icon.Group == null)
            {
                icon = new TrackedIcon { Transform = transform };
                icon.Group = transform.GetComponent<CanvasGroup>();
                icon.Added = icon.Group == null;
                if (icon.Added)
                    icon.Group = transform.gameObject.AddComponent<CanvasGroup>();
                else
                {
                    icon.OriginalAlpha = icon.Group.alpha;
                    icon.OriginalInteractable = icon.Group.interactable;
                    icon.OriginalBlocksRaycasts = icon.Group.blocksRaycasts;
                }
                Hidden[id] = icon;
            }
            icon.Group.alpha = 0f;
            icon.Group.interactable = false;
            icon.Group.blocksRaycasts = false;
        }

        private static void RestoreUnvisited()
        {
            Remove.Clear();
            foreach (KeyValuePair<int, TrackedIcon> item in Hidden)
            {
                if (Visited.Contains(item.Key)) continue;
                RestoreVisual(item.Value);
                Remove.Add(item.Key);
            }
            foreach (int id in Remove) Hidden.Remove(id);
            Remove.Clear();
        }

        private static void RestoreVisual(TrackedIcon icon)
        {
            if (icon == null || icon.Group == null) return;
            if (icon.Added) UnityEngine.Object.Destroy(icon.Group);
            else
            {
                icon.Group.alpha = icon.OriginalAlpha;
                icon.Group.interactable = icon.OriginalInteractable;
                icon.Group.blocksRaycasts = icon.OriginalBlocksRaycasts;
            }
        }

        private static void RestoreAllVisuals()
        {
            foreach (TrackedIcon icon in Hidden.Values) RestoreVisual(icon);
            Hidden.Clear();
        }

        internal static void Restore(Channel channel)
        {
            // A module's enabled state must never overwrite the user's
            // saved Hide Native effects preference. Legacy module cleanup
            // calls this when player/target frames are turned off.
            RestoreAllVisuals();
        }

        internal static void RestoreAll()
        {
            RestoreAllVisuals();
            for (int i = 0; i < 4; ++i) { NativeCounts[i] = 0; HiddenCounts[i] = 0; }
            _status = "Native effect icons restored.";
        }

        internal static void Inspect()
        {
            // A read-only inspection for game builds with differing HUDs.
            if (Time.unscaledTime < _nextDiagnosticTime) return;
            _nextDiagnosticTime = Time.unscaledTime + .5f;
            Plugin.LogInfo("[ArcaneUI Native Effects] " + Diagnostics);
            Stats stats = GameData.PlayerStats;
            if (stats != null && stats.StatusIcons != null)
            {
                Plugin.LogInfo("[ArcaneUI Native Effects] Stats.StatusIcons count=" + stats.StatusIcons.Count);
                for (int i = 0; i < stats.StatusIcons.Count && i < 5; ++i)
                {
                    StatusEffectIcon icon = stats.StatusIcons[i];
                    if (icon == null) continue;
                    Plugin.LogInfo("[ArcaneUI Native Effects] Player native icon slot=" + icon.SlotIndex +
                        " path=" + Path(icon.transform));
                }
            }
            ShowTargetEffects target = GameData.NPCEffects;
            Plugin.LogInfo("[ArcaneUI Native Effects] Target native icons=" +
                (target != null && target.AllIcons != null ? target.AllIcons.Count.ToString() : "not bound"));
        }

        private static string Path(Transform target)
        {
            var path = new List<string>();
            for (Transform t = target; t != null; t = t.parent) path.Add(t.name);
            path.Reverse();
            return string.Join("/", path.ToArray());
        }
    }
}
