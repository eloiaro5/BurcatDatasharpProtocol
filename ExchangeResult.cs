namespace BurcatProtocol
{
    public enum BurcatExchangeType
    {
        Object,
        RevisionRequest,
        ObjectRequest,
        Couple,
        Decouple,
        Action
    }

    public sealed class ExchangeResult
    {
        public BurcatExchangeType Type { get; }

        public object Recieved { get; }
        public object? Sent { get; }

        public IBurcatObject?[]? ArgumentMetadata { get; }
        public Type[]? GenericMetadata { get; }
        public string? NameMetadata { get; }

        public ExchangeResult(BurcatExchangeType type, object recieved, object? sent = null) { Type = type; Recieved = recieved; Sent = sent; }
        public ExchangeResult(BurcatExchangeType type, object recieved, object sent, string nameMetadata, IBurcatObject?[] argumentMetadata) : this(type, recieved, sent) { NameMetadata = nameMetadata; ArgumentMetadata = argumentMetadata; }
    }
}
