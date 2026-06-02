using System;
using System.Collections.Generic;
using System.Text;

namespace BurcatProtocol
{
    /// <summary>
    /// Represents a raw translated value for a non-Burcat CLR type.
    /// </summary>
    [BurcatIdentity("00000000-0000-0000-0000-6b9a80e29a67 ")]
    public sealed class BurcatTranslation : IBurcatObject
    {
        /// <inheritdoc/>
        public Guid Identifier { get; set => throw new InvalidOperationException(); } = Guid.Empty;

        /// <inheritdoc/>
        Guid IBurcatObject.Revision { get; set => throw new InvalidOperationException(); } = Guid.Empty;

        /// <summary>
        /// Gets the protocol class identity of the translated CLR type.
        /// </summary>
        public Guid ClassID { get; }

        /// <summary>
        /// Gets the serialized translation bytes.
        /// </summary>
        public byte[] Data { get; }

        /// <summary>
        /// Initializes a raw translated value.
        /// </summary>
        /// <param name="classID">The protocol class identity of the translated CLR type.</param>
        /// <param name="translation">The serialized translation bytes.</param>
        public BurcatTranslation(Guid classID, byte[] translation)
        {
            ClassID = classID;
            Data = translation;
        }

        /// <inheritdoc/>
        BurcatField[] IBurcatObject.GetBurcatFields() => [];

        /// <inheritdoc/>
        void IBurcatObject.SetBurcatFields(BurcatField[] fields) { }

        /// <inheritdoc/>
        public IBurcatObject?[] GetBurcatConstructionValues() => [];
    }
}
