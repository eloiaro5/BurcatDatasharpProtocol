using BurcatProtocol.Annotations;
using System.Runtime.InteropServices;
using System.Text;

namespace BurcatProtocol
{
    /// <summary>
    /// Represents an exception that can be transferred through the Burcat protocol.
    /// </summary>
    [BurcatIdentity("00000000-0000-0000-0000-a3ab034572e5")]
    public class BurcatException : IBurcatObject
    {
        /// <summary>
        /// Converts a CLR exception into a Burcat exception.
        /// </summary>
        /// <param name="exception">The CLR exception to convert.</param>
        /// <param name="includeStackTrace">Whether to include stack trace text.</param>
        /// <returns>The converted Burcat exception.</returns>
        public static BurcatException FromException(Exception exception, bool includeStackTrace = false)
        {
            if (exception.InnerException is Exception inner) return includeStackTrace && exception.StackTrace is not null ? new(exception.Message, exception.StackTrace, innerException: FromException(inner)) : new(exception.Message, innerException: FromException(inner));
            else return includeStackTrace && exception.StackTrace is not null ? new(exception.Message, exception.StackTrace) : new(exception.Message);
        }

        /// <summary>
        /// Converts a Burcat exception into a CLR exception.
        /// </summary>
        /// <param name="exception">The Burcat exception to convert.</param>
        /// <returns>The converted CLR exception.</returns>
        public static Exception ToException(BurcatException exception)
        {
            StringBuilder message = new();

            message.AppendLine(exception.Message);
            if (exception.StackTrace is not null) message.AppendLine(exception.StackTrace);

            return exception.InnerException is null ? new(message.ToString()) : new(message.ToString(), ToException(exception.InnerException));
        }

        /// <inheritdoc/>
        Guid IBurcatObject.Identifier { get; set => throw new InvalidOperationException(); } = Guid.Empty;

        /// <inheritdoc/>
        Guid IBurcatObject.Revision { get; set => throw new InvalidOperationException(); } = Guid.Empty;

        /// <summary>
        /// Gets the exception message.
        /// </summary>
        public string Message { get; }

        /// <summary>
        /// Gets the optional stack trace text.
        /// </summary>
        public string? StackTrace { get; }

        /// <summary>
        /// Gets the optional protocol payload associated with the exception.
        /// </summary>
        public IBurcatObject? Payload { get; }

        /// <summary>
        /// Gets the optional inner protocol exception.
        /// </summary>
        public BurcatException? InnerException { get; }

        /// <summary>
        /// Initializes a generic Burcat exception.
        /// </summary>
        public BurcatException() : this("An exception has been thrown.") { }

        /// <summary>
        /// Initializes a Burcat exception.
        /// </summary>
        /// <param name="message">The exception message.</param>
        /// <param name="stackTrace">The optional stack trace text.</param>
        /// <param name="payload">The optional protocol payload.</param>
        /// <param name="innerException">The optional inner exception.</param>
        public BurcatException(string message, string? stackTrace = null, IBurcatObject? payload = null, BurcatException? innerException = null) { Message = message; StackTrace = stackTrace; Payload = payload; InnerException = innerException; }

        /// <summary>
        /// Throws this protocol exception as a CLR exception.
        /// </summary>
        public void Throw() => throw ToException(this);

        /// <summary>
        /// Gets a formatted protocol exception message.
        /// </summary>
        /// <returns>The formatted exception text.</returns>
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

        /// <inheritdoc/>
        BurcatField[] IBurcatObject.GetBurcatFields() => [];

        /// <inheritdoc/>
        void IBurcatObject.SetBurcatFields(BurcatField[] fields) { }

        /// <inheritdoc/>
        IBurcatObject?[] IBurcatObject.GetBurcatConstructionValues() => BurcatTranslator.ObjectsTranslate([Message, StackTrace, Payload, InnerException]);
    }

    /// <summary>
    /// Represents an error for an object type not supported by the current provider.
    /// </summary>
    [BurcatIdentity("00000000-0000-0000-0000-493ba25085b5")]
    public class UnsupportedBurcatObjectException : BurcatException
    {
        /// <summary>
        /// Initializes the default unsupported-object exception.
        /// </summary>
        public UnsupportedBurcatObjectException() : base("The object sent is not supported by the current provider.") { }

        /// <summary>
        /// Initializes an unsupported-object exception.
        /// </summary>
        /// <param name="message">The exception message.</param>
        /// <param name="stackTrace">The optional stack trace text.</param>
        /// <param name="payload">The optional protocol payload.</param>
        /// <param name="innerException">The optional inner exception.</param>
        public UnsupportedBurcatObjectException(string message, string? stackTrace = null, IBurcatObject? payload = null, BurcatException? innerException = null) : base(message, stackTrace, payload, innerException) { }
    }

    /// <summary>
    /// Represents an error for a referenced object that could not be found.
    /// </summary>
    [BurcatIdentity("00000000-0000-0000-0000-b0c0ae7e42d4")]
    public class BurcatObjectNotFoundException : BurcatException
    {
        /// <summary>
        /// Initializes the default object-not-found exception.
        /// </summary>
        public BurcatObjectNotFoundException() : base("The provided identifier does not correspond to any object BDP.") { }

        /// <summary>
        /// Initializes an object-not-found exception.
        /// </summary>
        /// <param name="message">The exception message.</param>
        /// <param name="stackTrace">The optional stack trace text.</param>
        /// <param name="payload">The optional protocol payload.</param>
        /// <param name="innerException">The optional inner exception.</param>
        public BurcatObjectNotFoundException(string message, string? stackTrace = null, IBurcatObject? payload = null, BurcatException? innerException = null) : base(message, stackTrace, payload, innerException) { }
    }

    /// <summary>
    /// Represents an error for a requested member that is not present in <see cref="BurcatCache"/>.
    /// </summary>
    [BurcatIdentity("00000000-0000-0000-0000-a75e11a83692")]
    public class NotInBurcatCacheException : BurcatException
    {
        /// <summary>
        /// Initializes the default cache-miss exception.
        /// </summary>
        public NotInBurcatCacheException() : base("The requested type hasn't its fields / properties / constructors / methods in cache.") { }

        /// <summary>
        /// Initializes a cache-miss exception.
        /// </summary>
        /// <param name="message">The exception message.</param>
        /// <param name="stackTrace">The optional stack trace text.</param>
        /// <param name="payload">The optional protocol payload.</param>
        /// <param name="innerException">The optional inner exception.</param>
        public NotInBurcatCacheException(string message, string? stackTrace = null, IBurcatObject? payload = null, BurcatException? innerException = null) : base(message, stackTrace, payload, innerException) { }
    }

    /// <summary>
    /// Represents an error produced by Burcat validation.
    /// </summary>
    [BurcatIdentity("00000000-0000-0000-0000-acdfb2211da0")]
    public class BurcatValidationException : BurcatException
    {
        /// <summary>
        /// Initializes a validation exception.
        /// </summary>
        /// <param name="message">The exception message.</param>
        /// <param name="stackTrace">The optional stack trace text.</param>
        /// <param name="payload">The optional protocol payload.</param>
        /// <param name="innerException">The optional inner exception.</param>
        public BurcatValidationException(string message, string? stackTrace = null, IBurcatObject? payload = null, BurcatException? innerException = null) : base(message, stackTrace, payload, innerException) { }
    }
}
