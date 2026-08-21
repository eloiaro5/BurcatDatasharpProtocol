using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace BurcatProtocol
{
    public sealed class BurcatEqualityComparer<T> : IBurcatObject, IEqualityComparer<T>
    {
        public static BurcatEqualityComparer<T> Default => new();

        /// <inheritdoc/>
        Guid IBurcatObject.Identifier { get; set => throw new InvalidOperationException(); } = Guid.Empty;
        /// <inheritdoc/>
        Guid IBurcatObject.Revision { get; set => throw new InvalidOperationException(); } = Guid.Empty;

        public BurcatEqualityComparer() { }

        public bool Equals(T? x, T? y) => EqualityComparer<T>.Default.Equals(x, y);
        public int GetHashCode([DisallowNull] T obj) => EqualityComparer<T>.Default.GetHashCode(obj);

        /// <inheritdoc/>
        BurcatField[] IBurcatObject.GetBurcatFields() => [];
        /// <inheritdoc/>
        void IBurcatObject.SetBurcatFields(BurcatField[] fields) { }

        /// <inheritdoc/>
        IBurcatObject?[] IBurcatObject.GetBurcatConstructionValues() => [];
    }
}
