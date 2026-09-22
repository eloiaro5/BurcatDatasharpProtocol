using System;

namespace BurcatProtocol.Cache
{
    public class BeforeExecutingActionEventArgs(Type objectType, IBurcatObject? targetObject, string name, IBurcatObject?[] parameters)
    {
        public Type ObjectType { get; } = objectType;
        public IBurcatObject? TargetObject { get; } = targetObject;
        public string Name { get; } = name;
        public IBurcatObject?[] Parameters { get; set; } = parameters;
    }

    public class AfterExecutingActionEventArgs(Type objectType, IBurcatObject? targetObject, string name, IBurcatObject?[] parameters, ActionResult result)
    {
        public Type ObjectType { get; } = objectType;
        public IBurcatObject? TargetObject { get; } = targetObject;
        public string Name { get; } = name;
        public IBurcatObject?[] Parameters { get; } = parameters;
        public ActionResult Result { get; } = result;
    }
}
