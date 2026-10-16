using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using ArcanesGroupFrames;

namespace ArcanesGroupFrames.Modules.UnitFrames.Target
{
    /// <summary>
    /// Pure presentation policy: player / SimPlayer colours always follow the
    /// native/FOTF class resolver; positively friendly NPCs are green; all other
    /// NPCs retain the configured target-frame health colour.
    ///
    /// Native NPC friendliness members vary across Erenshor builds. Probe only
    /// explicit disposition flags and enum/string disposition names, caching
    /// reflection metadata by type. Unknowns deliberately use the configured
    /// NPC colour rather than inventing a friendly status.
    /// </summary>
    internal static class TargetHealthColourResolver
    {
        internal enum Kind { ConfiguredNpc, FriendlyNpc, Class }

        internal static readonly Color FriendlyGreen = new Color(0.19f, 0.76f, 0.32f, 1f);

        private static readonly string[] FriendlyFlags =
        {
            "IsFriendlyToPlayer", "FriendlyToPlayer", "IsFriendly", "FriendlyNPC",
            "Friendly", "IsAllyToPlayer", "IsAlly", "NonHostile", "IsNonHostile",
            "NonAggressive", "IsNonAggressive", "IsNonCombatant", "NonCombatant",
            "IsMerchant", "IsVendor", "IsQuestGiver"
        };
        private static readonly string[] HostileFlags =
        {
            "IsHostileToPlayer", "HostileToPlayer", "IsHostile", "Hostile",
            "IsEnemy", "Enemy", "AggroOnSight", "AttackOnSight", "KillOnSight",
            "IsAggressive", "Aggressive"
        };
        private static readonly string[] DispositionNames =
        {
            "Disposition", "Attitude", "Reaction", "RelationshipToPlayer", "NpcDisposition",
            "FactionReaction", "FactionRelation", "FactionStatus", "FactionStanding",
            "ReactionToPlayer", "Faction"
        };
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public |
            BindingFlags.NonPublic | BindingFlags.IgnoreCase;

        private sealed class DispositionMembers
        {
            internal readonly MemberInfo[] Friendly;
            internal readonly MemberInfo[] Hostile;
            internal readonly MemberInfo[] Disposition;
            internal DispositionMembers(Type type)
            {
                Friendly = Find(type, FriendlyFlags, true);
                Hostile = Find(type, HostileFlags, true);
                Disposition = Find(type, DispositionNames, false);
            }
        }

        private static readonly Dictionary<Type, DispositionMembers> Cache =
            new Dictionary<Type, DispositionMembers>();

        internal static Kind Classify(Character target)
        {
            if (target == null) return Kind.ConfiguredNpc;
            if (!target.isNPC || (target.MyNPC != null && target.MyNPC.SimPlayer))
                return Kind.Class;
            if (target.MyStats != null && target.MyStats.Charmed)
                return Kind.FriendlyNpc;
            // Native merchant/banker service metadata takes precedence over
            // generic hostility flags. Known vanilla banker identities also
            // have a strictly exact-name fallback when flags are unavailable.
            // This avoids classifying legitimate bankers as hostile.
            if (!string.IsNullOrEmpty(ArcanesGroupFrames.Modules.Nameplates.Native.NpcRoleTagResolver.Resolve(target)))
                return Kind.FriendlyNpc;

            // Only classify as friendly when the game's NPC/Character/Stats
            // explicitly exposes that state. Neutral/unknown NPCs use the
            // configured target health colour, not a guessed green.
            bool friendly, hostile;
            Inspect(target.MyNPC, out friendly, out hostile);
            if (hostile) return Kind.ConfiguredNpc;
            if (friendly) return Kind.FriendlyNpc;
            Inspect(target, out friendly, out hostile);
            if (hostile) return Kind.ConfiguredNpc;
            if (friendly) return Kind.FriendlyNpc;
            Inspect(target.MyStats, out friendly, out hostile);
            return friendly && !hostile ? Kind.FriendlyNpc : Kind.ConfiguredNpc;
        }

        internal static void Resolve(Character target, Stats stats, string name,
            out bool overrideHealth, out Color colour)
        {
            Kind kind = Classify(target);
            overrideHealth = kind != Kind.ConfiguredNpc;
            colour = kind == Kind.Class ? RaidClassColors.GetColorForActor(stats, name)
                   : FriendlyGreen;
        }

        private static void Inspect(object actor, out bool friendly, out bool hostile)
        {
            friendly = false;
            hostile = false;
            if (actor == null) return;
            Type type = actor.GetType();
            DispositionMembers members;
            if (!Cache.TryGetValue(type, out members))
            {
                members = new DispositionMembers(type);
                Cache[type] = members;
            }
            foreach (MemberInfo m in members.Hostile)
            {
                object value = Read(m, actor);
                if (value is bool && (bool)value) hostile = true;
            }
            foreach (MemberInfo m in members.Friendly)
            {
                object value = Read(m, actor);
                if (value is bool && (bool)value) friendly = true;
            }
            foreach (MemberInfo m in members.Disposition)
            {
                object v = Read(m, actor);
                if (v == null) continue;
                string description = v.ToString().ToLowerInvariant();
                if (description == "friendly" || description == "ally" ||
                    description == "allied" || description == "friend") friendly = true;
                else if (description == "enemy" || description == "hostile" ||
                         description == "aggressive") hostile = true;
            }
        }

        private static MemberInfo[] Find(Type type, string[] names, bool boolOnly)
        {
            var result = new List<MemberInfo>();
            foreach (string name in names)
            {
                try
                {
                    FieldInfo field = type.GetField(name, Flags);
                    if (field != null && (!boolOnly || field.FieldType == typeof(bool)))
                    {
                        result.Add(field);
                        continue;
                    }
                    PropertyInfo prop = type.GetProperty(name, Flags);
                    if (prop != null && prop.GetIndexParameters().Length == 0 &&
                        (!boolOnly || prop.PropertyType == typeof(bool)))
                    {
                        result.Add(prop);
                        continue;
                    }
                    if (boolOnly)
                        foreach (MethodInfo method in type.GetMethods(Flags))
                            if (method.Name.Equals(name, StringComparison.OrdinalIgnoreCase) &&
                                method.ReturnType == typeof(bool) &&
                                method.GetParameters().Length == 0)
                            { result.Add(method); break; }
                }
                catch (AmbiguousMatchException) { /* Version-specific member overload. */ }
            }
            return result.ToArray();
        }

        private static object Read(MemberInfo member, object actor)
        {
            try
            {
                var field = member as FieldInfo;
                if (field != null) return field.GetValue(actor);
                var property = member as PropertyInfo;
                if (property != null) return property.GetValue(actor, null);
                var method = member as MethodInfo;
                if (method != null) return method.Invoke(actor, null);
            }
            catch (Exception) { /* Native state may not be initialized. */ }
            return null;
        }
    }
}
