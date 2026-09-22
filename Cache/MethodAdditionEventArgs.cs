using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace BurcatProtocol.Cache
{
    public sealed class MethodAdditionEventArgs(Type objectType, MethodInfo method)
    {
        public Type ObjectType { get; } = objectType;
        public MethodInfo Method { get; } = method;
    }
}
