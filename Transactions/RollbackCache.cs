using BurcatProtocol.Cache;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;

namespace BurcatProtocol.Transactions
{
    public static class RollbackCache
    {
        private static ConcurrentDictionary<GuidList, ConcurrentStack<RollbackAction>> RollbackActions { get; } = [];
        private static AsyncLocal<bool> IsRollingBack { get; } = new();

        private static bool IsLogicInCache { get; set; }
        private static object CacheLock { get; } = new();

        /// <summary>
        /// Gets the ambient key under which successful atomic actions are registered.
        /// </summary>
        public static AsyncLocal<Guid?> CurrentStream { get; } = new();
        public static AsyncLocal<Guid?> CurrentTransaction { get; } = new();

        public static void AddLogicIntoCache()
        {
            if (!IsLogicInCache)
                lock (CacheLock)
                    if (!IsLogicInCache)
                    {
                        BurcatCache.AfterExecutingAction += BurcatCache_AfterExecutingAction;

                        IsLogicInCache = true;
                    }
        }

        private static void BurcatCache_AfterExecutingAction(object? sender, AfterExecutingActionEventArgs e)
        {
            if (IsRollingBack.Value)
            {
                if (e.CalledAction is null)
                    throw new InvalidOperationException($"Rollback action {e.TargetAction} could not be found with the atomic parameters provided.");

                return;
            }

            if (!e.Result.SuccessfulExecution || CurrentStream.Value is not Guid streamID || CurrentTransaction.Value is not Guid transactionID) return;
            if (e.CalledAction is null) throw new InvalidOperationException($"Atomic action {e.TargetAction} could not be found with the supplied parameters.");

            (IBurcatObject?[] genericParameters, IBurcatObject?[] actionParameters) = SplitGenericParameters(e.Parameters);
            RollbackActionAttribute? attribute = e.CalledAction.GetCustomAttribute<RollbackActionAttribute>();

            if (attribute is null) return;

            IBurcatObject?[] rollbackParameters = [.. genericParameters, .. attribute.GetRollbackParameters(actionParameters)];
            RollbackActions.GetOrAdd(new([streamID, transactionID]), []).Push(new(e.ObjectType, e.TargetObject, attribute.RollbackAction, rollbackParameters));
        }

        public static void ClearRollback(Guid streamID, Guid transactionID) => RollbackActions.TryRemove(new([streamID, transactionID]), out _);

        public static void Rollback(Guid streamID, Guid transactionID)
        {
            if (!RollbackActions.TryRemove(new([streamID, transactionID]), out ConcurrentStack<RollbackAction>? actions)) return;

            List<Exception> exceptions = [];
            IsRollingBack.Value = true;
            try
            {
                while (actions.TryPop(out RollbackAction? action))
                {
                    ActionResult result = BurcatCache.ExecuteAction(action.ObjectType, action.TargetObject, action.Name, action.Parameters);
                    if (!result.SuccessfulExecution)
                    {
                        Exception exception = result.Exception is BurcatException burcatException
                            ? BurcatException.ToException(burcatException)
                            : new InvalidOperationException($"Rollback action {action.Name} did not execute successfully.");
                        exceptions.Add(exception);
                    }
                }
            }
            finally
            {
                IsRollingBack.Value = false;
            }

            if (exceptions.Count != 0) throw new AggregateException("One or more rollback actions failed.", exceptions);
        }

        private static (IBurcatObject?[] GenericParameters, IBurcatObject?[] ActionParameters) SplitGenericParameters(IBurcatObject?[] parameters)
        {
            int genericParameterCount = 0;
            while (genericParameterCount < parameters.Length && parameters[genericParameterCount] is BurcatType) genericParameterCount++;

            return ([.. parameters[..genericParameterCount]], [.. parameters[genericParameterCount..]]);
        }

        private sealed record RollbackAction(Type ObjectType, IBurcatObject? TargetObject, string Name, IBurcatObject?[] Parameters);
    }
}
