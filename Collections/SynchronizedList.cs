using BurcatProtocol.Annotations;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace BurcatProtocol.Collections
{
    [BurcatIdentity("00000000-0000-0000-0000-cbbe59f80bce")]
    public class SynchronizedList<L, I> : BurcatObject, IList<I>, IReadOnlyList<I> where L : IList<I>, IBurcatObject
    {
        public static async Task<SynchronizedList<BL, BI>> BuildAsync<BL, BI>(Guid identifier, bool ignoreInternal = false, CancellationToken? token = null) where BL : IList<BI>, IBurcatObject
        {
            if (await BurcatChat.RelayRequestAsync<BL>(identifier, ignoreInternal, token) is BL synchronized) return new(synchronized);
            else throw new NullReferenceException();
        }
        public static SynchronizedList<BL, BI> Build<BL, BI>(Guid identifier, bool ignoreInternal = false, CancellationToken? token = null) where BL : IList<BI>, IBurcatObject => BuildAsync<BL, BI>(identifier, ignoreInternal, token).GetAwaiter().GetResult();

        private readonly L list;

        public int Count => list.Count;
        public bool IsReadOnly => list.IsReadOnly;

        public SynchronizedList(L list) { this.list = list; }

        [NotBurcatInvokable]
        public I this[int index] { get => list[index]; set => list[index] = value; }

        public void Add(I item) => AddAsync(item).GetAwaiter().GetResult();
        public async Task AddAsync(I item, CancellationToken? token = null)
        {
            if ((await BurcatChat.RelayActionAsync(new(list), nameof(list.Add), BurcatTranslator.FullObjectsTranslate([item]), token: token)).SuccessfulExecution) list.Add(item);
            else throw new SynchronizationException();
        }

        public void Clear() => ClearAsync().GetAwaiter().GetResult();
        public async Task ClearAsync(CancellationToken? token = null)
        {
            if ((await BurcatChat.RelayActionAsync(new(list), nameof(list.Clear), token: token)).SuccessfulExecution) list.Clear();
            else throw new SynchronizationException();
        }

        public void Insert(int index, I item) => InsertAsync(index, item).GetAwaiter().GetResult();
        public async Task InsertAsync(int index, I item, CancellationToken? token = null)
        {
            if ((await BurcatChat.RelayActionAsync(new(list), nameof(list.Insert), BurcatTranslator.FullObjectsTranslate([index, item]), token: token)).SuccessfulExecution) list.Insert(index, item);
            else throw new SynchronizationException();
        }

        public bool Remove(I item) => RemoveAsync(item).GetAwaiter().GetResult();
        public async Task<bool> RemoveAsync(I item, CancellationToken? token = null)
        {
            if ((await BurcatChat.RelayActionAsync(new(list), nameof(list.Remove), BurcatTranslator.FullObjectsTranslate([item]), token: token)).SuccessfulExecution) return list.Remove(item);
            else throw new SynchronizationException();
        }

        public void RemoveAt(int index) => RemoveAtAsync(index).GetAwaiter().GetResult();
        public async Task RemoveAtAsync(int index, CancellationToken? token = null)
        {
            if ((await BurcatChat.RelayActionAsync(new(list), nameof(list.RemoveAt), BurcatTranslator.FullObjectsTranslate([index]), token: token)).SuccessfulExecution) list.RemoveAt(index);
            else throw new SynchronizationException();
        }

        public bool Contains(I item) => list.Contains(item);
        public void CopyTo(I[] array, int arrayIndex) => list.CopyTo(array, arrayIndex);
        public IEnumerator<I> GetEnumerator() => list.GetEnumerator();
        public int IndexOf(I item) => list.IndexOf(item);

        IEnumerator IEnumerable.GetEnumerator() => list.GetEnumerator();

        public override IBurcatObject?[] GetBurcatConstructionValues() => [new BurcatType(typeof(L)), list];
    }
}
