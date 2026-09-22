using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace BurcatProtocol.Cache
{
    public sealed class FieldAdditionEventArgs(Type objectType, FieldInfo field)
    {
        public Type ObjectType { get; } = objectType;
        public FieldInfo Field { get; } = field;
    }
}
