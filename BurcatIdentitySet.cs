using BurcatProtocol.Collections;
using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace BurcatProtocol
{
    /// <summary>
    /// Represents the Burcat identities and corresponding CLR types accepted by an application.
    /// </summary>
    [BurcatIdentity("00000000-0000-0000-0000-69c154a0705d")]
    public sealed class BurcatIdentitySet : IBurcatObject, ISet<BurcatIdentity>
    {
        private SortedDictionary<Guid, Type> Identities { get; } = [];

        /// <inheritdoc/>
        Guid IBurcatObject.Identifier { get; set => throw new InvalidOperationException(); } = Guid.Empty;
        /// <inheritdoc/>
        Guid IBurcatObject.Revision { get; set => throw new InvalidOperationException(); } = Guid.Empty;

        /// <inheritdoc/>
        public int Count => Identities.Count;
        /// <inheritdoc/>
        public bool IsReadOnly => false;

        /// <summary>
        /// Gets the registered CLR types.
        /// </summary>
        public IEnumerable<Type> Types => Identities.Values;

        /// <summary>
        /// Initializes an empty identity collection.
        /// </summary>
        public BurcatIdentitySet() { }

        /// <summary>
        /// Initializes an identity collection from identity and type pairs.
        /// </summary>
        /// <param name="identities">The identities to register.</param>
        public BurcatIdentitySet(IEnumerable<BurcatIdentity> identities)
        {
            foreach (BurcatIdentity identity in identities)
                Add(identity);
        }

        /// <summary>
        /// Registers an identity and its corresponding CLR type.
        /// </summary>
        /// <param name="identity">The identity to register.</param>
        /// <returns><see langword="true"/> when the identity was registered; otherwise, <see langword="false"/>.</returns>
        public bool Add(BurcatIdentity identity) => Identities.TryAdd(identity.Guid, identity.Type);

        /// <summary>
        /// Registers a CLR type when it has a Burcat identity or translator.
        /// </summary>
        /// <param name="type">The CLR type to register.</param>
        /// <returns><see langword="true"/> when the type was registered; otherwise, <see langword="false"/>.</returns>
        public bool Add(Type type)
        {
            if (BurcatChat.TryGetClassIdentity(type, out Guid identity)) return Add(new BurcatIdentity(identity, type));
            else return false;
        }

        /// <summary>
        /// Registers all Burcat-identifiable types from an assembly.
        /// </summary>
        /// <param name="assembly">The assembly to scan.</param>
        public void Add(Assembly assembly)
        {
            foreach (Type type in assembly.GetTypes())
                Add(type);
        }

        /// <summary>
        /// Registers all Burcat-identifiable types from assemblies loaded in the current application domain.
        /// </summary>
        public void AddLoadedAssemblies()
        {
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
                Add(assembly);
        }

        /// <summary>
        /// Removes a registered identity.
        /// </summary>
        /// <param name="identity">The identity to remove.</param>
        /// <returns><see langword="true"/> when the identity was removed; otherwise, <see langword="false"/>.</returns>
        public bool Remove(BurcatIdentity identity) => Identities.Remove(identity.Guid);

        /// <summary>
        /// Removes the identity associated with a CLR type.
        /// </summary>
        /// <param name="type">The CLR type whose identity should be removed.</param>
        /// <returns><see langword="true"/> when the identity was removed; otherwise, <see langword="false"/>.</returns>
        public bool Remove(Type type)
        {
            if (BurcatChat.TryGetClassIdentity(type, out Guid identity)) return Identities.Remove(identity);
            else return false;
        }

        /// <summary>
        /// Removes the identities of all Burcat-identifiable types in an assembly.
        /// </summary>
        /// <param name="assembly">The assembly whose identities should be removed.</param>
        public void Remove(Assembly assembly)
        {
            foreach (Type type in assembly.GetTypes())
                Remove(type);
        }

        /// <summary>
        /// Removes the identities of all Burcat-identifiable types from assemblies loaded in the current application domain.
        /// </summary>
        public void RemoveLoadedAssemblies()
        {
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
                Remove(assembly);
        }

        /// <inheritdoc/>
        public void Clear() => Identities.Clear();

        /// <inheritdoc/>
        public void CopyTo(BurcatIdentity[] array, int arrayIndex)
        {
            ArgumentNullException.ThrowIfNull(array);
            ArgumentOutOfRangeException.ThrowIfNegative(arrayIndex);
            if (arrayIndex > array.Length || array.Length - arrayIndex < Count) throw new ArgumentException("The destination array does not have enough available space.", nameof(array));

            foreach (BurcatIdentity identity in this)
                array[arrayIndex++] = identity;
        }

        /// <inheritdoc/>
        public void UnionWith(IEnumerable<BurcatIdentity> other)
        {
            foreach (BurcatIdentity identity in other)
                Add(identity);
        }

        /// <inheritdoc/>
        public void IntersectWith(IEnumerable<BurcatIdentity> other)
        {
            HashSet<BurcatIdentity> otherIdentities = [.. other];
            BurcatIdentity[] removedIdentities = [.. this.Where(identity => !otherIdentities.Contains(identity))];
            foreach (BurcatIdentity identity in removedIdentities)
                Remove(identity);
        }

        /// <inheritdoc/>
        public void ExceptWith(IEnumerable<BurcatIdentity> other)
        {
            if (ReferenceEquals(this, other)) Clear();
            else
                foreach (BurcatIdentity identity in other)
                    Remove(identity);
        }

        /// <inheritdoc/>
        public void SymmetricExceptWith(IEnumerable<BurcatIdentity> other)
        {
            if (ReferenceEquals(this, other)) Clear();
            else
                foreach (BurcatIdentity identity in new HashSet<BurcatIdentity>(other))
                    if (!Remove(identity))
                        Add(identity);
        }

        /// <inheritdoc/>
        public bool Contains(BurcatIdentity item) => Identities.ContainsKey(item.Guid);

        /// <summary>
        /// Determines whether an identity is registered.
        /// </summary>
        /// <param name="identity">The identity to find.</param>
        /// <returns><see langword="true"/> when the identity is registered; otherwise, <see langword="false"/>.</returns>
        public bool Contains(Guid identity) => Identities.ContainsKey(identity);

        /// <summary>
        /// Determines whether a compatible type is registered under the type's Burcat identity.
        /// </summary>
        /// <param name="type">The CLR type to test.</param>
        /// <returns><see langword="true"/> when a compatible type is registered; otherwise, <see langword="false"/>.</returns>
        public bool Contains(Type type)
        {
            if (BurcatChat.TryGetClassIdentity(type, out Guid identity) && Identities.TryGetValue(identity, out Type? accepted)) return accepted.IsAssignableFrom(type);
            else return false;
        }

        /// <summary>
        /// Determines whether every Burcat-identifiable type in an assembly is registered.
        /// </summary>
        /// <param name="assembly">The assembly to test.</param>
        /// <returns><see langword="true"/> when every identifiable type is registered; otherwise, <see langword="false"/>.</returns>
        public bool Contains(Assembly assembly)
        {
            foreach (Type type in assembly.GetTypes())
                if (BurcatChat.TryGetClassIdentity(type, out _) && !Contains(type))
                    return false;

            return true;
        }

        /// <summary>
        /// Tries to resolve the CLR type registered for an identity.
        /// </summary>
        /// <param name="identity">The identity to resolve.</param>
        /// <param name="type">The registered CLR type when found.</param>
        /// <returns><see langword="true"/> when the identity was found; otherwise, <see langword="false"/>.</returns>
        public bool TryGetType(Guid identity, [MaybeNullWhen(false)] out Type type) => Identities.TryGetValue(identity, out type);

        /// <summary>
        /// Gets the CLR type registered for an identity.
        /// </summary>
        /// <param name="identity">The identity to resolve.</param>
        /// <returns>The registered CLR type.</returns>
        /// <exception cref="InvalidOperationException">No type is registered for the identity.</exception>
        public Type GetType(Guid identity)
        {
            if (TryGetType(identity, out Type? type)) return type;
            else throw new InvalidOperationException("There's no type with the specified identifier.");
        }

        /// <inheritdoc/>
        public bool IsProperSubsetOf(IEnumerable<BurcatIdentity> other)
        {
            HashSet<BurcatIdentity> otherIdentities = [.. other];
            return Count < otherIdentities.Count && this.All(otherIdentities.Contains);
        }

        /// <inheritdoc/>
        public bool IsProperSupersetOf(IEnumerable<BurcatIdentity> other)
        {
            HashSet<BurcatIdentity> otherIdentities = [.. other];
            return Count > otherIdentities.Count && otherIdentities.All(Contains);
        }

        /// <inheritdoc/>
        public bool IsSubsetOf(IEnumerable<BurcatIdentity> other)
        {
            HashSet<BurcatIdentity> otherIdentities = [.. other];
            return Count <= otherIdentities.Count && this.All(otherIdentities.Contains);
        }

        /// <inheritdoc/>
        public bool IsSupersetOf(IEnumerable<BurcatIdentity> other) => other.All(Contains);

        /// <inheritdoc/>
        public bool Overlaps(IEnumerable<BurcatIdentity> other) => other.Any(Contains);

        /// <inheritdoc/>
        public bool SetEquals(IEnumerable<BurcatIdentity> other)
        {
            HashSet<BurcatIdentity> otherIdentities = [.. other];
            return Count == otherIdentities.Count && this.All(otherIdentities.Contains);
        }

        /// <inheritdoc/>
        public IEnumerator<BurcatIdentity> GetEnumerator()
        {
            foreach (KeyValuePair<Guid, Type> identity in Identities)
                yield return new(identity.Key, identity.Value);
        }

        /// <inheritdoc/>
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        /// <inheritdoc/>
        void ICollection<BurcatIdentity>.Add(BurcatIdentity item) => Add(item);

        /// <inheritdoc/>
        BurcatField[] IBurcatObject.GetBurcatFields() => [];
        /// <inheritdoc/>
        void IBurcatObject.SetBurcatFields(BurcatField[] fields) { }

        /// <inheritdoc/>
        IBurcatObject?[] IBurcatObject.GetBurcatConstructionValues() => new BurcatList<BurcatIdentity>([.. this]);
    }
}
