using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Reflection;
using System.Security.AccessControl;
using System.Text;

namespace BurcatProtocol
{
    public static class Transformable
    {
        private static ConcurrentDictionary<Transform, byte> Transforms { get; } = [];
        private static ConcurrentDictionary<Guid, byte> FlowIdentifiers { get; } = [];
        private static Transform TryDynamicCast(Type sourceType, object value, Type targetType, Guid? flowID = null)
        {
            Guid fID = flowID ?? GuidExtensions.GenerateSequential();

            Transform transform = new(sourceType, targetType, TransformType.None);
            if (Transforms.Any(t => t.Key.CompareTo(transform) == 0)) return Transforms.First(t => t.Key.CompareTo(transform) == 0).Key;
            else if (targetType.IsAssignableFrom(sourceType)) transform = new(sourceType, targetType, TransformType.Direct);
            else if (sourceType.GetMethods(BindingFlags.Public | BindingFlags.Static).FirstOrDefault(m => (m.Name == "op_Implicit" || m.Name == "op_Explicit") && targetType.IsAssignableFrom(m.ReturnType)) is MethodInfo sChanger) transform = new(sourceType, targetType, sChanger);
            else if (targetType.GetMethods(BindingFlags.Public | BindingFlags.Static).FirstOrDefault(m =>
            {
                if (m.Name == "op_Implicit" || m.Name == "op_Explicit")
                {
                    ParameterInfo[] parameters = m.GetParameters();
                    if (parameters.Length == 1 && targetType != parameters[0].ParameterType)
                    {
                        bool canTransform = FlowIdentifiers.TryAdd(fID, 0) && TryDynamicCast(value.GetType(), value, parameters[0].ParameterType, fID).TransformType != TransformType.None;
                        if (canTransform) FlowIdentifiers.Remove(fID, out _);
                        return canTransform;
                    }
                    else return false;
                }
                else return false;
            }) is MethodInfo tChanger) transform = new(sourceType, targetType, tChanger);
            else if (targetType.GetConstructors().FirstOrDefault(c =>
            {
                ParameterInfo[] parameters = c.GetParameters();
                if (parameters.Length == 1)
                {
                    bool canTransform = FlowIdentifiers.TryAdd(fID, 0) && TryDynamicCast(value.GetType(), value, parameters[0].ParameterType, fID).TransformType != TransformType.None;
                    if (canTransform) FlowIdentifiers.Remove(fID, out _);
                    return canTransform;
                }
                else return false;
            }) is ConstructorInfo constructor) transform = new(sourceType, targetType, constructor);
            else if (targetType.IsArray && targetType.GetElementType() is Type elementType && TryDynamicCast<IEnumerable>(value, out IEnumerable? enumerable) && BuildArray(enumerable, elementType) is not null) transform = new(sourceType, targetType, elementType);
            else if (sourceType.BaseType is Type sBaseType && TryDynamicCast(sBaseType, value, targetType) is Transform innerTransform) transform = new(sourceType, targetType, innerTransform);
            else transform = new(sourceType, targetType, TransformType.None);

            FlowIdentifiers.Remove(fID, out _);
            Transforms.GetOrAdd(transform, 0);
            return transform;
        }
        private static Array? BuildArray(IEnumerable values, Type elementType)
        {
            List<object?> items = [];
            foreach (object? item in values)
                if (TryDynamicCast(item, elementType, out object? result)) items.Add(result);
                else return null;

            Array typed = Array.CreateInstance(elementType, items.Count);
            Array.Copy(items.ToArray(), typed, items.Count);
            return typed;
        }

        public static bool TryDynamicCast(Type sourceType, object value, Type targetType, [MaybeNullWhen(false)] out object result)
        {
            if (!sourceType.IsAssignableFrom(value.GetType())) throw new ArgumentException($"The specified value is not of type '{sourceType.Name}'.", nameof(sourceType));
            else
            {
                Transform transform = TryDynamicCast(sourceType, value, targetType);
                switch (transform.TransformType)
                {
                    case TransformType.None:
                        result = null;
                        return false;
                    case TransformType.Direct:
                        result = value;
                        return true;
                    case TransformType.Function:
                        result = transform.TransformFunction!(value);
                        return true;
                    default:
                        throw new InvalidOperationException();
                }
            }
        }
        public static bool TryDynamicCast<F, T>(F value, [MaybeNullWhen(false)] out T result) where F : notnull
        {
            if (TryDynamicCast(typeof(F), value, typeof(T), out object? r) && r is T rt)
            {
                result = rt;
                return true;
            }
            else
            {
                result = default;
                return false;
            }
        }
        public static bool TryDynamicCast<T>(object value, [MaybeNullWhen(false)] out T result)
        {
            if (TryDynamicCast(value.GetType(), value, typeof(T), out object? r) && r is T rt)
            {
                result = rt;
                return true;
            }
            else
            {
                result = default;
                return false;
            }
        }
        public static bool TryDynamicCast(object? value, Type targetType, out object? result)
        {
            if (value is null && MightBeNull(targetType))
            {
                result = null;
                return true;
            }
            else if (value is not null) return TryDynamicCast(value.GetType(), value, targetType, out result);
            else
            {
                result = null;
                return false;
            }
        }
        public static bool TryDynamicCast(object? value, FieldInfo info, out object? result)
        {
            if (value is null)
                if (CanBeNull(info))
                {
                    result = null;
                    return true;
                }
                else
                {
                    result = null;
                    return false;
                }
            else return TryDynamicCast(value.GetType(), value, info.FieldType, out result);
        }
        public static bool TryDynamicCast(object? value, PropertyInfo info, out object? result)
        {
            if (value is null)
                if (CanBeNull(info))
                {
                    result = null;
                    return true;
                }
                else
                {
                    result = null;
                    return false;
                }
            else return TryDynamicCast(value.GetType(), value, info.PropertyType, out result);
        }
        public static bool TryDynamicCast(object? value, ParameterInfo info, out object? result)
        {
            if (value is null)
                if (CanBeNull(info))
                {
                    result = null;
                    return true;
                }
                else
                {
                    result = null;
                    return false;
                }
            else return TryDynamicCast(value.GetType(), value, info.ParameterType, out result);
        }
        public static object DynamicCast(Type originType, object value, Type targetType)
        {
            if (TryDynamicCast(originType, value, targetType, out object? result)) return result;
            else throw new InvalidCastException();
        }
        public static object? DynamicCast(object? value, Type targetType)
        {
            if (TryDynamicCast(value, targetType, out object? result)) return result;
            else throw new InvalidCastException();
        }
        public static object? DynamicCast(object? value, FieldInfo info)
        {
            if (TryDynamicCast(value, info, out object? result)) return result;
            else throw new InvalidCastException();
        }
        public static object? DynamicCast(object? value, PropertyInfo info)
        {
            if (TryDynamicCast(value, info, out object? result)) return result;
            else throw new InvalidCastException();
        }
        public static object? DynamicCast(object? value, ParameterInfo info)
        {
            if (TryDynamicCast(value, info, out object? result)) return result;
            else throw new InvalidCastException();
        }
        public static T DynamicCast<F, T>(F value) where F : notnull
        {
            if (TryDynamicCast<F, T>(value, out T? result)) return result;
            else throw new InvalidCastException();
        }
        public static T DynamicCast<T>(object value)
        {
            if (TryDynamicCast<T>(value, out T? result)) return result;
            else throw new InvalidCastException();
        }

        public static bool BaseCopy(object source, object target, BindingFlags bindingFlags)
        {
            Type tSource = source.GetType(), tTarget = target.GetType();

            if (tSource.IsAssignableFrom(tTarget))
            {
                foreach (FieldInfo field in tSource.GetFields(bindingFlags)) field.SetValue(target, field.GetValue(source));
                foreach (PropertyInfo property in tSource.GetProperties(bindingFlags)) property.SetValue(target, property.GetValue(source));

                return true;
            }
            else if (tTarget.IsAssignableFrom(tSource))
            {
                foreach (FieldInfo field in tTarget.GetFields(bindingFlags)) field.SetValue(target, field.GetValue(source));
                foreach (PropertyInfo property in tTarget.GetProperties(bindingFlags)) property.SetValue(target, property.GetValue(source));

                return true;
            }
            else return false;
        }
        public static bool BaseCopy(object source, object target) => BaseCopy(source, target, BindingFlags.Instance | BindingFlags.Public);

        public static bool MightBeNull(this Type type) => !type.IsValueType || Nullable.GetUnderlyingType(type) != null;
        public static bool CanBeNull(this FieldInfo info) => new NullabilityInfoContext().Create(info).ReadState == NullabilityState.Nullable;
        public static bool CanBeNull(this PropertyInfo info) => new NullabilityInfoContext().Create(info).ReadState == NullabilityState.Nullable;
        public static bool CanBeNull(this ParameterInfo info) => new NullabilityInfoContext().Create(info).ReadState == NullabilityState.Nullable;

        public static Type MakeNonNullable(this Type type)
        {
            if (MightBeNull(type) && Nullable.GetUnderlyingType(type) is Type underlying) return underlying;
            else return type;
        }
        public static Type MakeNullable(this Type type)
        {
            if (type.IsValueType && Nullable.GetUnderlyingType(type) == null) return typeof(Nullable<>).MakeGenericType(type);
            else return type;
        }

        private enum TransformType
        {
            None,
            Direct,
            Function
        }

        private readonly struct Transform : IComparable<Transform>
        {
            private static MethodInfo Converter { get; } = typeof(Transformable).GetMethod(nameof(DynamicCast), [typeof(object), typeof(Type)])!;

            private static Func<object, object> CreateFunctionMethod(MethodInfo info)
            {
                Type parameterType = info.GetParameters()[0].ParameterType;

                var arg = Expression.Parameter(typeof(object), "arg");
                var transformable = Expression.Call(Converter, arg, Expression.Constant(parameterType, typeof(Type)));
                var call = Expression.Call(info, Expression.Convert(transformable, parameterType));
                return Expression.Lambda<Func<object, object>>(Expression.Convert(call, typeof(object)), arg).Compile();
            }
            private static Func<object, object> CreateArrayMethod(Type elementType)
            {
                MethodInfo info = typeof(Transformable).GetMethod(nameof(BuildArray), BindingFlags.Static | BindingFlags.NonPublic)!;

                var arg0 = Expression.Parameter(typeof(object), "arg0");
                var arg1 = Expression.Constant(elementType, typeof(Type));
                var transformable = Expression.Call(Converter, arg0, Expression.Constant(typeof(IEnumerable), typeof(Type)));
                var call = Expression.Call(info, Expression.Convert(arg0, typeof(IEnumerable)), arg1);
                return Expression.Lambda<Func<object, object>>(Expression.Convert(call, typeof(object)), arg0).Compile();
            }
            private static Func<object, object> CreateConstructor(ConstructorInfo info)
            {
                Type parameterType = info.GetParameters()[0].ParameterType;

                var arg = Expression.Parameter(typeof(object), "arg");
                var transformable = Expression.Call(Converter, arg, Expression.Constant(parameterType, typeof(Type)));
                var call = Expression.New(info, Expression.Convert(transformable, parameterType));
                return Expression.Lambda<Func<object, object>>(Expression.Convert(call, typeof(object)), arg).Compile();
            }

            public GuidList From { get; }
            public GuidList To { get; }
            public TransformType TransformType { get; }

            public Func<object, object>? TransformFunction { get; }

            public Transform(Type from, Type to, TransformType transformType) { From = GuidList.FromType(from); To = GuidList.FromType(to); TransformType = transformType; }
            public Transform(Type from, Type to, Transform transform) : this(from, to, transform.TransformType) { TransformFunction = transform.TransformFunction; }

            private Transform(Type from, Type to, Func<object, object> transformFunction) : this(from, to, TransformType.Function) { TransformFunction = transformFunction; }
            public Transform(Type from, Type to, ConstructorInfo constructor) : this(from, to, CreateConstructor(constructor)) { }
            public Transform(Type from, Type to, MethodInfo transformMethod) : this(from, to, CreateFunctionMethod(transformMethod)) { }
            public Transform(Type from, Type to, Type elementType) : this(from, to, CreateArrayMethod(elementType)) { }

            public override bool Equals([NotNullWhen(true)] object? obj)
            {
                if (obj is null) return false;
                else if (obj is Transform transform) return CompareTo(transform) == 0;
                else return false;
            }
            public override int GetHashCode() => HashCode.Combine(From, To, TransformType);

            public int CompareTo(Transform other)
            {
                int fromComparation = From.CompareTo(other.From);

                if (fromComparation == 0) return To.CompareTo(other.To);
                else return fromComparation;
            }
        }
    }
}
