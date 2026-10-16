using System.Diagnostics;
using HarmonyLib;
using Lunaris;
using ArcanesGroupFrames.Framework;
using ArcanesGroupFrames.Framework.Configuration.Commands;
using ArcanesGroupFrames.Framework.Modules;
using ArcanesGroupFrames.Modules.UnitFrames.Player;
using ArcanesGroupFrames.Framework.NativeUI.Player;
using ArcanesGroupFrames.Framework.NativeUI.Target;
using ArcanesGroupFrames.Framework.NativeUI.Status;
using ArcanesGroupFrames.Modules.UnitFrames.Target;
using ArcanesGroupFrames.Modules.Nameplates.Configuration;
using ArcanesGroupFrames.Modules.CooldownAuras.Configuration;
using ArcanesGroupFrames.Modules.CooldownAuras.Editor;
using ArcanesGroupFrames.Modules.CharacterSheet.Configuration;
using ArcanesGroupFrames.Modules.Inventory.Configuration;
using ArcanesGroupFrames.Modules.Experience.Configuration;
using ArcanesGroupFrames.Modules.Chat.Configuration;
using ArcanesGroupFrames.Modules.Minimap.Configuration;
using ArcanesGroupFrames.Modules.ActionBars.Native;

namespace ArcanesGroupFrames
{
    [LunarisPlugin(
        PluginName,
        PluginVersion,
        "Arcane",
        "Modular UI overhaul for Erenshor.")]
    [LunarisPermission(
        LunarisPermission.None)]
    public sealed class Plugin : LunarisPlugin
    {
        // ============================================================
        // PLUGIN INFORMATION
        // ============================================================

        internal const string PluginName =
            "ArcaneUI";

        internal const string PluginVersion =
            "1.1.1";


        // ============================================================
        // INSTANCE
        // ============================================================

        internal static Plugin Instance
        {
            get;
            private set;
        }


        private Harmony _harmony;

        internal ArcaneUiRuntime UiRuntime { get; private set; }


        // ============================================================
        // STARTUP
        // ============================================================

        private void Awake()
        {
            Instance =
                this;


            // --------------------------------------------------------
            // LOAD PERSISTENT SETTINGS FIRST
            // --------------------------------------------------------

            RaidFramesSettings
                .Load();

            PartyFramesSettings
                .Load();


            ArcaneUiModuleSettings.Load();
            PlayerFrameSettings.Load();
            NativePlayerHudBridge.Load();
            NativeCastBarBridge.Load();
            TargetFrameSettings.Load();
            NativeTargetHudBridge.Load();
            NativeBuffDebuffVisibility.Load();
            NameplateSettings.Load();
            CooldownAuraSettings.Load();
            CustomAuraLibrary.Load();
            CharacterSheetSettings.Load();
            InventorySettings.Load();
            ExperienceBarSettings.Load();
            ChatSettings.Load();
            MinimapSettings.Load();
            ArcanesGroupFrames.Modules.EnemyTracker.Configuration.EnemyTrackerSettings.Load();

            // Detect loading screens before creating persistent ArcaneUI canvases.
            NativeUiLayering.Initialize();

            UiRuntime = new ArcaneUiRuntime();
            UiRuntime.Initialize();

            // --------------------------------------------------------
            // HARMONY PATCHES
            //
            // Used by the mouseover casting target override.
            // --------------------------------------------------------

            _harmony =
                new Harmony(
                    "arcanes.groupframes");


            _harmony.PatchAll(
                typeof(Plugin).Assembly);
            ArcaneUiChatCommand.Install(_harmony);
            NativeAltHotbarSwapGuard.Install(_harmony);
            // --------------------------------------------------------
            // NATIVE RAID UI VISIBILITY
            //
            // The native raid-member list is automatically hidden while
            // a raid is active and restored when the raid ends.
            // --------------------------------------------------------

            RaidManagerVisibility
                .Initialize();


            NativeUiLayering
                .Initialize();


            // --------------------------------------------------------
            // RAID CONTROL PANEL
            // --------------------------------------------------------

            RaidControlPanel
                .Initialize();


            RaidMemberRolesPanel
                .Initialize();

            RaidHealingMatrixPanel
                .Initialize();

            RaidControlPanel.ApplyModuleSetting();


            // --------------------------------------------------------
            // CUSTOM RAID FRAMES
            // --------------------------------------------------------

            RaidFramesUI
                .Initialize();


            // --------------------------------------------------------
            // CUSTOM NORMAL PARTY FRAMES
            // --------------------------------------------------------

            PartyFramesUI
                .Initialize();



        }


        // ============================================================
        // UPDATE
        // ============================================================

        private void Update()
        {
            // Update loading/scene state FIRST. No ArcaneUI canvas is allowed
            // to render between the scene signal and its ordinary frame update.
            NativeUiLayering.Update();
            NativeAltHotbarSwapGuard.Update();
            bool showGameUi = NativeUiLayering.CanShowGameplayUi;
            if (UiRuntime != null) UiRuntime.SetGameplayVisible(showGameUi);
            PartyFramesUI.SetGameplayVisible(showGameUi);
            RaidFramesUI.SetGameplayVisible(showGameUi);
            RaidControlPanel.SetGameplayVisible(showGameUi);
            if (UiRuntime != null) UiRuntime.Update();


            // --------------------------------------------------------
            // AUTOMATIC NATIVE RAID UI VISIBILITY
            // --------------------------------------------------------

            RaidManagerVisibility
                .UpdateAutomatic();



            // --------------------------------------------------------
            // CUSTOM RAID CONTROLS
            // --------------------------------------------------------

            RaidControlPanel
                .Update();

            RaidLayoutPersistence
                .Update();

            RaidMemberRolesPanel
                .Update();

            RaidHealingMatrixPanel
                .Update();


            // --------------------------------------------------------
            // CUSTOM RAID FRAMES
            // --------------------------------------------------------

            RaidFramesUI
                .Update();


            // --------------------------------------------------------
            // CUSTOM NORMAL PARTY FRAMES
            // --------------------------------------------------------

            PartyFramesUI
                .Update();

        }


        // ============================================================
        // SHUTDOWN
        // ============================================================

        private void OnDestroy()
        {
            // Flush all module preferences before unload/character select.
            UnityEngine.PlayerPrefs.Save();
            try
            {

                // ----------------------------------------------------
                // RAID FRAMES
                // ----------------------------------------------------

                UnitFrameMouseoverState
                    .Reset();


                PartyFramesUI
                    .Shutdown();


                NativeGroupFramesVisibility
                    .Reset();


                RaidFramesUI
                    .Shutdown();


                // ----------------------------------------------------
                // RAID CONTROL PANEL
                // ----------------------------------------------------

                RaidHealingMatrixPanel
                    .Shutdown();

                RaidMemberRolesPanel
                    .Shutdown();

                NativeRaidRoleBackend
                    .Reset();

                RaidControlPanel
                    .Shutdown();


                // ----------------------------------------------------
                // RESTORE NATIVE RAID MANAGER
                // ----------------------------------------------------

                RaidManagerVisibility
                    .Reset();


                NativeUiLayering
                    .Shutdown();

                if (UiRuntime != null)
                {
                    UiRuntime.Shutdown();
                    UiRuntime = null;
                }


                // ----------------------------------------------------
                // REMOVE OUR HARMONY PATCHES
                // ----------------------------------------------------

                if (_harmony != null)
                {
                    _harmony.UnpatchSelf();
                    _harmony = null;
                }
            }
            finally
            {
                if (UiRuntime != null)
                {
                    UiRuntime.Shutdown();
                    UiRuntime = null;
                }
                Instance =
                    null;
            }
        }


        // ============================================================
        // LOGGING HELPERS
        // ============================================================

        // Diagnostic messages are compiled out of normal builds. To trace a
        // development build explicitly define ARCANEUI_DIAGNOSTICS.
        [Conditional("ARCANEUI_DIAGNOSTICS")]
        internal static void LogInfo(
            string message)
        {
            Instance?
                .Logging?
                .LogInfo(
                    message);
        }


        [Conditional("ARCANEUI_DIAGNOSTICS")]
        internal static void LogDiagnosticWarning(string message)
        {
            Instance?.Logging?.LogWarning(message);
        }

        // Genuine compatibility, user-action and fallback warnings remain on.
        internal static void LogWarning(
            string message)
        {
            Instance?
                .Logging?
                .LogWarning(
                    message);
        }


        internal static void LogError(
            string message)
        {
            Instance?
                .Logging?
                .LogError(
                    message);
        }
    }
}