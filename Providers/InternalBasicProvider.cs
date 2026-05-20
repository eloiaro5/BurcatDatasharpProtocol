using System;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;
using System.Text;

namespace BurcatProtocol.Providers
{
    public sealed class InternalBasicProvider : BurcatProtocol.InternalProvider
    {
        private SortedDictionary<Guid, SortedDictionary<Guid, IBurcatObject>> Objects { get; } = [];

        public static InternalBasicProvider Instance { get; } = new();

        public override IBurcatObject? GetObject(Guid? streamID, Type type, Guid objectID)
        {
            if (Objects.TryGetValue(BurcatChat.GetClassIdentity(type), out SortedDictionary<Guid, IBurcatObject>? inner))
                if (inner.TryGetValue(objectID, out IBurcatObject? reuslt)) return reuslt;
                else return null;
            else return null;
        }

        public override BurcatException? CreateObject(Guid? streamID, IBurcatObject objectBDP)
        {
            Guid classIdentifier = BurcatChat.GetClassIdentity(objectBDP.GetType());
            if (!Objects.TryGetValue(classIdentifier, out SortedDictionary<Guid, IBurcatObject>? inner))
            {
                inner = [];
                Objects.Add(classIdentifier, inner);
            }

            return inner.TryAdd(objectBDP.Identifier, objectBDP) ? null : new("The provided object already exists in the provider.");
        }
        public override BurcatException? DestroyObject(Guid? streamID, Type objectType, Guid objectID)
        {
            if (Objects.TryGetValue(BurcatChat.GetClassIdentity(objectType), out SortedDictionary<Guid, IBurcatObject>? inner)) return inner.Remove(objectID) ? null : new("The provided object already exists in the provider.");
            else return new("The provided object doesn't exist in the provider.");
        }
    }
}
