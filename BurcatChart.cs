using System.Runtime.InteropServices;

namespace BurcatProtocol
{
    [BurcatIdentity("00000000-0000-0000-0000-93425ec592b2")]
    public sealed class NothingChart : IBurcatObject
    {
        Guid IBurcatObject.Identifier { get; set => throw new InvalidOperationException(); } = Guid.Empty;

        public static NothingChart Instance { get; } = new();

        BurcatField[] IBurcatObject.GetBurcatFields() => [];
        bool IBurcatObject.SetBurcatField(BurcatField field) => false;

        IBurcatObject?[] IBurcatObject.GetBurcatConstructionValues() => [];
    }

    [BurcatIdentity("00000000-0000-0000-0000-70080ee0a69c")]
    public sealed class PingChart : IBurcatObject
    {
        Guid IBurcatObject.Identifier { get; set => throw new InvalidOperationException(); } = Guid.Empty;

        public static PingChart Instance { get; } = new();

        BurcatField[] IBurcatObject.GetBurcatFields() => [];
        bool IBurcatObject.SetBurcatField(BurcatField field) => false;

        IBurcatObject?[] IBurcatObject.GetBurcatConstructionValues() => [];
    }

    [BurcatIdentity("00000000-0000-0000-0000-74128b765b52")]
    public sealed class PurgeChart : IBurcatObject
    {
        Guid IBurcatObject.Identifier { get; set => throw new InvalidOperationException(); } = Guid.Empty;

        public static PurgeChart Instance { get; } = new();

        BurcatField[] IBurcatObject.GetBurcatFields() => [];
        bool IBurcatObject.SetBurcatField(BurcatField field) => false;

        IBurcatObject?[] IBurcatObject.GetBurcatConstructionValues() => [];
    }

    [BurcatIdentity("00000000-0000-0000-0000-3674efed6bed")]
    public sealed class EndOfCommunicationChart : IBurcatObject
    {
        Guid IBurcatObject.Identifier { get; set => throw new InvalidOperationException(); } = Guid.Empty;

        public static EndOfCommunicationChart Instance { get; } = new();

        BurcatField[] IBurcatObject.GetBurcatFields() => [];
        bool IBurcatObject.SetBurcatField(BurcatField field) => false;

        IBurcatObject?[] IBurcatObject.GetBurcatConstructionValues() => [];
    }
}
