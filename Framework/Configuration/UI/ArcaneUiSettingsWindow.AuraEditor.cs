using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using ArcanesGroupFrames.Modules.CooldownAuras.Editor;
using ArcanesGroupFrames.Modules.CooldownAuras.UI;

namespace ArcanesGroupFrames.Framework.Configuration.UI
{
    internal sealed partial class ArcaneUiSettingsWindow
    {
        private RectTransform _auraEditorExample;
        private Image _auraEditorExampleFill;
        private Image _auraEditorArtwork;
        private Image _auraPreviewTexture;
        private Image[] _auraPreviewBorders;
        private Image[] _auraPreviewGlows;
        private CanvasGroup _auraPreviewMotionGroup;
        private float _auraPreviewStartedAt, _auraPreviewBaseScale;
        private Vector2 _auraPreviewBasePosition;
        private string _auraPreviewGlowAction = "Keep";
        private float _auraPreviewLastGlowWidth = -1f, _auraPreviewLastGlowHeight = -1f;
        private int _auraPreviewLastGlowThickness = -1;
        private Text _auraEditorExampleLabel;
        private Text _auraEditorPreviewStatus;
        private ArcaneUiAuraShapeGraphic[] _auraPreviewShapes;
        private RectTransform _auraSearchList;
        private readonly HashSet<string> _collapsedAuraFolders = new HashSet<string>(StringComparer.Ordinal);
        private string _auraDeleteConfirmId;
        private string _auraTemplateFolder = "Emberweaver";
        private string _auraTemplateFeedback = "";
        private string _auraSpellSearch = "";
        private string _auraSpellScope = "Current Class";
        private int _auraSpellPage;
        private bool _auraFoldersInitialized;
        private string _selectedAuraGroupId;
        private string _deleteAuraGroupConfirm;
        private string _auraProfileDraft = "New Profile";
        private string _auraProfileFeedback = "";
        private string _deleteAuraProfileConfirm;
        private string _pendingAuraImportJson;
        private string _pendingAuraImportProfile;
        // Designer-only sample state; never persisted or applied to the game.
        private int _auraPreviewResourcePercent = 60;
        private string _auraExpressionFeedback = "Select TEST LIVE or CHECK SYNTAX.";
        private float _auraNextExpressionPreviewRead;


        private static readonly string[] AuraTriggerOptions =
        {
            "Always", "Player Buff", "Player Debuff", "Target Buff", "Target Debuff",
            "Cooldown Active", "Cooldown Ready", "Emberweaver Heat",
            "Emberweaver Flashfire", "Emberweaver Wildfire Stored", "Emberweaver Combust Projected", "Player Health", "Player Mana", "Target Health", "Target Mana", "Player Absorb"
        };

        private static bool IsAuraEditorTab(string tab)
        {
            return tab == "Aura Manager" || tab == "My Auras" || tab == "Display" || tab == "Trigger";
        }

        private void RenderCustomAuraPreview()
        {
            if (_previewHost == null) return;
            ArcaneUiControls.ClearChildren(_previewHost);
            _auraEditorExample = ArcaneUiControls.Panel(_previewHost, "AuraSample", 285f, 260f, 210f, 32f,
                new Color(.07f, .08f, .10f, .93f), false);
            _auraPreviewMotionGroup = _auraEditorExample.gameObject.AddComponent<CanvasGroup>();
            _auraPreviewMotionGroup.blocksRaycasts = false;
            _auraPreviewGlows = CustomAuraAnimation.CreateGlow(_auraEditorExample);
            _auraPreviewStartedAt = Time.unscaledTime;
            _auraPreviewLastGlowWidth = -1f;
            _auraPreviewLastGlowHeight = -1f;
            _auraPreviewLastGlowThickness = -1;
            _auraPreviewTexture = ArcaneUiControls.Panel(_auraEditorExample, "BackgroundArtwork",
                0f, 0f, 210f, 32f, Color.white, false).GetComponent<Image>();
            _auraPreviewTexture.raycastTarget = false;
            _auraEditorExampleFill = ArcaneUiControls.Panel(_auraEditorExample, "Fill", 0f, 0f,
                136f, 32f, new Color(.86f, .47f, .21f, 1f), false).GetComponent<Image>();
            _auraEditorArtwork = ArcaneUiControls.Panel(_auraEditorExample, "SpellArtwork", 0f, 0f,
                210f, 32f, Color.white, false).GetComponent<Image>();
            _auraEditorArtwork.preserveAspect = true;
            _auraEditorExampleLabel = ArcaneUiControls.Label(_auraEditorExample, "Text", "",
                2f, 0f, 204f, 32f, 14, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            _auraPreviewShapes = new ArcaneUiAuraShapeGraphic[20];
            for (int i = 0; i < _auraPreviewShapes.Length; ++i)
            {
                var element = ArcaneUiControls.Rect(_auraEditorExample, "SampleShape_" + i,
                    0f, 0f, 22f, 22f);
                _auraPreviewShapes[i] = element.gameObject.AddComponent<ArcaneUiAuraShapeGraphic>();
                _auraPreviewShapes[i].raycastTarget = false;
            }
            _auraPreviewBorders = new Image[4];
            for (int edge = 0; edge < 4; edge++)
                _auraPreviewBorders[edge] = ArcaneUiControls.Panel(_auraEditorExample,
                    "Outline_" + edge, 0f, 0f, 0f, 0f, Color.black, false).GetComponent<Image>();
            _auraEditorExampleLabel.transform.SetAsLastSibling();
            _auraEditorPreviewStatus = ArcaneUiControls.Label(_previewHost, "Status", "", 50f, 570f,
                710f, 40f, 13, Muted);
            RenderAuraGroupSelectionPreview();
            ArcaneUiControls.Button(_previewHost, "EditSelectedAppearance", "EDIT APPEARANCE",
                235f, 518f, 166f, 36f, Card, Theme.Text, () =>
                { _auraWorkspaceTab = "Display"; RenderPage(); }, 11);
            ArcaneUiControls.Button(_previewHost, "EditSelectedTrigger", "EDIT TRIGGER",
                410f, 518f, 166f, 36f, Card, Theme.Text, () =>
                { _auraWorkspaceTab = "Trigger"; RenderPage(); }, 11);
            UpdateCustomAuraPreview();
        }

        // 9.13: preview the whole parent HUD without allocating gameplay UI.
        // Every tile selects its real saved definition, so grouped editing stays
        // one-click rather than jumping between the group and aura library.
        private void RenderAuraGroupSelectionPreview()
        {
            var selected = CustomAuraLibrary.Selected;
            if (selected == null || string.IsNullOrEmpty(selected.GroupId)) return;
            var group = CustomAuraLibrary.FindGroup(selected.GroupId);
            if (group == null) return;
            var members = new List<CustomAuraDefinition>();
            foreach (var candidate in CustomAuraLibrary.All)
                if (candidate.GroupId == group.Id &&
                    Array.IndexOf(CustomAuraLibrary.Folders, candidate.Folder) >= 0) members.Add(candidate);
            members.Sort((a,b) =>
            {
                int cmp = a.GroupOrder.CompareTo(b.GroupOrder);
                return cmp != 0 ? cmp : string.Compare(a.Id,b.Id,StringComparison.Ordinal);
            });
            var surface = ArcaneUiControls.Panel(_previewHost, "GroupVisualSelector", 58f, 18f,
                696f, 230f, Card, false);
            ArcaneUiControls.Label(surface, "GroupTitle", group.Name.ToUpperInvariant() +
                "  -  CLICK AN ELEMENT TO EDIT", 12f, 4f, 660f, 22f, 13,
                Theme.Accent, TextAnchor.MiddleLeft, FontStyle.Bold);
            int shown = Mathf.Min(group.Layout == "HUD Panel" ? 4 : 8, members.Count);
            for (int i = 0; i < shown; i++)
            {
                CustomAuraDefinition member = members[i];
                float x, y, w, h;
                if (group.Layout == "HUD Panel" && member.GroupOrder >= 0 && member.GroupOrder <= 3)
                {
                    x = member.GroupOrder == 1 ? 126f : 14f;
                    y = member.GroupOrder <= 1 ? 34f : member.GroupOrder == 2 ? 109f : 164f;
                    w = member.GroupOrder == 0 ? 100f : member.GroupOrder == 1 ? 544f : 656f;
                    h = member.GroupOrder <= 1 ? 66f : member.GroupOrder == 2 ? 46f : 45f;
                }
                else
                {
                    // Works for any group layout including nested display examples.
                    int ordinal = group.Layout == "HUD Panel" ? Mathf.Min(7,i) : i;
                    x = 14f + (ordinal % 4) * 166f;
                    y = 34f + (ordinal / 4) * 86f;
                    w = 156f; h = 76f;
                }
                Color shade;
                if (!ColorUtility.TryParseHtmlString(member.BackgroundColour, out shade))
                    shade = Card;
                bool current = member.Id == selected.Id;
                Color buttonColour = current ? Theme.Accent : ArcaneUiControls.Multiply(shade, 1.5f);
                string label = member.Display == "Charge Segments" ? member.Name + "  [" +
                    Mathf.RoundToInt(_auraPreviewResourcePercent/10f) + "/10]" :
                    member.Display == "Progress Bar" ? member.Name + "  [" +
                    _auraPreviewResourcePercent + "%]" : member.Name;
                string id = member.Id;
                var button = ArcaneUiControls.Button(surface, "AuraComponent_" + id, label,
                    x, y, w, h, buttonColour, Theme.Text, () =>
                    {
                        CustomAuraLibrary.Select(id);
                        _auraWorkspaceTab = "Display";
                        RenderPage();
                    }, 11);
                Text caption = button.GetComponentInChildren<Text>();
                if (caption != null)
                {
                    caption.alignment = TextAnchor.MiddleCenter;
                    caption.resizeTextForBestFit = true;
                    caption.resizeTextMinSize = 10;
                    caption.resizeTextMaxSize = 13;
                }
                if (member.Display == "Icon")
                {
                    Sprite art = ArcaneUiAuraSpellArtwork.Select(AuraArtworkSelection(member.ArtSource),
                        null, member.ArtSpell, member.MatchName, member.Name);
                    if (art != null)
                    {
                        var image = ArcaneUiControls.Panel(button.transform, "SpellIcon", 5f, 5f,
                            Mathf.Min(54f,h-10f), h-10f, Color.white, false).GetComponent<Image>();
                        image.sprite = art; image.preserveAspect = true;
                        image.raycastTarget = false;
                        if (caption != null)
                        {
                            caption.rectTransform.anchoredPosition = new Vector2(60f,0f);
                            caption.rectTransform.sizeDelta = new Vector2(Mathf.Max(20f,w-70f),h);
                        }
                    }
                }
                if (member.Display == "Charge Segments" && w > 250f)
                {
                    for (int segment = 0; segment < 10; ++segment)
                        ArcaneUiControls.Panel(button.transform, "Charge" + segment,
                            12f + segment * (w-28f)/10f, h - 17f,
                            (w-28f)/10f - 5f, 8f,
                            segment < Mathf.CeilToInt(_auraPreviewResourcePercent/10f) ?
                                Theme.Accent : Theme.Border, false);
                }
            }
            if (members.Count > shown)
                ArcaneUiControls.Label(surface, "AdditionalMembers", "+ " + (members.Count-shown) +
                    " additional group elements", 16f, 210f, 650f, 18f, 10, Muted);
        }

        private void UpdateCustomAuraPreview()
        {
            if (_auraEditorExample == null) return;
            CustomAuraDefinition aura = CustomAuraLibrary.Selected;
            _auraEditorExample.gameObject.SetActive(aura != null);
            if (_auraEditorPreviewStatus != null)
            {
                _auraEditorPreviewStatus.text = aura == null ?
                    "Create a display from the Aura Manager to begin." :
                    (aura.Enabled ? "Enabled" : "Disabled in gameplay") + "   •   " +
                    aura.Trigger + "   •   " + aura.Display +
                    (CustomAuraLibrary.EditPositions ? "   •   DRAG MODE ACTIVE" : "");
            }
            if (aura == null) return;
            Transform tile = _previewHost != null ?
                _previewHost.Find("GroupVisualSelector/AuraComponent_" + aura.Id) : null;
            if (tile != null)
            {
                var label = tile.GetComponentInChildren<Text>();
                if (label != null) label.text = aura.Display == "Charge Segments" ?
                    aura.Name + "  [" + Mathf.RoundToInt(_auraPreviewResourcePercent/10f) + "/10]" :
                    aura.Display == "Progress Bar" ? aura.Name + "  [" + _auraPreviewResourcePercent + "%]" : aura.Name;
            }
            Color colour;
            if (!ColorUtility.TryParseHtmlString(aura.Colour, out colour)) colour = Theme.Accent;
            float previewAlpha = aura.Opacity / 100f;
            float conditionScale = 1f;
            _auraPreviewGlowAction = "Keep";
            // Designer preview samples resource percentage, 5s remaining, 2 stacks.
            // Only appearance is simulated; gameplay triggers are not fired.
            if (aura.Conditions != null)
                foreach (CustomAuraConditionRule rule in aura.Conditions)
                {
                    if (rule == null) continue;
                    float sampleMetric = rule.Metric == "Value Percentage" ? _auraPreviewResourcePercent :
                        rule.Metric == "Resource Value" ? Mathf.RoundToInt(_auraPreviewResourcePercent / 10f) :
                        rule.Metric == "Remaining Seconds" ? 5f : 2f;
                    bool matches = rule.Comparison == "At least" ? sampleMetric >= rule.Threshold :
                        rule.Comparison == "Above" ? sampleMetric > rule.Threshold :
                        rule.Comparison == "At most" ? sampleMetric <= rule.Threshold :
                        rule.Comparison == "Below" ? sampleMetric < rule.Threshold :
                        Mathf.Abs(sampleMetric - rule.Threshold) < .001f;
                    if (!matches) continue;
                    Color tint;
                    if (rule.ChangeColour && ColorUtility.TryParseHtmlString(rule.Colour, out tint)) colour = tint;
                    if (rule.ChangeOpacity) previewAlpha *= rule.Opacity / 100f;
                    if (rule.ChangeScale) conditionScale = rule.Scale / 100f;
                    _auraPreviewGlowAction = rule.GlowAction;
                    break;
                }
            bool compact = _page == "auras" && _tab == "Aura Manager";
            float width = Mathf.Min(compact ? 410f : 360f, aura.Width);
            float height = Mathf.Min(compact ? 140f : 70f, aura.Height);
            _auraEditorExample.sizeDelta = new Vector2(width, height);
            float parentWidth = _auraEditorExample.parent is RectTransform
                ? ((RectTransform)_auraEditorExample.parent).rect.width : 814f;
            bool hasGroup = !string.IsNullOrEmpty(aura.GroupId) &&
                CustomAuraLibrary.FindGroup(aura.GroupId) != null;
            float fitScale = Mathf.Min(3.5f, 670f / Mathf.Max(1f, width),
                (hasGroup ? 176f : 270f) / Mathf.Max(1f, height));
            _auraEditorExample.localScale = Vector3.one * fitScale * conditionScale;
            _auraEditorExample.anchoredPosition = new Vector2(
                (parentWidth - width * fitScale * conditionScale) * .5f,
                -(hasGroup ? 330f + (184f - height * fitScale * conditionScale) * .5f :
                  (640f - height * fitScale * conditionScale) * .5f));
            _auraPreviewBaseScale = fitScale * conditionScale;
            _auraPreviewBasePosition = _auraEditorExample.anchoredPosition;
            Color previewBackdrop = PreviewBackground(aura);
            previewBackdrop.a *= conditionScale > 0f ? previewAlpha / Mathf.Max(.001f, aura.Opacity / 100f) : 1f;
            _auraEditorExample.GetComponent<Image>().color = previewBackdrop;
            bool bar = aura.Display == "Progress Bar";
            bool shapes = aura.Display == "Shape" || aura.Display == "Charge Segments";
            _auraEditorExampleFill.gameObject.SetActive(bar);
            if (_auraPreviewShapes != null)
            {
                bool radial = aura.Display == "Shape" && aura.RadialProgress &&
                    (aura.Shape == "Ring" || aura.Shape == "Arc");
                int count = aura.Display == "Shape" ? 1 :
                    aura.Display == "Charge Segments" ? Mathf.Clamp(aura.SegmentCount,1,20) : 0;
                int columns = count <= 1 ? 1 : Mathf.Clamp(aura.SegmentsPerRow, 1, count);
                int rows = (count + columns - 1) / columns;
                float gap = count > 1 ? Mathf.Clamp(aura.SegmentSpacing,0,16) : 0f;
                float unit = count > 0 ? Mathf.Max(1f,(width - gap*(columns-1))/columns) : width;
                float unitHeight = Mathf.Max(1f,(height-gap*(rows-1))/rows);
                int lit = Mathf.CeilToInt(count * _auraPreviewResourcePercent / 100f);
                for (int i = 0; i < _auraPreviewShapes.Length; ++i)
                {
                    var shape = _auraPreviewShapes[i];
                    bool active = i < count || (radial && i == 1);
                    shape.gameObject.SetActive(active);
                    if (!active) continue;
                    shape.SetShape(aura.Shape);
                    shape.SetRingThickness(aura.RingThickness/100f);
                    shape.SetProgress(radial && i == 1 ? _auraPreviewResourcePercent / 100f : 1f,
                        radial && i == 1 && aura.RadialReverse);
                    Color gradient;
                    if (!ColorUtility.TryParseHtmlString(aura.GradientColour, out gradient)) gradient = colour;
                    shape.SetGradient(aura.UseGradient, gradient);
                    shape.rectTransform.anchoredPosition = new Vector2((i%columns)*(unit+gap),
                        -(i/columns)*(unitHeight+gap));
                    shape.rectTransform.sizeDelta = new Vector2(unit, unitHeight);
                    bool on = radial ? i == 1 : aura.Display == "Shape" ||
                        (aura.ReverseFill ? i >= count-lit : i < lit);
                    Color off;
                    if (!ColorUtility.TryParseHtmlString(aura.InactiveColour, out off))
                        off = new Color(.28f,.21f,.18f,1f);
                    shape.color = on ? colour : off;
                    shape.color = new Color(shape.color.r, shape.color.g, shape.color.b,
                        shape.color.a * previewAlpha);
                }
            }
            if (_auraPreviewTexture != null)
            {
                Sprite texture = string.IsNullOrEmpty(aura.BackgroundArtSpell) ? null :
                    ArcaneUiAuraSpellArtwork.Select(CustomAurasArtSelection.SpellOnly,
                        null, aura.BackgroundArtSpell, null, null);
                _auraPreviewTexture.gameObject.SetActive(texture != null && aura.TextureOpacity > 0);
                if (texture != null)
                {
                    _auraPreviewTexture.sprite = texture;
                    _auraPreviewTexture.preserveAspect = aura.TexturePreserveAspect;
                    _auraPreviewTexture.color = new Color(1f,1f,1f,previewAlpha * aura.TextureOpacity/100f);
                    _auraPreviewTexture.rectTransform.sizeDelta = new Vector2(width,height);
                }
            }
            _auraEditorExampleFill.color = new Color(colour.r,colour.g,colour.b,aura.Opacity/100f);
            if (_auraEditorArtwork != null)
            {
                Sprite art = ArcaneUiAuraSpellArtwork.Select(AuraArtworkSelection(aura.ArtSource),
                    null, aura.ArtSpell, aura.MatchName, aura.Name);
                bool showArt = aura.Display == "Icon" && art != null;
                _auraEditorArtwork.gameObject.SetActive(showArt);
                if (showArt)
                {
                    _auraEditorArtwork.sprite = art;
                    _auraEditorArtwork.color = new Color(1f,1f,1f,aura.Opacity/100f);
                    _auraEditorArtwork.rectTransform.anchoredPosition = new Vector2(aura.IconPadding,-aura.IconPadding);
                    _auraEditorArtwork.rectTransform.sizeDelta = new Vector2(Mathf.Max(1,width-2*aura.IconPadding),
                        Mathf.Max(1,height-2*aura.IconPadding));
                }
            }
            bool verticalFill = aura.FillDirection == "Bottom to Top" ||
                aura.FillDirection == "Top to Bottom";
            float fillFraction = _auraPreviewResourcePercent / 100f;
            _auraEditorExampleFill.rectTransform.sizeDelta = verticalFill ?
                new Vector2(width,height*fillFraction) : new Vector2(width*fillFraction,height);
            _auraEditorExampleFill.rectTransform.anchoredPosition =
                aura.FillDirection == "Right to Left" ? new Vector2(width*(1f-fillFraction),0f) :
                aura.FillDirection == "Bottom to Top" ? new Vector2(0f,-height*(1f-fillFraction)) : Vector2.zero;
            if (_auraPreviewBorders != null)
            {
                int thickness = Mathf.Clamp(aura.BorderSize,0,12);
                Color edgeColour;
                if (!ColorUtility.TryParseHtmlString(aura.BorderColour,out edgeColour))
                    edgeColour = Color.black;
                edgeColour.a *= aura.Opacity/100f;
                for (int i = 0; i < _auraPreviewBorders.Length; i++)
                {
                    Image edge = _auraPreviewBorders[i];
                    edge.gameObject.SetActive(thickness > 0);
                    edge.color = edgeColour;
                    RectTransform r = edge.rectTransform;
                    if (i == 0) { r.anchoredPosition = Vector2.zero; r.sizeDelta = new Vector2(width,thickness); }
                    else if (i == 1) { r.anchoredPosition = new Vector2(0f,-(height-thickness)); r.sizeDelta = new Vector2(width,thickness); }
                    else if (i == 2) { r.anchoredPosition = Vector2.zero; r.sizeDelta = new Vector2(thickness,height); }
                    else { r.anchoredPosition = new Vector2(width-thickness,0f); r.sizeDelta = new Vector2(thickness,height); }
                }
            }
            _auraEditorExampleLabel.rectTransform.sizeDelta = new Vector2(width, height);
            Color textColour;
            if (!ColorUtility.TryParseHtmlString(aura.TextColour, out textColour)) textColour = Color.white;
            _auraEditorExampleLabel.color = new Color(textColour.r,textColour.g,textColour.b,aura.Opacity/100f);
            _auraEditorExampleLabel.fontSize = aura.TextSize;
            _auraEditorExampleLabel.alignment = aura.TextAlignment == "Left" ? TextAnchor.MiddleLeft :
                aura.TextAlignment == "Right" ? TextAnchor.MiddleRight : TextAnchor.MiddleCenter;
            _auraEditorExampleLabel.gameObject.SetActive(aura.Display != "Icon" ||
                (_auraEditorArtwork != null && !_auraEditorArtwork.gameObject.activeSelf) || aura.ShowLabel);
            _auraEditorExampleLabel.text = aura.LabelPattern == "%n" ? aura.Name :
                aura.LabelPattern.Replace("%n",aura.Name).Replace("%v",
                    Mathf.RoundToInt(_auraPreviewResourcePercent / 10f).ToString()).Replace("%m","10")
                    .Replace("%p",_auraPreviewResourcePercent + "%").Replace("%t","5.0");
            AnimateCustomAuraPreview();
        }

        private void AnimateCustomAuraPreview()
        {
            if (_auraEditorExample == null || _auraPreviewMotionGroup == null) return;
            CustomAuraDefinition aura = CustomAuraLibrary.Selected;
            if (aura == null || !_auraEditorExample.gameObject.activeSelf) return;
            float clock = Time.unscaledTime;
            if (aura.Trigger == "Custom Expression" && _auraEditorPreviewStatus != null &&
                clock >= _auraNextExpressionPreviewRead)
            {
                _auraNextExpressionPreviewRead = clock + .25f;
                float liveValue, liveMaximum;
                bool liveVisible;
                string liveError;
                if (ArcaneUiAuraExpression.TryRead(aura, out liveValue, out liveMaximum,
                    out liveVisible, out liveError))
                    _auraEditorPreviewStatus.text = "LIVE EXPRESSION  " + liveValue.ToString("0.##") +
                        (liveMaximum > 0f ? " / " + liveMaximum.ToString("0.##") : " (uncapped)") +
                        (liveVisible ? "  •  ACTIVE" : "  •  HIDDEN");
                else _auraEditorPreviewStatus.text = "EXPRESSION UNAVAILABLE: " + liveError;
            }
            CustomAuraAnimation.Pose pose = CustomAuraAnimation.Evaluate(
                aura, clock, clock - _auraPreviewStartedAt, true, false, _auraPreviewGlowAction);
            _auraEditorExample.localScale = Vector3.one * _auraPreviewBaseScale * pose.Scale;
            _auraEditorExample.anchoredPosition = _auraPreviewBasePosition + pose.Offset;
            _auraPreviewMotionGroup.alpha = pose.Alpha;
            float glowWidth = _auraEditorExample.rect.width;
            float glowHeight = _auraEditorExample.rect.height;
            bool glowLayoutChanged = _auraPreviewLastGlowWidth != glowWidth ||
                _auraPreviewLastGlowHeight != glowHeight || _auraPreviewLastGlowThickness != aura.GlowWidth;
            float glowOpacity = pose.GlowAlpha / Mathf.Max(.001f, pose.Alpha) * aura.Opacity / 100f;
            CustomAuraAnimation.DrawGlow(_auraPreviewGlows, aura,
                glowWidth, glowHeight, glowOpacity, glowLayoutChanged);
            if (glowOpacity > .001f)
            {
                _auraPreviewLastGlowWidth = glowWidth;
                _auraPreviewLastGlowHeight = glowHeight;
                _auraPreviewLastGlowThickness = aura.GlowWidth;
            }
        }

        private static CustomAurasArtSelection AuraArtworkSelection(string source)
        {
            if (source == "Matched Spell") return CustomAurasArtSelection.SpellOnly;
            if (source == "Trigger") return CustomAurasArtSelection.TriggerOnly;
            if (source == "None") return CustomAurasArtSelection.NoArtwork;
            return CustomAurasArtSelection.Auto;
        }

        private static Color PreviewBackground(CustomAuraDefinition aura)
        {
            Color result;
            if (!ColorUtility.TryParseHtmlString(aura.BackgroundColour, out result))
                result = new Color(.07f,.08f,.10f,1f);
            result.a *= aura.BackgroundOpacity / 100f * aura.Opacity / 100f;
            return result;
        }

        private void RenderAuraEditor()
        {
            if (_tab == "Display") _auraWorkspaceTab = "Display";
            else if (_tab == "Trigger") _auraWorkspaceTab = "Trigger";
            else if (_tab == "My Auras" && string.IsNullOrEmpty(_auraWorkspaceTab)) _auraWorkspaceTab = "Display";

            // Dormant saved trackers for classes outside the release-test roster
            // stay in the profile but are not selected in the active editor.
            if (CustomAuraLibrary.Selected == null)
                foreach (var candidate in CustomAuraLibrary.All)
                    if (Array.IndexOf(CustomAuraLibrary.Folders, candidate.Folder) >= 0)
                    {
                        CustomAuraLibrary.Select(candidate.Id);
                        break;
                    }

            var hero = ArcaneUiControls.Row(_content, "AuraManagerHero", 62f, Card);
            ArcaneUiControls.Label(hero, "Title", "AURA MANAGER", 16f, 8f, 430f, 26f, 16,
                Theme.Text, TextAnchor.MiddleLeft, FontStyle.Bold);
            ArcaneUiControls.Label(hero, "Subtitle",
                "Build custom icon, bar and text auras in one workspace. Saved auras remain visible while you edit them.",
                16f, 32f, 560f, 21f, 11, Muted);
            ArcaneUiControls.Button(hero, "CreateAura", "NEW AURA", 585f, 12f, 90f, 30f,
                Theme.Accent, Color.white, () =>
                {
                    CustomAuraLibrary.Create();
                    _auraWorkspaceTab = "Display";
                    RenderPage();
                }, 11);
            ArcaneUiControls.Button(hero, "DuplicateAura", "DUPLICATE", 682f, 12f, 90f, 30f,
                Theme.Panel, Theme.Text, () =>
                {
                    CustomAuraLibrary.Duplicate();
                    RenderPage();
                }, 11);

            var quickStart = ArcaneUiControls.Row(_content, "AuraQuickStart", 53f, Card);
            ArcaneUiControls.Label(quickStart, "Caption", "CLASS TEMPLATES", 12f, 3f, 190f, 23f,
                12, Theme.Text, TextAnchor.MiddleLeft, FontStyle.Bold);
            ArcaneUiControls.Label(quickStart, "Hint", string.IsNullOrEmpty(_auraTemplateFeedback)
                ? CustomAuraTemplates.ForFolder(_auraTemplateFolder).Length + " editable presets for " + _auraTemplateFolder
                : _auraTemplateFeedback,
                12f, 25f, 285f, 21f, 10, Muted);
            Button classButton = ArcaneUiControls.Button(quickStart, "TemplateClass",
                _auraTemplateFolder + "  ▼", 309f, 9f, 188f, 33f,
                Theme.Panel, Theme.Text, null, 11);
            classButton.onClick.AddListener(() => OpenChoicePopup(classButton.GetComponent<RectTransform>(),
                CustomAuraLibrary.Folders, _auraTemplateFolder, chosen =>
                {
                    _auraTemplateFolder = chosen;
                    _auraTemplateFeedback = "";
                    RenderPage();
                }));
            Button templateButton = ArcaneUiControls.Button(quickStart, "BrowseTemplates",
                "CHOOSE TEMPLATE  ▼", 507f, 9f, 272f, 33f,
                Theme.Accent, Color.white, null, 11);
            templateButton.onClick.AddListener(() =>
            {
                CustomAuraTemplate[] presets = CustomAuraTemplates.ForFolder(_auraTemplateFolder);
                string[] labels = new string[presets.Length];
                for (int i = 0; i < presets.Length; i++)
                    labels[i] = presets[i].Name + (presets[i].NeedsSetup ? " *" : "");
                OpenChoicePopup(templateButton.GetComponent<RectTransform>(), labels, "", selected =>
                {
                    for (int i = 0; i < presets.Length; i++)
                    {
                        string label = presets[i].Name + (presets[i].NeedsSetup ? " *" : "");
                        if (label != selected) continue;
                        CustomAuraDefinition created = CustomAuraLibrary.CreateTemplate(presets[i].Id);
                        _auraTemplateFeedback = created == null
                            ? "Capacity reached: 200 auras or 32 groups. Free space before creating a grouped template."
                            : presets[i].NeedsSetup ? "Template created. Set a spell name in Trigger to enable."
                            : presets[i].Id == "g_icontext"
                                ? "Icon + Label group created. Edit artwork and text separately under My Auras."
                            : CustomAuraTemplates.FindHudRecipe(presets[i].Id) != null
                                ? "Group created with 4 auras. Open Groups to reposition; edit each element in My Auras."
                                : presets[i].Description;
                        if (created != null)
                        {
                            _auraLibraryFilter = ""; // Always show the newly created record.
                            _collapsedAuraFolders.Remove(created.Folder);
                            _auraWorkspaceTab = "Display";
                        }
                        RenderPage();
                        break;
                    }
                });
            });

            var workspace = ArcaneUiControls.Row(_content, "AuraManagerWorkspace", 400f, Color.clear);
            var left = ArcaneUiControls.Panel(workspace, "AuraLibraryPanel", 0f, 0f, 226f, 390f,
                ArcaneUiControls.Multiply(Card, 1.03f), false);
            var center = ArcaneUiControls.Panel(workspace, "AuraEditorPanel", 236f, 0f, 556f, 390f,
                ArcaneUiControls.Multiply(Card, 1.03f), false);
            // The third preview column has been removed. Every module, including
            // Aura Manager, now shares the sole interactive right-hand preview.
            left.gameObject.AddComponent<RectMask2D>();
            center.gameObject.AddComponent<RectMask2D>();

            RenderAuraWorkspaceLibrary(left);
            RenderAuraWorkspaceEditor(center);
        }

        private void RenderAuraWorkspaceLibrary(RectTransform panel)
        {
            if (!_auraFoldersInitialized)
            {
                _auraFoldersInitialized = true;
                string initiallySelected = CustomAuraLibrary.Selected == null ? "General" :
                    CustomAuraLibrary.Selected.Folder;
                foreach (string folderName in CustomAuraLibrary.Folders)
                    if (folderName != initiallySelected && folderName != "General")
                        _collapsedAuraFolders.Add(folderName);
            }
            ArcaneUiControls.Label(panel, "Header", "MY AURAS", 14f, 10f, 160f, 22f, 13,
                Theme.Text, TextAnchor.MiddleLeft, FontStyle.Bold);
            ArcaneUiControls.Label(panel, "Subheader", CustomAuraLibrary.All.Count + " / " + CustomAuraLibrary.Maximum + " saved",
                14f, 31f, 170f, 18f, 10, Muted);
            var filter = ArcaneUiControls.Search(panel, "AuraSearch", 14f, 56f, 198f, 30f,
                "Search auras...", Theme.Panel, Theme.Text,
                value => { _auraLibraryFilter = value ?? string.Empty; ApplyAuraLibraryFilter(); });
            filter.SetTextWithoutNotify(_auraLibraryFilter ?? string.Empty);

            bool confirmDelete = CustomAuraLibrary.Selected != null &&
                _auraDeleteConfirmId == CustomAuraLibrary.SelectedId;
            ArcaneUiControls.Button(panel, "DeleteAura", confirmDelete ? "CONFIRM" : "DELETE",
                14f, 92f, 95f, 28f, confirmDelete ? new Color(.62f,.18f,.18f) : Theme.Panel,
                Theme.Text, () =>
                {
                    if (CustomAuraLibrary.Selected == null) return;
                    if (_auraDeleteConfirmId != CustomAuraLibrary.SelectedId)
                        _auraDeleteConfirmId = CustomAuraLibrary.SelectedId;
                    else { CustomAuraLibrary.DeleteSelected(); _auraDeleteConfirmId = null; }
                    RenderPage();
                }, 10);
            ArcaneUiControls.Button(panel, "LockAuraPositions",
                CustomAuraLibrary.EditPositions ? "LOCK HUD" : "UNLOCK HUD",
                115f, 92f, 97f, 28f,
                CustomAuraLibrary.EditPositions ? Theme.Accent : Theme.Panel,
                Color.white, () =>
                {
                    CustomAuraLibrary.TogglePositionEditing();
                    RenderPage();
                }, 10);

            var list = ArcaneUiControls.ScrollContent(panel, "AuraLibraryScroll", 10f, 130f, 206f, 249f,
                new Color(0f, 0f, 0f, 0f));
            _auraSearchList = list;
            foreach (string folder in CustomAuraLibrary.Folders)
            {
                int count = 0;
                foreach (CustomAuraDefinition definition in CustomAuraLibrary.All)
                    if (definition.Folder == folder) count++;
                var group = ArcaneUiControls.Row(list, "AuraFolder_" + folder, 40f, Theme.Panel);
                bool collapsed = _collapsedAuraFolders.Contains(folder);
                var folderButton = ArcaneUiControls.Button(group, "ToggleFolder",
                    (collapsed ? "+  " : "-  ") + folder + " (" + count + ")",
                    5f, 3f, 188f, 33f, Card, Theme.Text, () =>
                    {
                        if (!_collapsedAuraFolders.Add(folder)) _collapsedAuraFolders.Remove(folder);
                        RenderPage();
                    }, 11);
                Text folderLabel = folderButton.GetComponentInChildren<Text>();
                if (folderLabel != null) folderLabel.alignment = TextAnchor.MiddleLeft;

                foreach (CustomAuraDefinition aura in CustomAuraLibrary.All)
                {
                    if (aura.Folder != folder) continue;
                    bool selected = aura.Id == CustomAuraLibrary.SelectedId;
                    var row = ArcaneUiControls.Row(list, "AuraEntry_" + aura.Id, 60f,
                        selected ? Card : Theme.Panel);
                    if (selected) ArcaneUiControls.Panel(row, "Selected", 0f, 4f, 3f, 52f,
                        Theme.Accent, false);
                    ArcaneUiControls.Label(row, "Name", aura.Name, 12f, 5f, 138f, 22f, 12,
                        Theme.Text, TextAnchor.MiddleLeft, FontStyle.Bold);
                    ArcaneUiControls.Label(row, "Meta", aura.Display + " • " + aura.Trigger,
                        12f, 27f, 178f, 18f, 10, Muted);
                    ArcaneUiControls.Label(row, "State", aura.Enabled ? "Enabled" : "Disabled",
                        12f, 43f, 100f, 14f, 9, aura.Enabled ? Theme.Accent : Muted);
                    ArcaneUiControls.Button(row, "Select", selected ? "ACTIVE" : "EDIT",
                        136f, 15f, 57f, 28f, selected ? Theme.Accent : Theme.Border,
                        Color.white, () =>
                        {
                            _auraDeleteConfirmId = null;
                            CustomAuraLibrary.Select(aura.Id); RenderPage();
                        }, 10);
                }
            }
            var empty = ArcaneUiControls.Row(list, "NoSearchResults", 74f, Theme.Panel);
            ArcaneUiControls.Label(empty, "Text", "No auras match your search.",
                12f, 12f, 174f, 48f, 11, Muted, TextAnchor.UpperLeft);
            ApplyAuraLibraryFilter();
        }

        private void ApplyAuraLibraryFilter()
        {
            if (_auraSearchList == null) return;
            string filterText = (_auraLibraryFilter ?? string.Empty).Trim();
            int totalMatches = 0;
            foreach (string folder in CustomAuraLibrary.Folders)
            {
                int folderMatches = 0;
                foreach (CustomAuraDefinition aura in CustomAuraLibrary.All)
                {
                    if (aura.Folder != folder) continue;
                    Transform entry = _auraSearchList.Find("AuraEntry_" + aura.Id);
                    if (entry == null) continue;
                    string hay = aura.Name + " " + aura.Display + " " + aura.Trigger + " " +
                        aura.MatchName + " " + aura.Folder;
                    bool matches = string.IsNullOrEmpty(filterText) ||
                        hay.IndexOf(filterText, StringComparison.OrdinalIgnoreCase) >= 0;
                    if (matches) { totalMatches++; folderMatches++; }
                    // Searching temporarily expands folders without changing saved UI state.
                    bool folded = _collapsedAuraFolders.Contains(folder) &&
                        string.IsNullOrEmpty(filterText);
                    entry.gameObject.SetActive(matches && !folded);
                }
                Transform header = _auraSearchList.Find("AuraFolder_" + folder);
                if (header != null) header.gameObject.SetActive(string.IsNullOrEmpty(filterText) || folderMatches > 0);
            }
            Transform empty = _auraSearchList.Find("NoSearchResults");
            if (empty != null)
            {
                Text label = empty.GetComponentInChildren<Text>();
                if (label != null) label.text = CustomAuraLibrary.All.Count == 0
                    ? "No saved auras. Click NEW AURA to create your first display."
                    : "No auras match your search.";
                empty.gameObject.SetActive(totalMatches == 0);
            }
            ArcaneUiControls.RefreshScrollLayout(_auraSearchList);
        }

        private void RenderAuraWorkspaceEditor(RectTransform panel)
        {
            CustomAuraDefinition aura = CustomAuraLibrary.Selected;
            ArcaneUiControls.Label(panel, "Header", aura == null ? "EDITOR" : aura.Name, 14f, 10f, 230f, 22f, 13,
                Theme.Text, TextAnchor.MiddleLeft, FontStyle.Bold);
            ArcaneUiControls.Label(panel, "Subheader", aura == null ? "Select an aura to begin editing." :
                aura.Display + " • " + aura.Trigger, 14f, 31f, 280f, 18f, 10, Muted);

            string[] tabs = { "Display", "Trigger", "Conditions", "Load",
                "Animations", "Groups", "Layers", "Profiles" };
            for (int i = 0; i < tabs.Length; i++)
            {
                string current = tabs[i];
                bool active = _auraWorkspaceTab == current;
                bool implemented = true;
                // Two full-width rows: labels never wrap into a neighbouring tab.
                float x = 14f + (i % 4) * 128f;
                float y = i < 4 ? 58f : 90f;
                float width = 122f;
                ArcaneUiControls.Button(panel, "AuraTab_" + current,
                    current, x, y, width, 28f,
                    active ? Theme.Accent : Theme.Panel,
                    active ? Color.white : (implemented ? Theme.Text : Muted),
                    () => { _auraWorkspaceTab = current; RenderPage(); }, 11);
            }

            var scroll = ArcaneUiControls.ScrollContent(panel, "AuraEditorScroll", 10f, 132f, 330f, 244f,
                new Color(0f, 0f, 0f, 0f));
            if (aura == null && _auraWorkspaceTab != "Profiles")
            {
                AuraPanelInfo(scroll, "No aura selected. Create a new aura or choose one from the library.");
                ArcaneUiControls.RefreshScrollLayout(scroll, true);
                return;
            }

            if (_auraWorkspaceTab == "Display")
            {
                AuraPanelSection(scroll, "Display", "Visual presentation and placement.");
                AuraPanelTextInput(scroll, "Display Name", aura.Name, "Name your display",
                    value => { CustomAuraLibrary.Change(d => d.Name = value); UpdateCustomAuraPreview(); });
                AuraPanelDropdown(scroll, "Class Folder", "Organise this aura within My Auras.",
                    aura.Folder, CustomAuraLibrary.Folders, value =>
                    { CustomAuraLibrary.Change(d => d.Folder = value);
                        _collapsedAuraFolders.Remove(value); RenderPage(); });
                AuraPanelToggle(scroll, "Enable This Aura", "Show the aura in gameplay when its trigger matches.",
                    () => aura.Enabled, value => { CustomAuraLibrary.Change(d => d.Enabled = value); UpdateCustomAuraPreview(); RenderPage(); });
                AuraPanelDropdown(scroll, "Display Type", "Choose a fully custom visual object.", aura.Display,
                    new[] { "Icon", "Progress Bar", "Text", "Shape", "Charge Segments" },
                    value => { CustomAuraLibrary.SetDisplay(value); RenderPage(); });
                if (aura.Display == "Icon")
                {
                    AuraPanelSection(scroll, "Spell Artwork", "Use game-registered spell icons, even when the spell is inactive.");
                    AuraPanelDropdown(scroll, "Artwork Source", "Automatic, chosen spell, active trigger or no artwork.",
                        aura.ArtSource, CustomAuraLibrary.ArtSources,
                        value => { CustomAuraLibrary.Change(d => d.ArtSource = value); UpdateCustomAuraPreview(); RenderPage(); });
                    if (aura.ArtSource == "Automatic" || aura.ArtSource == "Matched Spell")
                    {
                        AuraPanelSpellPicker(scroll, aura);
                        AuraPanelTextInput(scroll, "Manual Artwork Override (Advanced)", aura.ArtSpell,
                            "Exact registered spell name or ID",
                            value => { CustomAuraLibrary.Change(d => d.ArtSpell = value); UpdateCustomAuraPreview(); });
                        string name = ArcaneUiAuraSpellArtwork.ResolvedName(
                            !string.IsNullOrWhiteSpace(aura.ArtSpell) ? aura.ArtSpell :
                            (!string.IsNullOrWhiteSpace(aura.MatchName) ? aura.MatchName : aura.Name));
                        bool hasArtwork = ArcaneUiAuraSpellArtwork.Icon(
                            !string.IsNullOrWhiteSpace(aura.ArtSpell) ? aura.ArtSpell :
                            (!string.IsNullOrWhiteSpace(aura.MatchName) ? aura.MatchName : aura.Name)) != null;
                        AuraPanelInfo(scroll, name == null ?
                            "No unique matching spell in the live SpellDB yet. Check its name or browse registered spells after loading a character." :
                            hasArtwork ? "Spell resolved: " + name + ". Artwork is available even while the spell is inactive." :
                            "Spell resolved: " + name + ", but the game has not exposed a usable Sprite icon for it yet.");
                    }
                    AuraPanelSlider(scroll, "Icon Padding", 0f, 24f, () => aura.IconPadding,
                        value => { CustomAuraLibrary.Change(d => d.IconPadding = Mathf.RoundToInt(value)); UpdateCustomAuraPreview(); }, " px");
                    AuraPanelToggle(scroll, "Show Label On Icon", "Draw your label above the spell art.",
                        () => aura.ShowLabel,
                        value => { CustomAuraLibrary.Change(d => d.ShowLabel = value); UpdateCustomAuraPreview(); });
                }
                if (aura.Display == "Shape" || aura.Display == "Charge Segments")
                {
                    AuraPanelDropdown(scroll, "Shape Style", "Vector geometry (no texture assets required).",
                        aura.Shape, CustomAuraLibrary.ShapeStyles,
                        value => { CustomAuraLibrary.Change(d => d.Shape = value); UpdateCustomAuraPreview(); RenderPage(); });
                    if (aura.Display == "Charge Segments")
                    {
                        AuraPanelSlider(scroll, "Charge Segments", 1f, 20f, () => aura.SegmentCount,
                            value => { CustomAuraLibrary.Change(d => d.SegmentCount = Mathf.RoundToInt(value)); UpdateCustomAuraPreview(); }, "");
                        AuraPanelSlider(scroll, "Segment Spacing", 0f, 16f, () => aura.SegmentSpacing,
                            value => { CustomAuraLibrary.Change(d => d.SegmentSpacing = Mathf.RoundToInt(value)); UpdateCustomAuraPreview(); }, " px");
                        AuraPanelSlider(scroll, "Segments Per Row", 1f, 20f, () => aura.SegmentsPerRow,
                            value => { CustomAuraLibrary.Change(d => d.SegmentsPerRow = Mathf.RoundToInt(value)); UpdateCustomAuraPreview(); }, "");
                        AuraPanelColour(scroll, "Empty Charge Colour", "Choose the appearance of unfilled charges.",
                            () => aura.InactiveColour,
                            value => { CustomAuraLibrary.Change(d => d.InactiveColour = value); UpdateCustomAuraPreview(); });
                        AuraPanelToggle(scroll, "Reverse Charge Order", "Fill the segments from right to left.",
                            () => aura.ReverseFill, value => { CustomAuraLibrary.Change(d => d.ReverseFill = value); UpdateCustomAuraPreview(); });
                    }
                    AuraPanelToggle(scroll, "Show Resource Value", "Draw the current charge count above the shapes.",
                        () => aura.ShowValue, value => { CustomAuraLibrary.Change(d => d.ShowValue = value); UpdateCustomAuraPreview(); });
                }
                AuraPanelColour(scroll, "Aura Accent Colour", "Pick the main display colour.",
                    () => aura.Colour, value => { CustomAuraLibrary.Change(d => d.Colour = value); UpdateCustomAuraPreview(); });
                AuraPanelSection(scroll, "Opacity and Background", "Independent transparency and background tint.");
                AuraPanelSlider(scroll, "Aura Opacity", 0f, 100f, () => aura.Opacity,
                    value => { CustomAuraLibrary.Change(d => d.Opacity = Mathf.RoundToInt(value)); UpdateCustomAuraPreview(); }, "%");
                AuraPanelColour(scroll, "Background Colour", "Panel background behind icons, bars or text.",
                    () => aura.BackgroundColour,
                    value => { CustomAuraLibrary.Change(d => d.BackgroundColour = value); UpdateCustomAuraPreview(); });
                AuraPanelSlider(scroll, "Background Opacity", 0f, 100f, () => aura.BackgroundOpacity,
                    value => { CustomAuraLibrary.Change(d => d.BackgroundOpacity = Mathf.RoundToInt(value)); UpdateCustomAuraPreview(); }, "%");
                AuraPanelSection(scroll, "Borders and Typography", "Separate outlines, colours and text formatting.");
                AuraPanelSlider(scroll, "Border Thickness", 0f, 12f, () => aura.BorderSize,
                    value => { CustomAuraLibrary.Change(d => d.BorderSize = Mathf.RoundToInt(value)); UpdateCustomAuraPreview(); }, " px");
                AuraPanelColour(scroll, "Border Colour", "Colour of the four independent frame edges.",
                    () => aura.BorderColour,
                    value => { CustomAuraLibrary.Change(d => d.BorderColour = value); UpdateCustomAuraPreview(); });
                AuraPanelColour(scroll, "Aura Text Colour", "Label and timer text colour.",
                    () => aura.TextColour,
                    value => { CustomAuraLibrary.Change(d => d.TextColour = value); UpdateCustomAuraPreview(); });
                AuraPanelSlider(scroll, "Label Text Size", 8f, 36f, () => aura.TextSize,
                    value => { CustomAuraLibrary.Change(d => d.TextSize = Mathf.RoundToInt(value)); UpdateCustomAuraPreview(); }, " px");
                AuraPanelDropdown(scroll, "Text Alignment", "Left / Centre / Right",
                    aura.TextAlignment, CustomAuraLibrary.TextAlignments,
                    value => { CustomAuraLibrary.Change(d => d.TextAlignment = value); UpdateCustomAuraPreview(); RenderPage(); });
                AuraPanelTextInput(scroll, "Text Format", aura.LabelPattern, "%n / %v / %m / %p / %t",
                    value => { CustomAuraLibrary.Change(d => d.LabelPattern = value); UpdateCustomAuraPreview(); });
                AuraPanelInfo(scroll, "Tokens: %n = name, %v = current value, %m = maximum, %p = percent, %t = remaining time. Example: Heat %v/%m");
                if (aura.Display == "Progress Bar")
                    AuraPanelDropdown(scroll, "Progress Fill Direction", "Four-way progress bar filling.",
                        aura.FillDirection, CustomAuraLibrary.FillDirections,
                        value => { CustomAuraLibrary.Change(d => d.FillDirection = value); UpdateCustomAuraPreview(); RenderPage(); });
                AuraPanelSlider(scroll, "Timer Text Size", 8f, 32f, () => aura.TimerSize,
                    value => { CustomAuraLibrary.Change(d => d.TimerSize = Mathf.RoundToInt(value)); UpdateCustomAuraPreview(); }, " px");
                AuraPanelToggle(scroll, "Display Remaining Time", "Show native remaining duration when available.",
                    () => aura.ShowTimer, value => { CustomAuraLibrary.Change(d => d.ShowTimer = value); UpdateCustomAuraPreview(); RenderPage(); });
                AuraPanelSlider(scroll, "Aura Width", 32f, 480f, () => aura.Width,
                    value => { CustomAuraLibrary.Change(d => d.Width = Mathf.RoundToInt(value)); UpdateCustomAuraPreview(); }, " px");
                AuraPanelSlider(scroll, "Aura Height", 22f, 140f, () => aura.Height,
                    value => { CustomAuraLibrary.Change(d => d.Height = Mathf.RoundToInt(value)); UpdateCustomAuraPreview(); }, " px");
                AuraPanelSlider(scroll, "Progress Smoothing", 0f, 1000f,
                    () => aura.ProgressSmoothingMs,
                    value => CustomAuraLibrary.Change(d => d.ProgressSmoothingMs = Mathf.RoundToInt(value)), " ms");
                AuraPanelInfo(scroll, "0 ms keeps native instant updates. Larger values ease changes to progress bars, charge segments and radial gauges without delaying trigger or condition evaluation.");
                AuraPanelSection(scroll, "Position", "Set exact coordinates or unlock HUD dragging.");
                AuraPanelSlider(scroll, "Aura X Position", 0f, 1820f, () => aura.X,
                    value => { CustomAuraLibrary.Change(d => d.X = Mathf.RoundToInt(value)); UpdateCustomAuraPreview(); }, " px");
                AuraPanelSlider(scroll, "Aura Y Position", 0f, 980f, () => aura.Y,
                    value => { CustomAuraLibrary.Change(d => d.Y = Mathf.RoundToInt(value)); UpdateCustomAuraPreview(); }, " px");
                AuraPanelAction(scroll, "Unlock Custom Aura Dragging",
                    CustomAuraLibrary.EditPositions
                        ? "Drag mode is active. Close the settings window and move displays freely. Press Esc to stop."
                        : "Temporarily unlock your custom auras so you can drag them on the HUD.",
                    CustomAuraLibrary.EditPositions ? "LOCK" : "UNLOCK",
                    () => { CustomAuraLibrary.TogglePositionEditing(); RenderPage(); });
            }
            else if (_auraWorkspaceTab == "Trigger")
            {
                AuraPanelSection(scroll, "Trigger", "Read-only native triggers. No scripts or gameplay overrides.");
                AuraPanelDropdown(scroll, "Trigger Type", "Choose when this aura becomes visible.", aura.Trigger,
                    new[] { "Always", "Player Buff", "Player Debuff", "Target Buff", "Target Debuff",
                        "Cooldown Active", "Cooldown Ready", "Emberweaver Heat",
                        "Emberweaver Flashfire", "Emberweaver Wildfire Stored", "Emberweaver Combust Projected", "Player Health", "Player Mana", "Target Health", "Target Mana", "Player Absorb", "Custom Expression" },
                    value => { CustomAuraLibrary.SetTrigger(value); _auraExpressionFeedback = "Choose CHECK SYNTAX or TEST LIVE."; RenderPage(); });
                if (aura.Trigger == "Custom Expression")
                {
                    AuraPanelSection(scroll, "Custom Tracking Expressions", "Read-only calculations, no native code execution.");
                    AuraPanelInfo(scroll, "Operators: + - * / % >= <= == != > < && || ! ( ). Functions: min, max, clamp, abs, round, floor, ceil.");
                    AuraPanelInfo(scroll, "Player: player_hp, player_hp_max, player_hp_pct, player_mana, player_mana_max, player_mana_pct, absorb.");
                    AuraPanelInfo(scroll, "Target: target_exists, target_hp, target_hp_max, target_hp_pct, target_mana, target_mana_max, target_mana_pct.");
                    AuraPanelInfo(scroll, "Emberweaver: heat, flashfire, wildfire, combust. Missing target or FOTF data hides the aura safely.");
                    AuraPanelTextInput(scroll, "Expression Value", aura.ExpressionValue,
                        "e.g. wildfire or heat * 10", value =>
                        { CustomAuraLibrary.Change(d => d.ExpressionValue = value); _auraExpressionFeedback = "Expression updated. Test it below."; });
                    AuraPanelTextInput(scroll, "Expression Maximum (Optional)", aura.ExpressionMaximum,
                        "e.g. 100 or player_hp_max", value =>
                        { CustomAuraLibrary.Change(d => d.ExpressionMaximum = value); _auraExpressionFeedback = "Expression updated. Test it below."; });
                    AuraPanelTextInput(scroll, "Expression Condition (Optional)", aura.ExpressionCondition,
                        "e.g. heat >= 8 && flashfire > 0", value =>
                        { CustomAuraLibrary.Change(d => d.ExpressionCondition = value); _auraExpressionFeedback = "Expression updated. Test it below."; });
                    AuraPanelAction(scroll, "Validate Expression", "Check identifiers, formula syntax and function arguments.",
                        "CHECK", () => { string feedback;
                            bool ok = ArcaneUiAuraExpression.Validate(aura, out feedback);
                            _auraExpressionFeedback = (ok ? "PASS: " : "ERROR: ") + feedback; RenderPage(); });
                    AuraPanelAction(scroll, "Test Live Read", "Read your actual player/target/FOTF values without casting anything.",
                        "TEST LIVE", () => { float value, maximum; bool active; string error;
                            if (ArcaneUiAuraExpression.TryRead(aura, out value, out maximum, out active, out error))
                                _auraExpressionFeedback = "LIVE: " + value.ToString("0.##") +
                                    (maximum > 0f ? " / " + maximum.ToString("0.##") : " (uncapped)") +
                                    " | display " + (active ? "ACTIVE" : "HIDDEN");
                            else _auraExpressionFeedback = "UNAVAILABLE: " + error;
                            RenderPage(); });
                    AuraPanelInfo(scroll, _auraExpressionFeedback);
                    AuraPanelToggle(scroll, "Display At Zero", "Used only when no explicit condition expression is set.",
                        () => aura.ShowWhenEmpty, value => CustomAuraLibrary.Change(d => d.ShowWhenEmpty = value));
                    AuraPanelSlider(scroll, "Minimum Numeric Value", 0f, 100000f, () => aura.MinimumValue,
                        value => CustomAuraLibrary.Change(d => d.MinimumValue = Mathf.RoundToInt(value)), "");
                    AuraPanelInfo(scroll, "For uncapped values, leave Maximum blank and use a Text display with %v. Maximum only affects percent/fill; every condition and value is read-only.");
                }
                else if (ArcaneUiAuraResourceReader.IsResource(aura.Trigger))
                {
                    AuraPanelInfo(scroll, "Uses live read-only resource values. Emberweaver requires FOTF and the matching player class.");
                    if (aura.Trigger == "Emberweaver Wildfire Stored" || aura.Trigger == "Emberweaver Combust Projected")
                        AuraPanelInfo(scroll, "Reads FOTF's live Wildfire state on your CURRENT target. It is an uncapped damage number, not a timer, % bar, or charge pool. The display clears on target change / Wildfire expiry. Minimum Resource Value is an absolute damage threshold.");
                    if (aura.Trigger == "Emberweaver Heat" || aura.Trigger == "Emberweaver Flashfire")
                    {
                        AuraPanelInfo(scroll, ArcaneUiAuraResourceReader.DescribeEmberweaver(aura.Trigger));
                        AuraPanelInfo(scroll, "Heat builds only after learning the Heat passive skill in FOTF. At zero charges, enable Show With Zero Charges and set Minimum Resource Value to 0.");
                        AuraPanelAction(scroll, "Check Live Heat Bridge",
                            "Force a log even when FOTF returns zero or the aura is filtered out.", "REFRESH",
                            () => { ArcaneUiAuraResourceReader.ForceHeatDiagnostic(); RenderPage(); });
                        AuraPanelInfo(scroll, "Aura " + (aura.Enabled ? "enabled" : "disabled") +
                            " | Class filter: " + (aura.LoadClass ?? "General") +
                            " | Visible at zero: " + (aura.ShowWhenEmpty ? "yes" : "no") +
                            " | Minimum charges: " + aura.MinimumValue);
                    }
                    bool wildfireNumber = aura.Trigger == "Emberweaver Wildfire Stored" ||
                        aura.Trigger == "Emberweaver Combust Projected";
                    AuraPanelToggle(scroll, wildfireNumber ? "Show With Zero Damage" : "Show With Zero Charges",
                        "Keep the resource display visible at zero while a live provider is available.",
                        () => aura.ShowWhenEmpty, value => { CustomAuraLibrary.Change(d => d.ShowWhenEmpty = value); });
                    AuraPanelSlider(scroll, wildfireNumber ? "Minimum Stored Damage" : "Minimum Resource Value",
                        0f, wildfireNumber ? 100000f : 10f,
                        () => aura.MinimumValue,
                        value => { CustomAuraLibrary.Change(d => d.MinimumValue = Mathf.RoundToInt(value)); }, "");
                }
                else if (aura.Trigger != "Always")
                {
                    AuraPanelSpellPicker(scroll, aura);
                    AuraPanelTextInput(scroll, "Manual Match Override (Advanced)", aura.MatchName,
                        "Exact spell/effect name or ID",
                        value => { CustomAuraLibrary.Change(d => d.MatchName = value); UpdateCustomAuraPreview(); });
                    AuraPanelInfo(scroll,
                        "Pick a registered spell above (Current Class by default), or use the manual override. Buff/debuff triggers read actual status effects; cooldown triggers require a matching ability on a hotbar. Art can resolve independently.");
                }
                else AuraPanelInfo(scroll, "An enabled aura using Always stays visible whenever ArcaneUI gameplay UI is available.");

                if (aura.Trigger.EndsWith("Buff") || aura.Trigger.EndsWith("Debuff"))
                    AuraPanelInfo(scroll, "Duration bars use the best native timing information available. The progress fraction is based on the strongest observed duration, not a guaranteed spellbook duration.");
                else if (aura.Trigger.StartsWith("Cooldown"))
                    AuraPanelInfo(scroll, "Instant cooldown resets clear the display as soon as a matching native slot reports ready, including duplicate extra-bar slots.");
                AuraPanelSection(scroll, "Multiple Triggers", "Combine Trigger 1 with up to three additional rules.");
                AuraPanelDropdown(scroll, "Trigger Logic", "AND requires every trigger. OR requires any trigger.",
                    aura.TriggerLogic, CustomAuraLibrary.TriggerLogicOptions,
                    value => { CustomAuraLibrary.Change(d => d.TriggerLogic = value); RenderPage(); });
                for (int i = 0; i < aura.AdditionalTriggers.Count; ++i)
                {
                    int index = i;
                    CustomAuraTriggerRule rule = aura.AdditionalTriggers[i];
                    AuraPanelSection(scroll, "Trigger " + (i + 2), "Optional, read-only game-state rule.");
                    AuraPanelDropdown(scroll, "Type " + (i + 2), "Choose the secondary tracked event.",
                        rule.Type, AuraTriggerOptions,
                        value => { CustomAuraLibrary.ChangeTriggerRule(index, r => r.Type = value); RenderPage(); });
                    if (!ArcaneUiAuraResourceReader.IsResource(rule.Type) && rule.Type != "Always")
                        AuraPanelTextInput(scroll, "Match " + (i + 2), rule.MatchName,
                            "Spell or native ability ID", value => CustomAuraLibrary.ChangeTriggerRule(index,
                                r => r.MatchName = value));
                    if (ArcaneUiAuraResourceReader.IsResource(rule.Type))
                    {
                        AuraPanelSlider(scroll, "Minimum " + (i + 2), 0f, 1000f,
                            () => rule.MinimumValue, value => CustomAuraLibrary.ChangeTriggerRule(index,
                                r => r.MinimumValue = Mathf.RoundToInt(value)), "");
                        AuraPanelToggle(scroll, "Show Zero " + (i + 2),
                            "Allow resource-zero to count as active when it meets the threshold.",
                            () => rule.ShowWhenEmpty,
                            value => CustomAuraLibrary.ChangeTriggerRule(index, r => r.ShowWhenEmpty = value));
                    }
                    AuraPanelToggle(scroll, "NOT " + (i + 2),
                        "Match when this trigger is inactive; unavailable data never matches.",
                        () => rule.Not, value => CustomAuraLibrary.ChangeTriggerRule(index, r => r.Not = value));
                    AuraPanelAction(scroll, "Remove Trigger " + (i + 2), "Remove this secondary rule.",
                        "REMOVE", () => { CustomAuraLibrary.RemoveTriggerRule(index); RenderPage(); });
                }
                if (aura.AdditionalTriggers.Count < CustomAuraLibrary.MaxExtraTriggers)
                    AuraPanelAction(scroll, "Add Trigger", "Add another AND / OR / NOT rule.",
                        "ADD", () => { CustomAuraLibrary.AddTriggerRule(); RenderPage(); });
            }
            else if (_auraWorkspaceTab == "Conditions")
            {
                AuraPanelSection(scroll, "Conditional Appearance", "First matching rule wins. Rules do not alter spells or combat.");
                AuraPanelInfo(scroll, "Change an aura's colour, opacity, scale or glow at thresholds. Test the result using the sample resource slider; no spells are cast.");
                AuraPanelSlider(scroll, "Preview Resource Percentage", 0f, 100f,
                    () => _auraPreviewResourcePercent, value =>
                    { _auraPreviewResourcePercent = Mathf.RoundToInt(value);
                        UpdateCustomAuraPreview(); }, "%");
                for (int i = 0; i < aura.Conditions.Count; ++i)
                {
                    int index = i;
                    CustomAuraConditionRule rule = aura.Conditions[i];
                    AuraPanelSection(scroll, "Condition " + (i + 1), "Checked from top to bottom; first match wins.");
                    AuraPanelDropdown(scroll, "Metric " + (i + 1), "Value percentage, resource, duration or stacks.",
                        rule.Metric, CustomAuraLibrary.ConditionMetrics,
                        value => { CustomAuraLibrary.ChangeCondition(index, r => r.Metric = value); RenderPage(); });
                    AuraPanelDropdown(scroll, "Comparison " + (i + 1), "Choose how to compare against the threshold.",
                        rule.Comparison, CustomAuraLibrary.Comparisons,
                        value => { CustomAuraLibrary.ChangeCondition(index, r => r.Comparison = value); RenderPage(); });
                    AuraPanelSlider(scroll, "Threshold " + (i + 1), 0f,
                        rule.Metric == "Value Percentage" ? 100f : 1000f,
                        () => rule.Threshold, value => CustomAuraLibrary.ChangeCondition(index,
                            r => r.Threshold = Mathf.RoundToInt(value)), "");
                    AuraPanelToggle(scroll, "Change Colour " + (i + 1), "Use a different accent colour when matched.",
                        () => rule.ChangeColour,
                        value => { CustomAuraLibrary.ChangeCondition(index, r => r.ChangeColour = value); RenderPage(); });
                    if (rule.ChangeColour)
                        AuraPanelColour(scroll, "Condition Colour " + (i + 1), "Conditional shape, bar or icon accent.",
                            () => rule.Colour, value => CustomAuraLibrary.ChangeCondition(index, r => r.Colour = value));
                    AuraPanelToggle(scroll, "Change Opacity " + (i + 1), "Override the aura's overall opacity.",
                        () => rule.ChangeOpacity,
                        value => { CustomAuraLibrary.ChangeCondition(index, r => r.ChangeOpacity = value); RenderPage(); });
                    if (rule.ChangeOpacity)
                        AuraPanelSlider(scroll, "Condition Opacity " + (i + 1), 0f, 100f,
                            () => rule.Opacity, value => CustomAuraLibrary.ChangeCondition(index,
                                r => r.Opacity = Mathf.RoundToInt(value)), "%");
                    AuraPanelToggle(scroll, "Change Scale " + (i + 1), "Resize the whole aura when the rule matches.",
                        () => rule.ChangeScale,
                        value => { CustomAuraLibrary.ChangeCondition(index, r => r.ChangeScale = value); RenderPage(); });
                    if (rule.ChangeScale)
                        AuraPanelSlider(scroll, "Condition Scale " + (i + 1), 25f, 200f,
                            () => rule.Scale, value => CustomAuraLibrary.ChangeCondition(index,
                                r => r.Scale = Mathf.RoundToInt(value)), "%");
                    AuraPanelDropdown(scroll, "Glow Reaction " + (i + 1),
                        "Override aura glow while this condition matches.",
                        rule.GlowAction, CustomAuraLibrary.GlowActions,
                        value => { CustomAuraLibrary.ChangeCondition(index, r => r.GlowAction = value); RenderPage(); });
                    AuraPanelAction(scroll, "Remove Condition " + (i + 1), "Remove this conditional visual rule.",
                        "REMOVE", () => { CustomAuraLibrary.RemoveCondition(index); RenderPage(); });
                }
                if (aura.Conditions.Count < CustomAuraLibrary.MaxConditions)
                    AuraPanelAction(scroll, "Add Condition", "Create a new conditional appearance rule.",
                        "ADD", () => { CustomAuraLibrary.AddCondition(); RenderPage(); });
            }
            else if (_auraWorkspaceTab == "Load")
            {
                AuraPanelSection(scroll, "Load Rules", "Restrict when this aura is eligible to appear.");
                AuraPanelInfo(scroll, "All non-default load filters must match. If the requested combat-state data is unavailable, the aura stays hidden rather than guessing.");
                AuraPanelDropdown(scroll, "Class Filter", "General means any class, including future classes.",
                    aura.LoadClass, CustomAuraLibrary.Folders,
                    value => { CustomAuraLibrary.Change(d => d.LoadClass = value); RenderPage(); });
                AuraPanelDropdown(scroll, "Combat State", "Require combat, require peace, or leave unrestricted.",
                    aura.LoadCombat, CustomAuraLibrary.LoadCombatOptions,
                    value => { CustomAuraLibrary.Change(d => d.LoadCombat = value); RenderPage(); });
                AuraPanelDropdown(scroll, "Group State", "Restrict to solo, party or active raid membership.",
                    aura.LoadGroup, CustomAuraLibrary.LoadGroupOptions,
                    value => { CustomAuraLibrary.Change(d => d.LoadGroup = value); RenderPage(); });
                AuraPanelToggle(scroll, "Require Selected Target", "Only display while a native target is selected.",
                    () => aura.LoadRequiresTarget,
                    value => CustomAuraLibrary.Change(d => d.LoadRequiresTarget = value));
                AuraPanelTextInput(scroll, "Zone Scene", aura.LoadZone,
                    "Exact Unity scene name (blank = any)",
                    value => CustomAuraLibrary.Change(d => d.LoadZone = value));
                AuraPanelTextInput(scroll, "Character Name", aura.LoadCharacter,
                    "Exact player name (blank = any)",
                    value => CustomAuraLibrary.Change(d => d.LoadCharacter = value));
            }
            else if (_auraWorkspaceTab == "Groups")
            {
                AuraPanelSection(scroll, "Dynamic Aura Groups", "Parent containers automatically arrange their member auras.");
                AuraPanelInfo(scroll, "Create up to 32 groups. HUD Panel uses slots 0-3 for icon, readout, gauge and alert. Unlock HUD dragging to move a group by its title strip.");
                string[] groups = AuraGroupLabels();
                if (CustomAuraLibrary.Groups.Count > 0 &&
                    CustomAuraLibrary.FindGroup(_selectedAuraGroupId) == null)
                    _selectedAuraGroupId = CustomAuraLibrary.Groups[0].Id;
                string selectedGroup = AuraGroupLabel(_selectedAuraGroupId);
                AuraPanelDropdown(scroll, "Edit Group", "Select the parent group to configure.",
                    selectedGroup, groups, name =>
                    {
                        _selectedAuraGroupId = AuraGroupId(name); RenderPage();
                    });
                AuraPanelAction(scroll, "New Dynamic Group", "Creates an independent draggable parent container.",
                    "CREATE", () =>
                    {
                        var created = CustomAuraLibrary.CreateGroup();
                        if (created != null) _selectedAuraGroupId = created.Id;
                        RenderPage();
                    });
                var group = CustomAuraLibrary.FindGroup(_selectedAuraGroupId);
                if (group != null)
                {
                    string id = group.Id;
                    AuraPanelTextInput(scroll, "Group Name", group.Name, "Group name",
                        value => CustomAuraLibrary.ChangeGroup(id, g => g.Name = value));
                    AuraPanelToggle(scroll, "Group Enabled", "Hide/show the entire group together.",
                        () => group.Enabled, value => CustomAuraLibrary.ChangeGroup(id,g=>g.Enabled=value));
                    AuraPanelDropdown(scroll, "Group Layout", "Choose a simple row, a grid, stacked cards or a 4-slot HUD panel.",
                        group.Layout, CustomAuraLibrary.GroupLayouts,
                        value => { CustomAuraLibrary.ChangeGroup(id,g=>g.Layout=value); RenderPage(); });
                    AuraPanelDropdown(scroll, "Sorting", "Manual order, alphabetic or longest remaining duration.",
                        group.Order, CustomAuraLibrary.GroupOrders,
                        value => { CustomAuraLibrary.ChangeGroup(id,g=>g.Order=value); RenderPage(); });
                    AuraPanelToggle(scroll, "Reverse Order", "Reverse the chosen group ordering.",
                        () => group.Reverse, value => CustomAuraLibrary.ChangeGroup(id,g=>g.Reverse=value));
                    AuraPanelToggle(scroll, "Collapse Inactive", "Inactive auras don't occupy layout slots.",
                        () => group.HideInactive, value => CustomAuraLibrary.ChangeGroup(id,g=>g.HideInactive=value));
                    if (group.Layout == "Grid")
                        AuraPanelSlider(scroll, "Group Columns", 1f, 12f, () => group.Columns,
                            value => CustomAuraLibrary.ChangeGroup(id,g=>g.Columns=Mathf.RoundToInt(value)), "");
                    AuraPanelSlider(scroll, "Group Spacing", 0f, 48f, () => group.Spacing,
                        value => CustomAuraLibrary.ChangeGroup(id,g=>g.Spacing=Mathf.RoundToInt(value)), " px");
                    AuraPanelSlider(scroll, "Parent Scale", 25f, 200f, () => group.Scale,
                        value => CustomAuraLibrary.ChangeGroup(id,g=>g.Scale=Mathf.RoundToInt(value)), "%");
                    AuraPanelSlider(scroll, "Group X", 0f, 1820f, () => group.X,
                        value => CustomAuraLibrary.ChangeGroup(id,g=>g.X=Mathf.RoundToInt(value)), "");
                    AuraPanelSlider(scroll, "Group Y", 0f, 980f, () => group.Y,
                        value => CustomAuraLibrary.ChangeGroup(id,g=>g.Y=Mathf.RoundToInt(value)), "");
                    AuraPanelSection(scroll, "Placement & Scaling", "Group dragging can snap to a configurable screen grid.");
                    AuraPanelToggle(scroll, "Snap Group to Grid", "Snap when moving a group or pressing Snap Now.",
                        () => group.SnapToGrid, value => CustomAuraLibrary.ChangeGroup(id,g=>g.SnapToGrid=value));
                    AuraPanelSlider(scroll, "Grid Step", 4f, 64f, () => group.SnapSize,
                        value => CustomAuraLibrary.ChangeGroup(id,g=>g.SnapSize=Mathf.RoundToInt(value)), " px");
                    AuraPanelAction(scroll, "Snap Position Now", "Align group X and Y to the selected grid step.",
                        "SNAP", () => { CustomAuraLibrary.ChangeGroup(id,g=>g.SnapToGrid=true);
                            CustomAuraLibrary.MoveGroup(id,group.X,group.Y); RenderPage(); });
                    AuraPanelAction(scroll, "Shrink Group Elements", "Reduce member widths and heights by ten percent.",
                        "-10%", () => { CustomAuraLibrary.ScaleGroupMembers(id,.9f); RenderPage(); });
                    AuraPanelAction(scroll, "Enlarge Group Elements", "Increase member widths and heights by ten percent.",
                        "+10%", () => { CustomAuraLibrary.ScaleGroupMembers(id,1.1f); RenderPage(); });
                    AuraPanelAction(scroll, "Delete Group", "Ungroups children, preserving their aura settings.",
                        _deleteAuraGroupConfirm == id ? "CONFIRM" : "DELETE", () =>
                        {
                            if (_deleteAuraGroupConfirm != id) _deleteAuraGroupConfirm = id;
                            else { CustomAuraLibrary.DeleteGroup(id); _deleteAuraGroupConfirm = null; _selectedAuraGroupId = null; }
                            RenderPage();
                        });
                }
                AuraPanelSection(scroll, "Selected Aura", "Put this aura inside any group without changing its tracking.");
                var membership = new List<string> { "None" };
                membership.AddRange(groups);
                AuraPanelDropdown(scroll, "Parent Group", "Assign this aura to a parent container.",
                    AuraGroupLabel(aura.GroupId), membership.ToArray(), selected =>
                    {
                        string value = AuraGroupId(selected);
                        CustomAuraLibrary.Change(d=>d.GroupId=value);
                        RenderPage();
                    });
                AuraPanelSlider(scroll, "Manual Group Order", -100f, 100f, () => aura.GroupOrder,
                    value => CustomAuraLibrary.Change(d => d.GroupOrder = Mathf.RoundToInt(value)), "");
                if (!string.IsNullOrEmpty(aura.GroupId))
                {
                    AuraPanelAction(scroll, "Move Member Earlier", "Move this element one place earlier in its group.",
                        "UP", () => { CustomAuraLibrary.MoveSelectedInGroup(-1); RenderPage(); });
                    AuraPanelAction(scroll, "Move Member Later", "Move this element one place later in its group.",
                        "DOWN", () => { CustomAuraLibrary.MoveSelectedInGroup(1); RenderPage(); });
                }
                AuraPanelSection(scroll, "Linked Tracking", "Multiple visuals can use one aura's native data snapshot.");
                var sources = new List<string> { "Own Trigger" };
                foreach (var item in CustomAuraLibrary.All)
                    if (item.Id != aura.Id) sources.Add(AuraSourceLabel(item));
                AuraPanelDropdown(scroll, "Read Data From", "Share charges, stacks and timing with another aura.",
                    AuraLinkedLabel(aura.LinkedAuraId), sources.ToArray(), selected =>
                    {
                        CustomAuraLibrary.LinkSelected(AuraSourceId(selected)); RenderPage();
                    });
                AuraPanelInfo(scroll, "Linked displays use their source's tracking data but keep independent styling, load rules and appearance conditions. Cyclic links and unavailable sources fail closed.");
            }
            else if (_auraWorkspaceTab == "Layers")
            {
                AuraPanelSection(scroll, "Vector Geometry", "Customise the visual layers of icons, shapes and charge segments.");
                AuraPanelDropdown(scroll, "Shape Style", "Includes Ring and Arc geometry, with no external asset dependency.",
                    aura.Shape, CustomAuraLibrary.ShapeStyles,
                    value => { CustomAuraLibrary.Change(d=>d.Shape=value); RenderPage(); });
                AuraPanelSlider(scroll, "Ring Thickness", 2f, 48f, () => aura.RingThickness,
                    value => CustomAuraLibrary.Change(d=>d.RingThickness=Mathf.RoundToInt(value)), "%");
                if (aura.Display == "Shape" && (aura.Shape == "Ring" || aura.Shape == "Arc"))
                {
                    AuraPanelToggle(scroll, "Radial Progress", "Turn the ring/arc into a live sweeping gauge driven by the trigger's fraction.",
                        () => aura.RadialProgress, value => { CustomAuraLibrary.Change(d=>d.RadialProgress=value);
                            UpdateCustomAuraPreview(); RenderPage(); });
                    AuraPanelToggle(scroll, "Reverse Radial Sweep", "Fill the radial gauge counter-clockwise.",
                        () => aura.RadialReverse, value => { CustomAuraLibrary.Change(d=>d.RadialReverse=value);
                            UpdateCustomAuraPreview(); });
                }
                AuraPanelToggle(scroll, "Vertical Gradient", "Mix a second colour toward the top edge of the shape.",
                    () => aura.UseGradient, value => CustomAuraLibrary.Change(d=>d.UseGradient=value));
                AuraPanelColour(scroll, "Gradient End Colour", "Top tint for shapes and charge elements.",
                    () => aura.GradientColour, value => CustomAuraLibrary.Change(d=>d.GradientColour=value));
                AuraPanelSection(scroll, "Optional Image Texture", "Use any registered SpellDB artwork as a background layer.");
                AuraPanelTextInput(scroll, "Texture Spell", aura.BackgroundArtSpell,
                    "Spell name or ID; blank disables texture", value =>
                    { CustomAuraLibrary.Change(d=>d.BackgroundArtSpell=value); UpdateCustomAuraPreview(); });
                AuraPanelSlider(scroll, "Texture Opacity", 0f, 100f, () => aura.TextureOpacity,
                    value => CustomAuraLibrary.Change(d=>d.TextureOpacity=Mathf.RoundToInt(value)), "%");
                AuraPanelToggle(scroll, "Preserve Texture Aspect", "Fit the background artwork without stretching.",
                    () => aura.TexturePreserveAspect, value => CustomAuraLibrary.Change(d=>d.TexturePreserveAspect=value));
                AuraPanelSection(scroll, "Compositing", "Individual opacity, border, glow, shape and text layers.");
                AuraPanelInfo(scroll, "The Display and Animations tabs control the background, border, artwork, typography, opacity, transitions and glow independently. Shapes render vector geometry on the existing Unity UI canvas.");
            }
            else if (_auraWorkspaceTab == "Profiles")
            {
                AuraPanelSection(scroll, "Aura Profiles", "Independent saved libraries with their own groups and definitions.");
                AuraPanelDropdown(scroll, "Active Profile", "Switch between separately saved aura libraries.",
                    CustomAuraLibrary.ActiveProfile, CustomAuraLibrary.ProfileNames,
                    value => { CustomAuraLibrary.SwitchProfile(value); _auraFoldersInitialized = false;
                        _pendingAuraImportJson = null; _pendingAuraImportProfile = null; RenderPage(); });
                AuraPanelTextInput(scroll, "New Profile Name", _auraProfileDraft,
                    "Letters, numbers, spaces, underscores or hyphens",
                    value => _auraProfileDraft = value);
                AuraPanelAction(scroll, "Save As Profile", "Copies the current layout, groups and auras into a new profile.",
                    "CREATE", () =>
                    {
                        bool ok = CustomAuraLibrary.CreateProfile(_auraProfileDraft);
                        if (ok) CustomAuraLibrary.SwitchProfile(_auraProfileDraft.Trim());
                        _auraProfileFeedback = ok ? "Created and switched to new profile." :
                            "Invalid/duplicate profile name, or profile limit reached (16).";
                        _auraFoldersInitialized = false; RenderPage();
                    });
                if (CustomAuraLibrary.ActiveProfile != "Default")
                    AuraPanelAction(scroll, "Delete Current Profile", "Return to Default and delete this named profile.",
                        _deleteAuraProfileConfirm == CustomAuraLibrary.ActiveProfile ? "CONFIRM" : "DELETE", () =>
                        {
                            string name = CustomAuraLibrary.ActiveProfile;
                            if (_deleteAuraProfileConfirm != name) _deleteAuraProfileConfirm = name;
                            else { CustomAuraLibrary.DeleteProfile(name); _deleteAuraProfileConfirm = null; }
                            RenderPage();
                        });
                AuraPanelSection(scroll, "Import / Export", "Exchange JSON without overwriting existing saved auras.");
                AuraPanelAction(scroll, "Copy Profile JSON", "Copy the current aura library into the system clipboard.",
                    "COPY", () =>
                    {
                        UnityEngine.GUIUtility.systemCopyBuffer = CustomAuraLibrary.ExportJson();
                        _auraProfileFeedback = "Profile JSON copied to clipboard.";
                        RenderPage();
                    });
                AuraPanelAction(scroll, "Import from Clipboard", "Preview the incoming aura/group counts before changing the saved profile.",
                    _pendingAuraImportJson != null ? "CONFIRM" : "PREVIEW", () =>
                    {
                        if (_pendingAuraImportJson != null &&
                            _pendingAuraImportProfile == CustomAuraLibrary.ActiveProfile)
                        {
                            string result;
                            CustomAuraLibrary.ImportJson(_pendingAuraImportJson, out result);
                            _auraProfileFeedback = result;
                            _pendingAuraImportJson = null;
                            _pendingAuraImportProfile = null;
                            _auraFoldersInitialized = false;
                        }
                        else
                        {
                            string clipboard = UnityEngine.GUIUtility.systemCopyBuffer;
                            string result;
                            bool valid = CustomAuraLibrary.PreviewImportJson(clipboard, out result);
                            _pendingAuraImportJson = valid ? clipboard : null;
                            _pendingAuraImportProfile = valid ? CustomAuraLibrary.ActiveProfile : null;
                            _auraProfileFeedback = result;
                        }
                        RenderPage();
                    });
                if (_pendingAuraImportJson != null)
                    AuraPanelAction(scroll, "Cancel Pending Import", "Discard the clipboard preview without changing saved auras.",
                        "CANCEL", () => { _pendingAuraImportJson = null;
                            _pendingAuraImportProfile = null; _auraProfileFeedback = "Import cancelled."; RenderPage(); });
                AuraPanelInfo(scroll, string.IsNullOrEmpty(_auraProfileFeedback) ?
                    "Export a profile, share the JSON text, then import it. Existing auras are never overwritten. Limits: 200 auras and 32 groups per profile." : _auraProfileFeedback);
            }
            else if (_auraWorkspaceTab == "Animations")
            {
                AuraPanelSection(scroll, "Show / Hide Transitions", "Animate entering and leaving the screen without changing triggers.");
                AuraPanelDropdown(scroll, "Entrance", "Fade, pop or slide in when the trigger activates.",
                    aura.EnterAnimation, CustomAuraLibrary.EnterAnimations,
                    value => { CustomAuraLibrary.Change(d => d.EnterAnimation = value); RenderPage(); });
                AuraPanelDropdown(scroll, "Exit", "Fade, shrink or slide out when the trigger ends.",
                    aura.ExitAnimation, CustomAuraLibrary.ExitAnimations,
                    value => { CustomAuraLibrary.Change(d => d.ExitAnimation = value); RenderPage(); });
                AuraPanelSlider(scroll, "Transition Duration", 100f, 1200f,
                    () => aura.TransitionMs, value => CustomAuraLibrary.Change(d => d.TransitionMs = Mathf.RoundToInt(value)), " ms");
                AuraPanelSection(scroll, "Looping Motion", "Cosmetic motion while active. Does not affect gameplay state.");
                AuraPanelDropdown(scroll, "Loop Animation", "Pulse, breathe, float or shake, or leave still.",
                    aura.LoopAnimation, CustomAuraLibrary.LoopAnimations,
                    value => { CustomAuraLibrary.Change(d => d.LoopAnimation = value); RenderPage(); });
                if (aura.LoopAnimation != "None")
                {
                    AuraPanelSlider(scroll, "Animation Speed", 25f, 300f,
                        () => aura.LoopSpeed, value => CustomAuraLibrary.Change(d => d.LoopSpeed = Mathf.RoundToInt(value)), "%");
                    AuraPanelSlider(scroll, "Animation Intensity", 0f, 100f,
                        () => aura.LoopIntensity, value => CustomAuraLibrary.Change(d => d.LoopIntensity = Mathf.RoundToInt(value)), "%");
                }
                AuraPanelSection(scroll, "Glow Effects", "Optional raycast-transparent edge glow. Conditions can turn it on or off.");
                AuraPanelDropdown(scroll, "Glow Style", "Solid, breathing pulse or a flashing alert.",
                    aura.GlowStyle, CustomAuraLibrary.GlowStyles,
                    value => { CustomAuraLibrary.Change(d => d.GlowStyle = value); RenderPage(); });
                // Keep these editable even when Glow Style is None: a condition's
                // Enable override may turn on solid glow at runtime.
                {
                    AuraPanelColour(scroll, "Glow Colour", "Outline glow tint.",
                        () => aura.GlowColour, value => CustomAuraLibrary.Change(d => d.GlowColour = value));
                    AuraPanelSlider(scroll, "Glow Width", 1f, 16f,
                        () => aura.GlowWidth, value => CustomAuraLibrary.Change(d => d.GlowWidth = Mathf.RoundToInt(value)), " px");
                    AuraPanelSlider(scroll, "Glow Opacity", 0f, 100f,
                        () => aura.GlowOpacity, value => CustomAuraLibrary.Change(d => d.GlowOpacity = Mathf.RoundToInt(value)), "%");
                }
                AuraPanelSection(scroll, "Live Preview", "Loops and glow animate in the right-side panel.");
                AuraPanelAction(scroll, "Replay Entrance", "Restart the right-side entrance animation.",
                    "REPLAY", () => { _auraPreviewStartedAt = Time.unscaledTime; AnimateCustomAuraPreview(); });
                AuraPanelInfo(scroll, "To glow only when Heat is full, add a Condition: Value Percentage at least 100, then set Glow Reaction to Enable. A glow style of None uses solid glow for this override.");
            }
            ArcaneUiControls.RefreshScrollLayout(scroll, true);
        }

        private string[] AuraGroupLabels()
        {
            var choices = new List<string>();
            foreach (var g in CustomAuraLibrary.Groups) choices.Add(g.Name + " [" + g.Id.Substring(0, 6) + "]");
            return choices.ToArray();
        }
        private string AuraGroupLabel(string id)
        {
            var group = CustomAuraLibrary.FindGroup(id);
            return group == null ? "None" : group.Name + " [" + group.Id.Substring(0, 6) + "]";
        }
        private string AuraGroupId(string label)
        {
            foreach (var group in CustomAuraLibrary.Groups)
                if (AuraGroupLabel(group.Id) == label) return group.Id;
            return "";
        }
        private static string AuraSourceLabel(CustomAuraDefinition aura)
        { return aura.Name + " [" + aura.Id.Substring(0, 6) + "]"; }
        private static string AuraLinkedLabel(string id)
        {
            foreach (var aura in CustomAuraLibrary.All)
                if (aura.Id == id) return AuraSourceLabel(aura);
            return "Own Trigger";
        }
        private static string AuraSourceId(string label)
        {
            foreach (var aura in CustomAuraLibrary.All)
                if (AuraSourceLabel(aura) == label) return aura.Id;
            return "";
        }

        private void AuraPanelSpellPicker(RectTransform parent, CustomAuraDefinition aura)
        {
            // Class-aware, paged search over all live SpellDB entries. Manual
            // Match Name / Artwork Spell stays available for advanced users.
            var row = ArcaneUiControls.Row(parent, "AuraClassSpellPicker", 125f, Theme.Panel);
            ArcaneUiControls.Label(row, "Heading", "REGISTERED SPELL PICKER", 12f, 3f, 260f, 18f,
                12, Theme.Text, TextAnchor.MiddleLeft, FontStyle.Bold);
            InputField search = ArcaneUiControls.Search(row, "SpellQuery", 12f, 25f,
                291f, 24f, "Search spell name or ID", Card, Theme.Text,
                value => { _auraSpellSearch = value ?? ""; _auraSpellPage = 0; });
            search.SetTextWithoutNotify(_auraSpellSearch);
            search.characterLimit = 90;

            Button scope = ArcaneUiControls.Button(row, "Scope", _auraSpellScope + " ▼",
                12f, 57f, 198f, 26f, Theme.Panel, Theme.Text, null, 10);
            scope.onClick.AddListener(() => OpenChoicePopup(scope.GetComponent<RectTransform>(),
                AuraSpellPickerCatalog.Scopes, _auraSpellScope, value =>
                { _auraSpellScope = value; _auraSpellPage = 0; RenderPage(); }));

            Button choose = ArcaneUiControls.Button(row, "PickSpell", "PICK SPELL",
                215f, 57f, 88f, 26f, Theme.Accent, Color.white, null, 10);
            choose.onClick.AddListener(() =>
            {
                string filterClass = AuraSpellPickerCatalog.ResolvedClass(
                    _auraSpellScope, aura.LoadClass ?? aura.Folder);
                int total;
                string[] options = AuraSpellPickerCatalog.Search(_auraSpellSearch,
                    filterClass, _auraSpellPage, 28, out total);
                if (options.Length == 0) return;
                OpenChoicePopup(choose.GetComponent<RectTransform>(), options,
                    _auraWorkspaceTab == "Trigger" ? aura.MatchName : aura.ArtSpell,
                    name =>
                    {
                        if (_auraWorkspaceTab == "Trigger")
                            CustomAuraLibrary.Change(d => d.MatchName = name);
                        else CustomAuraLibrary.Change(d => d.ArtSpell = name);
                        UpdateCustomAuraPreview(); RenderPage();
                    });
            });
            string selectedClass = AuraSpellPickerCatalog.ResolvedClass(
                _auraSpellScope, aura.LoadClass ?? aura.Folder);
            int matches;
            AuraSpellPickerCatalog.Search(_auraSpellSearch, selectedClass,
                _auraSpellPage, 28, out matches);
            int pages = Math.Max(1, (matches + 27) / 28);
            if (_auraSpellPage >= pages) _auraSpellPage = pages - 1;
            ArcaneUiControls.Label(row, "Count", matches + " spells | " +
                (selectedClass.Length == 0 ? "all classes" : selectedClass) +
                " | page " + (_auraSpellPage + 1) + "/" + pages,
                12f, 88f, 202f, 29f, 10, Muted, TextAnchor.MiddleLeft);
            Button next = ArcaneUiControls.Button(row, "PageNext",
                pages <= 1 ? "PAGE 1" : "NEXT PAGE",
                215f, 89f, 88f, 26f, Theme.Panel, Theme.Text, null, 10);
            next.interactable = pages > 1;
            next.onClick.AddListener(() =>
            { _auraSpellPage = (_auraSpellPage + 1) % pages; RenderPage(); });
        }

        private void AuraPanelSection(RectTransform parent, string title, string subtitle)
        {
            var row = ArcaneUiControls.Row(parent, "AuraSection_" + title, 42f, Color.clear);
            ArcaneUiControls.Label(row, "Title", title.ToUpperInvariant(), 8f, 0f, 280f, 20f, 11,
                Theme.Text, TextAnchor.MiddleLeft, FontStyle.Bold);
            ArcaneUiControls.Label(row, "Subtitle", subtitle, 8f, 18f, 292f, 18f, 10, Muted);
        }

        private void AuraPanelInfo(RectTransform parent, string text)
        {
            var row = ArcaneUiControls.Row(parent, "AuraInfo_" + Guid.NewGuid().ToString("N"), 66f, Theme.Panel);
            ArcaneUiControls.Label(row, "Text", text, 12f, 10f, 280f, 46f, 11, Muted, TextAnchor.UpperLeft);
        }

        private void AuraPanelAction(RectTransform parent, string title, string subtitle, string button, Action onClick)
        {
            var row = ArcaneUiControls.Row(parent, "AuraAction_" + title, 54f, Theme.Panel);
            ArcaneUiControls.Label(row, "Title", title, 12f, 4f, 195f, 19f, 12,
                Theme.Text, TextAnchor.MiddleLeft, FontStyle.Bold);
            ArcaneUiControls.Label(row, "Sub", subtitle, 12f, 23f, 208f, 24f, 10, Muted, TextAnchor.UpperLeft);
            ArcaneUiControls.Button(row, "Button", button, 222f, 12f, 82f, 28f,
                Theme.Accent, Color.white, onClick, 10);
        }

        private void AuraPanelTextInput(RectTransform parent, string title, string value, string hint, Action<string> changed)
        {
            var row = ArcaneUiControls.Row(parent, "AuraText_" + title, 56f, Theme.Panel);
            ArcaneUiControls.Label(row, "Title", title, 12f, 5f, 290f, 18f, 12,
                Theme.Text, TextAnchor.MiddleLeft, FontStyle.Bold);
            InputField input = ArcaneUiControls.Search(row, "Input", 12f, 25f, 292f, 24f,
                hint, Card, Theme.Text, changed);
            input.SetTextWithoutNotify(value ?? string.Empty);
            input.characterLimit = title.StartsWith("Expression", StringComparison.Ordinal) ?
                ArcaneUiAuraExpression.MaxLength : 100;
        }

        private void AuraPanelToggle(RectTransform parent, string title, string subtitle, Func<bool> get, Action<bool> set)
        {
            var row = ArcaneUiControls.Row(parent, "AuraToggle_" + title, 54f, Theme.Panel);
            ArcaneUiControls.Label(row, "Title", title, 12f, 4f, 210f, 19f, 12,
                Theme.Text, TextAnchor.MiddleLeft, FontStyle.Bold);
            ArcaneUiControls.Label(row, "Sub", subtitle, 12f, 24f, 208f, 24f, 10, Muted, TextAnchor.UpperLeft);
            Button toggle = ArcaneUiControls.Button(row, "Button", get() ? "ON" : "OFF", 230f, 12f, 74f, 28f,
                get() ? Theme.Accent : Theme.Border, Color.white, null, 10);
            toggle.onClick.AddListener(() =>
            {
                bool next = !get();
                set(next);
                var img = toggle.GetComponent<Image>();
                if (img != null) img.color = next ? Theme.Accent : Theme.Border;
                var text = toggle.GetComponentInChildren<Text>();
                if (text != null) text.text = next ? "ON" : "OFF";
            });
        }

        private void AuraPanelChoice(RectTransform parent, string title, string subtitle, string current, Action next)
        {
            var row = ArcaneUiControls.Row(parent, "AuraChoice_" + title, 54f, Theme.Panel);
            ArcaneUiControls.Label(row, "Title", title, 12f, 4f, 170f, 19f, 12,
                Theme.Text, TextAnchor.MiddleLeft, FontStyle.Bold);
            ArcaneUiControls.Label(row, "Sub", subtitle, 12f, 24f, 208f, 24f, 10, Muted, TextAnchor.UpperLeft);
            ArcaneUiControls.Label(row, "Current", current, 192f, 4f, 112f, 18f, 11,
                Theme.Accent, TextAnchor.MiddleRight, FontStyle.Bold);
            ArcaneUiControls.Button(row, "Button", "CHANGE", 222f, 12f, 82f, 28f,
                Theme.Accent, Color.white, next, 10);
        }

        private void AuraPanelDropdown(RectTransform parent, string title, string subtitle,
            string current, string[] choices, Action<string> selected)
        {
            var row = ArcaneUiControls.Row(parent, "AuraDropdown_" + title, 60f, Theme.Panel);
            ArcaneUiControls.Label(row, "Title", title, 12f, 4f, 218f, 19f, 12,
                Theme.Text, TextAnchor.MiddleLeft, FontStyle.Bold);
            ArcaneUiControls.Label(row, "Sub", subtitle, 12f, 26f, 177f, 26f, 10,
                Muted, TextAnchor.UpperLeft);
            Button button = ArcaneUiControls.Button(row, "Choose", current + " ▼",
                183f, 20f, 131f, 32f, Theme.Panel, Theme.Text, null, 10);
            button.onClick.AddListener(() => OpenChoicePopup(button.GetComponent<RectTransform>(),
                choices, current, selected));
        }

        private void AuraPanelColour(RectTransform parent, string title, string subtitle, Func<string> get, Action<string> set)
        {
            var row = ArcaneUiControls.Row(parent, "AuraColour_" + title, 54f, Theme.Panel);
            ArcaneUiControls.Label(row, "Title", title, 12f, 4f, 210f, 19f, 12,
                Theme.Text, TextAnchor.MiddleLeft, FontStyle.Bold);
            ArcaneUiControls.Label(row, "Sub", subtitle, 12f, 24f, 208f, 24f, 10, Muted, TextAnchor.UpperLeft);
            Color swatchColor;
            if (!UnityEngine.ColorUtility.TryParseHtmlString(get(), out swatchColor)) swatchColor = Theme.Accent;
            var pickerButton = ArcaneUiControls.Button(row, "EditColour", get(), 182f, 12f, 122f, 28f,
                Card, Theme.Text, null, 10);
            var swatch = ArcaneUiControls.Panel(pickerButton.transform, "Swatch", 8f, 4f, 20f, 20f,
                swatchColor, false).GetComponent<Image>();
            var valueLabel = pickerButton.GetComponentInChildren<Text>();
            if (valueLabel != null) valueLabel.alignment = TextAnchor.MiddleRight;
            pickerButton.onClick.AddListener(() =>
            {
                if (_activeColorPicker != null) _activeColorPicker.Cancel();
                _activeColorPicker = ArcaneUiColorPickerDialog.Show(_window, title, get(),
                    Theme.Background, Card, Theme.Accent, Theme.Text,
                    hex =>
                    {
                        set(hex);
                        Color parsed;
                        if (ColorUtility.TryParseHtmlString(get(), out parsed)) swatch.color = parsed;
                        if (valueLabel != null) valueLabel.text = get();
                    },
                    () => { _activeColorPicker = null; });
            });
        }

        private void AuraPanelSlider(RectTransform parent, string title, float min, float max, Func<float> get, Action<float> set, string suffix)
        {
            var row = ArcaneUiControls.Row(parent, "AuraSlider_" + title, 54f, Theme.Panel);
            ArcaneUiControls.Label(row, "Title", title, 12f, 5f, 154f, 19f, 12,
                Theme.Text, TextAnchor.MiddleLeft, FontStyle.Bold);
            Text valueLabel = ArcaneUiControls.Label(row, "Value", get().ToString("0.#") + suffix,
                250f, 5f, 54f, 18f, 11, Theme.Accent, TextAnchor.MiddleRight, FontStyle.Bold);

            var sliderRoot = ArcaneUiControls.Panel(row, "Slider", 12f, 30f, 292f, 16f,
                new Color(0f, 0f, 0f, 0f));
            var slider = sliderRoot.gameObject.AddComponent<Slider>();
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = min;
            slider.maxValue = max;
            slider.wholeNumbers = true;

            var track = ArcaneUiControls.Panel(sliderRoot, "Track", 0f, 0f, 292f, 4f, Theme.Border, false);
            track.anchorMin = new Vector2(0f, .5f);
            track.anchorMax = new Vector2(1f, .5f);
            track.pivot = new Vector2(.5f, .5f);
            track.anchoredPosition = Vector2.zero;
            track.sizeDelta = new Vector2(0f, 4f);

            var fillArea = ArcaneUiControls.Rect(sliderRoot, "FillArea", 0f, 0f, 292f, 4f);
            fillArea.anchorMin = new Vector2(0f, .5f);
            fillArea.anchorMax = new Vector2(1f, .5f);
            fillArea.pivot = new Vector2(.5f, .5f);
            fillArea.anchoredPosition = Vector2.zero;
            fillArea.sizeDelta = new Vector2(-10f, 4f);
            var fill = ArcaneUiControls.Panel(fillArea, "Fill", 0f, 0f, 0f, 4f, Theme.Accent, false);
            fill.anchorMin = new Vector2(0f, 0f);
            fill.anchorMax = new Vector2(0f, 1f);
            fill.pivot = new Vector2(0f, .5f);
            fill.anchoredPosition = Vector2.zero;
            fill.sizeDelta = Vector2.zero;

            var handleArea = ArcaneUiControls.Rect(sliderRoot, "HandleArea", 0f, 0f, 292f, 16f);
            handleArea.anchorMin = new Vector2(0f, .5f);
            handleArea.anchorMax = new Vector2(1f, .5f);
            handleArea.pivot = new Vector2(.5f, .5f);
            handleArea.anchoredPosition = Vector2.zero;
            handleArea.sizeDelta = new Vector2(-10f, 16f);
            var handle = ArcaneUiControls.Panel(handleArea, "Handle", 0f, 0f, 10f, 0f, Theme.Text, false);
            handle.anchorMin = new Vector2(0f, 0f);
            handle.anchorMax = new Vector2(0f, 1f);
            handle.pivot = new Vector2(.5f, .5f);
            handle.anchoredPosition = Vector2.zero;
            handle.sizeDelta = new Vector2(10f, 0f);

            slider.targetGraphic = handle.GetComponent<Image>();
            slider.fillRect = fill;
            slider.handleRect = handle;
            slider.SetValueWithoutNotify(get());
            slider.onValueChanged.AddListener(value =>
            {
                set(value);
                valueLabel.text = get().ToString("0.#") + suffix;
            });
        }
        // Kept with the Aura Editor so the spell picker cannot be omitted from
        // changed-file source updates or older local Visual Studio projects.
        private static class AuraSpellPickerCatalog
            {
                internal static readonly string[] Scopes =
                    { "Current Class", "Aura Class", "All Classes" };

                internal static string CurrentClass()
                {
                    Stats player = GameData.PlayerStats;
                    if (player == null) return "";
                    return RaidClassColors.ResolveClassNameForActor(player, player.MyName) ?? "";
                }

                private static string Canonical(string value)
                {
                    return string.IsNullOrWhiteSpace(value) ? "" :
                        value.Trim().Replace(" ", "").Replace("'", "").ToLowerInvariant();
                }

                // The experimental FOTF expanded roster is not shipped as a playable
                // Aura Manager catalogue in this test branch. Manual custom trackers
                // are left in saved profiles, without treating unfinished spells as
                // selectable, released class abilities.
                private static bool IsExperimentalSpell(Spell spell)
                {
                    return spell != null && !string.IsNullOrEmpty(spell.Id) &&
                        spell.Id.StartsWith("FOTF_ER_", StringComparison.OrdinalIgnoreCase);
                }

                internal static bool AppliesTo(Spell spell, string className)
                {
                    if (spell == null || IsExperimentalSpell(spell)) return false;
                    string expected = Canonical(className);
                    if (expected.Length == 0 || expected == "general" || expected == "any") return true;
                    if (spell.UsedBy == null) return false;
                    foreach (Class usedBy in spell.UsedBy)
                    {
                        if (usedBy == null) continue;
                        if (Canonical(usedBy.ClassName) == expected ||
                            Canonical(usedBy.DisplayName) == expected) return true;
                    }
                    return false;
                }

                internal static string[] Search(string searchText, string className,
                    int page, int pageSize, out int total)
                {
                    total = 0;
                    var db = GameData.SpellDatabase;
                    if (db == null || db.SpellDatabase == null) return new string[0];
                    string search = (searchText ?? "").Trim();
                    var names = new List<string>();
                    var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    foreach (Spell spell in db.SpellDatabase)
                    {
                        if (spell == null || string.IsNullOrWhiteSpace(spell.SpellName) ||
                            !AppliesTo(spell, className)) continue;
                        if (search.Length > 0 && spell.SpellName.IndexOf(search,
                                StringComparison.OrdinalIgnoreCase) < 0 &&
                            (spell.Id == null || spell.Id.IndexOf(search,
                                StringComparison.OrdinalIgnoreCase) < 0)) continue;
                        if (seen.Add(spell.SpellName)) names.Add(spell.SpellName);
                    }
                    names.Sort(StringComparer.OrdinalIgnoreCase);
                    total = names.Count;
                    pageSize = Math.Max(1, Math.Min(40, pageSize));
                    page = Math.Max(0, page);
                    int start = Math.Min(names.Count, page * pageSize);
                    int count = Math.Min(pageSize, names.Count - start);
                    return names.GetRange(start, count).ToArray();
                }

                internal static string ResolvedClass(string scope, string folder)
                {
                    if (scope == "All Classes") return "";
                    if (scope == "Aura Class")
                        return folder == "General" ? "" : folder ?? "";
                    string current = CurrentClass();
                    return current.Length == 0 ? (folder == "General" ? "" : folder) : current;
                }
            }

    }
}
