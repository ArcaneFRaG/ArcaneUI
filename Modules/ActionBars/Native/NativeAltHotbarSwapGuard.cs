using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace ArcanesGroupFrames.Modules.ActionBars.Native
{
    /// <summary>
    /// Opt-out of Alt as a game control. First filter Unity legacy Alt key reads
    /// when a managed method can be patched; otherwise neutralize actual Alt
    /// native keybinding fields (never spell assignments or bar state). We do
    /// not call Input.GetKey for Alt from within an input patch.
    /// </summary>
    internal static class NativeAltHotbarSwapGuard
    {
        private static readonly BindingFlags Flags = BindingFlags.Public |
            BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
        private static readonly List<FieldInfo> NativeBindings = new List<FieldInfo>();
        private static bool _installed;
        private static int _managedHooks;
        private static int _nativeSwaps;
        private static int _cleared;
        private static float _nextBindingCheck;
        private static bool _loggedFallback;

        internal static void Install(Harmony harmony)
        {
            if (_installed || harmony == null) return;
            _installed = true;
            Type inputType = typeof(Input);
            MethodInfo codePrefix = typeof(NativeAltHotbarSwapGuard).GetMethod("FilterAltCode", Flags);
            MethodInfo textPrefix = typeof(NativeAltHotbarSwapGuard).GetMethod("FilterAltName", Flags);
            string[] methods = { "GetKey", "GetKeyDown", "GetKeyUp" };
            foreach (string name in methods)
            {
                TryPatchInput(harmony, inputType.GetMethod(name, Flags, null,
                    new[] { typeof(KeyCode) }, null), codePrefix);
                TryPatchInput(harmony, inputType.GetMethod(name, Flags, null,
                    new[] { typeof(string) }, null), textPrefix);
            }
            // Retain the original narrow guard as an additional backstop for
            // versions with a named swap entry point.
            HarmonyMethod guard = new HarmonyMethod(typeof(NativeAltHotbarSwapGuard)
                .GetMethod("AllowNativeSwap", Flags));
            Type[] types;
            try { types = typeof(Hotkeys).Assembly.GetTypes(); }
            catch (ReflectionTypeLoadException ex) { types = ex.Types; }
            catch { types = new Type[0]; }
            foreach (Type type in types)
            {
                if (type == null) continue;
                CollectBindingFields(type);
                MethodInfo[] methodsOnType;
                try { methodsOnType = type.GetMethods(Flags | BindingFlags.DeclaredOnly); }
                catch { continue; }
                foreach (MethodInfo method in methodsOnType)
                {
                    if (method == null || method.IsAbstract || method.ContainsGenericParameters ||
                        method.ReturnType != typeof(void) || method.GetParameters().Length != 0) continue;
                    string name = method.Name.ToLowerInvariant();
                    string owner = type.Name.ToLowerInvariant();
                    bool action = name.Contains("swap") || name.Contains("switch") || name.Contains("toggle");
                    bool bar = name.Contains("hotbar") || name.Contains("hotkeybar") ||
                        name.Contains("actionbar") || (name.Contains("bars") &&
                        (owner.Contains("hotbar") || owner.Contains("hotkey") || owner.Contains("actionbar")));
                    if (!action || !bar) continue;
                    try
                    {
                        if (method.GetMethodBody() == null) continue;
                        harmony.Patch(method, prefix: guard);
                        _nativeSwaps++;
                    }
                    catch (Exception ex)
                    {
                        Plugin.LogWarning("ArcaneUI Alt disable: native swap hook " +
                            type.Name + "." + method.Name + " failed: " + ex.GetType().Name);
                    }
                }
            }
            Plugin.LogInfo("ArcaneUI Alt disable: input hooks=" + _managedHooks +
                ", native swap hooks=" + _nativeSwaps +
                ", candidate native binding fields=" + NativeBindings.Count +
                ". Alt is reserved from game controls; operating-system shortcuts are unaffected.");
            if (_managedHooks < 6)
                Plugin.LogWarning("ArcaneUI Alt disable: some Unity input methods are native/internal calls " +
                    "and cannot be patched by Harmony. Native binding neutralization will be used where possible.");
        }

        private static void TryPatchInput(Harmony harmony, MethodInfo target, MethodInfo prefix)
        {
            if (target == null || prefix == null) return;
            try
            {
                // Don't attempt to detour a native internal-call stub without
                // managed IL: this is unsupported on some Unity Mono builds.
                if (target.GetMethodBody() == null) return;
                harmony.Patch(target, prefix: new HarmonyMethod(prefix));
                _managedHooks++;
            }
            catch (Exception ex)
            {
                Plugin.LogWarning("ArcaneUI Alt disable: " + target.Name + "(" +
                    target.GetParameters()[0].ParameterType.Name + ") patch unavailable (" +
                    ex.GetType().Name + ").");
            }
        }

        private static bool FilterAltCode(KeyCode key, ref bool __result)
        {
            if (key != KeyCode.LeftAlt && key != KeyCode.RightAlt) return true;
            __result = false;
            return false;
        }

        private static bool FilterAltName(string name, ref bool __result)
        {
            if (name == null) return true;
            string normalized = name.Trim().Replace(" ", "").Replace("_", "").ToLowerInvariant();
            if (normalized != "alt" && normalized != "leftalt" && normalized != "rightalt") return true;
            __result = false;
            return false;
        }

        private static bool AllowNativeSwap()
        {
            // When the direct GetKey hook works this is always true; the
            // Alt-triggered action was already suppressed at input read.
            return !Input.GetKey(KeyCode.LeftAlt) && !Input.GetKey(KeyCode.RightAlt);
        }

        private static bool IsNativeBinding(FieldInfo field)
        {
            if (field == null || field.IsLiteral || field.IsInitOnly ||
                field.FieldType != typeof(KeyCode)) return false;
            string name = field.Name.ToLowerInvariant();
            // Only actual saved input mappings; not last-key state, current
            // events, proficiency ranks, or the action bar assignment data.
            return name.Contains("key") || name.Contains("bind") || name.Contains("swap") ||
                name.Contains("hotbar") || name.Contains("hotkey") || name.Contains("alternate");
        }

        private static void CollectBindingFields(Type type)
        {
            try
            {
                foreach (FieldInfo field in type.GetFields(Flags | BindingFlags.DeclaredOnly))
                    if (IsNativeBinding(field)) NativeBindings.Add(field);
            }
            catch { }
        }

        internal static void Update()
        {
            if (!_installed || Time.unscaledTime < _nextBindingCheck) return;
            _nextBindingCheck = Time.unscaledTime + 8f;
            int changed = 0;
            for (int i = 0; i < NativeBindings.Count; i++)
            {
                FieldInfo field = NativeBindings[i];
                if (!field.IsStatic) continue;
                if (ClearBinding(field, null)) changed++;
            }
            MonoBehaviour[] nativeComponents = _managedHooks == 6
                ? new MonoBehaviour[0] : UnityEngine.Object.FindObjectsOfType<MonoBehaviour>();
            foreach (MonoBehaviour component in nativeComponents)
            {
                if (component == null || component.GetType().Assembly != typeof(Hotkeys).Assembly)
                    continue;
                Type t = component.GetType();
                foreach (FieldInfo field in NativeBindings)
                    if (!field.IsStatic && field.DeclaringType.IsAssignableFrom(t) &&
                        ClearBinding(field, component)) changed++;
            }
            if (changed > 0)
            {
                _cleared += changed;
                Plugin.LogInfo("ArcaneUI Alt disable: cleared " + changed +
                    " native Alt binding(s) this pass (total " + _cleared + ").");
            }
            else if (!_loggedFallback && _managedHooks == 0 && _nativeSwaps == 0)
            {
                _loggedFallback = true;
                Plugin.LogWarning("ArcaneUI Alt disable: no managed Alt input hook or named swap method. " +
                    "Native KeyCode mapping scan is active, but a game build using non-KeyCode " +
                    "bindings may still require manually unbinding Swap Hotbar in Erenshor controls.");
            }
        }

        private static bool ClearBinding(FieldInfo field, object owner)
        {
            try
            {
                KeyCode value = (KeyCode)field.GetValue(owner);
                if (value != KeyCode.LeftAlt && value != KeyCode.RightAlt) return false;
                field.SetValue(owner, KeyCode.None);
                return true;
            }
            catch { return false; }
        }
    }
}
