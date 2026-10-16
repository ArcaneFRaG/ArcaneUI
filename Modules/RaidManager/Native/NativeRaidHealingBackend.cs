using System;
using System.Collections;
using System.Reflection;
using UnityEngine;

namespace ArcanesGroupFrames
{
    internal static class NativeRaidHealingBackend
    {
        private const float DefaultHealAt =
            0.5f;


        // Native Erenshor convention:
        // RaidMemberSlot.Start creates healing assignments for indices 0..14.
        // SimPlayer.CheckHealsRaid queries index 14 when evaluating the
        // player character, so 14 is the player's authoritative raid-heal
        // threshold slot.
        internal const int PlayerHealAssignmentIndex =
            14;


        private static readonly FieldInfo
            AssignmentsField =
                typeof(RaidMemberSlot)
                    .GetField(
                        "MyHealAssignments",
                        BindingFlags.Instance |
                        BindingFlags.Public |
                        BindingFlags.NonPublic);


        internal static bool CanConfigure(
            RaidMemberSlot healer)
        {
            return healer != null &&
                   healer.AssignedAvatar != null &&
                   healer.Healer &&
                   GetAssignmentsList(
                       healer,
                       false) != null;
        }


        internal static float GetHealAt(
            RaidMemberSlot healer,
            RaidMemberSlot target)
        {
            if (target == null)
            {
                return DefaultHealAt;
            }


            return GetHealAtByIndex(
                healer,
                target.SlotIndex);
        }


        internal static float GetPlayerHealAt(
            RaidMemberSlot healer)
        {
            return GetHealAtByIndex(
                healer,
                PlayerHealAssignmentIndex);
        }


        private static float GetHealAtByIndex(
            RaidMemberSlot healer,
            int targetIndex)
        {
            if (healer == null)
            {
                return DefaultHealAt;
            }


            IList assignments =
                GetAssignmentsList(
                    healer,
                    false);


            if (assignments == null)
            {
                return DefaultHealAt;
            }


            for (int i = 0;
                 i < assignments.Count;
                 i++)
            {
                object assignment =
                    assignments[i];


                if (assignment == null ||
                    GetIntMember(
                        assignment,
                        "index",
                        -1) !=
                    targetIndex)
                {
                    continue;
                }


                return Mathf.Clamp01(
                    GetFloatMember(
                        assignment,
                        "healAt",
                        DefaultHealAt));
            }


            return DefaultHealAt;
        }


        internal static void SetHealAt(
            RaidMemberSlot healer,
            RaidMemberSlot target,
            float value)
        {
            if (target == null)
            {
                return;
            }


            SetHealAtByIndex(
                healer,
                target.SlotIndex,
                value);
        }


        internal static void SetPlayerHealAt(
            RaidMemberSlot healer,
            float value)
        {
            SetHealAtByIndex(
                healer,
                PlayerHealAssignmentIndex,
                value);
        }


        private static void SetHealAtByIndex(
            RaidMemberSlot healer,
            int targetIndex,
            float value)
        {
            if (healer == null)
            {
                return;
            }


            IList assignments =
                GetAssignmentsList(
                    healer,
                    true);


            if (assignments == null)
            {
                return;
            }


            float clamped =
                Mathf.Clamp01(
                    value);


            for (int i = 0;
                 i < assignments.Count;
                 i++)
            {
                object assignment =
                    assignments[i];


                if (assignment == null ||
                    GetIntMember(
                        assignment,
                        "index",
                        -1) !=
                    targetIndex)
                {
                    continue;
                }


                if (SetFloatMember(
                        assignment,
                        "healAt",
                        clamped))
                {
                    assignments[i] =
                        assignment;
                }


                return;
            }


            object created =
                CreateAssignment(
                    assignments,
                    targetIndex,
                    clamped);


            if (created != null)
            {
                assignments.Add(
                    created);
            }
        }


        internal static bool IsStillInRaid(
            RaidMemberSlot slot)
        {
            RaidManager manager =
                GameData.RaidManager;


            return manager != null &&
                   manager.Raiders != null &&
                   slot != null &&
                   manager.Raiders.Contains(
                       slot) &&
                   slot.AssignedSimTracking != null;
        }


        private static IList GetAssignmentsList(
            RaidMemberSlot healer,
            bool createIfMissing)
        {
            if (healer == null ||
                AssignmentsField == null)
            {
                return null;
            }


            object value =
                AssignmentsField.GetValue(
                    healer);


            IList list =
                value as IList;


            if (list != null ||
                !createIfMissing)
            {
                return list;
            }


            try
            {
                object created =
                    Activator.CreateInstance(
                        AssignmentsField.FieldType);


                AssignmentsField.SetValue(
                    healer,
                    created);


                return created as IList;
            }
            catch
            {
                return null;
            }
        }


        private static object CreateAssignment(
            IList assignments,
            int targetIndex,
            float healAt)
        {
            if (assignments == null)
            {
                return null;
            }


            Type listType =
                assignments.GetType();


            Type[] genericArguments =
                listType.GetGenericArguments();


            if (genericArguments == null ||
                genericArguments.Length !=
                    1)
            {
                return null;
            }


            Type assignmentType =
                genericArguments[0];


            object assignment =
                null;


            try
            {
                assignment =
                    Activator.CreateInstance(
                        assignmentType,
                        new object[]
                        {
                            targetIndex,
                            healAt
                        });
            }
            catch
            {
            }


            if (assignment == null)
            {
                try
                {
                    assignment =
                        Activator.CreateInstance(
                            assignmentType,
                            true);
                }
                catch
                {
                }
            }


            if (assignment == null)
            {
                return null;
            }


            SetIntMember(
                assignment,
                "index",
                targetIndex);


            SetFloatMember(
                assignment,
                "healAt",
                healAt);


            return assignment;
        }


        private static int GetIntMember(
            object target,
            string name,
            int fallback)
        {
            object value =
                GetMemberValue(
                    target,
                    name);


            if (value is int)
            {
                return (int)value;
            }


            try
            {
                return Convert.ToInt32(
                    value);
            }
            catch
            {
                return fallback;
            }
        }


        private static float GetFloatMember(
            object target,
            string name,
            float fallback)
        {
            object value =
                GetMemberValue(
                    target,
                    name);


            if (value is float)
            {
                return (float)value;
            }


            try
            {
                return Convert.ToSingle(
                    value);
            }
            catch
            {
                return fallback;
            }
        }


        private static object GetMemberValue(
            object target,
            string name)
        {
            if (target == null ||
                string.IsNullOrEmpty(
                    name))
            {
                return null;
            }


            Type type =
                target.GetType();


            FieldInfo field =
                type.GetField(
                    name,
                    BindingFlags.Instance |
                    BindingFlags.Public |
                    BindingFlags.NonPublic);


            if (field != null)
            {
                return field.GetValue(
                    target);
            }


            PropertyInfo property =
                type.GetProperty(
                    name,
                    BindingFlags.Instance |
                    BindingFlags.Public |
                    BindingFlags.NonPublic);


            return property != null &&
                   property.CanRead
                ? property.GetValue(
                    target,
                    null)
                : null;
        }


        private static bool SetIntMember(
            object target,
            string name,
            int value)
        {
            return SetMemberValue(
                target,
                name,
                value);
        }


        private static bool SetFloatMember(
            object target,
            string name,
            float value)
        {
            return SetMemberValue(
                target,
                name,
                value);
        }


        private static bool SetMemberValue(
            object target,
            string name,
            object value)
        {
            if (target == null ||
                string.IsNullOrEmpty(
                    name))
            {
                return false;
            }


            Type type =
                target.GetType();


            FieldInfo field =
                type.GetField(
                    name,
                    BindingFlags.Instance |
                    BindingFlags.Public |
                    BindingFlags.NonPublic);


            if (field != null)
            {
                field.SetValue(
                    target,
                    ConvertValue(
                        value,
                        field.FieldType));


                return true;
            }


            PropertyInfo property =
                type.GetProperty(
                    name,
                    BindingFlags.Instance |
                    BindingFlags.Public |
                    BindingFlags.NonPublic);


            if (property != null &&
                property.CanWrite)
            {
                property.SetValue(
                    target,
                    ConvertValue(
                        value,
                        property.PropertyType),
                    null);


                return true;
            }


            return false;
        }


        private static object ConvertValue(
            object value,
            Type destinationType)
        {
            if (value == null ||
                destinationType == null ||
                destinationType.IsInstanceOfType(
                    value))
            {
                return value;
            }


            try
            {
                return Convert.ChangeType(
                    value,
                    destinationType);
            }
            catch
            {
                return value;
            }
        }
    }
}
