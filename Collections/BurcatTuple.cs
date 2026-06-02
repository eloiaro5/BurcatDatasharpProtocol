using System;
using System.Collections.Generic;
using System.Text;

namespace BurcatProtocol.Collections
{
    /// <summary>
    /// Represents a two-value tuple as a Burcat protocol value.
    /// </summary>
    /// <typeparam name="T1">The first value type.</typeparam>
    /// <typeparam name="T2">The second value type.</typeparam>
    [BurcatIdentity("00000000-0000-0000-0000-c0531c59865a")]
    public readonly struct BurcatTuple<T1, T2> : IBurcatObject
    {
        /// <inheritdoc/>
        Guid IBurcatObject.Identifier { get; set => throw new InvalidOperationException(); } = Guid.Empty;

        /// <inheritdoc/>
        Guid IBurcatObject.Revision { get; set => throw new InvalidOperationException(); } = Guid.Empty;

        /// <summary>
        /// Gets the first tuple value.
        /// </summary>
        public T1 Value1 { get; }

        /// <summary>
        /// Gets the second tuple value.
        /// </summary>
        public T2 Value2 { get; }

        /// <summary>
        /// Initializes a protocol tuple.
        /// </summary>
        /// <param name="value1">The first value.</param>
        /// <param name="value2">The second value.</param>
        public BurcatTuple(T1 value1, T2 value2) { Value1 = value1; Value2 = value2; }

        /// <inheritdoc/>
        BurcatField[] IBurcatObject.GetBurcatFields() => [];

        /// <inheritdoc/>
        void IBurcatObject.SetBurcatFields(BurcatField[] fields) { }

        /// <inheritdoc/>
        IBurcatObject?[] IBurcatObject.GetBurcatConstructionValues()
        {
            BurcatType value1Type = new(BurcatChat.GetClassIdentity<T1>()), value2Type = new(BurcatChat.GetClassIdentity<T2>());
            return BurcatTranslator.ObjectsTranslate([value1Type, value2Type, Value1, Value2]);
        }
    }
}
