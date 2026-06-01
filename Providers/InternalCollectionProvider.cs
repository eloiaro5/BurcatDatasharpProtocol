using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

namespace BurcatProtocol.Providers
{
    public sealed class InternalCollectionProvider : InternalProvider, ICollection<IInternalProvider>, IInternalProvider
    {
        private LinkedList<IInternalProvider> Providers { get; } = new();

        public InternalCollectionProvider() { Providers = []; }
        public InternalCollectionProvider(IEnumerable<IInternalProvider> providers) { Providers = new(providers); }

        public int Count => Providers.Count;
        public bool IsReadOnly => false;

        public void Add(IInternalProvider item) => Providers.AddLast(item);
        public void Clear() => Providers.Clear();
        public bool Contains(IInternalProvider item) => Providers.Contains(item);
        public void CopyTo(IInternalProvider[] array, int arrayIndex) => Providers.CopyTo(array, arrayIndex);
        public bool Remove(IInternalProvider item) => Providers.Remove(item);
        public IEnumerator<IInternalProvider> GetEnumerator() => Providers.GetEnumerator();

        public override Guid GetRevision(Guid? streamID, Type objectType, Guid objectID)
        {
            foreach (IInternalProvider provider in Providers)
            {
                Guid result = provider.GetRevision(streamID, objectType, objectID);
                if (result != Guid.Empty) return result;
            }

            return Guid.Empty;
        }

        public override IBurcatObject? GetObject(Guid? streamID, Type objectType, Guid objectID)
        {
            foreach (IInternalProvider provider in Providers)
            {
                IBurcatObject? result = provider.GetObject(streamID, objectType, objectID);
                if (result is not null) return result;
            }

            return null;
        }

        public override BurcatException? CoupleCache(Guid? streamID, IBurcatObject objectBDP, bool explicitelyRequested)
        {
            foreach (IInternalProvider provider in Providers)
                if (provider.CoupleCache(streamID, objectBDP, explicitelyRequested) is BurcatException exception)
                    return exception;

            return null;
        }
        public override BurcatException? DecoupleCache(Guid? streamID, IBurcatObject objectBDP)
        {
            foreach (IInternalProvider provider in Providers)
                if (provider.DecoupleCache(streamID, objectBDP) is BurcatException exception)
                    return exception;

            return null;
        }

        public override ActionResult ExecuteAction(Guid? streamID, Type objectType, IBurcatObject? objectBDP, string action, object?[]? parameters)
        {
            ActionResult result = ActionResult.Unsuccessful;
            foreach (IInternalProvider provider in Providers)
            {
                result = provider.ExecuteAction(streamID, objectType, objectBDP, action, parameters);
                if (result.Exception is not null) return result;
            }

            return result;
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
