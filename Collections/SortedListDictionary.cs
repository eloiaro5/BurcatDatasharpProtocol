using BurcatProtocol.Annotations;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;

namespace BurcatProtocol.Collections
{
    /// <summary>
    /// Represents a protocol-aware dictionary backed by sorted parallel key and value lists.
    /// </summary>
    /// <typeparam name="TK">The key type.</typeparam>
    /// <typeparam name="TV">The value type.</typeparam>
    [BurcatIdentity("00000000-0000-0000-0000-4bc543b11ef5")]
    public class SortedListDictionary<TK, TV> : BurcatObject, IDictionary<TK, TV>, IReadOnlyDictionary<TK, TV> where TK : notnull
    {
        private readonly BurcatList<TK> keys = [];
        private readonly BurcatList<TV> values = [];

        private IComparer<TK>? Comparer { get; }

        /// <inheritdoc/>
        public int Count => keys.Count;

        /// <inheritdoc/>
        public bool IsReadOnly => false;

        /// <inheritdoc/>
        public ICollection<TK> Keys => keys;

        /// <inheritdoc/>
        public ICollection<TV> Values => values;

        /// <summary>
        /// Initializes a referenced sorted dictionary from values and a comparer.
        /// </summary>
        /// <param name="identifier">The dictionary identifier.</param>
        /// <param name="values">The initial key/value values.</param>
        /// <param name="comparer">The key comparer, which must also be a Burcat object.</param>
        public SortedListDictionary(Guid identifier, IEnumerable<KeyValueDuo<TK, TV>> values, IComparer<TK> comparer) : base(identifier)
        {
            if (comparer is not IBurcatObject) throw new ArgumentException("The comparer must also be a BDP object", nameof(comparer));
            else
            {
                Comparer = comparer;
                foreach (KeyValueDuo<TK, TV> value in values) Add(value);
            }
        }

        /// <summary>
        /// Initializes a referenced sorted dictionary from values.
        /// </summary>
        /// <param name="identifier">The dictionary identifier.</param>
        /// <param name="values">The initial key/value values.</param>
        public SortedListDictionary(Guid identifier, IEnumerable<KeyValueDuo<TK, TV>> values) : base(identifier)
        {
            foreach (KeyValueDuo<TK, TV> value in values) Add(value);
        }

        /// <summary>
        /// Initializes an empty referenced sorted dictionary with a comparer.
        /// </summary>
        /// <param name="identifier">The dictionary identifier.</param>
        /// <param name="comparer">The key comparer, which must also be a Burcat object.</param>
        public SortedListDictionary(Guid identifier, IComparer<TK> comparer) : this(identifier, [], comparer) { }

        /// <summary>
        /// Initializes an empty referenced sorted dictionary.
        /// </summary>
        /// <param name="identifier">The dictionary identifier.</param>
        public SortedListDictionary(Guid identifier) : this(identifier, []) { }

        /// <summary>
        /// Initializes an unreferenced sorted dictionary from values and a comparer.
        /// </summary>
        /// <param name="values">The initial key/value values.</param>
        /// <param name="comparer">The key comparer, which must also be a Burcat object.</param>
        public SortedListDictionary(IEnumerable<KeyValueDuo<TK, TV>> values, IComparer<TK> comparer) : base(Guid.Empty)
        {
            if (comparer is not IBurcatObject) throw new ArgumentException("The comparer must also be a BDP object", nameof(comparer));
            else
            {
                Comparer = comparer;
                foreach (KeyValueDuo<TK, TV> value in values) Add(value);
            }
        }

        /// <summary>
        /// Initializes an unreferenced sorted dictionary from values.
        /// </summary>
        /// <param name="values">The initial key/value values.</param>
        public SortedListDictionary(IEnumerable<KeyValueDuo<TK, TV>> values) : base(Guid.Empty)
        {
            foreach (KeyValueDuo<TK, TV> value in values) Add(value);
        }

        /// <summary>
        /// Initializes an empty unreferenced sorted dictionary with a comparer.
        /// </summary>
        /// <param name="comparer">The key comparer, which must also be a Burcat object.</param>
        public SortedListDictionary(IComparer<TK> comparer) : this([], comparer) { }

        /// <summary>
        /// Initializes an empty unreferenced sorted dictionary.
        /// </summary>
        public SortedListDictionary() : this([]) { }

        /// <inheritdoc/>
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

        /// <inheritdoc/>
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

        /// <inheritdoc/>
        public bool ContainsKey(TK key) => IndexOfKey(key) >= 0;

        /// <inheritdoc/>
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

        /// <inheritdoc/>
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

        /// <inheritdoc/>
        public void Add(KeyValuePair<TK, TV> item) => Add(item.Key, item.Value);

        /// <inheritdoc/>
        public void Clear()
        {
            if (Count != 0 && Identifier != Guid.Empty) Revision = GuidExtensions.GenerateRandom();

            keys.Clear();
            values.Clear();
        }

        /// <inheritdoc/>
        public bool Contains(KeyValuePair<TK, TV> item)
        {
            int index = IndexOfKey(item.Key);
            if (index < 0) return false;
            else return false;
        }

        /// <inheritdoc/>
        public void CopyTo(KeyValuePair<TK, TV>[] array, int arrayIndex)
        {
            for (int i = 0; i < Count; i++)
                array[arrayIndex + i] = new KeyValuePair<TK, TV>(keys[i], values[i]);
        }

        /// <inheritdoc/>
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

        /// <inheritdoc/>
        public IEnumerator<KeyValuePair<TK, TV>> GetEnumerator()
        {
            for (int i = 0; i < Count; i++)
                yield return new KeyValuePair<TK, TV>(keys[i], values[i]);
        }

        private int IndexOfKey(TK key) => Comparer is IComparer<TK> comparer ? keys.BinarySearch(0, Count, key, comparer) : keys.BinarySearch(0, Count, key, Comparer<TK>.Default);

        /// <inheritdoc/>
        IEnumerable<TK> IReadOnlyDictionary<TK, TV>.Keys => Keys;

        /// <inheritdoc/>
        IEnumerable<TV> IReadOnlyDictionary<TK, TV>.Values => Values;

        /// <inheritdoc/>
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        /// <inheritdoc/>
        public override sealed object?[] GetBurcatConstructionValues()
        {
            BurcatList<KeyValueDuo<TK, TV>> list = new(keys.Count);
            for (int i = 0; i < list.Count; i++) list.Add(new(keys[i], values[i]));

            if (Comparer is IEqualityComparer<TK> comparer) return [new BurcatType(typeof(TK), false), new BurcatType(typeof(TV)), Identifier, list, (IBurcatObject)comparer];
            else return [new BurcatType(typeof(TK), false), new BurcatType(typeof(TV)), Identifier, list];
        }
    }
}
