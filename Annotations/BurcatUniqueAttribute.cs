using System;
using System.Collections.Generic;
using System.Text;

namespace BurcatProtocol.Annotations
{
    /// <summary>
    /// Marks a Burcat object type as requiring uniqueness for one or more fields.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface, AllowMultiple = true)]
    public class BurcatUniqueAttribute : Attribute
    {
        /// <summary>
        /// Gets the primary field that must be unique.
        /// </summary>
        public string Field { get; }

        /// <summary>
        /// Gets additional fields combined with <see cref="Field"/> for uniqueness.
        /// </summary>
        public string[] CombinedWith { get; }

        /// <summary>
        /// Initializes a uniqueness rule.
        /// </summary>
        /// <param name="field">The primary unique field.</param>
        /// <param name="combinedWith">Additional fields that form the uniqueness key.</param>
        public BurcatUniqueAttribute(string field, params string[] combinedWith) { Field = field; CombinedWith = combinedWith; }

        /// <summary>
        /// Initializes a uniqueness rule for one field.
        /// </summary>
        /// <param name="field">The unique field.</param>
        public BurcatUniqueAttribute(string field) : this(field, []) { }
    }

    /// <summary>
    /// Represents a validation error for violated Burcat uniqueness constraints.
    /// </summary>
    [BurcatIdentity("00000000-0000-0000-0000-ebb4e3a3981c")]
    public class BurcatUniqueValidationException : BurcatValidationException
    {
        /// <summary>
        /// Initializes the default uniqueness validation exception.
        /// </summary>
        public BurcatUniqueValidationException() : base("There are values that should be unique, and currently they aren't.") { }

        /// <summary>
        /// Initializes a uniqueness validation exception.
        /// </summary>
        /// <param name="message">The exception message.</param>
        /// <param name="stackTrace">The optional stack trace text.</param>
        /// <param name="payload">The optional protocol payload.</param>
        /// <param name="innerException">The optional inner exception.</param>
        public BurcatUniqueValidationException(string message, string? stackTrace = null, IBurcatObject? payload = null, BurcatException? innerException = null) : base(message, stackTrace, payload, innerException) { }
    }
}
