using System;
using UnityEngine;
using ArcanesGroupFrames;
using UnityEngine.UI;
using ArcanesGroupFrames.Modules.UnitFrames.Player;

namespace ArcanesGroupFrames.Framework.Configuration.UI
{
    internal sealed partial class ArcaneUiSettingsWindow
    {
        private void LinkFOTFPreview(Image image, string tab, string label)
        {
            if (image == null) return;
            // Ignore children on live unit frames. Only the preview's own
            // background is a raycast target; fill and label stay transparent.
            image.raycastTarget = true;
            var button = image.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.targetGraphic = image;
            button.onClick.AddListener(() => JumpToSetting("fotf", tab, label));
            var highlight = image.gameObject.AddComponent<ArcaneUiPreviewHoverHighlight>();
            highlight.Initialize(Theme.Accent, false,
                (source, hovered) => OnPreviewHover(source, hovered, tab + " / " + label));
        }

        private static string NextFOTFChoice(string[] names, string selected)
        {
            for (int i = 0; i < names.Length; i++)
                if (string.Equals(names[i], selected, StringComparison.OrdinalIgnoreCase))
                    return names[(i + 1) % names.Length];
            return names[0];
        }

        private void FOTFChange()
        {
            UpdateFOTFResourcePreview();
            RefreshPlayerLayout();
        }

        private void FOTFChoice(string label,string description,string[] choices,
            Func<string> current,Action<string> apply)
        {
            ActionButton(label,description + "  Current: " + current(),"CHANGE", () =>
            {
                apply(NextFOTFChoice(choices,current()));
                FOTFChange(); RenderPage();
            });
        }

        private void RenderFOTFResourceOptionsFull()
        {
            Info("Supported numeric resource", "Emberweaver Heat / Flashfire. " +
                "Blightcaller and Lightbringer use regular aura and shield trackers.");
            if (_page == "player")
                ActionButton("Open FOTF settings", "Independent styles for Heat and Flashfire.",
                    "CUSTOMIZE", () => SelectPage("fotf"));
            string tab = _page == "player" ? "General" : _tab;
            if (tab == "General")
            {
                Section("RESOURCE INTEGRATION","Live Emberweaver resources are read from FOTF gameplay, never simulated outside the preview.");
                ToggleSetting("Show FOTF Resources","ArcaneUI displays FOTF class resources without changing gameplay values.",
                    () => FOTFResourceSettings.Enabled,v => { FOTFResourceSettings.SetEnabled(v); FOTFChange(); });
                ToggleSetting("Show Resource Numbers","Master label toggle; each resource also has its own label mode.",
                    () => FOTFResourceSettings.ShowNumbers,v => { FOTFResourceSettings.SetShowNumbers(v); FOTFChange(); });
                Info("Click to edit preview","Hover Heat or Flashfire to highlight it, then click to edit that resource.");
                ActionButton("FOTF Layout Editor","Independently move detached resources. Attached strips follow the player frame.",
                    "UNLOCK UI",_openLayoutEditor);
                Info("Resource mapping","Emberweaver Heat (0-10) and Flashfire (ready/not ready) automatically follow FOTF gameplay. Other implemented FOTF classes use normal aura and shield tracking.");
                Info("Preview counts","Emberweaver shows live Heat/Flashfire when playing the class, otherwise a design sample.");
                Info("Preview visibility","The Primary and Secondary previews represent Heat and Flashfire.");
                return;
            }
            FOTFResourcePart part = tab == "Secondary" ? FOTFResourcePart.Secondary : FOTFResourcePart.Primary;
            string prefix = tab;
            Section(prefix + " RESOURCE","Independent appearance, dimensions, positioning and typography. Changes apply to both preview and the actual FOTF widget.");
            ToggleSetting(prefix + " Visible","Show this resource whenever FOTF reports it for the current class.",
                () => FOTFResourceSettings.Visible(part),v => { FOTFResourceSettings.SetVisible(part,v); FOTFChange(); });
            ToggleSetting(prefix + " Detached","Allow this element to move separately in the Layout Editor.",
                () => FOTFResourceSettings.Detached(part),v => { FOTFResourceSettings.SetDetached(part,v); FOTFChange(); });
            Section("DIMENSIONS AND POSITION");
            SliderSetting(prefix+" Width",40f,175f,()=>FOTFResourceSettings.WidthScale(part),
                v=> { FOTFResourceSettings.SetWidthScale(part,v); FOTFChange(); }," %");
            SliderSetting(prefix+" Height",8f,48f,()=>FOTFResourceSettings.PartHeight(part),
                v=> { FOTFResourceSettings.SetPartHeight(part,v); FOTFChange(); }," px");
            SliderSetting(prefix+" Cell Gap",0f,12f,()=>FOTFResourceSettings.Gap(part),
                v=> { FOTFResourceSettings.SetGap(part,v); FOTFChange(); }," px");
            SliderSetting(prefix+" X Offset",-150f,150f,()=>FOTFResourceSettings.XOffset(part),
                v=> { FOTFResourceSettings.SetXOffset(part,v); FOTFChange(); }," px");
            SliderSetting(prefix+" Y Gap",0f,150f,()=>FOTFResourceSettings.YOffset(part),
                v=> { FOTFResourceSettings.SetYOffset(part,v); FOTFChange(); }," px");
            Section("COLOURS AND TEXTURES");
            string baseFill = part == FOTFResourcePart.Primary ?
                FOTFResourceSettings.ChargeColour : "#FF9C2A";
            HexSetting(prefix+" Fill Colour","Active pips or continuous progress fill.",
                ()=>FOTFResourceSettings.FillColour(part,baseFill),v=> { FOTFResourceSettings.SetPartColour(part,"FillColour",v); FOTFChange(); });
            HexSetting(prefix+" Empty Colour","Unfilled pips on segmented resources.",
                ()=>FOTFResourceSettings.EmptyColour(part),v=> { FOTFResourceSettings.SetPartColour(part,"EmptyColour",v); FOTFChange(); });
            HexSetting(prefix+" Background Colour","Colour behind the resource.",
                ()=>FOTFResourceSettings.BackgroundColour(part),v=> { FOTFResourceSettings.SetPartColour(part,"BackgroundColour",v); FOTFChange(); });
            HexSetting(prefix+" Border Colour","Independent outline tint.",
                ()=>FOTFResourceSettings.BorderColour(part),v=> { FOTFResourceSettings.SetPartColour(part,"BorderColour",v); FOTFChange(); });
            SliderSetting(prefix+" Fill Opacity",0f,100f,()=>FOTFResourceSettings.FillOpacity(part)*100f,
                v=> { FOTFResourceSettings.SetFillOpacity(part,v); FOTFChange(); }," %");
            SliderSetting(prefix+" Background Opacity",0f,100f,()=>FOTFResourceSettings.BackgroundOpacity(part)*100f,
                v=> { FOTFResourceSettings.SetBackgroundOpacity(part,v); FOTFChange(); }," %");
            SliderSetting(prefix+" Border Thickness",0f,6f,()=>FOTFResourceSettings.BorderSize(part),
                v=> { FOTFResourceSettings.SetBorderSize(part,v); FOTFChange(); }," px");
            FOTFChoice(prefix+" Bar Texture","Use ArcaneUI's existing bar texture catalogue.",
                UnitFrameAppearance.TextureNames,()=>FOTFResourceSettings.Texture(part),
                v=>FOTFResourceSettings.SetTexture(part,v));
            FOTFChoice(prefix+" Fill Direction","Controls which end fills first.",
                new [] {"Left to Right","Right to Left"},()=>FOTFResourceSettings.Direction(part),
                v=>FOTFResourceSettings.SetDirection(part,v));
            Section("TEXT & LABELS");
            FOTFChoice(prefix+" Text Mode","Name and value, name only, value only, or hidden.",
                new [] {"Name + Values","Name Only","Values Only","Hidden"},
                ()=>FOTFResourceSettings.TextMode(part),v=>FOTFResourceSettings.SetTextMode(part,v));
            FOTFChoice(prefix+" Font","Native-safe font selection using the existing ArcaneUI list.",
                UnitFrameAppearance.FontNames,()=>FOTFResourceSettings.Font(part),v=>FOTFResourceSettings.SetFont(part,v));
            SliderSetting(prefix+" Font Size",8f,30f,()=>FOTFResourceSettings.FontSize(part),
                v=> { FOTFResourceSettings.SetFontSize(part,v); FOTFChange(); }," px");
            ToggleSetting(prefix+" Bold Text","Embolden the resource label.",
                ()=>FOTFResourceSettings.Bold(part),v=> { FOTFResourceSettings.SetBold(part,v); FOTFChange(); });
            ToggleSetting(prefix+" Text Outline","Improve label contrast against bright fills.",
                ()=>FOTFResourceSettings.OutlineText(part),v=> { FOTFResourceSettings.SetOutlineText(part,v); FOTFChange(); });
            HexSetting(prefix+" Text Colour","Label tint independent of the resource fill.",
                ()=>FOTFResourceSettings.TextColour(part),v=> { FOTFResourceSettings.SetPartColour(part,"TextColour",v); FOTFChange(); });
            FOTFChoice(prefix+" Text Alignment","Horizontal label alignment.",
                new [] {"Left","Center","Right"},()=>FOTFResourceSettings.Alignment(part),
                v=>FOTFResourceSettings.SetAlignment(part,v));
            SliderSetting(prefix+" Label X",-50f,50f,()=>FOTFResourceSettings.LabelOffsetX(part),
                v=> { FOTFResourceSettings.SetLabelOffsetX(part,v); FOTFChange(); }," px");
            SliderSetting(prefix+" Label Y",-30f,30f,()=>FOTFResourceSettings.LabelOffsetY(part),
                v=> { FOTFResourceSettings.SetLabelOffsetY(part,v); FOTFChange(); }," px");
            ActionButton(prefix+" Reset Style","Restore this strip to its default look without deleting detached positions or other resource styles.",
                "RESET",()=> { FOTFResourceSettings.ResetPart(part); FOTFChange(); RenderPage(); });
            ActionButton("FOTF Layout Editor","Move this resource after detaching it, then save exact X/Y with the existing editor.","UNLOCK UI",_openLayoutEditor);
        }
    }
}
