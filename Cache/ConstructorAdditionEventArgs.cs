using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace BurcatProtocol.Cache
{
    public sealed class ConstructorAdditionEventArgs(Type objectType, ConstructorInfo constructor)
    {
        public Type ObjectType { get; } = objectType;
        public ConstructorInfo Constructor { get; } = constructor;
    }
}
