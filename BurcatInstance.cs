namespace BurcatProtocol
{
    public sealed class BurcatInstance
    {
        public static BurcatInstance Build<T>(T? instance) where T : IBurcatObject => new(typeof(T), instance);

        public Type Type { get; }
        public IBurcatObject? Value { get; }

        public BurcatInstance(Type type, IBurcatObject? value = null) { Type = type; Value = value; }
        public BurcatInstance(IBurcatObject value) : this(value.GetType(), value) { }

        public T ForceValue<T>()
        {
            if (typeof(T).IsAssignableFrom(Type)) return (T)Value!;
            else throw new InvalidCastException();
        }
    }
}
