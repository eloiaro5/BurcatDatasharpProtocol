using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Text;

namespace BurcatProtocol
{
    public static class BurcatTranslator
    {
        private static Dictionary<Type, Guid> TypeDictionary { get; } = [];
        private static Dictionary<Guid, Translator> Translators { get; } = [];

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
        public static bool Remove(Guid classID) => Translators.Remove(classID) && TypeDictionary.Remove(TypeDictionary.First(kvp => kvp.Value == classID).Key);
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
        public static bool CanTranslate(Type type, out Guid classID) => TypeDictionary.TryGetValue(type, out classID) || (type.IsEnum && BurcatChat.TryGetClassIdentity(type, out classID));

        public static bool TryTranslate(object value, [MaybeNullWhen(false)] out BurcatTranslation translation) => TryTranslate(value.GetType(), value, out translation);
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
        public static bool TryTranslate(BurcatTranslation translation, [MaybeNullWhen(false)] out object value) => TryTranslate(translation.ClassID, translation.Data, out value);
        public static bool TryTranslate<T>(BurcatTranslation translation, [MaybeNullWhen(false)] out T value) => TryTranslate(translation.ClassID, translation.Data, out value);

        public static BurcatTranslation Translate(object value) => Translate(value.GetType(), value);
        public static BurcatTranslation Translate(Type valueType, object value)
        {
            if (valueType.IsEnum && BurcatChat.TryGetClassIdentity(valueType, out Guid _)) return Translate(typeof(long), Transformable.DynamicCast<long>(value));
            else
            {
                Guid classID = TypeDictionary[valueType];
                return new(classID, Translators[classID].ToBDP(value));
            }
        }
        public static object Translate(Guid classID, byte[] translation) => Translators[classID].FromBDP(translation);
        public static T Translate<T>(Guid classID, byte[] translation) => (T)Translate(classID, translation);
        public static object Translate(BurcatTranslation translation) => Translate(translation.ClassID, translation.Data);
        public static T Translate<T>(BurcatTranslation translation) => (T)Translate(translation.ClassID, translation.Data);

        public static IBurcatObject? FullObjectTranslate(object? value)
        {
            if (value is null || value is NothingChart) return null;
            else if (CanTranslate(value.GetType(), out Guid classID)) return new BurcatTranslation(classID, Translators[classID].ToBDP(value));
            else if (value is IBurcatObject objectBDP) return objectBDP;
            else throw new InvalidCastException();
        }
        public static IBurcatObject?[] FullObjectsTranslate(object?[] values)
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
            Add(new("00000000-0000-0000-0000-F00000000015"), v => BitConverter.GetBytes(v.ToBinary()), t => new DateTime(BitConverter.ToInt64(t)));
            Add(new("00000000-0000-0000-0000-F00000000016"), v => BitConverter.GetBytes(v.ToDateTime(new()).ToBinary()), t => DateOnly.FromDateTime(new(BitConverter.ToInt64(t))));
            Add(new("00000000-0000-0000-0000-F00000000017"), v => BitConverter.GetBytes(DateOnly.FromDateTime(DateTime.Today).ToDateTime(v).ToBinary()), t => TimeOnly.FromDateTime(new(BitConverter.ToInt64(t))));
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
            Add(new("00000000-0000-0000-0000-F10000000012"), v => [.. decimal.GetBits(v).SelectMany(x => BitConverter.GetBytes(x))], t => new decimal(SpanTranslation(t, 4, b => BitConverter.ToInt32(b, 0))));
            Add(new("00000000-0000-0000-0000-F10000000013"),    v => [.. v.SelectMany(x => BitConverter.GetBytes(x))],    t => SpanTranslation(t, 2, b => BitConverter.ToChar(b, 0)));
            Add(new("00000000-0000-0000-0000-F10000000014"), EncodeStringArray, DecodeStringArray);
            Add(new("00000000-0000-0000-0000-F10000000015"), v => [.. v.SelectMany(x => BitConverter.GetBytes(x.ToBinary()))], t => SpanTranslation(t, 8, b => DateTime.FromBinary(BitConverter.ToInt64(b, 0))));
            Add(new("00000000-0000-0000-0000-F10000000016"), v => [.. v.SelectMany(x => BitConverter.GetBytes(x.DayNumber))], t => SpanTranslation(t, 4, b => DateOnly.FromDayNumber(BitConverter.ToInt32(b, 0))));
            Add(new("00000000-0000-0000-0000-F10000000017"), v => [.. v.SelectMany(x => BitConverter.GetBytes(x.Ticks))], t => SpanTranslation(t, 8, b => new TimeOnly(BitConverter.ToInt64(b, 0))));
            Add(new("00000000-0000-0000-0000-F10000000018"), v => [.. v.SelectMany(x => BitConverter.GetBytes(x.Ticks))], t => SpanTranslation(t, 8, b => new TimeSpan(BitConverter.ToInt64(b, 0))));
            Add(new("00000000-0000-0000-0000-F10000000019"), v => [.. v.SelectMany(x => x.ToByteArray())], t => SpanTranslation(t, 16, b => new Guid(b)));
        }

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
