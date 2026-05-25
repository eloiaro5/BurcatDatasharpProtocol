using System;
using System.Collections.Generic;
using System.Text;

namespace BurcatProtocol
{
    [BurcatIdentity("00000000-0000-0000-0000-6b9a80e29a67 ")]
    public sealed class BurcatTranslation : IBurcatObject
    {
        public Guid Identifier { get => Guid.Empty; set => throw new InvalidOperationException(); }

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
