using System.Runtime.InteropServices;

namespace BurcatProtocol
{
    /// <summary>
    /// Represents the protocol value used when a translated object value is null.
    /// </summary>
    [BurcatIdentity("00000000-0000-0000-0000-93425ec592b2")]
    public sealed class NothingChart : IBurcatObject
    {
        /// <inheritdoc/>
        Guid IBurcatObject.Identifier { get; set => throw new InvalidOperationException(); } = Guid.Empty;

        /// <inheritdoc/>
        Guid IBurcatObject.Revision { get; set => throw new InvalidOperationException(); } = Guid.Empty;

        /// <summary>
        /// Gets the singleton instance.
        /// </summary>
        public static NothingChart Instance { get; } = new();

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
    public sealed class PingChart : IBurcatObject
    {
        /// <inheritdoc/>
        Guid IBurcatObject.Identifier { get; set => throw new InvalidOperationException(); } = Guid.Empty;

        /// <inheritdoc/>
        Guid IBurcatObject.Revision { get; set => throw new InvalidOperationException(); } = Guid.Empty;

        /// <summary>
        /// Gets the singleton instance.
        /// </summary>
        public static PingChart Instance { get; } = new();

        /// <inheritdoc/>
        BurcatField[] IBurcatObject.GetBurcatFields() => [];

        /// <inheritdoc/>
        void IBurcatObject.SetBurcatFields(BurcatField[] fields) { }

        /// <inheritdoc/>
        IBurcatObject?[] IBurcatObject.GetBurcatConstructionValues() => [];
    }

    /// <summary>
    /// Represents a notification that the stream is being purged to the next protocol boundary.
    /// </summary>
    [BurcatIdentity("00000000-0000-0000-0000-74128b765b52")]
    public sealed class PurgeChart : IBurcatObject
    {
        /// <inheritdoc/>
        Guid IBurcatObject.Identifier { get; set => throw new InvalidOperationException(); } = Guid.Empty;

        /// <inheritdoc/>
        Guid IBurcatObject.Revision { get; set => throw new InvalidOperationException(); } = Guid.Empty;

        /// <summary>
        /// Gets the singleton instance.
        /// </summary>
        public static PurgeChart Instance { get; } = new();

        /// <inheritdoc/>
        BurcatField[] IBurcatObject.GetBurcatFields() => [];

        /// <inheritdoc/>
        void IBurcatObject.SetBurcatFields(BurcatField[] fields) { }

        /// <inheritdoc/>
        IBurcatObject?[] IBurcatObject.GetBurcatConstructionValues() => [];
    }

    /// <summary>
    /// Represents a protocol marker that ends communication.
    /// </summary>
    [BurcatIdentity("00000000-0000-0000-0000-3674efed6bed")]
    public sealed class EndOfCommunicationChart : IBurcatObject
    {
        /// <inheritdoc/>
        Guid IBurcatObject.Identifier { get; set => throw new InvalidOperationException(); } = Guid.Empty;

        /// <inheritdoc/>
        Guid IBurcatObject.Revision { get; set => throw new InvalidOperationException(); } = Guid.Empty;

        /// <summary>
        /// Gets the singleton instance.
        /// </summary>
        public static EndOfCommunicationChart Instance { get; } = new();

        /// <inheritdoc/>
        BurcatField[] IBurcatObject.GetBurcatFields() => [];

        /// <inheritdoc/>
        void IBurcatObject.SetBurcatFields(BurcatField[] fields) { }

        /// <inheritdoc/>
        IBurcatObject?[] IBurcatObject.GetBurcatConstructionValues() => [];
    }
}
