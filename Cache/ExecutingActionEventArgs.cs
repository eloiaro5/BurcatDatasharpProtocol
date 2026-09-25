using System;
using System.Reflection;

namespace BurcatProtocol.Cache
{
    public class BeforeExecutingActionEventArgs(Type objectType, IBurcatObject? targetObject, string targetAction, IBurcatObject?[] parameters)
    {
        public Type ObjectType { get; } = objectType;
        public IBurcatObject? TargetObject { get; } = targetObject;
        public string TargetAction { get; } = targetAction;
        public IBurcatObject?[] Parameters { get; set; } = parameters;
    }

    public class AfterExecutingActionEventArgs(Type objectType, IBurcatObject? targetObject, string targetAction, IBurcatObject?[] parameters, MethodInfo? calledAction, ActionResult result)
    {
        public Type ObjectType { get; } = objectType;
        public IBurcatObject? TargetObject { get; } = targetObject;
        public string TargetAction { get; } = targetAction;
        public IBurcatObject?[] Parameters { get; } = parameters;
        public MethodInfo? CalledAction { get; } = calledAction;
        public ActionResult Result { get; } = result;
    }
}
