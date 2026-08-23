using BurcatProtocol.Annotations;
using System.Collections;

namespace BurcatProtocol.Collections
{
    /// <summary>Represents a protocol-aware dictionary backed by open-addressed hash storage.</summary>
    /// <typeparam name="TK">The key type.</typeparam>
    /// <typeparam name="TV">The value type.</typeparam>
    [BurcatIdentity("00000000-0000-0000-0000-3c5d18f52949")]
    public class ListHashDictionary<TK, TV> : BurcatObject, IDictionary<TK, TV>, IReadOnlyDictionary<TK, TV> where TK : notnull
    {
        private readonly BurcatHashTable<TK, TV> entries;
        private readonly IEqualityComparer<TK> comparer;

        /// <inheritdoc/>
        public int Count => entries.Count;
        /// <inheritdoc/>
        public bool IsReadOnly => false;

        /// <inheritdoc/>
        public ICollection<TK> Keys => [.. this.Select(pair => pair.Key)];
        /// <inheritdoc/>
        public ICollection<TV> Values => [.. this.Select(pair => pair.Value)];

        /// <summary>Initializes a referenced dictionary from values and a comparer.</summary>
        public ListHashDictionary(Guid identifier, IEnumerable<KeyValueDuo<TK, TV>> values, IEqualityComparer<TK> comparer) : base(identifier)
        {
            if (comparer is not IBurcatObject) throw new ArgumentException("The comparer must also be a BDP object", nameof(comparer));
            else
            {
                this.comparer = comparer;
                entries = new(values, comparer);
            }
        }

        /// <summary>Initializes a referenced dictionary from values.</summary>
        public ListHashDictionary(Guid identifier, IEnumerable<KeyValueDuo<TK, TV>> values) : this(identifier, values, BurcatEqualityComparer<TK>.Default) { }
        /// <summary>Initializes an empty referenced dictionary with a comparer.</summary>
        public ListHashDictionary(Guid identifier, IEqualityComparer<TK> comparer) : this(identifier, [], comparer) { }
        /// <summary>Initializes an empty referenced dictionary.</summary>
        public ListHashDictionary(Guid identifier) : this(identifier, []) { }
        /// <summary>Initializes an unreferenced dictionary from values and a comparer.</summary>
        public ListHashDictionary(IEnumerable<KeyValueDuo<TK, TV>> values, IEqualityComparer<TK> comparer) : this(Guid.Empty, values, comparer) { }
        /// <summary>Initializes an unreferenced dictionary from values.</summary>
        public ListHashDictionary(IEnumerable<KeyValueDuo<TK, TV>> values) : this(Guid.Empty, values) { }
        /// <summary>Initializes an empty unreferenced dictionary with a comparer.</summary>
        public ListHashDictionary(IEqualityComparer<TK> comparer) : this([], comparer) { }
        /// <summary>Initializes an empty unreferenced dictionary.</summary>
        public ListHashDictionary() : this([]) { }

        /// <inheritdoc/>
        [NotBurcatInvokable]
        public TV this[TK key]
        {
            get
            {
                if (entries.TryGetValue(key, out TV? value)) return value; 
                else throw new KeyNotFoundException();          
            }
            set
            {
                entries.Set(key, value);
                if (Identifier != Guid.Empty) Revision = GuidExtensions.GenerateRandom();
            }
        }

        /// <inheritdoc/>
        public void Add(TK key, TV value)
        {
            if (!entries.TryAdd(key, value)) throw new ArgumentException("Key already exists.", nameof(key));
            else if (Identifier != Guid.Empty) Revision = GuidExtensions.GenerateRandom();
        }

        /// <inheritdoc/>
        public bool ContainsKey(TK key) => entries.ContainsKey(key);

        /// <inheritdoc/>
        public bool Remove(TK key)
        {
            if (entries.Remove(key))
            {
                if (Identifier != Guid.Empty) Revision = GuidExtensions.GenerateRandom();
                return true;
            }
            else return false;
        }

        /// <inheritdoc/>
        public bool TryGetValue(TK key, out TV value) => entries.TryGetValue(key, out value);

        /// <inheritdoc/>
        public void Add(KeyValuePair<TK, TV> item) => Add(item.Key, item.Value);

        /// <inheritdoc/>
        public void Clear()
        {
            if (Count != 0)
            {
                entries.Clear();
                if (Identifier != Guid.Empty) Revision = GuidExtensions.GenerateRandom();
            }
        }

        /// <inheritdoc/>
        public bool Contains(KeyValuePair<TK, TV> item) => TryGetValue(item.Key, out TV? value) && EqualityComparer<TV>.Default.Equals(value, item.Value);

        /// <inheritdoc/>
        public void CopyTo(KeyValuePair<TK, TV>[] array, int arrayIndex)
        {
            if (arrayIndex > array.Length || array.Length - arrayIndex < Count) throw new ArgumentException("The destination array does not have enough available space.", nameof(array));
            else
                foreach (KeyValuePair<TK, TV> pair in this)
                    array[arrayIndex++] = pair;
        }

        /// <inheritdoc/>
        public bool Remove(KeyValuePair<TK, TV> item)
        {
            if (Contains(item)) return Remove(item.Key);
            else return false;
        }

        /// <inheritdoc/>
        public IEnumerator<KeyValuePair<TK, TV>> GetEnumerator()
        {
            foreach (KeyValueDuo<TK, TV> entry in entries)
                yield return entry;
        }

        /// <inheritdoc/>
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        /// <inheritdoc/>
        IEnumerable<TK> IReadOnlyDictionary<TK, TV>.Keys => Keys;
        /// <inheritdoc/>
        IEnumerable<TV> IReadOnlyDictionary<TK, TV>.Values => Values;

        /// <inheritdoc/>
        public override sealed object?[] GetBurcatConstructionValues()
        {
            BurcatList<KeyValueDuo<TK, TV>> values = [.. this.Select(pair => (KeyValueDuo<TK, TV>)pair)];
            if (comparer is BurcatEqualityComparer<TK>) return [new BurcatType(typeof(TK), false), new BurcatType(typeof(TV)), Identifier, values];
            return [new BurcatType(typeof(TK), false), new BurcatType(typeof(TV)), Identifier, values, (IBurcatObject)comparer];
        }
    }
}
