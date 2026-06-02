using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace BurcatProtocol.Collections
{
    /// <summary>
    /// Represents a key/value pair as a Burcat protocol value.
    /// </summary>
    /// <typeparam name="TK">The key type.</typeparam>
    /// <typeparam name="TV">The value type.</typeparam>
    [BurcatIdentity("00000000-0000-0000-0000-c0531c59865a")]
    public readonly struct KeyValueDuo<TK, TV> : IBurcatObject where TK : notnull
    {
        /// <inheritdoc/>
        Guid IBurcatObject.Identifier { get; set => throw new InvalidOperationException(); } = Guid.Empty;

        /// <inheritdoc/>
        Guid IBurcatObject.Revision { get; set => throw new InvalidOperationException(); } = Guid.Empty;

        /// <summary>
        /// Gets the pair key.
        /// </summary>
        public TK Key { get; }

        /// <summary>
        /// Gets the pair value.
        /// </summary>
        public TV Value { get; }

        /// <summary>
        /// Initializes a protocol key/value pair.
        /// </summary>
        /// <param name="key">The pair key.</param>
        /// <param name="value">The pair value.</param>
        public KeyValueDuo(TK key, TV value) { Key = key; Value = value; }

        /// <inheritdoc/>
        BurcatField[] IBurcatObject.GetBurcatFields() => [];

        /// <inheritdoc/>
        void IBurcatObject.SetBurcatFields(BurcatField[] fields) { }

        /// <inheritdoc/>
        IBurcatObject?[] IBurcatObject.GetBurcatConstructionValues()
        {
            BurcatType keyType = new(BurcatChat.GetClassIdentity<TK>(), false), valueType = new(BurcatChat.GetClassIdentity<TV>());
            return BurcatTranslator.ObjectsTranslate([keyType, valueType, Key, Value]);
        }

        /// <summary>
        /// Converts a CLR key/value pair to a protocol key/value pair.
        /// </summary>
        /// <param name="pair">The source pair.</param>
        public static implicit operator KeyValueDuo<TK, TV>(KeyValuePair<TK, TV> pair) => new(pair.Key, pair.Value);

        /// <summary>
        /// Converts a protocol key/value pair to a CLR key/value pair.
        /// </summary>
        /// <param name="pair">The source pair.</param>
        public static implicit operator KeyValuePair<TK, TV>(KeyValueDuo<TK, TV> pair) => new(pair.Key, pair.Value);
    }
}
