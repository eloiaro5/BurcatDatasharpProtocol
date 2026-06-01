namespace BurcatProtocol
{
    public interface IExternalProvider
    {
        Task<Guid> GetRevision(Guid? streamID, Type objectType, Guid objectID, CancellationToken token);
        Task<IBurcatObject?> GetObject(Guid? streamID, Type objectType, Guid objectID, CancellationToken token);

        Task<BurcatException?> CoupleCache(Guid? streamID, IBurcatObject objectBDP, bool explicitelyRequested, CancellationToken token);
        Task<BurcatException?> DecoupleCache(Guid? streamID, IBurcatObject objectBDP, CancellationToken token);

        Task<ActionResult> ExecuteAction(Guid? streamID, Type objectType, IBurcatObject? objectBDP, string action, object?[]? parameters, CancellationToken token);
    }
}
