namespace BurcatProtocol.Transactions
{
    /// <summary>
    /// Defines transaction lifecycle operations for a provider serving Burcat protocol streams.
    /// </summary>
    public interface ITransactionalProvider : IInternalProvider
    {
        /// <summary>Begins a transaction owned by the specified stream.</summary>
        /// <param name="streamID">The identifier of the stream that owns the transaction.</param>
        /// <returns>The provider-assigned transaction identifier.</returns>
        int BeginTransaction(Guid streamID);

        /// <summary>Sets the active transaction for the specified stream.</summary>
        /// <param name="streamID">The identifier of the stream whose active transaction is being set.</param>
        /// <param name="transactionID">The provider-assigned transaction identifier.</param>
        /// <returns><see langword="null" /> on success; otherwise, the protocol exception describing the failure.</returns>
        BurcatException? SetTransaction(Guid streamID, int transactionID);

        /// <summary>Rolls back a transaction owned by the specified stream.</summary>
        /// <param name="streamID">The identifier of the stream that owns the transaction.</param>
        /// <param name="transactionID">The provider-assigned transaction identifier.</param>
        /// <returns><see langword="null" /> on success; otherwise, the protocol exception describing the failure.</returns>
        BurcatException? Rollback(Guid streamID, int transactionID);

        /// <summary>Commits a transaction owned by the specified stream.</summary>
        /// <param name="streamID">The identifier of the stream that owns the transaction.</param>
        /// <param name="transactionID">The provider-assigned transaction identifier.</param>
        /// <returns><see langword="null" /> on success; otherwise, the protocol exception describing the failure.</returns>
        BurcatException? Commit(Guid streamID, int transactionID);
    }
}
