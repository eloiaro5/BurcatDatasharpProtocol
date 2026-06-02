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
    /// <summary>
    /// Provides cached dynamic conversion helpers used by protocol construction, field assignment, and action invocation.
    /// </summary>
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

        /// <summary>
        /// Tries to convert a value from a declared source type to a target type.
        /// </summary>
        /// <param name="sourceType">The declared source type.</param>
        /// <param name="value">The value to convert.</param>
        /// <param name="targetType">The target type.</param>
        /// <param name="result">The converted value when successful.</param>
        /// <returns><see langword="true"/> when conversion succeeds; otherwise, <see langword="false"/>.</returns>
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

        /// <summary>
        /// Tries to convert a value from one static type to another.
        /// </summary>
        /// <typeparam name="F">The source type.</typeparam>
        /// <typeparam name="T">The target type.</typeparam>
        /// <param name="value">The value to convert.</param>
        /// <param name="result">The converted value when successful.</param>
        /// <returns><see langword="true"/> when conversion succeeds; otherwise, <see langword="false"/>.</returns>
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

        /// <summary>
        /// Tries to convert a value from its runtime type to a target type.
        /// </summary>
        /// <typeparam name="T">The target type.</typeparam>
        /// <param name="value">The value to convert.</param>
        /// <param name="result">The converted value when successful.</param>
        /// <returns><see langword="true"/> when conversion succeeds; otherwise, <see langword="false"/>.</returns>
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

        /// <summary>
        /// Tries to convert a nullable value to a target type.
        /// </summary>
        /// <param name="value">The value to convert.</param>
        /// <param name="targetType">The target type.</param>
        /// <param name="result">The converted value when successful.</param>
        /// <returns><see langword="true"/> when conversion succeeds; otherwise, <see langword="false"/>.</returns>
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

        /// <summary>
        /// Tries to convert a value to the type and nullability represented by a field.
        /// </summary>
        /// <param name="value">The value to convert.</param>
        /// <param name="info">The target field metadata.</param>
        /// <param name="result">The converted value when successful.</param>
        /// <returns><see langword="true"/> when conversion succeeds; otherwise, <see langword="false"/>.</returns>
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
        /// <summary>
        /// Tries to convert a value to the type and nullability represented by a property.
        /// </summary>
        /// <param name="value">The value to convert.</param>
        /// <param name="info">The target property metadata.</param>
        /// <param name="result">The converted value when successful.</param>
        /// <returns><see langword="true"/> when conversion succeeds; otherwise, <see langword="false"/>.</returns>
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
        /// <summary>
        /// Tries to convert a value to the type and nullability represented by a parameter.
        /// </summary>
        /// <param name="value">The value to convert.</param>
        /// <param name="info">The target parameter metadata.</param>
        /// <param name="result">The converted value when successful.</param>
        /// <returns><see langword="true"/> when conversion succeeds; otherwise, <see langword="false"/>.</returns>
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
        /// <summary>
        /// Converts a value from a declared source type to a target type.
        /// </summary>
        /// <param name="originType">The declared source type.</param>
        /// <param name="value">The value to convert.</param>
        /// <param name="targetType">The target type.</param>
        /// <returns>The converted value.</returns>
        /// <exception cref="InvalidCastException">Thrown when conversion is not possible.</exception>
        public static object DynamicCast(Type originType, object value, Type targetType)
        {
            if (TryDynamicCast(originType, value, targetType, out object? result)) return result;
            else throw new InvalidCastException();
        }
        /// <summary>
        /// Converts a nullable value to a target type.
        /// </summary>
        /// <param name="value">The value to convert.</param>
        /// <param name="targetType">The target type.</param>
        /// <returns>The converted value.</returns>
        /// <exception cref="InvalidCastException">Thrown when conversion is not possible.</exception>
        public static object? DynamicCast(object? value, Type targetType)
        {
            if (TryDynamicCast(value, targetType, out object? result)) return result;
            else throw new InvalidCastException();
        }
        /// <summary>
        /// Converts a value to the type and nullability represented by a field.
        /// </summary>
        /// <param name="value">The value to convert.</param>
        /// <param name="info">The target field metadata.</param>
        /// <returns>The converted value.</returns>
        /// <exception cref="InvalidCastException">Thrown when conversion is not possible.</exception>
        public static object? DynamicCast(object? value, FieldInfo info)
        {
            if (TryDynamicCast(value, info, out object? result)) return result;
            else throw new InvalidCastException();
        }
        /// <summary>
        /// Converts a value to the type and nullability represented by a property.
        /// </summary>
        /// <param name="value">The value to convert.</param>
        /// <param name="info">The target property metadata.</param>
        /// <returns>The converted value.</returns>
        /// <exception cref="InvalidCastException">Thrown when conversion is not possible.</exception>
        public static object? DynamicCast(object? value, PropertyInfo info)
        {
            if (TryDynamicCast(value, info, out object? result)) return result;
            else throw new InvalidCastException();
        }
        /// <summary>
        /// Converts a value to the type and nullability represented by a parameter.
        /// </summary>
        /// <param name="value">The value to convert.</param>
        /// <param name="info">The target parameter metadata.</param>
        /// <returns>The converted value.</returns>
        /// <exception cref="InvalidCastException">Thrown when conversion is not possible.</exception>
        public static object? DynamicCast(object? value, ParameterInfo info)
        {
            if (TryDynamicCast(value, info, out object? result)) return result;
            else throw new InvalidCastException();
        }
        /// <summary>
        /// Converts a value from one static type to another.
        /// </summary>
        /// <typeparam name="F">The source type.</typeparam>
        /// <typeparam name="T">The target type.</typeparam>
        /// <param name="value">The value to convert.</param>
        /// <returns>The converted value.</returns>
        /// <exception cref="InvalidCastException">Thrown when conversion is not possible.</exception>
        public static T DynamicCast<F, T>(F value) where F : notnull
        {
            if (TryDynamicCast<F, T>(value, out T? result)) return result;
            else throw new InvalidCastException();
        }
        /// <summary>
        /// Converts a value from its runtime type to a target type.
        /// </summary>
        /// <typeparam name="T">The target type.</typeparam>
        /// <param name="value">The value to convert.</param>
        /// <returns>The converted value.</returns>
        /// <exception cref="InvalidCastException">Thrown when conversion is not possible.</exception>
        public static T DynamicCast<T>(object value)
        {
            if (TryDynamicCast<T>(value, out T? result)) return result;
            else throw new InvalidCastException();
        }

        /// <summary>
        /// Copies fields and properties with matching names between compatible base and derived objects.
        /// </summary>
        /// <param name="source">The object to copy values from.</param>
        /// <param name="target">The object to copy values to.</param>
        /// <param name="bindingFlags">The binding flags used to discover members.</param>
        /// <returns><see langword="true"/> when the objects are related by inheritance; otherwise, <see langword="false"/>.</returns>
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
        /// <summary>
        /// Copies public instance fields and properties between compatible base and derived objects.
        /// </summary>
        /// <param name="source">The object to copy values from.</param>
        /// <param name="target">The object to copy values to.</param>
        /// <returns><see langword="true"/> when the objects are related by inheritance; otherwise, <see langword="false"/>.</returns>
        public static bool BaseCopy(object source, object target) => BaseCopy(source, target, BindingFlags.Instance | BindingFlags.Public);

        /// <summary>
        /// Determines whether a type may contain null.
        /// </summary>
        /// <param name="type">The type to inspect.</param>
        /// <returns><see langword="true"/> when the type may be null; otherwise, <see langword="false"/>.</returns>
        public static bool MightBeNull(this Type type) => !type.IsValueType || Nullable.GetUnderlyingType(type) != null;

        /// <summary>
        /// Determines whether a field permits null values.
        /// </summary>
        /// <param name="info">The field metadata.</param>
        /// <returns><see langword="true"/> when the field permits null; otherwise, <see langword="false"/>.</returns>
        public static bool CanBeNull(this FieldInfo info) => new NullabilityInfoContext().Create(info).ReadState == NullabilityState.Nullable;

        /// <summary>
        /// Determines whether a property permits null values.
        /// </summary>
        /// <param name="info">The property metadata.</param>
        /// <returns><see langword="true"/> when the property permits null; otherwise, <see langword="false"/>.</returns>
        public static bool CanBeNull(this PropertyInfo info) => new NullabilityInfoContext().Create(info).ReadState == NullabilityState.Nullable;

        /// <summary>
        /// Determines whether a parameter permits null values.
        /// </summary>
        /// <param name="info">The parameter metadata.</param>
        /// <returns><see langword="true"/> when the parameter permits null; otherwise, <see langword="false"/>.</returns>
        public static bool CanBeNull(this ParameterInfo info) => new NullabilityInfoContext().Create(info).ReadState == NullabilityState.Nullable;

        /// <summary>
        /// Gets the non-nullable form of a nullable value type.
        /// </summary>
        /// <param name="type">The type to convert.</param>
        /// <returns>The non-nullable type when applicable; otherwise, the original type.</returns>
        public static Type MakeNonNullable(this Type type)
        {
            if (MightBeNull(type) && Nullable.GetUnderlyingType(type) is Type underlying) return underlying;
            else return type;
        }

        /// <summary>
        /// Gets the nullable form of a value type.
        /// </summary>
        /// <param name="type">The type to convert.</param>
        /// <returns>The nullable type when applicable; otherwise, the original type.</returns>
        public static Type MakeNullable(this Type type)
        {
            if (type.IsValueType && Nullable.GetUnderlyingType(type) == null) return typeof(Nullable<>).MakeGenericType(type);
            else return type;
        }

        /// <summary>
        /// Identifies how a cached transformation is performed.
        /// </summary>
        private enum TransformType
        {
            None,
            Direct,
            Function
        }

        /// <summary>
        /// Represents a cached conversion between two CLR type keys.
        /// </summary>
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

            /// <summary>
            /// Gets the source type key.
            /// </summary>
            public GuidList From { get; }

            /// <summary>
            /// Gets the target type key.
            /// </summary>
            public GuidList To { get; }

            /// <summary>
            /// Gets the kind of cached transformation.
            /// </summary>
            public TransformType TransformType { get; }

            /// <summary>
            /// Gets the compiled transformation function.
            /// </summary>
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
