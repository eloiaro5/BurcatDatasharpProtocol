using System;
namespace BurcatProtocol.Providers
{
    public sealed class ExternalBasicProvider : IExternalProvider
    {
        public IdentifiedStream Stream { get; }

        public ExternalBasicProvider(IdentifiedStream stream) { Stream = stream; }

        public async Task<IBurcatObject?> GetObject(Guid? streamID, Type type, Guid identifier, CancellationToken token) => await BurcatChat.SendRequestAsync(Stream, BurcatChat.GetClassIdentity(type), identifier, token);
        public async Task<BurcatException?> CreateObject(Guid? streamID, IBurcatObject objectBDP, CancellationToken token) => await BurcatChat.SendConstructAsync(Stream, objectBDP, token);
        public Task<BurcatException?> UpdateObject(Guid? streamID, Type objectType, Guid? objectID, BurcatField field, CancellationToken token) => BurcatChat.SendUpdateAsync(Stream, BurcatChat.GetClassIdentity(objectType), objectID, field, token);
        public async Task<BurcatException?> DestroyObject(Guid? streamID, Type objectType, Guid objectID, CancellationToken token) => await BurcatChat.SendDestructAsync(Stream, BurcatChat.GetClassIdentity(objectType), objectID, token);
        public Task<ActionResult> ExecuteAction(Guid? streamID, Type objectType, IBurcatObject? objectBDP, string action, object?[]? parameters, CancellationToken token) => BurcatChat.SendActionAsync(Stream, new(objectType, objectBDP), action, parameters, token);
    }
}
