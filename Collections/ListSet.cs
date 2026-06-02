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
    /// Represents a protocol-aware set backed by a list and optional equality comparer.
    /// </summary>
    /// <typeparam name="T">The item type.</typeparam>
    [BurcatIdentity("00000000-0000-0000-0000-F10000000001")]
    public class ListSet<T> : BurcatObject, ISet<T>, IReadOnlySet<T>
    {
        private readonly BurcatList<T> items;
        private IEqualityComparer<T>? Comparer { get; }

        /// <inheritdoc/>
        public int Count => items.Count;

        /// <inheritdoc/>
        public bool IsReadOnly => false;

        /// <summary>
        /// Initializes a referenced set from values and a comparer.
        /// </summary>
        /// <param name="identifier">The set identifier.</param>
        /// <param name="values">The initial values.</param>
        /// <param name="comparer">The equality comparer, which must also be a Burcat object.</param>
        public ListSet(Guid identifier, IEnumerable<T> values, IEqualityComparer<T> comparer) : base(identifier)
        {
            if (comparer is not IBurcatObject) throw new ArgumentException("The comparer must also be a BDP object", nameof(comparer));
            else
            {
                items = new(identifier, values);
                Comparer = comparer;
            }
        }

        /// <summary>
        /// Initializes a referenced set from values.
        /// </summary>
        /// <param name="identifier">The set identifier.</param>
        /// <param name="values">The initial values.</param>
        public ListSet(Guid identifier, IEnumerable<T> values) : base(identifier) { items = new(identifier, values); }

        /// <summary>
        /// Initializes an empty referenced set with a comparer.
        /// </summary>
        /// <param name="identifier">The set identifier.</param>
        /// <param name="comparer">The equality comparer, which must also be a Burcat object.</param>
        public ListSet(Guid identifier, IEqualityComparer<T> comparer) : this(identifier, [], comparer) { }

        /// <summary>
        /// Initializes an empty referenced set.
        /// </summary>
        /// <param name="identifier">The set identifier.</param>
        public ListSet(Guid identifier) : this(identifier, []) { }

        /// <summary>
        /// Initializes an unreferenced set from values and a comparer.
        /// </summary>
        /// <param name="values">The initial values.</param>
        /// <param name="comparer">The equality comparer, which must also be a Burcat object.</param>
        public ListSet(IEnumerable<T> values, IEqualityComparer<T> comparer) : base(Guid.Empty)
        {
            if (comparer is not IBurcatObject) throw new ArgumentException("The comparer must also be a BDP object", nameof(comparer));
            else
            {
                items = [.. values];
                Comparer = comparer;
            }
        }

        /// <summary>
        /// Initializes an unreferenced set from values.
        /// </summary>
        /// <param name="values">The initial values.</param>
        public ListSet(IEnumerable<T> values) : base(Guid.Empty) { items = [.. values]; }

        /// <summary>
        /// Initializes an empty unreferenced set with a comparer.
        /// </summary>
        /// <param name="comparer">The equality comparer, which must also be a Burcat object.</param>
        public ListSet(IEqualityComparer<T> comparer) : this([], comparer) { }

        /// <summary>
        /// Initializes an empty unreferenced set.
        /// </summary>
        public ListSet() : this([]) { }

        /// <inheritdoc/>
        public bool Add(T item)
        {
            if (Contains(item)) return false;
            else
            {
                if (Identifier != Guid.Empty) Revision = GuidExtensions.GenerateRandom();

                items.Add(item);
                return true;
            }
        }

        /// <inheritdoc/>
        public bool Remove(T item)
        {
            int index = items.IndexOf(item);
            if (index < 0) return false;
            else
            {
                if (Identifier != Guid.Empty) Revision = GuidExtensions.GenerateRandom();

                items.RemoveAt(index);
                return true;
            }
        }

        /// <inheritdoc/>
        public bool Contains(T item) => IndexOf(item) >= 0;

        /// <inheritdoc/>
        public void Clear()
        {
            if (Count != 0 && Identifier != Guid.Empty) Revision = GuidExtensions.GenerateRandom();
            items.Clear();
        }

        /// <inheritdoc/>
        public void CopyTo(T[] array, int arrayIndex) => items.CopyTo(array, arrayIndex);

        /// <inheritdoc/>
        public IEnumerator<T> GetEnumerator() => items.GetEnumerator();

        /// <inheritdoc/>
        public void UnionWith(IEnumerable<T> other)
        {
            foreach (T item in other)
                Add(item);
        }

        /// <inheritdoc/>
        public void IntersectWith(IEnumerable<T> other)
        {
            foreach (T item in other.Where(i => !items.Contains(i)))
                Remove(item);
        }

        /// <inheritdoc/>
        public void ExceptWith(IEnumerable<T> other)
        {
            foreach (T item in other)
                Remove(item);
        }

        /// <inheritdoc/>
        public void SymmetricExceptWith(IEnumerable<T> other)
        {
            foreach (T item in other)
                if (!Remove(item))
                    Add(item);
        }

        /// <inheritdoc/>
        public bool IsSubsetOf(IEnumerable<T> other)
        {
            foreach (T item in items)
                if (!other.Contains(item, Comparer))
                    return false;

            return true;
        }

        /// <inheritdoc/>
        public bool IsSupersetOf(IEnumerable<T> other)
        {
            foreach (T item in other)
                if (!Contains(item))
                    return false;

            return true;
        }

        /// <inheritdoc/>
        public bool IsProperSupersetOf(IEnumerable<T> other) => Count > other.Count() && IsSupersetOf(other);

        /// <inheritdoc/>
        public bool IsProperSubsetOf(IEnumerable<T> other) => Count < other.Count() && IsSubsetOf(other);

        /// <inheritdoc/>
        public bool Overlaps(IEnumerable<T> other)
        {
            foreach (T item in other)
                if (Contains(item))
                    return true;

            return false;
        }

        /// <inheritdoc/>
        public bool SetEquals(IEnumerable<T> other)
        {
            if (Count == other.Count())
            {
                foreach (T item in items)
                    if (!other.Contains(item, Comparer))
                        return false;

                return true;
            }
            else return false;
        }

        private int IndexOf(T item)
        {
            if (Comparer is IEqualityComparer<T> comparer)
            {
                for (int i = 0; i < items.Count; i++)
                    if (comparer.Equals(items[i], item))
                        return i;

                return -1;
            }
            else return items.IndexOf(item);
        }

        /// <inheritdoc/>
        void ICollection<T>.Add(T item) => Add(item);

        /// <inheritdoc/>
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        /// <inheritdoc/>
        public override object?[] GetBurcatConstructionValues()
        {
            if (Comparer is IEqualityComparer<T> comparer) return [new BurcatType(typeof(T)), Identifier, items, (IBurcatObject)comparer];
            else return [new BurcatType(typeof(T)), Identifier, items];
        }
    }
}
