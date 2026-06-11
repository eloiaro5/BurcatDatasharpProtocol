namespace BurcatProtocol
{
    /// <summary>
    /// Carries a Burcat object type together with an optional object value.
    /// </summary>
    public sealed class BurcatInstance
    {
        /// <summary>
        /// Builds an instance wrapper for a typed Burcat object value.
        /// </summary>
        /// <typeparam name="T">The Burcat object type.</typeparam>
        /// <param name="instance">The object value, or <see langword="null"/>.</param>
        /// <returns>A Burcat instance wrapper.</returns>
        public static BurcatInstance Build<T>(T? instance = default) where T : IBurcatObject => new(typeof(T), instance);

        /// <summary>
        /// Builds an instance wrapper for a type unable to be constructed.
        /// </summary>
        /// <param name="type">The type to be sent.</param>
        /// <returns>A Burcat instance wrapper.</returns>
        public static BurcatInstance Build(Type type)
        {
            if (BurcatChat.AcceptsClass(type)) return new(type);
            else throw new InvalidOperationException("The building type is not part of the accepted classes.");
        }

        /// <summary>
        /// Gets the Burcat object type.
        /// </summary>
        public Type Type { get; }

        /// <summary>
        /// Gets the optional object value.
        /// </summary>
        public IBurcatObject? Value { get; }

        /// <summary>
        /// Initializes a Burcat instance wrapper.
        /// </summary>
        /// <param name="type">The Burcat object type.</param>
        /// <param name="value">The optional object value.</param>
        internal BurcatInstance(Type type, IBurcatObject? value = null) { Type = type; Value = value; }

        /// <summary>
        /// Initializes a Burcat instance wrapper from an object value.
        /// </summary>
        /// <param name="value">The object value.</param>
        public BurcatInstance(IBurcatObject value) : this(value.GetType(), value) { }

        /// <summary>
        /// Gets the wrapped value as the requested type.
        /// </summary>
        /// <typeparam name="T">The requested value type.</typeparam>
        /// <returns>The wrapped value.</returns>
        /// <exception cref="InvalidCastException">Thrown when the requested type is not compatible with <see cref="Type"/>.</exception>
        public T ForceValue<T>()
        {
            if (typeof(T).IsAssignableFrom(Type)) return (T)Value!;
            else throw new InvalidCastException();
        }
    }
}
