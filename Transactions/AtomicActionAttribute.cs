using System;
using System.Collections.Generic;
using System.Text;

namespace BurcatProtocol.Transactions
{
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Method | AttributeTargets.Event | AttributeTargets.Delegate)]
    public class AtomicActionAttribute : Attribute
    {
        public AtomicActionAttribute() { }
        public AtomicActionAttribute(string rollbackAction) { RollbackAction = rollbackAction; }

        public string? RollbackAction { get; }
    }
}
