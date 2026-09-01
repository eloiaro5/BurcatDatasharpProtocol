namespace BurcatProtocol
{
    /// <summary>
    /// Represents the protocol value used when a translated object value is null.
    /// </summary>
    [BurcatIdentity("00000000-0000-0000-0000-93425ec592b2")]
    public sealed class NothingInstance : IBurcatObject
    {
        /// <inheritdoc/>
        Guid IBurcatObject.Identifier { get; set => throw new InvalidOperationException(); } = Guid.Empty;

        /// <inheritdoc/>
        Guid IBurcatObject.Revision { get; set => throw new InvalidOperationException(); } = Guid.Empty;

        /// <summary>
        /// Gets the singleton instance.
        /// </summary>
        public static NothingInstance Instance { get; } = new();

        /// <inheritdoc/>
        BurcatField[] IBurcatObject.GetBurcatFields() => [];
        /// <inheritdoc/>
        void IBurcatObject.SetBurcatFields(BurcatField[] fields) { }

        /// <inheritdoc/>
        IBurcatObject?[] IBurcatObject.GetBurcatConstructionValues() => [];
    }
}
