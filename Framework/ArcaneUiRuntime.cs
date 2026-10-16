using System;
using ArcanesGroupFrames.Framework.Canvas;
using ArcanesGroupFrames.Framework.Configuration.UI;
using ArcanesGroupFrames.Framework.Modules;
using ArcanesGroupFrames.Framework.Layout.Editor;
using ArcanesGroupFrames.Framework.Theming;
using ArcanesGroupFrames.Framework.Configuration;
using ArcanesGroupFrames.Modules.UnitFrames.Player;
using ArcanesGroupFrames.Modules.UnitFrames.Target;
using ArcanesGroupFrames.Framework.NativeUI.Target;
using ArcanesGroupFrames.Framework.NativeUI.Player;
using ArcanesGroupFrames.Framework.NativeUI;
using ArcanesGroupFrames.Framework.NativeUI.Status;
using ArcanesGroupFrames.Modules.ActionBars.UI;
using ArcanesGroupFrames.Modules.Nameplates.UI;
using ArcanesGroupFrames.Modules.CooldownAuras.UI;
using ArcanesGroupFrames.Modules.CharacterSheet.Native;
using ArcanesGroupFrames.Modules.Inventory.Native;
using ArcanesGroupFrames.Modules.Experience.UI;
using ArcanesGroupFrames.Modules.Chat.Native;
using ArcanesGroupFrames.Modules.Minimap.Native;
using ArcanesGroupFrames.Modules.EnemyTracker.UI;

namespace ArcanesGroupFrames.Framework
{
    public sealed class ArcaneUiRuntime
    {
        public ArcaneCanvasManager Canvas { get; private set; }
        public ThemeManager Themes { get; private set; }
        internal ArcaneUiSettingsWindow Settings { get; private set; }
        internal ArcaneUiLayoutEditor LayoutEditor { get; private set; }
        public UiModuleRegistry Modules { get; private set; }
        internal ArcaneUiPlayerFrame PlayerFrame { get; private set; }
        internal ArcaneUiTargetFrame TargetFrame { get; private set; }
        internal NativeCharacterSheetDecorator CharacterSheet { get; private set; }
        internal NativeInventoryDecorator Inventory { get; private set; }
        internal NativeChatPresentation Chat { get; private set; }
        internal NativeMinimapPresentation Minimap { get; private set; }
        internal ArcaneUiEnemyTracker EnemyTracker { get; private set; }
        internal UnityEngine.RectTransform GetEnemyTrackerRect() { return EnemyTracker != null ? EnemyTracker.EditorRect : null; }
        private ArcaneUiNativeBarMirror _actionBarMirror;
        private ArcaneUiOverheadNameplates _nameplates;
        private ArcaneUiCooldownAuraTracker _cooldownAuras;
        private ArcaneUiExperienceBar _xpBar;
        private ArcaneUiCustomAuraHud _customAuraHud;
        internal UnityEngine.RectTransform GetActionBarRect(int index)
        {
            return _actionBarMirror != null ? _actionBarMirror.GetBarRect(index) : null;
        }
        internal UnityEngine.RectTransform GetAuraTrackerFrameRect(int index)
        {
            return _cooldownAuras != null ? _cooldownAuras.GroupRect(index) : null;
        }
        internal UnityEngine.RectTransform GetExperienceBarRect()
        {
            return _xpBar != null ? _xpBar.Rect : null;
        }
        internal void LogExperienceDiagnostics()
        {
            if (_xpBar != null) _xpBar.LogDiagnostics();
        }
        private bool _started;
        private bool _pendingSettingsToggle;
        private bool _uiVisible = true;

        internal bool IsBlockingGameplayInput
        {
            get { return ArcaneUiModalInputGuard.IsBlocking; }
        }

        internal bool IsCapturingRaidManagerHotkey
        {
            get { return Settings != null && Settings.IsCapturingRaidManagerHotkey; }
        }

        public void Initialize()
        {
            if (_started) return;
            Canvas = new ArcaneCanvasManager();
            Themes = new ThemeManager();
            Modules = new UiModuleRegistry();
            ArcaneUiStyleService.Initialize(Themes);
            ArcaneUiStyleService.Changed += OnStyleChanged;
            // Register only; the native-bound legacy lifecycle is intentionally
            // left untouched in Plugin during this compatibility-first stage.
            Modules.Register(new LegacyUiModule("party-frames"));
            Modules.Register(new LegacyUiModule("raid-frames"));
            Modules.Register(new LegacyUiModule("raid-manager"));
            Modules.Register(new LegacyUiModule(ArcaneUiModuleSettings.Player));
            Modules.Register(new LegacyUiModule(ArcaneUiModuleSettings.Target));
            Modules.Register(new LegacyUiModule(ArcaneUiModuleSettings.Character));
            Modules.Register(new LegacyUiModule(ArcaneUiModuleSettings.Inventory));
            try
            {
                Canvas.Initialize();
                Modules.InitializeAll();
                PlayerFrame = new ArcaneUiPlayerFrame(Canvas);
                PlayerFrame.Initialize();
                TargetFrame = new ArcaneUiTargetFrame(Canvas);
                TargetFrame.Initialize();
                _actionBarMirror = new ArcaneUiNativeBarMirror(Canvas);
                _nameplates = new ArcaneUiOverheadNameplates(Canvas);
                _cooldownAuras = new ArcaneUiCooldownAuraTracker(Canvas);
                _xpBar = new ArcaneUiExperienceBar(Canvas);
                _customAuraHud = new ArcaneUiCustomAuraHud(Canvas);
                CharacterSheet = new NativeCharacterSheetDecorator(Themes, Canvas);
                Inventory = new NativeInventoryDecorator(Themes);
                Chat = new NativeChatPresentation(Themes);
                Minimap = new NativeMinimapPresentation(Themes);
                EnemyTracker = new ArcaneUiEnemyTracker(Canvas);
                LayoutEditor = new ArcaneUiLayoutEditor(Canvas, Themes,
                    page => Settings.Open(page));
                Settings = new ArcaneUiSettingsWindow(Canvas, Themes, Modules,
                    () => { Settings.Close(); LayoutEditor.Open(); });
                Settings.Initialize();
                ArcaneUiModuleSettings.EnabledChanged += OnModuleChanged;
                _started = true;
            }
            catch (Exception)
            {
                ArcaneUiModuleSettings.EnabledChanged -= OnModuleChanged;
                ArcaneUiStyleService.Changed -= OnStyleChanged;
                ArcaneUiStyleService.Shutdown();
                if (LayoutEditor != null) { LayoutEditor.Shutdown(); LayoutEditor = null; }
                if (Settings != null) { Settings.Shutdown(); Settings = null; }
                if (PlayerFrame != null) { PlayerFrame.Shutdown(); PlayerFrame = null; }
                if (TargetFrame != null) { TargetFrame.Shutdown(); TargetFrame = null; }
                if (Inventory != null) { Inventory.Shutdown(); Inventory = null; }
                if (Chat != null) { Chat.Shutdown(); Chat = null; }
                if (Minimap != null) { Minimap.Shutdown(); Minimap = null; }
                if (EnemyTracker != null) { EnemyTracker.Shutdown(); EnemyTracker = null; }
                if (CharacterSheet != null) { CharacterSheet.Shutdown(); CharacterSheet = null; }
                if (_customAuraHud != null) { _customAuraHud.Shutdown(); _customAuraHud = null; }
                if (_cooldownAuras != null) { _cooldownAuras.Shutdown(); _cooldownAuras = null; }
                if (_xpBar != null) { _xpBar.Shutdown(); _xpBar = null; }
                if (_nameplates != null) { _nameplates.Shutdown(); _nameplates = null; }
                if (_actionBarMirror != null) { _actionBarMirror.Shutdown(); _actionBarMirror = null; }
                Modules.ShutdownAll();
                Canvas.Shutdown();
                throw;
            }
        }

        /// <summary>
        /// Defer opening until after native chat has finished submitting.
        /// There is intentionally only one supported command: /arcaneui.
        /// </summary>
        internal void QueueSettingsToggle()
        {
            if (!_started || Settings == null || !_uiVisible) return;
            _pendingSettingsToggle = true;
        }

        internal void SetGameplayVisible(bool visible)
        {
            if (!_started || Canvas == null) return;
            if (visible != _uiVisible)
            {
                _uiVisible = visible;
                if (!visible)
                {
                    _pendingSettingsToggle = false;
                    if (LayoutEditor != null && LayoutEditor.IsOpen)
                        LayoutEditor.Close(false);
                    if (Settings != null) Settings.Close();
                    if (_nameplates != null) _nameplates.Suspend();
                    if (CharacterSheet != null) CharacterSheet.Restore();
                    if (Inventory != null) Inventory.Restore();
                    if (Chat != null) Chat.Restore();
                    if (Minimap != null) Minimap.Restore();
                    if (EnemyTracker != null) EnemyTracker.RestoreNative();
                    ArcaneUiModalInputGuard.Shutdown();
                    NativePlayerHudBridge.Restore();
                    NativeCastBarBridge.Restore();
                    NativeTargetHudBridge.Restore();
                    NativeBuffDebuffVisibility.RestoreAll();
                }
            }
            Canvas.SetGameplayVisible(visible);
        }

        public void Update()
        {
            if (!_started || Settings == null || !_uiVisible) return;
            if (_pendingSettingsToggle)
            {
                _pendingSettingsToggle = false;
                if (LayoutEditor != null && LayoutEditor.IsOpen)
                    LayoutEditor.Close(false); // /arcaneui closes editor as a single toggle.
                else Settings.Toggle();
            }
            Settings.Update();
            if (LayoutEditor != null) LayoutEditor.Update();
            if (PlayerFrame != null) PlayerFrame.Update();
            if (TargetFrame != null) TargetFrame.Update();
            if (_actionBarMirror != null) _actionBarMirror.Update();
            if (_nameplates != null) _nameplates.Update();
            if (_cooldownAuras != null) _cooldownAuras.Update();
            if (_xpBar != null) _xpBar.Update();
            if (_customAuraHud != null) _customAuraHud.Update();
            if (CharacterSheet != null) CharacterSheet.Update();
            if (Inventory != null) Inventory.Update();
            if (Chat != null) Chat.Update();
            if (Minimap != null) Minimap.Update();
            if (EnemyTracker != null) EnemyTracker.Update();
            NativeBuffDebuffVisibility.Update();
            ArcaneUiModalInputGuard.Tick();
        }



        private void OnStyleChanged()
        {
            // Recolour currently visible member frames immediately without
            // destroying/rebuilding UI objects or their interaction handlers.
            PartyFramesUI.RefreshTheme();
            RaidFramesUI.RefreshTheme();
            RaidControlPanel.ApplyTheme();
            if (PlayerFrame != null) PlayerFrame.RefreshStyle();
            if (TargetFrame != null) TargetFrame.RefreshStyle();
            if (CharacterSheet != null) CharacterSheet.ApplyStyle();
            if (Inventory != null) Inventory.ApplyStyle();
            if (Chat != null) Chat.ApplyStyle();
            if (Minimap != null) Minimap.Refresh();
        }

        private void OnModuleChanged(string id, bool enabled)
        {
            // Preserve the native Erenshor UI when a replacement is off.
            // Each visual layer is independent; the raid-member list is
            // replaced by Raid Frames, not by the Raid Manager controls.
            if (id == ArcaneUiModuleSettings.Party)
            {
                PartyFramesUI.Update();
                if (!enabled) NativeGroupFramesVisibility.Reset();
            }
            else if (id == ArcaneUiModuleSettings.Raid)
            {
                RaidFramesUI.Update();
                RaidManagerVisibility.RefreshForModuleSetting();
            }
            else if (id == ArcaneUiModuleSettings.Manager)
            {
                RaidControlPanel.ApplyModuleSetting();
            }
            else if (id == ArcaneUiModuleSettings.Player)
            {
                if (!enabled) { NativePlayerHudBridge.Restore(); NativeCastBarBridge.Restore(); NativeBuffDebuffVisibility.Restore(NativeBuffDebuffVisibility.Channel.PlayerBuffs); NativeBuffDebuffVisibility.Restore(NativeBuffDebuffVisibility.Channel.PlayerDebuffs); }
                if (PlayerFrame != null) PlayerFrame.Update();
            }
            else if (id == ArcaneUiModuleSettings.Target)
            {
                if (!enabled) { NativeTargetHudBridge.Restore(); NativeBuffDebuffVisibility.Restore(NativeBuffDebuffVisibility.Channel.TargetBuffs); NativeBuffDebuffVisibility.Restore(NativeBuffDebuffVisibility.Channel.TargetDebuffs); }
                if (TargetFrame != null) TargetFrame.Update();
            }
            else if (id == ArcaneUiModuleSettings.Character)
            {
                if (CharacterSheet != null)
                {
                    if (!enabled) CharacterSheet.Restore();
                    else CharacterSheet.Update();
                }
            }
            else if (id == ArcaneUiModuleSettings.Inventory)
            {
                if (Inventory != null)
                {
                    if (!enabled) Inventory.Restore();
                    else Inventory.Update();
                }
            }
        }

        public void Shutdown()
        {
            ArcaneUiModalInputGuard.Shutdown();
            ArcaneUiModuleSettings.EnabledChanged -= OnModuleChanged;
            ArcaneUiStyleService.Changed -= OnStyleChanged;
            ArcaneUiStyleService.Shutdown();
            _pendingSettingsToggle = false;
            if (!_started) return;
            try
            {
                if (LayoutEditor != null) { LayoutEditor.Shutdown(); LayoutEditor = null; }
                if (Settings != null) { Settings.Shutdown(); Settings = null; }
                if (PlayerFrame != null) { PlayerFrame.Shutdown(); PlayerFrame = null; }
                if (TargetFrame != null) { TargetFrame.Shutdown(); TargetFrame = null; }
                if (Inventory != null) { Inventory.Shutdown(); Inventory = null; }
                if (Chat != null) { Chat.Shutdown(); Chat = null; }
                if (Minimap != null) { Minimap.Shutdown(); Minimap = null; }
                if (EnemyTracker != null) { EnemyTracker.Shutdown(); EnemyTracker = null; }
                if (CharacterSheet != null) { CharacterSheet.Shutdown(); CharacterSheet = null; }
                if (_customAuraHud != null) { _customAuraHud.Shutdown(); _customAuraHud = null; }
                if (_cooldownAuras != null) { _cooldownAuras.Shutdown(); _cooldownAuras = null; }
                if (_xpBar != null) { _xpBar.Shutdown(); _xpBar = null; }
                if (_nameplates != null) { _nameplates.Shutdown(); _nameplates = null; }
                if (_actionBarMirror != null) { _actionBarMirror.Shutdown(); _actionBarMirror = null; }
                Modules.ShutdownAll();
            }
            finally { NativeBuffDebuffVisibility.RestoreAll(); Canvas.Shutdown(); _started = false; }
        }
    }
}
