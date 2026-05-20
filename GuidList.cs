namespace BurcatProtocol
{
    public class GuidList : IComparable<GuidList>
    {
        public static GuidList FromType(Type type) => FromTypes([type]);
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

        private readonly Guid[] guids;
        private readonly int hashCode;

        public GuidList() { guids = []; }

        public GuidList(Guid guid) : this([guid]) { }
        public GuidList(Guid[] guids) { this.guids = guids; hashCode = ComputeHashCode(guids); }

        public GuidList(IBurcatObject obj)
        {
            LinkedList<Guid> guids = [];

            foreach (Guid guid in ProcessType(obj.GetType()))
                guids.AddLast(guid);

            this.guids = [.. guids];
            hashCode = ComputeHashCode(this.guids);
        }

        public override bool Equals(object? obj)
        {
            if (obj is null) return false;
            else if (ReferenceEquals(this, obj)) return true;
            else if (obj is GuidList guidList) return CompareTo(guidList) == 0;
            else return false;
        }
        public override int GetHashCode() => hashCode;

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
    }
}
