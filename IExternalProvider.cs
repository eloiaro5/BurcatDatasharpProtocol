namespace BurcatProtocol
{
    /// <summary>
    /// Forwards header-aware Burcat object operations to an external source.
    /// </summary>
    public interface IExternalProvider
    {
        /// <summary>
        /// Gets the Burcat identities supported by this provider.
        /// </summary>
        /// <param name="token">The cancellation token.</param>
        /// <returns>The identities supported by the provider.</returns>
        Task<BurcatIdentitySet> GetIdentities(CancellationToken token);

        /// <summary>
        /// Gets the headers supported by this provider.
        /// </summary>
        /// <param name="token">The cancellation token.</param>
        /// <returns>The headers supported by the provider.</returns>
        Task<BurcatHeaderSet> GetHeaders(CancellationToken token);

        /// <summary>
        /// Gets the revision of an external object reference.
        /// </summary>
        /// <param name="head">The stream identity and additional headers to forward with the request.</param>
        /// <param name="objectType">The CLR type of the referenced object.</param>
        /// <param name="objectID">The provider reference of the requested object.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns>The current revision, or <see cref="Guid.Empty"/> when no revision is available.</returns>
        Task<Guid> GetRevision(BurcatBoradcastHead head, Type objectType, Guid objectID, CancellationToken token);

        /// <summary>
        /// Gets an external object reference.
        /// </summary>
        /// <param name="head">The stream identity and additional headers to forward with the request.</param>
        /// <param name="objectType">The CLR type of the referenced object.</param>
        /// <param name="objectID">The provider reference of the requested object.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns>The referenced object, or <see langword="null"/> when it is unavailable.</returns>
        Task<IBurcatObject?> GetObject(BurcatBoradcastHead head, Type objectType, Guid objectID, CancellationToken token);

        /// <summary>
        /// Requests that an external provider add or update an object in cache or storage.
        /// </summary>
        /// <param name="head">The stream identity and additional headers to forward with the request.</param>
        /// <param name="objectBDP">The object to add or update.</param>
        /// <param name="explicitelyRequested">Whether the caller explicitly requested the operation.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns><see langword="null"/> on success; otherwise, the protocol exception describing the failure.</returns>
        Task<BurcatException?> CoupleCache(BurcatBoradcastHead head, IBurcatObject objectBDP, bool explicitelyRequested, CancellationToken token);

        /// <summary>
        /// Requests that an external provider delete an object from cache or storage.
        /// </summary>
        /// <param name="head">The stream identity and additional headers to forward with the request.</param>
        /// <param name="objectBDP">The object to remove.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns><see langword="null"/> on success; otherwise, the protocol exception describing the failure.</returns>
        Task<BurcatException?> DecoupleCache(BurcatBoradcastHead head, IBurcatObject objectBDP, CancellationToken token);

        /// <summary>
        /// Executes an action against an external object or type.
        /// </summary>
        /// <param name="head">The stream identity and additional headers to forward with the request.</param>
        /// <param name="objectType">The CLR type that declares or receives the action.</param>
        /// <param name="objectBDP">The target object, or <see langword="null"/> for static or type-level actions.</param>
        /// <param name="action">The protocol-visible action name.</param>
        /// <param name="parameters">The action parameters.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns>The action result.</returns>
        Task<ActionResult> ExecuteAction(BurcatBoradcastHead head, Type objectType, IBurcatObject? objectBDP, string action, object?[]? parameters, CancellationToken token);
    }
}
