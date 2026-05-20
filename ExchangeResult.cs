namespace BurcatProtocol
{
    public enum BurcatExchangeType
    {
        Object,
        Request,
        Construct,
        Update,
        Destruct,
        Action
    }

    public sealed class ExchangeResult
    {
        public BurcatExchangeType Type { get; }

        public BurcatInstance Recieved { get; }
        public BurcatInstance? Sent { get; }

        public IBurcatObject?[]? ArgumentMetadata { get; }
        public Type[]? GenericMetadata { get; }
        public string? NameMetadata { get; }

        public ExchangeResult(BurcatExchangeType type, BurcatInstance recieved, BurcatInstance? sent = null) { Type = type; Recieved = recieved; Sent = sent; }
        public ExchangeResult(BurcatExchangeType type, BurcatInstance recieved, BurcatInstance sent, string nameMetadata) : this(type, recieved, sent) { NameMetadata = nameMetadata; }
        public ExchangeResult(BurcatExchangeType type, BurcatInstance recieved, BurcatInstance sent, IBurcatObject?[] argumentMetadata, string nameMetadata) : this(type, recieved, sent, nameMetadata) { ArgumentMetadata = argumentMetadata; }
    }
}
