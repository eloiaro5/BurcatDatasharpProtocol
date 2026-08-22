using BurcatProtocol.Collections;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection.PortableExecutable;
using System.Text;
using System.Xml.Linq;

namespace BurcatProtocol
{
    [BurcatIdentity("00000000-0000-0000-0000-281b2c132e88")]
    public sealed class BurcatHeaderCollection : IBurcatObject, IEnumerable<BurcatHeaderCollection.BurcatHeader>
    {
        private Dictionary<Guid, Dictionary<string, IBurcatObject?>> Headers { get; } = [];

        /// <inheritdoc/>
        Guid IBurcatObject.Identifier { get; set => throw new InvalidOperationException(); } = Guid.Empty;
        /// <inheritdoc/>
        Guid IBurcatObject.Revision { get; set => throw new InvalidOperationException(); } = Guid.Empty;

        public BurcatHeaderCollection() { }
        public BurcatHeaderCollection(IEnumerable<BurcatHeader> headers)
        {
            foreach (BurcatHeader header in headers)
                AddHeader(header);
        }

        public bool AddHeader(BurcatHeader header)
        {
            Headers.TryAdd(header.Package, []);
            return Headers[header.Package].TryAdd(header.Name, header.Value);
        }

        public bool RemoveHeader(Guid package, string name) => Headers.TryGetValue(package, out Dictionary<string, IBurcatObject?>? headers) && headers.Remove(name);
        public bool RemoveHeader(BurcatHeader header) => RemoveHeader(header.Package, header.Name);

        public bool RemoveHeaders(Guid package) => Headers.Remove(package);

        /// <inheritdoc/>
        BurcatField[] IBurcatObject.GetBurcatFields() => [];
        /// <inheritdoc/>
        void IBurcatObject.SetBurcatFields(BurcatField[] fields) { }

        /// <inheritdoc/>
        public IEnumerator<BurcatHeader> GetEnumerator()
        {
            foreach (KeyValuePair<Guid, Dictionary<string, IBurcatObject?>> package in Headers)
                foreach (KeyValuePair<string, IBurcatObject?> header in package.Value)
                    yield return new(package.Key, header.Key, header.Value);
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        /// <inheritdoc/>
        IBurcatObject?[] IBurcatObject.GetBurcatConstructionValues() => new BurcatList<BurcatHeader>([.. this]);

        public sealed class BurcatHeader : IBurcatObject
        {
            /// <inheritdoc/>
            Guid IBurcatObject.Identifier { get; set => throw new InvalidOperationException(); } = Guid.Empty;
            /// <inheritdoc/>
            Guid IBurcatObject.Revision { get; set => throw new InvalidOperationException(); } = Guid.Empty;

            public Guid Package { get; }
            public string Name { get; }
            public IBurcatObject? Value { get; }

            public BurcatHeader(Guid package, string name, IBurcatObject? value = null)
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
}
