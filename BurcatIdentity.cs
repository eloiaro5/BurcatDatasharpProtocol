using System;
using System.Collections.Generic;
using System.Text;

namespace BurcatProtocol
{
    /// <summary>
    /// Associates a Burcat identity with its CLR type.
    /// </summary>
    [BurcatIdentity("00000000-0000-0000-0000-beba89f9d342")]
    public sealed class BurcatIdentity : IBurcatObject
    {
        /// <inheritdoc/>
        Guid IBurcatObject.Identifier { get; set => throw new InvalidOperationException(); } = Guid.Empty;
        /// <inheritdoc/>
        Guid IBurcatObject.Revision { get; set => throw new InvalidOperationException(); } = Guid.Empty;

        /// <summary>
        /// Gets the Burcat class identity.
        /// </summary>
        public Guid Guid { get; }

        /// <summary>
        /// Gets the CLR type associated with the identity.
        /// </summary>
        public Type Type { get; }

        /// <summary>
        /// Initializes an identity and type association.
        /// </summary>
        /// <param name="guid">The Burcat class identity.</param>
        /// <param name="type">The associated CLR type.</param>
        public BurcatIdentity(Guid guid, Type type) { Guid = guid; Type = type; }

        /// <summary>
        /// Initializes an identity from its serialized type descriptor.
        /// </summary>
        /// <param name="guid">The Burcat class identity.</param>
        /// <param name="type">The serialized CLR type descriptor.</param>
        public BurcatIdentity(Guid guid, BurcatType type) : this(guid, type.GetTypeCLR()) { }

        /// <inheritdoc/>
        public override bool Equals(object? obj)
        {
            if (ReferenceEquals(this, obj)) return true;
            else if (obj is BurcatIdentity other) return Guid.Equals(other.Guid);
            else return false;
        }

        /// <inheritdoc/>
        public override int GetHashCode() => Guid.GetHashCode();

        /// <inheritdoc/>
        BurcatField[] IBurcatObject.GetBurcatFields() => [];
        /// <inheritdoc/>
        void IBurcatObject.SetBurcatFields(BurcatField[] fields) { }

        /// <inheritdoc/>
        IBurcatObject?[] IBurcatObject.GetBurcatConstructionValues() => BurcatTranslator.ObjectsTranslate([Guid, new BurcatType(Type)]);
    }
}
