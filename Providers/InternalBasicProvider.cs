using System;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;
using System.Text;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace BurcatProtocol.Providers
{
    public sealed class InternalBasicProvider : InternalProvider
    {
        private Dictionary<GuidList, IBurcatObject> Objects { get; } = [];

        public static InternalBasicProvider Instance { get; } = new();

        public override Guid GetRevision(Guid? streamID, Type objectType, Guid objectID)
        {
            GuidList guid = new([BurcatChat.GetClassIdentity(objectType), objectID]);

            if (Objects.TryGetValue(guid, out IBurcatObject? result)) return result.Revision;
            else return Guid.Empty;
        }
        public override IBurcatObject? GetObject(Guid? streamID, Type objectType, Guid objectID)
        {
            GuidList guid = new([BurcatChat.GetClassIdentity(objectType), objectID]);

            if (Objects.TryGetValue(guid, out IBurcatObject? result)) return result;
            else return null;
        }

        public override BurcatException? CoupleCache(Guid? streamID, IBurcatObject objectBDP, bool explicitelyRequested)
        {
            GuidList guid = new([BurcatChat.GetClassIdentity(objectBDP), objectBDP.Identifier]);
            return Objects.TryAdd(guid, objectBDP) ? null : new("The provided object already exists in the provider.");
        }
        public override BurcatException? DecoupleCache(Guid? streamID, IBurcatObject objectBDP)
        {
            GuidList guid = new([BurcatChat.GetClassIdentity(objectBDP), objectBDP.Identifier]);
            Objects.Remove(guid);
            return null;
        }
    }
}
