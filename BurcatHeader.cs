using System;
using System.Collections.Generic;
using System.Text;

namespace BurcatProtocol
{
    public sealed class BurcatHeader : IBurcatObject
    {
        /// <inheritdoc/>
        Guid IBurcatObject.Identifier { get; set => throw new InvalidOperationException(); } = Guid.Empty;
        /// <inheritdoc/>
        Guid IBurcatObject.Revision { get; set => throw new InvalidOperationException(); } = Guid.Empty;

        public Guid Package { get; }
        public string Name { get; }
        public IBurcatObject? Value { get; }

        public BurcatHeader(Guid package, string name, IBurcatObject? value)
        {
            Package = package;
            Name = name;
            Value = value;
        }

        /// <inheritdoc/>
        BurcatField[] IBurcatObject.GetBurcatFields() => [];
        /// <inheritdoc/>
        void IBurcatObject.SetBurcatFields(BurcatField[] fields) { }

        /// <inheritdoc/>
        IBurcatObject?[] IBurcatObject.GetBurcatConstructionValues() => BurcatTranslator.ObjectsTranslate([Package, Name, Value]);
    }
}
