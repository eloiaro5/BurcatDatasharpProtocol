using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace BurcatProtocol.Collections
{
    [BurcatIdentity("00000000-0000-0000-0000-c0531c59865a")]
    public readonly struct KeyValueDuo<TK, TV> : IBurcatObject where TK : notnull
    {
        Guid IBurcatObject.Identifier { get; set => throw new InvalidOperationException(); } = Guid.Empty;

        public TK Key { get; }
        public TV Value { get; }

        public KeyValueDuo(TK key, TV value) { Key = key; Value = value; }

        BurcatField[] IBurcatObject.GetBurcatFields() => [];
        bool IBurcatObject.SetBurcatField(BurcatField field) => false;

        IBurcatObject?[] IBurcatObject.GetBurcatConstructionValues()
        {
            BurcatType keyType = new(BurcatChat.GetClassIdentity<TK>(), false), valueType = new(BurcatChat.GetClassIdentity<TV>());
            return BurcatTranslator.ObjectsTranslate([keyType, valueType, Key, Value]);
        }

        public static implicit operator KeyValueDuo<TK, TV>(KeyValuePair<TK, TV> pair) => new(pair.Key, pair.Value);
        public static implicit operator KeyValuePair<TK, TV>(KeyValueDuo<TK, TV> pair) => new(pair.Key, pair.Value);
    }
}
