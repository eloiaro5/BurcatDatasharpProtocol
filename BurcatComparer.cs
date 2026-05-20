using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Text;

namespace BurcatProtocol
{
    [BurcatIdentity("00000000-0000-0000-0000-16e25158b345")]
    public class BurcatComparer : IBurcatObject, IComparer<IBurcatObject>, IEqualityComparer<IBurcatObject>
    {
        public static BurcatComparer Default { get; } = new();

        Guid IBurcatObject.Identifier { get; set => throw new InvalidOperationException(); } = Guid.Empty;

        public int Compare(IBurcatObject? x, IBurcatObject? y)
        {
            if (x is null && y is null) return 0;
            else if (x is null) return -1;
            else if (y is null) return 1;
            else if (ReferenceEquals(x, y)) return 0;
            else
            {
                Guid xV = BurcatChat.GetClassIdentity(x.GetType()), yV = BurcatChat.GetClassIdentity(y.GetType());
                if (xV == yV) return x.Identifier.CompareTo(y.Identifier);
                else if (xV < yV) return -1;
                else return 1;
            }
        }

        public bool Equals(IBurcatObject? x, IBurcatObject? y) => Compare(x, y) == 0;
        public int GetHashCode([DisallowNull] IBurcatObject objectBDP) => objectBDP.Identifier.GetHashCode();

        BurcatField[] IBurcatObject.GetBurcatFields() => [];
        bool IBurcatObject.SetBurcatField(BurcatField field) => false;
        IBurcatObject?[] IBurcatObject.GetBurcatConstructionValues() => [];
    }

    [BurcatIdentity("00000000-0000-0000-0000-b506c6bc5c2b")]
    public class BurcatComparerBDP<T> : BurcatComparer, IComparer<T>, IEqualityComparer<T> where T : IBurcatObject?
    {
        public static BurcatComparerBDP<T> GenericDefault { get; } = new();

        public int Compare(T? x, T? y) => base.Compare(x, y);

        public bool Equals(T? x, T? y) => base.Equals(x, y);
        public int GetHashCode([DisallowNull] T obj) => base.GetHashCode(obj);
    }
}
