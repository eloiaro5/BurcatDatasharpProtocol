using BurcatProtocol.Collections;
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
    public class BurcatContext : ListHashDictionary<string, object?>
    {
        public BurcatContext() { }

        public BurcatContext(Guid identifier) : base(identifier) { }

        public BurcatContext(IEnumerable<KeyValueDuo<string, object?>> values) : base(values) { }

        public BurcatContext(IEqualityComparer<string> comparer) : base(comparer) { }

        public BurcatContext(Guid identifier, IEnumerable<KeyValueDuo<string, object?>> values) : base(identifier, values) { }

        public BurcatContext(Guid identifier, IEqualityComparer<string> comparer) : base(identifier, comparer) { }

        public BurcatContext(IEnumerable<KeyValueDuo<string, object?>> values, IEqualityComparer<string> comparer) : base(values, comparer) { }

        public BurcatContext(Guid identifier, IEnumerable<KeyValueDuo<string, object?>> values, IEqualityComparer<string> comparer) : base(identifier, values, comparer) { }
    }
}
