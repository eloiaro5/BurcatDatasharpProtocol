using BurcatProtocol.Annotations;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;

namespace BurcatProtocol.Collections
{
    [BurcatIdentity("00000000-0000-0000-0000-F10000000001")]
    public class ListSet<T> : BurcatObject, ISet<T>, IReadOnlySet<T>
    {
        private readonly BurcatList<T> items;
        private IEqualityComparer<T>? Comparer { get; }

        public int Count => items.Count;
        public bool IsReadOnly => false;

        public ListSet(Guid identifier, IEnumerable<T> values, IEqualityComparer<T> comparer) : base(identifier)
        {
            if (comparer is not IBurcatObject) throw new ArgumentException("The comparer must also be a BDP object", nameof(comparer));
            else
            {
                items = new(identifier, values);
                Comparer = comparer;
            }
        }
        public ListSet(Guid identifier, IEnumerable<T> values) : base(identifier) { items = new(identifier, values); }
        public ListSet(Guid identifier, IEqualityComparer<T> comparer) : this(identifier, [], comparer) { }
        public ListSet(Guid identifier) : this(identifier, []) { }

        public ListSet(IEnumerable<T> values, IEqualityComparer<T> comparer) : base(Guid.Empty)
        {
            if (comparer is not IBurcatObject) throw new ArgumentException("The comparer must also be a BDP object", nameof(comparer));
            else
            {
                items = [.. values];
                Comparer = comparer;
            }
        }
        public ListSet(IEnumerable<T> values) : base(Guid.Empty) { items = [.. values]; }
        public ListSet(IEqualityComparer<T> comparer) : this([], comparer) { }
        public ListSet() : this([]) { }

        public bool Add(T item)
        {
            if (Contains(item)) return false;
            else
            {
                items.Add(item);
                return true;
            }
        }

        public bool Remove(T item)
        {
            int index = items.IndexOf(item);
            if (index < 0) return false;
            else
            {
                items.RemoveAt(index);
                return true;
            }
        }

        public bool Contains(T item) => IndexOf(item) >= 0;

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
            foreach (T item in other.Where(i => !items.Contains(i)))
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
                if (!other.Contains(item, Comparer))
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

        void ICollection<T>.Add(T item) => Add(item);
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public override IBurcatObject?[] GetBurcatConstructionValues()
        {
            if (Comparer is IEqualityComparer<T> comparer) return BurcatTranslator.FullObjectsTranslate([new BurcatType(typeof(T)), Identifier, items, (IBurcatObject)comparer]);
            else return BurcatTranslator.FullObjectsTranslate([new BurcatType(typeof(T)), Identifier, items]);
        }
    }
}
