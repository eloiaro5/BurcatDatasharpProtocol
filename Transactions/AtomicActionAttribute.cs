using System;

namespace BurcatProtocol.Transactions
{
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Method | AttributeTargets.Event | AttributeTargets.Delegate)]
    public sealed class AtomicActionAttribute : Attribute { }
}
