using System;
using System.Collections.Generic;

namespace BurcatProtocol
{
    /// <summary>
    /// Represents named ambient values associated with a Burcat operation.
    /// </summary>
    /// <remarks>
    /// Context keys use ordinal, case-sensitive comparison by default so their
    /// meaning remains stable across cultures and communicating applications.
    /// </remarks>
    public class BurcatContext : Dictionary<string, object?>
    {
        /// <summary>
        /// Initializes an empty context using ordinal key comparison.
        /// </summary>
        public BurcatContext() : base(StringComparer.Ordinal) { }

        /// <summary>
        /// Initializes an empty context with space for the specified number of values.
        /// </summary>
        /// <param name="capacity">The initial number of values the context can hold.</param>
        public BurcatContext(int capacity) : base(capacity, StringComparer.Ordinal) { }

        /// <summary>
        /// Initializes an empty context with the specified key comparer.
        /// </summary>
        /// <param name="comparer">The comparer used to compare context keys.</param>
        public BurcatContext(IEqualityComparer<string>? comparer) : base(comparer) { }

        /// <summary>
        /// Initializes an empty context with the specified capacity and key comparer.
        /// </summary>
        /// <param name="capacity">The initial number of values the context can hold.</param>
        /// <param name="comparer">The comparer used to compare context keys.</param>
        public BurcatContext(int capacity, IEqualityComparer<string>? comparer) : base(capacity, comparer) { }

        /// <summary>
        /// Initializes a context from named values using ordinal key comparison.
        /// </summary>
        /// <param name="values">The values to copy into the context.</param>
        public BurcatContext(IEnumerable<KeyValuePair<string, object?>> values) : base(values, StringComparer.Ordinal) { }

        /// <summary>
        /// Initializes a context from named values with the specified key comparer.
        /// </summary>
        /// <param name="values">The values to copy into the context.</param>
        /// <param name="comparer">The comparer used to compare context keys.</param>
        public BurcatContext(IEnumerable<KeyValuePair<string, object?>> values, IEqualityComparer<string>? comparer) : base(values, comparer) { }
    }
}
