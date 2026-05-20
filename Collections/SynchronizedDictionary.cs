using BurcatProtocol.Annotations;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Text;

namespace BurcatProtocol.Collections
{
    [BurcatIdentity("00000000-0000-0000-0000-cbbe59f80bce")]
    public class SynchronizedDictionary<D, K, V> : BurcatObject, IDictionary<K, V>, IReadOnlyDictionary<K, V> where D : IDictionary<K, V>, IBurcatObject where K : notnull
    {
        public static async Task<SynchronizedDictionary<BD, BK, BV>> BuildAsync<BD, BK, BV>(Guid identifier, bool ignoreInternal = false, CancellationToken? token = null) where BD : IDictionary<BK, BV>, IBurcatObject where BK : notnull
        {
            if (await BurcatChat.RelayRequestAsync<BD>(identifier, ignoreInternal, token) is BD synchronized) return new(synchronized);
            else throw new NullReferenceException();
        }
        public static SynchronizedDictionary<BD, BK, BV> Build<BD, BK, BV>(Guid identifier, bool ignoreInternal = false, CancellationToken? token = null) where BD : IDictionary<BK, BV>, IBurcatObject where BK : notnull => BuildAsync<BD, BK, BV>(identifier, ignoreInternal, token).GetAwaiter().GetResult();

        private readonly D dictionary;

        public int Count => dictionary.Count;
        public bool IsReadOnly => dictionary.IsReadOnly;

        public ICollection<K> Keys => dictionary.Keys;
        public ICollection<V> Values => dictionary.Values;

        public SynchronizedDictionary(D dictionary) { this.dictionary = dictionary; }

        [NotBurcatInvokable]
        public V this[K key] { get => dictionary[key]; set => dictionary[key] = value; }

        public void Add(K key, V value) => AddAsync(key, value).GetAwaiter().GetResult();
        public async Task AddAsync(K key, V value, CancellationToken? token = null)
        {
            if ((await BurcatChat.RelayActionAsync(new(dictionary), nameof(dictionary.Add), BurcatTranslator.FullObjectsTranslate([key, value]), token: token)).SuccessfulExecution) dictionary.Add(key, value);
            else throw new SynchronizationException();
        }

        public void Add(KeyValuePair<K, V> item) => AddAsync(item).GetAwaiter().GetResult();
        public async Task AddAsync(KeyValuePair<K, V> item, CancellationToken? token = null)
        {
            if ((await BurcatChat.RelayActionAsync(new(dictionary), nameof(dictionary.Add), [(KeyValueDuo<K, V>)item], token: token)).SuccessfulExecution) dictionary.Add(item);
            else throw new SynchronizationException();
        }

        public void Clear() => ClearAsync().GetAwaiter().GetResult();
        public async Task ClearAsync(CancellationToken? token = null)
        {
            if ((await BurcatChat.RelayActionAsync(new(dictionary), nameof(dictionary.Clear), token: token)).SuccessfulExecution) dictionary.Clear();
            else throw new SynchronizationException();
        }

        public bool Remove(K key) => RemoveAsync(key).GetAwaiter().GetResult();
        public async Task<bool> RemoveAsync(K key, CancellationToken? token = null)
        {
            if ((await BurcatChat.RelayActionAsync(new(dictionary), nameof(dictionary.Remove), BurcatTranslator.FullObjectsTranslate([key]), token: token)).SuccessfulExecution) return dictionary.Remove(key);
            else throw new SynchronizationException();
        }

        public bool Remove(KeyValuePair<K, V> item) => RemoveAsync(item).GetAwaiter().GetResult();
        public async Task<bool> RemoveAsync(KeyValuePair<K, V> item, CancellationToken? token = null)
        {
            if ((await BurcatChat.RelayActionAsync(new(dictionary), nameof(dictionary.Remove), parameters: [(KeyValueDuo<K, V>)item], token: token)).SuccessfulExecution) return dictionary.Remove(item);
            else throw new SynchronizationException();
        }

        public bool ContainsKey(K key) => dictionary.ContainsKey(key);
        public bool TryGetValue(K key, [MaybeNullWhen(false)] out V value) => dictionary.TryGetValue(key, out value);
        public bool Contains(KeyValuePair<K, V> item) => dictionary.Contains(item);
        public void CopyTo(KeyValuePair<K, V>[] array, int arrayIndex) => dictionary.CopyTo(array, arrayIndex);
        public IEnumerator<KeyValuePair<K, V>> GetEnumerator() => dictionary.GetEnumerator();

        IEnumerable<K> IReadOnlyDictionary<K, V>.Keys => Keys;
        IEnumerable<V> IReadOnlyDictionary<K, V>.Values => Values;
        IEnumerator IEnumerable.GetEnumerator() => dictionary.GetEnumerator();

        public override IBurcatObject?[] GetBurcatConstructionValues() => [new BurcatType(typeof(D)), dictionary];
    }
}
