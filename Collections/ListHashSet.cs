using BurcatProtocol.Annotations;
using System.Collections;

namespace BurcatProtocol.Collections
{
    /// <summary>Represents a protocol-aware hash set backed by sorted hash groups.</summary>
    /// <typeparam name="T">The item type.</typeparam>
    [BurcatIdentity("00000000-0000-0000-0000-3c5d18f52948")]
    public class ListHashSet<T> : BurcatObject, ISet<T>, IReadOnlySet<T> where T : notnull
    {
        private readonly BurcatList<KeyValueDuo<int, BurcatList<T>>> buckets = [];
        private readonly IEqualityComparer<T> comparer;

        /// <inheritdoc/>
        public int Count { get; private set; }
        /// <inheritdoc/>
        public bool IsReadOnly => false;

        /// <summary>Initializes a referenced set from values and a comparer.</summary>
        public ListHashSet(Guid identifier, IEnumerable<T> values, IEqualityComparer<T> comparer) : base(identifier)
        {
            if (comparer is not IBurcatObject) throw new ArgumentException("The comparer must also be a BDP object", nameof(comparer));

            this.comparer = comparer;
            foreach (T item in values) Add(item);
        }

        /// <summary>Initializes a referenced set from values.</summary>
        public ListHashSet(Guid identifier, IEnumerable<T> values) : this(identifier, values, BurcatEqualityComparer<T>.Default) { }

        /// <summary>Initializes an empty referenced set with a comparer.</summary>
        public ListHashSet(Guid identifier, IEqualityComparer<T> comparer) : this(identifier, [], comparer) { }

        /// <summary>Initializes an empty referenced set.</summary>
        public ListHashSet(Guid identifier) : this(identifier, []) { }

        /// <summary>Initializes an unreferenced set from values and a comparer.</summary>
        public ListHashSet(IEnumerable<T> values, IEqualityComparer<T> comparer) : this(Guid.Empty, values, comparer) { }

        /// <summary>Initializes an unreferenced set from values.</summary>
        public ListHashSet(IEnumerable<T> values) : this(Guid.Empty, values) { }

        /// <summary>Initializes an empty unreferenced set with a comparer.</summary>
        public ListHashSet(IEqualityComparer<T> comparer) : this([], comparer) { }

        /// <summary>Initializes an empty unreferenced set.</summary>
        public ListHashSet() : this([]) { }

        /// <inheritdoc/>
        public bool Add(T item)
        {
            if (TryFindItem(item, out int bucketIndex, out _)) return false;
            else
            {
                if (bucketIndex >= 0) buckets[bucketIndex].Value.Add(item);
                else buckets.Insert(~bucketIndex, new(comparer.GetHashCode(item), [item]));
                Count++;

                if (Identifier != Guid.Empty) Revision = GuidExtensions.GenerateRandom();
                return true;
            }
        }

        /// <inheritdoc/>
        public bool Remove(T item)
        {
            if (TryFindItem(item, out int bucketIndex, out int itemIndex))
            {
                BurcatList<T> bucket = buckets[bucketIndex].Value;

                bucket.RemoveAt(itemIndex);
                if (bucket.Count == 0) buckets.RemoveAt(bucketIndex);
                Count--;

                if (Identifier != Guid.Empty) Revision = GuidExtensions.GenerateRandom();
                return true;
            }
            else return false;
        }

        /// <inheritdoc/>
        public bool Contains(T item) => TryFindItem(item, out _, out _);

        /// <summary>Searches for an item and returns the stored value that compares equal.</summary>
        public bool TryGetValue(T equalValue, out T actualValue)
        {
            if (TryFindItem(equalValue, out int bucketIndex, out int itemIndex))
            {
                actualValue = buckets[bucketIndex].Value[itemIndex];
                return true;
            }

            actualValue = default!;
            return false;
        }

        /// <inheritdoc/>
        public void Clear()
        {
            if (Count != 0)
            {
                buckets.Clear();
                Count = 0;

                if (Identifier != Guid.Empty) Revision = GuidExtensions.GenerateRandom();
            }
        }

        /// <summary>Copies all elements to an array.</summary>
        public void CopyTo(T[] array) => CopyTo(array, 0, Count);
        /// <inheritdoc/>
        public void CopyTo(T[] array, int arrayIndex) => CopyTo(array, arrayIndex, Count);
        /// <summary>Copies a specified number of elements to an array.</summary>
        public void CopyTo(T[] array, int arrayIndex, int count)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(arrayIndex);
            ArgumentOutOfRangeException.ThrowIfNegative(count);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(count, Count);

            if (arrayIndex > array.Length || array.Length - arrayIndex < count) throw new ArgumentException("The destination array does not have enough available space.", nameof(array));
            else
            {
                using IEnumerator<T> enumerator = GetEnumerator();
                while (count-- > 0 && enumerator.MoveNext())
                    array[arrayIndex++] = enumerator.Current;
            }
        }

        /// <inheritdoc/>
        public void UnionWith(IEnumerable<T> other)
        {
            foreach (T item in other)
                Add(item);
        }

        /// <inheritdoc/>
        public void IntersectWith(IEnumerable<T> other)
        {
            ListHashSet<T> otherSet = new(other, comparer);
            int removed = 0;

            for (int bucketIndex = buckets.Count - 1; bucketIndex >= 0; bucketIndex--)
            {
                BurcatList<T> bucket = buckets[bucketIndex].Value;
                for (int itemIndex = bucket.Count - 1; itemIndex >= 0; itemIndex--)
                    if (!otherSet.Contains(bucket[itemIndex]))
                    {
                        bucket.RemoveAt(itemIndex);
                        removed++;
                    }

                if (bucket.Count == 0) buckets.RemoveAt(bucketIndex);
            }

            if (removed != 0)
            {
                Count -= removed;

                if (Identifier != Guid.Empty) Revision = GuidExtensions.GenerateRandom();
            }
        }

        /// <inheritdoc/>
        public void ExceptWith(IEnumerable<T> other)
        {
            if (ReferenceEquals(other, this)) Clear();
            else
                foreach (T item in other)
                    Remove(item);
        }

        /// <inheritdoc/>
        public void SymmetricExceptWith(IEnumerable<T> other)
        {
            if (ReferenceEquals(other, this)) Clear();
            else
                foreach (T item in new ListHashSet<T>(other, comparer))
                    if (!Remove(item)) Add(item);
        }

        /// <inheritdoc/>
        public bool IsSubsetOf(IEnumerable<T> other)
        {
            ListHashSet<T> otherSet = new(other, comparer);
            return Count <= otherSet.Count && this.All(otherSet.Contains);
        }

        /// <inheritdoc/>
        public bool IsSupersetOf(IEnumerable<T> other) => other.All(Contains);

        /// <inheritdoc/>
        public bool IsProperSupersetOf(IEnumerable<T> other)
        {
            ListHashSet<T> otherSet = new(other, comparer);
            return Count > otherSet.Count && otherSet.All(Contains);
        }

        /// <inheritdoc/>
        public bool IsProperSubsetOf(IEnumerable<T> other)
        {
            ListHashSet<T> otherSet = new(other, comparer);
            return Count < otherSet.Count && this.All(otherSet.Contains);
        }

        /// <inheritdoc/>
        public bool Overlaps(IEnumerable<T> other) => other.Any(Contains);

        /// <inheritdoc/>
        public bool SetEquals(IEnumerable<T> other)
        {
            ListHashSet<T> otherSet = new(other, comparer);
            return Count == otherSet.Count && this.All(otherSet.Contains);
        }

        /// <inheritdoc/>
        public IEnumerator<T> GetEnumerator()
        {
            foreach (KeyValueDuo<int, BurcatList<T>> pair in buckets)
                foreach (T item in pair.Value)
                    yield return item;
        }

        private bool TryFindItem(T item, out int bucketIndex, out int itemIndex)
        {
            int hash = comparer.GetHashCode(item), low = 0, high = buckets.Count - 1;
            while (low <= high)
            {
                int middle = low + ((high - low) / 2);
                int comparison = buckets[middle].Key.CompareTo(hash);
                if (comparison < 0) low = middle + 1;
                else if (comparison > 0) high = middle - 1;
                else
                {
                    bucketIndex = middle;
                    BurcatList<T> bucket = buckets[middle].Value;
                    for (itemIndex = 0; itemIndex < bucket.Count; itemIndex++)
                        if (comparer.Equals(bucket[itemIndex], item))
                            return true;

                    itemIndex = -1;
                    return false;
                }
            }

            bucketIndex = ~low;
            itemIndex = -1;
            return false;
        }

        /// <inheritdoc/>
        void ICollection<T>.Add(T item) => Add(item);

        /// <inheritdoc/>
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        /// <inheritdoc/>
        public override sealed object?[] GetBurcatConstructionValues()
        {
            BurcatList<T> values = [.. this];
            if (comparer is BurcatEqualityComparer<T>) return [new BurcatType(typeof(T)), Identifier, values];
            return [new BurcatType(typeof(T)), Identifier, values, (IBurcatObject)comparer];
        }
    }
}
