using System;
namespace BurcatProtocol.Providers
{
    public sealed class ExternalBasicProvider : IExternalProvider
    {
        public IdentifiedStream Stream { get; }

        public ExternalBasicProvider(IdentifiedStream stream) { Stream = stream; }

        public async Task<Guid> GetRevision(Guid? streamID, Type objectType, Guid objectID, CancellationToken token) => await BurcatChat.SendRevisionRequestAsync(Stream, BurcatChat.GetClassIdentity(objectType), objectID, token);
        public async Task<IBurcatObject?> GetObject(Guid? streamID, Type objectType, Guid objectID, CancellationToken token) => await BurcatChat.SendObjectRequestAsync(Stream, BurcatChat.GetClassIdentity(objectType), objectID, token);

        public async Task<BurcatException?> CoupleCache(Guid? streamID, IBurcatObject objectBDP, bool explicitelyRequested, CancellationToken token) => await BurcatChat.SendCoupleAsync(Stream, objectBDP, token);
        public async Task<BurcatException?> DecoupleCache(Guid? streamID, IBurcatObject objectBDP, CancellationToken token) => await BurcatChat.SendDecoupleAsync(Stream, objectBDP, token);

        public Task<ActionResult> ExecuteAction(Guid? streamID, Type objectType, IBurcatObject? objectBDP, string action, object?[]? parameters, CancellationToken token) => BurcatChat.SendActionAsync(Stream, new(objectType, objectBDP), action, parameters, token);
    }
}
