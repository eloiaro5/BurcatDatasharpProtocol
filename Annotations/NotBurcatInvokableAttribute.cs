using System;
using System.Collections.Generic;
using System.Text;

namespace BurcatProtocol.Annotations
{
    /// <summary>
    /// Excludes a member from Burcat protocol field discovery, construction, or action invocation.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Constructor | AttributeTargets.Method)]
    public class NotBurcatInvokableAttribute : Attribute { }
}
