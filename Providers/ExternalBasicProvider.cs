using System;
namespace BurcatProtocol.Providers
{
    /// <summary>
    /// External provider that forwards every operation and its additional headers through one identified stream.
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
        public Task<BurcatIdentitySet> GetIdentities(CancellationToken token) => BurcatChat.GetIdentitiesAsync(Stream, token);

        /// <inheritdoc/>
        public Task<BurcatHeaderSet> GetHeaders(CancellationToken token) => BurcatChat.GetHeadersAsync(Stream, token);

        /// <inheritdoc/>
        public Task<Guid> GetRevision(BurcatBoradcastHead head, Type objectType, Guid objectID, CancellationToken token) => BurcatChat.SendRevisionRequestAsync(new(Stream, head.AdditionalHeaders), BurcatChat.GetClassIdentity(objectType), objectID, token);

        /// <inheritdoc/>
        public Task<IBurcatObject?> GetObject(BurcatBoradcastHead head, Type objectType, Guid objectID, CancellationToken token) => BurcatChat.SendObjectRequestAsync(new(Stream, head.AdditionalHeaders), BurcatChat.GetClassIdentity(objectType), objectID, token);

        /// <inheritdoc/>
        public Task<BurcatException?> CoupleCache(BurcatBoradcastHead head, IBurcatObject objectBDP, bool explicitelyRequested, CancellationToken token) => BurcatChat.SendCoupleAsync(new(Stream, head.AdditionalHeaders), objectBDP, token);

        /// <inheritdoc/>
        public Task<BurcatException?> DecoupleCache(BurcatBoradcastHead head, IBurcatObject objectBDP, CancellationToken token) => BurcatChat.SendDecoupleAsync(new(Stream, head.AdditionalHeaders), objectBDP, token);

        /// <inheritdoc/>
        public Task<ActionResult> ExecuteAction(BurcatBoradcastHead head, Type objectType, IBurcatObject? objectBDP, string action, object?[]? parameters, CancellationToken token) => BurcatChat.SendActionAsync(new(Stream, head.AdditionalHeaders), new(objectType, objectBDP), action, parameters, token);
    }
}
