using System;
using System.Reflection;
using UnityEngine;
using ArcanesGroupFrames;
using UnityEngine.UI;
using ArcanesGroupFrames.Modules.CooldownAuras.UI;

namespace ArcanesGroupFrames.Modules.UnitFrames.Player
{
    /// <summary>Optional display bridge. ArcaneUI never depends on FOTF.dll at build time.</summary>
    // Release-test reader: only Emberweaver has a supported numeric class
    // resource. Blightcaller and Lightbringer use the ordinary Aura Manager and
    // native shield/buff readers. Expanded classes are not part of this build.
    internal static class ArcaneUiFOTFBridge
    {
        internal struct Values
        {
            internal string ClassKey;
            internal int Primary, PrimaryMax, Secondary, SecondaryMax, Mass, MassMax;
        }

        private static MethodInfo _report;
        private static float _nextLookup;
        private static bool _reported;
        private static bool _reportBound;

        internal static string ConnectionStatus
        {
            get
            {
                Values values;
                if (Read(out values))
                    return "Connected: Emberweaver Heat " + values.Primary + "/" +
                        values.PrimaryMax + ", Flashfire " + values.Secondary + "/" +
                        values.SecondaryMax + ".";
                return "No active numeric FOTF class resource. This test build supports " +
                    "Emberweaver Heat/Flashfire; Blightcaller and Lightbringer " +
                    "use normal spell, buff and shield tracking.";
            }
        }

        internal static bool Read(out Values values)
        {
            values = new Values();
            if (!ArcaneUiAuraResourceReader.IsCurrentPlayerEmberweaver()) return false;
            float heat, heatMaximum, flashfire, flashfireMaximum;
            if (!ArcaneUiAuraResourceReader.TryRead("Emberweaver Heat", out heat, out heatMaximum) ||
                heatMaximum <= 0f) return false;
            bool hasFlashfire = ArcaneUiAuraResourceReader.TryRead(
                "Emberweaver Flashfire", out flashfire, out flashfireMaximum);
            values.ClassKey = "Emberweaver";
            values.PrimaryMax = 10;
            values.Primary = Mathf.Clamp(Mathf.RoundToInt(heat), 0, 10);
            values.SecondaryMax = 1;
            values.Secondary = hasFlashfire && flashfire > 0.5f ? 1 : 0;
            return true;
        }

        // Optional interop only: tell FOTF when ArcaneUI owns the shown HUD.
        // No experimental class-resource methods are read or registered.
        internal static void ReportVisible(bool visible)
        {
            if (_report == null && Time.unscaledTime >= _nextLookup)
            {
                _nextLookup = Time.unscaledTime + 3f;
                foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    try
                    {
                        Type type = assembly.GetType("FateOfTheFallen.FOTFArcaneResourceBridge", false);
                        if (type == null) continue;
                        MethodInfo method = type.GetMethod("ReportArcaneUIDisplay",
                            BindingFlags.Public | BindingFlags.Static);
                        if (method == null) continue;
                        if (method.GetParameters().Length != 1) continue;
                        _report = method;
                        _reportBound = true;
                        break;
                    }
                    catch (Exception) { }
                }
            }
            if (!_reportBound || _reported == visible) return;
            try { _report.Invoke(null, new object[] { visible }); _reported = visible; }
            catch (Exception) { _report = null; _reportBound = false; }
        }
    }

    internal static partial class FOTFResourceSettings
    {
        private const string Prefix = "ArcaneUI.FOTFResources.";
        internal static bool Enabled { get { return PlayerPrefs.GetInt(Prefix + "Enabled", 1) != 0; } }
        internal static bool ShowNumbers { get { return PlayerPrefs.GetInt(Prefix + "Numbers", 1) != 0; } }
        internal static bool DetachedPrimary { get { return PlayerPrefs.GetInt(Prefix + "DetachPrimary", 0) != 0; } }
        internal static bool DetachedSecondary { get { return PlayerPrefs.GetInt(Prefix + "DetachSecondary", 0) != 0; } }
        internal static float Height { get { return Mathf.Clamp(PlayerPrefs.GetFloat(Prefix + "Height", 16f), 8f, 34f); } }
        internal static float CellSpacing { get { return Mathf.Clamp(PlayerPrefs.GetFloat(Prefix + "Gap", 2f), 0f, 8f); } }
        // Older integration used 20/18 pixel gaps by default. Upgrade only
        // those legacy defaults to flush attachment; preserve custom spacing.
        private static bool _spacingMigrationChecked;
        private static Vector2? _primaryDragPosition, _secondaryDragPosition;
        private static void MigrateAttachedSpacing()
        {
            if (_spacingMigrationChecked) return;
            _spacingMigrationChecked = true;
            if (PlayerPrefs.GetInt(Prefix + "FlushAttachmentV2", 0) != 0) return;
            if (Mathf.Approximately(PlayerPrefs.GetFloat(Prefix + "PrimaryOffset", 20f), 20f))
                PlayerPrefs.SetFloat(Prefix + "PrimaryOffset", 0f);
            if (Mathf.Approximately(PlayerPrefs.GetFloat(Prefix + "SecondaryOffset", 18f), 18f))
                PlayerPrefs.SetFloat(Prefix + "SecondaryOffset", 0f);
            PlayerPrefs.SetInt(Prefix + "FlushAttachmentV2", 1);
            PlayerPrefs.Save();
        }
        internal static float PrimaryOffset { get { MigrateAttachedSpacing(); return Mathf.Clamp(PlayerPrefs.GetFloat(Prefix + "PrimaryOffset", 0f), 0f, 100f); } }
        internal static float SecondaryOffset { get { MigrateAttachedSpacing(); return Mathf.Clamp(PlayerPrefs.GetFloat(Prefix + "SecondaryOffset", 0f), 0f, 100f); } }
        internal static Vector2 PrimaryPosition { get { return _primaryDragPosition ?? new Vector2(PlayerPrefs.GetFloat(Prefix + "PrimaryX", 330f), PlayerPrefs.GetFloat(Prefix + "PrimaryY", -180f)); } }
        internal static Vector2 SecondaryPosition { get { return _secondaryDragPosition ?? new Vector2(PlayerPrefs.GetFloat(Prefix + "SecondaryX", 330f), PlayerPrefs.GetFloat(Prefix + "SecondaryY", -215f)); } }
        // The Layout Editor owns these temporary positions until mouse release.
        // Do not write PlayerPrefs every frame or let the renderer overwrite drags.
        internal static void SetDragPosition(bool secondary, Vector2 position)
        {
            if (secondary) _secondaryDragPosition = position;
            else _primaryDragPosition = position;
        }
        internal static void ClearDragPositions()
        {
            _primaryDragPosition = null;
            _secondaryDragPosition = null;
        }
        internal static string ChargeColour { get { return PlayerPrefs.GetString(Prefix + "ChargeColour", "#43C9E8"); } }
        internal static string PowerColour { get { return PlayerPrefs.GetString(Prefix + "PowerColour", "#8C5CCE"); } }
        internal static void SetEnabled(bool value) { SetBool("Enabled", value); }
        internal static void SetShowNumbers(bool value) { SetBool("Numbers", value); }
        internal static void SetDetachedPrimary(bool value) { SetBool("DetachPrimary", value); }
        internal static void SetDetachedSecondary(bool value) { SetBool("DetachSecondary", value); }
        internal static void SetHeight(float value) { SetFloat("Height", Mathf.Clamp(value, 8f, 34f)); }
        internal static void SetCellSpacing(float value) { SetFloat("Gap", Mathf.Clamp(value, 0f, 8f)); }
        internal static void SetPrimaryOffset(float value) { MigrateAttachedSpacing(); SetFloat("PrimaryOffset", Mathf.Clamp(value, 0f, 100f)); }
        internal static void SetSecondaryOffset(float value) { MigrateAttachedSpacing(); SetFloat("SecondaryOffset", Mathf.Clamp(value, 0f, 100f)); }
        internal static void SetPrimaryPosition(Vector2 value) { _primaryDragPosition = null; SetFloat("PrimaryX", value.x); SetFloat("PrimaryY", value.y); }
        internal static void SetSecondaryPosition(Vector2 value) { _secondaryDragPosition = null; SetFloat("SecondaryX", value.x); SetFloat("SecondaryY", value.y); }
        internal static void SetChargeColour(string value) { SetHex("ChargeColour", value); }
        internal static void SetPowerColour(string value) { SetHex("PowerColour", value); }
        private static void SetBool(string key, bool value) { PlayerPrefs.SetInt(Prefix + key, value ? 1 : 0); PlayerPrefs.Save(); }
        private static void SetFloat(string key, float value) { PlayerPrefs.SetFloat(Prefix + key, value); PlayerPrefs.Save(); }
        private static void SetHex(string key, string value)
        {
            Color colour;
            if (!ColorUtility.TryParseHtmlString(value ?? "", out colour)) return;
            PlayerPrefs.SetString(Prefix + key, "#" + ColorUtility.ToHtmlStringRGB(colour));
            PlayerPrefs.Save();
        }
    }

    /// <summary>
    /// Two independent genuine player-frame elements (primary and secondary).
    /// <=10 are ten distinct pips ABOVE the player frame; continuous power runs
    /// BELOW mana. Detached pips are separately registered in Layout Editor.
    /// Never writes into FOTF's resource totals or native player stats.
    /// </summary>
    internal sealed class ArcaneUiFOTFResources
    {
        private sealed class Strip
        {
            internal RectTransform Root;
            internal Image Background;
            internal Image Fill;
            internal Image PreviewHotspot;
            internal Text Label;
            internal readonly Image[] Cells = new Image[10];
            internal readonly Image[] Borders = new Image[4];
            internal UnityEngine.UI.Outline TextOutline;
        }
        private readonly RectTransform _layer, _player;
        private readonly bool _isPreview;
        private readonly Strip _primary, _secondary, _mass;
        internal RectTransform PrimaryRect { get { return _primary.Root; } }
        internal RectTransform SecondaryRect { get { return _secondary.Root; } }
        internal RectTransform MassRect { get { return _mass.Root; } }
        // Preview-only click areas. Never enable raycasts on live resources.
        internal Image PrimaryClickArea { get { return _primary.PreviewHotspot; } }
        internal Image SecondaryClickArea { get { return _secondary.PreviewHotspot; } }
        internal Image MassClickArea { get { return _mass.PreviewHotspot; } }
        internal ArcaneUiFOTFResources(RectTransform layer, RectTransform player, bool isPreview = false)
        {
            _layer = layer; _player = player; _isPreview = isPreview;
            _primary = Make("FOTF Primary Resource");
            _secondary = Make("FOTF Secondary Resource");
            _mass = Make("FOTF Bone Mass");
        }
        private Strip Make(string title)
        {
            var strip = new Strip();
            var obj = new GameObject(title, typeof(RectTransform));
            strip.Root = obj.GetComponent<RectTransform>();
            strip.Root.SetParent(_player, false);
            strip.Root.anchorMin = strip.Root.anchorMax = new Vector2(0,1);
            strip.Root.pivot = new Vector2(0,1);
            strip.Background = AddImage(strip.Root,"Background");
            strip.Background.raycastTarget = false;
            strip.Fill = AddImage(strip.Root,"Continuous Fill");
            for (int i=0;i<10;i++) strip.Cells[i] = AddImage(strip.Root,"Charge " + (i+1));
            var labelObject = new GameObject("Resource Text",typeof(RectTransform),typeof(Text));
            labelObject.transform.SetParent(strip.Root,false);
            strip.Label = labelObject.GetComponent<Text>();
            strip.Label.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            strip.Label.raycastTarget = false;
            strip.TextOutline = labelObject.AddComponent<UnityEngine.UI.Outline>();
            strip.TextOutline.effectColor = new Color(0f,0f,0f,.9f);
            strip.TextOutline.effectDistance = new Vector2(1f,-1f);
            for (int i=0;i<4;i++)
            {
                strip.Borders[i] = AddImage(strip.Root,"Border " + i);
                strip.Borders[i].raycastTarget = false;
            }
            if (_isPreview)
            {
                strip.PreviewHotspot = AddImage(strip.Root, "Preview Select Resource");
                strip.PreviewHotspot.color = new Color(1f,1f,1f,0f);
                strip.PreviewHotspot.raycastTarget = true;
            }
            strip.Root.gameObject.SetActive(false);
            return strip;
        }
        private static Image AddImage(RectTransform parent,string name)
        {
            var go = new GameObject(name,typeof(RectTransform),typeof(Image));
            go.transform.SetParent(parent,false);
            Image result = go.GetComponent<Image>(); result.raycastTarget=false;
            return result;
        }
        private static void Rect(RectTransform r,float x,float y,float width,float height)
        {
            r.anchorMin=r.anchorMax=new Vector2(0,1); r.pivot=new Vector2(0,1);
            r.anchoredPosition=new Vector2(x,y); r.sizeDelta=new Vector2(Mathf.Max(0,width),Mathf.Max(0,height));
        }
        private static Color Parse(string hex,Color fallback)
        { Color c; return ColorUtility.TryParseHtmlString(hex, out c) ? c : fallback; }
        private static Color Fade(Color color,float alpha)
        { color.a *= alpha; return color; }
        private static string NameFor(string key)
        {
            return key == "Emberweaver" ? "Heat" : "Class Resource";
        }
        internal void Update(bool showPlayer,float width,float frameHeight)
        {
            ArcaneUiFOTFBridge.Values snapshot = new ArcaneUiFOTFBridge.Values();
            bool valid = showPlayer && FOTFResourceSettings.Enabled && ArcaneUiFOTFBridge.Read(out snapshot);
            if (!valid) { Hide(); return; }
            bool visible = Draw(snapshot,width,frameHeight);
            if (!_isPreview) ArcaneUiFOTFBridge.ReportVisible(visible);
        }
        internal void UpdatePreview(string classKey,int primary,int primaryMax,int secondary,int secondaryMax,
            int mass,int massMax,float width,float frameHeight)
        {
            if (!FOTFResourceSettings.Enabled) { Hide(); return; }
            var sample = new ArcaneUiFOTFBridge.Values
            { ClassKey=classKey,Primary=primary,PrimaryMax=primaryMax,Secondary=secondary,
              SecondaryMax=secondaryMax,Mass=mass,MassMax=massMax };
            Draw(sample,width,frameHeight);
        }
        private bool Draw(ArcaneUiFOTFBridge.Values v,float frameWidth,float frameHeight)
        {
            bool first = FOTFResourceSettings.Visible(FOTFResourcePart.Primary) && v.PrimaryMax>0;
            bool second = FOTFResourceSettings.Visible(FOTFResourcePart.Secondary) && v.SecondaryMax>0;
            bool mass = false; // Auxiliary experimental resource is not offered in this build.
            bool charges = v.PrimaryMax<=10;
            float firstH = FOTFResourceSettings.PartHeight(FOTFResourcePart.Primary);
            float secondH = FOTFResourceSettings.PartHeight(FOTFResourcePart.Secondary);
            float firstY = charges ? firstH+FOTFResourceSettings.YOffset(FOTFResourcePart.Primary) : -frameHeight;
            DrawStrip(_primary,FOTFResourcePart.Primary,first,FOTFResourceSettings.Detached(FOTFResourcePart.Primary),
                FOTFResourceSettings.PrimaryPosition,frameWidth,firstY,NameFor(v.ClassKey),v.Primary,v.PrimaryMax,charges,
                charges ? FOTFResourceSettings.ChargeColour : FOTFResourceSettings.PowerColour);
            // A secondary charge strip stacks above the first when attached.
            float secondY = firstY+secondH+FOTFResourceSettings.YOffset(FOTFResourcePart.Secondary);
            DrawStrip(_secondary,FOTFResourcePart.Secondary,second,FOTFResourceSettings.Detached(FOTFResourcePart.Secondary),
                FOTFResourceSettings.SecondaryPosition,frameWidth,secondY,"Flashfire",v.Secondary,v.SecondaryMax,true,"#FF9C2A");
            // Bone Mass is a distinct 0-8 budget, not embedded inside the shard label.
            float massY = -frameHeight - FOTFResourceSettings.YOffset(FOTFResourcePart.Mass);
            DrawStrip(_mass,FOTFResourcePart.Mass,mass,FOTFResourceSettings.Detached(FOTFResourcePart.Mass),
                FOTFResourceSettings.MassPosition,frameWidth,massY,"Auxiliary",v.Mass,v.MassMax,true,"#E0AE5C");
            return first || second || mass;
        }
        private void DrawStrip(Strip strip,FOTFResourcePart part,bool visible,bool detached,
            Vector2 detachedPos,float frameWidth,float attachedY,string name,int value,int max,
            bool segmented,string fallbackFill)
        {
            strip.Root.gameObject.SetActive(visible);
            if (!visible) return;
            float w = Mathf.Clamp(frameWidth * FOTFResourceSettings.WidthScale(part)/100f,80f,650f);
            float h = FOTFResourceSettings.PartHeight(part);
            RectTransform parent = detached ? _layer : _player;
            if (strip.Root.parent!=parent) strip.Root.SetParent(parent,false);
            Rect(strip.Root, detached ? detachedPos.x : FOTFResourceSettings.XOffset(part),
                detached ? detachedPos.y : attachedY,w,h);
            value = Mathf.Clamp(value,0,Mathf.Max(1,max));
            Color fill=Fade(Parse(FOTFResourceSettings.FillColour(part,fallbackFill),Color.white),FOTFResourceSettings.FillOpacity(part));
            Color empty=Parse(FOTFResourceSettings.EmptyColour(part),new Color(.22f,.25f,.29f));
            strip.Background.color=Fade(Parse(FOTFResourceSettings.BackgroundColour(part),Color.black),
                FOTFResourceSettings.BackgroundOpacity(part));
            Rect(strip.Background.rectTransform,0,0,w,h);
            if (strip.PreviewHotspot != null) Rect(strip.PreviewHotspot.rectTransform,0f,0f,w,h);
            string texture = FOTFResourceSettings.Texture(part);
            Sprite sprite = UnitFrameAppearance.GetBarSprite(texture);
            strip.Fill.sprite = sprite;
            strip.Fill.type = Image.Type.Tiled;
            bool reverse = FOTFResourceSettings.Direction(part)=="Right to Left";
            strip.Fill.gameObject.SetActive(!segmented);
            if (segmented)
            {
                float gap=FOTFResourceSettings.Gap(part);
                int count=Mathf.Clamp(max,1,10);
                float cellWidth=Mathf.Max(1f,(w-(count-1)*gap)/count);
                for (int i=0;i<10;i++)
                {
                    var cell=strip.Cells[i];
                    cell.gameObject.SetActive(i<count);
                    if (i>=count) continue;
                    Rect(cell.rectTransform,i*(cellWidth+gap),0,cellWidth,h);
                    cell.sprite=sprite; cell.type=Image.Type.Tiled;
                    bool on=reverse ? count-1-i < value : i<value;
                    cell.color=on ? fill : empty;
                }
            }
            else
            {
                foreach (Image cell in strip.Cells) cell.gameObject.SetActive(false);
                float fillWidth=w*(float)value/Mathf.Max(1,max);
                Rect(strip.Fill.rectTransform,reverse ? w-fillWidth : 0,0,fillWidth,h);
                strip.Fill.color=fill;
            }
            float border=FOTFResourceSettings.BorderSize(part);
            Color edge=Parse(FOTFResourceSettings.BorderColour(part),Color.white);
            for (int i=0;i<4;i++) { strip.Borders[i].gameObject.SetActive(border>0f); strip.Borders[i].color=edge; }
            if (border>0f)
            {
                Rect(strip.Borders[0].rectTransform,0,0,w,border);
                Rect(strip.Borders[1].rectTransform,0,-(h-border),w,border);
                Rect(strip.Borders[2].rectTransform,0,0,border,h);
                Rect(strip.Borders[3].rectTransform,w-border,0,border,h);
            }
            string mode=FOTFResourceSettings.TextMode(part);
            bool show=FOTFResourceSettings.ShowNumbers && mode!="Hidden";
            strip.Label.gameObject.SetActive(show);
            if (show)
            {
                Rect(strip.Label.rectTransform,FOTFResourceSettings.LabelOffsetX(part),
                    FOTFResourceSettings.LabelOffsetY(part),w,h);
                strip.Label.text= mode=="Name Only" ? name : mode=="Values Only" ? value+"/"+max : name+"  "+value+"/"+max;
                strip.Label.color=Parse(FOTFResourceSettings.TextColour(part),Color.white);
                strip.Label.font=UnitFrameAppearance.ResolveFont(FOTFResourceSettings.Font(part));
                strip.Label.fontSize=Mathf.RoundToInt(FOTFResourceSettings.FontSize(part));
                strip.Label.fontStyle=FOTFResourceSettings.Bold(part)?FontStyle.Bold:FontStyle.Normal;
                string align=FOTFResourceSettings.Alignment(part);
                strip.Label.alignment=align=="Left"?TextAnchor.MiddleLeft:align=="Right"?TextAnchor.MiddleRight:TextAnchor.MiddleCenter;
                strip.TextOutline.enabled=FOTFResourceSettings.OutlineText(part);
            }
        }
        internal void RefreshPositions(float width,float height)
        { Update(_player!=null && _player.gameObject.activeInHierarchy,width,height); }
        internal void Hide()
        {
            _primary.Root.gameObject.SetActive(false);
            _secondary.Root.gameObject.SetActive(false);
            _mass.Root.gameObject.SetActive(false);
            if (!_isPreview) ArcaneUiFOTFBridge.ReportVisible(false);
        }
        internal void Shutdown() { Hide(); }
    }
}
