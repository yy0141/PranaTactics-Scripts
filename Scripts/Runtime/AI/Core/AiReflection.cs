using System;
using System.Collections;
using System.Reflection;

namespace PranaTactics.AI.Solo
{
    /// <summary>
    /// 現行データクラスの公開プロパティ名が変更されても、
    /// AI側の修正箇所を限定するための読み取り補助です。
    /// </summary>
    internal static class AiReflection
    {
        private const BindingFlags k_Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.IgnoreCase;

        public static object ReadObject(object source, params string[] memberNames)
        {
            if (source == null || memberNames == null)
            {
                return null;
            }

            Type type = source.GetType();

            foreach (string memberName in memberNames)
            {
                if (string.IsNullOrWhiteSpace(memberName))
                {
                    continue;
                }

                PropertyInfo property = type.GetProperty(memberName, k_Flags);

                if (property != null && property.GetIndexParameters().Length == 0)
                {
                    return property.GetValue(source);
                }

                FieldInfo field = type.GetField(memberName, k_Flags);

                if (field != null)
                {
                    return field.GetValue(source);
                }

                MethodInfo method = type.GetMethod(memberName, k_Flags, null, Type.EmptyTypes, null);

                if (method != null)
                {
                    return method.Invoke(source, null);
                }
            }

            return null;
        }

        public static float ReadFloat(object source, float fallback, params string[] memberNames)
        {
            object value = ReadObject(source, memberNames);

            if (value == null)
            {
                return fallback;
            }

            try
            {
                return Convert.ToSingle(value);
            }
            catch
            {
                return fallback;
            }
        }

        public static int ReadInt(object source, int fallback, params string[] memberNames)
        {
            object value = ReadObject(source, memberNames);

            if (value == null)
            {
                return fallback;
            }

            try
            {
                return Convert.ToInt32(value);
            }
            catch
            {
                return fallback;
            }
        }

        public static bool ReadBool(object source, bool fallback, params string[] memberNames)
        {
            object value = ReadObject(source, memberNames);

            if (value == null)
            {
                return fallback;
            }

            try
            {
                return Convert.ToBoolean(value);
            }
            catch
            {
                return fallback;
            }
        }

        public static string ReadString(object source, string fallback, params string[] memberNames)
        {
            object value = ReadObject(source, memberNames);

            return value != null ? value.ToString() : fallback;
        }

        public static int CountItems(object source, params string[] memberNames)
        {
            object value = ReadObject(source, memberNames);

            if (value is ICollection collection)
            {
                return collection.Count;
            }

            if (value is IEnumerable enumerable)
            {
                int count = 0;

                foreach (object unused in enumerable)
                {
                    count++;
                }

                return count;
            }

            return 0;
        }

        public static void ClearList(object source, params string[] memberNames)
        {
            object value = ReadObject(source, memberNames);

            if (value is IList list)
            {
                list.Clear();
            }
        }
    }
}
