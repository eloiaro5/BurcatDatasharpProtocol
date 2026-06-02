using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Text;

namespace BurcatProtocol
{
    /// <summary>
    /// Compares Burcat objects by class identity, object identifier, and revision.
    /// </summary>
    [BurcatIdentity("00000000-0000-0000-0000-16e25158b345")]
    public class BurcatComparer : IBurcatObject, IComparer<IBurcatObject>, IEqualityComparer<IBurcatObject>
    {
        /// <summary>
        /// Gets the default Burcat object comparer.
        /// </summary>
        public static BurcatComparer Default { get; } = new();

        /// <inheritdoc/>
        Guid IBurcatObject.Identifier { get; set => throw new InvalidOperationException(); } = Guid.Empty;

        /// <inheritdoc/>
        Guid IBurcatObject.Revision { get; set => throw new InvalidOperationException(); } = Guid.Empty;

        /// <inheritdoc/>
        public int Compare(IBurcatObject? x, IBurcatObject? y)
        {
            if (x is null && y is null) return 0;
            else if (x is null) return -1;
            else if (y is null) return 1;
            else if (ReferenceEquals(x, y)) return 0;
            else
            {
                int comparationV = BurcatChat.GetClassIdentity(x.GetType()).CompareTo(BurcatChat.GetClassIdentity(y.GetType()));
                if (comparationV == 0)
                {
                    int comparationI = x.Identifier.CompareTo(y.Identifier);

                    if (comparationI == 0) return x.Revision.CompareTo(y.Revision);
                    else return comparationI;
                }
                else return comparationV;
            }
        }

        /// <inheritdoc/>
        public bool Equals(IBurcatObject? x, IBurcatObject? y) => Compare(x, y) == 0;

        /// <inheritdoc/>
        public int GetHashCode([DisallowNull] IBurcatObject objectBDP) => objectBDP.Identifier.GetHashCode();

        /// <inheritdoc/>
        BurcatField[] IBurcatObject.GetBurcatFields() => [];

        /// <inheritdoc/>
        void IBurcatObject.SetBurcatFields(BurcatField[] fields) { }

        /// <inheritdoc/>
        IBurcatObject?[] IBurcatObject.GetBurcatConstructionValues() => [];
    }

    /// <summary>
    /// Type-specific Burcat comparer for objects assignable to <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">The Burcat object type to compare.</typeparam>
    [BurcatIdentity("00000000-0000-0000-0000-b506c6bc5c2b")]
    public class BurcatComparerBDP<T> : BurcatComparer, IComparer<T>, IEqualityComparer<T> where T : IBurcatObject?
    {
        /// <summary>
        /// Gets the default comparer for <typeparamref name="T"/>.
        /// </summary>
        public static BurcatComparerBDP<T> GenericDefault { get; } = new();

        /// <inheritdoc/>
        public int Compare(T? x, T? y) => base.Compare(x, y);

        /// <inheritdoc/>
        public bool Equals(T? x, T? y) => base.Equals(x, y);

        /// <inheritdoc/>
        public int GetHashCode([DisallowNull] T obj) => base.GetHashCode(obj);
    }
}
