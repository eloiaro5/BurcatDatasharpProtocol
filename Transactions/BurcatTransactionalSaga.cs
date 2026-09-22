using System;
using System.Collections.Generic;
using System.Text;
using System.Transactions;

namespace BurcatProtocol.Transactions
{
    public sealed class BurcatTransactionalSaga(IdentifiedStream stream, BurcatHeaderSet additionalHeaders)
    {
        private readonly Queue<Func<BurcatDirectionalHead, ActionResult>> _commitSaga = [];
        private readonly Queue<Action> _rollbackSaga = [];

        public IdentifiedStream Stream { get; } = stream;
        public BurcatHeaderSet AdditionalHeaders { get; } = additionalHeaders;

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

        public async Task<BurcatException?> CommitAsync(CommitTransactionChart commitTransaction, RollbackTransactionChart rollbackTransaction, CancellationToken? token = null)
        {
            if (commitTransaction.TransactionID != rollbackTransaction.TransactionID) throw new InvalidOperationException("Cannot send a commit and rollback for different transactions.");
            else if (commitTransaction.TransactionID == Guid.Empty) throw new InvalidOperationException("Cannot initiate a transactional saga with an empty transaction.");
            else
            {
                BurcatDirectionalHead head = new(Stream, [BurcatTransaction.BuildHeader(commitTransaction.TransactionID), .. AdditionalHeaders]);
                BurcatException? exception = null;
                while (exception is null && _commitSaga.TryDequeue(out Func<BurcatDirectionalHead, ActionResult>? func))
                {
                    ActionResult result;
                    try { result = await Task.Run(() => func.Invoke(head)); }
                    catch (OperationCanceledException) { result = ActionResult.Thrown(new("A opeation has been cancelled and, thus, failed execution.")); }

                    if (!result.SuccessfulExecution) exception = result.Exception;
                }

                if (exception is null)
                {
                    ActionResult result = await BurcatChat.SendActionAsync(head, commitTransaction, nameof(BurcatChart.Acknowledge), token: token);
                    exception = result.Exception ?? ((CommitTransactionChart)result.Value!).CommitException;
                }
                else
                {
                    while (_rollbackSaga.TryDequeue(out Action? action))
                        try { await Task.Run(action); }
                        catch (OperationCanceledException) { }       

                    exception = (await BurcatChat.SendActionAsync(head, rollbackTransaction, nameof(BurcatChart.Acknowledge), token: token)).Exception ?? exception;
                }

                return exception;
            }
        }
    }
}
