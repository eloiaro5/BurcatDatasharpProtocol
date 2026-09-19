using System;
using System.Collections.Generic;
using System.Text;

namespace BurcatProtocol
{
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Event | AttributeTargets.Delegate)]
    public class BurcatContextAttribute : Attribute { }
}
