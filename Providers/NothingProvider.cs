using System;
using System.Collections.Generic;
using System.Text;

namespace BurcatProtocol.Providers
{
    public sealed class NothingProvider : InternalProvider
    {
        public static NothingProvider Instance { get; } = new();

        public override Guid GetRevision(Guid? streamID, Type objectType, Guid objectID) => Guid.Empty;
        public override IBurcatObject? GetObject(Guid? streamID, Type objectType, Guid objectID) => null;

        public override BurcatException? CoupleCache(Guid? streamID, IBurcatObject objectBDP, bool explicitelyRequested) => new($"A {nameof(NothingProvider)} is not able to maintain objects.");
        public override BurcatException? DecoupleCache(Guid? streamID, IBurcatObject objectBDP) => new($"A {nameof(NothingProvider)} is not able to maintain objects.");
    }
}
