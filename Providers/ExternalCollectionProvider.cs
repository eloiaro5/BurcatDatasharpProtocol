using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

namespace BurcatProtocol.Providers
{
    /// <summary>
    /// External provider that broadcasts operations across a collection of external providers.
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
        public async Task<Guid> GetRevision(Guid? streamID, Type objectType, Guid objectID, CancellationToken token)
        {
            foreach (IExternalProvider provider in Providers)
            {
                token.ThrowIfCancellationRequested();

                Guid result = await provider.GetRevision(streamID, objectType, objectID, token);
                if (result != Guid.Empty) return result;

                token.ThrowIfCancellationRequested();
            }

            token.ThrowIfCancellationRequested();
            return Guid.Empty;
        }

        /// <inheritdoc/>
        public async Task<IBurcatObject?> GetObject(Guid? streamID, Type objectType, Guid objectID, CancellationToken token)
        {
            foreach (IExternalProvider provider in Providers)
            {
                token.ThrowIfCancellationRequested();

                IBurcatObject? result = await provider.GetObject(streamID, objectType, objectID, token);
                if (result is not null) return result;

                token.ThrowIfCancellationRequested();
            }

            token.ThrowIfCancellationRequested();
            return null;
        }

        /// <inheritdoc/>
        public async Task<BurcatException?> CoupleCache(Guid? streamID, IBurcatObject objectBDP, bool explicitelyRequested, CancellationToken token)
        {
            foreach (IExternalProvider provider in Providers)
            {
                token.ThrowIfCancellationRequested();
                if (await provider.CoupleCache(streamID, objectBDP, explicitelyRequested, token) is BurcatException exception)
                    return exception;
            }

            token.ThrowIfCancellationRequested();
            return null;
        }

        /// <inheritdoc/>
        public async Task<BurcatException?> DecoupleCache(Guid? streamID, IBurcatObject objectBDP, CancellationToken token)
        {
            foreach (IExternalProvider provider in Providers)
            {
                token.ThrowIfCancellationRequested();
                if (await provider.DecoupleCache(streamID, objectBDP, token) is BurcatException exception)
                    return exception;
            }

            token.ThrowIfCancellationRequested();
            return null;
        }

        /// <inheritdoc/>
        public async Task<ActionResult> ExecuteAction(Guid? streamID, Type objectType, IBurcatObject? objectBDP, string action, object?[]? parameters, CancellationToken token)
        {
            ActionResult result = ActionResult.Unsuccessful;
            foreach (IExternalProvider provider in Providers)
            {
                token.ThrowIfCancellationRequested();

                result = await provider.ExecuteAction(streamID, objectType, objectBDP, action, parameters, token);
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
