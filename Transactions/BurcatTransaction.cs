using System;
using System.Collections.Generic;
using System.Text;
using System.Transactions;

namespace BurcatProtocol.Transactions
{
    [BurcatIdentity("00000000-0000-0000-0000-6c6a355327fd")]
    public abstract class BurcatTransaction : BurcatObject
    {
        public abstract Guid BeginTransaction(bool onlyAtomicActions = true);
        public abstract BurcatCommitException? Commit(Guid transactionID);
        public abstract void Rollback(Guid transactionID);
    }
}
