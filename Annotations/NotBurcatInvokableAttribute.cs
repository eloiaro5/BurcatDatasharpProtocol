using System;
using System.Collections.Generic;
using System.Text;

namespace BurcatProtocol.Annotations
{
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Constructor | AttributeTargets.Method)]
    public class NotBurcatInvokableAttribute : Attribute { }
}
