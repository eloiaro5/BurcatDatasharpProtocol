using System.Runtime.InteropServices;

namespace BurcatProtocol
{
    public abstract class BurcatChart : IBurcatObject
    {
        /// <inheritdoc/>
        Guid IBurcatObject.Identifier { get; set => throw new InvalidOperationException(); } = Guid.Empty;
        /// <inheritdoc/>
        Guid IBurcatObject.Revision { get; set => throw new InvalidOperationException(); } = Guid.Empty;

        public static void Acknowledge() => throw new NotImplementedException();

        /// <inheritdoc/>
        BurcatField[] IBurcatObject.GetBurcatFields() => [];
        /// <inheritdoc/>
        void IBurcatObject.SetBurcatFields(BurcatField[] fields) { }

        /// <inheritdoc/>
        IBurcatObject?[] IBurcatObject.GetBurcatConstructionValues() => [];
    }

    /// <summary>
    /// Represents a protocol ping message.
    /// </summary>
    [BurcatIdentity("00000000-0000-0000-0000-70080ee0a69c")]
    public sealed class PingChart : BurcatChart { }

    /// <summary>
    /// Represents a protocol marker that ends communication.
    /// </summary>
    [BurcatIdentity("00000000-0000-0000-0000-3674efed6bed")]
    public sealed class EndOfCommunicationChart : BurcatChart { }
}
