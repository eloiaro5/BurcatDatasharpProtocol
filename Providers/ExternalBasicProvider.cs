using System;
namespace BurcatProtocol.Providers
{
    /// <summary>
    /// External provider that forwards every operation through one identified stream.
    /// </summary>
    public sealed class ExternalBasicProvider : IExternalProvider
    {
        /// <summary>
        /// Gets the stream used for forwarded operations.
        /// </summary>
        public IdentifiedStream Stream { get; }

        /// <summary>
        /// Initializes an external provider over a stream.
        /// </summary>
        /// <param name="stream">The stream used for forwarded operations.</param>
        public ExternalBasicProvider(IdentifiedStream stream) { Stream = stream; }

        /// <inheritdoc/>
        public async Task<Guid> GetRevision(Guid? streamID, Type objectType, Guid objectID, CancellationToken token) => await BurcatChat.SendRevisionRequestAsync(Stream, BurcatChat.GetClassIdentity(objectType), objectID, token);

        /// <inheritdoc/>
        public async Task<IBurcatObject?> GetObject(Guid? streamID, Type objectType, Guid objectID, CancellationToken token) => await BurcatChat.SendObjectRequestAsync(Stream, BurcatChat.GetClassIdentity(objectType), objectID, token);

        /// <inheritdoc/>
        public async Task<BurcatException?> CoupleCache(Guid? streamID, IBurcatObject objectBDP, bool explicitelyRequested, CancellationToken token) => await BurcatChat.SendCoupleAsync(Stream, objectBDP, token);

        /// <inheritdoc/>
        public async Task<BurcatException?> DecoupleCache(Guid? streamID, IBurcatObject objectBDP, CancellationToken token) => await BurcatChat.SendDecoupleAsync(Stream, objectBDP, token);

        /// <inheritdoc/>
        public Task<ActionResult> ExecuteAction(Guid? streamID, Type objectType, IBurcatObject? objectBDP, string action, object?[]? parameters, CancellationToken token) => BurcatChat.SendActionAsync(Stream, new(objectType, objectBDP), action, parameters, token);
    }
}
