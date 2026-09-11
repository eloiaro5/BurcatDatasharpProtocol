using System;
using System.Collections.Generic;
using System.Text;

namespace BurcatProtocol.Transactions
{
    public sealed class BurcatTransactionalChat(IdentifiedStream stream, BurcatHeaderSet additionalHeaders, Guid transactionID)
    {
        private readonly Queue<Func<BurcatDirectionalHead, ActionResult>> _commitTransaction = [];
        private readonly Queue<Action<BurcatDirectionalHead>> _rollbackTransaction = [];

        public IdentifiedStream Stream { get; } = stream;
        public BurcatHeaderSet AdditionalHeaders { get; } = additionalHeaders;
        public Guid TransactionID { get; } = transactionID;

        public void QueueCouple(BurcatInstance instance, CancellationToken? token = null)
        {
            _commitTransaction.Enqueue(head => BurcatChat.SendCouple(head, instance, token) is BurcatException exception ? ActionResult.Thrown(exception) : new());
        }
        public void QueueCouple<T>(T objectBDP, CancellationToken? token = null) where T : IBurcatObject => QueueCouple(BurcatInstance.Build(objectBDP), token);

        public void QueueDecouple(BurcatInstance instance, CancellationToken? token = null)
        {
            _commitTransaction.Enqueue(head => BurcatChat.SendDecouple(head, instance, token) is BurcatException exception ? ActionResult.Thrown(exception) : new());
        }
        public void QueueDecouple<T>(T objectBDP, CancellationToken? token = null) where T : IBurcatObject => QueueDecouple(BurcatInstance.Build(objectBDP), token);

        public void QueueAction(BurcatInstance instance, string action, object?[]? parameters = null, CancellationToken? token = null)
        {
            _commitTransaction.Enqueue(head => BurcatChat.SendAction(head, instance, action, parameters, token));
        }
        public void QueueAction<T>(T objectBDP, string action, object?[]? parameters = null, CancellationToken? token = null) where T : IBurcatObject => QueueAction(BurcatInstance.Build(objectBDP), action, parameters, token);
        public void QueueAction<T>(string action, object?[]? parameters = null, CancellationToken? token = null) where T : IBurcatObject => QueueAction(BurcatInstance.Build<T>(), action, parameters, token);

        public void QueueRollbackAction(BurcatInstance instance, string action, object?[]? parameters = null, CancellationToken? token = null)
        {
            _rollbackTransaction.Enqueue(head => BurcatChat.SendAction(head, instance, action, parameters, token));
        }
        public void QueueRollbackAction<T>(T objectBDP, string action, object?[]? parameters = null, CancellationToken? token = null) where T : IBurcatObject => QueueAction(BurcatInstance.Build(objectBDP), action, parameters, token);
        public void QueueRollbackAction<T>(string action, object?[]? parameters = null, CancellationToken? token = null) where T : IBurcatObject => QueueAction(BurcatInstance.Build<T>(), action, parameters, token);

        public async Task<BurcatException?> CommitAsync(CancellationToken? token = null)
        {
            BurcatDirectionalHead head = new(Stream, [new BurcatHeader(Guid.Empty, "transaction", TransactionID.ToString()), .. AdditionalHeaders]);
            BurcatException? exception = null;
            while (exception is null && _commitTransaction.TryDequeue(out Func<BurcatDirectionalHead, ActionResult>? func))
            {
                token?.ThrowIfCancellationRequested();
                ActionResult result = func.Invoke(head);
                token?.ThrowIfCancellationRequested();

                token?.ThrowIfCancellationRequested();
                if (!result.SuccessfulExecution) exception = result.Exception;
                token?.ThrowIfCancellationRequested();
            }

            if (exception is not null)
                while (_rollbackTransaction.TryDequeue(out Action<BurcatDirectionalHead>? action))
                {
                    token?.ThrowIfCancellationRequested();
                    action.Invoke(head);
                    token?.ThrowIfCancellationRequested();
                }

            _commitTransaction.Clear();
            token?.ThrowIfCancellationRequested();

            _rollbackTransaction.Clear();
            token?.ThrowIfCancellationRequested();

            return exception;
        }
        public BurcatException? Commit(CancellationToken? token = null) => CommitAsync(token).GetAwaiter().GetResult();
    }
}
