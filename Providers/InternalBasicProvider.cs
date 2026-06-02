using System;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;
using System.Text;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace BurcatProtocol.Providers
{
    /// <summary>
    /// In-memory internal provider backed by a dictionary of Burcat object references.
    /// </summary>
    public sealed class InternalBasicProvider : InternalProvider
    {
        private Dictionary<GuidList, IBurcatObject> Objects { get; } = [];

        /// <summary>
        /// Gets the shared in-memory provider instance.
        /// </summary>
        public static InternalBasicProvider Instance { get; } = new();

        /// <inheritdoc/>
        public override Guid GetRevision(Guid? streamID, Type objectType, Guid objectID)
        {
            GuidList guid = new([BurcatChat.GetClassIdentity(objectType), objectID]);

            if (Objects.TryGetValue(guid, out IBurcatObject? result)) return result.Revision;
            else return Guid.Empty;
        }

        /// <inheritdoc/>
        public override IBurcatObject? GetObject(Guid? streamID, Type objectType, Guid objectID)
        {
            GuidList guid = new([BurcatChat.GetClassIdentity(objectType), objectID]);

            if (Objects.TryGetValue(guid, out IBurcatObject? result)) return result;
            else return null;
        }

        /// <inheritdoc/>
        public override BurcatException? CoupleCache(Guid? streamID, IBurcatObject objectBDP, bool explicitelyRequested)
        {
            GuidList guid = new([BurcatChat.GetClassIdentity(objectBDP), objectBDP.Identifier]);
            return Objects.TryAdd(guid, objectBDP) ? null : new("The provided object already exists in the provider.");
        }

        /// <inheritdoc/>
        public override BurcatException? DecoupleCache(Guid? streamID, IBurcatObject objectBDP)
        {
            GuidList guid = new([BurcatChat.GetClassIdentity(objectBDP), objectBDP.Identifier]);
            Objects.Remove(guid);
            return null;
        }
    }
}
