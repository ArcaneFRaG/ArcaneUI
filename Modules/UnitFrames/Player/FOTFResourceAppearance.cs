using UnityEngine;

namespace ArcanesGroupFrames.Modules.UnitFrames.Player
{
    /// <summary>Pure UI settings. Each strip has its own settings, independent of FOTF gameplay.</summary>
    internal enum FOTFResourcePart { Primary, Secondary, Mass }

    internal static partial class FOTFResourceSettings
    {
        private static string Key(FOTFResourcePart part, string name)
        { return Prefix + part + "." + name; }
        private static float Number(FOTFResourcePart part, string name, float fallback, float min, float max)
        { return Mathf.Clamp(PlayerPrefs.GetFloat(Key(part, name), fallback), min, max); }
        private static string Colour(FOTFResourcePart part, string name, string fallback)
        { return PlayerPrefs.GetString(Key(part, name), fallback); }
        internal static bool Visible(FOTFResourcePart part)
        { return PlayerPrefs.GetInt(Key(part, "Visible"), 1) != 0; }
        internal static void SetVisible(FOTFResourcePart part, bool on)
        { PlayerPrefs.SetInt(Key(part, "Visible"), on ? 1 : 0); PlayerPrefs.Save(); }
        internal static bool Detached(FOTFResourcePart part)
        { return part == FOTFResourcePart.Primary ? DetachedPrimary :
                 part == FOTFResourcePart.Secondary ? DetachedSecondary :
                 PlayerPrefs.GetInt(Key(part, "Detached"), 0) != 0; }
        internal static void SetDetached(FOTFResourcePart part, bool on)
        {
            if (part == FOTFResourcePart.Primary) SetDetachedPrimary(on);
            else if (part == FOTFResourcePart.Secondary) SetDetachedSecondary(on);
            else { PlayerPrefs.SetInt(Key(part, "Detached"), on ? 1 : 0); PlayerPrefs.Save(); }
        }
        internal static float WidthScale(FOTFResourcePart part) { return Number(part,"WidthScale", 100f, 40f, 175f); }
        internal static void SetWidthScale(FOTFResourcePart part,float value) { SaveNumber(part,"WidthScale",value,40f,175f); }
        internal static float PartHeight(FOTFResourcePart part) { return Number(part,"Height",Height,8f,48f); }
        internal static void SetPartHeight(FOTFResourcePart part,float value) { SaveNumber(part,"Height",value,8f,48f); }
        internal static float Gap(FOTFResourcePart part) { return Number(part,"Gap",CellSpacing,0f,12f); }
        internal static void SetGap(FOTFResourcePart part,float value) { SaveNumber(part,"Gap",value,0f,12f); }
        internal static float XOffset(FOTFResourcePart part) { return Number(part,"OffsetX",0f,-150f,150f); }
        internal static void SetXOffset(FOTFResourcePart part,float value) { SaveNumber(part,"OffsetX",value,-150f,150f); }
        internal static float YOffset(FOTFResourcePart part)
        { return Number(part,"OffsetY", part == FOTFResourcePart.Primary ? PrimaryOffset : part == FOTFResourcePart.Secondary ? SecondaryOffset : 0f,0f,150f); }
        internal static void SetYOffset(FOTFResourcePart part,float value) { SaveNumber(part,"OffsetY",value,0f,150f); }
        internal static float FillOpacity(FOTFResourcePart part) { return Number(part,"FillOpacity",100f,0f,100f) / 100f; }
        internal static void SetFillOpacity(FOTFResourcePart part,float value) { SaveNumber(part,"FillOpacity",value,0f,100f); }
        internal static float BackgroundOpacity(FOTFResourcePart part) { return Number(part,"BackgroundOpacity",86f,0f,100f) / 100f; }
        internal static void SetBackgroundOpacity(FOTFResourcePart part,float value) { SaveNumber(part,"BackgroundOpacity",value,0f,100f); }
        internal static float BorderSize(FOTFResourcePart part) { return Number(part,"BorderSize",0f,0f,6f); }
        internal static void SetBorderSize(FOTFResourcePart part,float value) { SaveNumber(part,"BorderSize",value,0f,6f); }
        internal static float FontSize(FOTFResourcePart part) { return Number(part,"FontSize",12f,8f,30f); }
        internal static void SetFontSize(FOTFResourcePart part,float value) { SaveNumber(part,"FontSize",value,8f,30f); }
        internal static float LabelOffsetX(FOTFResourcePart part) { return Number(part,"LabelOffsetX",0f,-50f,50f); }
        internal static void SetLabelOffsetX(FOTFResourcePart part,float value) { SaveNumber(part,"LabelOffsetX",value,-50f,50f); }
        internal static float LabelOffsetY(FOTFResourcePart part) { return Number(part,"LabelOffsetY",0f,-30f,30f); }
        internal static void SetLabelOffsetY(FOTFResourcePart part,float value) { SaveNumber(part,"LabelOffsetY",value,-30f,30f); }
        internal static string FillColour(FOTFResourcePart part, string fallback) { return Colour(part,"FillColour", fallback); }
        internal static string EmptyColour(FOTFResourcePart part) { return Colour(part,"EmptyColour","#383F4A"); }
        internal static string BackgroundColour(FOTFResourcePart part) { return Colour(part,"BackgroundColour","#10121C"); }
        internal static string TextColour(FOTFResourcePart part) { return Colour(part,"TextColour","#FFFFFF"); }
        internal static string BorderColour(FOTFResourcePart part) { return Colour(part,"BorderColour","#52657A"); }
        internal static void SetPartColour(FOTFResourcePart part,string name,string colour)
        {
            Color c;
            if (!ColorUtility.TryParseHtmlString(colour ?? "", out c)) return;
            PlayerPrefs.SetString(Key(part,name),"#"+ColorUtility.ToHtmlStringRGB(c)); PlayerPrefs.Save();
        }
        internal static string Texture(FOTFResourcePart part) { return PlayerPrefs.GetString(Key(part,"Texture"),"Flat"); }
        internal static void SetTexture(FOTFResourcePart part,string name) { SaveChoice(part,"Texture",name); }
        internal static string Font(FOTFResourcePart part) { return PlayerPrefs.GetString(Key(part,"Font"),"Default"); }
        internal static void SetFont(FOTFResourcePart part,string name) { SaveChoice(part,"Font",name); }
        internal static string TextMode(FOTFResourcePart part) { return PlayerPrefs.GetString(Key(part,"TextMode"),"Name + Values"); }
        internal static void SetTextMode(FOTFResourcePart part,string value) { SaveChoice(part,"TextMode",value); }
        internal static string Alignment(FOTFResourcePart part) { return PlayerPrefs.GetString(Key(part,"Alignment"),"Center"); }
        internal static void SetAlignment(FOTFResourcePart part,string value) { SaveChoice(part,"Alignment",value); }
        internal static string Direction(FOTFResourcePart part) { return PlayerPrefs.GetString(Key(part,"Direction"),"Left to Right"); }
        internal static void SetDirection(FOTFResourcePart part,string value) { SaveChoice(part,"Direction",value); }
        internal static bool Bold(FOTFResourcePart part) { return PlayerPrefs.GetInt(Key(part,"Bold"),0)!=0; }
        internal static void SetBold(FOTFResourcePart part,bool v) { PlayerPrefs.SetInt(Key(part,"Bold"),v?1:0); PlayerPrefs.Save(); }
        internal static bool OutlineText(FOTFResourcePart part) { return PlayerPrefs.GetInt(Key(part,"TextOutline"),0)!=0; }
        internal static void SetOutlineText(FOTFResourcePart part,bool v) { PlayerPrefs.SetInt(Key(part,"TextOutline"),v?1:0); PlayerPrefs.Save(); }
        internal static Vector2 MassPosition
        { get { return _massDragPosition ?? new Vector2(PlayerPrefs.GetFloat(Prefix+"MassX",330f),PlayerPrefs.GetFloat(Prefix+"MassY",-250f)); } }
        private static Vector2? _massDragPosition;
        internal static void SetMassPosition(Vector2 pos)
        { _massDragPosition=null; PlayerPrefs.SetFloat(Prefix+"MassX",pos.x); PlayerPrefs.SetFloat(Prefix+"MassY",pos.y); PlayerPrefs.Save(); }
        internal static void SetMassDragPosition(Vector2 pos) { _massDragPosition=pos; }
        internal static void ClearMassDragPosition() { _massDragPosition=null; }
        private static void SaveChoice(FOTFResourcePart part,string name,string value)
        { PlayerPrefs.SetString(Key(part,name),value??""); PlayerPrefs.Save(); }
        private static void SaveNumber(FOTFResourcePart part,string name,float value,float min,float max)
        { PlayerPrefs.SetFloat(Key(part,name),Mathf.Clamp(value,min,max)); PlayerPrefs.Save(); }
        internal static void ResetPart(FOTFResourcePart part)
        {
            string[] keys = {"Visible","WidthScale","Height","Gap","OffsetX","OffsetY","FillOpacity","BackgroundOpacity","BorderSize","FontSize","LabelOffsetX","LabelOffsetY","FillColour","EmptyColour","BackgroundColour","TextColour","BorderColour","Texture","Font","TextMode","Alignment","Direction","Bold","TextOutline","Detached"};
            foreach (string key in keys) PlayerPrefs.DeleteKey(Key(part,key));
            PlayerPrefs.Save();
        }
    }
}
