using System;
using System.Collections.Generic;
using System.Text;

namespace BurcatProtocol.Providers
{
    public sealed class NothingProvider : BurcatProtocol.InternalProvider
    {
        public static NothingProvider Instance { get; } = new();

        public override IBurcatObject? GetObject(Guid? streamID, Type type, Guid identifier) => null;

        public override BurcatException? CreateObject(Guid? streamID, IBurcatObject objectBDP) => new($"A {nameof(NothingProvider)} is not able to maintain objects.");
        public override BurcatException? UpdateObject(Guid? streamID, Type objectType, Guid? objectID, BurcatField field) => new($"A {nameof(NothingProvider)} is not able to maintain objects.");
        public override BurcatException? DestroyObject(Guid? streamID, Type objectType, Guid objectID) => new($"A {nameof(NothingProvider)} is not able to maintain objects.");

        public override ActionResult ExecuteAction(Guid? streamID, Type objectType, IBurcatObject? objectBDP, string action, IBurcatObject?[] parameters) => ActionResult.Unsuccessful;
    }
}
