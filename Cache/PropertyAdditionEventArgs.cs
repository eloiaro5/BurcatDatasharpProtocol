using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace BurcatProtocol.Cache
{
    public sealed class PropertyAdditionEventArgs(Type objectType, PropertyInfo property)
    {
        public Type ObjectType { get; } = objectType;
        public PropertyInfo Property { get; } = property;
    }
}
