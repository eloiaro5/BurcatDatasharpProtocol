using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

namespace BurcatProtocol.Providers
{
    /// <summary>
    /// Internal provider that unions stream capabilities and fans header-aware operations out across its providers.
    /// </summary>
    public sealed class InternalCollectionProvider : InternalProvider, ICollection<IInternalProvider>, IInternalProvider
    {
        private LinkedList<IInternalProvider> Providers { get; } = new();

        /// <summary>
        /// Initializes an empty internal provider collection.
        /// </summary>
        public InternalCollectionProvider() { Providers = []; }

        /// <summary>
        /// Initializes an internal provider collection.
        /// </summary>
        /// <param name="providers">The providers to include.</param>
        public InternalCollectionProvider(IEnumerable<IInternalProvider> providers) { Providers = new(providers); }

        /// <inheritdoc/>
        public int Count => Providers.Count;

        /// <inheritdoc/>
        public bool IsReadOnly => false;

        /// <inheritdoc/>
        public void Add(IInternalProvider item) => Providers.AddLast(item);

        /// <inheritdoc/>
        public void Clear() => Providers.Clear();

        /// <inheritdoc/>
        public bool Contains(IInternalProvider item) => Providers.Contains(item);

        /// <inheritdoc/>
        public void CopyTo(IInternalProvider[] array, int arrayIndex) => Providers.CopyTo(array, arrayIndex);

        /// <inheritdoc/>
        public bool Remove(IInternalProvider item) => Providers.Remove(item);

        /// <inheritdoc/>
        public IEnumerator<IInternalProvider> GetEnumerator() => Providers.GetEnumerator();

        /// <inheritdoc/>
        public override BurcatIdentitySet GetIdentities(Guid streamID)
        {
            BurcatIdentitySet result = [];
            foreach (IInternalProvider provider in Providers)
                foreach (BurcatIdentity header in provider.GetIdentities(streamID))
                    result.Add(header);

            return result;
        }

        /// <inheritdoc/>
        public override BurcatHeaderSet GetHeaders(Guid streamID)
        {
            BurcatHeaderSet result = [];
            foreach (IInternalProvider provider in Providers)
                foreach (BurcatHeader header in provider.GetHeaders(streamID))
                    result.Add(header);

            return result;
        }

        /// <inheritdoc/>
        public override Guid GetRevision(BurcatHead head, Type objectType, Guid objectID)
        {
            foreach (IInternalProvider provider in Providers)
            {
                Guid result = provider.GetRevision(head, objectType, objectID);
                if (result != Guid.Empty) return result;
            }

            return Guid.Empty;
        }

        /// <inheritdoc/>
        public override IBurcatObject? GetObject(BurcatHead head, Type objectType, Guid objectID)
        {
            foreach (IInternalProvider provider in Providers)
            {
                IBurcatObject? result = provider.GetObject(head, objectType, objectID);
                if (result is not null) return result;
            }

            return null;
        }

        /// <inheritdoc/>
        public override BurcatException? CoupleCache(BurcatHead head, IBurcatObject objectBDP, bool explicitelyRequested)
        {
            foreach (IInternalProvider provider in Providers)
                if (provider.CoupleCache(head, objectBDP, explicitelyRequested) is BurcatException exception)
                    return exception;

            return null;
        }

        /// <inheritdoc/>
        public override BurcatException? DecoupleCache(BurcatHead head, IBurcatObject objectBDP)
        {
            foreach (IInternalProvider provider in Providers)
                if (provider.DecoupleCache(head, objectBDP) is BurcatException exception)
                    return exception;

            return null;
        }

        /// <inheritdoc/>
        public override ActionResult ExecuteAction(BurcatHead head, Type objectType, IBurcatObject? objectBDP, string action, object?[]? parameters)
        {
            ActionResult result = ActionResult.Unsuccessful;
            foreach (IInternalProvider provider in Providers)
            {
                result = provider.ExecuteAction(head, objectType, objectBDP, action, parameters);
                if (result.Exception is not null) return result;
            }

            return result;
        }

        /// <inheritdoc/>
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
