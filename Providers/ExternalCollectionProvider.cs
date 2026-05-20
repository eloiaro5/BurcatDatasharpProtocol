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

        public async Task<IBurcatObject?> GetObject(Guid? streamID, Type type, Guid objectID, CancellationToken token)
        {
            foreach (IExternalProvider provider in Providers)
            {
                token.ThrowIfCancellationRequested();

                IBurcatObject? result = await provider.GetObject(streamID, type, objectID, token);
                if (result is not null) return result;

                token.ThrowIfCancellationRequested();
            }

            token.ThrowIfCancellationRequested();
            return null;
        }

        public async Task<BurcatException?> CreateObject(Guid? streamID, IBurcatObject objectBDP, CancellationToken token)
        {
            foreach (IExternalProvider provider in Providers)
            {
                token.ThrowIfCancellationRequested();
                if (await provider.CreateObject(streamID, objectBDP, token) is BurcatException exception)
                    return exception;
            }

            token.ThrowIfCancellationRequested();
            return null;
        }
        public async Task<BurcatException?> UpdateObject(Guid? streamID, Type objectType, Guid? objectID, BurcatField field, CancellationToken token)
        {
            foreach (IExternalProvider provider in Providers)
            {
                token.ThrowIfCancellationRequested();
                if (await provider.UpdateObject(streamID, objectType, objectID, field, token) is BurcatException exception)
                    return exception;
            }

            token.ThrowIfCancellationRequested();
            return null;
        }
        public async Task<BurcatException?> DestroyObject(Guid? streamID, Type objectType, Guid objectID, CancellationToken token)
        {
            foreach (IExternalProvider provider in Providers)
            {
                token.ThrowIfCancellationRequested();
                if (await provider.DestroyObject(streamID, objectType, objectID, token) is BurcatException exception)
                    return exception;
            }

            token.ThrowIfCancellationRequested();
            return null;
        }

        public async Task<ActionResult> ExecuteAction(Guid? streamID, Type objectType, IBurcatObject? objectBDP, string action, IBurcatObject?[] parameters, CancellationToken token)
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

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
