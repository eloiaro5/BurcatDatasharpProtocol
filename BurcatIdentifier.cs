using System.Runtime.InteropServices;

namespace BurcatProtocol
{
    /// <summary>
    /// Represents a typed reference to a Burcat object.
    /// </summary>
    /// <typeparam name="T">The referenced Burcat object type.</typeparam>
    [BurcatIdentity("00000000-0000-0000-0000-2728bc4f1b77")]
    public readonly struct BurcatIdentifier<T> : IBurcatObject, IEquatable<BurcatIdentifier<T>> where T : IBurcatObject
    {
        /// <inheritdoc/>
        Guid IBurcatObject.Identifier { get; set => throw new InvalidOperationException(); } = Guid.Empty;

        /// <inheritdoc/>
        Guid IBurcatObject.Revision { get; set => throw new InvalidOperationException(); } = Guid.Empty;

        /// <summary>
        /// Gets the referenced object's identifier.
        /// </summary>
        public Guid Value { get; }

        /// <summary>
        /// Initializes a typed Burcat reference.
        /// </summary>
        /// <param name="value">The referenced object's identifier.</param>
        public BurcatIdentifier(Guid value) { Value = value; }

        /// <summary>
        /// Initializes a typed Burcat reference from an object.
        /// </summary>
        /// <param name="value">The referenced object.</param>
        public BurcatIdentifier(T value) : this(value.Identifier) { }

        /// <summary>
        /// Reinterprets this reference as a reference to a derived type.
        /// </summary>
        /// <typeparam name="N">The derived Burcat object type.</typeparam>
        /// <returns>A typed reference with the same identifier.</returns>
        public BurcatIdentifier<N> Upcast<N>() where N : T => new(Value);

        /// <summary>
        /// Reinterprets this reference as a reference to another compatible type.
        /// </summary>
        /// <typeparam name="N">The target Burcat object type.</typeparam>
        /// <returns>A typed reference with the same identifier.</returns>
        /// <exception cref="InvalidCastException">Thrown when the target type is not compatible.</exception>
        public BurcatIdentifier<N> Downcast<N>() where N : IBurcatObject
        {
            if (!typeof(N).IsAssignableFrom(typeof(T))) throw new InvalidCastException();
            else return new(Value);
        }

        /// <inheritdoc/>
        public bool Equals(BurcatIdentifier<T> other) => this == other;

        /// <inheritdoc/>
        public override bool Equals(object? obj)
        {
            if (obj is null) return false;
            else if (obj is BurcatIdentifier<T> other) return this == other;
            else return false;
        }

        /// <inheritdoc/>
        public override int GetHashCode() => Value.GetHashCode();

        /// <inheritdoc/>
        BurcatField[] IBurcatObject.GetBurcatFields() => [];

        /// <inheritdoc/>
        void IBurcatObject.SetBurcatFields(BurcatField[] fields) { }

        /// <inheritdoc/>
        IBurcatObject?[] IBurcatObject.GetBurcatConstructionValues() => BurcatTranslator.ObjectsTranslate([new BurcatType(typeof(T), false), Value]);

        /// <summary>
        /// Determines whether two typed Burcat references point to the same identifier.
        /// </summary>
        /// <param name="i1">The first reference.</param>
        /// <param name="i2">The second reference.</param>
        /// <returns><see langword="true"/> when both references are equal; otherwise, <see langword="false"/>.</returns>
        public static bool operator ==(BurcatIdentifier<T>? i1, BurcatIdentifier<T>? i2) => i1 is null && i2 is null || (i1 is not null && i2 is not null && i1.Value.Value == i2.Value.Value);
        /// <summary>
        /// Determines whether two typed Burcat references point to different identifiers.
        /// </summary>
        /// <param name="i1">The first reference.</param>
        /// <param name="i2">The second reference.</param>
        /// <returns><see langword="true"/> when the references are different; otherwise, <see langword="false"/>.</returns>
        public static bool operator !=(BurcatIdentifier<T>? i1, BurcatIdentifier<T>? i2) => !(i1 == i2);

        /// <summary>
        /// Determines whether a uuid, representing an identifier, points to the same identifier as a typed Burcat reference.
        /// </summary>
        /// <param name="g">The uuid.</param>
        /// <param name="i">The reference.</param>
        /// <returns><see langword="true"/> when both references are equal; otherwise, <see langword="false"/>.</returns>
        public static bool operator ==(Guid? g, BurcatIdentifier<T>? i) => g is null && i is null || (g is not null && i is not null && g.Value == i.Value.Value);
        /// <summary>
        /// Determines whether a uuid, representing an identifier, points to a different identifier as a typed Burcat reference.
        /// </summary>
        /// <param name="g">The uuid.</param>
        /// <param name="i">The reference.</param>
        /// <returns><see langword="true"/> when the references are different; otherwise, <see langword="false"/>.</returns>
        public static bool operator !=(Guid? g, BurcatIdentifier<T>? i) => !(g == i);
        /// <summary>
        /// Determines whether a uuid, representing an identifier, points to the same identifier as a typed Burcat reference.
        /// </summary>
        /// <param name="g">The uuid.</param>
        /// <param name="i">The reference.</param>
        /// <returns><see langword="true"/> when both references are equal; otherwise, <see langword="false"/>.</returns>
        public static bool operator ==(BurcatIdentifier<T>? i, Guid? g) => g == i;
        /// <summary>
        /// Determines whether a uuid, representing an identifier, points to a different identifier as a typed Burcat reference.
        /// </summary>
        /// <param name="g">The uuid.</param>
        /// <param name="i">The reference.</param>
        /// <returns><see langword="true"/> when the references are different; otherwise, <see langword="false"/>.</returns>
        public static bool operator !=(BurcatIdentifier<T>? i, Guid? g) => g != i;

        /// <summary>
        /// Converts an object to a typed Burcat reference.
        /// </summary>
        /// <param name="value">The referenced object.</param>
        public static implicit operator BurcatIdentifier<T>(T value) => new(value);

        /// <summary>
        /// Converts a typed Burcat reference to its raw identifier.
        /// </summary>
        /// <param name="identifier">The typed reference.</param>
        public static explicit operator Guid(BurcatIdentifier<T> identifier) => identifier.Value;
    }
}
