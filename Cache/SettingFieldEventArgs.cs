using System;
using System.Reflection;

namespace BurcatProtocol.Cache
{
    public class BeforeSettingFieldEventArgs(Type objectType, IBurcatObject? targetObject, BurcatField targetField, bool validate)
    {
        public Type ObjectType { get; } = objectType;
        public IBurcatObject? TargetObject { get; } = targetObject;
        public BurcatField TargetField { get; private set; } = targetField;
        public bool Validate { get; set; } = validate;

        public void SetValue(object? value) => TargetField = new(TargetField.Name, value);
    }

    public class AfterSettingFieldEventArgs(Type objectType, IBurcatObject? targetObject, MemberInfo? targetMember, BurcatField targetField, bool? validated, BurcatException? exception)
    {
        public Type ObjectType { get; } = objectType;
        public IBurcatObject? TargetObject { get; } = targetObject;
        public MemberInfo? TargetMember { get; } = targetMember;
        public BurcatField TargetField { get; } = targetField;
        public bool? Validated { get; } = validated;
        public BurcatException? Exception { get; } = exception;
    }
}
