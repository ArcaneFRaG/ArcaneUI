using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using ArcanesGroupFrames.Framework.Canvas;
using ArcanesGroupFrames.Framework.Theming;
using ArcanesGroupFrames.Modules.Nameplates.Configuration;
using ArcanesGroupFrames.Modules.Nameplates.Native;
using ArcanesGroupFrames.Modules.UnitFrames.Target;

namespace ArcanesGroupFrames.Modules.Nameplates.UI
{
    /// <summary>
    /// Phase 8.1: screen-projected world nameplates for nearby NPCs and SimPlayers.
    /// Discovery is throttled; only a small, bounded pool of visual objects is
    /// created. This layer never changes native targeting/casting/health state.
    /// </summary>
    internal sealed class ArcaneUiOverheadNameplates
    {
        private const int MaximumVisible = 24;
        private const float DiscoveryInterval = 1.25f;
        private readonly ArcaneCanvasManager _canvas;
        private readonly List<Character> _actors = new List<Character>();
        private readonly List<Character> _filtered = new List<Character>();
        private readonly HashSet<Character> _friendlies = new HashSet<Character>();
        private readonly List<Plate> _pool = new List<Plate>();
        private RectTransform _layer;
        private float _nextDiscovery;
        private Font _font;
        private Camera _camera;
        private static string _diagnostics = "Waiting for an in-game nameplate update.";
        internal static string Diagnostics { get { return _diagnostics; } }
        private int _lastDiscovered;
        private int _lastEligible;
        private int _lastFiltered;
        private bool _wasShowingNpcs = true;
        private bool _wasShowingSims = true;
        private bool _wasShowingFriendly = true;
        private float _wasMaxDistance = -1f;
        private sealed class Plate
        {
            internal RectTransform Rect;
            internal Text Name;
            internal Text NpcTag;
            internal Image Background;
            internal RectTransform Fill;
            internal Image FillImage;
            internal Outline NameOutline;
            internal Outline TargetOutline;
            internal Text HealthPercent;
        }
        internal ArcaneUiOverheadNameplates(ArcaneCanvasManager canvas) { _canvas = canvas; }

        internal void Update()
        {
            bool visible = NameplateSettings.Enabled && _canvas != null && _canvas.IsReady &&
                NativeUiLayering.CanShowGameplayUi && !GameData.InCharSelect &&
                GameData.PlayerStats != null && GameData.PlayerControl != null;
            if (!visible)
            {
                _diagnostics = !NameplateSettings.Enabled ? "Disabled in Nameplates > General." :
                    "ArcaneUI gameplay rendering is paused (loading screen, character select, or missing player).";
                HideAll();
                NativeOverheadNameplateBridge.Restore();
                return;
            }
            if (!NameplateSettings.HideNative && NativeOverheadNameplateBridge.HasHiddenVisuals)
                NativeOverheadNameplateBridge.Restore();
            if (_layer == null) CreateLayer();
            if (_layer == null)
            {
                _diagnostics = "Custom nameplate canvas could not be created.";
                return;
            }
            if (!_layer.gameObject.activeSelf) _layer.gameObject.SetActive(true);
            if (_camera == null || !_camera.isActiveAndEnabled) _camera = FindGameCamera();
            if (_camera == null)
            {
                _diagnostics = "No active game camera found. Check MainCamera and render target.";
                HideAll(); NativeOverheadNameplateBridge.Restore(); return;
            }

            if (_wasShowingNpcs != NameplateSettings.ShowNpcs ||
                _wasShowingSims != NameplateSettings.ShowSims ||
                _wasShowingFriendly != NameplateSettings.ShowFriendly ||
                !Mathf.Approximately(_wasMaxDistance, NameplateSettings.MaxDistance))
                _nextDiscovery = 0f;
            if (Time.unscaledTime >= _nextDiscovery)
            {
                _nextDiscovery = Time.unscaledTime + DiscoveryInterval;
                Discover();
            }
            Character target = GameData.PlayerControl.CurrentTarget;
            int shown = 0;
            for (int i = 0; i < _pool.Count; i++)
            {
                Plate plate = _pool[i];
                Character actor = i < _actors.Count ? _actors[i] : null;
                if (actor == null || !actor.gameObject.activeInHierarchy || actor.MyStats == null)
                {
                    plate.Rect.gameObject.SetActive(false);
                    continue;
                }
                Vector3 anchor = actor.transform.position + Vector3.up * (2.2f + NameplateSettings.HeightOffset);
                Vector3 screen = _camera.WorldToScreenPoint(anchor);
                float sqr = (actor.transform.position - GameData.PlayerControl.transform.position).sqrMagnitude;
                if (screen.z <= 0.1f || sqr > NameplateSettings.MaxDistance * NameplateSettings.MaxDistance ||
                    screen.x < -60f || screen.x > Screen.width + 60f ||
                    screen.y < -40f || screen.y > Screen.height + 50f)
                {
                    plate.Rect.gameObject.SetActive(false);
                    continue;
                }
                Vector2 local;
                if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_layer, screen, null, out local))
                {
                    plate.Rect.gameObject.SetActive(false);
                    continue;
                }
                plate.Rect.gameObject.SetActive(true);
                shown++;
                plate.Rect.anchoredPosition = local;
                RefreshPlate(plate, actor, actor == target);
                NativeOverheadNameplateBridge.Apply(actor);
            }
            if (NameplateSettings.HideNative)
                foreach (Character excluded in _filtered)
                    if (excluded != null && excluded.gameObject.activeInHierarchy)
                        NativeOverheadNameplateBridge.Apply(excluded);
            _diagnostics = "Found " + _lastDiscovered + " Characters; " + _lastEligible +
                " NPC/SimPlayer candidates, " + _lastFiltered + " filtered, " +
                _actors.Count + " in range, " + shown +
                " visible on screen. Camera: " + _camera.name +
                "; custom canvas order: 100.";
        }

        private static Camera FindGameCamera()
        {
            Camera camera = Camera.main;
            if (camera != null && camera.isActiveAndEnabled) return camera;
            // Some Erenshor scenes lack a MainCamera tag. This fallback runs
            // only when the cached camera has become unavailable.
            foreach (Camera candidate in Camera.allCameras)
                if (candidate != null && candidate.isActiveAndEnabled &&
                    candidate.targetTexture == null && candidate.cameraType == CameraType.Game)
                    return candidate;
            return null;
        }

        private void Discover()
        {
            _actors.Clear();
            _filtered.Clear();
            _friendlies.Clear();
            _wasShowingNpcs = NameplateSettings.ShowNpcs;
            _wasShowingSims = NameplateSettings.ShowSims;
            _wasShowingFriendly = NameplateSettings.ShowFriendly;
            _wasMaxDistance = NameplateSettings.MaxDistance;
            _lastFiltered = 0;
            _lastDiscovered = 0;
            _lastEligible = 0;
            // Preserve native suppression for actors still in range. Restoring on
            // every discovery tick briefly exposes the game's own labels and
            // makes them appear alongside ArcaneUI nameplates.
            if (GameData.PlayerControl == null) return;
            float maxSq = NameplateSettings.MaxDistance * NameplateSettings.MaxDistance;
            Vector3 playerPos = GameData.PlayerControl.transform.position;
            Character[] found = UnityEngine.Object.FindObjectsOfType<Character>();
            _lastDiscovered = found.Length;
            foreach (Character actor in found)
            {
                if (actor == null || actor.MyStats == null || !actor.gameObject.activeInHierarchy ||
                    actor.MyStats == GameData.PlayerStats || actor.MyStats.CurrentHP <= 0) continue;
                // SimPlayers can exist as NPC-backed Character objects even
                // when Character.isNPC differs from a normal enemy.
                bool sim = actor.MyNPC != null && actor.MyNPC.SimPlayer;
                if (!actor.isNPC && !sim) continue;
                _lastEligible++;
                float sqr = (actor.transform.position - playerPos).sqrMagnitude;
                if (sqr > maxSq) continue;
                // SimPlayers remain independent of the friendly-NPC toggle.
                bool isFriendlyNpc = !sim &&
                    TargetHealthColourResolver.Classify(actor) == TargetHealthColourResolver.Kind.FriendlyNpc;
                if (isFriendlyNpc) _friendlies.Add(actor);
                if ((sim && !NameplateSettings.ShowSims) ||
                    (!sim && (!NameplateSettings.ShowNpcs ||
                        (isFriendlyNpc && !NameplateSettings.ShowFriendly))))
                {
                    _lastFiltered++;
                    // Disabling custom SimPlayer nameplates must NOT hide the
                    // native SimPlayer overhead name or guild. Only filtered
                    // NPCs participate in native-overhead suppression.
                    if (!sim && NameplateSettings.HideNative && _filtered.Count < 64)
                        _filtered.Add(actor);
                    continue;
                }
                int pos = 0;
                // Keep closest 24 instead of creating UI for all characters in the scene.
                while (pos < _actors.Count &&
                    (_actors[pos].transform.position - playerPos).sqrMagnitude <= sqr) pos++;
                if (pos >= MaximumVisible) continue;
                _actors.Insert(pos, actor);
                if (_actors.Count > MaximumVisible) _actors.RemoveAt(MaximumVisible);
            }
            NativeOverheadNameplateBridge.RestoreActorsNotIn(_actors, _filtered);
            while (_pool.Count < _actors.Count) _pool.Add(CreatePlate(_pool.Count));
        }

        private void CreateLayer()
        {
            _layer = _canvas.CreateLayer("ArcaneUI_OverheadNameplates");
            if (_layer == null) return;
            var canvas = _layer.gameObject.AddComponent<UnityEngine.Canvas>();
            canvas.overrideSorting = true;
            // The ordinary ArcaneUI HUD is -100. Native overhead labels can
            // be on a separate native canvas (often order 0). Draw above those
            // labels while keeping the settings/editor/tooltip canvases
            // (32760+) above this gameplay-only nameplate layer.
            // World nameplates should be below Erenshor's foreground inventory,
            // merchant and character windows, not above them (native canvas 0).
            // Native SimPlayer name/guild restoration is handled separately.
            canvas.sortingOrder = -1;
            var group = _layer.gameObject.AddComponent<CanvasGroup>();
            group.interactable = false;
            group.blocksRaycasts = false;
            _layer.gameObject.AddComponent<CanvasRenderer>();
            _font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        }
        private Plate CreatePlate(int index)
        {
            var item = new Plate();
            GameObject root = new GameObject("ArcaneUI_Nameplate_" + index, typeof(RectTransform));
            root.transform.SetParent(_layer, false);
            item.Rect = root.GetComponent<RectTransform>();
            item.Rect.anchorMin = item.Rect.anchorMax = new Vector2(.5f, .5f);
            item.Rect.pivot = new Vector2(.5f, 0f);
            item.Rect.sizeDelta = new Vector2(NameplateSettings.Width, 34f);

            GameObject nameObject = new GameObject("Name", typeof(RectTransform), typeof(Text));
            nameObject.transform.SetParent(root.transform, false);
            RectTransform nameRect = nameObject.GetComponent<RectTransform>();
            nameRect.anchorMin = new Vector2(0f, 1f);
            nameRect.anchorMax = new Vector2(1f, 1f);
            nameRect.pivot = new Vector2(.5f, 1f);
            nameRect.sizeDelta = new Vector2(0f, 18f);
            nameRect.anchoredPosition = Vector2.zero;
            item.Name = nameObject.GetComponent<Text>();
            item.Name.font = _font;
            item.Name.fontSize = 12;
            item.Name.alignment = TextAnchor.MiddleCenter;
            item.Name.raycastTarget = false;
            item.Name.supportRichText = false;
            item.Name.horizontalOverflow = HorizontalWrapMode.Overflow;
            item.NameOutline = nameObject.AddComponent<Outline>();
            item.NameOutline.effectColor = new Color(0f, 0f, 0f, 1f);
            item.NameOutline.effectDistance = new Vector2(1f, -1f);
            item.NameOutline.useGraphicAlpha = false;

            GameObject tagObject = new GameObject("NpcRoleTag", typeof(RectTransform), typeof(Text));
            tagObject.transform.SetParent(root.transform, false);
            RectTransform tagRect = tagObject.GetComponent<RectTransform>();
            tagRect.anchorMin = new Vector2(0f, 0f);
            tagRect.anchorMax = new Vector2(1f, 0f);
            tagRect.pivot = new Vector2(.5f, 0f);
            tagRect.sizeDelta = new Vector2(0f, 16f);
            tagRect.anchoredPosition = new Vector2(0f, NameplateSettings.Height + 2f);
            item.NpcTag = tagObject.GetComponent<Text>();
            item.NpcTag.font = _font;
            item.NpcTag.fontSize = NameplateSettings.NpcTagFontSize;
            item.NpcTag.alignment = TextAnchor.MiddleCenter;
            item.NpcTag.raycastTarget = false;
            item.NpcTag.supportRichText = false;
            item.NpcTag.horizontalOverflow = HorizontalWrapMode.Overflow;
            var tagOutline = tagObject.AddComponent<Outline>();
            tagOutline.effectColor = Color.black;
            tagOutline.effectDistance = new Vector2(1f, -1f);
            tagOutline.useGraphicAlpha = false;
            tagObject.SetActive(false);

            GameObject bg = new GameObject("HealthBackground", typeof(RectTransform), typeof(Image));
            bg.transform.SetParent(root.transform, false);
            item.Background = bg.GetComponent<Image>();
            item.Background.color = new Color(.065f, .075f, .09f, .88f);
            item.Background.raycastTarget = false;
            item.TargetOutline = bg.AddComponent<Outline>();
            item.TargetOutline.useGraphicAlpha = false;
            item.TargetOutline.enabled = false;
            RectTransform bgRect = bg.GetComponent<RectTransform>();
            bgRect.anchorMin = new Vector2(0f, 0f); bgRect.anchorMax = new Vector2(1f, 0f);
            bgRect.pivot = new Vector2(.5f, 0f);
            bgRect.sizeDelta = new Vector2(0f, NameplateSettings.Height);
            bgRect.anchoredPosition = Vector2.zero;

            GameObject fill = new GameObject("HealthFill", typeof(RectTransform), typeof(Image));
            fill.transform.SetParent(bg.transform, false);
            item.Fill = fill.GetComponent<RectTransform>();
            item.Fill.anchorMin = new Vector2(0f, 0f); item.Fill.anchorMax = new Vector2(1f, 1f);
            item.Fill.pivot = new Vector2(0f, .5f);
            item.Fill.offsetMin = Vector2.zero; item.Fill.offsetMax = Vector2.zero;
            item.FillImage = fill.GetComponent<Image>();
            item.FillImage.raycastTarget = false;

            GameObject healthText = new GameObject("HealthPercent", typeof(RectTransform), typeof(Text));
            healthText.transform.SetParent(bg.transform, false);
            RectTransform healthTextRect = healthText.GetComponent<RectTransform>();
            healthTextRect.anchorMin = Vector2.zero;
            healthTextRect.anchorMax = Vector2.one;
            healthTextRect.offsetMin = Vector2.zero;
            healthTextRect.offsetMax = Vector2.zero;
            item.HealthPercent = healthText.GetComponent<Text>();
            item.HealthPercent.font = _font;
            item.HealthPercent.alignment = TextAnchor.MiddleCenter;
            item.HealthPercent.color = Color.white;
            item.HealthPercent.raycastTarget = false;
            var percentOutline = healthText.AddComponent<Outline>();
            percentOutline.effectColor = Color.black;
            percentOutline.effectDistance = new Vector2(1f, -1f);
            percentOutline.useGraphicAlpha = false;
            root.SetActive(false);
            return item;
        }
        internal static Color ParseColour(string hex, Color fallback)
        {
            Color colour;
            return ColorUtility.TryParseHtmlString(hex, out colour) ? colour : fallback;
        }
        internal static void SetHealthFill(RectTransform rect, float fraction, int direction)
        {
            fraction = Mathf.Clamp01(fraction);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            switch (direction)
            {
                case 1: // Right to left
                    rect.anchorMin = new Vector2(1f - fraction, 0f);
                    rect.anchorMax = Vector2.one;
                    break;
                case 2: // Bottom to top
                    rect.anchorMin = Vector2.zero;
                    rect.anchorMax = new Vector2(1f, fraction);
                    break;
                case 3: // Top to bottom
                    rect.anchorMin = new Vector2(0f, 1f - fraction);
                    rect.anchorMax = Vector2.one;
                    break;
                default: // Left to right
                    rect.anchorMin = Vector2.zero;
                    rect.anchorMax = new Vector2(fraction, 1f);
                    break;
            }
        }
        private void RefreshPlate(Plate plate, Character actor, bool targeted)
        {
            Stats stats = actor.MyStats;
            string name = !string.IsNullOrEmpty(stats.MyName) ? stats.MyName :
                (actor.MyNPC != null && !string.IsNullOrEmpty(actor.MyNPC.NPCName) ? actor.MyNPC.NPCName : actor.name);
            int nameLimit = NameplateSettings.NameCharacterLimit;
            if (name.Length > nameLimit) name = name.Substring(0, nameLimit - 1) + "…";
            bool sim = actor.MyNPC != null && actor.MyNPC.SimPlayer;
            bool friendly = !sim && _friendlies.Contains(actor);
            Color hostileColour = ParseColour(NameplateSettings.HostileColour, new Color(.71f, .28f, .28f, 1f));
            Color friendlyColour = ParseColour(NameplateSettings.FriendlyColour, TargetHealthColourResolver.FriendlyGreen);
            Color fill = sim
                ? (NameplateSettings.SimClassColours ? RaidClassColors.GetColorForActor(stats, name) :
                    ParseColour(NameplateSettings.SimColour, new Color(.27f, .55f, .77f, 1f)))
                : friendly ? friendlyColour : hostileColour;
            plate.Name.text = name;
            plate.Name.gameObject.SetActive(NameplateSettings.ShowNames);
            // Roles are derived from explicit NPC service metadata, never from
            // the NPC's display name. SimPlayers deliberately have no NPC tag.
            string npcRole = !sim && NameplateSettings.ShowNpcTags
                ? NpcRoleTagResolver.Resolve(actor) : "";
            bool showTag = !string.IsNullOrEmpty(npcRole);
            plate.NpcTag.gameObject.SetActive(showTag);
            if (showTag)
            {
                plate.NpcTag.text = "<" + npcRole + ">";
                plate.NpcTag.fontSize = NameplateSettings.NpcTagFontSize;
                plate.NpcTag.color = ParseColour(NameplateSettings.NpcTagColour, new Color(.84f, .76f, .59f));
                plate.NpcTag.rectTransform.anchoredPosition = new Vector2(0f, NameplateSettings.Height + 2f);
                plate.NpcTag.rectTransform.sizeDelta = new Vector2(0f, Mathf.Max(16f, NameplateSettings.NpcTagFontSize + 3f));
            }
            plate.Name.fontSize = NameplateSettings.NameFontSize;
            plate.Name.color = targeted ? Color.white :
                (sim && NameplateSettings.SimClassColours ? fill :
                ParseColour(NameplateSettings.NameColour, new Color(.94f, .90f, .82f, 1f)));
            plate.FillImage.color = fill;
            float hp = stats.CurrentMaxHP > 0 ? Mathf.Clamp01((float)stats.CurrentHP / stats.CurrentMaxHP) : 0f;
            SetHealthFill(plate.Fill, hp, NameplateSettings.HealthDirection);
            plate.HealthPercent.gameObject.SetActive(NameplateSettings.ShowHealthPercent);
            if (NameplateSettings.ShowHealthPercent)
            {
                plate.HealthPercent.text = Mathf.RoundToInt(hp * 100f) + "%";
                plate.HealthPercent.fontSize = NameplateSettings.PercentFontSize;
            }
            plate.Rect.localScale = Vector3.one * (targeted ? NameplateSettings.TargetScale / 100f : 1f);
            plate.Rect.sizeDelta = new Vector2(NameplateSettings.Width, NameplateSettings.Height +
                (NameplateSettings.ShowNames ? 20f : 0f) +
                (showTag ? Mathf.Max(16f, NameplateSettings.NpcTagFontSize + 3f) + 2f : 0f));
            RectTransform bg = plate.Background.rectTransform;
            bg.sizeDelta = new Vector2(0f, NameplateSettings.Height);
            float opacity = NameplateSettings.BackgroundOpacity / 100f;
            plate.Background.color = new Color(.065f, .075f, .09f, opacity);
            bool outline = targeted && NameplateSettings.ShowTargetOutline;
            plate.TargetOutline.enabled = outline;
            if (outline)
            {
                plate.TargetOutline.effectColor = ParseColour(NameplateSettings.TargetColour, Color.yellow);
                float size = NameplateSettings.TargetOutlineSize;
                plate.TargetOutline.effectDistance = new Vector2(size, -size);
            }
        }
        private void HideAll()
        {
            foreach (Plate plate in _pool)
                if (plate.Rect != null && plate.Rect.gameObject.activeSelf) plate.Rect.gameObject.SetActive(false);
            if (_layer != null && _layer.gameObject.activeSelf) _layer.gameObject.SetActive(false);
            _actors.Clear();
            _filtered.Clear();
            _friendlies.Clear();
            _nextDiscovery = 0f;
        }
        internal void Suspend()
        {
            HideAll();
            NativeOverheadNameplateBridge.Restore();
        }
        internal void Shutdown()
        {
            NativeOverheadNameplateBridge.Restore();
            _actors.Clear(); _pool.Clear();
            if (_layer != null) UnityEngine.Object.Destroy(_layer.gameObject);
            _layer = null; _camera = null;
        }
    }
}
