using System;

namespace BurcatProtocol.Cache
{
    public class BeforeConstructingEventArgs(Type objectType, IBurcatObject?[] parameters)
    {
        public Type ObjectType { get; } = objectType;
        public IBurcatObject?[] Parameters { get; set; } = parameters;
    }

    public class AfterConstructingEventArgs(Type objectType, IBurcatObject?[] parameters, IBurcatObject? constructedObject)
    {
        public Type ObjectType { get; } = objectType;
        public IBurcatObject?[] Parameters { get; } = parameters;
        public IBurcatObject? ConstructedObject { get; } = constructedObject;
    }
}
