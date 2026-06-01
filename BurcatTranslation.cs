using System;
using System.Collections.Generic;
using System.Text;

namespace BurcatProtocol
{
    [BurcatIdentity("00000000-0000-0000-0000-6b9a80e29a67 ")]
    public sealed class BurcatTranslation : IBurcatObject
    {
        public Guid Identifier { get; set => throw new InvalidOperationException(); } = Guid.Empty;
        Guid IBurcatObject.Revision { get; set => throw new InvalidOperationException(); } = Guid.Empty;

        public Guid ClassID { get; }
        public byte[] Data { get; }

        public BurcatTranslation(Guid classID, byte[] translation)
        {
            ClassID = classID;
            Data = translation;
        }

        public BurcatField[] GetBurcatFields() => [];
        public bool SetBurcatField(BurcatField field) => false;

        public IBurcatObject?[] GetBurcatConstructionValues() => [];
    }
}
