using System;
using System.Reflection;

namespace BurcatProtocol.Cache
{
    public class BeforeConstructingEventArgs(Type objectType, IBurcatObject?[] parameters)
    {
        public Type ObjectType { get; } = objectType;
        public IBurcatObject?[] Parameters { get; set; } = parameters;
    }

    public class AfterConstructingEventArgs(Type objectType, IBurcatObject?[] parameters, ConstructorInfo? calledConstructor, IBurcatObject? constructedObject)
    {
        public Type ObjectType { get; } = objectType;
        public IBurcatObject?[] Parameters { get; } = parameters;
        public ConstructorInfo? CalledConstructor { get; } = calledConstructor;
        public IBurcatObject? ConstructedObject { get; } = constructedObject;
    }
}
