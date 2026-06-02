using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Text;

namespace BurcatProtocol
{
    /// <summary>
    /// Converts supported CLR values to and from Burcat protocol translations.
    /// </summary>
    public static class BurcatTranslator
    {
        private static Dictionary<Type, Guid> TypeDictionary { get; } = [];
        private static Dictionary<Guid, Translator> Translators { get; } = [];

        /// <summary>
        /// Registers a translator for a CLR type.
        /// </summary>
        /// <typeparam name="T">The CLR type to translate.</typeparam>
        /// <param name="classID">The Burcat class identity for the translated type.</param>
        /// <param name="toBDP">The function that serializes a value to bytes.</param>
        /// <param name="fromBDP">The function that deserializes bytes to a value.</param>
        /// <returns><see langword="true"/> when the translator was added; otherwise, <see langword="false"/>.</returns>
        public static bool Add<T>(Guid classID, Func<T, byte[]> toBDP, Func<byte[], T> fromBDP)
        {
            if (Translators.ContainsKey(classID)) return false;
            else
            {
                var objParam = Expression.Parameter(typeof(object), "obj");
                var toExpression = Expression.Lambda<Func<object, byte[]>>(Expression.Invoke(Expression.Constant(toBDP), Expression.Convert(objParam, typeof(T))), objParam);

                var bytesParam = Expression.Parameter(typeof(byte[]), "bytes");
                var fromExpression = Expression.Lambda<Func<byte[], object>>(Expression.Convert(Expression.Invoke(Expression.Constant(fromBDP), bytesParam), typeof(object)), bytesParam);

                TypeDictionary.Add(typeof(T), classID);
                Translators.Add(classID, new(typeof(T), toExpression.Compile(), fromExpression.Compile()));

                return true;
            }
        }

        /// <summary>
        /// Registers an enum translator that stores enum values as signed 64-bit integers.
        /// </summary>
        /// <typeparam name="T">The enum type to translate.</typeparam>
        /// <param name="classID">The Burcat class identity for the enum type.</param>
        /// <returns><see langword="true"/> when the translator was added; otherwise, <see langword="false"/>.</returns>
        public static bool Add<T>(Guid classID) where T : Enum => Add(classID, v => BitConverter.GetBytes(Convert.ToInt64(v)), t => (T)Enum.ToObject(typeof(T), BitConverter.ToInt64(t)));

        /// <summary>
        /// Removes a translator by class identity.
        /// </summary>
        /// <param name="classID">The Burcat class identity to remove.</param>
        /// <returns><see langword="true"/> when the translator was removed; otherwise, <see langword="false"/>.</returns>
        public static bool Remove(Guid classID) => Translators.Remove(classID) && TypeDictionary.Remove(TypeDictionary.First(kvp => kvp.Value == classID).Key);

        /// <summary>
        /// Removes a translator by CLR type.
        /// </summary>
        /// <param name="type">The CLR type to remove.</param>
        /// <returns><see langword="true"/> when the translator was removed; otherwise, <see langword="false"/>.</returns>
        public static bool Remove(Type type)
        {
            if (TypeDictionary.TryGetValue(type, out Guid classID))
            {
                TypeDictionary.Remove(type);
                Translators.Remove(classID);

                return true;
            }
            else return false;
        }

        /// <summary>
        /// Determines whether a class identity has a registered translator.
        /// </summary>
        /// <param name="classID">The Burcat class identity to test.</param>
        /// <param name="type">The translated CLR type when found.</param>
        /// <returns><see langword="true"/> when the translator exists; otherwise, <see langword="false"/>.</returns>
        public static bool CanTranslate(Guid classID, [MaybeNullWhen(false)] out Type type)
        {
            if (Translators.TryGetValue(classID, out Translator? translator))
            {
                type = translator.Type;
                return true;
            }
            else
            {
                type = null;
                return false;
            }
        }

        /// <summary>
        /// Determines whether a CLR type has a registered translator.
        /// </summary>
        /// <param name="type">The CLR type to test.</param>
        /// <param name="classID">The Burcat class identity when found.</param>
        /// <returns><see langword="true"/> when the translator exists; otherwise, <see langword="false"/>.</returns>
        public static bool CanTranslate(Type type, out Guid classID) => TypeDictionary.TryGetValue(type, out classID);

        /// <summary>
        /// Tries to translate a CLR value into a Burcat translation.
        /// </summary>
        /// <param name="value">The CLR value to translate.</param>
        /// <param name="translation">The protocol translation when successful.</param>
        /// <returns><see langword="true"/> when the value was translated; otherwise, <see langword="false"/>.</returns>
        public static bool TryTranslate(object value, [MaybeNullWhen(false)] out BurcatTranslation translation) => TryTranslate(value.GetType(), value, out translation);

        /// <summary>
        /// Tries to translate a CLR value into a Burcat translation using an explicit value type.
        /// </summary>
        /// <param name="valueType">The CLR value type.</param>
        /// <param name="value">The CLR value to translate.</param>
        /// <param name="translation">The protocol translation when successful.</param>
        /// <returns><see langword="true"/> when the value was translated; otherwise, <see langword="false"/>.</returns>
        public static bool TryTranslate(Type valueType, object value, [MaybeNullWhen(false)] out BurcatTranslation translation)
        {
            if (valueType.IsEnum && BurcatChat.TryGetClassIdentity(valueType, out Guid _))
            {
                translation = Translate(typeof(long), Transformable.DynamicCast<long>(value));
                return true;
            }
            else if (TypeDictionary.TryGetValue(valueType, out Guid classID) && Translators.TryGetValue(classID, out Translator? translator))
            {
                translation = new(classID, translator.ToBDP(value));
                return true;
            }
            else
            {
                translation = null;
                return false;
            }
        }

        /// <summary>
        /// Tries to translate protocol bytes back to a CLR value.
        /// </summary>
        /// <param name="classID">The translated value class identity.</param>
        /// <param name="translation">The translated value bytes.</param>
        /// <param name="value">The CLR value when successful.</param>
        /// <returns><see langword="true"/> when the bytes were translated; otherwise, <see langword="false"/>.</returns>
        public static bool TryTranslate(Guid classID, byte[] translation, [MaybeNullWhen(false)] out object value)
        {
            if (Translators.TryGetValue(classID, out Translator? translator))
            {
                value = translator.FromBDP(translation);
                return true;
            }
            else
            {
                value = default;
                return false;
            }
        }

        /// <summary>
        /// Tries to translate protocol bytes back to a typed CLR value.
        /// </summary>
        /// <typeparam name="T">The expected CLR value type.</typeparam>
        /// <param name="classID">The translated value class identity.</param>
        /// <param name="translation">The translated value bytes.</param>
        /// <param name="value">The CLR value when successful.</param>
        /// <returns><see langword="true"/> when the bytes were translated; otherwise, <see langword="false"/>.</returns>
        public static bool TryTranslate<T>(Guid classID, byte[] translation, [MaybeNullWhen(false)] out T value)
        {
            if (TryTranslate(classID, translation, out object? obj))
            {
                value = (T)obj;
                return true;
            }
            else
            {
                value = default;
                return false;
            }
        }

        /// <summary>
        /// Tries to translate a protocol translation back to a CLR value.
        /// </summary>
        /// <param name="translation">The protocol translation.</param>
        /// <param name="value">The CLR value when successful.</param>
        /// <returns><see langword="true"/> when the translation was converted; otherwise, <see langword="false"/>.</returns>
        public static bool TryTranslate(BurcatTranslation translation, [MaybeNullWhen(false)] out object value) => TryTranslate(translation.ClassID, translation.Data, out value);

        /// <summary>
        /// Tries to translate a protocol translation back to a typed CLR value.
        /// </summary>
        /// <typeparam name="T">The expected CLR value type.</typeparam>
        /// <param name="translation">The protocol translation.</param>
        /// <param name="value">The CLR value when successful.</param>
        /// <returns><see langword="true"/> when the translation was converted; otherwise, <see langword="false"/>.</returns>
        public static bool TryTranslate<T>(BurcatTranslation translation, [MaybeNullWhen(false)] out T value) => TryTranslate(translation.ClassID, translation.Data, out value);

        /// <summary>
        /// Translates a CLR value into a Burcat translation.
        /// </summary>
        /// <param name="value">The CLR value to translate.</param>
        /// <returns>The protocol translation.</returns>
        public static BurcatTranslation Translate(object value) => Translate(value.GetType(), value);

        /// <summary>
        /// Translates a CLR value into a Burcat translation using an explicit value type.
        /// </summary>
        /// <param name="valueType">The CLR value type.</param>
        /// <param name="value">The CLR value to translate.</param>
        /// <returns>The protocol translation.</returns>
        public static BurcatTranslation Translate(Type valueType, object value)
        {
            if (valueType.IsEnum && BurcatChat.TryGetClassIdentity(valueType, out Guid _)) return Translate(typeof(long), Transformable.DynamicCast<long>(value));
            else
            {
                Guid classID = TypeDictionary[valueType];
                return new(classID, Translators[classID].ToBDP(value));
            }
        }

        /// <summary>
        /// Translates protocol bytes back to a CLR value.
        /// </summary>
        /// <param name="classID">The translated value class identity.</param>
        /// <param name="translation">The translated value bytes.</param>
        /// <returns>The CLR value.</returns>
        public static object Translate(Guid classID, byte[] translation) => Translators[classID].FromBDP(translation);

        /// <summary>
        /// Translates protocol bytes back to a typed CLR value.
        /// </summary>
        /// <typeparam name="T">The expected CLR value type.</typeparam>
        /// <param name="classID">The translated value class identity.</param>
        /// <param name="translation">The translated value bytes.</param>
        /// <returns>The CLR value.</returns>
        public static T Translate<T>(Guid classID, byte[] translation) => (T)Translate(classID, translation);

        /// <summary>
        /// Translates a protocol translation back to a CLR value.
        /// </summary>
        /// <param name="translation">The protocol translation.</param>
        /// <returns>The CLR value.</returns>
        public static object Translate(BurcatTranslation translation) => Translate(translation.ClassID, translation.Data);

        /// <summary>
        /// Translates a protocol translation back to a typed CLR value.
        /// </summary>
        /// <typeparam name="T">The expected CLR value type.</typeparam>
        /// <param name="translation">The protocol translation.</param>
        /// <returns>The CLR value.</returns>
        public static T Translate<T>(BurcatTranslation translation) => (T)Translate(translation.ClassID, translation.Data);

        /// <summary>
        /// Converts a protocol object or translation back to a CLR object value.
        /// </summary>
        /// <param name="value">The protocol value to convert.</param>
        /// <returns>The CLR object value.</returns>
        public static object? ObjectBDPTranslate(IBurcatObject? value)
        {
            if (value is IBurcatObject objectBDP)
            {
                if (objectBDP is NothingChart) return null;
                else if (objectBDP is BurcatTranslation translation) return Translate(translation);
                else return objectBDP;
            }
            else return null;
        }

        /// <summary>
        /// Converts protocol objects or translations back to CLR object values.
        /// </summary>
        /// <param name="values">The protocol values to convert.</param>
        /// <returns>The CLR object values.</returns>
        public static object?[] ObjectsBDPTranslate(IBurcatObject?[]? values)
        {
            if (values is null) return [];
            else
            {
                object?[] objects = new object[values.Length];
                for (int i = 0; i < values.Length; i++) objects[i] = ObjectBDPTranslate(values[i]);
                return objects;
            }
        }

        /// <summary>
        /// Converts a CLR object value into a Burcat protocol object or translation.
        /// </summary>
        /// <param name="value">The CLR value to convert.</param>
        /// <returns>The protocol object value.</returns>
        public static IBurcatObject? ObjectTranslate(object? value)
        {
            if (value is null || value is NothingChart) return null;
            else if (CanTranslate(value.GetType(), out Guid classID)) return new BurcatTranslation(classID, Translators[classID].ToBDP(value));
            else if (value is IBurcatObject objectBDP) return objectBDP;
            else throw new InvalidCastException();
        }

        /// <summary>
        /// Converts CLR object values into Burcat protocol objects or translations.
        /// </summary>
        /// <param name="values">The CLR values to convert.</param>
        /// <returns>The protocol object values.</returns>
        public static IBurcatObject?[] ObjectsTranslate(object?[]? values)
        {
            if (values is null) return [];
            else
            {
                IBurcatObject?[] translations = new IBurcatObject?[values.Length];
                for (int i = 0; i < values.Length; i++)
                {
                    if (values[i] is object value)
                    {
                        if (value is NothingChart) translations[i] = null;
                        else if (CanTranslate(value.GetType(), out Guid classID)) translations[i] = new BurcatTranslation(classID, Translators[classID].ToBDP(value));
                        else if (value is IBurcatObject objectBDP) translations[i] = objectBDP;
                        else throw new InvalidCastException();
                    }
                }

                return translations;
            }
        }

        /// <summary>
        /// Registers the built-in translators for primitive CLR values and arrays.
        /// </summary>
        public static void LoadDefaults()
        {
            Add(new("00000000-0000-0000-0000-F00000000000"), BitConverter.GetBytes, t => BitConverter.ToBoolean(t));
            Add(new("00000000-0000-0000-0000-F00000000001"), v => [v], t => t[0]);
            Add(new("00000000-0000-0000-0000-F00000000002"), v => [(byte)v], t => (sbyte)t[0]);
            Add(new("00000000-0000-0000-0000-F00000000003"), BitConverter.GetBytes, t => BitConverter.ToInt16(t));
            Add(new("00000000-0000-0000-0000-F00000000004"), BitConverter.GetBytes, t => (ushort)BitConverter.ToInt16(t));
            Add(new("00000000-0000-0000-0000-F00000000005"), BitConverter.GetBytes, t => BitConverter.ToInt32(t));
            Add(new("00000000-0000-0000-0000-F00000000006"), BitConverter.GetBytes, t => (uint)BitConverter.ToInt32(t));
            Add(new("00000000-0000-0000-0000-F00000000007"), BitConverter.GetBytes, t => BitConverter.ToInt64(t));
            Add(new("00000000-0000-0000-0000-F00000000008"), BitConverter.GetBytes, t => (ulong)BitConverter.ToInt64(t));
            Add(new("00000000-0000-0000-0000-F00000000009"), BitConverter.GetBytes, t => BitConverter.ToInt128(t));
            Add(new("00000000-0000-0000-0000-F00000000010"), BitConverter.GetBytes, t => BitConverter.ToSingle(t));
            Add(new("00000000-0000-0000-0000-F00000000011"), BitConverter.GetBytes, t => BitConverter.ToDouble(t));
            Add(new("00000000-0000-0000-0000-F00000000012"), v => [.. decimal.GetBits(v).SelectMany(BitConverter.GetBytes)], t => (decimal)BitConverter.ToInt64(t));
            Add(new("00000000-0000-0000-0000-F00000000013"), BitConverter.GetBytes, t => BitConverter.ToChar(t));
            Add(new("00000000-0000-0000-0000-F00000000014"), Encoding.UTF8.GetBytes, Encoding.UTF8.GetString);
            Add(new("00000000-0000-0000-0000-F00000000015"), v => BitConverter.GetBytes(v.ToBinary()), t => DateTime.FromBinary(BitConverter.ToInt64(t)));
            Add(new("00000000-0000-0000-0000-F00000000016"), v => BitConverter.GetBytes(v.ToDateTime(new()).ToBinary()), t => DateOnly.FromDateTime(DateTime.FromBinary(BitConverter.ToInt64(t))));
            Add(new("00000000-0000-0000-0000-F00000000017"), v => BitConverter.GetBytes(DateOnly.FromDateTime(DateTime.Today).ToDateTime(v).ToBinary()), t => TimeOnly.FromDateTime(DateTime.FromBinary(BitConverter.ToInt64(t))));
            Add(new("00000000-0000-0000-0000-F00000000018"), v => BitConverter.GetBytes(v.Ticks), t => new TimeSpan(BitConverter.ToInt64(t)));
            Add(new("00000000-0000-0000-0000-F00000000019"), v => v.ToByteArray(), t => new Guid(t));

            Add(new("00000000-0000-0000-0000-F10000000000"), v => [.. v.SelectMany(BitConverter.GetBytes)], t => SpanTranslation(t, 1, t => BitConverter.ToBoolean(t)));
            Add(new("00000000-0000-0000-0000-F10000000001"), v => v, t => t);
            Add<sbyte[]>(new("00000000-0000-0000-0000-F10000000002"), v => [.. v.Select(s => (byte)s)], t => [.. t.Select(b => (sbyte)b)]);
            Add(new("00000000-0000-0000-0000-F10000000003"), v => [.. v.SelectMany(BitConverter.GetBytes)], t => SpanTranslation(t, 2, t => BitConverter.ToInt16(t)));
            Add(new("00000000-0000-0000-0000-F10000000004"), v => [.. v.SelectMany(BitConverter.GetBytes)], t => SpanTranslation(t, 2, t => (ushort)BitConverter.ToInt16(t)));
            Add(new("00000000-0000-0000-0000-F10000000005"), v => [.. v.SelectMany(BitConverter.GetBytes)], t => SpanTranslation(t, 4, t => BitConverter.ToInt32(t)));
            Add(new("00000000-0000-0000-0000-F10000000006"), v => [.. v.SelectMany(BitConverter.GetBytes)], t => SpanTranslation(t, 4, t => (uint)BitConverter.ToInt32(t)));
            Add(new("00000000-0000-0000-0000-F10000000007"), v => [.. v.SelectMany(BitConverter.GetBytes)], t => SpanTranslation(t, 8, t => BitConverter.ToInt64(t)));
            Add(new("00000000-0000-0000-0000-F10000000008"), v => [.. v.SelectMany(BitConverter.GetBytes)], t => SpanTranslation(t, 8, t => (ulong)BitConverter.ToInt64(t)));
            Add(new("00000000-0000-0000-0000-F10000000010"), v => [.. v.SelectMany(BitConverter.GetBytes)], t => SpanTranslation(t, 4, t => BitConverter.ToSingle(t)));
            Add(new("00000000-0000-0000-0000-F10000000011"), v => [.. v.SelectMany(BitConverter.GetBytes)], t => SpanTranslation(t, 8, t => BitConverter.ToDouble(t)));
            Add(new("00000000-0000-0000-0000-F10000000012"), v => [.. v.SelectMany(d => decimal.GetBits(d).SelectMany(BitConverter.GetBytes))], t => SpanTranslation(t, 16, b => new decimal(SpanTranslation(b, 4, i => BitConverter.ToInt32(i, 0)))));
            Add(new("00000000-0000-0000-0000-F10000000013"),    v => [.. v.SelectMany(x => BitConverter.GetBytes(x))],    t => SpanTranslation(t, 2, b => BitConverter.ToChar(b, 0)));
            Add(new("00000000-0000-0000-0000-F10000000014"), EncodeStringArray, DecodeStringArray);
            Add(new("00000000-0000-0000-0000-F10000000015"), v => [.. v.SelectMany(x => BitConverter.GetBytes(x.ToBinary()))], t => SpanTranslation(t, 8, b => DateTime.FromBinary(BitConverter.ToInt64(b, 0))));
            Add(new("00000000-0000-0000-0000-F10000000016"), v => [.. v.SelectMany(x => BitConverter.GetBytes(x.DayNumber))], t => SpanTranslation(t, 4, b => DateOnly.FromDayNumber(BitConverter.ToInt32(b, 0))));
            Add(new("00000000-0000-0000-0000-F10000000017"), v => [.. v.SelectMany(x => BitConverter.GetBytes(x.Ticks))], t => SpanTranslation(t, 8, b => new TimeOnly(BitConverter.ToInt64(b, 0))));
            Add(new("00000000-0000-0000-0000-F10000000018"), v => [.. v.SelectMany(x => BitConverter.GetBytes(x.Ticks))], t => SpanTranslation(t, 8, b => new TimeSpan(BitConverter.ToInt64(b, 0))));
            Add(new("00000000-0000-0000-0000-F10000000019"), v => [.. v.SelectMany(x => x.ToByteArray())], t => SpanTranslation(t, 16, b => new Guid(b)));
        }

        /// <summary>
        /// Splits translated bytes into fixed-size spans and translates each span.
        /// </summary>
        /// <typeparam name="T">The translated value type.</typeparam>
        /// <param name="data">The translated byte data.</param>
        /// <param name="spanLenght">The fixed byte length of each value.</param>
        /// <param name="translation">The function that translates each span.</param>
        /// <returns>The translated values.</returns>
        public static T[] SpanTranslation<T>(byte[] data, int spanLenght, Func<byte[], T> translation)
        {
            if (data.Length % spanLenght == 0)
            {
                T[] values = new T[data.Length / spanLenght];
                byte[] current = new byte[spanLenght];
                for (int i = 0; i < values.Length; i++)
                {
                    Array.Copy(data, spanLenght * i, current, 0, spanLenght);
                    values[i] = translation(current);
                }

                return values;
            }
            else throw new InvalidOperationException("The provided data has partial spans.");
        }

        private static byte[] EncodeStringArray(string[] values)
        {
            using MemoryStream stream = new();

            foreach (string value in values)
            {
                byte[] bytes = Encoding.UTF8.GetBytes(value);

                stream.Write(BitConverter.GetBytes(bytes.Length));
                stream.Write(bytes);
            }

            return stream.ToArray();
        }
        private static string[] DecodeStringArray(byte[] data)
        {
            List<string> values = [];
            int offset = 0;

            while (offset < data.Length)
            {
                if (data.Length - offset < 4) throw new InvalidOperationException("The provided data has a partial string length prefix.");
                else
                {
                    int length = BitConverter.ToInt32(data, offset);
                    offset += 4;

                    if (length < 0) throw new InvalidOperationException("String length cannot be negative.");
                    else if (data.Length - offset < length) throw new InvalidOperationException("The provided data has a partial string payload.");
                    else
                    {
                        values.Add(Encoding.UTF8.GetString(data, offset, length));
                        offset += length;
                    }
                }
            }

            return [.. values];
        }

        private class Translator
        {
            public Type Type { get; }
            public Func<object, byte[]> ToBDP { get; }
            public Func<byte[], object> FromBDP { get; }

            public Translator(Type type, Func<object, byte[]> toBDP, Func<byte[], object> fromBDP)
            {
                Type = type;
                ToBDP = toBDP;
                FromBDP = fromBDP;
            }
        }
    }
}
