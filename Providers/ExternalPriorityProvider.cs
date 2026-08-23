using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

namespace BurcatProtocol.Providers
{
    /// <summary>
    /// External provider that forwards headers through an ordered provider chain until an operation succeeds.
    /// </summary>
    public sealed class ExternalPriorityProvider : IList<IExternalProvider>, IExternalProvider
    {
        List<IExternalProvider> Providers { get; }

        /// <summary>
        /// Initializes an empty priority provider list.
        /// </summary>
        public ExternalPriorityProvider() { Providers = []; }

        /// <summary>
        /// Initializes a priority provider list.
        /// </summary>
        /// <param name="providers">The providers in priority order.</param>
        public ExternalPriorityProvider(IEnumerable<IExternalProvider> providers) { Providers = [.. providers]; }

        /// <summary>
        /// Initializes a priority provider list with an initial capacity.
        /// </summary>
        /// <param name="count">The initial provider capacity.</param>
        public ExternalPriorityProvider(int count) { Providers = new(count); }

        /// <inheritdoc/>
        public int Count => Providers.Count;       

        /// <inheritdoc/>
        public bool IsReadOnly => false;

        /// <inheritdoc/>
        public IExternalProvider this[int index] { get => Providers[index]; set => Providers[index] = value; }

        /// <inheritdoc/>
        public void Add(IExternalProvider item) => Providers.Add(item);

        /// <inheritdoc/>
        public void Clear() => Providers.Clear();

        /// <inheritdoc/>
        public bool Contains(IExternalProvider item) => Providers.Contains(item); 

        /// <inheritdoc/>
        public void CopyTo(IExternalProvider[] array, int arrayIndex) => Providers.CopyTo(array, arrayIndex);

        /// <inheritdoc/>
        public IEnumerator<IExternalProvider> GetEnumerator() => Providers.GetEnumerator();

        /// <inheritdoc/>
        public int IndexOf(IExternalProvider item) => Providers.IndexOf(item);

        /// <inheritdoc/>
        public void Insert(int index, IExternalProvider item)  => Providers.Insert(index, item);

        /// <inheritdoc/>
        public bool Remove(IExternalProvider item) => Providers.Remove(item);

        /// <inheritdoc/>
        public void RemoveAt(int index) => Providers.RemoveAt(index);

        /// <inheritdoc/>
        public async Task<BurcatIdentitySet> GetIdentities(CancellationToken token)
        {
            using IEnumerator<IExternalProvider> providers = Providers.GetEnumerator();
            token.ThrowIfCancellationRequested();

            if (providers.MoveNext())
            {
                BurcatIdentitySet result = new(await providers.Current.GetIdentities(token));
                while (result.Count != 0 && providers.MoveNext())
                {
                    token.ThrowIfCancellationRequested();
                    result.IntersectWith(await providers.Current.GetIdentities(token));
                }

                token.ThrowIfCancellationRequested();
                return result;
            }
            else return [];
        }

        /// <inheritdoc/>
        public async Task<BurcatHeaderSet> GetHeaders(CancellationToken token)
        {
            using IEnumerator<IExternalProvider> providers = Providers.GetEnumerator();
            token.ThrowIfCancellationRequested();

            if (providers.MoveNext())
            {
                BurcatHeaderSet result = [.. await providers.Current.GetHeaders(token)];
                while (result.Count != 0 && providers.MoveNext())
                {
                    token.ThrowIfCancellationRequested();
                    result.IntersectWith(await providers.Current.GetHeaders(token));
                }

                token.ThrowIfCancellationRequested();
                return result;
            }
            else return [];
        }

        /// <inheritdoc/>
        public async Task<Guid> GetRevision(BurcatBoradcastHead head, Type objectType, Guid objectID, CancellationToken token)
        {
            foreach (IExternalProvider provider in Providers)
            {
                token.ThrowIfCancellationRequested();

                Guid result = await provider.GetRevision(head, objectType, objectID, token);
                if (result != Guid.Empty) return result;

                token.ThrowIfCancellationRequested();
            }

            token.ThrowIfCancellationRequested();
            return Guid.Empty;
        }

        /// <inheritdoc/>
        public async Task<IBurcatObject?> GetObject(BurcatBoradcastHead head, Type objectType, Guid objectID, CancellationToken token)
        {
            foreach (IExternalProvider provider in Providers)
            {
                token.ThrowIfCancellationRequested();

                IBurcatObject? result = await provider.GetObject(head, objectType, objectID, token);
                if (result is not null) return result;

                token.ThrowIfCancellationRequested();
            }

            token.ThrowIfCancellationRequested();
            return null;
        }

        /// <inheritdoc/>
        public async Task<BurcatException?> CoupleCache(BurcatBoradcastHead head, IBurcatObject objectBDP, bool explicitelyRequested, CancellationToken token)
        {
            foreach(IExternalProvider provider in Providers)
            {
                token.ThrowIfCancellationRequested();
                if (await provider.CoupleCache(head, objectBDP, explicitelyRequested, token) is null)
                    return null;
            }

            token.ThrowIfCancellationRequested();
            return null;
        }

        /// <inheritdoc/>
        public async Task<BurcatException?> DecoupleCache(BurcatBoradcastHead head, IBurcatObject objectBDP, CancellationToken token)
        {
            foreach (IExternalProvider provider in Providers)
            {
                token.ThrowIfCancellationRequested();
                if (await provider.DecoupleCache(head, objectBDP, token) is null)
                    return null;
            }
            
            token.ThrowIfCancellationRequested();
            return null;
        }

        /// <inheritdoc/>
        public async Task<ActionResult> ExecuteAction(BurcatBoradcastHead head, Type objectType, IBurcatObject? objectBDP, string action, object?[]? parameters, CancellationToken token)
        {
            foreach (IExternalProvider provider in Providers)
            {
                token.ThrowIfCancellationRequested();

                ActionResult result = await provider.ExecuteAction(head, objectType, objectBDP, action, parameters, token);
                if (result.SuccessfulExecution) return result;

                token.ThrowIfCancellationRequested();
            }

            token.ThrowIfCancellationRequested();
            return ActionResult.Unsuccessful;
        }

        /// <inheritdoc/>
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
