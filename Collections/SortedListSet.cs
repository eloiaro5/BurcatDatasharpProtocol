using BurcatProtocol.Annotations;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;

namespace BurcatProtocol.Collections
{
    /// <summary>
    /// Represents a protocol-aware sorted set backed by a list and optional comparer.
    /// </summary>
    /// <typeparam name="T">The item type.</typeparam>
    [BurcatIdentity("00000000-0000-0000-0000-5b447bc86de7")]
    public class SortedListSet<T> : BurcatObject, ISet<T>, IReadOnlySet<T> where T : IBurcatObject?
    {
        private readonly BurcatList<T> items;

        private IComparer<T>? Comparer { get; }
        private IEqualityComparer<T>? EqualityComparer { get; set; }

        /// <inheritdoc/>
        public int Count => items.Count;

        /// <inheritdoc/>
        public bool IsReadOnly => false;

        /// <summary>
        /// Initializes a referenced sorted set from values and a comparer.
        /// </summary>
        /// <param name="identifier">The set identifier.</param>
        /// <param name="values">The initial values.</param>
        /// <param name="comparer">The comparer, which must also be a Burcat object.</param>
        public SortedListSet(Guid identifier, IEnumerable<T> values, IComparer<T> comparer) : base(identifier)
        {
            if (comparer is not IBurcatObject) throw new ArgumentException("The comparer must also be a BDP object", nameof(comparer));
            else
            {
                items = new(identifier, values);
                Comparer = comparer;
            }
        }

        /// <summary>
        /// Initializes a referenced sorted set from values.
        /// </summary>
        /// <param name="identifier">The set identifier.</param>
        /// <param name="values">The initial values.</param>
        public SortedListSet(Guid identifier, IEnumerable<T> values) : base(identifier) { items = new(identifier, values); }

        /// <summary>
        /// Initializes an empty referenced sorted set with a comparer.
        /// </summary>
        /// <param name="identifier">The set identifier.</param>
        /// <param name="comparer">The comparer, which must also be a Burcat object.</param>
        public SortedListSet(Guid identifier, IComparer<T> comparer) : this(identifier, [], comparer) { }

        /// <summary>
        /// Initializes an empty referenced sorted set.
        /// </summary>
        /// <param name="identifier">The set identifier.</param>
        public SortedListSet(Guid identifier) : this(identifier, []) { }

        /// <summary>
        /// Initializes an unreferenced sorted set from values and a comparer.
        /// </summary>
        /// <param name="values">The initial values.</param>
        /// <param name="comparer">The comparer, which must also be a Burcat object.</param>
        public SortedListSet(IEnumerable<T> values, IComparer<T> comparer) : base(Guid.Empty)
        {
            if (comparer is not IBurcatObject) throw new ArgumentException("The comparer must also be a BDP object", nameof(comparer));
            else
            {
                items = [.. values];
                Comparer = comparer;
            }
        }

        /// <summary>
        /// Initializes an unreferenced sorted set from values.
        /// </summary>
        /// <param name="values">The initial values.</param>
        public SortedListSet(IEnumerable<T> values) : base(Guid.Empty) { items = [.. values]; }

        /// <summary>
        /// Initializes an empty unreferenced sorted set with a comparer.
        /// </summary>
        /// <param name="comparer">The comparer, which must also be a Burcat object.</param>
        public SortedListSet(IComparer<T> comparer) : this([], comparer) { }

        /// <summary>
        /// Initializes an empty unreferenced sorted set.
        /// </summary>
        public SortedListSet() : this([]) { }

        /// <inheritdoc/>
        public bool Add(T item)
        {
            int index = BinarySearch(item);
            if (index >= 0) return false;
            else
            {
                if (Identifier != Guid.Empty) Revision = GuidExtensions.GenerateRandom();

                items.Insert(~index, item);

                return true;
            }
        }

        /// <inheritdoc/>
        public bool Remove(T item)
        {
            int index = BinarySearch(item);
            if (index < 0) return false;
            else
            {
                if (Identifier != Guid.Empty) Revision = GuidExtensions.GenerateRandom();

                items.RemoveAt(index);
                return true;
            }
        }

        /// <inheritdoc/>
        public bool Contains(T item) => BinarySearch(item) >= 0;

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
            foreach (T item in other.Where(i => !items.Contains(i, GetComparer())))
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
                if (!other.Contains(item, GetComparer()))
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
                    if (!other.Contains(item, GetComparer()))
                        return false;

                return true;
            }
            else return false;
        }

        private int BinarySearch(T item) => Comparer is IComparer<T> comparer ? items.BinarySearch(0, Count, item, comparer) : items.BinarySearch(0, Count, item, Comparer<T>.Default);
        private IEqualityComparer<T> GetComparer() { EqualityComparer ??= new SetEqualityComparer(this); return EqualityComparer; }

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

        /// <summary>
        /// Adapts the sorted set comparer to equality comparison.
        /// </summary>
        private class SetEqualityComparer : IEqualityComparer<T>
        {
            private SortedListSet<T> Set { get; }

            public SetEqualityComparer(SortedListSet<T> set) { Set = set; }

            public bool Equals(T? x, T? y)
            {
                if (Set.Comparer is IComparer<T> comparer) return comparer.Compare(x, y) == 0;
                else if (x is null && y is null) return true;
                else if (x is null) return false;
                else if (y is null) return false;
                else if (ReferenceEquals(x, y)) return true;
                else return x.Equals(y);
            }

            public int GetHashCode([DisallowNull] T obj) => obj.GetHashCode();
        }
    }
}
