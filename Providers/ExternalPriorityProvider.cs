using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

namespace BurcatProtocol.Providers
{
    public sealed class ExternalPriorityProvider : IList<IExternalProvider>, IExternalProvider
    {
        List<IExternalProvider> Providers { get; }

        public ExternalPriorityProvider() { Providers = []; }
        public ExternalPriorityProvider(IEnumerable<IExternalProvider> providers) { Providers = [.. providers]; }
        public ExternalPriorityProvider(int count) { Providers = new(count); }

        public int Count => Providers.Count;       
        public bool IsReadOnly => false;

        public IExternalProvider this[int index] { get => Providers[index]; set => Providers[index] = value; }

        public void Add(IExternalProvider item) => Providers.Add(item);
        public void Clear() => Providers.Clear();
        public bool Contains(IExternalProvider item) => Providers.Contains(item); 
        public void CopyTo(IExternalProvider[] array, int arrayIndex) => Providers.CopyTo(array, arrayIndex);
        public IEnumerator<IExternalProvider> GetEnumerator() => Providers.GetEnumerator();
        public int IndexOf(IExternalProvider item) => Providers.IndexOf(item);
        public void Insert(int index, IExternalProvider item)  => Providers.Insert(index, item);
        public bool Remove(IExternalProvider item) => Providers.Remove(item);
        public void RemoveAt(int index) => Providers.RemoveAt(index);

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
            foreach(IExternalProvider provider in Providers)
            {
                token.ThrowIfCancellationRequested();
                if (await provider.CoupleCache(streamID, objectBDP, explicitelyRequested, token) is null)
                    return null;
            }

            token.ThrowIfCancellationRequested();
            return null;
        }
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

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
