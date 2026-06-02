using System;
using System.Collections.Generic;
using System.Text;

namespace BurcatProtocol
{
    /// <summary>
    /// Assigns a stable Burcat protocol identity to a type.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface | AttributeTargets.Enum, Inherited = false)]
    public sealed class BurcatIdentityAttribute : Attribute
    {
        /// <summary>
        /// Gets the protocol identity for the attributed type.
        /// </summary>
        public Guid Identity { get; }

        /// <summary>
        /// Initializes the attribute with a protocol identity.
        /// </summary>
        /// <param name="identity">The stable protocol identity.</param>
        public BurcatIdentityAttribute(Guid identity) { Identity = identity; }

        /// <summary>
        /// Initializes the attribute with a protocol identity string.
        /// </summary>
        /// <param name="guid">The stable protocol identity string.</param>
        public BurcatIdentityAttribute(string guid) : this(new Guid(guid)) { }  
    }
}
