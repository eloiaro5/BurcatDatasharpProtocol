using System;
using System.Collections.Generic;
using System.Text;

namespace BurcatProtocol.Transactions
{
    public sealed class BurcatTransactionalSaga(IdentifiedStream stream, BurcatHeaderSet additionalHeaders, Guid transactionID)
    {
        private readonly Queue<Func<BurcatDirectionalHead, ActionResult>> _commitSaga = [];
        private readonly Queue<Action> _rollbackSaga = [];

        public IdentifiedStream Stream { get; } = stream;
        public BurcatHeaderSet AdditionalHeaders { get; } = additionalHeaders;
        public Guid TransactionID { get; } = transactionID;

        public void QueueCouple(BurcatInstance instance, CancellationToken? token = null)
        {
            _commitSaga.Enqueue(head => BurcatChat.SendCouple(head, instance, token) is BurcatException exception ? ActionResult.Thrown(exception) : new());
        }
        public void QueueCouple<T>(T objectBDP, CancellationToken? token = null) where T : IBurcatObject => QueueCouple(BurcatInstance.Build(objectBDP), token);

        public void QueueDecouple(BurcatInstance instance, CancellationToken? token = null)
        {
            _commitSaga.Enqueue(head => BurcatChat.SendDecouple(head, instance, token) is BurcatException exception ? ActionResult.Thrown(exception) : new());
        }
        public void QueueDecouple<T>(T objectBDP, CancellationToken? token = null) where T : IBurcatObject => QueueDecouple(BurcatInstance.Build(objectBDP), token);

        public void QueueAction(BurcatInstance instance, string action, object?[]? parameters = null, CancellationToken? token = null)
        {
            _commitSaga.Enqueue(head => BurcatChat.SendAction(head, instance, action, parameters, token));
        }
        public void QueueAction<T>(T objectBDP, string action, object?[]? parameters = null, CancellationToken? token = null) where T : IBurcatObject => QueueAction(BurcatInstance.Build(objectBDP), action, parameters, token);
        public void QueueAction<T>(string action, object?[]? parameters = null, CancellationToken? token = null) where T : IBurcatObject => QueueAction(BurcatInstance.Build<T>(), action, parameters, token);

        public void QueueRollback(Action action)
        {
            _rollbackSaga.Enqueue(action);
        }

        public BurcatException? Commit()
        {
            BurcatDirectionalHead head = new(Stream, [BurcatTransaction.BuildHeader(TransactionID), .. AdditionalHeaders]);
            BurcatException? exception = null;
            while (exception is null && _commitSaga.TryDequeue(out Func<BurcatDirectionalHead, ActionResult>? func))
            {
                ActionResult result = func.Invoke(head);
                if (!result.SuccessfulExecution) exception = result.Exception;
            }

            if (exception is not null)
            {
                while (_rollbackSaga.TryDequeue(out Action? action))
                    action.Invoke();
            }

            _commitSaga.Clear();
            _rollbackSaga.Clear();

            return exception;
        }
    }
}
