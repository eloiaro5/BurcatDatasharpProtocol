using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

namespace BurcatProtocol.Providers
{
    public sealed class InternalCollectionProvider : BurcatProtocol.InternalProvider, ICollection<IInternalProvider>, IInternalProvider
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

        public override IBurcatObject? GetObject(Guid? streamID, Type type, Guid objectID)
        {
            foreach (IInternalProvider provider in Providers)
            {
                IBurcatObject? result = provider.GetObject(streamID, type, objectID);
                if (result is not null) return result;
            }

            return null;
        }

        public override BurcatException? CreateObject(Guid? streamID, IBurcatObject objectBDP)
        {
            foreach (IInternalProvider provider in Providers)
                if (provider.CreateObject(streamID, objectBDP) is BurcatException exception)
                    return exception;

            return null;
        }
        public override BurcatException? UpdateObject(Guid? streamID, Type objectType, Guid? objectID, BurcatField field)
        {
            foreach (IInternalProvider provider in Providers)
                if (provider.UpdateObject(streamID, objectType, objectID, field) is BurcatException exception)
                    return exception;

            return null;
        }
        public override BurcatException? DestroyObject(Guid? streamID, Type objectType, Guid objectID)
        {
            foreach (IInternalProvider provider in Providers)
                if (provider.DestroyObject(streamID, objectType, objectID) is BurcatException exception)
                    return exception;

            return null;
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
