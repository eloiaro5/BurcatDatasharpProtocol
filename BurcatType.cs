using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace BurcatProtocol
{
    /// <summary>
    /// Represents a CLR type as a Burcat class identity and nullability flag.
    /// </summary>
    [BurcatIdentity("00000000-0000-0000-0000-59769999f8f4")]
    public sealed class BurcatType : IBurcatObject
    {
        /// <inheritdoc/>
        Guid IBurcatObject.Identifier { get; set => throw new InvalidOperationException(); } = Guid.Empty;
        /// <inheritdoc/>
        Guid IBurcatObject.Revision { get; set => throw new InvalidOperationException(); } = Guid.Empty;

        /// <summary>
        /// Gets the Burcat class identity for the represented type.
        /// </summary>
        public Guid ClassID { get; }

        /// <summary>
        /// Gets whether the represented value may be null.
        /// </summary>
        public bool Nullable { get; }

        /// <summary>
        /// Initializes a Burcat type descriptor from a CLR type and explicit nullability.
        /// </summary>
        /// <param name="type">The CLR type to represent.</param>
        /// <param name="nullable">Whether the represented value may be null.</param>
        public BurcatType(Type type, bool nullable)
        {
            Nullable = nullable;

            if (System.Nullable.GetUnderlyingType(type) is Type underlying) ClassID = BurcatChat.GetClassIdentity(underlying);
            else ClassID = BurcatChat.GetClassIdentity(type);
        }

        /// <summary>
        /// Initializes a Burcat type descriptor from a CLR type.
        /// </summary>
        /// <param name="type">The CLR type to represent.</param>
        public BurcatType(Type type) : this(type, type.MightBeNull()) { }

        /// <summary>
        /// Initializes a Burcat type descriptor from a class identity.
        /// </summary>
        /// <param name="classID">The Burcat class identity.</param>
        public BurcatType(Guid classID) { ClassID = classID; }

        /// <summary>
        /// Initializes a Burcat type descriptor from a class identity and explicit nullability.
        /// </summary>
        /// <param name="classID">The Burcat class identity.</param>
        /// <param name="nullable">Whether the represented value may be null.</param>
        public BurcatType(Guid classID, bool nullable) : this(classID) { Nullable = nullable; }

        /// <summary>
        /// Resolves the represented Burcat class identity to a CLR type.
        /// </summary>
        /// <returns>The registered CLR type.</returns>
        public Type GetTypeCLR() => BurcatChat.GetType(ClassID);

        /// <inheritdoc/>
        BurcatField[] IBurcatObject.GetBurcatFields() => [];
        /// <inheritdoc/>
        void IBurcatObject.SetBurcatFields(BurcatField[] fields) { }

        /// <inheritdoc/>
        IBurcatObject?[] IBurcatObject.GetBurcatConstructionValues() => BurcatTranslator.ObjectsTranslate([ClassID, Nullable]);
    }
}
