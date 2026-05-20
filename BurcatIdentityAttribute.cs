using System;
using System.Collections.Generic;
using System.Text;

namespace BurcatProtocol
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface | AttributeTargets.Enum, Inherited = false)]
    public sealed class BurcatIdentityAttribute : Attribute
    {
        public Guid Identity { get; }

        public BurcatIdentityAttribute(Guid identity) { Identity = identity; }
        public BurcatIdentityAttribute(string guid) : this(new Guid(guid)) { }  
    }
}
