namespace BurcatProtocol
{
    public interface IExternalProvider
    {
        Task<IBurcatObject?> GetObject(Guid? streamID, Type type, Guid objectID, CancellationToken token);
        Task<BurcatException?> CreateObject(Guid? streamID, IBurcatObject objectBDP, CancellationToken token);
        Task<BurcatException?> UpdateObject(Guid? streamID, Type objectType, Guid? objectID, BurcatField field, CancellationToken token);
        Task<BurcatException?> DestroyObject(Guid? streamID, Type objectType, Guid objectID, CancellationToken token);
        Task<ActionResult> ExecuteAction(Guid? streamID, Type objectType, IBurcatObject? objectBDP, string action, IBurcatObject?[] parameters, CancellationToken token);
    }
}
