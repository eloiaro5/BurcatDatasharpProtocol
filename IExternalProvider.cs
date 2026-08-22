namespace BurcatProtocol
{
    /// <summary>
    /// Forwards Burcat object operations to an external source, usually another application over a stream.
    /// </summary>
    public interface IExternalProvider
    {
        /// <summary>
        /// Gets the Burcat identities this provider supports for a communication session.
        /// </summary>
        /// <param name="streamID">The optional permission or communication session.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns>The identities supported by the provider.</returns>
        Task<BurcatIdentitySet> GetIdentities(Guid? streamID, CancellationToken token);

        /// <summary>
        /// Gets the headers this provider can handle for a communication session.
        /// </summary>
        /// <param name="streamID">The optional permission or communication session.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns>The headers supported by the provider.</returns>
        Task<BurcatHeaderSet> GetHeaders(Guid? streamID, CancellationToken token);

        /// <summary>
        /// Gets the revision of an external object reference.
        /// </summary>
        /// <param name="streamID">The optional permission or communication session that requested the revision.</param>
        /// <param name="objectType">The CLR type of the referenced object.</param>
        /// <param name="objectID">The provider reference of the requested object.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns>The current revision, or <see cref="Guid.Empty"/> when no revision is available.</returns>
        Task<Guid> GetRevision(Guid? streamID, Type objectType, Guid objectID, CancellationToken token);

        /// <summary>
        /// Gets an external object reference.
        /// </summary>
        /// <param name="streamID">The optional permission or communication session that requested the object.</param>
        /// <param name="objectType">The CLR type of the referenced object.</param>
        /// <param name="objectID">The provider reference of the requested object.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns>The referenced object, or <see langword="null"/> when it is unavailable.</returns>
        Task<IBurcatObject?> GetObject(Guid? streamID, Type objectType, Guid objectID, CancellationToken token);

        /// <summary>
        /// Requests that an external provider add or update an object in cache or storage.
        /// </summary>
        /// <param name="streamID">The optional permission or communication session that requested the operation.</param>
        /// <param name="objectBDP">The object to add or update.</param>
        /// <param name="explicitelyRequested">Whether the caller explicitly requested the operation.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns><see langword="null"/> on success; otherwise, the protocol exception describing the failure.</returns>
        Task<BurcatException?> CoupleCache(Guid? streamID, IBurcatObject objectBDP, bool explicitelyRequested, CancellationToken token);

        /// <summary>
        /// Requests that an external provider delete an object from cache or storage.
        /// </summary>
        /// <param name="streamID">The optional permission or communication session that requested the operation.</param>
        /// <param name="objectBDP">The object to remove.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns><see langword="null"/> on success; otherwise, the protocol exception describing the failure.</returns>
        Task<BurcatException?> DecoupleCache(Guid? streamID, IBurcatObject objectBDP, CancellationToken token);

        /// <summary>
        /// Executes an action against an external object or type.
        /// </summary>
        /// <param name="streamID">The optional permission or communication session that requested the action.</param>
        /// <param name="objectType">The CLR type that declares or receives the action.</param>
        /// <param name="objectBDP">The target object, or <see langword="null"/> for static or type-level actions.</param>
        /// <param name="action">The protocol-visible action name.</param>
        /// <param name="parameters">The action parameters.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns>The action result.</returns>
        Task<ActionResult> ExecuteAction(Guid? streamID, Type objectType, IBurcatObject? objectBDP, string action, object?[]? parameters, CancellationToken token);
    }
}
