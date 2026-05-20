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
    [BurcatIdentity("00000000-0000-0000-0000-5b447bc86de7")]
    public class SortedListSet<T> : BurcatObject, ISet<T>, IReadOnlySet<T> where T : IBurcatObject?
    {
        private readonly BurcatList<T> items;

        private IComparer<T>? Comparer { get; }
        private IEqualityComparer<T>? EqualityComparer { get; set; }

        public int Count => items.Count;
        public bool IsReadOnly => false;

        public SortedListSet(Guid identifier, IEnumerable<T> values, IComparer<T> comparer) : base(identifier)
        {
            if (comparer is not IBurcatObject) throw new ArgumentException("The comparer must also be a BDP object", nameof(comparer));
            else
            {
                items = new(identifier, values);
                Comparer = comparer;
            }
        }
        public SortedListSet(Guid identifier, IEnumerable<T> values) : base(identifier) { items = new(identifier, values); }
        public SortedListSet(Guid identifier, IComparer<T> comparer) : this(identifier, [], comparer) { }
        public SortedListSet(Guid identifier) : this(identifier, []) { }

        public SortedListSet(IEnumerable<T> values, IComparer<T> comparer) : base(Guid.Empty)
        {
            if (comparer is not IBurcatObject) throw new ArgumentException("The comparer must also be a BDP object", nameof(comparer));
            else
            {
                items = [.. values];
                Comparer = comparer;
            }
        }
        public SortedListSet(IEnumerable<T> values) : base(Guid.Empty) { items = [.. values]; }
        public SortedListSet(IComparer<T> comparer) : this([], comparer) { }
        public SortedListSet() : this([]) { }

        public bool Add(T item)
        {
            int index = BinarySearch(item);
            if (index >= 0) return false;
            else
            {
                items.Insert(index, item);
                return true;
            }
        }

        public bool Remove(T item)
        {
            int index = BinarySearch(item);
            if (index < 0) return false;
            else
            {
                items.RemoveAt(index);
                return true;
            }
        }

        public bool Contains(T item) => BinarySearch(item) >= 0;

        public void Clear() => items.Clear();

        public void CopyTo(T[] array, int arrayIndex) => items.CopyTo(array, arrayIndex);

        public IEnumerator<T> GetEnumerator() => items.GetEnumerator();

        public void UnionWith(IEnumerable<T> other)
        {
            foreach (T item in other)
                Add(item);
        }

        public void IntersectWith(IEnumerable<T> other)
        {
            foreach (T item in other.Where(i => !items.Contains(i, GetComparer())))
                Remove(item);
        }

        public void ExceptWith(IEnumerable<T> other)
        {
            foreach (T item in other)
                Remove(item);
        }

        public void SymmetricExceptWith(IEnumerable<T> other)
        {
            foreach (T item in other)
                if (!Remove(item))
                    Add(item);
        }

        public bool IsSubsetOf(IEnumerable<T> other)
        {
            foreach (T item in items)
                if (!other.Contains(item, GetComparer()))
                    return false;

            return true;
        }

        public bool IsSupersetOf(IEnumerable<T> other)
        {
            foreach (T item in other)
                if (!Contains(item))
                    return false;

            return true;
        }

        public bool IsProperSupersetOf(IEnumerable<T> other) => Count > other.Count() && IsSupersetOf(other);
        public bool IsProperSubsetOf(IEnumerable<T> other) => Count < other.Count() && IsSubsetOf(other);

        public bool Overlaps(IEnumerable<T> other)
        {
            foreach (T item in other)
                if (Contains(item))
                    return true;

            return false;
        }

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

        private int BinarySearch(T item) => Comparer is IComparer<T> comparer ? items.BinarySearch(item, comparer) : items.BinarySearch(item);
        private IEqualityComparer<T> GetComparer() { EqualityComparer ??= new SetEqualityComparer(this); return EqualityComparer; }

        void ICollection<T>.Add(T item) => Add(item);
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public override IBurcatObject?[] GetBurcatConstructionValues()
        {
            if (Comparer is IEqualityComparer<T> comparer) return BurcatTranslator.FullObjectsTranslate([new BurcatType(typeof(T)), Identifier, items, (IBurcatObject)comparer]);
            else return BurcatTranslator.FullObjectsTranslate([new BurcatType(typeof(T)), Identifier, items]);
        }

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
