using System;
using System.Collections.Generic;
using System.Text;

namespace BurcatProtocol.Collections
{
    [BurcatIdentity("00000000-0000-0000-0000-c0531c59865a")]
    public readonly struct BurcatTuple<T1, T2> : IBurcatObject
    {
        Guid IBurcatObject.Identifier { get; set => throw new InvalidOperationException(); } = Guid.Empty;
        Guid IBurcatObject.Revision { get; set => throw new InvalidOperationException(); } = Guid.Empty;

        public T1 Value1 { get; }
        public T2 Value2 { get; }

        public BurcatTuple(T1 value1, T2 value2) { Value1 = value1; Value2 = value2; }

        BurcatField[] IBurcatObject.GetBurcatFields() => [];
        bool IBurcatObject.SetBurcatField(BurcatField field) => false;

        IBurcatObject?[] IBurcatObject.GetBurcatConstructionValues()
        {
            BurcatType value1Type = new(BurcatChat.GetClassIdentity<T1>()), value2Type = new(BurcatChat.GetClassIdentity<T2>());
            return BurcatTranslator.ObjectsTranslate([value1Type, value2Type, Value1, Value2]);
        }
    }
}
