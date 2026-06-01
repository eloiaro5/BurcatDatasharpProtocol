using BurcatProtocol.Annotations;
using System.Runtime.InteropServices;
using System.Text;

namespace BurcatProtocol
{
    [BurcatIdentity("00000000-0000-0000-0000-a3ab034572e5")]
    public class BurcatException : IBurcatObject
    {
        public static BurcatException FromException(Exception exception, bool includeStackTrace = false)
        {
            if (exception.InnerException is Exception inner) return includeStackTrace && exception.StackTrace is not null ? new(exception.Message, exception.StackTrace, innerException: FromException(inner)) : new(exception.Message, innerException: FromException(inner));
            else return includeStackTrace && exception.StackTrace is not null ? new(exception.Message, exception.StackTrace) : new(exception.Message);
        }
        public static Exception ToException(BurcatException exception)
        {
            StringBuilder message = new();

            message.AppendLine(exception.Message);
            if (exception.StackTrace is not null) message.AppendLine(exception.StackTrace);

            return exception.InnerException is null ? new(message.ToString()) : new(message.ToString(), ToException(exception.InnerException));
        }

        Guid IBurcatObject.Identifier { get; set => throw new InvalidOperationException(); } = Guid.Empty;
        Guid IBurcatObject.Revision { get; set => throw new InvalidOperationException(); } = Guid.Empty;

        public string Message { get; }
        public string? StackTrace { get; }
        public IBurcatObject? Payload { get; }
        public BurcatException? InnerException { get; }

        public BurcatException() : this("An exception has been thrown.") { }
        public BurcatException(string message, string? stackTrace = null, IBurcatObject? payload = null, BurcatException? innerException = null) { Message = message; StackTrace = stackTrace; Payload = payload; InnerException = innerException; }

        public void Throw() => throw ToException(this);

        public override string ToString()
        {
            StringBuilder sb = new();
            sb.AppendLine(Message);

            if (StackTrace is not null)
            {
                sb.AppendLine("Stack trace:");
                sb.AppendLine(StackTrace);
            }

            if (InnerException is not null)
            {
                sb.AppendLine("Inner exception:");
                sb.AppendLine(InnerException.ToString());
            }

            return sb.ToString();
        }

        BurcatField[] IBurcatObject.GetBurcatFields() => [];
        bool IBurcatObject.SetBurcatField(BurcatField field) => false;

        IBurcatObject?[] IBurcatObject.GetBurcatConstructionValues() => BurcatTranslator.ObjectsTranslate([Message, StackTrace, Payload, InnerException]);
    }

    [BurcatIdentity("00000000-0000-0000-0000-493ba25085b5")]
    public class UnsupportedBurcatObjectException : BurcatException
    {
        public UnsupportedBurcatObjectException() : base("The object sent is not supported by the current provider.") { }
        public UnsupportedBurcatObjectException(string message, string? stackTrace = null, IBurcatObject? payload = null, BurcatException? innerException = null) : base(message, stackTrace, payload, innerException) { }
    }

    [BurcatIdentity("00000000-0000-0000-0000-b0c0ae7e42d4")]
    public class BurcatObjectNotFoundException : BurcatException
    {
        public BurcatObjectNotFoundException() : base("The provided identifier does not correspond to any object BDP.") { }
        public BurcatObjectNotFoundException(string message, string? stackTrace = null, IBurcatObject? payload = null, BurcatException? innerException = null) : base(message, stackTrace, payload, innerException) { }
    }

    [BurcatIdentity("00000000-0000-0000-0000-a75e11a83692")]
    public class NotInBurcatCacheException : BurcatException
    {
        public NotInBurcatCacheException() : base("The requested type hasn't its fields / properties / constructors / methods in cache.") { }
        public NotInBurcatCacheException(string message, string? stackTrace = null, IBurcatObject? payload = null, BurcatException? innerException = null) : base(message, stackTrace, payload, innerException) { }
    }

    [BurcatIdentity("00000000-0000-0000-0000-acdfb2211da0")]
    public class BurcatValidationException : BurcatException
    {
        public BurcatValidationException(string message, string? stackTrace = null, IBurcatObject? payload = null, BurcatException? innerException = null) : base(message, stackTrace, payload, innerException) { }
    }
}
