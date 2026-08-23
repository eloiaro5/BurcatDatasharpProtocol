using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace BurcatProtocol
{
    /// <summary>
    /// Represents a protocol header identified by its package and name.
    /// </summary>
    /// <remarks>
    /// Equality and hashing use only <see cref="Package"/> and <see cref="Name"/>;
    /// <see cref="Value"/> does not participate in header identity.
    /// </remarks>
    [BurcatIdentity("00000000-0000-0000-0000-b997facfcadb")]
    public sealed class BurcatHeader : IBurcatObject
    {
        /// <inheritdoc/>
        Guid IBurcatObject.Identifier { get; set => throw new InvalidOperationException(); } = Guid.Empty;
        /// <inheritdoc/>
        Guid IBurcatObject.Revision { get; set => throw new InvalidOperationException(); } = Guid.Empty;

        /// <summary>
        /// Gets the identity of the package that defines the header.
        /// </summary>
        public Guid Package { get; }

        /// <summary>
        /// Gets the header name within its package.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Gets the optional protocol value carried by the header.
        /// </summary>
        public string? Value { get; }

        /// <summary>
        /// Initializes a header with its package, name, and optional value.
        /// </summary>
        /// <param name="package">The identity of the package that defines the header.</param>
        /// <param name="name">The header name within the package.</param>
        /// <param name="value">The optional protocol value carried by the header.</param>
        public BurcatHeader(Guid package, string name, string? value = null)
        {
            Package = package;
            Name = name;
            Value = value;
        }

        public bool TryGetValue<T>([MaybeNullWhen(false)] out T? value) where T : notnull
        {
            if (Value is null)
            {
                value = default;
                return false;
            }
            else return Transformable.TryDynamicCast<T>(Value, out value);
        }
        public T TryGetValue<T>() where T : notnull
        {
            if (TryGetValue(out T? value)) return value!;
            else throw new InvalidCastException();
        }

        /// <inheritdoc/>
        public override bool Equals(object? obj)
        {
            if (ReferenceEquals(this, obj)) return true;
            else if (obj is BurcatHeader other) return Package.Equals(other.Package) && Name.Equals(other.Name);
            else return false;
        }
        /// <inheritdoc/>
        public override int GetHashCode() => HashCode.Combine(Package, Name);

        /// <inheritdoc/>
        BurcatField[] IBurcatObject.GetBurcatFields() => [];
        /// <inheritdoc/>
        void IBurcatObject.SetBurcatFields(BurcatField[] fields) { }

        /// <inheritdoc/>
        IBurcatObject?[] IBurcatObject.GetBurcatConstructionValues() => BurcatTranslator.ObjectsTranslate([Package, Name, Value]);
    }
}
