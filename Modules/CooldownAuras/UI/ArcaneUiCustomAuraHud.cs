using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using ArcanesGroupFrames.Framework.Canvas;
using ArcanesGroupFrames.Framework.Configuration.UI;
using ArcanesGroupFrames.Framework.NativeUI;
using ArcanesGroupFrames.Modules.ActionBars.Configuration;
using ArcanesGroupFrames.Modules.ActionBars.Native;
using ArcanesGroupFrames.Modules.CooldownAuras.Editor;
using ArcanesGroupFrames.Modules.UnitFrames.Target;

namespace ArcanesGroupFrames.Modules.CooldownAuras.UI
{
    /// <summary>
    /// Independent player-created displays, capped at 200. Only reads
    /// native StatusEffects and existing Hotkeys cooldown snapshots; the native
    /// game remains authoritative. No global Object.Find/LoadAll in Update.
    /// </summary>
    internal sealed class ArcaneUiCustomAuraHud
    {
        internal sealed class Snapshot
        {
            internal bool Active;
            internal bool Available;
            internal float Progress;
            internal float Remaining;
            internal Sprite Sprite;
            internal string Name;
            internal int Stacks;
            internal float Value, Maximum;
            internal float SampleAt;
        }
        private sealed class View
        {
            internal RectTransform Root;
            internal Image Background, TextureLayer, Art, Fill;
            internal Text Caption, Timer;
            internal CustomAuraDragHandle Drag;
            internal string Display, GroupId;
            internal ArcaneUiAuraShapeGraphic[] Shapes;
            internal Image[] Borders;
            internal Image[] Glow;
            internal int GlowLastWidth, GlowLastHeight, GlowLastThickness;
            internal bool AnimationInitialized, LastTriggered, EverShown;
            internal bool ProgressInitialized;
            internal float DisplayProgress;
            internal float TransitionAt;
        }
        private readonly ArcaneCanvasManager _canvas;
        private RectTransform _layer;
        private readonly Dictionary<string, CustomAuraGroupDragHandle> _groupDrags = new Dictionary<string, CustomAuraGroupDragHandle>(StringComparer.Ordinal);
        private readonly Dictionary<string, RectTransform> _groupRoots = new Dictionary<string, RectTransform>(StringComparer.Ordinal);
        // 9.13: O(1) parent lookup for up to 200 active displays per visual frame.
        private readonly Dictionary<string, CustomAuraGroupDefinition> _groupDefinitions =
            new Dictionary<string, CustomAuraGroupDefinition>(StringComparer.Ordinal);
        private readonly Dictionary<string, Vector2> _groupPositions = new Dictionary<string, Vector2>(StringComparer.Ordinal);
        private readonly List<CustomAuraDefinition> _groupMembers = new List<CustomAuraDefinition>();
        private readonly Dictionary<string, CustomAuraDefinition> _definitions = new Dictionary<string, CustomAuraDefinition>(StringComparer.Ordinal);
        private readonly Dictionary<string, View> _views = new Dictionary<string, View>(StringComparer.Ordinal);
        private readonly Dictionary<string, float> _maxCooldown = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, float> _maxEffect = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Snapshot> _snapshots = new Dictionary<string, Snapshot>(StringComparer.Ordinal);
        private readonly Snapshot _extraSample = new Snapshot();
        private int _revision = -1;
        private Stats _lastPlayer;
        private float _nextRead;
        private Texture2D _whiteTexture;
        private Sprite _whiteSprite;

        internal ArcaneUiCustomAuraHud(ArcaneCanvasManager canvas) { _canvas = canvas; }
        private Sprite WhiteSprite()
        {
            if (_whiteSprite != null) return _whiteSprite;
            _whiteTexture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            _whiteTexture.name = "ArcaneUI_CustomAuraFill";
            _whiteTexture.SetPixels(new[] { Color.white, Color.white, Color.white, Color.white });
            _whiteTexture.Apply();
            _whiteSprite = Sprite.Create(_whiteTexture, new Rect(0f, 0f, 2f, 2f), new Vector2(.5f, .5f));
            return _whiteSprite;
        }
        private void Initialize()
        {
            if (_layer != null || !_canvas.IsReady) return;
            _layer = _canvas.CreateLayer("ArcaneUI_CustomAuraDisplays");
            var canvas = _layer.gameObject.AddComponent<UnityEngine.Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = -97;
            var raycaster = _layer.gameObject.AddComponent<GraphicRaycaster>();
            raycaster.enabled = false;
            _layer.gameObject.SetActive(false);
        }
        private void Rebuild()
        {
            foreach (var view in _views.Values)
                if (view.Root != null) UnityEngine.Object.Destroy(view.Root.gameObject);
            // Destroy old parent groups too: Unity destroys child views with them.
            foreach (var groupRoot in _groupRoots.Values)
                if (groupRoot != null) UnityEngine.Object.Destroy(groupRoot.gameObject);
            _views.Clear(); _snapshots.Clear(); _groupPositions.Clear(); _groupRoots.Clear(); _groupDrags.Clear();
            _definitions.Clear(); _groupDefinitions.Clear();
            foreach (var def in CustomAuraLibrary.All) _definitions[def.Id] = def;
            foreach (var group in CustomAuraLibrary.Groups) _groupDefinitions[group.Id] = group;
            foreach (var group in CustomAuraLibrary.Groups)
            {
                RectTransform parent = ArcaneUiControls.Rect(_layer, "AuraGroup_" + group.Id,
                    group.X, group.Y, 1f, 1f);
                // The group is a real parent: translation/scale/hiding act on all children.
                var header = ArcaneUiControls.Panel(parent, "GroupDragHeader", 0f, -26f,
                    160f, 22f, new Color(.1f,.16f,.22f,.8f), false);
                var headerImage = header.GetComponent<Image>();
                if (headerImage != null) headerImage.raycastTarget = CustomAuraLibrary.EditPositions;
                var text = ArcaneUiControls.Label(header, "GroupName", group.Name, 6f, 2f,
                    148f, 18f, 11, Color.white);
                text.raycastTarget = false;
                var drag = header.gameObject.AddComponent<CustomAuraGroupDragHandle>();
                drag.Bind(group.Id, parent, _canvas.Canvas);
                _groupDrags.Add(group.Id, drag);
                header.gameObject.SetActive(CustomAuraLibrary.EditPositions);
                _groupRoots.Add(group.Id, parent);
            }
            foreach (var def in CustomAuraLibrary.All)
            {
                // At a 200-aura limit, disabled records must not allocate
                // permanent Canvas objects (unless HUD editing is unlocked).
                if (!def.Enabled && !CustomAuraLibrary.EditPositions) continue;
                RectTransform groupParent = null;
                bool grouped = !string.IsNullOrEmpty(def.GroupId) &&
                    _groupRoots.TryGetValue(def.GroupId, out groupParent);
                var root = ArcaneUiControls.Panel(grouped ? groupParent : _layer,
                    "CustomAura_" + def.Id, grouped ? 0 : def.X, grouped ? 0 : def.Y,
                    def.Width, def.Height, new Color(.06f, .07f, .09f, .82f), false);
                var backdrop = root.GetComponent<Image>();
                var textureLayer = ArcaneUiControls.Panel(root, "CustomTextureLayer", 0, 0,
                    def.Width, def.Height, Color.white, false).GetComponent<Image>();
                textureLayer.raycastTarget = false;
                var fill = ArcaneUiControls.Panel(root, "ProgressFill", 0, 0, def.Width, def.Height,
                    Color.white, false).GetComponent<Image>();
                fill.sprite = WhiteSprite(); fill.type = Image.Type.Filled;
                fill.fillMethod = Image.FillMethod.Horizontal;
                fill.fillOrigin = (int)Image.OriginHorizontal.Left;
                var art = ArcaneUiControls.Panel(root, "ActionIcon", 1, 1, def.Width - 2, def.Height - 2,
                    Color.white, false).GetComponent<Image>();
                var caption = ArcaneUiControls.Label(root, "Title", def.Name, 3, 0,
                    def.Width - 6, def.Height, 13, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
                var timer = ArcaneUiControls.Label(root, "Timer", "", 2, def.Height - 15,
                    def.Width - 4, 14, 11, Color.white, TextAnchor.LowerRight, FontStyle.Bold);
                ArcaneUiAuraShapeGraphic[] shapes = null;
                if (def.Display == "Shape" || def.Display == "Charge Segments")
                {
                    int count = def.Display == "Shape" ? 2 : 20;
                    shapes = new ArcaneUiAuraShapeGraphic[count];
                    for (int i = 0; i < count; ++i)
                    {
                        var shapeRoot = ArcaneUiControls.Rect(root, "Shape_" + i,
                            0, 0, def.Width, def.Height);
                        shapes[i] = shapeRoot.gameObject.AddComponent<ArcaneUiAuraShapeGraphic>();
                        shapes[i].raycastTarget = false;
                    }
                    // Draw charge values over the shapes, never behind them.
                    var textOutline = caption.gameObject.AddComponent<Outline>();
                    textOutline.effectColor = Color.black;
                    textOutline.effectDistance = new Vector2(1f, -1f);
                    textOutline.useGraphicAlpha = false;
                    caption.transform.SetAsLastSibling();
                    timer.transform.SetAsLastSibling();
                }
                var glow = CustomAuraAnimation.CreateGlow(root);
                var borders = new Image[4];
                for (int side = 0; side < 4; side++)
                    borders[side] = ArcaneUiControls.Panel(root, "Border_" + side,
                        0f, 0f, 0f, 0f, Color.black, false).GetComponent<Image>();
                caption.transform.SetAsLastSibling();
                timer.transform.SetAsLastSibling();
                var drag = root.gameObject.AddComponent<CustomAuraDragHandle>();
                drag.Bind(def.Id, _canvas.Canvas, grouped);
                _views.Add(def.Id, new View { Root=root, Background=backdrop, TextureLayer=textureLayer, Art=art,
                    Fill=fill, Caption=caption, Timer=timer, Drag=drag,
                    Shapes=shapes, Borders=borders, Glow=glow, Display=def.Display, GroupId=def.GroupId });
                _snapshots.Add(def.Id, new Snapshot());
            }
            _revision = CustomAuraLibrary.Revision;
        }
        internal void Update()
        {
            // Drag mode deliberately survives closing Settings so the player
            // can move HUD elements unobstructed. Escape locks all displays.
            if (CustomAuraLibrary.EditPositions && Input.GetKeyDown(KeyCode.Escape))
                CustomAuraLibrary.SetEditing(false);
            if (_layer == null) Initialize();
            if (_layer == null) return;
            bool gameplay = !GameData.InCharSelect && NativeUiLayering.CanShowGameplayUi &&
                GameData.PlayerStats != null;
            bool visible = gameplay && (CustomAuraLibrary.EditPositions || AnyEnabled());
            if (_layer.gameObject.activeSelf != visible) _layer.gameObject.SetActive(visible);
            if (!visible) { _nextRead = 0f; return; }
            if (_revision != CustomAuraLibrary.Revision)
            {
                // Slider moves, text input and colour changes do not destroy live
                // UI. Rebuild only if the collection of identities changed.
                int expectedCount = 0;
                bool rebuild = false;
                foreach (var definition in CustomAuraLibrary.All)
                {
                    bool shouldHaveView = definition.Enabled || CustomAuraLibrary.EditPositions;
                    if (shouldHaveView) expectedCount++;
                    View existing;
                    bool hasView = _views.TryGetValue(definition.Id, out existing);
                    if (hasView != shouldHaveView ||
                        (hasView && (existing.Display != definition.Display ||
                            existing.GroupId != definition.GroupId)))
                    { rebuild = true; break; }
                }
                if (!rebuild && _views.Count != expectedCount) rebuild = true;
                if (!rebuild && _groupRoots.Count != CustomAuraLibrary.Groups.Count) rebuild = true;
                if (!rebuild)
                    foreach (var group in CustomAuraLibrary.Groups)
                        if (!_groupRoots.ContainsKey(group.Id)) { rebuild = true; break; }
                if (rebuild) Rebuild();
                else
                {
                    // Switching profiles may reuse the same aura IDs with new
                    // objects and new linked sources. Refresh their references.
                    _definitions.Clear(); _groupDefinitions.Clear();
                    foreach (var current in CustomAuraLibrary.All)
                        _definitions[current.Id] = current;
                    foreach (var group in CustomAuraLibrary.Groups)
                        _groupDefinitions[group.Id] = group;
                    foreach (var group in CustomAuraLibrary.Groups)
                    {
                        RectTransform parent;
                        if (!_groupRoots.TryGetValue(group.Id, out parent)) continue;
                        Transform header = parent.Find("GroupDragHeader/GroupName");
                        if (header != null)
                        {
                            Text heading = header.GetComponent<Text>();
                            if (heading != null) heading.text = group.Name;
                        }
                    }
                    _revision = CustomAuraLibrary.Revision;
                }
            }
            if (!object.ReferenceEquals(_lastPlayer, GameData.PlayerStats))
            {
                _lastPlayer = GameData.PlayerStats;
                _maxCooldown.Clear(); _maxEffect.Clear(); _nextRead = 0f;
            }
            _layer.GetComponent<GraphicRaycaster>().enabled = CustomAuraLibrary.EditPositions;
            if (Time.unscaledTime >= _nextRead)
            {
                _nextRead = Time.unscaledTime + .10f;
                // Pass 1: independently evaluate every source (including those that
                // other displays borrow). No duplicate spell/status read per link.
                foreach (var def in CustomAuraLibrary.All)
                {
                    if (!def.Enabled && !CustomAuraLibrary.EditPositions) continue;
                    Snapshot sample;
                    if (!_snapshots.TryGetValue(def.Id, out sample)) continue;
                    // Linked displays consume a source snapshot in pass 2, so
                    // don't read their redundant spell/effect triggers here.
                    if (!string.IsNullOrEmpty(def.LinkedAuraId))
                    {
                        sample.Active = false; sample.Available = false;
                        sample.SampleAt = Time.unscaledTime; continue;
                    }
                    if (PassesLoadRules(def)) ReadCombined(def, sample);
                    else
                    {
                        sample.Active = false; sample.Available = false;
                        // Without this, an Emberweaver class/load mismatch
                        // skips TryRead and produces no resource log at all.
                        if (def.Trigger == "Emberweaver Heat")
                            ArcaneUiAuraResourceReader.TraceAuraSuppressed(
                                "'" + def.Name + "' blocked by Load filters (class='" +
                                def.LoadClass + "', zone='" + def.LoadZone +
                                "', combat='" + def.LoadCombat + "', group='" +
                                def.LoadGroup + "', target=" + def.LoadRequiresTarget +
                                "). Actual class='" + RaidClassColors.ResolveClassNameForActor(
                                    GameData.PlayerStats, GameData.PlayerStats.MyName) +
                                "' learnedHeat=" + ArcaneUiAuraResourceReader.HasLearnedHeat());
                    }
                    sample.SampleAt = Time.unscaledTime;
                }
                // Pass 2: resolve shared source chains; cyclic/missing sources fail closed.
                foreach (var def in CustomAuraLibrary.All)
                {
                    if (string.IsNullOrEmpty(def.LinkedAuraId)) continue;
                    Snapshot dest;
                    if (!_snapshots.TryGetValue(def.Id, out dest)) continue;
                    if (!PassesLoadRules(def)) { dest.Active = false; dest.Available = false; continue; }
                    Snapshot source = ResolveLinkedSource(def);
                    if (source == null) { dest.Active = false; dest.Available = false; continue; }
                    CopySample(dest, source);
                    dest.Name = def.Name;
                    dest.SampleAt = Time.unscaledTime;
                }
                ArrangeGroups();
            }
            foreach (var group in CustomAuraLibrary.Groups)
            {
                RectTransform parent;
                if (!_groupRoots.TryGetValue(group.Id, out parent)) continue;
                bool editing = CustomAuraLibrary.EditPositions;
                parent.gameObject.SetActive(group.Enabled || editing);
                parent.localScale = Vector3.one * (group.Scale / 100f);
                if (!_groupDrags[group.Id].IsDragging)
                    parent.anchoredPosition = new Vector2(group.X, -group.Y);
                Transform header = parent.Find("GroupDragHeader");
                if (header != null && header.gameObject.activeSelf != editing) header.gameObject.SetActive(editing);
            }
            foreach (var def in CustomAuraLibrary.All)
            {
                View view;
                Snapshot sample;
                if (!_views.TryGetValue(def.Id, out view) || !_snapshots.TryGetValue(def.Id, out sample)) continue;
                bool edit = CustomAuraLibrary.EditPositions;
                float targetProgress = Mathf.Clamp01(sample.Progress);
                if (!view.ProgressInitialized)
                {
                    view.ProgressInitialized = true;
                    view.DisplayProgress = targetProgress;
                }
                else if (def.ProgressSmoothingMs > 0 && !edit)
                {
                    float factor = 1f - Mathf.Exp(-4000f * Mathf.Max(0f,Time.unscaledDeltaTime) /
                        def.ProgressSmoothingMs);
                    view.DisplayProgress = Mathf.Lerp(view.DisplayProgress,targetProgress,factor);
                    if (Mathf.Abs(view.DisplayProgress-targetProgress) < .001f)
                        view.DisplayProgress = targetProgress;
                }
                else view.DisplayProgress = targetProgress;
                CustomAuraGroupDefinition parentGroup = null;
                if (!string.IsNullOrEmpty(def.GroupId))
                    _groupDefinitions.TryGetValue(def.GroupId, out parentGroup);
                bool triggered = def.Enabled && sample.Active && (parentGroup == null || parentGroup.Enabled);
                float clock = Time.unscaledTime;
                if (!view.AnimationInitialized)
                {
                    view.AnimationInitialized = true;
                    view.LastTriggered = triggered;
                    view.EverShown = triggered;
                    view.TransitionAt = clock;
                }
                else if (view.LastTriggered != triggered)
                {
                    view.LastTriggered = triggered;
                    view.TransitionAt = clock;
                    if (triggered) view.EverShown = true;
                }
                float elapsed = clock - view.TransitionAt;
                // Preserve inactive records long enough to complete an optional exit.
                // Editing skips animation so drag hitboxes always match the visible UI.
                bool show = edit || triggered ||
                    (view.EverShown && CustomAuraAnimation.ShouldKeepExiting(def, elapsed));
                if (view.Root.gameObject.activeSelf != show) view.Root.gameObject.SetActive(show);
                if (!show) continue;
                CustomAuraConditionRule conditional = FindCondition(def, sample);
                string glowAction = conditional != null ? conditional.GlowAction : "Keep";
                CustomAuraAnimation.Pose motion = CustomAuraAnimation.Evaluate(
                    def, clock, elapsed, triggered, edit, glowAction);
                Color conditionalTint = ParseColour(def.Colour, Color.yellow);
                float scale = 1f;
                float customOpacity = 1f;
                if (conditional != null)
                {
                    if (conditional.ChangeColour) conditionalTint = ParseColour(conditional.Colour, conditionalTint);
                    if (conditional.ChangeOpacity) customOpacity = conditional.Opacity / 100f;
                    if (conditional.ChangeScale) scale = conditional.Scale / 100f;
                }
                view.Root.localScale = Vector3.one * scale * motion.Scale;
                if (!view.Drag.IsDragging)
                {
                    Vector2 groupOffset = Vector2.zero;
                    bool hasGroupPosition = parentGroup != null && _groupPositions.TryGetValue(def.Id, out groupOffset);
                    view.Root.anchoredPosition = (hasGroupPosition ? groupOffset :
                        parentGroup != null ? new Vector2(-5000f,-5000f) :
                        new Vector2(def.X, -def.Y)) + motion.Offset;
                }
                view.Root.sizeDelta = new Vector2(def.Width, def.Height);
                view.Background.raycastTarget = edit;
                Color background = ParseColour(def.BackgroundColour, new Color(.06f,.07f,.09f,1f));
                float alpha = def.Opacity / 100f * customOpacity * motion.Alpha;
                background.a *= alpha * def.BackgroundOpacity / 100f;
                if (edit && !def.Enabled) background.a = Mathf.Max(.25f, background.a);
                view.Background.color = background;
                Sprite texture = string.IsNullOrEmpty(def.BackgroundArtSpell) ? null :
                    ArcaneUiAuraSpellArtwork.Select(CustomAurasArtSelection.SpellOnly,
                        null, def.BackgroundArtSpell, null, null);
                view.TextureLayer.gameObject.SetActive(texture != null && def.TextureOpacity > 0);
                if (texture != null)
                {
                    view.TextureLayer.sprite = texture;
                    view.TextureLayer.preserveAspect = def.TexturePreserveAspect;
                    view.TextureLayer.color = new Color(1f,1f,1f,
                        alpha * def.TextureOpacity / 100f);
                    view.TextureLayer.rectTransform.sizeDelta = new Vector2(def.Width,def.Height);
                }
                Color accent = conditionalTint;
                bool icon = def.Display == "Icon";
                bool bar = def.Display == "Progress Bar";
                bool shapeDisplay = def.Display == "Shape" || def.Display == "Charge Segments";
                ConfigureBorders(view, def, alpha);
                bool glowLayoutChanged = view.GlowLastWidth != def.Width ||
                    view.GlowLastHeight != def.Height || view.GlowLastThickness != def.GlowWidth;
                float glowOpacity = motion.GlowAlpha * def.Opacity / 100f * customOpacity;
                CustomAuraAnimation.DrawGlow(view.Glow, def, def.Width, def.Height,
                    glowOpacity, glowLayoutChanged);
                if (glowOpacity > .001f)
                {
                    view.GlowLastWidth = def.Width;
                    view.GlowLastHeight = def.Height;
                    view.GlowLastThickness = def.GlowWidth;
                }
                view.Fill.gameObject.SetActive(bar);
                // Shapes are vector UI meshes; changing charges only updates their
                // colour and dimensions, not their GameObjects or Unity textures.
                if (view.Shapes != null)
                {
                    int n = def.Display == "Shape" ? 1 : Mathf.Clamp(def.SegmentCount, 1, 20);
                    int columns = def.Display == "Shape" ? 1 : Mathf.Clamp(def.SegmentsPerRow, 1, n);
                    int rows = (n + columns - 1) / columns;
                    float gap = n > 1 ? Mathf.Clamp(def.SegmentSpacing, 0, 16) : 0f;
                    float segmentWidth = Mathf.Max(1f, (def.Width - gap * (columns - 1)) / columns);
                    float segmentHeight = Mathf.Max(1f, (def.Height - gap * (rows - 1)) / rows);
                    Color unlit = ParseColour(def.InactiveColour, new Color(.28f,.21f,.18f,1f));
                    float shownProgress = edit && !sample.Active ? .6f : view.DisplayProgress;
                    float litProgress = Mathf.Clamp01(shownProgress) * n;
                    bool radialGauge = def.Display == "Shape" && def.RadialProgress &&
                        (def.Shape == "Ring" || def.Shape == "Arc");
                    for (int i = 0; i < view.Shapes.Length; ++i)
                    {
                        var graphic = view.Shapes[i];
                        bool active = i < n || (radialGauge && i == 1);
                        if (graphic.gameObject.activeSelf != active) graphic.gameObject.SetActive(active);
                        if (!active) continue;
                        graphic.SetShape(def.Shape);
                        graphic.SetRingThickness(def.RingThickness / 100f);
                        graphic.SetGradient(def.UseGradient, ParseColour(def.GradientColour, accent));
                        graphic.SetProgress(radialGauge && i == 1 ?
                            Mathf.Clamp01(shownProgress) : 1f,
                            radialGauge && i == 1 && def.RadialReverse);
                        graphic.rectTransform.anchoredPosition = radialGauge ? Vector2.zero :
                            new Vector2((i % columns) * (segmentWidth + gap), -(i / columns) * (segmentHeight + gap));
                        graphic.rectTransform.sizeDelta = new Vector2(segmentWidth, segmentHeight);
                        bool on = radialGauge ? i == 1 :
                            (def.Display == "Shape" ? sample.Progress > 0f : false);
                        Color shade;
                        if (def.Display == "Charge Segments")
                        {
                            int order = def.ReverseFill ? n - 1 - i : i;
                            shade = Color.Lerp(unlit,accent,Mathf.Clamp01(litProgress - order));
                        }
                        else shade = on ? accent : unlit;
                        shade.a *= alpha;
                        graphic.color = shade;
                    }
                }
                Sprite actualArt = ArcaneUiAuraSpellArtwork.Select(ArtSelection(def.ArtSource),
                    sample.Sprite, def.ArtSpell, def.MatchName, def.Name);
                view.Art.gameObject.SetActive(icon && actualArt != null);
                if (bar)
                {
                    view.Fill.color = new Color(accent.r, accent.g, accent.b, alpha);
                    view.Fill.rectTransform.sizeDelta = new Vector2(def.Width, def.Height);
                    view.Fill.rectTransform.anchoredPosition = Vector2.zero;
                    SetFillDirection(view.Fill, def.FillDirection);
                    float fraction = sample.Active ? view.DisplayProgress : .65f;
                    if (sample.Active && sample.Remaining > 0.001f)
                        fraction *= Mathf.Clamp01(1f -
                            (Time.unscaledTime - sample.SampleAt) / sample.Remaining);
                    view.Fill.fillAmount = Mathf.Clamp01(fraction);
                }
                if (icon && actualArt != null)
                {
                    view.Art.sprite = actualArt;
                    view.Art.color = conditional != null && conditional.ChangeColour
                        ? new Color(accent.r, accent.g, accent.b, alpha)
                        : new Color(1f,1f,1f,alpha);
                    view.Art.rectTransform.anchoredPosition = new Vector2(def.IconPadding, -def.IconPadding);
                    view.Art.rectTransform.sizeDelta = new Vector2(Mathf.Max(1f,def.Width-2*def.IconPadding),
                        Mathf.Max(1f,def.Height-2*def.IconPadding));
                    view.Art.preserveAspect = true;
                }
                view.Caption.gameObject.SetActive(shapeDisplay ? def.ShowValue : (!icon || actualArt == null || def.ShowLabel));
                view.Caption.text = shapeDisplay && def.LabelPattern == "%n" ?
                    (sample.Maximum > 0f ? Mathf.RoundToInt(sample.Value) + " / " +
                    Mathf.RoundToInt(sample.Maximum) : (edit ? "PREVIEW" : "")) :
                    FormatLabel(def.LabelPattern, sample, def);
                if (icon && actualArt == null && def.LabelPattern == "%n")
                    view.Caption.text = edit && !sample.Active ? "EDIT" : FirstLetter(sample.Name ?? def.Name);
                Color labelColour = ParseColour(def.TextColour, Color.white);
                labelColour.a *= alpha;
                view.Caption.color = labelColour;
                view.Caption.fontSize = def.TextSize;
                view.Caption.alignment = def.TextAlignment == "Left" ? TextAnchor.MiddleLeft :
                    def.TextAlignment == "Right" ? TextAnchor.MiddleRight : TextAnchor.MiddleCenter;
                view.Caption.rectTransform.sizeDelta = new Vector2(def.Width-6, def.Height);
                view.Timer.gameObject.SetActive(def.ShowTimer && sample.Active && sample.Remaining > .001f);
                view.Timer.fontSize = def.TimerSize;
                view.Timer.color = new Color(labelColour.r,labelColour.g,labelColour.b,labelColour.a);
                float timeLeft = Mathf.Max(0f, sample.Remaining -
                    (Time.unscaledTime - sample.SampleAt));
                view.Timer.text = timeLeft >= 10f ? Mathf.CeilToInt(timeLeft) + "s" :
                    timeLeft.ToString("0.0") + "s";
                view.Timer.rectTransform.anchoredPosition = new Vector2(2f, -(def.Height - 15));
                view.Timer.rectTransform.sizeDelta = new Vector2(def.Width-4, 14);
            }
        }

        private Snapshot ResolveLinkedSource(CustomAuraDefinition def)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal) { def.Id };
            string linked = def.LinkedAuraId;
            while (!string.IsNullOrEmpty(linked))
            {
                if (!seen.Add(linked)) return null;
                CustomAuraDefinition sourceDef;
                if (!_definitions.TryGetValue(linked, out sourceDef) || !sourceDef.Enabled) return null;
                if (string.IsNullOrEmpty(sourceDef.LinkedAuraId))
                {
                    Snapshot result;
                    return _snapshots.TryGetValue(linked, out result) && result.Available ? result : null;
                }
                linked = sourceDef.LinkedAuraId;
            }
            return null;
        }
        private void ArrangeGroups()
        {
            _groupPositions.Clear();
            foreach (var group in CustomAuraLibrary.Groups)
            {
                _groupMembers.Clear();
                foreach (var def in CustomAuraLibrary.All)
                {
                    if (def.GroupId != group.Id || !_views.ContainsKey(def.Id)) continue;
                    Snapshot state;
                    if (group.HideInactive && !CustomAuraLibrary.EditPositions &&
                        (!_snapshots.TryGetValue(def.Id, out state) || !state.Active))
                    {
                        View recent;
                        if (!_views.TryGetValue(def.Id, out recent) || !recent.EverShown ||
                            (!recent.LastTriggered && !CustomAuraAnimation.ShouldKeepExiting(
                                def, Time.unscaledTime - recent.TransitionAt))) continue;
                    }
                    _groupMembers.Add(def);
                }
                _groupMembers.Sort((a,b) =>
                {
                    if (group.Order == "Name") return string.Compare(a.Name,b.Name,StringComparison.OrdinalIgnoreCase);
                    if (group.Order == "Remaining")
                    {
                        Snapshot asnap, bsnap;
                        float ar = _snapshots.TryGetValue(a.Id,out asnap) ? asnap.Remaining : 0f;
                        float br = _snapshots.TryGetValue(b.Id,out bsnap) ? bsnap.Remaining : 0f;
                        int cmp = br.CompareTo(ar);
                        if (cmp != 0) return cmp;
                    }
                    int rank = a.GroupOrder.CompareTo(b.GroupOrder);
                    return rank != 0 ? rank : string.Compare(a.Id,b.Id,StringComparison.Ordinal);
                });
                if (group.Reverse) _groupMembers.Reverse();
                if (group.Layout == "HUD Panel")
                {
                    // Stable named slots (0 icon, 1 readout, 2 full-width gauge,
                    // 3 alert). An inactive alert never makes the bar jump.
                    CustomAuraDefinition icon = null, title = null, gauge = null;
                    foreach (var member in CustomAuraLibrary.All)
                    {
                        if (member.GroupId != group.Id) continue;
                        if (member.GroupOrder == 0) icon = member;
                        else if (member.GroupOrder == 1) title = member;
                        else if (member.GroupOrder == 2) gauge = member;
                    }
                    float gap = Mathf.Max(0f, group.Spacing);
                    float top = Mathf.Max(icon != null ? icon.Height : 0f,
                        title != null ? title.Height : 0f);
                    float sideX = (icon != null ? icon.Width : 0f) + gap;
                    float barY = top + gap;
                    float alertY = barY + (gauge != null ? gauge.Height : 0f) + gap;
                    foreach (var member in _groupMembers)
                    {
                        Vector2 pos = member.GroupOrder == 0 ? Vector2.zero :
                            member.GroupOrder == 1 ? new Vector2(sideX, 0f) :
                            member.GroupOrder == 2 ? new Vector2(0f, -barY) :
                            new Vector2(0f, -(alertY + Mathf.Max(0, member.GroupOrder - 3) *
                                (member.Height + gap)));
                        _groupPositions[member.Id] = pos;
                    }
                    continue;
                }
                float x = group.Layout == "Stacked Cards" ? 8f : 0f;
                float y = group.Layout == "Stacked Cards" ? 8f : 0f;
                float rowHeight = 0f;
                for (int i = 0; i < _groupMembers.Count; ++i)
                {
                    var def = _groupMembers[i];
                    _groupPositions[def.Id] = new Vector2(x, -y);
                    if (group.Layout == "Vertical" || group.Layout == "Stacked Cards") y += def.Height + group.Spacing;
                    else if (group.Layout == "Horizontal" || group.Layout == "Compact Row")
                        x += def.Width + (group.Layout == "Compact Row"
                            ? Mathf.Min(4, group.Spacing) : group.Spacing);
                    else
                    {
                        rowHeight = Mathf.Max(rowHeight, def.Height);
                        if ((i + 1) % group.Columns == 0)
                        { x = 0f; y += rowHeight + group.Spacing; rowHeight = 0f; }
                        else x += def.Width + group.Spacing;
                    }
                }
            }
        }

        private static Color ParseColour(string hex, Color fallback)
        {
            Color parsed;
            return ColorUtility.TryParseHtmlString(hex ?? "", out parsed) ? parsed : fallback;
        }
        private static CustomAurasArtSelection ArtSelection(string value)
        {
            if (value == "Matched Spell") return CustomAurasArtSelection.SpellOnly;
            if (value == "Trigger") return CustomAurasArtSelection.TriggerOnly;
            if (value == "None") return CustomAurasArtSelection.NoArtwork;
            return CustomAurasArtSelection.Auto;
        }
        private static void SetFillDirection(Image fill, string direction)
        {
            if (direction == "Bottom to Top" || direction == "Top to Bottom")
            {
                fill.fillMethod = Image.FillMethod.Vertical;
                fill.fillOrigin = direction == "Top to Bottom" ?
                    (int)Image.OriginVertical.Top : (int)Image.OriginVertical.Bottom;
            }
            else
            {
                fill.fillMethod = Image.FillMethod.Horizontal;
                fill.fillOrigin = direction == "Right to Left" ?
                    (int)Image.OriginHorizontal.Right : (int)Image.OriginHorizontal.Left;
            }
        }
        private static string FormatLabel(string format, Snapshot sample, CustomAuraDefinition def)
        {
            if (string.IsNullOrEmpty(format)) return sample.Name ?? def.Name;
            float remaining = Mathf.Max(0f, sample.Remaining - (Time.unscaledTime - sample.SampleAt));
            float percentage = sample.Maximum > 0f ? sample.Value / sample.Maximum * 100f : sample.Progress * 100f;
            return format.Replace("%n", sample.Name ?? def.Name)
                .Replace("%v", Mathf.RoundToInt(sample.Value).ToString())
                .Replace("%m", Mathf.RoundToInt(sample.Maximum).ToString())
                .Replace("%p", Mathf.RoundToInt(percentage).ToString() + "%")
                .Replace("%t", remaining.ToString("0.0"));
        }
        private static void ConfigureBorders(View view, CustomAuraDefinition def, float opacity)
        {
            if (view.Borders == null) return;
            int thick = Mathf.Clamp(def.BorderSize, 0, 12);
            Color color = ParseColour(def.BorderColour, Color.black);
            color.a *= opacity;
            for (int i = 0; i < view.Borders.Length; ++i)
            {
                Image border = view.Borders[i];
                border.gameObject.SetActive(thick > 0);
                if (thick <= 0) continue;
                border.color = color;
                RectTransform r = border.rectTransform;
                if (i == 0) { r.anchoredPosition = Vector2.zero; r.sizeDelta = new Vector2(def.Width, thick); }
                else if (i == 1) { r.anchoredPosition = new Vector2(0f, -(def.Height - thick)); r.sizeDelta = new Vector2(def.Width, thick); }
                else if (i == 2) { r.anchoredPosition = Vector2.zero; r.sizeDelta = new Vector2(thick, def.Height); }
                else { r.anchoredPosition = new Vector2(def.Width-thick,0f); r.sizeDelta = new Vector2(thick,def.Height); }
            }
        }

        private static string FirstLetter(string name) { return string.IsNullOrEmpty(name) ? "?" : name.Substring(0, 1); }
        private static bool AnyEnabled()
        {
            foreach (var def in CustomAuraLibrary.All)
                if (def.Enabled && Array.IndexOf(CustomAuraLibrary.Folders, def.Folder) >= 0) return true;
            return false;
        }
        private static bool PassesLoadRules(CustomAuraDefinition def)
        {
            // Unfinished class profiles remain serialized for later builds,
            // but do not activate as General-class trackers in this test build.
            if (Array.IndexOf(CustomAuraLibrary.Folders, def.Folder) < 0 ||
                (!string.IsNullOrEmpty(def.LoadClass) &&
                 Array.IndexOf(CustomAuraLibrary.Folders, def.LoadClass) < 0)) return false;
            Stats player = GameData.PlayerStats;
            if (player == null) return false;
            if (!string.IsNullOrEmpty(def.LoadCharacter) &&
                !string.Equals(def.LoadCharacter, player.MyName, StringComparison.OrdinalIgnoreCase))
                return false;
            if (!string.IsNullOrEmpty(def.LoadClass) && def.LoadClass != "General")
            {
                string resolvedClass = RaidClassColors.ResolveClassNameForActor(player, player.MyName);
                // FOTF Heat is class-authoritative even when the native character
                // briefly reports its Arcanist-backed class during login.
                // Every Emberweaver HUD child (including Always-visible art and
                // linked readouts) must survive the Arcanist backing-class name.
                // This is read-only and uses the same validated r107C bridge.
                bool emberHeat = def.LoadClass == "Emberweaver" &&
                    ArcaneUiAuraResourceReader.IsCurrentPlayerEmberweaver();
                if (!emberHeat && !string.Equals(def.LoadClass, resolvedClass,
                    StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }
            if (!string.IsNullOrEmpty(def.LoadZone) &&
                !string.Equals(def.LoadZone, SceneManager.GetActiveScene().name,
                    StringComparison.OrdinalIgnoreCase)) return false;
            if (!string.IsNullOrEmpty(def.LoadGroup) && def.LoadGroup != "Any")
            {
                bool hasParty = false;
                if (GameData.GroupMembers != null)
                    foreach (SimPlayerTracking member in GameData.GroupMembers)
                        if (member != null) { hasParty = true; break; }
                if (def.LoadGroup == "Raid" && !GameData.RaidActive) return false;
                if (def.LoadGroup == "Party" && (GameData.RaidActive || !hasParty)) return false;
                if (def.LoadGroup == "Solo" && (GameData.RaidActive || hasParty)) return false;
            }
            if (def.LoadRequiresTarget &&
                (GameData.PlayerControl == null || GameData.PlayerControl.CurrentTarget == null)) return false;
            if (!string.IsNullOrEmpty(def.LoadCombat) && def.LoadCombat != "Any")
            {
                bool combat;
                // Fail closed when the native version provides no reliable combat flag.
                if (!TargetNativeReadAdapter.TryReadCombat(player, out combat) &&
                    !TargetNativeReadAdapter.TryReadCombat(player.Myself, out combat) &&
                    !TargetNativeReadAdapter.TryReadCombat(GameData.PlayerControl, out combat))
                    return false;
                if ((def.LoadCombat == "In Combat") != combat) return false;
            }
            return true;
        }

        private static void CopySample(Snapshot to, Snapshot from)
        {
            to.Available = from.Available; to.Active = from.Active;
            to.Progress = from.Progress; to.Remaining = from.Remaining;
            to.Sprite = from.Sprite; to.Name = from.Name; to.Stacks = from.Stacks;
            to.Value = from.Value; to.Maximum = from.Maximum;
        }

        private void ReadCombined(CustomAuraDefinition def, Snapshot result)
        {
            ReadSingle(def, result, def.Trigger, def.MatchName, def.Id, def.MinimumValue, def.ShowWhenEmpty);
            if (def.AdditionalTriggers == null || def.AdditionalTriggers.Count == 0) return;
            bool any = result.Active;
            bool all = result.Active;
            bool usingExtra = false;
            bool allDataAvailable = result.Available;
            for (int i = 0; i < def.AdditionalTriggers.Count; i++)
            {
                CustomAuraTriggerRule rule = def.AdditionalTriggers[i];
                if (rule == null) continue;
                ReadSingle(def, _extraSample, rule.Type, rule.MatchName, def.Id + ":t" + i,
                    rule.MinimumValue, rule.ShowWhenEmpty);
                bool passed = _extraSample.Available && (rule.Not ? !_extraSample.Active : _extraSample.Active);
                allDataAvailable &= _extraSample.Available;
                any |= passed;
                all &= passed;
                if (passed && !rule.Not && !result.Active && !usingExtra)
                {
                    CopySample(result, _extraSample);
                    usingExtra = true;
                }
            }
            // Unavailable resources never satisfy a NOT rule and never cause
            // an AND rule to appear true due to an absent addon/provider.
            result.Active = def.TriggerLogic == "OR" ? any : all && allDataAvailable;
            if (result.Active && result.Progress <= 0f) result.Progress = 1f;
        }

        private static CustomAuraConditionRule FindCondition(CustomAuraDefinition def, Snapshot snapshot)
        {
            if (!snapshot.Active || def.Conditions == null) return null;
            foreach (CustomAuraConditionRule rule in def.Conditions)
            {
                if (rule == null) continue;
                float value;
                if (rule.Metric == "Value Percentage")
                {
                    if (snapshot.Maximum <= 0f) continue;
                    value = 100f * snapshot.Value / snapshot.Maximum;
                }
                else if (rule.Metric == "Resource Value")
                {
                    // An uncapped value, such as Wildfire stored damage, can
                    // still be compared against an absolute trigger threshold.
                    value = snapshot.Value;
                }
                else if (rule.Metric == "Remaining Seconds") value = snapshot.Remaining;
                else if (rule.Metric == "Stack Count") value = snapshot.Stacks;
                else continue;
                bool match = rule.Comparison == "At least" ? value >= rule.Threshold :
                    rule.Comparison == "Above" ? value > rule.Threshold :
                    rule.Comparison == "At most" ? value <= rule.Threshold :
                    rule.Comparison == "Below" ? value < rule.Threshold :
                    Mathf.Abs(value - rule.Threshold) < .001f;
                if (match) return rule; // First matching rule wins: explicit priority.
            }
            return null;
        }

        private void ReadSingle(CustomAuraDefinition def, Snapshot result,
            string trigger, string matchName, string cacheKey, int minimumValue, bool showWhenEmpty)
        {
            result.Available = false;
            result.Active = false; result.Remaining = 0f; result.Progress = 0f;
            result.Sprite = null; result.Name = def.Name; result.Stacks = 0;
            result.Value = 0f; result.Maximum = 0f;
            if (trigger == "Custom Expression")
            {
                float computed, maxComputed;
                bool active;
                string error;
                if (!ArcaneUiAuraExpression.TryRead(def, out computed, out maxComputed, out active, out error)) return;
                result.Available = true;
                result.Value = computed;
                result.Maximum = maxComputed;
                result.Progress = maxComputed > 0f ? Mathf.Clamp01(computed / maxComputed) :
                    (active ? 1f : 0f);
                result.Active = active;
                return;
            }
            if (ArcaneUiAuraResourceReader.IsResource(trigger))
            {
                float current, max;
                if (!ArcaneUiAuraResourceReader.TryRead(trigger, out current, out max)) return;
                result.Available = true;
                bool uncappedWildfire = trigger == "Emberweaver Wildfire Stored" ||
                    trigger == "Emberweaver Combust Projected";
                result.Value = uncappedWildfire ? Mathf.Max(0f, current) : Mathf.Clamp(current, 0f, max);
                result.Maximum = uncappedWildfire ? 0f : max;
                // An uncapped reservoir must never imply a fictitious percentage.
                // Text displays use the live absolute value, with no denominator.
                result.Progress = uncappedWildfire ? (current > 0f ? 1f : 0f) :
                    (max > 0f ? Mathf.Clamp01(current / max) : 0f);
                result.Active = (showWhenEmpty || current > 0f) && current >= minimumValue;
                return;
            }
            if (trigger == "Always") { result.Available = true; result.Active = true; result.Progress = 1f; return; }
            if (trigger == "Player Buff" || trigger == "Player Debuff" ||
                trigger == "Target Buff" || trigger == "Target Debuff")
            {
                bool target = trigger.StartsWith("Target", StringComparison.Ordinal);
                Stats stats = target ? (GameData.PlayerControl != null &&
                    GameData.PlayerControl.CurrentTarget != null ?
                    GameData.PlayerControl.CurrentTarget.MyStats : null) : GameData.PlayerStats;
                if (stats == null || stats.StatusEffects == null || stats.CurrentHP <= 0f ||
                    string.IsNullOrWhiteSpace(matchName)) { _maxEffect.Remove(cacheKey); return; }
                result.Available = true;
                bool expectBuff = trigger.EndsWith("Buff", StringComparison.Ordinal);
                foreach (StatusEffect status in stats.StatusEffects)
                {
                    if (status == null || status.Effect == null || status.Duration <= 0f) continue;
                    Spell spell = status.Effect;
                    bool buff = spell.Type == Spell.SpellType.Beneficial || spell.TargetHealing > 0 || spell.ShieldingAmt > 0;
                    if (buff != expectBuff || !Matches(spell.SpellName, matchName, spell.Id)) continue;
                    result.Active = true;
                    result.Remaining = status.Duration;
                    // A status's authored total duration is not reliably exposed
                    // in all Erenshor versions. Use highest observed remaining
                    // time as a clearly best-effort progress fraction; the
                    // numeric remaining time always comes from native state.
                    float max;
                    if (!_maxEffect.TryGetValue(cacheKey, out max) || status.Duration > max)
                        _maxEffect[cacheKey] = max = status.Duration;
                    result.Progress = max > 0f ? Mathf.Clamp01(status.Duration / max) : 1f;
                    result.Sprite = TargetNativeReadAdapter.ReadSpellIcon(spell);
                    result.Name = spell.SpellName;
                    result.Stacks = TargetNativeReadAdapter.ReadStacks(status);
                    return;
                }
                _maxEffect.Remove(cacheKey);
                return;
            }
            if (trigger == "Cooldown Active" || trigger == "Cooldown Ready")
            {
                if (string.IsNullOrWhiteSpace(matchName)) return;
                string foundName = null;
                Sprite foundIcon = null;
                float cooldown = 0f;
                bool anyMatching = false;
                bool ready = false;
                if (NativeActionBarDiscovery.HasValidLiveSnapshot)
                {
                    for (int slot = 1; slot <= 10; ++slot)
                    {
                        NativeActionBarDiscovery.NativeSlotSnapshot hk;
                        if (!NativeActionBarDiscovery.TryGetActiveSlot(slot, out hk) || hk == null ||
                            !hk.HasAssignment || !Matches(hk.AbilityName, matchName, null)) continue;
                        anyMatching = true;
                        foundName = hk.AbilityName; foundIcon = hk.AbilityIcon;
                        float time = Mathf.Max(0f, hk.RawCooldown / 60f);
                        if (time <= .001f) ready = true;
                        else cooldown = Mathf.Max(cooldown, time);
                    }
                }
                for (int bar=1; bar<ActionBarPlanSettings.Count; ++bar)
                    for (int slot=1; slot<=ActionBarPlanSettings.SlotCount(bar); ++slot)
                    {
                        string key, name; Sprite icon; float time;
                        if (!NativeExtraHotkeySlots.TryReadTrackerCooldown(bar, slot, out key, out name,
                            out icon, out time) || !Matches(name, matchName, null)) continue;
                        anyMatching = true;
                        foundName = name; foundIcon = icon;
                        if (time <= .001f) ready = true;
                        else cooldown = Mathf.Max(cooldown, time);
                    }
                result.Available = anyMatching;
                result.Sprite = foundIcon;
                if (!anyMatching) _maxCooldown.Remove(cacheKey);
                if (foundName != null) result.Name = foundName;
                // Any live ready copy wins over a stale duplicate (Pyroclasm reset).
                if (ready) cooldown = 0f;
                result.Active = anyMatching && (trigger == "Cooldown Ready" ? ready || cooldown <= .001f : cooldown > .001f);
                if (trigger == "Cooldown Ready")
                { result.Progress = 1f; _maxCooldown.Remove(cacheKey); return; }
                result.Remaining = cooldown;
                float max;
                if (!_maxCooldown.TryGetValue(cacheKey, out max) || cooldown > max)
                    _maxCooldown[cacheKey] = max = cooldown;
                if (cooldown <= .001f) _maxCooldown.Remove(cacheKey);
                result.Progress = max > .001f ? Mathf.Clamp01(cooldown / max) : 0f;
            }
        }
        private static bool Matches(string name, string filter, string id)
        {
            if (string.IsNullOrEmpty(filter)) return false;
            // Allow a spell's stable native id as an exact match or match its name ignoring case.
            return (id != null && string.Equals(id, filter, StringComparison.OrdinalIgnoreCase)) ||
                (!string.IsNullOrEmpty(name) && name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0);
        }
        internal void Shutdown()
        {
            if (_layer != null) UnityEngine.Object.Destroy(_layer.gameObject);
            _layer = null; _views.Clear(); _snapshots.Clear(); _groupRoots.Clear();
            _groupDrags.Clear(); _groupPositions.Clear(); _groupDefinitions.Clear();
            _definitions.Clear(); _maxCooldown.Clear();
            _maxEffect.Clear(); _lastPlayer = null;
            if (_whiteSprite != null) UnityEngine.Object.Destroy(_whiteSprite);
            if (_whiteTexture != null) UnityEngine.Object.Destroy(_whiteTexture);
            _whiteSprite = null; _whiteTexture = null;
            CustomAuraLibrary.SetEditing(false);
        }
    }

    /// <summary>Raycast-transparent unless explicitly unlocked in the aura editor.
    /// Drag saves once on release. This does not move native hotkey UI.</summary>
    internal sealed class CustomAuraGroupDragHandle : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        private string _id;
        private RectTransform _group;
        private UnityEngine.Canvas _canvas;
        internal bool IsDragging { get; private set; }
        internal void Bind(string id, RectTransform group, UnityEngine.Canvas canvas)
        { _id = id; _group = group; _canvas = canvas; }
        public void OnBeginDrag(PointerEventData e)
        { IsDragging = CustomAuraLibrary.EditPositions && e.button == PointerEventData.InputButton.Left; }
        public void OnDrag(PointerEventData e)
        {
            if (!IsDragging || _group == null) return;
            float scale = _canvas != null ? Mathf.Max(.01f,_canvas.scaleFactor) : 1f;
            Vector2 pos = _group.anchoredPosition + e.delta / scale;
            _group.anchoredPosition = new Vector2(Mathf.Clamp(pos.x,0,1820),Mathf.Clamp(pos.y,-980,0));
        }
        public void OnEndDrag(PointerEventData e)
        {
            if (!IsDragging || _group == null) return;
            IsDragging = false;
            CustomAuraLibrary.MoveGroup(_id, _group.anchoredPosition.x, -_group.anchoredPosition.y);
        }
    }

    internal sealed class CustomAuraDragHandle : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        private string _id;
        private UnityEngine.Canvas _canvas;
        private RectTransform _rect;
        internal bool IsDragging { get; private set; }
        private bool _grouped;
        internal void Bind(string id, UnityEngine.Canvas canvas, bool grouped)
        { _id = id; _canvas = canvas; _rect = GetComponent<RectTransform>(); _grouped = grouped; }
        public void OnBeginDrag(PointerEventData eventData)
        { IsDragging = !_grouped && CustomAuraLibrary.EditPositions && eventData.button == PointerEventData.InputButton.Left; }
        public void OnDrag(PointerEventData eventData)
        {
            if (!IsDragging || _rect == null) return;
            float scale = _canvas != null ? Mathf.Max(.01f, _canvas.scaleFactor) : 1f;
            Vector2 p = _rect.anchoredPosition + eventData.delta / scale;
            _rect.anchoredPosition = new Vector2(Mathf.Clamp(p.x,0,1820),Mathf.Clamp(p.y,-980,0));
        }
        public void OnEndDrag(PointerEventData eventData)
        {
            if (!IsDragging || _rect == null) return;
            IsDragging = false;
            CustomAuraLibrary.Move(_id, _rect.anchoredPosition.x, -_rect.anchoredPosition.y);
        }
    }
}
