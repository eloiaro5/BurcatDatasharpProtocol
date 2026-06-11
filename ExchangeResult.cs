namespace BurcatProtocol
{
    /// <summary>
    /// Identifies the kind of protocol exchange that was received.
    /// </summary>
    public enum BurcatExchangeType
    {
        /// <summary>
        /// An object was sent.
        /// </summary>
        Object,

        /// <summary>
        /// A revision was requested.
        /// </summary>
        RevisionRequest,

        /// <summary>
        /// An object was requested.
        /// </summary>
        ObjectRequest,

        /// <summary>
        /// An object cache add or update was requested.
        /// </summary>
        Couple,

        /// <summary>
        /// An object cache delete was requested.
        /// </summary>
        Decouple,

        /// <summary>
        /// An action was requested.
        /// </summary>
        Action
    }

    /// <summary>
    /// Describes the result of receiving and processing one Burcat protocol exchange.
    /// </summary>
    public sealed class ExchangeResult
    {
        /// <summary>
        /// Gets the exchange type.
        /// </summary>
        public BurcatExchangeType Type { get; }

        /// <summary>
        /// Gets the instance received from the stream.
        /// </summary>
        public BurcatInstance Recieved { get; }

        /// <summary>
        /// Gets the optional instance sent in response.
        /// </summary>
        public BurcatInstance? Sent { get; }

        /// <summary>
        /// Gets optional action argument metadata.
        /// </summary>
        public IBurcatObject?[]? ArgumentMetadata { get; }

        /// <summary>
        /// Gets optional generic type metadata.
        /// </summary>
        public Type[]? GenericMetadata { get; }

        /// <summary>
        /// Gets optional name metadata, such as an action name.
        /// </summary>
        public string? NameMetadata { get; }

        /// <summary>
        /// Initializes an exchange result.
        /// </summary>
        /// <param name="type">The exchange type.</param>
        /// <param name="recieved">The received instance.</param>
        /// <param name="sent">The optional sent instance.</param>
        public ExchangeResult(BurcatExchangeType type, BurcatInstance recieved, BurcatInstance? sent = null) { Type = type; Recieved = recieved; Sent = sent; }

        /// <summary>
        /// Initializes an exchange result with action metadata.
        /// </summary>
        /// <param name="type">The exchange type.</param>
        /// <param name="recieved">The received instance.</param>
        /// <param name="sent">The sent instance.</param>
        /// <param name="nameMetadata">The name metadata.</param>
        /// <param name="argumentMetadata">The action argument metadata.</param>
        public ExchangeResult(BurcatExchangeType type, BurcatInstance recieved, BurcatInstance sent, string nameMetadata, IBurcatObject?[] argumentMetadata) : this(type, recieved, sent) { NameMetadata = nameMetadata; ArgumentMetadata = argumentMetadata; }
    }
}
