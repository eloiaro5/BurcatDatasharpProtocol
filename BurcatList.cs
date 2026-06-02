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
    /// <summary>
    /// Represents a protocol-aware list whose mutations update the Burcat revision when referenced.
    /// </summary>
    /// <typeparam name="T">The item type.</typeparam>
    [BurcatIdentity("00000000-0000-0000-0000-cbb4dedec0ec")]
    public sealed class BurcatList<T> : BurcatObject, IList<T>, IReadOnlyList<T>
    {
        [NotBurcatInvokable]
        private T[] values;

        /// <inheritdoc/>
        public int Count { get; private set; }

        /// <inheritdoc/>
        public bool IsReadOnly => false;

        /// <summary>
        /// Initializes a referenced list from values.
        /// </summary>
        /// <param name="identifier">The list identifier.</param>
        /// <param name="values">The initial values.</param>
        public BurcatList(Guid identifier, IEnumerable<T> values) : base(identifier)
        {
            this.values = [.. values];
            Count = this.values.Length;
        }

        /// <summary>
        /// Initializes a referenced list with capacity.
        /// </summary>
        /// <param name="identifier">The list identifier.</param>
        /// <param name="capacity">The initial capacity.</param>
        public BurcatList(Guid identifier, int capacity) : base(identifier) { values = new T[capacity]; }

        /// <summary>
        /// Initializes an empty referenced list.
        /// </summary>
        /// <param name="identifier">The list identifier.</param>
        public BurcatList(Guid identifier) : this(identifier, []) { }

        /// <summary>
        /// Initializes an unreferenced list from values.
        /// </summary>
        /// <param name="values">The initial values.</param>
        public BurcatList(IEnumerable<T> values) : base(Guid.Empty)
        {
            this.values = [.. values];
            Count = this.values.Length;
        }

        /// <summary>
        /// Initializes an unreferenced list with capacity.
        /// </summary>
        /// <param name="capacity">The initial capacity.</param>
        public BurcatList(int capacity) : base(Guid.Empty) { values = new T[capacity]; }

        /// <summary>
        /// Initializes an empty unreferenced list.
        /// </summary>
        public BurcatList() : this([]) { }

        /// <inheritdoc/>
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
                else
                {
                    if (Identifier != Guid.Empty) Revision = GuidExtensions.GenerateRandom();
                    values[index] = value;
                }
            }
        }

        /// <inheritdoc/>
        public void Add(T item)
        {
            if (Identifier != Guid.Empty) Revision = GuidExtensions.GenerateRandom();

            EnsureCapacity(Count + 1);
            values[Count++] = item;
        }

        /// <inheritdoc/>
        public void Clear()
        {
            if (Count != 0 && Identifier != Guid.Empty) Revision = GuidExtensions.GenerateRandom();

            Array.Clear(values, 0, Count);
            Count = 0;
        }

        /// <inheritdoc/>
        public bool Contains(T item) => IndexOf(item) >= 0;

        /// <inheritdoc/>
        public void CopyTo(T[] array, int arrayIndex) => Array.Copy(values, 0, array, arrayIndex, Count);

        /// <summary>
        /// Searches a sorted range of the list for an item.
        /// </summary>
        /// <param name="index">The starting index.</param>
        /// <param name="count">The range length.</param>
        /// <param name="item">The item to search for.</param>
        /// <param name="comparer">The comparer to use.</param>
        /// <returns>The item index, or a negative insertion index.</returns>
        public int BinarySearch(int index, int count, T item, IComparer<T> comparer) => Array.BinarySearch(values, index, count, item, comparer);

        /// <summary>
        /// Searches the list for an item using a comparer.
        /// </summary>
        /// <param name="item">The item to search for.</param>
        /// <param name="comparer">The comparer to use.</param>
        /// <returns>The item index, or a negative insertion index.</returns>
        public int BinarySearch(T item, IComparer<T> comparer) => BinarySearch(0, values.Length, item, comparer);

        /// <summary>
        /// Searches the list for an item using the default comparer.
        /// </summary>
        /// <param name="item">The item to search for.</param>
        /// <returns>The item index, or a negative insertion index.</returns>
        public int BinarySearch(T item) => BinarySearch(item, Comparer<T>.Default);

        /// <inheritdoc/>
        public IEnumerator<T> GetEnumerator()
        {
            for (int i = 0; i < Count; i++)
                yield return values[i];
        }

        /// <inheritdoc/>
        public int IndexOf(T item) => Array.IndexOf(values, item, 0, Count);

        /// <inheritdoc/>
        public void Insert(int index, T item)
        {
            if (index < 0 || index > Count) throw new ArgumentOutOfRangeException(nameof(index));
            else
            {
                if (Identifier != Guid.Empty) Revision = GuidExtensions.GenerateRandom();
                EnsureCapacity(Count + 1);

                Array.Copy(values, index, values, index + 1, Count - index);
                values[index] = item;
                Count++;
            }
        }

        /// <inheritdoc/>
        public bool Remove(T item)
        {
            int index = IndexOf(item);
            if (index < 0) return false;
            else
            {
                if (Identifier != Guid.Empty) Revision = GuidExtensions.GenerateRandom();

                RemoveAt(index);
                return true;
            }
        }

        /// <inheritdoc/>
        public void RemoveAt(int index)
        {
            if (index < 0 || index >= Count) throw new ArgumentOutOfRangeException(nameof(index));
            else
            {
                if (Identifier != Guid.Empty) Revision = GuidExtensions.GenerateRandom();

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

        /// <inheritdoc/>
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        /// <inheritdoc/>
        public override BurcatField[] GetBurcatFields()
        {
            BurcatField[] fields = new BurcatField[Count];
            for (int i = 0; i < Count; i++) fields[i] = new($"{i}", BurcatTranslator.ObjectTranslate(values[i]));

            return fields;

        }

        /// <inheritdoc/>
        public override void SetBurcatFields(BurcatField[] fields)
        {
            foreach (BurcatField field in fields)
            {
                if (int.TryParse(field.Name, out int index))
                {
                    if (field.Value is null) values[index] = default!;
                    else if (GetType().GetGenericArguments()[0].IsAssignableFrom(field.Value.GetType())) values[index] = (T)field.Value;
                    else if (field.Value is BurcatTranslation translation && BurcatTranslator.TryTranslate<T>(translation, out T? value)) values[index] = value;
                    else throw new InvalidCastException();

                    if (Count <= index) Count = index + 1;
                }
            }
        }

        /// <inheritdoc/>
        public override object?[] GetBurcatConstructionValues() => [new BurcatType(typeof(T)), Identifier, Count];

        /// <summary>
        /// Converts an array to a Burcat list.
        /// </summary>
        /// <param name="array">The source array.</param>
        public static implicit operator BurcatList<T>(T[] array) => new([.. array]);

        /// <summary>
        /// Converts a Burcat list to an array.
        /// </summary>
        /// <param name="list">The source list.</param>
        public static implicit operator T[](BurcatList<T> list) { Array.Resize(ref list.values, list.Count); return list.values; }
    }
}
