using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace BurcatProtocol.Collections
{
    [BurcatIdentity("00000000-0000-0000-0000-cbbe59f80bce")]
    public class SynchronizedSet<S, I> : BurcatObject, ISet<I>, IReadOnlySet<I> where S : ISet<I>, IBurcatObject
    {
        public static async Task<SynchronizedSet<BS, BI>> BuildAsync<BS, BI>(Guid identifier, bool ignoreInternal = false, CancellationToken? token = null) where BS : ISet<BI>, IBurcatObject
        {
            if (await BurcatChat.RelayRequestAsync<BS>(identifier, ignoreInternal, token) is BS synchronized) return new(synchronized);
            else throw new NullReferenceException();
        }
        public static SynchronizedSet<BS, BI> Build<BS, BI>(Guid identifier, bool ignoreInternal = false, CancellationToken? token = null) where BS : ISet<BI>, IBurcatObject => BuildAsync<BS, BI>(identifier, ignoreInternal, token).GetAwaiter().GetResult();

        private readonly S set;

        public int Count => set.Count;
        public bool IsReadOnly => set.IsReadOnly;

        public SynchronizedSet(S set) { this.set = set; }

        public bool Add(I item) => AddAsync(item).GetAwaiter().GetResult();
        public async Task<bool> AddAsync(I item, CancellationToken? token = null)
        {
            if ((await BurcatChat.RelayActionAsync(new(set), nameof(set.Add), BurcatTranslator.ObjectsTranslate([item]), token: token)).SuccessfulExecution) return set.Add(item);
            else throw new SynchronizationException();
        }

        public void Clear() => ClearAsync().GetAwaiter().GetResult();
        public async Task ClearAsync(CancellationToken? token = null)
        {
            if ((await BurcatChat.RelayActionAsync(new(set), nameof(set.Clear), token: token)).SuccessfulExecution) set.Clear();
            else throw new SynchronizationException();
        }

        public bool Remove(I item) => RemoveAsync(item).GetAwaiter().GetResult();
        public async Task<bool> RemoveAsync(I item, CancellationToken? token = null)
        {
            if ((await BurcatChat.RelayActionAsync(new(set), nameof(set.Remove), BurcatTranslator.ObjectsTranslate([item]), token: token)).SuccessfulExecution) return set.Remove(item);
            else throw new SynchronizationException();
        }

        public bool Contains(I item) => set.Contains(item);
        public void CopyTo(I[] array, int arrayIndex) => set.CopyTo(array, arrayIndex);
        public IEnumerator<I> GetEnumerator() => set.GetEnumerator();

        public void ExceptWith(IEnumerable<I> other) => set.ExceptWith(other);
        public void IntersectWith(IEnumerable<I> other) => set.IntersectWith(other);
        public bool IsProperSubsetOf(IEnumerable<I> other) => set.IsProperSubsetOf(other);
        public bool IsProperSupersetOf(IEnumerable<I> other) => set.IsProperSupersetOf(other);
        public bool IsSubsetOf(IEnumerable<I> other) => set.IsSubsetOf(other);
        public bool IsSupersetOf(IEnumerable<I> other) => set.IsSupersetOf(other);
        public bool Overlaps(IEnumerable<I> other) => set.Overlaps(other);
        public bool SetEquals(IEnumerable<I> other) => set.SetEquals(other);
        public void SymmetricExceptWith(IEnumerable<I> other) => set.SymmetricExceptWith(other);
        public void UnionWith(IEnumerable<I> other) => set.UnionWith(other);

        void ICollection<I>.Add(I item) => set.Add(item);
        IEnumerator IEnumerable.GetEnumerator() => set.GetEnumerator();

        public override object?[] GetBurcatConstructionValues() => [new BurcatType(typeof(S)), set];
    }
}
