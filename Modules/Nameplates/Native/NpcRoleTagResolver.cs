using System;
using System.Collections.Generic;
using System.Reflection;

namespace ArcanesGroupFrames.Modules.Nameplates.Native
{
    /// <summary>
    /// Display-only NPC service tags. Prefer native service metadata; some
    /// vanilla Erenshor bankers have no exposed boolean banker flag, so use
    /// a deliberately small exact-name fallback for documented bank NPCs.
    /// Do not infer NPC roles from partial names or nearby bank objects.
    /// </summary>
    internal static class NpcRoleTagResolver
    {
        private static readonly string[] BankFields =
            { "isBanker", "IsBanker", "Banker", "isBank", "IsBank", "IsBankNPC", "BankNPC", "CanBank" };
        private static readonly string[] MerchantFields =
            { "isVendor", "IsVendor", "isMerchant", "IsMerchant" };
        private static readonly string[] RoleFields =
            { "NPCRole", "NpcRole", "ServiceRole", "ServiceType", "MerchantType", "VendorType", "Role" };

        // Explicit vanilla bankers with documented banking interactions.
        // These are exact character names, not fuzzy name/title matching.
        // This fallback is reached only when native banker metadata is absent.
        private static readonly HashSet<string> KnownBankers = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Validus Greencent",    // Stowaway's Step
            "Prestigio Valusha",   // Port Azure
            "Comstock Retalio",    // Ripper's Keep
            "Wealthen Giallara"    // Willowwatch Ridge
        };

        private const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic |
            BindingFlags.Instance | BindingFlags.IgnoreCase;

        internal static string Resolve(Character actor)
        {
            if (actor == null || actor.MyNPC == null || actor.MyNPC.SimPlayer) return "";

            // Bankers take precedence: a bank NPC may also carry a generic
            // vendor flag depending on the Erenshor build.
            if (Flag(actor, BankFields) || Flag(actor.MyNPC, BankFields) ||
                HasBankServiceRole(actor) || HasBankServiceRole(actor.MyNPC) ||
                IsKnownBanker(actor.MyNPC.NPCName) ||
                (actor.MyStats != null && IsKnownBanker(actor.MyStats.MyName)))
                return "Banker";

            if (actor.isVendor || Flag(actor.MyNPC, MerchantFields)) return "Vendor";
            return "";
        }

        private static bool IsKnownBanker(string name)
        {
            return !string.IsNullOrEmpty(name) && KnownBankers.Contains(name.Trim());
        }

        private static bool HasBankServiceRole(object obj)
        {
            if (obj == null) return false;
            Type type = obj.GetType();
            foreach (string name in RoleFields)
            {
                try
                {
                    FieldInfo field = type.GetField(name, Flags);
                    if (field != null && IsBankRoleValue(field.GetValue(obj))) return true;
                    PropertyInfo property = type.GetProperty(name, Flags);
                    if (property != null && property.GetIndexParameters().Length == 0 &&
                        IsBankRoleValue(property.GetValue(obj, null))) return true;
                }
                catch { /* Optional native member missing or inaccessible. */ }
            }
            return false;
        }

        private static bool IsBankRoleValue(object value)
        {
            // Only actual role strings/enums; numeric values are not reliable
            // indicators of an NPC's service in different game versions.
            if (!(value is string) && !(value is Enum)) return false;
            string role = value.ToString().Trim();
            return string.Equals(role, "Banker", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(role, "Bank", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(role, "BankKeeper", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(role, "Bank NPC", StringComparison.OrdinalIgnoreCase);
        }

        private static bool Flag(object obj, string[] names)
        {
            if (obj == null) return false;
            Type type = obj.GetType();
            foreach (string name in names)
            {
                try
                {
                    FieldInfo f = type.GetField(name, Flags);
                    if (f != null && f.FieldType == typeof(bool) && (bool)f.GetValue(obj)) return true;
                    PropertyInfo p = type.GetProperty(name, Flags);
                    if (p != null && p.PropertyType == typeof(bool) &&
                        p.GetIndexParameters().Length == 0 && (bool)p.GetValue(obj, null)) return true;
                }
                catch { /* Optional native member missing or inaccessible. */ }
            }
            return false;
        }
    }
}
