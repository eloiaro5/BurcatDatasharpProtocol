using System.Runtime.InteropServices;

namespace BurcatProtocol
{
    [BurcatIdentity("00000000-0000-0000-0000-2728bc4f1b77")]
    public readonly struct BurcatIdentifier<T> : IBurcatObject, IEquatable<BurcatIdentifier<T>> where T : IBurcatObject
    {
        Guid IBurcatObject.Identifier { get; set => throw new InvalidOperationException(); } = Guid.Empty;
        Guid IBurcatObject.Revision { get; set => throw new InvalidOperationException(); } = Guid.Empty;

        public Guid Value { get; }

        public BurcatIdentifier(Guid value) { Value = value; }
        public BurcatIdentifier(T value) : this(value.Identifier) { }

        public BurcatIdentifier<N> Upcast<N>() where N : T => new(Value);
        public BurcatIdentifier<N> Downcast<N>() where N : IBurcatObject
        {
            if (!typeof(N).IsAssignableFrom(typeof(T))) throw new InvalidCastException();
            else return new(Value);
        }

        public bool Equals(BurcatIdentifier<T> other) => this == other;
        public override bool Equals(object? obj)
        {
            if (obj is null) return false;
            else if (obj is BurcatIdentifier<T> other) return this == other;
            else return false;
        }
        public override int GetHashCode() => Value.GetHashCode();

        BurcatField[] IBurcatObject.GetBurcatFields() => [];
        bool IBurcatObject.SetBurcatField(BurcatField field) => false;
        IBurcatObject?[] IBurcatObject.GetBurcatConstructionValues() => BurcatTranslator.ObjectsTranslate([new BurcatType(typeof(T), false), Value]);

        public static bool operator ==(BurcatIdentifier<T>? i1, BurcatIdentifier<T>? i2) => i1 is null && i2 is null || (i1 is not null && i2 is not null && i1.Value.Value == i2.Value.Value);
        public static bool operator !=(BurcatIdentifier<T>? i1, BurcatIdentifier<T>? i2) => !(i1 == i2);

        public static implicit operator BurcatIdentifier<T>(T value) => new(value);
        public static explicit operator Guid(BurcatIdentifier<T> identifier) => identifier.Value;
    }
}
