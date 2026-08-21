using BurcatProtocol.Annotations;
using System.Collections;

namespace BurcatProtocol.Collections
{
    /// <summary>Represents a protocol-aware dictionary backed by sorted hash groups.</summary>
    /// <typeparam name="TK">The key type.</typeparam>
    /// <typeparam name="TV">The value type.</typeparam>
    [BurcatIdentity("00000000-0000-0000-0000-3c5d18f52949")]
    public class ListHashDictionary<TK, TV> : BurcatObject, IDictionary<TK, TV>, IReadOnlyDictionary<TK, TV> where TK : notnull
    {
        private readonly BurcatList<KeyValueDuo<int, BurcatList<KeyValueDuo<TK, TV>>>> buckets = [];

        /// <summary>Gets the comparer used to determine key equality.</summary>
        public IEqualityComparer<TK> Comparer { get; }

        /// <inheritdoc/>
        public int Count { get; private set; }

        /// <inheritdoc/>
        public bool IsReadOnly => false;

        /// <inheritdoc/>
        public ICollection<TK> Keys => [.. this.Select(pair => pair.Key)];

        /// <inheritdoc/>
        public ICollection<TV> Values => [.. this.Select(pair => pair.Value)];

        /// <summary>Initializes a referenced dictionary from values and a comparer.</summary>
        public ListHashDictionary(Guid identifier, IEnumerable<KeyValueDuo<TK, TV>> values, IEqualityComparer<TK> comparer) : base(identifier)
        {
            ArgumentNullException.ThrowIfNull(values);
            ArgumentNullException.ThrowIfNull(comparer);
            if (comparer is not IBurcatObject) throw new ArgumentException("The comparer must also be a BDP object", nameof(comparer));

            Comparer = comparer;
            foreach (KeyValueDuo<TK, TV> pair in values) Add(pair.Key, pair.Value);
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
                if (!TryFindEntry(key, out int bucketIndex, out int entryIndex)) throw new KeyNotFoundException();
                return buckets[bucketIndex].Value[entryIndex].Value;
            }
            set
            {
                if (TryFindEntry(key, out int bucketIndex, out int entryIndex))
                {
                    KeyValueDuo<TK, TV> entry = buckets[bucketIndex].Value[entryIndex];
                    buckets[bucketIndex].Value[entryIndex] = new(entry.Key, value);
                    TouchRevision();
                }
                else AddCore(key, value, bucketIndex);
            }
        }

        /// <inheritdoc/>
        public void Add(TK key, TV value)
        {
            if (TryFindEntry(key, out int bucketIndex, out _)) throw new ArgumentException("Key already exists.", nameof(key));
            AddCore(key, value, bucketIndex);
        }

        /// <inheritdoc/>
        public bool ContainsKey(TK key) => TryFindEntry(key, out _, out _);

        /// <inheritdoc/>
        public bool Remove(TK key)
        {
            if (!TryFindEntry(key, out int bucketIndex, out int entryIndex)) return false;

            BurcatList<KeyValueDuo<TK, TV>> bucket = buckets[bucketIndex].Value;
            bucket.RemoveAt(entryIndex);
            if (bucket.Count == 0) buckets.RemoveAt(bucketIndex);
            Count--;

            TouchRevision();
            return true;
        }

        /// <inheritdoc/>
        public bool TryGetValue(TK key, out TV value)
        {
            if (TryFindEntry(key, out int bucketIndex, out int entryIndex))
            {
                value = buckets[bucketIndex].Value[entryIndex].Value;
                return true;
            }

            value = default!;
            return false;
        }

        /// <inheritdoc/>
        public void Add(KeyValuePair<TK, TV> item) => Add(item.Key, item.Value);

        /// <inheritdoc/>
        public void Clear()
        {
            if (Count == 0) return;

            buckets.Clear();
            Count = 0;
            TouchRevision();
        }

        /// <inheritdoc/>
        public bool Contains(KeyValuePair<TK, TV> item) => TryGetValue(item.Key, out TV? value) && EqualityComparer<TV>.Default.Equals(value, item.Value);

        /// <inheritdoc/>
        public void CopyTo(KeyValuePair<TK, TV>[] array, int arrayIndex)
        {
            ArgumentNullException.ThrowIfNull(array);
            ArgumentOutOfRangeException.ThrowIfNegative(arrayIndex);
            if (arrayIndex > array.Length || array.Length - arrayIndex < Count) throw new ArgumentException("The destination array does not have enough available space.", nameof(array));

            foreach (KeyValuePair<TK, TV> pair in this) array[arrayIndex++] = pair;
        }

        /// <inheritdoc/>
        public bool Remove(KeyValuePair<TK, TV> item)
        {
            if (!Contains(item)) return false;
            return Remove(item.Key);
        }

        /// <inheritdoc/>
        public IEnumerator<KeyValuePair<TK, TV>> GetEnumerator()
        {
            foreach (KeyValueDuo<int, BurcatList<KeyValueDuo<TK, TV>>> hashGroup in buckets)
                foreach (KeyValueDuo<TK, TV> entry in hashGroup.Value)
                    yield return entry;
        }

        private void AddCore(TK key, TV value, int bucketIndex)
        {
            KeyValueDuo<TK, TV> entry = new(key, value);
            if (bucketIndex >= 0) buckets[bucketIndex].Value.Add(entry);
            else buckets.Insert(~bucketIndex, new(Comparer.GetHashCode(key), [entry]));
            Count++;

            TouchRevision();
        }

        private bool TryFindEntry(TK key, out int bucketIndex, out int entryIndex)
        {
            int hash = Comparer.GetHashCode(key), low = 0, high = buckets.Count - 1;
            while (low <= high)
            {
                int middle = low + ((high - low) / 2);
                int comparison = buckets[middle].Key.CompareTo(hash);
                if (comparison < 0) low = middle + 1;
                else if (comparison > 0) high = middle - 1;
                else
                {
                    bucketIndex = middle;
                    BurcatList<KeyValueDuo<TK, TV>> bucket = buckets[middle].Value;
                    for (entryIndex = 0; entryIndex < bucket.Count; entryIndex++)
                        if (Comparer.Equals(bucket[entryIndex].Key, key))
                            return true;

                    entryIndex = -1;
                    return false;
                }
            }

            bucketIndex = ~low;
            entryIndex = -1;
            return false;
        }

        private void TouchRevision()
        {
            if (Identifier != Guid.Empty) Revision = GuidExtensions.GenerateRandom();
        }

        /// <inheritdoc/>
        IEnumerable<TK> IReadOnlyDictionary<TK, TV>.Keys => Keys;

        /// <inheritdoc/>
        IEnumerable<TV> IReadOnlyDictionary<TK, TV>.Values => Values;

        /// <inheritdoc/>
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        /// <inheritdoc/>
        public override sealed object?[] GetBurcatConstructionValues()
        {
            BurcatList<KeyValueDuo<TK, TV>> values = [.. this.Select(pair => (KeyValueDuo<TK, TV>)pair)];
            if (Comparer is BurcatEqualityComparer<TK>) return [new BurcatType(typeof(TK), false), new BurcatType(typeof(TV)), Identifier, values];
            return [new BurcatType(typeof(TK), false), new BurcatType(typeof(TV)), Identifier, values, (IBurcatObject)Comparer];
        }
    }
}
