using System;

namespace BurcatProtocol.Cache
{
    public class BeforeSettingFieldEventArgs(Type objectType, IBurcatObject? targetObject, BurcatField field, bool validate)
    {
        public Type ObjectType { get; } = objectType;
        public IBurcatObject? TargetObject { get; } = targetObject;
        public BurcatField Field { get; private set; } = field;
        public bool Validate { get; set; } = validate;

        public void SetValue(object? value) => Field = new(Field.Name, value);
    }

    public class AfterSettingFieldEventArgs(Type objectType, IBurcatObject? targetObject, BurcatField field, bool validate, BurcatException? exception)
    {
        public Type ObjectType { get; } = objectType;
        public IBurcatObject? TargetObject { get; } = targetObject;
        public BurcatField Field { get; } = field;
        public bool Validate { get; } = validate;
        public BurcatException? Exception { get; } = exception;
    }
}
