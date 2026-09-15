using System.Runtime.InteropServices;

namespace BurcatProtocol
{
    public abstract class BurcatChart : BurcatObject
    {
        public abstract BurcatChart Acknowledge();

        public override sealed BurcatField[] GetBurcatFields() => [];
        public override sealed void SetBurcatFields(BurcatField[] fields) { }
    }

    /// <summary>
    /// Represents a protocol ping message.
    /// </summary>
    [BurcatIdentity("00000000-0000-0000-0000-70080ee0a69c")]
    public sealed class PingChart : BurcatChart
    {
        public static PingChart Instance { get; } = new();
        public override BurcatChart Acknowledge() => Instance;

        public override object?[] GetBurcatConstructionValues() => [];
    }

    /// <summary>
    /// Represents a protocol marker that ends communication.
    /// </summary>
    [BurcatIdentity("00000000-0000-0000-0000-3674efed6bed")]
    public abstract class EndOfCommunicationChart : BurcatChart { }
}
