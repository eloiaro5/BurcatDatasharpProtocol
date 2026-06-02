using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

namespace BurcatProtocol.Providers
{
    /// <summary>
    /// External provider that queries ordered providers until a successful result is found.
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
            foreach(IExternalProvider provider in Providers)
            {
                token.ThrowIfCancellationRequested();
                if (await provider.CoupleCache(streamID, objectBDP, explicitelyRequested, token) is null)
                    return null;
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
                if (await provider.DecoupleCache(streamID, objectBDP, token) is null)
                    return null;
            }
            
            token.ThrowIfCancellationRequested();
            return null;
        }

        /// <inheritdoc/>
        public async Task<ActionResult> ExecuteAction(Guid? streamID, Type objectType, IBurcatObject? objectBDP, string action, object?[]? parameters, CancellationToken token)
        {
            foreach (IExternalProvider provider in Providers)
            {
                token.ThrowIfCancellationRequested();

                ActionResult result = await provider.ExecuteAction(streamID, objectType, objectBDP, action, parameters, token);
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
