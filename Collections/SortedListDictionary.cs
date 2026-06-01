using BurcatProtocol.Annotations;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;

namespace BurcatProtocol.Collections
{
    [BurcatIdentity("00000000-0000-0000-0000-4bc543b11ef5")]
    public class SortedListDictionary<TK, TV> : BurcatObject, IDictionary<TK, TV>, IReadOnlyDictionary<TK, TV> where TK : notnull
    {
        private readonly BurcatList<TK> keys = [];
        private readonly BurcatList<TV> values = [];

        private IComparer<TK>? Comparer { get; }

        public int Count => keys.Count;
        public bool IsReadOnly => false;
        public ICollection<TK> Keys => keys;
        public ICollection<TV> Values => values;

        public SortedListDictionary(Guid identifier, IEnumerable<KeyValueDuo<TK, TV>> values, IComparer<TK> comparer) : base(identifier)
        {
            if (comparer is not IBurcatObject) throw new ArgumentException("The comparer must also be a BDP object", nameof(comparer));
            else
            {
                Comparer = comparer;
                foreach (KeyValueDuo<TK, TV> value in values) Add(value);
            }
        }
        public SortedListDictionary(Guid identifier, IEnumerable<KeyValueDuo<TK, TV>> values) : base(identifier)
        {
            foreach (KeyValueDuo<TK, TV> value in values) Add(value);
        }
        public SortedListDictionary(Guid identifier, IComparer<TK> comparer) : this(identifier, [], comparer) { }
        public SortedListDictionary(Guid identifier) : this(identifier, []) { }

        public SortedListDictionary(IEnumerable<KeyValueDuo<TK, TV>> values, IComparer<TK> comparer) : base(Guid.Empty)
        {
            if (comparer is not IBurcatObject) throw new ArgumentException("The comparer must also be a BDP object", nameof(comparer));
            else
            {
                Comparer = comparer;
                foreach (KeyValueDuo<TK, TV> value in values) Add(value);
            }
        }
        public SortedListDictionary(IEnumerable<KeyValueDuo<TK, TV>> values) : base(Guid.Empty)
        {
            foreach (KeyValueDuo<TK, TV> value in values) Add(value);
        }
        public SortedListDictionary(IComparer<TK> comparer) : this([], comparer) { }
        public SortedListDictionary() : this([]) { }

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

                    int insertIndex = ~index;
                    keys.Insert(insertIndex, key);
                    values.Insert(insertIndex, value);
                }
            }
        }

        public void Add(TK key, TV value)
        {
            int index = IndexOfKey(key);
            if (index >= 0) throw new ArgumentException("Key already exists");
            else
            {
                if (Identifier != Guid.Empty) Revision = GuidExtensions.GenerateRandom();

                int insertIndex = ~index;
                keys.Insert(insertIndex, key);
                values.Insert(insertIndex, value);
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

        public bool Contains(KeyValuePair<TK, TV> item)
        {
            int index = IndexOfKey(item.Key);
            if (index < 0) return false;
            else return false;
        }

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

        private int IndexOfKey(TK key) => Comparer is IComparer<TK> comparer ? keys.BinarySearch(0, Count, key, comparer) : keys.BinarySearch(0, Count, key, Comparer<TK>.Default);

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
