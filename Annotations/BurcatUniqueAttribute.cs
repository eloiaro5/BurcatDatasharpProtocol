using System;
using System.Collections.Generic;
using System.Text;

namespace BurcatProtocol.Annotations
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface, AllowMultiple = true)]
    public class BurcatUniqueAttribute : Attribute
    {
        public string Field { get; }
        public string[] CombinedWith { get; }

        public BurcatUniqueAttribute(string field, params string[] combinedWith) { Field = field; CombinedWith = combinedWith; }
        public BurcatUniqueAttribute(string field) : this(field, []) { }
    }

    [BurcatIdentity("00000000-0000-0000-0000-ebb4e3a3981c")]
    public class BurcatUniqueValidationException : BurcatValidationException
    {
        public BurcatUniqueValidationException() : base("There are values that should be unique, and currently they aren't.") { }
        public BurcatUniqueValidationException(string message, string? stackTrace = null, IBurcatObject? payload = null, BurcatException? innerException = null) : base(message, stackTrace, payload, innerException) { }
    }
}
