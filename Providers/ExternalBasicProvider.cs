using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace BurcatProtocol.Providers
{
    public sealed class ExternalBasicProvider : IExternalProvider
    {
        private static SortedDictionary<Guid, Delegate> UpdateDelegates { get; } = [];
        private static SortedDictionary<Guid, Delegate> ActionDelegates { get; } = [];

        private static Delegate GetActionDelegate(Type objectType)
        {
            Guid classID = BurcatChat.GetClassIdentity(objectType);
            if (ActionDelegates.TryGetValue(classID, out Delegate? d)) return d;
            else
            {
                Delegate bdpDelegate = Delegate.CreateDelegate(typeof(Func<>).MakeGenericType(typeof(IdentifiedStream), objectType, typeof(Type[]), typeof(IBurcatObject[]), typeof(CancellationToken?)), typeof(BurcatChat).GetMethod(nameof(BurcatChat.SendActionAsync), BindingFlags.Public | BindingFlags.Static, [typeof(IdentifiedStream), typeof(IBurcatObject), typeof(Type[]), typeof(IBurcatObject[]), typeof(CancellationToken?)])!.MakeGenericMethod(objectType));
                Delegate newDelegate = new Func<IdentifiedStream, IBurcatObject?, Type[], IBurcatObject?[], CancellationToken, Task<ActionResult>>((stream, objectBDP, genericTypes, parameters, token) => (Task<ActionResult>)bdpDelegate.DynamicInvoke(stream, objectBDP, genericTypes, parameters, token)!);
                ActionDelegates.Add(classID, newDelegate);

                return newDelegate;
            }
        }

        public IdentifiedStream Stream { get; }

        public ExternalBasicProvider(IdentifiedStream stream) { Stream = stream; }

        public async Task<IBurcatObject?> GetObject(Guid? streamID, Type type, Guid identifier, CancellationToken token) => await BurcatChat.SendRequestAsync(Stream, BurcatChat.GetClassIdentity(type), identifier, token);
        public async Task<BurcatException?> CreateObject(Guid? streamID, IBurcatObject objectBDP, CancellationToken token) => await BurcatChat.SendConstructAsync(Stream, objectBDP, token);
        public Task<BurcatException?> UpdateObject(Guid? streamID, Type objectType, Guid? objectID, BurcatField field, CancellationToken token) => BurcatChat.SendUpdateAsync(Stream, BurcatChat.GetClassIdentity(objectType), objectID, field, token);
        public async Task<BurcatException?> DestroyObject(Guid? streamID, Type objectType, Guid objectID, CancellationToken token) => await BurcatChat.SendDestructAsync(Stream, BurcatChat.GetClassIdentity(objectType), objectID, token);
        public Task<ActionResult> ExecuteAction(Guid? streamID, Type objectType, IBurcatObject? objectBDP, string action, IBurcatObject?[] parameters, CancellationToken token) => (Task<ActionResult>)GetActionDelegate(objectType).DynamicInvoke(Stream, objectBDP, parameters, token)!;
    }
}
