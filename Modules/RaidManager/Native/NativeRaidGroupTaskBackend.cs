using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

namespace ArcanesGroupFrames
{
    internal static class NativeRaidGroupTaskBackend
    {
        private const int RaidGroupCount =
            3;


        internal static List<string> GetOptions(
            int group)
        {
            List<string> result =
                new List<string>();


            object dropdown =
                ResolveNativeDropdown(
                    group);


            if (dropdown != null)
            {
                object options =
                    GetMemberValue(
                        dropdown,
                        "options");


                IEnumerable enumerable =
                    options as IEnumerable;


                if (enumerable != null)
                {
                    foreach (object option
                             in enumerable)
                    {
                        string text =
                            ReadOptionText(
                                option);


                        if (!string.IsNullOrEmpty(
                                text))
                        {
                            result.Add(
                                text);
                        }
                    }
                }
            }


            if (result.Count == 0)
            {
                ReadFallbackTaskList(
                    group,
                    result);
            }


            return result;
        }


        internal static int GetValue(
            int group)
        {
            object dropdown =
                ResolveNativeDropdown(
                    group);


            if (dropdown == null)
            {
                return 0;
            }


            object value =
                GetMemberValue(
                    dropdown,
                    "value");


            try
            {
                return Convert.ToInt32(
                    value);
            }
            catch
            {
                return 0;
            }
        }


        internal static bool SetValue(
            int group,
            int value)
        {
            object dropdown =
                ResolveNativeDropdown(
                    group);


            if (dropdown == null)
            {
                return false;
            }


            Type type =
                dropdown.GetType();


            MethodInfo setWithoutNotify =
                type.GetMethod(
                    "SetValueWithoutNotify",
                    BindingFlags.Instance |
                    BindingFlags.Public |
                    BindingFlags.NonPublic,
                    null,
                    new Type[]
                    {
                        typeof(int)
                    },
                    null);


            bool valueApplied =
                false;


            if (setWithoutNotify != null)
            {
                try
                {
                    setWithoutNotify.Invoke(
                        dropdown,
                        new object[]
                        {
                            value
                        });


                    valueApplied =
                        true;
                }
                catch
                {
                }
            }


            if (!valueApplied)
            {
                PropertyInfo valueProperty =
                    type.GetProperty(
                        "value",
                        BindingFlags.Instance |
                        BindingFlags.Public |
                        BindingFlags.NonPublic);


                if (valueProperty != null &&
                    valueProperty.CanWrite)
                {
                    try
                    {
                        valueProperty.SetValue(
                            dropdown,
                            value,
                            null);


                        valueApplied =
                            true;
                    }
                    catch
                    {
                    }
                }
            }


            if (!valueApplied)
            {
                return false;
            }


            bool invoked =
                InvokeNativeValueChanged(
                    dropdown,
                    value);


            MethodInfo refreshShown =
                type.GetMethod(
                    "RefreshShownValue",
                    BindingFlags.Instance |
                    BindingFlags.Public |
                    BindingFlags.NonPublic);


            if (refreshShown != null)
            {
                try
                {
                    refreshShown.Invoke(
                        dropdown,
                        null);
                }
                catch
                {
                }
            }


            return invoked ||
                   valueApplied;
        }


        private static object ResolveNativeDropdown(
            int group)
        {
            if (group < 1 ||
                group > RaidGroupCount)
            {
                return null;
            }


            RaidManager manager =
                GameData.RaidManager;


            if (manager == null)
            {
                return null;
            }


            string memberName =
                "G" +
                group +
                "TaskDD";


            return GetMemberValue(
                manager,
                memberName);
        }


        private static bool InvokeNativeValueChanged(
            object dropdown,
            int value)
        {
            object eventObject =
                GetMemberValue(
                    dropdown,
                    "onValueChanged");


            if (eventObject == null)
            {
                return false;
            }


            MethodInfo invoke =
                eventObject
                    .GetType()
                    .GetMethod(
                        "Invoke",
                        BindingFlags.Instance |
                        BindingFlags.Public |
                        BindingFlags.NonPublic,
                        null,
                        new Type[]
                        {
                            typeof(int)
                        },
                        null);


            if (invoke == null)
            {
                return false;
            }


            try
            {
                invoke.Invoke(
                    eventObject,
                    new object[]
                    {
                        value
                    });


                return true;
            }
            catch
            {
                return false;
            }
        }


        private static void ReadFallbackTaskList(
            int group,
            List<string> destination)
        {
            RaidManager manager =
                GameData.RaidManager;


            if (manager == null ||
                destination == null)
            {
                return;
            }


            object value =
                GetMemberValue(
                    manager,
                    "Group" +
                    group +
                    "Tasks");


            IEnumerable enumerable =
                value as IEnumerable;


            if (enumerable == null)
            {
                return;
            }


            foreach (object item
                     in enumerable)
            {
                if (item == null)
                {
                    continue;
                }


                string text =
                    ReadOptionText(
                        item);


                if (string.IsNullOrEmpty(
                        text))
                {
                    text =
                        item.ToString();
                }


                if (!string.IsNullOrEmpty(
                        text))
                {
                    destination.Add(
                        text);
                }
            }
        }


        private static string ReadOptionText(
            object option)
        {
            if (option == null)
            {
                return "";
            }


            object text =
                GetMemberValue(
                    option,
                    "text");


            if (text != null)
            {
                return text.ToString();
            }


            if (option is string)
            {
                return (string)option;
            }


            return "";
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
                try
                {
                    return field.GetValue(
                        target);
                }
                catch
                {
                }
            }


            PropertyInfo property =
                type.GetProperty(
                    name,
                    BindingFlags.Instance |
                    BindingFlags.Public |
                    BindingFlags.NonPublic);


            if (property != null &&
                property.CanRead)
            {
                try
                {
                    return property.GetValue(
                        target,
                        null);
                }
                catch
                {
                }
            }


            return null;
        }
    }
}
