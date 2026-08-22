using BurcatProtocol.Collections;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection.PortableExecutable;
using System.Text;
using System.Xml.Linq;

namespace BurcatProtocol
{
    /// <summary>
    /// Represents the protocol headers supported during a Burcat communication session.
    /// </summary>
    /// <remarks>
    /// Headers are uniquely identified by their package and name. Their values carry
    /// header-specific data and do not participate in membership or equality checks.
    /// </remarks>
    [BurcatIdentity("00000000-0000-0000-0000-f045afd6bbb9")]
    public sealed class BurcatHeaderSet : IBurcatObject, ISet<BurcatHeaderSet.BurcatHeader>
    {
        private Dictionary<Guid, Dictionary<string, IBurcatObject?>> Headers { get; } = [];

        /// <inheritdoc/>
        Guid IBurcatObject.Identifier { get; set => throw new InvalidOperationException(); } = Guid.Empty;
        /// <inheritdoc/>
        Guid IBurcatObject.Revision { get; set => throw new InvalidOperationException(); } = Guid.Empty;

        /// <inheritdoc/>
        public int Count => Headers.Sum(header => header.Value.Count);
        /// <inheritdoc/>
        public bool IsReadOnly => false;

        /// <summary>
        /// Initializes an empty header collection.
        /// </summary>
        public BurcatHeaderSet() { }

        /// <summary>
        /// Initializes a header collection from the specified headers.
        /// </summary>
        /// <param name="headers">The headers to add. Duplicate package-and-name pairs are ignored.</param>
        public BurcatHeaderSet(IEnumerable<BurcatHeader> headers)
        {
            foreach (BurcatHeader header in headers)
                Add(header);
        }

        /// <summary>
        /// Adds a header when its package and name are not already present.
        /// </summary>
        /// <param name="header">The header to add.</param>
        /// <returns><see langword="true"/> when the header was added; otherwise, <see langword="false"/>.</returns>
        public bool Add(BurcatHeader header)
        {
            Headers.TryAdd(header.Package, []);
            return Headers[header.Package].TryAdd(header.Name, header.Value);
        }

        /// <summary>
        /// Removes the header identified by a package and name.
        /// </summary>
        /// <param name="package">The package that owns the header.</param>
        /// <param name="name">The header name.</param>
        /// <returns><see langword="true"/> when the header was removed; otherwise, <see langword="false"/>.</returns>
        public bool Remove(Guid package, string name) => Headers.TryGetValue(package, out Dictionary<string, IBurcatObject?>? headers) && headers.Remove(name);

        /// <summary>
        /// Removes the header with the same package and name as the specified header.
        /// </summary>
        /// <param name="header">The header whose identity should be removed.</param>
        /// <returns><see langword="true"/> when the header was removed; otherwise, <see langword="false"/>.</returns>
        public bool Remove(BurcatHeader header) => Remove(header.Package, header.Name);

        /// <summary>
        /// Removes every header belonging to a package.
        /// </summary>
        /// <param name="package">The package whose headers should be removed.</param>
        /// <returns><see langword="true"/> when the package and its headers were removed; otherwise, <see langword="false"/>.</returns>
        public bool Remove(Guid package) => Headers.Remove(package);

        /// <inheritdoc/>
        public void Clear() => Headers.Clear();

        /// <inheritdoc/>
        public void CopyTo(BurcatHeader[] array, int arrayIndex)
        {
            if (arrayIndex > array.Length || array.Length - arrayIndex < Count) throw new ArgumentException("The destination array does not have enough available space.", nameof(array));
            else
                foreach (BurcatHeader header in this)
                    array[arrayIndex++] = header;
        }

        /// <inheritdoc/>
        public void UnionWith(IEnumerable<BurcatHeader> other)
        {
            foreach (BurcatHeader header in other)
                Add(header);
        }

        /// <inheritdoc/>
        public void IntersectWith(IEnumerable<BurcatHeader> other)
        {
            HashSet<BurcatHeader> otherHeaders = [.. other];
            BurcatHeader[] removedHeaders = [.. this.Where(header => !otherHeaders.Contains(header))];
            foreach (BurcatHeader header in removedHeaders)
                Remove(header);
        }

        /// <inheritdoc/>
        public void ExceptWith(IEnumerable<BurcatHeader> other)
        {
            if (ReferenceEquals(this, other)) Clear();
            else
                foreach (BurcatHeader header in other)
                    Remove(header);
        }

        /// <inheritdoc/>
        public void SymmetricExceptWith(IEnumerable<BurcatHeader> other)
        {
            if (ReferenceEquals(this, other)) Clear();
            else
                foreach (BurcatHeader header in new HashSet<BurcatHeader>(other))
                    if (!Remove(header))
                        Add(header);
        }

        /// <inheritdoc/>
        public IEnumerator<BurcatHeader> GetEnumerator()
        {
            foreach (KeyValuePair<Guid, Dictionary<string, IBurcatObject?>> package in Headers)
                foreach (KeyValuePair<string, IBurcatObject?> header in package.Value)
                    yield return new(package.Key, header.Key, header.Value);
        }

        /// <inheritdoc/>
        public bool Contains(BurcatHeader item) => Headers.TryGetValue(item.Package, out Dictionary<string, IBurcatObject?>? headers) && headers.ContainsKey(item.Name);

        /// <inheritdoc/>
        public bool IsProperSubsetOf(IEnumerable<BurcatHeader> other)
        {
            HashSet<BurcatHeader> otherHeaders = [.. other];
            return Count < otherHeaders.Count && this.All(otherHeaders.Contains);
        }

        /// <inheritdoc/>
        public bool IsProperSupersetOf(IEnumerable<BurcatHeader> other)
        {
            HashSet<BurcatHeader> otherHeaders = [.. other];
            return Count > otherHeaders.Count && otherHeaders.All(Contains);
        }

        /// <inheritdoc/>
        public bool IsSubsetOf(IEnumerable<BurcatHeader> other)
        {
            HashSet<BurcatHeader> otherHeaders = [.. other];
            return Count <= otherHeaders.Count && this.All(otherHeaders.Contains);
        }

        /// <inheritdoc/>
        public bool IsSupersetOf(IEnumerable<BurcatHeader> other)
        {
            foreach (BurcatHeader header in other)
                if (!Contains(header))
                    return false;

            return true;
        }

        /// <inheritdoc/>
        public bool Overlaps(IEnumerable<BurcatHeader> other)
        {
            foreach (BurcatHeader header in other)
                if (Contains(header))
                    return true;

            return false;
        }

        /// <inheritdoc/>
        public bool SetEquals(IEnumerable<BurcatHeader> other)
        {
            HashSet<BurcatHeader> otherHeaders = [.. other];
            return Count == otherHeaders.Count && this.All(otherHeaders.Contains);
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        void ICollection<BurcatHeader>.Add(BurcatHeader item) => Add(item);

        /// <inheritdoc/>
        BurcatField[] IBurcatObject.GetBurcatFields() => [];
        /// <inheritdoc/>
        void IBurcatObject.SetBurcatFields(BurcatField[] fields) { }

        /// <inheritdoc/>
        IBurcatObject?[] IBurcatObject.GetBurcatConstructionValues() => new BurcatList<BurcatHeader>([.. this]);

        /// <summary>
        /// Represents a protocol header identified by its package and name.
        /// </summary>
        /// <remarks>
        /// Equality and hashing use only <see cref="Package"/> and <see cref="Name"/>;
        /// <see cref="Value"/> does not participate in header identity.
        /// </remarks>
        [BurcatIdentity("00000000-0000-0000-0000-b997facfcadb")]
        public sealed class BurcatHeader : IBurcatObject
        {
            /// <inheritdoc/>
            Guid IBurcatObject.Identifier { get; set => throw new InvalidOperationException(); } = Guid.Empty;
            /// <inheritdoc/>
            Guid IBurcatObject.Revision { get; set => throw new InvalidOperationException(); } = Guid.Empty;

            /// <summary>
            /// Gets the identity of the package that defines the header.
            /// </summary>
            public Guid Package { get; }

            /// <summary>
            /// Gets the header name within its package.
            /// </summary>
            public string Name { get; }

            /// <summary>
            /// Gets the optional protocol value carried by the header.
            /// </summary>
            public IBurcatObject? Value { get; }

            /// <summary>
            /// Initializes a header with its package, name, and optional value.
            /// </summary>
            /// <param name="package">The identity of the package that defines the header.</param>
            /// <param name="name">The header name within the package.</param>
            /// <param name="value">The optional protocol value carried by the header.</param>
            public BurcatHeader(Guid package, string name, IBurcatObject? value = null)
            {
                Package = package;
                Name = name;
                Value = value;
            }

            /// <inheritdoc/>
            public override bool Equals(object? obj)
            {
                if (ReferenceEquals(this, obj)) return true;
                else if (obj is BurcatHeader other) return Package.Equals(other.Package) && Name.Equals(other.Name);
                else return false;
            }
            /// <inheritdoc/>
            public override int GetHashCode() => HashCode.Combine(Package, Name);

            /// <inheritdoc/>
            BurcatField[] IBurcatObject.GetBurcatFields() => [];
            /// <inheritdoc/>
            void IBurcatObject.SetBurcatFields(BurcatField[] fields) { }

            /// <inheritdoc/>
            IBurcatObject?[] IBurcatObject.GetBurcatConstructionValues() => BurcatTranslator.ObjectsTranslate([Package, Name, Value]);
        }
    }
}
