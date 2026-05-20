using BurcatProtocol.Annotations;
using BurcatProtocol.Collections;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;

namespace BurcatProtocol
{
    [BurcatIdentity("00000000-0000-0000-0000-cbb4dedec0ec")]
    public sealed class BurcatList<T> : BurcatObject, IList<T>, IReadOnlyList<T>
    {
        [NotBurcatInvokable]
        private T[] values;

        public int Count { get; private set; }
        public bool IsReadOnly => false;

        public BurcatList(Guid identifier, IEnumerable<T> values) : base(identifier)
        {
            this.values = [.. values];
            Count = this.values.Length;
        }
        public BurcatList(Guid identifier, int capacity) : base(identifier) { values = new T[capacity]; }
        public BurcatList(Guid identifier) : this(identifier, []) { }

        public BurcatList(IEnumerable<T> values) : base(Guid.Empty)
        {
            this.values = [.. values];
            Count = this.values.Length;
        }
        public BurcatList(int capacity) : base(Guid.Empty) { values = new T[capacity]; }
        public BurcatList() : this([]) { }

        [NotBurcatInvokable]
        public T this[int index]
        {
            get
            {
                if (index < 0 || index >= Count) throw new ArgumentOutOfRangeException(nameof(index));
                else return values[index];
            }
            set
            {
                if (index < 0 || index >= Count) throw new ArgumentOutOfRangeException(nameof(index));
                else values[index] = value;
            }
        }

        public void Add(T item)
        {
            EnsureCapacity(Count + 1);
            values[Count++] = item;
        }

        public void Clear()
        {
            Array.Clear(values, 0, Count);
            Count = 0;
        }

        public bool Contains(T item) => IndexOf(item) >= 0;

        public void CopyTo(T[] array, int arrayIndex) => Array.Copy(values, 0, array, arrayIndex, Count);

        public int BinarySearch(int index, int count, T item, IComparer<T> comparer) => Array.BinarySearch(values, index, count, item, comparer);
        public int BinarySearch(T item, IComparer<T> comparer) => BinarySearch(0, values.Length, item, comparer);
        public int BinarySearch(T item) => BinarySearch(item, Comparer<T>.Default);

        public IEnumerator<T> GetEnumerator()
        {
            for (int i = 0; i < Count; i++)
                yield return values[i];
        }

        public int IndexOf(T item) => Array.IndexOf(values, item, 0, Count);

        public void Insert(int index, T item)
        {
            if (index < 0 || index > Count) throw new ArgumentOutOfRangeException(nameof(index));
            else
            {
                EnsureCapacity(Count + 1);

                Array.Copy(values, index, values, index + 1, Count - index);
                values[index] = item;
                Count++;
            }
        }

        public bool Remove(T item)
        {
            int index = IndexOf(item);
            if (index < 0) return false;
            else
            {
                RemoveAt(index);
                return true;
            }
        }

        public void RemoveAt(int index)
        {
            if (index < 0 || index >= Count) throw new ArgumentOutOfRangeException(nameof(index));
            else
            {
                Count--;
                Array.Copy(values, index + 1, values, index, Count - index);
                values[Count] = default!;
            }
        }

        private void EnsureCapacity(int min)
        {
            if (values.Length < min)
            {
                int newCapacity = values.Length == 0 ? 4 : values.Length * 2;
                if (newCapacity < min) newCapacity = min;

                Array.Resize(ref values, newCapacity);
            }
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public override BurcatField[] GetBurcatFields()
        {
            BurcatField[] fields = new BurcatField[Count];
            for (int i = 0; i < Count; i++) fields[i] = new($"{i}", values[i] is object obj ? BurcatTranslator.Translate(obj) : null);

            return fields;

        }
        public override bool SetBurcatField(BurcatField field)
        {
            if (int.TryParse(field.Name, out int index))
            {
                if (field.Value is null) values[index] = default!;
                else if (field.Value is BurcatTranslation translation) values[index] = BurcatTranslator.Translate<T>(translation);
                else throw new InvalidCastException();

                if (Count <= index) Count = index + 1;
            }
            else return false;

            return true;
        }
        public override IBurcatObject?[] GetBurcatConstructionValues() => BurcatTranslator.FullObjectsTranslate([new BurcatType(typeof(T)), Identifier, Count]);

        public static implicit operator BurcatList<T>(T[] array) => new([.. array]);
        public static implicit operator T[](BurcatList<T> list) { Array.Resize(ref list.values, list.Count); return list.values; }
    }
}