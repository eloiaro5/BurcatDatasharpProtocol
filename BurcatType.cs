using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace BurcatProtocol
{
    [BurcatIdentity("00000000-0000-0000-0000-59769999f8f4")]
    public sealed class BurcatType : IBurcatObject
    {
        Guid IBurcatObject.Identifier { get; set => throw new InvalidOperationException(); } = Guid.Empty;

        public Guid ClassID { get; }
        public bool Nullable { get; }

        public BurcatType(Type type, bool nullable)
        {
            Nullable = nullable;

            if (System.Nullable.GetUnderlyingType(type) is Type underlying) ClassID = BurcatChat.GetClassIdentity(underlying);
            else ClassID = BurcatChat.GetClassIdentity(type);
        }
        public BurcatType(Type type) : this(type, type.MightBeNull()) { }
        public BurcatType(Guid classID) { ClassID = classID; }
        public BurcatType(Guid classID, bool nullable) : this(classID) { Nullable = nullable; }

        public Type GetTypeCLR() => BurcatChat.GetType(ClassID);

        BurcatField[] IBurcatObject.GetBurcatFields() => [];
        bool IBurcatObject.SetBurcatField(BurcatField field) => false;
        IBurcatObject?[] IBurcatObject.GetBurcatConstructionValues() => BurcatTranslator.FullObjectsTranslate([ClassID, Nullable]);
    }
}
