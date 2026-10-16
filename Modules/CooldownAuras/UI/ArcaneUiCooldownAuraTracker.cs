using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using ArcanesGroupFrames.Framework.Canvas;
using ArcanesGroupFrames.Framework.Configuration.UI;
using ArcanesGroupFrames.Modules.CooldownAuras.Configuration;
using ArcanesGroupFrames.Modules.ActionBars.Configuration;
using ArcanesGroupFrames.Modules.ActionBars.Native;
using ArcanesGroupFrames.Modules.UnitFrames.Target;

namespace ArcanesGroupFrames.Modules.CooldownAuras.UI
{
    /// <summary>
    /// Read-only Phase 9 tracker. Native Stats.StatusEffects and Hotkeys.Cooldown
    /// remain authoritative: no gameplay state is added, modified or inferred.
    /// Refresh is capped at ten times per second; icons are pooled, not recreated.
    /// </summary>
    internal sealed class ArcaneUiCooldownAuraTracker
    {
        private const int PoolSize = 12;
        private readonly ArcaneCanvasManager _canvas;
        private RectTransform _layer;
        private readonly TrackerRow[] _rows = new TrackerRow[5];
        private readonly List<TrackedIcon> _playerBuffs = new List<TrackedIcon>(24);
        private readonly List<TrackedIcon> _playerDebuffs = new List<TrackedIcon>(24);
        private readonly List<TrackedIcon> _targetBuffs = new List<TrackedIcon>(24);
        private readonly List<TrackedIcon> _targetDebuffs = new List<TrackedIcon>(24);
        private readonly List<TrackedIcon> _cooldowns = new List<TrackedIcon>(48);
        private readonly HashSet<string> _readyActions = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> _seenActions = new HashSet<string>(StringComparer.Ordinal);
        private readonly Dictionary<string, float> _observedMaximum =
            new Dictionary<string, float>(StringComparer.Ordinal);
        internal static string Diagnostics { get; private set; } = "Not started.";
        private float _nextRead;
        private Texture2D _fillTexture;
        private Sprite _fillSprite;
        private Sprite FillSprite()
        {
            if (_fillSprite != null) return _fillSprite;
            _fillTexture = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            _fillTexture.name = "ArcaneUI_TrackerCooldownFill";
            var pixels = new Color[16];
            for (int i=0;i<pixels.Length;i++) pixels[i] = Color.white;
            _fillTexture.SetPixels(pixels);
            _fillTexture.Apply();
            _fillSprite = Sprite.Create(_fillTexture, new Rect(0,0,4,4),
                new Vector2(.5f,.5f), 100f);
            return _fillSprite;
        }
        private Stats _lastOwner;
        private sealed class TrackedIcon
        {
            internal string Key, Name;
            internal Sprite Sprite;
            internal float Remaining, Progress;
            internal int Stacks;
            internal bool IsDebuff;
        }
        private sealed class IconView
        {
            internal RectTransform Root;
            internal Image Art, Shade, Backing;
            internal Outline Border;
            internal Text Fallback, Timer, Stack;
            internal float SampleAt, SampleRemaining, SampleFraction;
        }
        private sealed class TrackerRow
        {
            internal RectTransform Root;
            internal Text Label;
            internal IconView[] Icons;
        }

        internal ArcaneUiCooldownAuraTracker(ArcaneCanvasManager canvas) { _canvas = canvas; }
        private void Initialize()
        {
            if (_layer != null || !_canvas.IsReady) return;
            _layer = _canvas.CreateLayer("ArcaneUI_CooldownAuraTracker");
            var sorting = _layer.gameObject.AddComponent<UnityEngine.Canvas>();
            sorting.overrideSorting = true;
            sorting.sortingOrder = -99;
            var raycaster = _layer.gameObject.AddComponent<GraphicRaycaster>();
            raycaster.enabled = false;
            string[] titles = { "PLAYER BUFFS", "PLAYER DEBUFFS", "TARGET BUFFS", "TARGET DEBUFFS", "COOLDOWNS" };
            for (int row = 0; row < _rows.Length; row++)
            {
                var holder = ArcaneUiControls.Rect(_layer, "Tracker_" + row, 0, 0, 1, 1);
                holder.pivot = new Vector2(0f, 1f);
                holder.anchorMin = holder.anchorMax = new Vector2(0f, 1f);
                var label = ArcaneUiControls.Label(holder, "Label", titles[row], 0, 0, 240, 16,
                    11, new Color(.88f,.88f,.92f), TextAnchor.MiddleLeft, FontStyle.Bold);
                label.raycastTarget = false;
                var items = new IconView[PoolSize];
                for (int i = 0; i < items.Length; ++i)
                {
                    var root = ArcaneUiControls.Panel(holder, "Effect_" + i, 0, 0, 32, 32,
                        new Color(.045f,.055f,.07f,.92f), false);
                    var backing = root.GetComponent<Image>();
                    var border = root.gameObject.AddComponent<Outline>();
                    border.effectDistance = new Vector2(1f, -1f);
                    border.useGraphicAlpha = true;
                    var icon = ArcaneUiControls.Panel(root, "Icon", 1, 1, 30, 30, Color.white, false).GetComponent<Image>();
                    var shade = ArcaneUiControls.Panel(root, "CooldownShade", 1, 1, 30, 30,
                        new Color(0f,0f,0f,.42f), false).GetComponent<Image>();
                    shade.sprite = FillSprite();
                    shade.type = Image.Type.Filled;
                    shade.fillMethod = Image.FillMethod.Radial360;
                    shade.fillOrigin = (int)Image.Origin360.Top;
                    shade.fillClockwise = true;
                    var initial = ArcaneUiControls.Label(root,"Fallback","",1,0,30,29,16,Color.white,
                        TextAnchor.MiddleCenter,FontStyle.Bold);
                    var timer = ArcaneUiControls.Label(root,"Duration","",0,19,31,13,11,Color.white,
                        TextAnchor.MiddleRight,FontStyle.Bold);
                    var stack = ArcaneUiControls.Label(root,"Stacks","",0,1,29,13,11,Color.white,
                        TextAnchor.UpperRight,FontStyle.Bold);
                    items[i] = new IconView { Root=root, Art=icon, Shade=shade, Backing=backing, Border=border, Fallback=initial, Timer=timer, Stack=stack };
                    root.gameObject.SetActive(false);
                }
                _rows[row] = new TrackerRow { Root=holder, Label=label, Icons=items };
            }
            _layer.gameObject.SetActive(false);
        }

        // Exposes only ArcaneUI-owned read-only tracker frames to the Layout Editor.
        // These RectTransforms exist even if there are no active buffs/debuffs.
        internal RectTransform GroupRect(int index)
        {
            if (index < 0 || index >= _rows.Length) return null;
            if (_layer == null && _canvas.IsReady) Initialize();
            return _rows[index] != null ? _rows[index].Root : null;
        }

        internal void Update()
        {
            if (_layer == null && _canvas.IsReady) Initialize();
            if (_layer == null) return;
            bool show = CooldownAuraSettings.Enabled && !GameData.InCharSelect &&
                NativeUiLayering.CanShowGameplayUi && GameData.PlayerStats != null;
            if (_layer.gameObject.activeSelf != show) _layer.gameObject.SetActive(show);
            if (!show) { _nextRead = 0f; Diagnostics = "Hidden: tracker disabled or gameplay unavailable."; return; }
            if (!object.ReferenceEquals(_lastOwner, GameData.PlayerStats))
            { _observedMaximum.Clear(); _lastOwner = GameData.PlayerStats; _nextRead = 0f; }
            AnimateCooldownSwipe();
            if (Time.unscaledTime < _nextRead) return;
            _nextRead = Time.unscaledTime + .10f;
            Stats player = GameData.PlayerStats;
            Character target = GameData.PlayerControl != null ? GameData.PlayerControl.CurrentTarget : null;
            Stats targetStats = target != null ? target.MyStats : null;
            ReadAuras(player, _playerBuffs, _playerDebuffs);
            ReadAuras(targetStats, _targetBuffs, _targetDebuffs);
            ReadCooldowns();
            Apply(0, _playerBuffs, CooldownAuraSettings.PlayerBuffs);
            Apply(1, _playerDebuffs, CooldownAuraSettings.PlayerDebuffs);
            Apply(2, _targetBuffs, CooldownAuraSettings.TargetBuffs && targetStats != null);
            Apply(3, _targetDebuffs, CooldownAuraSettings.TargetDebuffs && targetStats != null);
            Apply(4, _cooldowns, CooldownAuraSettings.Cooldowns);
            Diagnostics = "Native effects — player buffs: " + _playerBuffs.Count +
                ", player debuffs: " + _playerDebuffs.Count +
                ", target buffs: " + _targetBuffs.Count +
                ", target debuffs: " + _targetDebuffs.Count +
                ". Active assigned-action cooldowns: " + _cooldowns.Count + ".";
        }

        private static void ReadAuras(Stats stats, List<TrackedIcon> buffs, List<TrackedIcon> debuffs)
        {
            buffs.Clear(); debuffs.Clear();
            if (stats == null || stats.StatusEffects == null || stats.CurrentHP <= 0f) return;
            foreach (StatusEffect effect in stats.StatusEffects)
            {
                if (effect == null || effect.Effect == null || effect.Duration <= 0f) continue;
                Spell spell = effect.Effect;
                bool helpful = spell.Type == Spell.SpellType.Beneficial ||
                    spell.TargetHealing > 0 || spell.ShieldingAmt > 0;
                var value = new TrackedIcon {
                    Key = spell.Id, Name = spell.SpellName,
                    Sprite = TargetNativeReadAdapter.ReadSpellIcon(spell),
                    Remaining = effect.Duration, Stacks = TargetNativeReadAdapter.ReadStacks(effect),
                    IsDebuff = !helpful
                };
                (helpful ? buffs : debuffs).Add(value);
            }
            // Sorting is independently configured for each visual group at Apply().
        }

        private void ReadCooldowns()
        {
            _cooldowns.Clear(); _readyActions.Clear(); _seenActions.Clear();
            // Action Bars already refresh the live primary snapshots. Do not
            // scan the Unity hierarchy from this HUD; never guess recast time.
            if (NativeActionBarDiscovery.HasValidLiveSnapshot)
            {
                for (int slot=1; slot<=10; ++slot)
                {
                    NativeActionBarDiscovery.NativeSlotSnapshot native;
                    if (!NativeActionBarDiscovery.TryGetActiveSlot(slot, out native) ||
                        native == null || !native.HasAssignment) continue;
                    AddCooldown(native.AbilityKind + ":" + native.AbilityName,
                        native.AbilityName, native.AbilityIcon,
                        Mathf.Max(0f, native.RawCooldown / 60f));
                }
            }
            for (int bar=1; bar<ActionBarPlanSettings.Count; ++bar)
                for (int slot=1; slot<=ActionBarPlanSettings.SlotCount(bar); ++slot)
                {
                    string key, name; Sprite icon; float remaining;
                    if (NativeExtraHotkeySlots.TryReadTrackerCooldown(bar, slot,
                        out key, out name, out icon, out remaining))
                        AddCooldown(key, name, icon, remaining);
                }
            // Any live instance reporting ready wins over stale duplicates.
            _cooldowns.RemoveAll(a => _readyActions.Contains(a.Key));
            // Per-frame sorting is applied after the authoritative native snapshot is collected.
        }
        private void AddCooldown(string key, string name, Sprite icon, float remaining)
        {
            if (string.IsNullOrEmpty(key)) return;
            if (remaining <= .001f)
            {
                _readyActions.Add(key);
                _observedMaximum.Remove(key);
                return;
            }
            if (remaining < CooldownAuraSettings.MinimumCooldown || !_seenActions.Add(key)) return;
            float total;
            if (!_observedMaximum.TryGetValue(key, out total) || remaining > total)
            {
                total = remaining;
                _observedMaximum[key] = total;
            }
            _cooldowns.Add(new TrackedIcon { Key=key, Name=name, Sprite=icon,
                Remaining=remaining, Progress=total > 0f ? Mathf.Clamp01(remaining/total) : 0f });
        }

        private static void SortForFrame(List<TrackedIcon> values, int sort)
        {
            // Avoid mutating gameplay/native collections: these are ArcaneUI snapshots.
            if (sort == 0) values.Sort((a,b) => a.Remaining.CompareTo(b.Remaining));
            else if (sort == 1) values.Sort((a,b) => b.Remaining.CompareTo(a.Remaining));
            else if (sort == 2) values.Sort((a,b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
        }
        private void Apply(int rowId, List<TrackedIcon> source, bool enabled)
        {
            TrackerRow row = _rows[rowId];
            CooldownAuraSettings.AuraFrame f = CooldownAuraSettings.Frame(rowId);
            int size = f.IconSize;
            int step = size + f.Spacing;
            int max = Mathf.Min(source.Count, f.MaxIcons);
            // Keep a useful editor outline for a presently empty category.
            int layoutCount = Mathf.Max(1, max);
            int perLine = Mathf.Clamp(f.Columns, 1, f.MaxIcons);
            int lines = Mathf.CeilToInt(layoutCount / (float)perLine);
            bool vertical = f.Growth == 1 || f.Growth == 3;
            int primary = Mathf.Min(layoutCount, perLine);
            int horizontalSlots = vertical ? lines : primary;
            int verticalSlots = vertical ? primary : lines;
            float labelHeight = f.ShowTitle ? 18f : 0f;
            row.Root.anchorMin = row.Root.anchorMax = new Vector2(0f, 1f);
            row.Root.pivot = new Vector2(0f, 1f);
            row.Root.anchoredPosition = f.Position;
            row.Root.sizeDelta = new Vector2(Mathf.Max(80f, horizontalSlots * step - f.Spacing),
                labelHeight + verticalSlots * step - f.Spacing);
            row.Label.gameObject.SetActive(f.ShowTitle);
            row.Label.color = CooldownAuraSettings.Accent(rowId);
            row.Label.rectTransform.sizeDelta = new Vector2(row.Root.sizeDelta.x, 16);
            row.Root.gameObject.SetActive(enabled && source.Count > 0);
            if (!row.Root.gameObject.activeSelf) return;
            SortForFrame(source, f.Sort);
            Color accent = CooldownAuraSettings.Accent(rowId);
            Color background = f.Style == 1 ? new Color(.03f, .03f, .04f, .12f * f.Opacity) :
                f.Style == 2 ? new Color(.055f, .08f, .14f, .95f * f.Opacity) :
                f.Style == 3 ? new Color(.018f, .023f, .032f, .97f * f.Opacity) :
                new Color(.045f, .055f, .07f, .92f * f.Opacity);
            for (int i=0; i<row.Icons.Length; ++i)
            {
                IconView v = row.Icons[i];
                bool visible = i < max;
                v.Root.gameObject.SetActive(visible);
                if (!visible) continue;
                TrackedIcon data = source[i];
                int along = i % perLine;
                int across = i / perLine;
                int col = vertical ? across : along;
                int line = vertical ? along : across;
                if (f.Growth == 2) col = horizontalSlots - 1 - col;
                if (f.Growth == 3) line = verticalSlots - 1 - line;
                v.Root.sizeDelta = new Vector2(size, size);
                v.Root.anchoredPosition = new Vector2(col * step, -(labelHeight + line * step));
                v.Backing.color = background;
                v.Border.enabled = f.Style != 1;
                v.Border.effectColor = new Color(accent.r, accent.g, accent.b,
                    (f.Style == 2 ? .90f : .45f) * f.Opacity);
                v.Art.rectTransform.sizeDelta = new Vector2(size-2, size-2);
                v.Shade.rectTransform.sizeDelta = new Vector2(size-2, size-2);
                v.Art.sprite = data.Sprite;
                v.Art.color = new Color(1f, 1f, 1f, f.Opacity);
                v.Art.gameObject.SetActive(data.Sprite != null);
                v.Fallback.gameObject.SetActive(data.Sprite == null);
                v.Fallback.text = string.IsNullOrEmpty(data.Name) ? "?" : data.Name.Substring(0, 1);
                v.Fallback.color = accent;
                v.Fallback.rectTransform.sizeDelta = new Vector2(size-2, size-2);
                v.Timer.gameObject.SetActive(f.ShowDuration);
                v.Timer.text = data.Remaining >= 60f ? Mathf.CeilToInt(data.Remaining/60f) + "m" :
                    data.Remaining >= 10f ? Mathf.CeilToInt(data.Remaining).ToString() :
                    data.Remaining.ToString("0.0");
                v.Timer.color = Color.white;
                v.Timer.rectTransform.anchoredPosition = new Vector2(0f, -(size-13));
                v.Timer.rectTransform.sizeDelta = new Vector2(size-2, 13);
                v.Stack.gameObject.SetActive(f.ShowStacks && data.Stacks > 1);
                v.Stack.text = data.Stacks.ToString();
                v.Stack.color = accent;
                v.Stack.rectTransform.sizeDelta = new Vector2(size-3, 13);
                v.Shade.gameObject.SetActive(rowId == 4);
                v.Shade.fillAmount = data.Progress;
                v.SampleAt = Time.unscaledTime;
                v.SampleRemaining = data.Remaining;
                v.SampleFraction = data.Progress;
            }
        }
        // Native sampling is deliberately bounded to 10 Hz, but the purely
        // visual radial fill interpolates every frame so it does not stutter.
        // A new native sample (including an instant reset) overrides it.
        private void AnimateCooldownSwipe()
        {
            TrackerRow row = _rows[4];
            if (row == null || !row.Root.gameObject.activeSelf) return;
            float now = Time.unscaledTime;
            foreach (IconView icon in row.Icons)
            {
                if (!icon.Root.gameObject.activeSelf || icon.SampleRemaining <= 0f) continue;
                icon.Shade.fillAmount = Mathf.Clamp01(icon.SampleFraction *
                    Mathf.Max(0f, 1f - (now - icon.SampleAt) / icon.SampleRemaining));
            }
        }
        internal void Shutdown()
        {
            if (_layer != null) UnityEngine.Object.Destroy(_layer.gameObject);
            _layer = null;
            _lastOwner = null;
            _observedMaximum.Clear();
            if (_fillSprite != null) UnityEngine.Object.Destroy(_fillSprite);
            if (_fillTexture != null) UnityEngine.Object.Destroy(_fillTexture);
            _fillSprite = null; _fillTexture = null;
        }
    }
}
