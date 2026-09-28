using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Dott
{
    public enum DottValueKind
    {
        Unsupported,
        Number,
        Vector2,
        Vector3,
        Vector4,
        Color,
        String
    }

    /// <summary>
    /// Reflection helpers used by <see cref="DOTweenProperty"/> to discover and access tweenable members
    /// (fields and properties) of components and ScriptableObjects, including nested paths like "stats.health".
    /// </summary>
    public static class DottMembers
    {
        private const int MaxDepth = 2;
        private const BindingFlags DeclaredFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
        private const string BACKING_FIELD_SUFFIX = ">k__BackingField";

        public readonly struct Entry
        {
            public readonly string Path;
            public readonly string Label;
            public readonly Type ValueType;
            public readonly DottValueKind Kind;

            public Entry(string path, string label, Type valueType, DottValueKind kind)
            {
                Path = path;
                Label = label;
                ValueType = valueType;
                Kind = kind;
            }
        }

        public static DottValueKind KindOf(Type type)
        {
            if (type == typeof(float) || type == typeof(double) || type == typeof(int) || type == typeof(long) ||
                type == typeof(short) || type == typeof(byte) || type == typeof(sbyte) || type == typeof(uint) ||
                type == typeof(ulong) || type == typeof(ushort))
            {
                return DottValueKind.Number;
            }

            if (type == typeof(Vector2)) return DottValueKind.Vector2;
            if (type == typeof(Vector3)) return DottValueKind.Vector3;
            if (type == typeof(Vector4)) return DottValueKind.Vector4;
            if (type == typeof(Color)) return DottValueKind.Color;
            if (type == typeof(string)) return DottValueKind.String;
            return DottValueKind.Unsupported;
        }

        public static bool IsIntegral(Type type) =>
            type == typeof(int) || type == typeof(long) || type == typeof(short) || type == typeof(byte) ||
            type == typeof(sbyte) || type == typeof(uint) || type == typeof(ulong) || type == typeof(ushort);

        #region Discovery

        public static List<Entry> Enumerate(Object root)
        {
            var result = new List<Entry>();
            if (root == null) return result;

            Collect(root, root.GetType(), "", 0, result, new HashSet<string>());
            return result;
        }

        private static void Collect(object instance, Type type, string prefix, int depth, List<Entry> result, HashSet<string> seen)
        {
            for (var t = type; t != null && !IsStopType(t); t = t.BaseType)
            {
                foreach (var field in t.GetFields(DeclaredFlags))
                {
                    if (!IsSerializedField(field)) continue;

                    var path = prefix + field.Name;
                    var kind = KindOf(field.FieldType);
                    if (kind != DottValueKind.Unsupported)
                    {
                        if (seen.Add(path))
                        {
                            result.Add(new Entry(path, Label(prefix + CleanName(field.Name), field.FieldType), field.FieldType, kind));
                            AddChannels(path, kind, result, seen);
                        }

                        continue;
                    }

                    if (depth >= MaxDepth) continue;

                    if (typeof(ScriptableObject).IsAssignableFrom(field.FieldType))
                    {
                        // Use the actual asset so that members of derived types are found
                        var child = instance != null ? field.GetValue(instance) as Object : null;
                        if (child != null)
                        {
                            Collect(child, child.GetType(), path + ".", depth + 1, result, seen);
                        }
                    }
                    else if (IsNestedSerializable(field.FieldType))
                    {
                        object child = null;
                        if (instance != null)
                        {
                            child = field.GetValue(instance);
                        }

                        if (child != null || field.FieldType.IsValueType)
                        {
                            Collect(child, field.FieldType, path + ".", depth + 1, result, seen);
                        }
                    }
                }

                foreach (var property in t.GetProperties(DeclaredFlags))
                {
                    if (property.GetIndexParameters().Length > 0) continue;
                    if (property.GetGetMethod(false) == null || property.GetSetMethod(false) == null) continue;
                    if (property.IsDefined(typeof(ObsoleteAttribute), true)) continue;

                    var kind = KindOf(property.PropertyType);
                    if (kind == DottValueKind.Unsupported) continue;

                    var path = prefix + property.Name;
                    if (seen.Add(path))
                    {
                        result.Add(new Entry(path, Label(path, property.PropertyType), property.PropertyType, kind));
                        AddChannels(path, kind, result, seen);
                    }
                }
            }
        }

        // Single channels of vectors and colors (position.x, color.a...) so that e.g. only the alpha can be faded
        private static void AddChannels(string path, DottValueKind kind, List<Entry> result, HashSet<string> seen)
        {
            string channels;
            switch (kind)
            {
                case DottValueKind.Vector2: channels = "xy"; break;
                case DottValueKind.Vector3: channels = "xyz"; break;
                case DottValueKind.Vector4: channels = "xyzw"; break;
                case DottValueKind.Color: channels = "rgba"; break;
                default: return;
            }

            foreach (var channel in channels)
            {
                var channelPath = $"{path}.{channel}";
                if (seen.Add(channelPath))
                {
                    result.Add(new Entry(channelPath, Label(channelPath, typeof(float)), typeof(float), DottValueKind.Number));
                }
            }
        }

        private static string Label(string path, Type type) => $"{path}  ({TypeName(type)})";

        private static string TypeName(Type type)
        {
            if (type == typeof(float)) return "float";
            if (type == typeof(double)) return "double";
            if (type == typeof(int)) return "int";
            if (type == typeof(long)) return "long";
            if (type == typeof(string)) return "string";
            return type.Name;
        }

        // "<Health>k__BackingField" -> "Health"
        private static string CleanName(string name) =>
            name.Length > 2 && name[0] == '<' && name.EndsWith(BACKING_FIELD_SUFFIX)
                ? name.Substring(1, name.Length - 1 - BACKING_FIELD_SUFFIX.Length)
                : name;

        private static bool IsSerializedField(FieldInfo field)
        {
            if (field.IsStatic || field.IsInitOnly || field.IsLiteral) return false;
            if (field.IsNotSerialized) return false;
            if (field.IsDefined(typeof(ObsoleteAttribute), true)) return false;
            return field.IsPublic || field.IsDefined(typeof(SerializeField), true);
        }

        private static bool IsNestedSerializable(Type type)
        {
            if (type.IsPrimitive || type.IsEnum || type.IsArray || type.IsGenericType || type.IsAbstract) return false;
            if (type == typeof(string) || typeof(Object).IsAssignableFrom(type)) return false;
            if (!type.IsSerializable) return false;

            var ns = type.Namespace ?? string.Empty;
            return !ns.StartsWith("UnityEngine") && !ns.StartsWith("System");
        }

        private static bool IsStopType(Type type) =>
            type == typeof(Object) || type == typeof(Component) || type == typeof(Behaviour) ||
            type == typeof(MonoBehaviour) || type == typeof(ScriptableObject) ||
            type.FullName == "UnityEngine.EventSystems.UIBehaviour";

        #endregion

        #region Access

        /// <summary>Reads and writes a (possibly nested) member of <c>root</c>.</summary>
        public sealed class Accessor
        {
            private readonly object root;
            private readonly MemberInfo[] chain;

            public Type ValueType { get; }
            public DottValueKind Kind { get; }
            public bool IsAlive => !(root is Object unityObject) || unityObject != null;

            internal Accessor(object root, MemberInfo[] chain, Type valueType)
            {
                this.root = root;
                this.chain = chain;
                ValueType = valueType;
                Kind = KindOf(valueType);
            }

            public object Get()
            {
                if (!IsAlive) return null;

                var current = root;
                foreach (var member in chain)
                {
                    current = GetMember(member, current);
                    if (current == null) return null;
                }

                return current;
            }

            public void Set(object value)
            {
                if (!IsAlive) return;
                SetAt(root, 0, value);
            }

            public double GetNumber() => Convert.ToDouble(Get());

            public void SetNumber(double value)
            {
                if (IsIntegral(ValueType))
                {
                    try
                    {
                        Set(Convert.ChangeType(Math.Round(value), ValueType));
                    }
                    catch (OverflowException)
                    {
                        // Eases like Back/Elastic can overshoot the range of small integer types
                    }

                    return;
                }

                Set(Convert.ChangeType(value, ValueType));
            }

            private void SetAt(object container, int index, object value)
            {
                var member = chain[index];
                if (index == chain.Length - 1)
                {
                    SetMember(member, container, value);
                    return;
                }

                var child = GetMember(member, container);
                if (child == null) return;

                SetAt(child, index + 1, value);

                // Structs are boxed copies, so write them back into their owner
                if (child.GetType().IsValueType)
                {
                    SetMember(member, container, child);
                }
            }
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("ReSharper", "PossibleNullReferenceException")]
        public static Accessor CreateAccessor(object root, string path)
        {
            if (root == null || string.IsNullOrEmpty(path)) return null;

            try
            {
                var segments = path.Split('.');
                var chain = new MemberInfo[segments.Length];
                var current = root;
                var currentType = root.GetType();
                Type valueType = null;

                for (var i = 0; i < segments.Length; i++)
                {
                    var member = FindMember(currentType, segments[i]);
                    if (member == null) return null;

                    chain[i] = member;

                    if (i == segments.Length - 1)
                    {
                        valueType = MemberType(member);
                        break;
                    }

                    current = GetMember(member, current);
                    if (current == null || (current is Object unityObject && unityObject == null)) return null;
                    currentType = current.GetType();
                }

                return new Accessor(root, chain, valueType);
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static MemberInfo FindMember(Type type, string name)
        {
            for (var t = type; t != null; t = t.BaseType)
            {
                var field = t.GetField(name, DeclaredFlags);
                if (field != null) return field;

                var property = t.GetProperty(name, DeclaredFlags);
                if (property != null) return property;
            }

            return null;
        }

        private static Type MemberType(MemberInfo member) =>
            member is FieldInfo field ? field.FieldType : ((PropertyInfo)member).PropertyType;

        private static object GetMember(MemberInfo member, object target) =>
            member is FieldInfo field ? field.GetValue(target) : ((PropertyInfo)member).GetValue(target);

        private static void SetMember(MemberInfo member, object target, object value)
        {
            if (member is FieldInfo field)
            {
                field.SetValue(target, value);
            }
            else
            {
                ((PropertyInfo)member).SetValue(target, value);
            }
        }

        #endregion
    }
}
