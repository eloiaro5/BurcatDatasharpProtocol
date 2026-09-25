using System;

namespace BurcatProtocol.Transactions
{
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Method | AttributeTargets.Event | AttributeTargets.Delegate)]
    public abstract class RollbackActionAttribute(string rollbackAction) : Attribute
    {
        public string RollbackAction { get; } = rollbackAction;
        public abstract IBurcatObject?[] GetRollbackParameters(IBurcatObject?[] parameters);
    }

    public sealed class ParameterlessRollbackActionAttribute(string rollbackAction) : RollbackActionAttribute(rollbackAction)
    {
        public override IBurcatObject?[] GetRollbackParameters(IBurcatObject?[] parameters) => [];
    }

    public sealed class SameParametersRollbackActionAttribute(string rollbackAction) : RollbackActionAttribute(rollbackAction)
    {
        public override IBurcatObject?[] GetRollbackParameters(IBurcatObject?[] parameters) => parameters;
    }
}
