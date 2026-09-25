using System;
using System.Collections.Generic;

namespace BurcatProtocol.Transactions
{
    /// <summary>
    /// Keeps the objects used by one transaction together with the revisions observed when they were first used.
    /// </summary>
    public sealed class TransactionalStash(IInternalProvider internalProvider, Guid transactionID)
    {
        private readonly Dictionary<Guid, Dictionary<Guid, StashedInstance>> classes = [];

        /// <summary>
        /// Gets the provider used to determine the current revision of stashed objects.
        /// </summary>
        public IInternalProvider InternalProvider { get; } = internalProvider;

        /// <summary>
        /// Gets the identifier of the transaction represented by this stash.
        /// </summary>
        public Guid TransactionID { get; } = transactionID;

        /// <summary>
        /// Gets the instance already stashed for the transaction and protocol object identity, or stashes and returns <paramref name="instance"/>.
        /// </summary>
        /// <param name="instance">The referenced instance to stash.</param>
        /// <returns>The instance first stashed for the transaction, class, and object identity.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="instance"/> is <see langword="null"/>.</exception>
        public BurcatInstance GetOrAdd(BurcatInstance instance)
        {
            if (instance.Value is not IBurcatObject value) return instance;
            else
            {
                Guid classID = BurcatChat.GetClassIdentity(instance.Type);
                if (!classes.TryGetValue(classID, out Dictionary<Guid, StashedInstance>? instances))
                {
                    instances = [];
                    classes.Add(classID, instances);
                }

                if (instances.TryGetValue(value.Identifier, out StashedInstance? stashed)) return new(stashed.Type, stashed.Object);
                else
                {
                    instances.Add(value.Identifier, new(instance.Type, value));
                    return instance;
                }
            }
        }

        /// <summary>
        /// Determines whether an object used by a transaction has changed since it was stashed.
        /// </summary>
        /// <returns><see langword="true"/> when a stashed object's revision has changed; otherwise, <see langword="false"/>.</returns>
        public bool IsStale()
        {
            foreach (Dictionary<Guid, StashedInstance> instances in classes.Values)
            {
                foreach (StashedInstance stashed in instances.Values)
                {
                    if (stashed.Object.Revision == Guid.Empty) return false;
                    else if (InternalProvider.GetRevision(new(Guid.Empty, []), stashed.Type, stashed.Object.Identifier) != stashed.Object.Revision) return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Removes an instance from the transaction stash when it is present.
        /// </summary>
        /// <param name="instance">The referenced instance to remove.</param>
        /// <exception cref="ArgumentNullException"><paramref name="instance"/> is <see langword="null"/>.</exception>
        public void Remove(BurcatInstance instance)
        {
            if (instance.Value is IBurcatObject value)
            {
                Guid classID = BurcatChat.GetClassIdentity(instance.Type);
                if (!classes.TryGetValue(classID, out Dictionary<Guid, StashedInstance>? instances)) return;

                instances.Remove(value.Identifier);
                if (instances.Count == 0) classes.Remove(classID);
            }
        }

        private sealed class StashedInstance(Type type, IBurcatObject obj)
        {
            public Type Type { get; } = type;
            public IBurcatObject Object { get; } = obj;
        }
    }
}
