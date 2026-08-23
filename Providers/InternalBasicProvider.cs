using System;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;
using System.Text;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace BurcatProtocol.Providers
{
    /// <summary>
    /// In-memory internal provider that accepts header-aware operations and stores Burcat object references by type and identifier.
    /// </summary>
    public sealed class InternalBasicProvider : InternalProvider
    {
        private Dictionary<GuidList, IBurcatObject> Objects { get; } = [];

        /// <summary>
        /// Gets the shared in-memory provider instance.
        /// </summary>
        public static InternalBasicProvider Instance { get; } = new();

        /// <inheritdoc/>
        public override Guid GetRevision(BurcatHead head, Type objectType, Guid objectID)
        {
            GuidList guid = new([BurcatChat.GetClassIdentity(objectType), objectID]);

            if (Objects.TryGetValue(guid, out IBurcatObject? result)) return result.Revision;
            else return Guid.Empty;
        }

        /// <inheritdoc/>
        public override IBurcatObject? GetObject(BurcatHead head, Type objectType, Guid objectID)
        {
            GuidList guid = new([BurcatChat.GetClassIdentity(objectType), objectID]);

            if (Objects.TryGetValue(guid, out IBurcatObject? result)) return result;
            else return null;
        }

        /// <inheritdoc/>
        public override BurcatException? CoupleCache(BurcatHead head, IBurcatObject objectBDP, bool explicitelyRequested)
        {
            GuidList guid = new([BurcatChat.GetClassIdentity(objectBDP), objectBDP.Identifier]);
            return Objects.TryAdd(guid, objectBDP) ? null : new("The provided object already exists in the provider.");
        }

        /// <inheritdoc/>
        public override BurcatException? DecoupleCache(BurcatHead head, IBurcatObject objectBDP)
        {
            GuidList guid = new([BurcatChat.GetClassIdentity(objectBDP), objectBDP.Identifier]);
            Objects.Remove(guid);
            return null;
        }
    }
}
