using UnityEngine;
using UnityEngine.UI;
using ArcanesGroupFrames.Modules.Chat.Configuration;
using ArcanesGroupFrames.Modules.Chat.Native;

namespace ArcanesGroupFrames.Framework.Configuration.UI
{
    internal sealed partial class ArcaneUiSettingsWindow
    {
        private string _chatPresetMessage = "Save or load chat presets for the current character.";
        private NativeChatPresentation ChatPresentation
        {
            get { return Plugin.Instance != null && Plugin.Instance.UiRuntime != null
                ? Plugin.Instance.UiRuntime.Chat : null; }
        }
        private void RefreshChat()
        {
            if (ChatPresentation != null) ChatPresentation.Refresh();
            if (_page == "chat") RenderChatPreview();
        }
        private void RenderChat()
        {
            switch (_tab)
            {
                case "Appearance":
                    Section("CHAT WINDOW THEMES", "Presentation of native chat; does not replace message delivery, native channel colours or keyboard input.");
                    DirectChoiceSetting("Chat Theme", "Arcane Dark, Refined Fantasy or Minimal Clean.",
                        ChatSettings.Preset, ChatSettings.Presets, v => { ChatSettings.SetPreset(v); RefreshChat(); });
                    ToggleSetting("Inherit Global Chat Theme", "Use the active ArcaneUI colours; choose individual colours below to override.",
                        () => ChatSettings.InheritTheme, v => { ChatSettings.SetBool("InheritTheme", v); RefreshChat(); });
                    HexSetting("Chat Accent Colour", "Applied to the non-interactive frame decoration.",
                        () => ChatSettings.AccentHex, v => { ChatSettings.SetHex("AccentHex", v); RefreshChat(); });
                    HexSetting("Chat Background Colour", "Used only when native chat tinting is enabled.",
                        () => ChatSettings.BackgroundHex, v => { ChatSettings.SetHex("BackgroundHex", v); RefreshChat(); });
                    DirectChoiceSetting("Chat Backdrop Style", "Solid, gradient or procedural bands. Affects only the ArcaneUI skin, never incoming chat messages.",
                        ChatSettings.BackgroundStyle, ChatSettings.BackgroundStyles, v => { ChatSettings.SetChoice("BackgroundStyle", v); RefreshChat(); });
                    HexSetting("Chat Gradient Second Colour", "Second gradient/band colour for the optional backdrop styles.",
                        () => ChatSettings.GradientHex, v => { ChatSettings.SetHex("GradientHex", v); RefreshChat(); });
                    ToggleSetting("Use Custom Chat Border Colour", "Choose a separate border colour instead of the theme accent.",
                        () => ChatSettings.CustomBorderColor, v => { ChatSettings.SetBool("CustomBorderColor", v); RefreshChat(); });
                    HexSetting("Custom Chat Border Colour", "Used when independent border colouring is enabled.",
                        () => ChatSettings.BorderHex, v => { ChatSettings.SetHex("BorderHex", v); RefreshChat(); });
                    ToggleSetting("Show Chat Border", "Theme border drawn behind native chat controls without blocking input.",
                        () => ChatSettings.Frame, v => { ChatSettings.SetBool("Frame", v); RefreshChat(); });
                    SliderSetting("Chat Border Thickness", 0f, 6f,
                        () => ChatSettings.BorderThickness, v => { ChatSettings.SetFloat("BorderThickness", v); RefreshChat(); }, " px", false);
                    ToggleSetting("Tint Native Chat Background", "Tint a validated native backdrop or draw a non-interactive surface behind the verified transcript.",
                        () => ChatSettings.TintBackground, v => { ChatSettings.SetBool("TintBackground", v); RefreshChat(); });
                    SliderSetting("Chat Background Opacity", 0f, 100f,
                        () => ChatSettings.BackgroundOpacity, v => { ChatSettings.SetFloat("BackgroundOpacity", v); RefreshChat(); }, "%");
                    DirectChoiceSetting("Chat Border Style", "Single, Double, Inset or None.",
                        ChatSettings.BorderStyle, ChatSettings.Borders, v => { ChatSettings.SetChoice("BorderStyle", v); RefreshChat(); });
                    SliderSetting("Chat Border Opacity", 0f, 100f,
                        () => ChatSettings.BorderOpacity, v => { ChatSettings.SetFloat("BorderOpacity", v); RefreshChat(); }, "%");
                    SliderSetting("Chat Horizontal Padding", 0f, 32f,
                        () => ChatSettings.PaddingX, v => { ChatSettings.SetFloat("PaddingX", v); RefreshChat(); }, " px");
                    SliderSetting("Chat Vertical Padding", 0f, 32f,
                        () => ChatSettings.PaddingY, v => { ChatSettings.SetFloat("PaddingY", v); RefreshChat(); }, " px");
                    break;
                case "Text":
                    Section("CHAT MESSAGE READABILITY", "Adjust native transcript text only. Chat input, text entry and channel-specific text colours are not modified.");
                    ToggleSetting("Customize Chat Text", "Allow ArcaneUI to adjust verified native chat log text sizes and line spacing.",
                        () => ChatSettings.StyleText, v => { ChatSettings.SetBool("StyleText", v); RefreshChat(); });
                    SliderSetting("Chat Font Size", 9f, 24f,
                        () => ChatSettings.FontSize, v => { ChatSettings.SetFloat("FontSize", v); RefreshChat(); }, " px");
                    SliderSetting("Chat Line Spacing", .8f, 2f,
                        () => ChatSettings.LineSpacing, v => { ChatSettings.SetFloat("LineSpacing", v); RefreshChat(); }, " x", false);
                    DirectChoiceSetting("Transcript Font Family", "Use the original Erenshor font or any available ArcaneUI font. Applies to native UI Text; TMP font assets remain native.",
                        ChatSettings.FontFamily, ChatSettings.FontFamilies, v => { ChatSettings.SetChoice("FontFamily", v); RefreshChat(); });
                    DirectChoiceSetting("Transcript Font Style", "Preserve native font styling or choose Normal, Bold or Italic.",
                        ChatSettings.FontStyle, ChatSettings.FontStyles, v => { ChatSettings.SetChoice("FontStyle", v); RefreshChat(); });
                    ToggleSetting("Chat Text Shadow", "Reversible drop shadow on verified native Unity Text transcript labels.",
                        () => ChatSettings.ShadowText, v => { ChatSettings.SetBool("ShadowText", v); RefreshChat(); });
                    ToggleSetting("Chat Text Outline", "Reversible readability outline on verified native Unity Text transcript labels.",
                        () => ChatSettings.OutlineText, v => { ChatSettings.SetBool("OutlineText", v); RefreshChat(); });
                    SliderSetting("Chat Text Effect Strength", 1f, 3f,
                        () => ChatSettings.TextEffectStrength, v => { ChatSettings.SetFloat("TextEffectStrength", v); RefreshChat(); }, " px", false);
                    Info("Text Effects", "Font style works on native UI Text and TMP. Font families, shadow and outline apply only to native UI Text; original appearance is restored when disabled.");
                    Info("Channel Colours", "Erenshor's existing message colours are preserved. ArcaneUI never recolours individual chat channels in this first pass.");
                    Info("Message Processing", "Native text, commands, whispers and message delivery remain untouched. Custom channel recolouring and timestamps need verified native message events, so are not injected into the transcript.");
                    break;
                case "Layout":
                    Section("CHAT POSITION & SIZE", "Move the real native chat, including its input and transcript together, when ArcaneUI verifies their common container.");
                    Info("Layout Safety", ChatPresentation != null ? ChatPresentation.LayoutStatus : "Chat module unavailable.");
                    ActionButton("Move Native Chat in Layout Editor", "Select NATIVE CHAT and drag its verified transcript outline; the native chat input moves with it.", "OPEN EDITOR", _openLayoutEditor);
                    ToggleSetting("Allow Moving Native Chat", "Allows native chat movement in both this tab and Layout Editor after native-root verification.",
                        () => ChatSettings.MoveNative, v => { ChatSettings.SetBool("MoveNative", v); RefreshChat(); });
                    SliderSetting("Chat Position X", -900f, 900f,
                        () => ChatSettings.OffsetX, v => { ChatSettings.SetFloat("OffsetX", v); RefreshChat(); }, " px");
                    SliderSetting("Chat Position Y", -900f, 900f,
                        () => ChatSettings.OffsetY, v => { ChatSettings.SetFloat("OffsetY", v); RefreshChat(); }, " px");
                    ToggleSetting("Allow Resizing Native Chat", "Works only when native chat viewport anchors indicate safe root resizing.",
                        () => ChatSettings.ResizeNative, v => { ChatSettings.SetBool("ResizeNative", v); RefreshChat(); });
                    SliderSetting("Native Chat Width", 0f, 1000f,
                        () => ChatSettings.Width, v => { ChatSettings.SetFloat("Width", v); RefreshChat(); }, " px");
                    SliderSetting("Native Chat Height", 0f, 750f,
                        () => ChatSettings.Height, v => { ChatSettings.SetFloat("Height", v); RefreshChat(); }, " px");
                    Info("Native Size", "0 = original size. If the detected native hierarchy cannot reflow safely, these values are ignored and the native geometry stays unchanged.");
                    ToggleSetting("Unlock Chat Drag Handles", "Show small MOVE and, where verified, RESIZE handles on the chat skin. Session-only; automatically relocks on restart.",
                        () => ChatSettings.EditLayout, v => { ChatSettings.SetBool("EditLayout", v); RefreshChat(); });
                    ActionButton("Reset Chat Position and Size", "Disable overrides and return to the current native root geometry.", "RESET LAYOUT",
                        () => { ChatSettings.ResetLayout(); RefreshChat(); RenderPage(); });
                    Section("SAVED CHAT PROFILES", "Three character-scoped presentation and position presets; does not save messages or alter native keybinds.");
                    Info("Chat Preset Status", _chatPresetMessage);
                    Info("Layout Persistence", "Use SAVE/LOAD in the main Layout Editor for its full-frame snapshots, or SAVE CHAT PRESET here for per-character chat appearance and position. A preset does not store chat messages.");
                    for (int slot = 1; slot <= ChatLayoutPresets.Slots; ++slot)
                    {
                        int chosen = slot;
                        ActionButton("Save Chat Preset " + slot, "Slot " + slot + ": " + ChatLayoutPresets.Description(slot), "SAVE " + slot,
                            () => { ChatLayoutPresets.Save(chosen, out _chatPresetMessage); RenderPage(); });
                        ActionButton("Load Chat Preset " + slot, "Restore the saved style, visibility and layout overrides for this character.", "LOAD " + slot,
                            () => { ChatLayoutPresets.Load(chosen, out _chatPresetMessage); RefreshChat(); RenderPage(); });
                    }
                    break;
                case "Visibility":
                    Section("CHAT SKIN VISIBILITY", "Only ArcaneUI decoration fades. Native messages, chat input and commands stay visible and interactive.");
                    DirectChoiceSetting("Chat Skin Visibility", "Always, fade on mouse leave, hide on mouse leave or show the skin only during combat. Native text/input remain visible.",
                        ChatSettings.SkinVisibility, ChatSettings.SkinVisibilityModes, v => { ChatSettings.SetChoice("SkinVisibility", v); RefreshChat(); });
                    SliderSetting("Unhovered Skin Opacity", 0f, 100f,
                        () => ChatSettings.IdleOpacity, v => { ChatSettings.SetFloat("IdleOpacity", v); RefreshChat(); }, "%");
                    Info("No Gameplay Suppression", "Combat Skin Only uses the existing native combat-state reader and fades only the optional skin. If combat state is unknown, decoration stays visible. The transcript and input never fade.");
                    break;
                default:
                    Section("ARCANEUI CHAT", "Opt-in appearance customization of Erenshor's live chat window, using its real native controls.");
                    ToggleSetting("Enable Themed Native Chat", "Safely decorate chat when native TypeText input and the scrolling transcript can both be verified. Off by default until scanned.",
                        () => ChatSettings.Enabled, v => { ChatSettings.SetBool("Enabled", v); RefreshChat(); });
                    Info("Getting Started", "Enable the theme, then choose Appearance, Text, Layout or Visibility. The native transcript, input and commands remain untouched.");
                    Info("Gameplay Unchanged", "Native chat input, slash commands, message sending, ScrollRect, whispers and channel messages continue to run through Erenshor.");
                    Section("ADVANCED CHAT CUSTOMIZATION", "These controls modify the actual Erenshor chat; there are no copied or mirrored chat windows.");
                    ActionButton("Edit Chat Appearance", "Borders, textures, opacity, padding and colours.", "APPEARANCE",
                        () => { _tab = "Appearance"; RenderPage(); });
                    ActionButton("Edit Chat Typography", "Font family, size, spacing, shadows and outline.", "TEXT",
                        () => { _tab = "Text"; RenderPage(); });
                    ActionButton("Edit Chat Position and Size", "Move or resize the real chat container and save per-character presets.", "LAYOUT",
                        () => { _tab = "Layout"; RenderPage(); });
                    ActionButton("Edit Chat Visibility", "Skin fading and opacity. Native chat messages remain visible.", "VISIBILITY",
                        () => { _tab = "Visibility"; RenderPage(); });
                    Info("Editor Access", "Choose Unlock UI / Layout, then NATIVE CHAT. Layout editing uses the same verified chat container as the advanced Chat Layout tab.");
                    break;
            }
        }
        private void RenderChatPreview()
        {
            if (_previewHost == null) return;
            ArcaneUiControls.ClearChildren(_previewHost);
            _previewHost.localScale = Vector3.one * .575f;
            _previewHost.sizeDelta = new Vector2(814f, 640f);
            Color accent = Theme.Accent, surface = Theme.Panel, ink = Theme.Text;
            if (!ChatSettings.InheritTheme)
            {
                Color c;
                if (ColorUtility.TryParseHtmlString(ChatSettings.AccentHex, out c)) accent = c;
                if (ColorUtility.TryParseHtmlString(ChatSettings.BackgroundHex, out c)) surface = c;
                if (ColorUtility.TryParseHtmlString(ChatSettings.TextHex, out c)) ink = c;
            }
            if (ChatSettings.Preset == "Refined Fantasy")
            {
                accent = Color.Lerp(accent, new Color(.83f, .63f, .33f), .45f);
                surface = Color.Lerp(surface, new Color(.13f, .1f, .12f), .5f);
            }
            else if (ChatSettings.Preset == "Minimal Clean")
            {
                accent = Color.Lerp(accent, new Color(.47f, .73f, .77f), .4f);
                surface = Color.Lerp(surface, new Color(.08f, .11f, .14f), .55f);
            }
            ArcaneUiControls.Label(_previewHost, "Title", "CHAT APPEARANCE  |  " + ChatSettings.Preset.ToUpperInvariant(),
                22f, 10f, 770f, 30f, 17, accent, TextAnchor.MiddleCenter, FontStyle.Bold);
            bool patterned = ChatSettings.BackgroundStyle != "Solid";
            RectTransform panel = ArcaneUiControls.Panel(_previewHost, "ChatSample", 65f, 64f, 684f, 487f,
                patterned ? Color.clear : new Color(surface.r, surface.g, surface.b, ChatSettings.BackgroundOpacity / 100f), false);
            if (patterned)
            {
                Color second = surface;
                ColorUtility.TryParseHtmlString(ChatSettings.GradientHex, out second);
                bool horizontal = ChatSettings.BackgroundStyle == "Horizontal Gradient";
                for (int i = 0; i < 20; ++i)
                {
                    var go = new GameObject("PreviewBackgroundBand_" + i, typeof(RectTransform), typeof(Image));
                    RectTransform r = go.GetComponent<RectTransform>();
                    r.SetParent(panel, false);
                    float a = i / 20f, b = (i + 1) / 20f;
                    r.anchorMin = horizontal ? new Vector2(a, 0f) : new Vector2(0f, a);
                    r.anchorMax = horizontal ? new Vector2(b, 1f) : new Vector2(1f, b);
                    r.offsetMin = Vector2.zero; r.offsetMax = Vector2.zero;
                    var image = go.GetComponent<Image>();
                    Color colour = ChatSettings.BackgroundStyle == "Soft Bands" ?
                        (i % 2 == 0 ? surface : Color.Lerp(surface, second, .35f)) :
                        Color.Lerp(surface, second, horizontal ? (i + .5f) / 20f : 1f - (i + .5f) / 20f);
                    colour.a = ChatSettings.BackgroundOpacity / 100f;
                    image.color = colour; image.raycastTarget = false;
                }
            }
            LinkPreview(panel.GetComponent<Image>(), "Appearance", "Chat Backdrop Style");
            if (ChatSettings.Frame && ChatSettings.BorderThickness >= .5f && ChatSettings.BorderStyle != "None")
            {
                var outline = panel.gameObject.AddComponent<Outline>();
                Color border = accent;
                if (ChatSettings.CustomBorderColor) ColorUtility.TryParseHtmlString(ChatSettings.BorderHex, out border);
                outline.effectColor = new Color(border.r, border.g, border.b, ChatSettings.BorderOpacity / 100f);
                outline.effectDistance = new Vector2(ChatSettings.BorderThickness, -ChatSettings.BorderThickness);
            }
            if (ChatSettings.Frame && ChatSettings.BorderThickness >= .5f && (ChatSettings.BorderStyle == "Double" || ChatSettings.BorderStyle == "Inset"))
            {
                RectTransform inner = ArcaneUiControls.Panel(panel, "InsetSample", 7f, 7f, 670f, 473f, Color.clear, false);
                var second = inner.gameObject.AddComponent<Outline>();
                Color border2 = accent;
                if (ChatSettings.CustomBorderColor) ColorUtility.TryParseHtmlString(ChatSettings.BorderHex, out border2);
                second.effectColor = ChatSettings.BorderStyle == "Double" ? border2 : Color.Lerp(border2, surface, .65f);
                second.effectDistance = new Vector2(1f, -1f);
            }
            Text headerText = ArcaneUiControls.Label(panel, "Header", "CHAT  /  NATIVE VIEW", 22f, 10f, 620f, 27f,
                15, accent, TextAnchor.MiddleLeft, FontStyle.Bold);
            LinkPreview(headerText, "Appearance", "Show Chat Border");
            string[] samples = {
                "[General] Adventurer: Looking for a group!",
                "[Party] Ally: Ready when you are.",
                "[Guild] Guildmate: See you in Port Azure.",
                "[Whisper] Friend: Found the quest item."
            };
            Color[] samplesInk = { ink, new Color(.6f,.82f,1f), new Color(.5f,.95f,.65f), new Color(.95f,.7f,.97f) };
            for (int i = 0; i < samples.Length; i++)
            {
                Text sampleText = ArcaneUiControls.Label(panel, "Message" + i, samples[i], 20f, 58f + i * 52f, 638f, 37f,
                    Mathf.RoundToInt(ChatSettings.FontSize), samplesInk[i], TextAnchor.MiddleLeft,
                    ChatSettings.FontStyle == "Bold" ? FontStyle.Bold : ChatSettings.FontStyle == "Italic" ? FontStyle.Italic : FontStyle.Normal);
                LinkPreview(sampleText, "Text", "Chat Font Size");
            }
            RectTransform input = ArcaneUiControls.Panel(panel, "Input", 18f, 407f, 648f, 48f,
                ArcaneUiControls.Multiply(surface, 1.3f), false);
            LinkPreview(input.GetComponent<Image>(), "Layout", "Chat Position X");
            Text inputHint = ArcaneUiControls.Label(input, "Hint", "Type a message...  (native Erenshor input)", 16f, 5f, 612f, 37f,
                15, ink, TextAnchor.MiddleLeft);
            inputHint.raycastTarget = false;
            ArcaneUiControls.Label(_previewHost, "Foot", "CLICK ANY CHAT PREVIEW AREA TO EDIT  •  ERENSHOR CHAT REMAINS NATIVE",
                58f, 564f, 710f, 26f, 12, Theme.Text, TextAnchor.MiddleCenter);
        }
    }
}
