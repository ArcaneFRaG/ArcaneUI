using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.UI;
using ArcanesGroupFrames.Modules.CharacterSheet.Configuration;

namespace ArcanesGroupFrames.Modules.CharacterSheet.Native
{
    // Rehosts the verified native cosmetic controls, including each ItemIcon,
    // without duplicating gameplay components. Original transforms/parents are
    // restored by CharacterSheetBoundedView.RestoreControls on closing.
    internal sealed partial class CharacterSheetBoundedView
    {
        private sealed class CosmeticEntry
        {
            internal RectTransform Native;
            internal string Label;
        }
        private readonly List<CosmeticEntry> _cosmeticEntries = new List<CosmeticEntry>();
        private RectTransform _cosmeticCustom;
        private RectTransform _cosmeticGrid;
        private RectTransform _cosmeticHelmHost;
        private Text _cosmeticFootnote;
        private bool _cosmeticMapped;
        private bool _cosmeticWarned;
        private static readonly string[] CosmeticSlotNames = {
            "BackSlotTM", "HeadSlotTM", "RightWristSlotTM", "ChestSlotTM",
            "ArmSlotTM", "HandSlotTM", "LegSlotTM", "FootSlotTM"
        };
        private static readonly string[] CosmeticLabels = {
            "BACK", "HEAD", "WRIST", "CHEST", "ARMS", "HANDS", "LEGS", "FEET"
        };
        private void CreateCosmeticWardrobe(RectTransform parent, SheetPalette palette)
        {
            _cosmeticCustom=Rect(parent,"ArcaneUI_WardrobePage",Vector2.zero,Vector2.one,
                Vector2.zero,Vector2.zero);
            RectTransform heading=Box(_cosmeticCustom,"Heading",new Vector2(.025f,.913f),
                new Vector2(.975f,.995f),palette.Header,Vector2.zero,Vector2.zero);
            CreateText(heading,"Title","WARDROBE  •  APPEARANCE",16,
                TextAnchor.MiddleCenter,palette.Accent);
            RectTransform info=Rect(_cosmeticCustom,"Hint",new Vector2(.03f,.85f),
                new Vector2(.97f,.915f),Vector2.zero,Vector2.zero);
            CreateText(info,"Info","Visual equipment • native item drag & drop",11,
                TextAnchor.MiddleCenter,palette.Muted);
            _cosmeticGrid=Rect(_cosmeticCustom,"SlotGrid",new Vector2(.04f,.22f),
                new Vector2(.96f,.845f),Vector2.zero,Vector2.zero);
            RectTransform footer=Box(_cosmeticCustom,"Footer",new Vector2(.035f,.018f),
                new Vector2(.965f,.205f),palette.Equipment,Vector2.zero,Vector2.zero);
            RectTransform caption=Rect(footer,"Caption",new Vector2(.025f,.52f),
                new Vector2(.975f,.98f),Vector2.zero,Vector2.zero);
            _cosmeticFootnote=CreateText(caption,"CaptionText",
                "Drag cosmetic items onto their native slot. Your stats remain unchanged.",11,
                TextAnchor.MiddleCenter,palette.Body);
            _cosmeticHelmHost=Box(footer,"NativeHelm",new Vector2(.23f,.08f),
                new Vector2(.77f,.52f),palette.Section,Vector2.zero,Vector2.zero);
            CreateText(_cosmeticHelmHost,"EmptyHelmHint","COSMETIC CONTROLS",11,
                TextAnchor.MiddleCenter,palette.Muted);
            _cosmeticCustom.gameObject.SetActive(false);
        }
        private static bool CosmeticInteractive(RectTransform rect)
        {
            if (rect == null) return false;
            if (HasOwnInput(rect)) return true;
            foreach (Component component in rect.GetComponentsInChildren<Component>(true))
            {
                if (component == null) continue;
                if (component.GetType().Name == "ItemIcon") return true;
                if (component is UnityEngine.UI.Selectable) return true;
            }
            return false;
        }
        private void CollectCosmeticSlots()
        {
            _cosmeticEntries.Clear();
            if (_nativeCosmetic == null) return;
            for (int i=0;i<CosmeticSlotNames.Length;i++)
            {
                RectTransform rect=_nativeCosmetic.Find(CosmeticSlotNames[i]) as RectTransform;
                if (rect!=null)
                    _cosmeticEntries.Add(new CosmeticEntry {Native=rect,Label=CosmeticLabels[i]});
            }
            // 'CosSlot' names are native extra cosmetic inventory categories.
            // They may be duplicated with Unity's (1), (2) naming scheme.
            foreach (Transform child in _nativeCosmetic)
            {
                RectTransform rect=child as RectTransform;
                if (rect == null || !Regex.IsMatch(rect.name,@"^CosSlot\d+(?: \(\d+\))?$")) continue;
                _cosmeticEntries.Add(new CosmeticEntry {Native=rect,Label="MISC"});
            }
            RectTransform hide=_nativeCosmetic.Find("DebugSlot") as RectTransform;
            if (hide!=null)
                _cosmeticEntries.Add(new CosmeticEntry {Native=hide,Label="HIDE SLOT"});
        }
        private void MountCosmeticWardrobe()
        {
            _cosmeticMapped=false;
            if (_nativeCosmetic==null || _cosmeticGrid==null || _cosmeticCustom==null) return;
            CollectCosmeticSlots();
            // Expected at least the 8 named native cosmetic slots. Do not
            // obscure unknown game layouts or substitute dead display icons.
            int recognized=0;
            foreach (CosmeticEntry entry in _cosmeticEntries)
                if (entry.Native != null && Array.IndexOf(CosmeticSlotNames,entry.Native.name)>=0) recognized++;
            if (recognized<8)
            {
                if (!_cosmeticWarned)
                {
                    _cosmeticWarned=true;
                    Plugin.LogWarning("ArcaneUI wardrobe: expected 8 interactive native cosmetic slots, found " + recognized + "; keeping native Cosmetics visible.");
                }
                _cosmeticCustom.gameObject.SetActive(false);
                return;
            }
            SheetPalette p=Palette();
            int total=Mathf.Min(_cosmeticEntries.Count,16);
            int columns = CharacterSheetSettings.WardrobeLayout == "Gallery" ? 3 : 4;
            int rows=Mathf.CeilToInt(total/(float)columns);
            for(int i=0;i<total;i++)
            {
                CosmeticEntry entry=_cosmeticEntries[i];
                int col=i%columns, row=i/columns;
                float cell=1f/columns;
                float left=col*cell+.020f, right=(col+1)*cell-.020f;
                float top=1f-row/(float)rows-.012f;
                float bottom=1f-(row+1)/(float)rows+.012f;
                RectTransform frame=Box(_cosmeticGrid,"ArcaneUI_CosmeticFrame_"+i,
                    new Vector2(left,bottom),new Vector2(right,top),
                    CharacterSheetSettings.WardrobeLayout == "Minimal Slots" ? p.RowB : p.Slot,
                    Vector2.zero,Vector2.zero);
                if (CharacterSheetSettings.WardrobeLayout != "Minimal Slots")
                    OutlineBox(frame,p.Trim,1f);
                Box(frame,"AccentStripe",new Vector2(.06f,.955f),new Vector2(.94f,.968f),
                    p.Accent,Vector2.zero,Vector2.zero);
                RectTransform caption=Rect(frame,"SlotLabel",new Vector2(0f,.03f),
                    new Vector2(1f,.25f),Vector2.zero,Vector2.zero);
                CreateText(caption,"Name",entry.Label,10,TextAnchor.MiddleCenter,p.Accent);
                RectTransform nativeHost=Rect(frame,"NativeIcon",new Vector2(.5f,.67f),
                    new Vector2(.5f,.67f),Vector2.zero,Vector2.zero);
                nativeHost.sizeDelta=new Vector2(64f,64f);
                // Move the *real* native input container (not a copied icon).
                // The control is preserved by the existing reversible move log.
                MoveNative(entry.Native,nativeHost,true);
            }
            RectTransform playerInv=_native.Find("PlayerInv") as RectTransform;
            RectTransform nativeHelm=playerInv!=null?playerInv.Find("HideHelmToggle (1)") as RectTransform:null;
            if (nativeHelm!=null && nativeHelm.GetComponent<Toggle>()!=null)
            {
                Transform hint=_cosmeticHelmHost.Find("EmptyHelmHint");
                if(hint!=null) hint.gameObject.SetActive(false);
                MoveNative(nativeHelm,_cosmeticHelmHost,false);
            }
            _cosmeticMapped=true;
            Plugin.LogInfo("ArcaneUI wardrobe: rehosted " + total +
                " native cosmetic controls; original drag/drop and hide control components retained.");
        }
        private void ShowCosmeticWardrobe(bool shown)
        {
            if(_cosmeticCustom!=null) _cosmeticCustom.gameObject.SetActive(shown && _cosmeticMapped);
            if(_nativeCosmetic!=null) SetNativeVisibility(_nativeCosmetic,shown && _cosmeticMapped);
        }
        private void LeaveCosmeticWardrobe()
        {
            _cosmeticMapped=false;
            _cosmeticEntries.Clear();
            if(_cosmeticCustom!=null)
            {
                _cosmeticCustom.gameObject.SetActive(false);
                // Destroy visual hosts only after RestoreControls has returned
                // the real native item controls to their original hierarchy.
                if(_cosmeticGrid!=null)
                    for(int i=_cosmeticGrid.childCount-1;i>=0;i--)
                        UnityEngine.Object.Destroy(_cosmeticGrid.GetChild(i).gameObject);
                Transform caption=_cosmeticHelmHost!=null?
                    _cosmeticHelmHost.Find("EmptyHelmHint"):null;
                if(caption!=null) caption.gameObject.SetActive(true);
            }
        }
    }
}
