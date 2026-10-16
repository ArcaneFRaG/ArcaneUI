using System;
using System.Reflection;
using UnityEngine;

namespace ArcanesGroupFrames
{
    internal static class RaidClassColors
    {
        // Cache a strongly typed delegate instead of allocating an object[]
        // and a reflection invocation on every visible frame refresh.
        private static Func<Stats, string, string> _fotfResolver;
        private static float _nextInteropProbe;

        // Phase 6 Player Frame and Party/Raid Frames share one class identity
        // source. Resolve the selected FOTF class BEFORE the native Arcanist-
        // backed class can select a fallback bar colour.
        internal static string ResolveClassNameForActor(Stats stats, string knownName)
        {
            string nativeName = stats != null && stats.CharacterClass != null
                ? stats.CharacterClass.ClassName : null;

            if (IsFOTFClass(nativeName))
                return nativeName;

            bool playerMatch =
                (stats != null && ReferenceEquals(stats, GameData.PlayerStats)) ||
                (!string.IsNullOrWhiteSpace(knownName) &&
                 GameData.PlayerStats != null &&
                 string.Equals(knownName, GameData.PlayerStats.MyName,
                     StringComparison.OrdinalIgnoreCase));

            // FOTF persists its selected class separately from the native
            // class object. Do not replace it with a native backing template.
            if (playerMatch)
            {
                string savedClass = ReadSavedPlayerClass();
                if (IsFOTFClass(savedClass))
                    return savedClass;
            }

            // Resolve by cached delegate: no per-frame MethodInfo.Invoke or
            // temporary object[] allocations. FOTF is optional.
            if (_fotfResolver == null && Time.realtimeSinceStartup >= _nextInteropProbe)
            {
                _nextInteropProbe = Time.realtimeSinceStartup + 2f;
                try
                {
                    Type bridge = Type.GetType(
                        "FateOfTheFallen.FOTFClassColorInterop, FateOfTheFallen", false);
                    MethodInfo method = bridge != null
                        ? bridge.GetMethod("ResolveClassName", BindingFlags.Public |
                            BindingFlags.Static, null,
                            new[] { typeof(Stats), typeof(string) }, null)
                        : null;
                    if (method != null)
                        _fotfResolver = (Func<Stats, string, string>)
                            Delegate.CreateDelegate(typeof(Func<Stats, string, string>), method);
                }
                catch { _fotfResolver = null; }
            }

            if (_fotfResolver != null)
            {
                try
                {
                    string customClass = _fotfResolver(stats, knownName);
                    if (IsFOTFClass(customClass))
                        return customClass;
                }
                catch { _fotfResolver = null; }
            }

            return nativeName ?? string.Empty;
        }

        // The GameData save-slot member name is stable in known Erenshor
        // builds, but is not part of ArcaneUI's compile-time contract.
        // Resolve it lazily so a game update or an absent slot safely falls
        // back to the native class rather than breaking the whole UI mod.
        private static readonly BindingFlags StaticFlags =
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
        private static readonly BindingFlags InstanceFlags =
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
        private static bool _slotLookupInitialized;
        private static PropertyInfo _slotProperty;
        private static FieldInfo _slotField;
        private static Type _slotType;
        private static PropertyInfo _charClassProperty;
        private static FieldInfo _charClassField;

        private static string ReadSavedPlayerClass()
        {
            try
            {
                if (!_slotLookupInitialized)
                {
                    _slotLookupInitialized = true;
                    Type dataType = typeof(GameData);
                    _slotProperty = dataType.GetProperty("CurrentCharacterSlot", StaticFlags);
                    _slotField = dataType.GetField("CurrentCharacterSlot", StaticFlags);
                }
                object slot = _slotProperty != null
                    ? _slotProperty.GetValue(null, null)
                    : _slotField != null ? _slotField.GetValue(null) : null;
                if (slot == null) return null;
                Type type = slot.GetType();
                if (_slotType != type)
                {
                    _slotType = type;
                    _charClassProperty = type.GetProperty("CharClass", InstanceFlags);
                    _charClassField = type.GetField("CharClass", InstanceFlags);
                }
                object value = _charClassProperty != null
                    ? _charClassProperty.GetValue(slot, null)
                    : _charClassField != null ? _charClassField.GetValue(slot) : null;
                return value as string;
            }
            catch
            {
                // Missing or changed save metadata must never break frames.
                return null;
            }
        }

        internal static Color GetColorForActor(Stats stats, string knownName)
        {
            return GetColor(ResolveClassNameForActor(stats, knownName));
        }

        private static bool IsFOTFClass(string name)
        {
            return string.Equals(name, "Blightcaller", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(name, "Emberweaver", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(name, "Lightbringer", StringComparison.OrdinalIgnoreCase);
        }

        internal static Color GetColor(string className)
        {
            if (string.IsNullOrWhiteSpace(className))
            {
                return DefaultColor;
            }

            switch (className.Trim().ToLowerInvariant())
            {
                // Paladin deliberately keeps the Group Frames pink.
                case "paladin": return Paladin;
                case "arcanist": return Arcanist;
                case "duelist": return Duelist;
                case "druid": return Druid;
                case "stormcaller": return Stormcaller;
                case "reaver": return Reaver;
                case "blightcaller": return Blightcaller;
                case "emberweaver": return Emberweaver;
                case "lightbringer": return Lightbringer;
                case "necromancer": return Necromancer;
                case "monk": return Monk;
                default: return DefaultColor;
            }
        }

        internal static readonly Color Paladin =
            new Color(0.96f, 0.55f, 0.73f, 0.96f);

        // Exact Erenshor Combat Meter rc2 palette for the remaining classes.
        internal static readonly Color Arcanist =
            new Color(0.48f, 0.35f, 0.88f, 1f);
        internal static readonly Color Duelist =
            new Color(0.94f, 0.42f, 0.18f, 1f);
        internal static readonly Color Druid =
            new Color(0.30f, 0.76f, 0.34f, 1f);
        internal static readonly Color Stormcaller =
            new Color(0.24f, 0.62f, 0.94f, 1f);
        internal static readonly Color Reaver =
            new Color(0.82f, 0.20f, 0.24f, 1f);
        internal static readonly Color Blightcaller =
            new Color(0.55f, 0.22f, 0.72f, 1f);
        internal static readonly Color Emberweaver =
            new Color(1.00f, 0.50f, 0.08f, 1f);
        // FOTF class identity is resolved from the saved character / FOTF
        // interop, not just Erenshor's native backing class.
        internal static readonly Color Lightbringer =
            new Color(1.00f, 1.00f, 1.00f, 1f);
        internal static readonly Color Necromancer =
            new Color(0.64f, 0.32f, 0.78f, 1f);
        internal static readonly Color Monk =
            new Color(0.78f, 0.62f, 0.31f, 1f);

        internal static readonly Color DefaultColor =
            new Color(0.18f, 0.52f, 0.22f, 1f);
    }
}
