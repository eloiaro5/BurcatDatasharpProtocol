using BurcatProtocol.Annotations;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;
using System.Reflection.Metadata;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Xml.Linq;
using static System.Collections.Specialized.BitVector32;

namespace BurcatProtocol
{
    /// <summary>
    /// Caches reflected Burcat fields, properties, constructors, and methods for faster protocol execution.
    /// </summary>
    /// <remarks>
    /// The cache stores compiled accessors and invokers used for object construction,
    /// field extraction, field assignment, and action execution. It also validates
    /// field values, method parameters, method results, and object state by using
    /// <see cref="ValidationAttribute"/> metadata.
    /// </remarks>
    public static class BurcatCache
    {
        /// <summary>
        /// Binding flags used to discover public Burcat fields and properties.
        /// </summary>
        public const BindingFlags PublicFieldsFlags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.FlattenHierarchy;

        /// <summary>
        /// Binding flags used to discover public Burcat methods.
        /// </summary>
        public const BindingFlags PublicMehodsFlags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.FlattenHierarchy;

        /// <summary>
        /// Binding flags used to discover public Burcat constructors.
        /// </summary>
        public const BindingFlags PublicConstructorsFlags = BindingFlags.Public | BindingFlags.Instance;

        private static ConcurrentDictionary<GuidList, ConcurrentDictionary<ObjectField, byte>> Fields { get; } = [];
        private static ConcurrentDictionary<GuidList, ConcurrentDictionary<ObjectMethod, byte>> Constructors { get; } = [];

        private static ConcurrentDictionary<GuidList, ConcurrentDictionary<GenericMethod, byte>> GenericMethods { get; } = [];
        private static ConcurrentDictionary<MethodKey, ConcurrentDictionary<ObjectMethod, byte>> Methods { get; } = [];

        /// <summary>
        /// Adds a field to the cache for a Burcat object type.
        /// </summary>
        /// <param name="objectType">The type that owns or exposes the field.</param>
        /// <param name="info">The field metadata to cache.</param>
        /// <returns><see langword="true"/> when the field was added; otherwise, <see langword="false"/>.</returns>
        public static bool AddToCache(Type objectType, FieldInfo info)
        {
            GuidList guid = GuidList.FromType(objectType);
            ConcurrentDictionary<ObjectField, byte> fields = Fields.GetOrAdd(guid, []);
            return fields.TryAdd(new(info), 0);
        }

        /// <summary>
        /// Adds a field to the cache for a Burcat object's runtime type.
        /// </summary>
        /// <param name="objectBDP">The object whose runtime type owns or exposes the field.</param>
        /// <param name="info">The field metadata to cache.</param>
        /// <returns><see langword="true"/> when the field was added; otherwise, <see langword="false"/>.</returns>
        public static bool AddToCache(IBurcatObject objectBDP, FieldInfo info) => AddToCache(objectBDP.GetType(), info);

        /// <summary>
        /// Adds a property to the cache for a Burcat object type.
        /// </summary>
        /// <param name="objectType">The type that owns or exposes the property.</param>
        /// <param name="info">The property metadata to cache.</param>
        /// <returns><see langword="true"/> when the property was added; otherwise, <see langword="false"/>.</returns>
        public static bool AddToCache(Type objectType, PropertyInfo info)
        {
            GuidList guid = GuidList.FromType(objectType);
            ConcurrentDictionary<ObjectField, byte> fields = Fields.GetOrAdd(guid, []);
            return fields.TryAdd(new(info), 0);
        }

        /// <summary>
        /// Adds a property to the cache for a Burcat object's runtime type.
        /// </summary>
        /// <param name="objectBDP">The object whose runtime type owns or exposes the property.</param>
        /// <param name="info">The property metadata to cache.</param>
        /// <returns><see langword="true"/> when the property was added; otherwise, <see langword="false"/>.</returns>
        public static bool AddToCache(IBurcatObject objectBDP, PropertyInfo info) => AddToCache(objectBDP.GetType(), info);

        /// <summary>
        /// Adds a constructor to the cache for a Burcat object type.
        /// </summary>
        /// <param name="objectType">The type that owns the constructor.</param>
        /// <param name="info">The constructor metadata to cache.</param>
        /// <returns><see langword="true"/> when the constructor was added; otherwise, <see langword="false"/>.</returns>
        public static bool AddToCache(Type objectType, ConstructorInfo info)
        {
            GuidList guid = GuidList.FromType(objectType);
            ConcurrentDictionary<ObjectMethod, byte> constructors = Constructors.GetOrAdd(guid, []);
            return constructors.TryAdd(new(info), 0);
        }

        /// <summary>
        /// Adds a method to the cache for a Burcat object type.
        /// </summary>
        /// <param name="objectType">The type that owns or exposes the method.</param>
        /// <param name="info">The method metadata to cache.</param>
        /// <returns><see langword="true"/> when the method was added; otherwise, <see langword="false"/>.</returns>
        public static bool AddToCache(Type objectType, MethodInfo info)
        {
            if (info.ContainsGenericParameters)
            {
                GuidList guid = GuidList.FromType(objectType);
                ConcurrentDictionary<GenericMethod, byte> methods = GenericMethods.GetOrAdd(guid, []);
                return methods.TryAdd(new(info), 0);
            }
            else
            {
                MethodKey key = new(GuidList.FromType(objectType), info);
                ConcurrentDictionary<ObjectMethod, byte> methods = Methods.GetOrAdd(key, []);
                return methods.TryAdd(new([], info), 0);
            }
        }

        private static ConcurrentDictionary<GuidList, byte> InCache { get; } = [];
        private static object CacheLock { get; } = new();

        /// <summary>
        /// Discovers and caches all public Burcat-invokable members for a type.
        /// </summary>
        /// <remarks>
        /// Members marked with <see cref="NotBurcatInvokableAttribute"/> are ignored.
        /// </remarks>
        /// <param name="objectType">The type to scan and cache.</param>
        public static void AddToCache(Type objectType)
        {
            GuidList guid = GuidList.FromType(objectType);
            if (!InCache.ContainsKey(guid))
                lock (CacheLock)
                    if (!InCache.ContainsKey(guid))
                    {
                        foreach (FieldInfo info in objectType.GetFields(PublicFieldsFlags).Where(f => f.GetCustomAttribute<NotBurcatInvokableAttribute>() is null)) AddToCache(objectType, info);
                        foreach (PropertyInfo info in objectType.GetProperties(PublicFieldsFlags).Where(f => f.GetCustomAttribute<NotBurcatInvokableAttribute>() is null)) AddToCache(objectType, info);
                        if (objectType.IsValueType || (objectType.IsClass && !objectType.IsAbstract)) foreach (ConstructorInfo info in objectType.GetConstructors(PublicConstructorsFlags).Where(f => f.GetCustomAttribute<NotBurcatInvokableAttribute>() is null)) AddToCache(objectType, info);
                        foreach (MethodInfo info in objectType.GetMethods(PublicMehodsFlags).Where(f => f.GetCustomAttribute<NotBurcatInvokableAttribute>() is null)) AddToCache(objectType, info);

                        InCache.TryAdd(guid, 0);
                    }
        }

        /// <summary>
        /// Checks whether a type has already been scanned into the cache.
        /// </summary>
        /// <param name="objectType">The type to check.</param>
        /// <returns><see langword="true"/> when type has already been scanned; otherwise, <see langword="false"/>.</returns>
        public static bool IsInCache(Type objectType) => InCache.ContainsKey(GuidList.FromType(objectType));

        /// <summary>
        /// Gets the cached readable fields and properties for a Burcat object.
        /// </summary>
        /// <param name="objectBDP">The object whose field values are read.</param>
        /// <returns>The readable protocol fields currently cached for the object's type.</returns>
        public static BurcatField[] GetFields(IBurcatObject objectBDP)
        {
            if (Fields.TryGetValue(new(objectBDP), out ConcurrentDictionary<ObjectField, byte>? fields)) return [.. fields.Keys.Where(f => f.GetFunction is not null).Select(f => new BurcatField(f.PublicName, BurcatTranslator.ObjectTranslate(f.GetFunction!(objectBDP))))];
            else return [];
        }

        /// <summary>
        /// Sets a cached field or property value on a Burcat object.
        /// </summary>
        /// <param name="objectType">The type that owns or exposes the field.</param>
        /// <param name="objectBDP">The target object, or <see langword="null"/> for static fields.</param>
        /// <param name="field">The protocol field name and value to apply.</param>
        /// <param name="validate">Whether to validate the value before assigning it.</param>
        /// <returns><see langword="null"/> on success; otherwise, the protocol exception describing the failure.</returns>
        public static BurcatException? SetField(Type objectType, IBurcatObject? objectBDP, BurcatField field, bool validate = false)
        {
            LinkedList<ValidationResult> validations = [];
            ObjectField same = new(objectType, field);

            if (Fields[objectBDP is null ? GuidList.FromType(objectType) : new(objectBDP)].Keys.FirstOrDefault(k => k.CompareTo(same) == 0) is ObjectField f)
                if (f.SetAction is Action<object?, object?> action)
                {
                    object? value = field.Value;
                    bool invokable = true;

                    if (value is null && !f.CanBeNull) invokable = false;
                    else if (value is object fValue)
                    {
                        if (f.FieldType.IsAssignableFrom(fValue.GetType())) value = fValue;
                        else if (fValue is BurcatTranslation translation && BurcatTranslator.TryTranslate(translation, out value) && f.FieldType.IsAssignableFrom(fValue.GetType())) invokable = true;
                    }

                    if (invokable)
                    {
                        if (!validate || Validator.TryValidateValue(value, new ValidationContext(value ?? NothingChart.Instance) { MemberName = f.PublicName }, validations, f.Validations))
                        {
                            action(objectBDP, value);
                            return null;
                        }
                        else return new BurcatValidationException($"Validation failed at field with name {field.Name} in {objectType.Name}.", innerException: new BurcatException(validations.First!.Value.ErrorMessage ?? "No validation error message provided."));
                    }
                    else return new BurcatException("The object cannot be converted to the field type.");
                }
                else return new NotInBurcatCacheException($"Field with name {field.Name} in {objectType.Name} has no setter cached.");
            else return new NotInBurcatCacheException($"Field with name {field.Name} in {objectType.Name} is not cached.");
        }

        /// <summary>
        /// Constructs a Burcat object from cached constructors and protocol parameters.
        /// </summary>
        /// <param name="objectType">The type to construct.</param>
        /// <param name="parameters">The protocol constructor parameters.</param>
        /// <returns>The constructed object, or <see langword="null"/> when the constructor returns a null value.</returns>
        /// <exception cref="InvalidOperationException">Thrown when no cached constructor can accept the provided parameters.</exception>
        public static IBurcatObject? Construct(Type objectType, IBurcatObject?[] parameters)
        {
            if (Constructors.TryGetValue(GuidList.FromType(objectType), out ConcurrentDictionary<ObjectMethod, byte>? constructors))
            {
                foreach (ObjectMethod constructor in constructors.Keys)
                {
                    ActionResult result = constructor.TryDirectInvoke(null, parameters);
                    if (result.SuccessfulExecution) return result.Value;
                }

                foreach (ObjectMethod constructor in constructors.Keys)
                {
                    ActionResult result = constructor.TryInvoke(null, parameters, false, out IEnumerable<string> _);
                    if (result.SuccessfulExecution) return result.Value;
                }

                throw new InvalidOperationException($"There's no constructors avaliable in {objectType.Name} for the provided parameters.");
            }
            else throw new InvalidOperationException($"There's no constructors avaliable in {objectType.Name}.");
        }

        /// <summary>
        /// Executes a cached method or action on a Burcat object type.
        /// </summary>
        /// <param name="objectType">The type that owns or exposes the action.</param>
        /// <param name="objectBDP">The target object, or <see langword="null"/> for static actions.</param>
        /// <param name="name">The action name.</param>
        /// <param name="parameters">The protocol action parameters.</param>
        /// <returns>The action result or the protocol exception produced while trying to execute it.</returns>
        public static ActionResult ExecuteAction(Type objectType, IBurcatObject? objectBDP, string name, IBurcatObject?[] parameters)
        {
            LinkedList<Type> genericTypesList = [];
            for (int i = 0; i < parameters.Length && parameters[i] is BurcatType type; i++) genericTypesList.AddLast(type.Nullable ? type.GetTypeCLR().MakeGenericType() : type.GetTypeCLR());
            Type[] genericTypes = [.. genericTypesList];

            MethodKey key = new(GuidList.FromType(objectType), genericTypes.Length == 0 ? GuidList.Empty : GuidList.FromTypes(genericTypes), name);
            if (genericTypes.Length != 0)
            {
                IBurcatObject?[] tmp = parameters;
                parameters = new IBurcatObject?[tmp.Length - genericTypes.Length];
                Array.Copy(tmp, genericTypes.Length, parameters, 0, parameters.Length);

                GenericMethod same = new(name, parameters.Length);
                ConcurrentDictionary<ObjectMethod, byte> methodsDictionary = Methods.GetOrAdd(key, []);
                if (GenericMethods[key.ClassGuid].Keys.FirstOrDefault(k => k.CompareTo(same) == 0) is GenericMethod method) methodsDictionary.TryAdd(new(genericTypes, method.Method), 0);
            }

            BurcatList<string> failedMessages = [];
            if (Methods.TryGetValue(key, out ConcurrentDictionary<ObjectMethod, byte>? methods))
            {
                foreach (ObjectMethod method in methods.Keys)
                {
                    LinkedList<ValidationResult> validations = [];
                    ActionResult result = method.TryDirectInvoke(objectBDP, parameters);
                    if (result.SuccessfulExecution)
                        if (Validator.TryValidateValue(result.Value, new ValidationContext(result.Value ?? NothingChart.Instance), validations, method.ObjectValidations)) return result;
                       else return ActionResult.Thrown(new($"Validation failed at method with name {name} in {objectType.Name}.", innerException: new BurcatException(validations.First!.Value.ErrorMessage ?? "No validation error message provided.")));
                }

                foreach (ObjectMethod method in methods.Keys)
                {
                    LinkedList<ValidationResult> validations = [];
                    ActionResult result = method.TryInvoke(objectBDP, parameters, true, out IEnumerable<string> failedValidations);
                    if (result.SuccessfulExecution)
                        if (Validator.TryValidateValue(result.Value, new ValidationContext(result.Value ?? NothingChart.Instance), validations, method.ObjectValidations)) return result;
                        else return ActionResult.Thrown(new($"Validation failed at method with name {name} in {objectType.Name}.", innerException: new BurcatException(validations.First!.Value.ErrorMessage ?? "No validation error message provided.")));
                    else foreach (string validation in failedValidations) failedMessages.Add(validation);
                }

                return ActionResult.Thrown(new BurcatValidationException($"Method with name {name} in {objectType.Name} was found in cache, but none was executable with the provided parameters.", payload: failedMessages));
            }
            else return ActionResult.Thrown(new NotInBurcatCacheException($"Method with name {name} in {objectType.Name} is not cached."));
        }

        /// <summary>
        /// Validates the cached field values and object-level state of a Burcat object.
        /// </summary>
        /// <param name="objectBDP">The object to validate.</param>
        /// <returns><see langword="null"/> when the state is valid; otherwise, the validation exception.</returns>
        public static BurcatException? ValidateState(IBurcatObject objectBDP)
        {
            Type objectType = objectBDP.GetType();
            LinkedList<ValidationResult> validations = [];

            foreach (ObjectField field in Fields[GuidList.FromType(objectType)].Keys)
                if (field.GetFunction is Func<object?, object?> function)
                {
                    object? value = function(objectBDP);
                    if (!Validator.TryValidateValue(value, new ValidationContext(value ?? NothingChart.Instance) { MemberName = field.PublicName }, validations, field.Validations))
                        return new BurcatValidationException($"Validation failed at field with name {field.PublicName}.", innerException: new BurcatException(validations.First!.Value.ErrorMessage ?? "No validation error message provided."));
                }

            if (Constructors.TryGetValue(GuidList.FromType(objectType), out ConcurrentDictionary<ObjectMethod, byte>? constructors) && constructors.Keys.FirstOrDefault() is ObjectMethod method)
                if (!Validator.TryValidateValue(objectBDP, new ValidationContext(objectBDP ?? NothingChart.Instance), validations, method.ObjectValidations))
                    return new BurcatValidationException($"General validation state of object failed.", innerException: new BurcatException(validations.First!.Value.ErrorMessage ?? "No validation error message provided."));

            return null;
        }

        /// <summary>
        /// Represents a cached field or property with compiled get and set delegates.
        /// </summary>
        private class ObjectField : IComparable<ObjectField>
        {
            private static Func<object?, object?> CreateGetter(FieldInfo info, bool isStatic)
            {
                var target = Expression.Parameter(typeof(object), "target");
                var castTarget = Expression.Convert(target, info.DeclaringType!);
                var fieldAccess = Expression.Field(isStatic ? null : castTarget, info);
                var castResult = Expression.Convert(fieldAccess, typeof(object));
                return Expression.Lambda<Func<object?, object?>>(castResult, target).Compile();
            }
            private static Func<object?, object?> CreateGetter(PropertyInfo info, bool isStatic)
            {
                var target = Expression.Parameter(typeof(object), "target");
                var castTarget = Expression.Convert(target, info.DeclaringType!);
                var propertyAccess = Expression.Property(isStatic ? null : castTarget, info);
                var castResult = Expression.Convert(propertyAccess, typeof(object));
                return Expression.Lambda<Func<object?, object?>>(castResult, target).Compile();
            }

            public static Action<object?, object?> CreateSetter(FieldInfo info, bool isStatic)
            {
                var targetParam = Expression.Parameter(typeof(object), "target");
                var valueParam = Expression.Parameter(typeof(object), "value");
                var convertMethod = typeof(Transformable).GetMethod(nameof(Transformable.DynamicCast), [typeof(object), typeof(FieldInfo)])!;
                var convertedValue = Expression.Convert(Expression.Call(convertMethod, valueParam, Expression.Constant(info)), info.FieldType);
                var fieldAccess = Expression.Field(isStatic ? null : Expression.Convert(targetParam, info.DeclaringType!), info);
                return Expression.Lambda<Action<object?, object?>>(Expression.Assign(fieldAccess, convertedValue), targetParam, valueParam).Compile();
            }
            private static Action<object?, object?> CreateSetter(PropertyInfo info, bool isStatic)
            {
                var targetParam = Expression.Parameter(typeof(object), "target");
                var valueParam = Expression.Parameter(typeof(object), "value");
                var convertMethod = typeof(Transformable).GetMethod(nameof(Transformable.DynamicCast), [typeof(object), typeof(PropertyInfo)])!;
                var convertedValue = Expression.Convert(Expression.Call(convertMethod, valueParam, Expression.Constant(info)), info.PropertyType);
                var propertyAccess = Expression.Property(isStatic ? null : Expression.Convert(targetParam, info.DeclaringType!), info);
                return Expression.Lambda<Action<object?, object?>>(Expression.Assign(propertyAccess, convertedValue), targetParam, valueParam).Compile();
            }

            private Type DeclaringType { get; }
            private string Key { get; }

            public Type FieldType { get; }
            public string PublicName { get; }
            public bool CanBeNull { get; }
            public bool IsStatic { get; }
            public Func<object?, object?>? GetFunction { get; private set; }
            public Action<object?, object?>? SetAction { get; private set; }

            public IEnumerable<ValidationAttribute> Validations { get; private set; }

            public ObjectField(Type declaringType, BurcatField field)
            {
                DeclaringType = declaringType;
                Key = field.Name.ToLower().Replace("_", null);

                FieldType = typeof(object);
                PublicName = field.Name;

                Validations = [];
            }
            public ObjectField(FieldInfo info)
            {
                DeclaringType = info.DeclaringType!;
                Key = info.Name.ToLower().Replace("_", null);

                FieldType = info.FieldType;
                PublicName = info.Name;
                CanBeNull = info.CanBeNull();
                IsStatic = info.IsStatic;

                GetFunction = CreateGetter(info, IsStatic);
                SetAction = CreateSetter(info, IsStatic);

                Validations = [.. info.GetCustomAttributes<ValidationAttribute>()];
            }
            public ObjectField(PropertyInfo info)
            {
                DeclaringType = info.DeclaringType!;
                Key = info.Name.ToLower().Replace("_", null);

                FieldType = info.PropertyType;
                PublicName = info.Name;
                CanBeNull = info.CanBeNull();
                IsStatic = (info.GetGetMethod()?.IsStatic ?? false) || (info.GetSetMethod()?.IsStatic ?? false);

                if (info.CanRead) GetFunction = CreateGetter(info, IsStatic);
                if (info.CanWrite) SetAction = CreateSetter(info, IsStatic);

                Validations = [.. info.GetCustomAttributes<ValidationAttribute>()];
            }

            public override bool Equals(object? obj)
            {
                if (obj is null) return false;
                else if (ReferenceEquals(this, obj)) return true;
                else if (obj is ObjectField objectField) return CompareTo(objectField) == 0;
                else return false;
            }
            public override int GetHashCode() => HashCode.Combine(Key);

            public int CompareTo(ObjectField? other)
            {
                if (other is null) return 1;
                else if (ReferenceEquals(this, other)) return 0;
                else
                {
                    int keyComparation = Key.CompareTo(other.Key);
                    if (keyComparation == 0 && DeclaringType != other.DeclaringType && DeclaringType.IsAssignableFrom(other.DeclaringType))
                    {
                        if (other.GetFunction is not null) GetFunction = other.GetFunction;
                        if (other.SetAction is not null) SetAction = other.SetAction;

                        Validations = Validations.Concat(other.Validations);

                        return 0;
                    }
                    else return keyComparation;
                }
            }
        }

        /// <summary>
        /// Represents an open generic method cached by name and parameter count.
        /// </summary>
        private class GenericMethod : IComparable<GenericMethod>
        {
            private string Key { get; }
            private int ParameterCount { get; }

            public MethodInfo Method { get; }

            public GenericMethod(string name, int parameterCount) { Key = name.ToLower().Replace("_", null); ParameterCount = parameterCount; Method = null!; }
            public GenericMethod(MethodInfo info)
            {
                Key = info.Name.ToLower().Replace("_", null);
                ParameterCount = info.GetParameters().Length;

                Method = info;
            }

            public override bool Equals(object? obj)
            {
                if (obj is null) return false;
                else if (ReferenceEquals(this, obj)) return true;
                else if (obj is GenericMethod genericMethod) return CompareTo(genericMethod) == 0;
                else return false;
            }
            public override int GetHashCode() => HashCode.Combine(Key, ParameterCount);


            public int CompareTo(GenericMethod? other)
            {
                if (other is null) return 0;
                else if (ReferenceEquals(this, other)) return 0;
                else
                {
                    int nameComparation = Key.CompareTo(other.Key);
                    if (nameComparation == 0) return ParameterCount.CompareTo(other.ParameterCount);
                    else return nameComparation;
                }
            }
        }

        /// <summary>
        /// Represents a cached constructor or method with compiled invocation delegates and validation metadata.
        /// </summary>
        private class ObjectMethod : IEquatable<ObjectMethod>
        {
            private static Func<object?[], object?> CreateConstructor(ConstructorInfo info)
            {
                var paramsInfo = info.GetParameters();
                var argsParam = Expression.Parameter(typeof(object[]), "args");
                var arguments = new Expression[paramsInfo.Length];
                for (int i = 0; i < paramsInfo.Length; i++) arguments[i] = Expression.Convert(Expression.ArrayIndex(argsParam, Expression.Constant(i)), paramsInfo[i].ParameterType);
                return Expression.Lambda<Func<object?[], object?>>(Expression.Convert(Expression.New(info, arguments), typeof(object)), argsParam).Compile();
            }

            private static Func<object?[], object?> CreateStaticMethod(MethodInfo info)
            {
                var paramsInfo = info.GetParameters();
                var argsParam = Expression.Parameter(typeof(object[]), "args");
                var arguments = new Expression[paramsInfo.Length];
                for (int i = 0; i < paramsInfo.Length; i++) arguments[i] = Expression.Convert(Expression.ArrayIndex(argsParam, Expression.Constant(i)), paramsInfo[i].ParameterType);

                var call = Expression.Call(info, arguments);
                Expression body = info.ReturnType == typeof(void) ? Expression.Block(call, Expression.Constant(null)) : Expression.Convert(call, typeof(object));
                return Expression.Lambda<Func<object?[], object>>(body, argsParam).Compile();
            }

            public static Func<object, object?[], object?> CreateInstanceMethod(MethodInfo info)
            {
                var paramsInfo = info.GetParameters();
                var targetParam = Expression.Parameter(typeof(object), "target");
                var argsParam = Expression.Parameter(typeof(object[]), "args");
                var instanceCast = Expression.Convert(targetParam, info.DeclaringType!);
                var arguments = new Expression[paramsInfo.Length];
                for (int i = 0; i < paramsInfo.Length; i++) arguments[i] = Expression.Convert(Expression.ArrayIndex(argsParam, Expression.Constant(i)), paramsInfo[i].ParameterType);

                var call = Expression.Call(instanceCast, info, arguments);
                Expression body = info.ReturnType == typeof(void) ? Expression.Block(call, Expression.Constant(null)) : Expression.Convert(call, typeof(object));
                return Expression.Lambda<Func<object, object?[], object?>>(body, targetParam, argsParam).Compile();
            }

            private List<ParameterInfo> Parameters { get; } = [];

            public Type DeclaringType { get; }
            public Func<object?[], object?>? StaticDelegate { get; private set; }
            public Func<object, object?[], object?>? InstanceDelegate { get; private set; }

            private SortedDictionary<int, List<ValidationAttribute>> ParameterValidations { get; set; } = [];
            public IEnumerable<ValidationAttribute> ObjectValidations { get; private set; }

            public ObjectMethod(ConstructorInfo info)
            {
                int i = 0;
                foreach (ParameterInfo parameter in info.GetParameters())
                {
                    Parameters.Add(parameter);
                    ParameterValidations.Add(i, []);
                    foreach (ValidationAttribute validator in parameter.GetCustomAttributes<ValidationAttribute>()) ParameterValidations[i].Add(validator);
                    i++;
                }

                DeclaringType = info.DeclaringType!;
                StaticDelegate = CreateConstructor(info);

                ObjectValidations = [.. DeclaringType.GetCustomAttributes<ValidationAttribute>()];
            }
            public ObjectMethod(Type[] methodGenericTypes, MethodInfo info)
            {
                if (methodGenericTypes.Length > 0) info = info.MakeGenericMethod(methodGenericTypes);

                int i = 0;
                List<Type> types = [];
                foreach (ParameterInfo parameter in info.GetParameters())
                {
                    types.Add(parameter.ParameterType);

                    Parameters.Add(parameter);
                    ParameterValidations.Add(i, []);
                    foreach (BurcatCustomValidationAttribute validator in parameter.GetCustomAttributes<BurcatCustomValidationAttribute>()) ParameterValidations[i].Add(validator);
                    i++;
                }
                if (info.ReturnType != typeof(void)) types.Add(info.ReturnType);

                DeclaringType = info.DeclaringType!;

                if (info.IsStatic) StaticDelegate = CreateStaticMethod(info);
                else InstanceDelegate = CreateInstanceMethod(info);

                ObjectValidations = [];//[.. DeclaringType.GetCustomAttributes<ValidationAttribute>()];
            }

            public override bool Equals(object? objectBDP)
            {
                if (objectBDP is ObjectMethod method) return Equals(method);
                else return base.Equals(objectBDP);
            }
            public bool Equals(ObjectMethod? other)
            {
                if (other is null) return false;
                else if (ReferenceEquals(this, other)) return true;
                else if (Parameters.Count == other.Parameters.Count)
                {
                    for (int i = 0; i < Parameters.Count; i++)
                        if (Parameters[i].ParameterType != other.Parameters[i].ParameterType)
                            return false;

                    if (DeclaringType != other.DeclaringType && DeclaringType.IsAssignableFrom(other.DeclaringType))
                    {
                        if (other.StaticDelegate is not null) StaticDelegate = other.StaticDelegate;
                        if (other.InstanceDelegate is not null) InstanceDelegate = other.InstanceDelegate;
                    }

                    return true;
                }
                else return false;
            }

            public override int GetHashCode()
            {
                HashCode hash = new();
                foreach (ParameterInfo parameter in Parameters) hash.Add(parameter.ParameterType);
                return hash.ToHashCode();
            }

            public ActionResult TryDirectInvoke(IBurcatObject? target, IBurcatObject?[] parameters)
            {
                if (Parameters.Count == parameters.Length)
                {
                    object?[] values = new object?[parameters.Length];
                    bool invokable = true;

                    for (int i = 0; i < parameters.Length && invokable; i++)
                        if (parameters[i] is null && Parameters[i].CanBeNull()) values[i] = null;
                        else if (parameters[i] is object pValue)
                        {
                            if (Parameters[i].ParameterType.IsAssignableFrom(pValue.GetType())) values[i] = parameters[i];
                            else if (parameters[i] is BurcatTranslation translation && BurcatTranslator.TryTranslate(translation, out object? translationValue) && Parameters[i].ParameterType.IsAssignableFrom(translationValue.GetType())) values[i] = translationValue;
                            else invokable = false;
                        }
                        else invokable = false;

                    if (invokable)
                    {
                        foreach (KeyValuePair<int, List<ValidationAttribute>> kvp in ParameterValidations)
                            if (!Validator.TryValidateValue(values[kvp.Key], new ValidationContext(values[kvp.Key] ?? NothingChart.Instance), null, kvp.Value))
                                return ActionResult.Unsuccessful;

                        object? result = target is null ? StaticDelegate!(values) : InstanceDelegate!(target, values);
                        if (result is object obj) return new(BurcatTranslator.ObjectTranslate(obj));
                        else return new(null);
                    }
                    else return ActionResult.Unsuccessful;
                }
                else return ActionResult.Unsuccessful;
            }

            public ActionResult TryInvoke(IBurcatObject? target, IBurcatObject?[] parameters, bool validate, out IEnumerable<string> validations)
            {
                validations = [];
                if (Parameters.Count == parameters.Length)
                {
                    object?[] values = new object?[parameters.Length];
                    bool invokable = true;

                    for (int i = 0; i < parameters.Length && invokable; i++)
                    if (Transformable.TryDynamicCast(parameters[i], Parameters[i], out object? value)) values[i] = value;
                    else if (parameters[i] is BurcatTranslation translation && BurcatTranslator.TryTranslate(translation, out value) && Transformable.TryDynamicCast(value, Parameters[i], out value)) values[i] = value;
                    else invokable = false;

                    LinkedList<ValidationResult> failedValidations = [];
                    if (invokable)
                    {
                        foreach (KeyValuePair<int, List<ValidationAttribute>> kvp in ParameterValidations)
                            if (validate && !Validator.TryValidateValue(values[kvp.Key], new ValidationContext(values[kvp.Key] ?? NothingChart.Instance), failedValidations, kvp.Value))
                            { validations = failedValidations.Where(v => v.ErrorMessage is not null).Select(v => v.ErrorMessage!); return ActionResult.Unsuccessful; }

                        object? result = target is null ? StaticDelegate!(values) : InstanceDelegate!(target, values);
                        if (result is object obj) return new(BurcatTranslator.ObjectTranslate(obj));
                        else return new(null);
                    }
                    else return ActionResult.Unsuccessful;
                }
                else return ActionResult.Unsuccessful;
            }
        }

        /// <summary>
        /// Identifies a cached method group by declaring type, generic type arguments, and action name.
        /// </summary>
        private class MethodKey : IComparable<MethodKey>
        {
            public GuidList ClassGuid { get; }
            public GuidList MethodGuid { get; }
            public string Name { get; }

            public MethodKey(GuidList classGuid, GuidList methodGuid, string name) { ClassGuid = classGuid; Name = name.ToLower().Replace("_", null); MethodGuid = methodGuid; }
            public MethodKey(GuidList classGuid, GuidList methodGuid, MethodInfo info) : this(classGuid, methodGuid, info.Name) { }
            public MethodKey(GuidList classGuid, MethodInfo info) : this(classGuid, GuidList.Empty, info.Name) { }

            public override bool Equals(object? obj)
            {
                if (obj is null) return false;
                else if (ReferenceEquals(this, obj)) return true;
                else if (obj is MethodKey methodKey) return CompareTo(methodKey) == 0;
                else return false;
            }
            public override int GetHashCode() => HashCode.Combine(ClassGuid, MethodGuid, Name);

            public int CompareTo(MethodKey? other)
            {
                if (other is null) return 1;
                else if (ReferenceEquals(this, other)) return 0;
                else
                {
                    int classComparation = ClassGuid.CompareTo(other.ClassGuid);
                    if (classComparation == 0)
                    {
                        int methodComparation = MethodGuid.CompareTo(other.MethodGuid);
                        if (methodComparation == 0) return Name.CompareTo(other.Name);
                        else return methodComparation;
                    }
                    else return classComparation;
                }
            }
        }
    }
}
