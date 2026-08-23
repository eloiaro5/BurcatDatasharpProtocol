using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

namespace BurcatProtocol.Providers
{
    /// <summary>
    /// External provider that forwards headers while broadcasting operations across its providers.
    /// </summary>
    public sealed class ExternalCollectionProvider : ICollection<IExternalProvider>, IExternalProvider
    {
        private LinkedList<IExternalProvider> Providers { get; } = new();

        /// <summary>
        /// Initializes an empty external provider collection.
        /// </summary>
        public ExternalCollectionProvider() { Providers = []; }

        /// <summary>
        /// Initializes an external provider collection.
        /// </summary>
        /// <param name="providers">The providers to include.</param>
        public ExternalCollectionProvider(IEnumerable<IExternalProvider> providers) { Providers = new(providers); }

        /// <inheritdoc/>
        public int Count => Providers.Count;

        /// <inheritdoc/>
        public bool IsReadOnly => false;

        /// <inheritdoc/>
        public void Add(IExternalProvider item) => Providers.AddLast(item);

        /// <inheritdoc/>
        public void Clear() => Providers.Clear();

        /// <inheritdoc/>
        public bool Contains(IExternalProvider item) => Providers.Contains(item);

        /// <inheritdoc/>
        public void CopyTo(IExternalProvider[] array, int arrayIndex) => Providers.CopyTo(array, arrayIndex);

        /// <inheritdoc/>
        public bool Remove(IExternalProvider item) => Providers.Remove(item);

        /// <inheritdoc/>
        public IEnumerator<IExternalProvider> GetEnumerator() => Providers.GetEnumerator();

        /// <inheritdoc/>
        public async Task<BurcatIdentitySet> GetIdentities(CancellationToken token)
        {
            using IEnumerator<IExternalProvider> providers = Providers.GetEnumerator();
            token.ThrowIfCancellationRequested();

            if (providers.MoveNext())
            {
                BurcatIdentitySet result = [.. await providers.Current.GetIdentities(token)];
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
            foreach (IExternalProvider provider in Providers)
            {
                token.ThrowIfCancellationRequested();
                if (await provider.CoupleCache(head, objectBDP, explicitelyRequested, token) is BurcatException exception)
                    return exception;
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
                if (await provider.DecoupleCache(head, objectBDP, token) is BurcatException exception)
                    return exception;
            }

            token.ThrowIfCancellationRequested();
            return null;
        }

        /// <inheritdoc/>
        public async Task<ActionResult> ExecuteAction(BurcatBoradcastHead head, Type objectType, IBurcatObject? objectBDP, string action, object?[]? parameters, CancellationToken token)
        {
            ActionResult result = ActionResult.Unsuccessful;
            foreach (IExternalProvider provider in Providers)
            {
                token.ThrowIfCancellationRequested();

                result = await provider.ExecuteAction(head, objectType, objectBDP, action, parameters, token);
                if (result.Exception is not null) return result;

                token.ThrowIfCancellationRequested();
            }

            token.ThrowIfCancellationRequested();
            return result;
        }

        /// <inheritdoc/>
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
