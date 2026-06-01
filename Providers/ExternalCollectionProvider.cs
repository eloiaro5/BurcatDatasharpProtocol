using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

namespace BurcatProtocol.Providers
{
    public sealed class ExternalCollectionProvider : ICollection<IExternalProvider>, IExternalProvider
    {
        private LinkedList<IExternalProvider> Providers { get; } = new();

        public ExternalCollectionProvider() { Providers = []; }
        public ExternalCollectionProvider(IEnumerable<IExternalProvider> providers) { Providers = new(providers); }

        public int Count => Providers.Count;
        public bool IsReadOnly => false;

        public void Add(IExternalProvider item) => Providers.AddLast(item);
        public void Clear() => Providers.Clear();
        public bool Contains(IExternalProvider item) => Providers.Contains(item);
        public void CopyTo(IExternalProvider[] array, int arrayIndex) => Providers.CopyTo(array, arrayIndex);
        public bool Remove(IExternalProvider item) => Providers.Remove(item);
        public IEnumerator<IExternalProvider> GetEnumerator() => Providers.GetEnumerator();

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

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
