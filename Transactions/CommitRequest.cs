using System;
using System.Collections.Generic;
using System.Text;

namespace BurcatProtocol.Transactions
{
    /// <summary>
    /// Represents a commit request.
    /// </summary>
    [BurcatIdentity("00000000-0000-0000-0000-6c6a355327fd")]
    public sealed class CommitRequest : IBurcatObject
    {
        /// <inheritdoc/>
        Guid IBurcatObject.Identifier { get; set => throw new InvalidOperationException(); } = Guid.Empty;
        /// <inheritdoc/>
        Guid IBurcatObject.Revision { get; set => throw new InvalidOperationException(); } = Guid.Empty;

        public CommitRequest(int transaction) { Transaction = transaction; }

        public int Transaction { get; }


        /// <inheritdoc/>
        BurcatField[] IBurcatObject.GetBurcatFields() => [];
        /// <inheritdoc/>
        void IBurcatObject.SetBurcatFields(BurcatField[] fields) { }

        /// <inheritdoc/>
        IBurcatObject?[] IBurcatObject.GetBurcatConstructionValues() => BurcatTranslator.ObjectsTranslate([Transaction]);
    }
}
