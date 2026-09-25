using System;
using System.Collections.Generic;
using System.Text;
using System.Transactions;

namespace BurcatProtocol.Transactions
{
    public static class BurcatTransaction
    {
        public static BurcatHeader BuildHeader(Guid transactionID) => new(Guid.Empty, "transaction", transactionID.ToString());
        public static BurcatHeader Header { get; } = new(Guid.Empty, "transaction");
    }

    [BurcatIdentity("00000000-0000-0000-0000-6c6a355327fd")]
    public abstract class BeginTransactionChart : BurcatChart
    {
        protected BeginTransactionChart(Guid transactionID) => TransactionID = transactionID;
        public Guid TransactionID { get; }

        public override sealed object?[] GetBurcatConstructionValues() => [TransactionID];
    }

    [BurcatIdentity("00000000-0000-0000-0000-dbbaf6e795ad")]
    public abstract class CommitTransactionChart : BurcatChart
    {
        protected CommitTransactionChart(Guid transactionID) { TransactionID = transactionID; }
        protected CommitTransactionChart(Guid transactionID, CommitException? commitException) : this(transactionID) { CommitException = commitException; }
        
        public Guid TransactionID { get; }
        public CommitException? CommitException { get; }

        public override sealed object?[] GetBurcatConstructionValues() => [TransactionID, CommitException];
    }

    [BurcatIdentity("00000000-0000-0000-0000-8787a28b9509")]
    public abstract class RollbackTransactionChart : BurcatChart
    {
        protected RollbackTransactionChart(Guid transactionID) => TransactionID = transactionID;
        public Guid TransactionID { get; }

        public override sealed object?[] GetBurcatConstructionValues() => [TransactionID];
    }
}
