using BurcatProtocol.Annotations;
using System.Collections;

namespace BurcatProtocol.Collections
{
    /// <summary>Prototypes a protocol-aware set backed by open-addressed hash storage.</summary>
    /// <typeparam name="T">The item type.</typeparam>
    [BurcatIdentity("00000000-0000-0000-0000-3c5d18f52949")]
    public class ListHashSet<T> : BurcatObject, ISet<T>, IReadOnlySet<T> where T : notnull
    {
        private readonly BurcatHashTable<T> items;
        private readonly IEqualityComparer<T> comparer;

        /// <inheritdoc/>
        public int Count => items.Count;
        /// <inheritdoc/>
        public bool IsReadOnly => false;

        /// <summary>Initializes a referenced set from values and a comparer.</summary>
        public ListHashSet(Guid identifier, IEnumerable<T> values, IEqualityComparer<T> comparer) : base(identifier)
        {
            if (comparer is not IBurcatObject) throw new ArgumentException("The comparer must also be a BDP object", nameof(comparer));
            else
            {
                this.comparer = comparer;
                items = new(values, comparer);
            }
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
            if (items.Add(item))
            {
                if (Identifier != Guid.Empty) Revision = GuidExtensions.GenerateRandom();
                return true;
            }
            else return false;
        }

        /// <inheritdoc/>
        public bool Remove(T item)
        {
            if (items.Remove(item))
            {
                if (Identifier != Guid.Empty) Revision = GuidExtensions.GenerateRandom();
                return true;
            }
            else return false;
        }

        /// <inheritdoc/>
        public bool Contains(T item) => items.Contains(item);

        /// <summary>Searches for an item and returns the stored value that compares equal.</summary>
        public bool TryGetValue(T equalValue, out T actualValue) => items.TryGetValue(equalValue, out actualValue);

        /// <inheritdoc/>
        public void Clear()
        {
            if (Count != 0)
            {
                items.Clear();
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
                while (count-- > 0 && enumerator.MoveNext()) array[arrayIndex++] = enumerator.Current;
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
            BurcatList<T> removedItems = [.. this.Where(item => !otherSet.Contains(item))];
            foreach (T item in removedItems) Remove(item);
        }

        /// <inheritdoc/>
        public void ExceptWith(IEnumerable<T> other)
        {
            if (ReferenceEquals(other, this)) Clear();
            else
                foreach (T item in other) Remove(item);
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
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        /// <inheritdoc/>
        public IEnumerator<T> GetEnumerator() => items.GetEnumerator();
        /// <inheritdoc/>
        void ICollection<T>.Add(T item) => Add(item);

        /// <inheritdoc/>
        public override sealed object?[] GetBurcatConstructionValues()
        {
            BurcatList<T> values = [.. this];
            if (comparer is BurcatEqualityComparer<T>) return [new BurcatType(typeof(T)), Identifier, values];
            return [new BurcatType(typeof(T)), Identifier, values, (IBurcatObject)comparer];
        }
    }
}
