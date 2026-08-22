using System.Collections;
using System.Reflection;
using System.Runtime.InteropServices;

namespace BurcatProtocol
{
    /// <summary>
    /// Represents an ordered list of GUIDs used as a composite cache key.
    /// </summary>
    public class GuidList : IComparable<GuidList>, IEnumerable<Guid>
    {
        /// <summary>
        /// Builds a composite GUID key from a CLR type.
        /// </summary>
        /// <param name="type">The type to represent.</param>
        /// <returns>The composite GUID key.</returns>
        public static GuidList FromType(Type type) => FromTypes([type]);

        /// <summary>
        /// Builds a composite GUID key from several CLR types.
        /// </summary>
        /// <param name="types">The types to represent.</param>
        /// <returns>The composite GUID key.</returns>
        public static GuidList FromTypes(IEnumerable<Type> types)
        {
            LinkedList<Guid> guids = [];

            foreach (Type type in types)
                foreach (Guid guid in ProcessType(type))
                    guids.AddLast(guid);

            return new([.. guids]);
        }

        private static int ComputeHashCode(Guid[] guids)
        {
            HashCode hash = new();
            hash.Add(guids.Length);
            foreach (Guid guid in guids) hash.Add(guid);
            return hash.ToHashCode();
        }

        private static IEnumerable<Guid> ProcessType(Type type)
        {
            if (type.IsArray && type.GetElementType() is Type elementType)
            {
                yield return type.GUID;
                foreach (Guid guid in ProcessType(elementType)) yield return guid;
            }
            else if (type.IsGenericType)
                foreach (Type genericType in type.GetGenericArguments())
                {
                    yield return type.GUID;
                    foreach (Guid guid in ProcessType(genericType)) yield return guid;
                }
            else yield return type.GUID;
        }

        /// <summary>
        /// Gets an empty composite GUID key.
        /// </summary>
        public static GuidList Empty { get; } = new();

        private readonly Guid[] guids;
        private readonly int hashCode;

        private GuidList() { guids = []; hashCode = 0; }

        /// <summary>
        /// Initializes a composite key with one GUID.
        /// </summary>
        /// <param name="guid">The GUID value.</param>
        public GuidList(Guid guid) : this([guid]) { }

        /// <summary>
        /// Initializes a composite key from GUID values.
        /// </summary>
        /// <param name="guids">The GUID values.</param>
        public GuidList(IEnumerable<Guid> guids) { this.guids = [.. guids]; hashCode = ComputeHashCode(this.guids); }

        /// <summary>
        /// Initializes a composite key joining other composite keys.
        /// </summary>
        /// <param name="guidLists">The composite keys.</param>
        public GuidList(IEnumerable<GuidList> guidLists)
        {
            LinkedList<Guid> guids = [];

            foreach (GuidList guidList in guidLists)
                foreach (Guid guid in guidList)
                    guids.AddLast(guid);

            this.guids = [.. guids];
            hashCode = ComputeHashCode(this.guids);
        }

        /// <summary>
        /// Initializes a composite key from an object's runtime type.
        /// </summary>
        /// <param name="obj">The object whose runtime type is represented.</param>
        public GuidList(object obj)
        {
            LinkedList<Guid> guids = [];

            foreach (Guid guid in ProcessType(obj.GetType()))
                guids.AddLast(guid);

            this.guids = [.. guids];
            hashCode = ComputeHashCode(this.guids);
        }

        /// <inheritdoc/>
        public override bool Equals(object? obj)
        {
            if (obj is null) return false;
            else if (ReferenceEquals(this, obj)) return true;
            else if (obj is GuidList guidList) return CompareTo(guidList) == 0;
            else return false;
        }

        /// <inheritdoc/>
        public override int GetHashCode() => hashCode;

        /// <inheritdoc/>
        public int CompareTo(GuidList? other)
        {
            if (other is null) return 1;
            else if (ReferenceEquals(this, other)) return 0;
            else if (guids.Length < other.guids.Length) return -1;
            else if (guids.Length > other.guids.Length) return 1;
            else
            {
                for (int i = 0; i < guids.Length; i++)
                {
                    int guidComparation = guids[i].CompareTo(other.guids[i]);
                    if (guidComparation != 0) return guidComparation;
                }

                return 0;
            }
        }

        /// <inheritdoc/>
        public IEnumerator<Guid> GetEnumerator()
        {
            foreach (Guid guid in guids)
                yield return guid;
        }

        /// <inheritdoc/>
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
