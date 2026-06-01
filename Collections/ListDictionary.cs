using BurcatProtocol.Annotations;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace BurcatProtocol.Collections
{
    [BurcatIdentity("00000000-0000-0000-0000-3c5d18f52947")]
    public class ListDictionary<TK, TV> : BurcatObject, IDictionary<TK, TV>, IReadOnlyDictionary<TK, TV> where TK : notnull
    {
        private readonly BurcatList<TK> keys = [];
        private readonly BurcatList<TV> values = [];
        private IEqualityComparer<TK>? Comparer { get; }

        public int Count => keys.Count;
        public bool IsReadOnly => false;
        public ICollection<TK> Keys => keys;
        public ICollection<TV> Values => values;

        public ListDictionary(Guid identifier, IEnumerable<KeyValueDuo<TK, TV>> values, IEqualityComparer<TK> comparer) : base(identifier)
        {
            if (comparer is not IBurcatObject) throw new ArgumentException("The comparer must also be a BDP object", nameof(comparer));
            else
            {
                Comparer = comparer;
                foreach (KeyValueDuo<TK, TV> value in values) Add(value);
            }
        }
        public ListDictionary(Guid identifier, IEnumerable<KeyValueDuo<TK, TV>> values) : base(identifier)
        {
            foreach (KeyValueDuo<TK, TV> value in values) Add(value);
        }
        public ListDictionary(Guid identifier, IEqualityComparer<TK> comparer) : this(identifier, [], comparer) { }
        public ListDictionary(Guid identifier) : this(identifier, []) { }

        public ListDictionary(IEnumerable<KeyValueDuo<TK, TV>> values, IEqualityComparer<TK> comparer) : base(Guid.Empty)
        {
            if (comparer is not IBurcatObject) throw new ArgumentException("The comparer must also be a BDP object", nameof(comparer));
            else
            {
                Comparer = comparer;
                foreach (KeyValueDuo<TK, TV> value in values) Add(value);
            }
        }
        public ListDictionary(IEnumerable<KeyValueDuo<TK, TV>> values) : base(Guid.Empty)
        {
            foreach (KeyValueDuo<TK, TV> value in values) Add(value);
        }
        public ListDictionary(IEqualityComparer<TK> comparer) : this([], comparer) { }
        public ListDictionary() : this([]) { }

        [NotBurcatInvokable]
        public TV this[TK key]
        {
            get
            {
                int index = IndexOfKey(key);
                if (index < 0) throw new KeyNotFoundException();
                return values[index];
            }
            set
            {
                int index = IndexOfKey(key);
                if (index >= 0) values[index] = value;
                else
                {
                    if (Identifier != Guid.Empty) Revision = GuidExtensions.GenerateRandom();

                    keys.Add(key);
                    values.Add(value);
                }
            }
        }

        public void Add(TK key, TV value)
        {
            if (ContainsKey(key)) throw new ArgumentException("Key already exists.", nameof(key));
            else
            {
                if (Identifier != Guid.Empty) Revision = GuidExtensions.GenerateRandom();

                keys.Add(key);
                values.Add(value);
            }
        }

        public bool ContainsKey(TK key) => IndexOfKey(key) >= 0;

        public bool Remove(TK key)
        {
            int index = IndexOfKey(key);
            if (index < 0) return false;
            else
            {
                if (Identifier != Guid.Empty) Revision = GuidExtensions.GenerateRandom();

                keys.RemoveAt(index);
                values.RemoveAt(index);
                return true;
            }
        }

        public bool TryGetValue(TK key, out TV value)
        {
            int index = IndexOfKey(key);
            if (index >= 0)
            {
                value = values[index];
                return true;
            }
            else
            {
                value = default!;
                return false;
            }
        }

        public void Add(KeyValuePair<TK, TV> item) => Add(item.Key, item.Value);

        public void Clear()
        {
            if (Count != 0 && Identifier != Guid.Empty) Revision = GuidExtensions.GenerateRandom();

            keys.Clear();
            values.Clear();
        }

        public bool Contains(KeyValuePair<TK, TV> item) => IndexOfKey(item.Key) >= 0;

        public void CopyTo(KeyValuePair<TK, TV>[] array, int arrayIndex)
        {
            for (int i = 0; i < Count; i++)
                array[arrayIndex + i] = new KeyValuePair<TK, TV>(keys[i], values[i]);
        }

        public bool Remove(KeyValuePair<TK, TV> item)
        {
            int index = IndexOfKey(item.Key);
            if (index < 0) return false;
            else
            {
                if (Identifier != Guid.Empty) Revision = GuidExtensions.GenerateRandom();

                keys.RemoveAt(index);
                values.RemoveAt(index);
                return true;
            }
        }

        public IEnumerator<KeyValuePair<TK, TV>> GetEnumerator()
        {
            for (int i = 0; i < Count; i++)
                yield return new KeyValuePair<TK, TV>(keys[i], values[i]);
        }

        private int IndexOfKey(TK key)
        {
            if (Comparer is IEqualityComparer<TK> comparer)
            {
                for (int i = 0; i < keys.Count; i++)
                    if (comparer.Equals(keys[i], key))
                        return i;

                return -1;
            }
            else return keys.IndexOf(key);
        }

        IEnumerable<TK> IReadOnlyDictionary<TK, TV>.Keys => Keys;
        IEnumerable<TV> IReadOnlyDictionary<TK, TV>.Values => Values;
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public override sealed object?[] GetBurcatConstructionValues()
        {
            BurcatList<KeyValueDuo<TK, TV>> list = new(keys.Count);
            for (int i = 0; i < list.Count; i++) list.Add(new(keys[i], values[i]));

            if (Comparer is IEqualityComparer<TK> comparer) return [new BurcatType(typeof(TK), false), new BurcatType(typeof(TV)), Identifier, list, (IBurcatObject)comparer];
            else return [new BurcatType(typeof(TK), false), new BurcatType(typeof(TV)), Identifier, list];
        }
    }
}
