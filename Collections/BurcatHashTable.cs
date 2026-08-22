using BurcatProtocol.Annotations;
using System.Collections;

namespace BurcatProtocol.Collections
{
    /// <summary>Provides protocol-aware keyed hash storage using open addressing.</summary>
    /// <typeparam name="TK">The key type.</typeparam>
    /// <typeparam name="TV">The value type.</typeparam>
    [BurcatIdentity("00000000-0000-0000-0000-b887bf0fb440")]
    public sealed class BurcatHashTable<TK, TV> : BurcatObject, IReadOnlyCollection<KeyValueDuo<TK, TV>> where TK : notnull
    {
        private const int InitialCapacity = 4;
        private const int LoadFactorPercent = 72;
        private BurcatList<Slot> slots;
        private readonly IEqualityComparer<TK> comparer;
        private int deletedCount;

        /// <inheritdoc/>
        public int Count { get; private set; }

        /// <summary>Initializes referenced keyed hash storage from values and a comparer.</summary>
        public BurcatHashTable(Guid identifier, IEnumerable<KeyValueDuo<TK, TV>> values, IEqualityComparer<TK> comparer) : base(identifier)
        {
            if (comparer is not IBurcatObject) throw new ArgumentException("The comparer must also be a BDP object", nameof(comparer));

            this.comparer = comparer;
            slots = CreateSlots(InitialCapacity);
            foreach (KeyValueDuo<TK, TV> pair in values)
                if (!TryAddCore(pair.Key, pair.Value, false))
                    throw new ArgumentException("An item with the same key has already been added.", nameof(values));
        }

        /// <summary>Initializes referenced keyed hash storage from values.</summary>
        public BurcatHashTable(Guid identifier, IEnumerable<KeyValueDuo<TK, TV>> values) : this(identifier, values, BurcatEqualityComparer<TK>.Default) { }
        /// <summary>Initializes empty referenced keyed hash storage with a comparer.</summary>
        public BurcatHashTable(Guid identifier, IEqualityComparer<TK> comparer) : this(identifier, [], comparer) { }
        /// <summary>Initializes empty referenced keyed hash storage.</summary>
        public BurcatHashTable(Guid identifier) : this(identifier, []) { }
        /// <summary>Initializes unreferenced keyed hash storage from values and a comparer.</summary>
        public BurcatHashTable(IEnumerable<KeyValueDuo<TK, TV>> values, IEqualityComparer<TK> comparer) : this(Guid.Empty, values, comparer) { }
        /// <summary>Initializes unreferenced keyed hash storage from values.</summary>
        public BurcatHashTable(IEnumerable<KeyValueDuo<TK, TV>> values) : this(Guid.Empty, values) { }
        /// <summary>Initializes empty unreferenced keyed hash storage with a comparer.</summary>
        public BurcatHashTable(IEqualityComparer<TK> comparer) : this([], comparer) { }
        /// <summary>Initializes empty unreferenced keyed hash storage.</summary>
        public BurcatHashTable() : this([]) { }

        /// <summary>Adds a key and value when an equal key is not already stored.</summary>
        public bool TryAdd(TK key, TV value) => TryAddCore(key, value, true);

        private bool TryAddCore(TK key, TV value, bool revise)
        {
            EnsureCapacityForAddition();
            int hashCode = comparer.GetHashCode(key);
            if (TryFindSlot(key, hashCode, out _, out int insertionIndex)) return false;

            if (slots[insertionIndex].State == SlotState.Deleted) deletedCount--;
            slots[insertionIndex] = new(SlotState.Occupied, hashCode, key, value);
            Count++;
            if (revise) Revise();
            return true;
        }

        /// <summary>Adds or replaces the value associated with a key.</summary>
        /// <returns><see langword="true"/> when a new key was added; otherwise, <see langword="false"/>.</returns>
        public bool Set(TK key, TV value)
        {
            EnsureCapacityForAddition();
            int hashCode = comparer.GetHashCode(key);
            if (TryFindSlot(key, hashCode, out int index, out int insertionIndex))
            {
                slots[index] = new(SlotState.Occupied, hashCode, slots[index].Key!, value);
                Revise();
                return false;
            }

            if (slots[insertionIndex].State == SlotState.Deleted) deletedCount--;
            slots[insertionIndex] = new(SlotState.Occupied, hashCode, key, value);
            Count++;
            Revise();
            return true;
        }

        /// <summary>Removes a key and its value when present.</summary>
        public bool Remove(TK key)
        {
            if (!TryFindSlot(key, comparer.GetHashCode(key), out int index, out _)) return false;

            slots[index] = new(SlotState.Deleted, 0, default, default);
            Count--;
            deletedCount++;
            Revise();

            if (Count == 0) ResetSlots(InitialCapacity);
            else if (deletedCount > Count) Resize(slots.Count);

            return true;
        }

        /// <summary>Determines whether an equal key is stored.</summary>
        public bool ContainsKey(TK key) => TryFindSlot(key, comparer.GetHashCode(key), out _, out _);

        /// <summary>Gets the value associated with an equal key.</summary>
        public bool TryGetValue(TK key, out TV value)
        {
            if (TryFindSlot(key, comparer.GetHashCode(key), out int index, out _))
            {
                value = slots[index].Value!;
                return true;
            }

            value = default!;
            return false;
        }

        /// <summary>Removes every stored key and value.</summary>
        public void Clear()
        {
            if (Count == 0) return;

            ResetSlots(InitialCapacity);
            Revise();
        }

        /// <inheritdoc/>
        public IEnumerator<KeyValueDuo<TK, TV>> GetEnumerator()
        {
            foreach (Slot slot in slots)
                if (slot.State == SlotState.Occupied)
                    yield return new(slot.Key!, slot.Value!);
        }

        private void EnsureCapacityForAddition()
        {
            if ((Count + deletedCount + 1) * 100 > slots.Count * LoadFactorPercent) Resize(slots.Count * 2);
        }

        private bool TryFindSlot(TK key, int hashCode, out int itemIndex, out int insertionIndex)
        {
            int firstDeletedIndex = -1;
            int index = GetInitialIndex(hashCode, slots.Count);

            for (int probe = 0; probe < slots.Count; probe++)
            {
                Slot slot = slots[index];
                if (slot.State == SlotState.Empty)
                {
                    itemIndex = -1;
                    insertionIndex = firstDeletedIndex >= 0 ? firstDeletedIndex : index;
                    return false;
                }

                if (slot.State == SlotState.Deleted)
                {
                    if (firstDeletedIndex < 0) firstDeletedIndex = index;
                }
                else if (slot.HashCode == hashCode && comparer.Equals(slot.Key!, key))
                {
                    itemIndex = insertionIndex = index;
                    return true;
                }

                index = (index + 1) & (slots.Count - 1);
            }

            itemIndex = -1;
            insertionIndex = firstDeletedIndex;
            return false;
        }

        private void Resize(int capacity)
        {
            BurcatList<Slot> oldSlots = slots;
            slots = CreateSlots(capacity);
            deletedCount = 0;

            foreach (Slot slot in oldSlots)
                if (slot.State == SlotState.Occupied)
                    InsertDuringResize(slot.HashCode, slot.Key!, slot.Value!);
        }

        private void InsertDuringResize(int hashCode, TK key, TV value)
        {
            int index = GetInitialIndex(hashCode, slots.Count);
            while (slots[index].State == SlotState.Occupied) index = (index + 1) & (slots.Count - 1);
            slots[index] = new(SlotState.Occupied, hashCode, key, value);
        }

        private void ResetSlots(int capacity)
        {
            slots = CreateSlots(capacity);
            Count = 0;
            deletedCount = 0;
        }

        private void Revise()
        {
            if (Identifier != Guid.Empty) Revision = GuidExtensions.GenerateRandom();
        }

        private static int GetInitialIndex(int hashCode, int capacity) => (int)((uint)hashCode & (uint)(capacity - 1));

        private static BurcatList<Slot> CreateSlots(int capacity)
        {
            BurcatList<Slot> result = new(capacity);
            for (int i = 0; i < capacity; i++) result.Add(default);
            return result;
        }

        /// <inheritdoc/>
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        /// <inheritdoc/>
        public override object?[] GetBurcatConstructionValues()
        {
            BurcatList<KeyValueDuo<TK, TV>> values = [.. this];
            if (comparer is BurcatEqualityComparer<TK>) return [new BurcatType(typeof(TK), false), new BurcatType(typeof(TV)), Identifier, values];
            return [new BurcatType(typeof(TK), false), new BurcatType(typeof(TV)), Identifier, values, (IBurcatObject)comparer];
        }

        private enum SlotState : byte { Empty, Occupied, Deleted }

        private readonly struct Slot
        {
            public SlotState State { get; }
            public int HashCode { get; }
            public TK? Key { get; }
            public TV? Value { get; }

            public Slot(SlotState state, int hashCode, TK? key, TV? value)
            {
                State = state;
                HashCode = hashCode;
                Key = key;
                Value = value;
            }
        }
    }

    /// <summary>Provides protocol-aware hash storage using open addressing.</summary>
    /// <typeparam name="T">The stored item type.</typeparam>
    [BurcatIdentity("00000000-0000-0000-0000-4f6e3a97cdff")]
    public sealed class BurcatHashTable<T> : BurcatObject, IReadOnlyCollection<T> where T : notnull
    {
        private const int InitialCapacity = 4;
        private const int LoadFactorPercent = 72;
        private BurcatList<Slot> slots;
        private readonly IEqualityComparer<T> comparer;
        private int deletedCount;

        /// <inheritdoc/>
        public int Count { get; private set; }

        /// <summary>Initializes referenced hash storage from values and a comparer.</summary>
        public BurcatHashTable(Guid identifier, IEnumerable<T> values, IEqualityComparer<T> comparer) : base(identifier)
        {
            if (comparer is not IBurcatObject) throw new ArgumentException("The comparer must also be a BDP object", nameof(comparer));

            this.comparer = comparer;
            slots = CreateSlots(InitialCapacity);
            foreach (T item in values) AddCore(item, false);
        }

        /// <summary>Initializes referenced hash storage from values.</summary>
        public BurcatHashTable(Guid identifier, IEnumerable<T> values) : this(identifier, values, BurcatEqualityComparer<T>.Default) { }
        /// <summary>Initializes empty referenced hash storage with a comparer.</summary>
        public BurcatHashTable(Guid identifier, IEqualityComparer<T> comparer) : this(identifier, [], comparer) { }
        /// <summary>Initializes empty referenced hash storage.</summary>
        public BurcatHashTable(Guid identifier) : this(identifier, []) { }
        /// <summary>Initializes unreferenced hash storage from values and a comparer.</summary>
        public BurcatHashTable(IEnumerable<T> values, IEqualityComparer<T> comparer) : this(Guid.Empty, values, comparer) { }
        /// <summary>Initializes unreferenced hash storage from values.</summary>
        public BurcatHashTable(IEnumerable<T> values) : this(Guid.Empty, values) { }
        /// <summary>Initializes empty unreferenced hash storage with a comparer.</summary>
        public BurcatHashTable(IEqualityComparer<T> comparer) : this([], comparer) { }
        /// <summary>Initializes empty unreferenced hash storage.</summary>
        public BurcatHashTable() : this([]) { }

        /// <summary>Adds an item when an equal value is not already stored.</summary>
        public bool Add(T item) => AddCore(item, true);

        private bool AddCore(T item, bool revise)
        {
            EnsureCapacityForAddition();
            int hashCode = comparer.GetHashCode(item);
            if (TryFindSlot(item, hashCode, out _, out int insertionIndex)) return false;

            if (slots[insertionIndex].State == SlotState.Deleted) deletedCount--;
            slots[insertionIndex] = new(SlotState.Occupied, hashCode, item);
            Count++;
            if (revise) Revise();
            return true;
        }

        /// <summary>Removes an equal item when present.</summary>
        public bool Remove(T item)
        {
            if (!TryFindSlot(item, comparer.GetHashCode(item), out int index, out _)) return false;

            slots[index] = new(SlotState.Deleted, 0, default);
            Count--;
            deletedCount++;
            Revise();

            if (Count == 0) ResetSlots(InitialCapacity);
            else if (deletedCount > Count) Resize(slots.Count);

            return true;
        }

        /// <summary>Determines whether an equal item is stored.</summary>
        public bool Contains(T item) => TryFindSlot(item, comparer.GetHashCode(item), out _, out _);

        /// <summary>Gets the stored value that compares equal to the supplied value.</summary>
        public bool TryGetValue(T equalValue, out T actualValue)
        {
            if (TryFindSlot(equalValue, comparer.GetHashCode(equalValue), out int index, out _))
            {
                actualValue = slots[index].Value!;
                return true;
            }

            actualValue = default!;
            return false;
        }

        /// <summary>Removes every stored item.</summary>
        public void Clear()
        {
            if (Count == 0) return;

            ResetSlots(InitialCapacity);
            Revise();
        }

        /// <inheritdoc/>
        public IEnumerator<T> GetEnumerator()
        {
            foreach (Slot slot in slots)
                if (slot.State == SlotState.Occupied)
                    yield return slot.Value!;
        }

        private void EnsureCapacityForAddition()
        {
            if ((Count + deletedCount + 1) * 100 > slots.Count * LoadFactorPercent) Resize(slots.Count * 2);
        }

        private bool TryFindSlot(T item, int hashCode, out int itemIndex, out int insertionIndex)
        {
            int firstDeletedIndex = -1;
            int index = GetInitialIndex(hashCode, slots.Count);

            for (int probe = 0; probe < slots.Count; probe++)
            {
                Slot slot = slots[index];
                if (slot.State == SlotState.Empty)
                {
                    itemIndex = -1;
                    insertionIndex = firstDeletedIndex >= 0 ? firstDeletedIndex : index;
                    return false;
                }

                if (slot.State == SlotState.Deleted)
                {
                    if (firstDeletedIndex < 0) firstDeletedIndex = index;
                }
                else if (slot.HashCode == hashCode && comparer.Equals(slot.Value!, item))
                {
                    itemIndex = insertionIndex = index;
                    return true;
                }

                index = (index + 1) & (slots.Count - 1);
            }

            itemIndex = -1;
            insertionIndex = firstDeletedIndex;
            return false;
        }

        private void Resize(int capacity)
        {
            BurcatList<Slot> oldSlots = slots;
            slots = CreateSlots(capacity);
            deletedCount = 0;

            foreach (Slot slot in oldSlots)
                if (slot.State == SlotState.Occupied)
                    InsertDuringResize(slot.HashCode, slot.Value!);
        }

        private void InsertDuringResize(int hashCode, T item)
        {
            int index = GetInitialIndex(hashCode, slots.Count);
            while (slots[index].State == SlotState.Occupied) index = (index + 1) & (slots.Count - 1);
            slots[index] = new(SlotState.Occupied, hashCode, item);
        }

        private void ResetSlots(int capacity)
        {
            slots = CreateSlots(capacity);
            Count = 0;
            deletedCount = 0;
        }

        private void Revise()
        {
            if (Identifier != Guid.Empty) Revision = GuidExtensions.GenerateRandom();
        }

        private static int GetInitialIndex(int hashCode, int capacity) => (int)((uint)hashCode & (uint)(capacity - 1));

        private static BurcatList<Slot> CreateSlots(int capacity)
        {
            BurcatList<Slot> result = new(capacity);
            for (int i = 0; i < capacity; i++) result.Add(default);
            return result;
        }

        /// <inheritdoc/>
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        /// <inheritdoc/>
        public override object?[] GetBurcatConstructionValues()
        {
            BurcatList<T> values = [.. this];
            if (comparer is BurcatEqualityComparer<T>) return [new BurcatType(typeof(T)), Identifier, values];
            return [new BurcatType(typeof(T)), Identifier, values, (IBurcatObject)comparer];
        }

        private enum SlotState : byte { Empty, Occupied, Deleted }

        private readonly struct Slot
        {
            public SlotState State { get; }
            public int HashCode { get; }
            public T? Value { get; }

            public Slot(SlotState state, int hashCode, T? value)
            {
                State = state;
                HashCode = hashCode;
                Value = value;
            }
        }
    }
}
