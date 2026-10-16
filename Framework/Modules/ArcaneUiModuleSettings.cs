using System;
using UnityEngine;

namespace ArcanesGroupFrames.Framework.Modules
{
    /// <summary>
    /// Persisted replacement switches. Gameplay code consults these switches
    /// before drawing custom frames or suppressing Erenshor's native UI.
    /// All modules default to enabled to preserve pre-migration behaviour.
    /// </summary>
    internal static class ArcaneUiModuleSettings
    {
        internal const string Party = "party-frames";
        internal const string Raid = "raid-frames";
        internal const string Manager = "raid-manager";
        internal const string Player = "player-frame";
        internal const string Target = "target-frame";
        internal const string Character = "character-sheet";
        internal const string Inventory = "inventory";

        private const string Prefix = "ArcaneUI.Modules.";
        private static bool _party = true;
        private static bool _raid = true;
        private static bool _manager = true;
        private static bool _player;
        private static bool _target;
        private static bool _character;
        private static bool _inventory;

        internal static event Action<string, bool> EnabledChanged;

        internal static bool PartyFramesEnabled { get { return _party; } }
        internal static bool RaidFramesEnabled { get { return _raid; } }
        internal static bool RaidManagerEnabled { get { return _manager; } }
        internal static bool PlayerFrameEnabled { get { return _player; } }
        internal static bool TargetFrameEnabled { get { return _target; } }
        internal static bool CharacterSheetEnabled { get { return _character; } }
        internal static bool InventoryEnabled { get { return _inventory; } }

        internal static void Load()
        {
            _party = PlayerPrefs.GetInt(Prefix + Party + ".Enabled", 1) != 0;
            _raid = PlayerPrefs.GetInt(Prefix + Raid + ".Enabled", 1) != 0;
            _manager = PlayerPrefs.GetInt(Prefix + Manager + ".Enabled", 1) != 0;
            _player = PlayerPrefs.GetInt(Prefix + Player + ".Enabled", 0) != 0;
            _target = PlayerPrefs.GetInt(Prefix + Target + ".Enabled", 0) != 0;
            _character = PlayerPrefs.GetInt(Prefix + Character + ".Enabled", 0) != 0;
            _inventory = PlayerPrefs.GetInt(Prefix + Inventory + ".Enabled", _character ? 1 : 0) != 0;
        }

        internal static bool IsEnabled(string id)
        {
            switch (id)
            {
                case Party: return _party;
                case Raid: return _raid;
                case Manager: return _manager;
                case Player: return _player;
                case Target: return _target;
                case Character: return _character;
                case Inventory: return _inventory;
                default: throw new ArgumentException("Unknown module: " + id, "id");
            }
        }

        internal static void SetEnabled(string id, bool enabled)
        {
            if (IsEnabled(id) == enabled) return;
            switch (id)
            {
                case Party: _party = enabled; break;
                case Raid: _raid = enabled; break;
                case Manager: _manager = enabled; break;
                case Player: _player = enabled; break;
                case Target: _target = enabled; break;
                case Character: _character = enabled; break;
                case Inventory: _inventory = enabled; break;
            }
            PlayerPrefs.SetInt(Prefix + id + ".Enabled", enabled ? 1 : 0);
            PlayerPrefs.Save();
            var changed = EnabledChanged;
            if (changed != null) changed(id, enabled);
        }
    }
}
