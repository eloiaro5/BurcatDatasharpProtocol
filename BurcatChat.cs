using BurcatProtocol.Providers;
using System.Collections.Concurrent;
using System.Data;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace BurcatProtocol
{
    public static class BurcatChat
    {
        public static TimeSpan DefaultTimeOut { get; set; } = new(TimeSpan.TicksPerSecond * 5);
        public static bool ControlAsyncAccess { get; set; } = true;
        public static bool IncludeStackTraceOnException { get; set; } = false;

        public static bool TryGetClassIdentity(Type type, out Guid identity)
        {
            if (type.GetCustomAttribute<BurcatIdentityAttribute>() is BurcatIdentityAttribute identityAttribute) { identity = identityAttribute.Identity; return true; }
            else if (BurcatTranslator.CanTranslate(type, out identity)) return true;
            else { identity = Guid.Empty; return false; }
        }
        public static bool TryGetClassIdentity<T>(out Guid identifier) => TryGetClassIdentity(typeof(T), out identifier);
        public static bool TryGetClassIdentity(object objectBDP, out Guid identifier) => TryGetClassIdentity(objectBDP.GetType(), out identifier);
        public static Guid GetClassIdentity(Type type)
        {
            if (TryGetClassIdentity(type, out Guid identifier)) return identifier;
            else throw new InvalidOperationException($"To get the BDP class identity, a class needs to be a BDP object and implement {nameof(BurcatIdentityAttribute)}.");
        }
        public static Guid GetClassIdentity<T>() => GetClassIdentity(typeof(T));
        public static Guid GetClassIdentity(object objectBDP) => GetClassIdentity(objectBDP.GetType());

        private static SortedDictionary<Guid, Type> AcceptedClasses { get; } = [];
        public static bool AcceptClass(Type type)
        {
            if (TryGetClassIdentity(type, out Guid identity)) return AcceptedClasses.TryAdd(identity, type);
            else return false;
        }
        public static void AcceptClasses(Assembly assembly)
        {
            foreach (Type type in assembly.GetTypes())
                AcceptClass(type);
        }
        public static void AcceptClasses()
        {
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
                AcceptClasses(assembly);
        }

        public static bool AcceptsClass(Guid identity)
        {
            if (!AcceptedClasses.ContainsKey(GetClassIdentity<NothingChart>())) AcceptClasses(typeof(BurcatChat).Assembly);
            return AcceptedClasses.ContainsKey(identity);
        }
        public static bool AcceptsClass(Type type)
        {
            if (AcceptedClasses.TryGetValue(GetClassIdentity(type), out Type? accepted)) return accepted.IsAssignableFrom(type);
            else return false;
        }
        public static bool AcceptsClass<T>() => AcceptsClass(typeof(T));
        public static bool AcceptsClasses(Assembly assembly)
        {
            foreach (Type type in assembly.GetTypes())
                if (!AcceptsClass(type)) return false;

            return true;
        }

        public static bool TryGetType(Guid classID, [MaybeNullWhen(false)] out Type type) => AcceptedClasses.TryGetValue(classID, out type);
        public static Type GetType(Guid classID)
        {
            if (TryGetType(classID, out Type? type)) return type;
            else throw new InvalidOperationException($"There's no type with the specified identifier.");
        }
        public static IEnumerable<Type> GetAcceptedClasses() => AcceptedClasses.Values;

        private static ConcurrentDictionary<Guid, SemaphoreSlim> Semaphores { get; } = [];
        public static async Task PurgeAsync(IdentifiedStream stream, CancellationToken? token = null)
        {
            CancellationToken cancellation = token ?? new CancellationTokenSource(DefaultTimeOut).Token;
            if (ControlAsyncAccess) if (ControlAsyncAccess) await Semaphores.GetOrAdd(stream.Identifier, new SemaphoreSlim(1, 1)).WaitAsync(cancellation);

            try
            {
                Guid[] endings = [
                    GetClassIdentity<EndObjectSchematic>(),
                                GetClassIdentity<EndRequestSchematic>(),
                                GetClassIdentity<EndConstructSchematic>(),
                                GetClassIdentity<EndUpdateSchematic>(),
                                GetClassIdentity<EndDestructSchematic>(),
                                GetClassIdentity<EndActionSchematic>()
                ];

                byte[] data = new byte[16];
                await stream.ReadExactlyAsync(data, cancellation);
                cancellation.ThrowIfCancellationRequested();

                Guid actual = new(data);
                while (!endings.Contains(actual))
                {
                    byte[] d = new byte[1];
                    await stream.ReadExactlyAsync(d, cancellation);
                    cancellation.ThrowIfCancellationRequested();

                    for (int i = 14; i >= 0; i--) data[i] = data[i + 1];
                    data[15] = d[0];
                    actual = new(data);

                    cancellation.ThrowIfCancellationRequested();
                }
            }
            finally { if (Semaphores.TryGetValue(stream.Identifier, out SemaphoreSlim? semaphore)) semaphore.Release(); }
        }
        public static void Purge(IdentifiedStream stream, CancellationToken? token = null) => PurgeAsync(stream, token).GetAwaiter().GetResult();

        public async static Task SendAsync(IdentifiedStream stream, BurcatInstance instance, CancellationToken? token = null)
        {
            try
            {
                CancellationToken cancellation = token ?? new CancellationTokenSource(DefaultTimeOut).Token;
                if (ControlAsyncAccess) await Semaphores.GetOrAdd(stream.Identifier, new SemaphoreSlim(1, 1)).WaitAsync(cancellation);

                await stream.WriteAsync(GetClassIdentity<BeginObjectSchematic>().ToByteArray(), cancellation);
                cancellation.ThrowIfCancellationRequested();

                await stream.WriteAsync(GetClassIdentity<StreamScheme>().ToByteArray(), cancellation);
                cancellation.ThrowIfCancellationRequested();

                await stream.WriteAsync(stream.Identifier.ToByteArray(), cancellation);
                cancellation.ThrowIfCancellationRequested();

                await stream.WriteAsync(GetClassIdentity<StreamScheme>().ToByteArray(), cancellation);
                cancellation.ThrowIfCancellationRequested();

                await SendObject(stream, instance, cancellation);
                cancellation.ThrowIfCancellationRequested();

                await stream.WriteAsync(GetClassIdentity<EndObjectSchematic>().ToByteArray(), cancellation);
                cancellation.ThrowIfCancellationRequested();
            }
            finally { if (Semaphores.TryGetValue(stream.Identifier, out SemaphoreSlim? semaphore)) semaphore.Release(); }
        }
        public static Task SendAsync<T>(IdentifiedStream stream, T objectBDP, CancellationToken? token = null) where T : IBurcatObject => SendAsync(stream, new(objectBDP), token);
        public static Task SendAsync<T>(IdentifiedStream stream, CancellationToken? token = null) where T : IBurcatObject => SendAsync(stream, new(typeof(T), null), token);
        public static void Send(IdentifiedStream stream, BurcatInstance instance, CancellationToken? token = null) => SendAsync(stream, instance, token).GetAwaiter().GetResult();
        public static void Send<T>(IdentifiedStream stream, T objectBDP, CancellationToken? token = null) where T : IBurcatObject => SendAsync(stream, objectBDP, token).GetAwaiter().GetResult();
        public static void Send<T>(IdentifiedStream stream, CancellationToken? token = null) where T : IBurcatObject => SendAsync<T>(stream, token).GetAwaiter().GetResult();

        private static async Task<IBurcatObject?> RelayRequestAsync(Guid? streamID, Guid classID, Guid objectID, bool ignoreInternal, CancellationToken? token)
        {
            CancellationToken cancellation = token ?? new CancellationTokenSource(DefaultTimeOut).Token;
            IBurcatObject? reference;

            if (AcceptedClasses.TryGetValue(classID, out Type? type))
            {
                reference = ignoreInternal ? null : InternalProvider.GetObject(streamID, type, objectID);
                if (reference is null && ExternalProvider is not null) reference = await ExternalProvider.GetObject(streamID, type, objectID, cancellation);
                cancellation.ThrowIfCancellationRequested();
            }
            else throw new NotSupportedException($"Version with identifier {classID} is not supported.");

            return reference;
        }
        public static Task<IBurcatObject?> RelayRequestAsync(Guid classID, Guid objectID, bool ignoreInternal = false, CancellationToken? token = null) => RelayRequestAsync(null, classID, objectID, ignoreInternal, token);
        public static async Task<T?> RelayRequestAsync<T>(Guid objectID, bool ignoreInternal = false, CancellationToken? token = null) where T : IBurcatObject { T? result = (T?)await RelayRequestAsync(GetClassIdentity<T>(), objectID, ignoreInternal, token); return result; }
        public static Task<T?> RelayRequestAsync<T>(BurcatIdentifier<T> objectID, bool ignoreInternal = false, CancellationToken? token = null) where T : IBurcatObject => RelayRequestAsync<T>((Guid)objectID, ignoreInternal, token);
        public static IBurcatObject? RelayRequest(Guid classID, Guid objectID, bool ignoreInternal = false, CancellationToken? token = null) => RelayRequestAsync(classID, objectID, ignoreInternal, token).GetAwaiter().GetResult();
        public static T? RelayRequest<T>(Guid objectID, bool ignoreInternal = false, CancellationToken? token = null) where T : IBurcatObject => RelayRequestAsync<T>(objectID, ignoreInternal, token).GetAwaiter().GetResult();
        public static T? RelayRequest<T>(BurcatIdentifier<T> objectID, bool ignoreInternal = false, CancellationToken? token = null) where T : IBurcatObject => RelayRequestAsync<T>(objectID, ignoreInternal, token).GetAwaiter().GetResult();


        public static async Task<IBurcatObject?> SendRequestAsync(IdentifiedStream stream, Guid classID, Guid objectID, CancellationToken? token = null)
        {
            try
            {
                CancellationToken cancellation = token ?? new CancellationTokenSource(DefaultTimeOut).Token;
                if (ControlAsyncAccess) await Semaphores.GetOrAdd(stream.Identifier, new SemaphoreSlim(1, 1)).WaitAsync(cancellation);

                await stream.WriteAsync(GetClassIdentity<BeginRequestSchematic>().ToByteArray(), cancellation);
                cancellation.ThrowIfCancellationRequested();

                await stream.WriteAsync(GetClassIdentity<StreamScheme>().ToByteArray(), cancellation);
                cancellation.ThrowIfCancellationRequested();

                await stream.WriteAsync(stream.Identifier.ToByteArray(), cancellation);
                cancellation.ThrowIfCancellationRequested();

                await stream.WriteAsync(GetClassIdentity<StreamScheme>().ToByteArray(), cancellation);
                cancellation.ThrowIfCancellationRequested();

                await stream.WriteAsync(GetClassIdentity<VersionScheme>().ToByteArray(), cancellation);
                cancellation.ThrowIfCancellationRequested();

                await stream.WriteAsync(classID.ToByteArray(), cancellation);
                cancellation.ThrowIfCancellationRequested();

                await stream.WriteAsync(objectID.ToByteArray(), cancellation);
                cancellation.ThrowIfCancellationRequested();

                await stream.WriteAsync(GetClassIdentity<VersionScheme>().ToByteArray(), cancellation);
                cancellation.ThrowIfCancellationRequested();

                IBurcatObject? result = (await RecieveObject(stream, cancellation)).Value;
                cancellation.ThrowIfCancellationRequested();

                await stream.WriteAsync(GetClassIdentity<EndRequestSchematic>().ToByteArray(), cancellation);
                cancellation.ThrowIfCancellationRequested();

                return result;
            }
            finally { if (Semaphores.TryGetValue(stream.Identifier, out SemaphoreSlim? semaphore)) semaphore.Release(); }
        }
        public static IBurcatObject? SendRequest(IdentifiedStream stream, Guid classID, Guid objectID, CancellationToken? token = null) => SendRequestAsync(stream, classID, objectID, token).GetAwaiter().GetResult();
        public static async Task<T?> SendRequestAsync<T>(IdentifiedStream stream, Guid objectID, CancellationToken? token = null) where T : IBurcatObject { T? result = (T?)await SendRequestAsync(stream, GetClassIdentity<T>(), objectID, token); return result; }
        public static Task<T?> SendRequestAsync<T>(IdentifiedStream stream, BurcatIdentifier<T> objectID, CancellationToken? token = null) where T : IBurcatObject => SendRequestAsync<T>(stream, (Guid)objectID, token);
        public static T? SendRequest<T>(IdentifiedStream stream, Guid objectID, CancellationToken? token = null) where T : IBurcatObject => SendRequestAsync<T>(stream, objectID, token).GetAwaiter().GetResult();
        public static T? SendRequest<T>(IdentifiedStream stream, BurcatIdentifier<T> objectID, CancellationToken? token = null) where T : IBurcatObject => SendRequestAsync<T>(stream, objectID, token).GetAwaiter().GetResult();

        private static async Task<BurcatException?> RelayConstructAsync<T>(Guid? streamID, T objectBDP, bool ignoreInternal, CancellationToken? token) where T : IBurcatObject
        {
            CancellationToken cancellation = token ?? new CancellationTokenSource(DefaultTimeOut).Token;

            cancellation.ThrowIfCancellationRequested();
            BurcatException? creationException = InternalProvider.CreateObject(streamID, objectBDP);

            cancellation.ThrowIfCancellationRequested();
            if (ExternalProvider is not null) creationException = await ExternalProvider.CreateObject(streamID, objectBDP, cancellation) ?? creationException;

            cancellation.ThrowIfCancellationRequested();
            return creationException;
        }
        public static Task<BurcatException?> RelayConstructAsync<T>(T objectBDP, bool ignoreInternal = false, CancellationToken? token = null) where T : IBurcatObject => RelayConstructAsync(null, objectBDP, ignoreInternal, token);
        public static BurcatException? RelayConstruct<T>(T objectBDP, bool ignoreInternal = false, CancellationToken? token = null) where T : IBurcatObject => RelayConstructAsync(objectBDP, ignoreInternal, token).GetAwaiter().GetResult();

        public static async Task<BurcatException?> SendConstructAsync<T>(IdentifiedStream stream, T objectBDP, CancellationToken? token = null) where T : IBurcatObject
        {
            try
            {
                CancellationToken cancellation = token ?? new CancellationTokenSource(DefaultTimeOut).Token;
                if (ControlAsyncAccess) await Semaphores.GetOrAdd(stream.Identifier, new SemaphoreSlim(1, 1)).WaitAsync(cancellation);

                await stream.WriteAsync(GetClassIdentity<BeginConstructSchematic>().ToByteArray(), cancellation);
                cancellation.ThrowIfCancellationRequested();

                await stream.WriteAsync(GetClassIdentity<StreamScheme>().ToByteArray(), cancellation);
                cancellation.ThrowIfCancellationRequested();

                await stream.WriteAsync(stream.Identifier.ToByteArray(), cancellation);
                cancellation.ThrowIfCancellationRequested();

                await stream.WriteAsync(GetClassIdentity<StreamScheme>().ToByteArray(), cancellation);
                cancellation.ThrowIfCancellationRequested();

                await SendObject(stream, objectBDP, cancellation);
                cancellation.ThrowIfCancellationRequested();

                BurcatException? result = (await RecieveObject(stream, cancellation)).ForceValue<BurcatException?>();
                cancellation.ThrowIfCancellationRequested();

                await stream.WriteAsync(GetClassIdentity<EndConstructSchematic>().ToByteArray(), cancellation);
                cancellation.ThrowIfCancellationRequested();

                return result;
            }
            finally { if (Semaphores.TryGetValue(stream.Identifier, out SemaphoreSlim? semaphore)) semaphore.Release(); }
        }
        public static BurcatException? SendConstruct<T>(IdentifiedStream stream, T objectBDP, CancellationToken? token = null) where T : IBurcatObject => SendConstructAsync(stream, objectBDP, token).GetAwaiter().GetResult();

        private async static Task<BurcatException?> RelayUpdateAsync(Guid? streamID, Guid classID, Guid? objectID, BurcatField field, bool ignoreInternal, CancellationToken? token)
        {
            CancellationToken cancellation = token ?? new CancellationTokenSource(DefaultTimeOut).Token;
            BurcatException? updateException;

            if (AcceptedClasses.TryGetValue(classID, out Type? type))
            {
                cancellation.ThrowIfCancellationRequested();
                updateException = InternalProvider.UpdateObject(streamID, type, objectID, field);

                cancellation.ThrowIfCancellationRequested();
                if (ExternalProvider is not null) updateException = await ExternalProvider.UpdateObject(streamID, type, objectID, field, cancellation) ?? updateException;
            }
            else throw new NotSupportedException($"Version with identifier {classID} is not supported.");

            return updateException;
        }
        public static Task<BurcatException?> RelayUpdateAsync(IdentifiedStream stream, Guid classID, Guid? objectID, BurcatField field, bool ignoreInternal = false, CancellationToken? token = null) => RelayUpdateAsync(stream.Identifier, classID, objectID, field, ignoreInternal, token);
        public static Task<BurcatException?> RelayUpdateAsync<T>(IdentifiedStream stream, BurcatIdentifier<T> objectID, BurcatField field, bool ignoreInternal = false, CancellationToken? token = null) where T : IBurcatObject => RelayUpdateAsync(stream, GetClassIdentity<T>(), (Guid)objectID, field, ignoreInternal, token);
        public static Task<BurcatException?> RelayUpdateAsync<T>(IdentifiedStream stream, T objectBDP, BurcatField field, bool ignoreInternal = false, CancellationToken? token = null) where T : IBurcatObject => RelayUpdateAsync(stream, GetClassIdentity<T>(), objectBDP.Identifier, field, ignoreInternal, token);
        public static Task<BurcatException?> RelayUpdateAsync<T>(IdentifiedStream stream, BurcatField field, bool ignoreInternal = false, CancellationToken? token = null) where T : IBurcatObject => RelayUpdateAsync(stream, GetClassIdentity<T>(), null, field, ignoreInternal, token);
        public static BurcatException? RelayUpdate(IdentifiedStream stream, Guid classID, Guid? objectID, BurcatField field, bool ignoreInternal = false, CancellationToken? token = null) => RelayUpdateAsync(stream, classID, objectID, field, ignoreInternal, token).GetAwaiter().GetResult();
        public static BurcatException? RelayUpdate<T>(IdentifiedStream stream, BurcatIdentifier<T> objectID, BurcatField field, bool ignoreInternal = false, CancellationToken? token = null) where T : IBurcatObject => RelayUpdateAsync(stream, GetClassIdentity<T>(), (Guid)objectID, field, ignoreInternal, token).GetAwaiter().GetResult();
        public static BurcatException? RelayUpdate<T>(IdentifiedStream stream, T objectBDP, BurcatField field, bool ignoreInternal = false, CancellationToken? token = null) where T : IBurcatObject => RelayUpdateAsync(stream, GetClassIdentity<T>(), objectBDP.Identifier, field, ignoreInternal, token).GetAwaiter().GetResult();
        public static BurcatException? RelayUpdate<T>(IdentifiedStream stream, BurcatField field, bool ignoreInternal = false, CancellationToken? token = null) where T : IBurcatObject => RelayUpdateAsync<T>(stream, field, ignoreInternal, token).GetAwaiter().GetResult();

        public async static Task<BurcatException?> SendUpdateAsync(IdentifiedStream stream, Guid classID, Guid? objectID, BurcatField field, CancellationToken? token = null)
        {
            try
            {
                CancellationToken cancellation = token ?? new CancellationTokenSource(DefaultTimeOut).Token;
                if (ControlAsyncAccess) await Semaphores.GetOrAdd(stream.Identifier, new SemaphoreSlim(1, 1)).WaitAsync(cancellation);

                await stream.WriteAsync(GetClassIdentity<BeginUpdateSchematic>().ToByteArray(), cancellation);
                cancellation.ThrowIfCancellationRequested();

                await stream.WriteAsync(GetClassIdentity<StreamScheme>().ToByteArray(), cancellation);
                cancellation.ThrowIfCancellationRequested();

                await stream.WriteAsync(stream.Identifier.ToByteArray(), cancellation);
                cancellation.ThrowIfCancellationRequested();

                await stream.WriteAsync(GetClassIdentity<StreamScheme>().ToByteArray(), cancellation);
                cancellation.ThrowIfCancellationRequested();

                await stream.WriteAsync(GetClassIdentity<VersionScheme>().ToByteArray(), cancellation);
                cancellation.ThrowIfCancellationRequested();

                await stream.WriteAsync(classID.ToByteArray(), cancellation);
                cancellation.ThrowIfCancellationRequested();

                await stream.WriteAsync(objectID?.ToByteArray() ?? Guid.AllBitsSet.ToByteArray(), cancellation);
                cancellation.ThrowIfCancellationRequested();

                await stream.WriteAsync(GetClassIdentity<VersionScheme>().ToByteArray(), cancellation);
                cancellation.ThrowIfCancellationRequested();

                await stream.WriteAsync(GetClassIdentity<FieldUpdateScheme>().ToByteArray(), cancellation);
                cancellation.ThrowIfCancellationRequested();

                await SendObject(stream, field, cancellation);
                cancellation.ThrowIfCancellationRequested();

                await stream.WriteAsync(GetClassIdentity<FieldUpdateScheme>().ToByteArray(), cancellation);
                cancellation.ThrowIfCancellationRequested();

                BurcatException? result = (await RecieveObject(stream, cancellation)).ForceValue<BurcatException?>();
                cancellation.ThrowIfCancellationRequested();

                await stream.WriteAsync(GetClassIdentity<EndUpdateSchematic>().ToByteArray(), cancellation);
                cancellation.ThrowIfCancellationRequested();

                return result;
            }
            finally { if (Semaphores.TryGetValue(stream.Identifier, out SemaphoreSlim? semaphore)) semaphore.Release(); }
        }
        public static Task<BurcatException?> SendUpdateAsync<T>(IdentifiedStream stream, BurcatIdentifier<T> objectID, BurcatField field, CancellationToken? token = null) where T : IBurcatObject => SendUpdateAsync(stream, GetClassIdentity<T>(), (Guid)objectID, field, token);
        public static Task<BurcatException?> SendUpdateAsync<T>(IdentifiedStream stream, T objectBDP, BurcatField field, CancellationToken? token = null) where T : IBurcatObject => SendUpdateAsync(stream, GetClassIdentity<T>(), objectBDP.Identifier, field, token);
        public static Task<BurcatException?> SendUpdateAsync<T>(IdentifiedStream stream, BurcatField field, CancellationToken? token = null) where T : IBurcatObject => SendUpdateAsync(stream, GetClassIdentity<T>(), null, field, token);
        public static BurcatException? SendUpdate(IdentifiedStream stream, Guid classID, Guid? objectID, BurcatField field, CancellationToken? token = null) => SendUpdateAsync(stream, classID, objectID, field, token).GetAwaiter().GetResult();
        public static BurcatException? SendUpdate<T>(IdentifiedStream stream, BurcatIdentifier<T> objectID, BurcatField field, CancellationToken? token = null) where T : IBurcatObject => SendUpdateAsync(stream, GetClassIdentity<T>(), (Guid)objectID, field, token).GetAwaiter().GetResult();
        public static BurcatException? SendUpdate<T>(IdentifiedStream stream, T objectBDP, BurcatField field, CancellationToken? token = null) where T : IBurcatObject => SendUpdateAsync(stream, GetClassIdentity<T>(), objectBDP.Identifier, field, token).GetAwaiter().GetResult();
        public static BurcatException? SendUpdate<T>(IdentifiedStream stream, BurcatField field, CancellationToken? token = null) where T : IBurcatObject => SendUpdateAsync<T>(stream, field, token).GetAwaiter().GetResult();

        private static async Task<BurcatException?> RelayDestructAsync(Guid? streamID, Guid classID, Guid objectID, bool ignoreInternal, CancellationToken? token)
        {
            CancellationToken cancellation = token ?? new CancellationTokenSource(DefaultTimeOut).Token;
            BurcatException? destructionException;

            if (AcceptedClasses.TryGetValue(classID, out Type? type))
            {
                destructionException = InternalProvider.DestroyObject(streamID, type, objectID);
                cancellation.ThrowIfCancellationRequested();

                if (ExternalProvider is not null) destructionException = await ExternalProvider.DestroyObject(streamID, type, objectID, cancellation) ?? destructionException;
                cancellation.ThrowIfCancellationRequested();
            }
            else throw new NotSupportedException($"Version with identifier {classID} is not supported.");

            return destructionException;
        }
        public static Task<BurcatException?> RelayDestructAsync(Guid classID, Guid objectID, bool ignoreInternal = false, CancellationToken? token = null) => RelayDestructAsync(null, classID, objectID, ignoreInternal, token);
        public static Task<BurcatException?> RelayDestructAsync<T>(BurcatIdentifier<T> objectID, bool ignoreInternal = false, CancellationToken? token = null) where T : IBurcatObject => RelayDestructAsync(null, GetClassIdentity<T>(), (Guid)objectID, ignoreInternal, token);
        public static Task<BurcatException?> RelayDestructAsync<T>(T objectBDP, bool ignoreInternal = false, CancellationToken? token = null) where T : IBurcatObject => RelayDestructAsync(null, GetClassIdentity<T>(), objectBDP.Identifier, ignoreInternal, token);
        public static BurcatException? RelayDestruct(Guid classID, Guid objectID, bool ignoreInternal = false, CancellationToken? token = null) => RelayDestructAsync(classID, objectID, ignoreInternal, token).GetAwaiter().GetResult();
        public static BurcatException? RelayDestruct<T>(BurcatIdentifier<T> objectID, bool ignoreInternal = false, CancellationToken? token = null) where T : IBurcatObject => RelayDestructAsync<T>(objectID, ignoreInternal, token).GetAwaiter().GetResult();
        public static BurcatException? RelayDestruct<T>(T objectBDP, bool ignoreInternal = false, CancellationToken? token = null) where T : IBurcatObject => RelayDestructAsync<T>(objectBDP, ignoreInternal, token).GetAwaiter().GetResult();

        public static async Task<BurcatException?> SendDestructAsync(IdentifiedStream stream, Guid classID, Guid objectID, CancellationToken? token = null)
        {
            try
            {
                CancellationToken cancellation = token ?? new CancellationTokenSource(DefaultTimeOut).Token;
                if (ControlAsyncAccess) await Semaphores.GetOrAdd(stream.Identifier, new SemaphoreSlim(1, 1)).WaitAsync(cancellation);

                await stream.WriteAsync(GetClassIdentity<BeginDestructSchematic>().ToByteArray(), cancellation);
                cancellation.ThrowIfCancellationRequested();

                await stream.WriteAsync(GetClassIdentity<StreamScheme>().ToByteArray(), cancellation);
                cancellation.ThrowIfCancellationRequested();

                await stream.WriteAsync(stream.Identifier.ToByteArray(), cancellation);
                cancellation.ThrowIfCancellationRequested();

                await stream.WriteAsync(GetClassIdentity<StreamScheme>().ToByteArray(), cancellation);
                cancellation.ThrowIfCancellationRequested();

                await stream.WriteAsync(GetClassIdentity<VersionScheme>().ToByteArray(), cancellation);
                cancellation.ThrowIfCancellationRequested();

                await stream.WriteAsync(classID.ToByteArray(), cancellation);
                cancellation.ThrowIfCancellationRequested();

                await stream.WriteAsync(objectID.ToByteArray(), cancellation);
                cancellation.ThrowIfCancellationRequested();

                await stream.WriteAsync(GetClassIdentity<VersionScheme>().ToByteArray(), cancellation);
                cancellation.ThrowIfCancellationRequested();

                BurcatException? result = (await RecieveObject(stream, cancellation)).ForceValue<BurcatException?>();
                cancellation.ThrowIfCancellationRequested();

                await stream.WriteAsync(GetClassIdentity<EndDestructSchematic>().ToByteArray(), cancellation);
                cancellation.ThrowIfCancellationRequested();

                return result;
            }
            finally { if (Semaphores.TryGetValue(stream.Identifier, out SemaphoreSlim? semaphore)) semaphore.Release(); }
        }
        public static Task<BurcatException?> SendDestructAsync<T>(IdentifiedStream stream, BurcatIdentifier<T> objectID, CancellationToken? token = null) where T : IBurcatObject => SendDestructAsync(stream, GetClassIdentity<T>(), (Guid)objectID, token);
        public static Task<BurcatException?> SendDestructAsync<T>(IdentifiedStream stream, T objectBDP, CancellationToken? token = null) where T : IBurcatObject => SendDestructAsync(stream, GetClassIdentity<T>(), objectBDP.Identifier, token);
        public static BurcatException? SendDestruct<T>(IdentifiedStream stream, Guid classID, Guid objectID, CancellationToken? token = null) where T : IBurcatObject => SendDestructAsync(stream, classID, objectID, token).GetAwaiter().GetResult();
        public static BurcatException? SendDestruct<T>(IdentifiedStream stream, BurcatIdentifier<T> objectID, CancellationToken? token = null) where T : IBurcatObject => SendDestructAsync<T>(stream, objectID, token).GetAwaiter().GetResult();
        public static BurcatException? SendDestruct<T>(IdentifiedStream stream, T objectBDP, CancellationToken? token = null) where T : IBurcatObject => SendDestructAsync<T>(stream, objectBDP, token).GetAwaiter().GetResult();

        private static async Task<ActionResult> RelayActionAsync(Guid? streamID, BurcatInstance instance, string action, IBurcatObject?[]? parameters, bool ignoreInternal, CancellationToken? token)
        {
            if (action.Length != 0 && !char.IsLetterOrDigit(action[0])) throw new ArgumentException("An action must start with a letter or number", nameof(action));
            else
            {
                CancellationToken cancellation = token ?? new CancellationTokenSource(DefaultTimeOut).Token;
                parameters ??= [];

                ActionResult result = InternalProvider.ExecuteAction(streamID, instance.Type, instance.Value, action, parameters);
                cancellation.ThrowIfCancellationRequested();

                if (!result.SuccessfulExecution && ExternalProvider is not null) result = await ExternalProvider.ExecuteAction(streamID, instance.Type, instance.Value, action, parameters, cancellation);
                cancellation.ThrowIfCancellationRequested();

                return result;
            }
        }
        public static Task<ActionResult> RelayActionAsync(BurcatInstance instance, string action, IBurcatObject?[]? parameters = null, bool ignoreInternal = false, CancellationToken? token = null) => RelayActionAsync(null, instance, action, parameters, ignoreInternal, token);
        public static Task<ActionResult> RelayActionAsync<T>(T objectBDP, string action, IBurcatObject?[]? parameters = null, bool ignoreInternal = false, CancellationToken? token = null) where T : IBurcatObject => RelayActionAsync(null, new(objectBDP), action, parameters, ignoreInternal, token);
        public static Task<ActionResult> RelayActionAsync<T>(string action, IBurcatObject?[]? parameters = null, bool ignoreInternal = false, CancellationToken? token = null) where T : IBurcatObject => RelayActionAsync(null, new(typeof(T), null), action, parameters, ignoreInternal, token);
        public static ActionResult RelayAction(BurcatInstance instance, string action, IBurcatObject?[]? parameters = null, bool ignoreInternal = false, CancellationToken? token = null) => RelayActionAsync(instance, action, parameters, ignoreInternal, token).GetAwaiter().GetResult();
        public static ActionResult RelayAction<T>(T objectBDP, string action, IBurcatObject?[]? parameters = null, bool ignoreInternal = false, CancellationToken? token = null) where T : IBurcatObject => RelayAction(new(objectBDP), action, parameters, ignoreInternal, token);
        public static ActionResult RelayAction<T>(string action, IBurcatObject?[]? parameters = null, bool ignoreInternal = false, CancellationToken? token = null) where T : IBurcatObject => RelayAction(new(typeof(T), null), action, parameters, ignoreInternal, token);


        public static async Task<ActionResult> SendActionAsync(IdentifiedStream stream, BurcatInstance instance, string action, IBurcatObject?[]? parameters = null, CancellationToken? token = null)
        {
            if (action.Length != 0 && !char.IsLetterOrDigit(action[0])) throw new ArgumentException("An action must start with a letter or number", nameof(action));
            else
            {
                try
                {
                    CancellationToken cancellation = token ?? new CancellationTokenSource(DefaultTimeOut).Token;
                    if (ControlAsyncAccess) await Semaphores.GetOrAdd(stream.Identifier, new SemaphoreSlim(1, 1)).WaitAsync(cancellation);
                    parameters ??= [];

                    await stream.WriteAsync(GetClassIdentity<BeginActionSchematic>().ToByteArray(), cancellation);
                    cancellation.ThrowIfCancellationRequested();

                    await stream.WriteAsync(GetClassIdentity<StreamScheme>().ToByteArray(), cancellation);
                    cancellation.ThrowIfCancellationRequested();

                    await stream.WriteAsync(stream.Identifier.ToByteArray(), cancellation);
                    cancellation.ThrowIfCancellationRequested();

                    await stream.WriteAsync(GetClassIdentity<StreamScheme>().ToByteArray(), cancellation);
                    cancellation.ThrowIfCancellationRequested();

                    await SendObject(stream, instance, cancellation);
                    cancellation.ThrowIfCancellationRequested();

                    await stream.WriteAsync(GetClassIdentity<ActionScheme>().ToByteArray(), cancellation);
                    byte[] actionData = Encoding.Unicode.GetBytes(action);
                    await stream.WriteAsync(BitConverter.GetBytes(actionData.Length), cancellation);
                    await stream.WriteAsync(actionData, cancellation);
                    await stream.WriteAsync(GetClassIdentity<ActionScheme>().ToByteArray(), cancellation);
                    cancellation.ThrowIfCancellationRequested();

                    await stream.WriteAsync(GetClassIdentity<ParameterScheme>().ToByteArray(), cancellation);
                    await stream.WriteAsync(BitConverter.GetBytes(parameters.Length), cancellation);
                    foreach (IBurcatObject? parameter in parameters)
                    {
                        cancellation.ThrowIfCancellationRequested();

                        if (parameter is null) await SendObject(stream, NothingChart.Instance, cancellation);
                        else await SendObject(stream, new(parameter), cancellation);

                        cancellation.ThrowIfCancellationRequested();
                    }
                    await stream.WriteAsync(GetClassIdentity<ParameterScheme>().ToByteArray(), cancellation);

                    ActionResult result = (await RecieveObject(stream, cancellation)).ForceValue<ActionResult>();
                    cancellation.ThrowIfCancellationRequested();

                    await stream.WriteAsync(GetClassIdentity<EndActionSchematic>().ToByteArray(), cancellation);
                    cancellation.ThrowIfCancellationRequested();

                    return result;
                }
                finally { if (Semaphores.TryGetValue(stream.Identifier, out SemaphoreSlim? semaphore)) semaphore.Release(); }
            }
        }
        public static Task<ActionResult> SendActionAsync<T>(IdentifiedStream stream, T objectBDP, string action, IBurcatObject?[]? parameters = null, CancellationToken? token = null) where T : IBurcatObject => SendActionAsync(stream, new(objectBDP), action, parameters, token);
        public static Task<ActionResult> SendActionAsync<T>(IdentifiedStream stream, string action, IBurcatObject?[]? parameters = null, CancellationToken? token = null) where T : IBurcatObject => SendActionAsync(stream, new(typeof(T), null), action, parameters, token);
        public static ActionResult SendAction(IdentifiedStream stream, BurcatInstance instance, string action, IBurcatObject?[]? parameters = null, CancellationToken? token = null) => SendActionAsync(stream, instance, action, parameters, token).GetAwaiter().GetResult();
        public static ActionResult SendAction<T>(IdentifiedStream stream, T objectBDP, string action, IBurcatObject?[]? parameters = null, CancellationToken? token = null) where T : IBurcatObject => SendAction(stream, new(objectBDP), action, parameters, token);
        public static ActionResult SendAction<T>(IdentifiedStream stream, string action, IBurcatObject?[]? parameters = null, CancellationToken? token = null) where T : IBurcatObject => SendAction(stream, new(typeof(T), null), action, parameters, token);

        public static IInternalProvider InternalProvider { get; set; } = new NothingProvider();
        public static IExternalProvider? ExternalProvider { get; set; }

        public static async Task<ExchangeResult> RecieveAsync(IdentifiedStream stream, CancellationToken? token)
        {
            try
            {
                CancellationToken cancellation = token ?? new CancellationTokenSource(DefaultTimeOut).Token;
                if (ControlAsyncAccess) await Semaphores.GetOrAdd(stream.Identifier, new SemaphoreSlim(1, 1)).WaitAsync(cancellation);

                Guid scheme = await RecieveScheme(stream, cancellation);
                cancellation.ThrowIfCancellationRequested();

                if (scheme == GetClassIdentity<BeginRequestSchematic>())
                {
                    if (!await RecieveScheme<StreamScheme>(stream, cancellation)) throw new InvalidDataException($"Expected scheme with identifier {GetClassIdentity<VersionScheme>()}, but data read doesn't correspond to");
                    cancellation.ThrowIfCancellationRequested();

                    byte[] guid = new byte[16];
                    await stream.ReadExactlyAsync(guid, cancellation); Guid streamID = new(guid);
                    cancellation.ThrowIfCancellationRequested();

                    if (!await RecieveScheme<StreamScheme>(stream, cancellation)) throw new InvalidDataException($"Expected scheme with identifier {GetClassIdentity<VersionScheme>()}, but data read doesn't correspond to");
                    cancellation.ThrowIfCancellationRequested();

                    if (!await RecieveScheme<VersionScheme>(stream, cancellation)) throw new InvalidDataException($"Expected scheme with identifier {GetClassIdentity<VersionScheme>()}, but data read doesn't correspond to");
                    cancellation.ThrowIfCancellationRequested();

                    await stream.ReadExactlyAsync(guid, cancellation); Guid classID = new(guid);
                    cancellation.ThrowIfCancellationRequested();

                    await stream.ReadExactlyAsync(guid, cancellation); Guid objectID = new(guid);
                    cancellation.ThrowIfCancellationRequested();

                    if (!await RecieveScheme<VersionScheme>(stream, cancellation)) throw new InvalidDataException($"Expected scheme with identifier {GetClassIdentity<VersionScheme>()}, but data read doesn't correspond to");
                    cancellation.ThrowIfCancellationRequested();

                    BurcatInstance? instance = new(AcceptedClasses[classID], await RelayRequestAsync(streamID, classID, objectID, false, cancellation));
                    cancellation.ThrowIfCancellationRequested();

                    await SendObject(stream, instance, cancellation);
                    cancellation.ThrowIfCancellationRequested();

                    if (!await RecieveScheme<EndRequestSchematic>(stream, cancellation)) throw new InvalidDataException($"Expected scheme with identifier {GetClassIdentity<EndRequestSchematic>()}, but data read doesn't correspond to");
                    cancellation.ThrowIfCancellationRequested();

                    return new(BurcatExchangeType.Request, new(AcceptedClasses[classID], null), instance);
                }
                else if (scheme == GetClassIdentity<BeginObjectSchematic>())
                {
                    if (!await RecieveScheme<StreamScheme>(stream, cancellation)) throw new InvalidDataException($"Expected scheme with identifier {GetClassIdentity<VersionScheme>()}, but data read doesn't correspond to");
                    cancellation.ThrowIfCancellationRequested();

                    byte[] guid = new byte[16];
                    await stream.ReadExactlyAsync(guid, cancellation); Guid streamID = new(guid);
                    cancellation.ThrowIfCancellationRequested();

                    if (!await RecieveScheme<StreamScheme>(stream, cancellation)) throw new InvalidDataException($"Expected scheme with identifier {GetClassIdentity<VersionScheme>()}, but data read doesn't correspond to");
                    cancellation.ThrowIfCancellationRequested();

                    BurcatInstance instance = await RecieveObject(stream, cancellation);
                    cancellation.ThrowIfCancellationRequested();

                    if (!await RecieveScheme<EndObjectSchematic>(stream, cancellation)) throw new InvalidDataException($"Expected scheme with identifier {GetClassIdentity<EndObjectSchematic>()}, but data read doesn't correspond to");
                    cancellation.ThrowIfCancellationRequested();

                    return new(BurcatExchangeType.Object, instance);
                }
                else if (scheme == GetClassIdentity<BeginConstructSchematic>())
                {
                    if (!await RecieveScheme<StreamScheme>(stream, cancellation)) throw new InvalidDataException($"Expected scheme with identifier {GetClassIdentity<VersionScheme>()}, but data read doesn't correspond to");
                    cancellation.ThrowIfCancellationRequested();

                    byte[] guid = new byte[16];
                    await stream.ReadExactlyAsync(guid, cancellation); Guid streamID = new(guid);
                    cancellation.ThrowIfCancellationRequested();

                    if (!await RecieveScheme<StreamScheme>(stream, cancellation)) throw new InvalidDataException($"Expected scheme with identifier {GetClassIdentity<VersionScheme>()}, but data read doesn't correspond to");
                    cancellation.ThrowIfCancellationRequested();

                    BurcatInstance instance = await RecieveObject(stream, cancellation);
                    cancellation.ThrowIfCancellationRequested();

                    IBurcatObject reference = instance.Value ?? throw new NullReferenceException("Cannot construct an empty object.");
                    cancellation.ThrowIfCancellationRequested();

                    BurcatException? creationException = await RelayConstructAsync(streamID, reference, false, cancellation);
                    cancellation.ThrowIfCancellationRequested();

                    await SendObject(stream, creationException, cancellation);
                    cancellation.ThrowIfCancellationRequested();

                    if (!await RecieveScheme<EndConstructSchematic>(stream, cancellation)) throw new InvalidDataException($"Expected scheme with identifier {GetClassIdentity<EndConstructSchematic>()}, but data read doesn't correspond to");
                    cancellation.ThrowIfCancellationRequested();

                    return new(BurcatExchangeType.Construct, instance, new(typeof(BurcatException), creationException));
                }
                else if (scheme == GetClassIdentity<BeginUpdateSchematic>())
                {
                    if (!await RecieveScheme<StreamScheme>(stream, cancellation)) throw new InvalidDataException($"Expected scheme with identifier {GetClassIdentity<VersionScheme>()}, but data read doesn't correspond to");
                    cancellation.ThrowIfCancellationRequested();

                    byte[] guid = new byte[16];
                    await stream.ReadExactlyAsync(guid, cancellation); Guid streamID = new(guid);
                    cancellation.ThrowIfCancellationRequested();

                    if (!await RecieveScheme<StreamScheme>(stream, cancellation)) throw new InvalidDataException($"Expected scheme with identifier {GetClassIdentity<VersionScheme>()}, but data read doesn't correspond to");
                    cancellation.ThrowIfCancellationRequested();

                    if (!await RecieveScheme<VersionScheme>(stream, cancellation)) throw new InvalidDataException($"Expected scheme with identifier {GetClassIdentity<VersionScheme>()}, but data read doesn't correspond to");
                    cancellation.ThrowIfCancellationRequested();

                    await stream.ReadExactlyAsync(guid, cancellation); Guid classID = new(guid);
                    cancellation.ThrowIfCancellationRequested();

                    await stream.ReadExactlyAsync(guid, cancellation); Guid objectID = new(guid);
                    cancellation.ThrowIfCancellationRequested();

                    if (!await RecieveScheme<VersionScheme>(stream, cancellation)) throw new InvalidDataException($"Expected scheme with identifier {GetClassIdentity<VersionScheme>()}, but data read doesn't correspond to");
                    cancellation.ThrowIfCancellationRequested();

                    if (!await RecieveScheme<FieldUpdateScheme>(stream, cancellation)) throw new InvalidDataException($"Expected scheme with identifier {GetClassIdentity<FieldUpdateScheme>()}, but data read doesn't correspond to");
                    cancellation.ThrowIfCancellationRequested();

                    BurcatField field = (await RecieveObject(stream, cancellation)).ForceValue<BurcatField>()!;
                    cancellation.ThrowIfCancellationRequested();

                    if (!await RecieveScheme<FieldUpdateScheme>(stream, cancellation)) throw new InvalidDataException($"Expected scheme with identifier {GetClassIdentity<FieldUpdateScheme>()}, but data read doesn't correspond to");
                    cancellation.ThrowIfCancellationRequested();

                    BurcatException? updateException = await RelayUpdateAsync(streamID, classID, objectID == Guid.AllBitsSet ? null : objectID, field, false, token);
                    cancellation.ThrowIfCancellationRequested();

                    await SendObject(stream, updateException, cancellation);
                    cancellation.ThrowIfCancellationRequested();

                    if (!await RecieveScheme<EndUpdateSchematic>(stream, cancellation)) throw new InvalidDataException($"Expected scheme with identifier {GetClassIdentity<EndUpdateSchematic>()}, but data read doesn't correspond to");
                    cancellation.ThrowIfCancellationRequested();

                    return new(BurcatExchangeType.Update, new(AcceptedClasses[classID], BurcatTranslator.Translate(objectID)), new(typeof(BurcatException), updateException), field.Name);
                }
                else if (scheme == GetClassIdentity<BeginDestructSchematic>())
                {
                    if (!await RecieveScheme<StreamScheme>(stream, cancellation)) throw new InvalidDataException($"Expected scheme with identifier {GetClassIdentity<VersionScheme>()}, but data read doesn't correspond to");
                    cancellation.ThrowIfCancellationRequested();

                    byte[] guid = new byte[16];
                    await stream.ReadExactlyAsync(guid, cancellation); Guid streamID = new(guid);
                    cancellation.ThrowIfCancellationRequested();

                    if (!await RecieveScheme<StreamScheme>(stream, cancellation)) throw new InvalidDataException($"Expected scheme with identifier {GetClassIdentity<VersionScheme>()}, but data read doesn't correspond to");
                    cancellation.ThrowIfCancellationRequested();

                    if (!await RecieveScheme<VersionScheme>(stream, cancellation)) throw new InvalidDataException($"Expected scheme with identifier {GetClassIdentity<VersionScheme>()}, but data read doesn't correspond to");
                    cancellation.ThrowIfCancellationRequested();

                    await stream.ReadExactlyAsync(guid, cancellation); Guid classID = new(guid);
                    cancellation.ThrowIfCancellationRequested();

                    await stream.ReadExactlyAsync(guid, cancellation); Guid objectID = new(guid);
                    cancellation.ThrowIfCancellationRequested();

                    if (!await RecieveScheme<VersionScheme>(stream, cancellation)) throw new InvalidDataException($"Expected scheme with identifier {GetClassIdentity<VersionScheme>()}, but data read doesn't correspond to");
                    cancellation.ThrowIfCancellationRequested();

                    BurcatException? destroyException = await RelayDestructAsync(streamID, classID, objectID, false, token);
                    cancellation.ThrowIfCancellationRequested();

                    await SendObject(stream, destroyException, cancellation);
                    cancellation.ThrowIfCancellationRequested();

                    if (!await RecieveScheme<EndDestructSchematic>(stream, cancellation)) throw new InvalidDataException($"Expected scheme with identifier {GetClassIdentity<EndDestructSchematic>()}, but data read doesn't correspond to");
                    cancellation.ThrowIfCancellationRequested();

                    return new(BurcatExchangeType.Destruct, new(AcceptedClasses[classID], BurcatTranslator.Translate(objectID)), new(typeof(BurcatException), destroyException));
                }
                else if (scheme == GetClassIdentity<BeginActionSchematic>())
                {
                    if (!await RecieveScheme<StreamScheme>(stream, cancellation)) throw new InvalidDataException($"Expected scheme with identifier {GetClassIdentity<VersionScheme>()}, but data read doesn't correspond to");
                    cancellation.ThrowIfCancellationRequested();

                    byte[] guid = new byte[16];
                    await stream.ReadExactlyAsync(guid, cancellation); Guid streamID = new(guid);
                    cancellation.ThrowIfCancellationRequested();

                    if (!await RecieveScheme<StreamScheme>(stream, cancellation)) throw new InvalidDataException($"Expected scheme with identifier {GetClassIdentity<VersionScheme>()}, but data read doesn't correspond to");
                    cancellation.ThrowIfCancellationRequested();

                    BurcatInstance instance = await RecieveObject(stream, cancellation);
                    cancellation.ThrowIfCancellationRequested();

                    if (!await RecieveScheme<ActionScheme>(stream, cancellation)) throw new InvalidDataException($"Expected scheme with identifier {GetClassIdentity<ActionScheme>()}, but data read doesn't correspond to");
                    cancellation.ThrowIfCancellationRequested();

                    byte[] length = new byte[4];
                    await stream.ReadExactlyAsync(length, cancellation);
                    cancellation.ThrowIfCancellationRequested();
                    byte[] data = new byte[BitConverter.ToInt32(length)];
                    await stream.ReadExactlyAsync(data, cancellation);
                    cancellation.ThrowIfCancellationRequested();

                    if (!await RecieveScheme<ActionScheme>(stream, cancellation)) throw new InvalidDataException($"Expected scheme with identifier {GetClassIdentity<ActionScheme>()}, but data read doesn't correspond to");
                    cancellation.ThrowIfCancellationRequested();

                    if (!await RecieveScheme<ParameterScheme>(stream, cancellation)) throw new InvalidDataException($"Expected scheme with identifier {GetClassIdentity<ParameterScheme>()}, but data read doesn't correspond to");
                    cancellation.ThrowIfCancellationRequested();

                    await stream.ReadExactlyAsync(length, cancellation);
                    IBurcatObject?[] parameters = new IBurcatObject?[BitConverter.ToInt32(length)];
                    for (int j = 0; j < parameters.Length; j++)
                    {
                        cancellation.ThrowIfCancellationRequested();
                        parameters[j] = (await RecieveObject(stream, cancellation)).Value;
                        cancellation.ThrowIfCancellationRequested();
                    }
                    if (!await RecieveScheme<ParameterScheme>(stream, cancellation)) throw new InvalidDataException($"Expected scheme with identifier {GetClassIdentity<ParameterScheme>()}, but data read doesn't correspond to");
                    cancellation.ThrowIfCancellationRequested();

                    ActionResult result = await RelayActionAsync(streamID, instance, Encoding.Unicode.GetString(data), parameters, false, cancellation);
                    cancellation.ThrowIfCancellationRequested();

                    await SendObject(stream, result, cancellation);
                    cancellation.ThrowIfCancellationRequested();

                    if (!await RecieveScheme<EndActionSchematic>(stream, cancellation)) throw new InvalidDataException($"Expected scheme with identifier {GetClassIdentity<EndActionSchematic>()}, but data read doesn't correspond to");
                    cancellation.ThrowIfCancellationRequested();

                    return new(BurcatExchangeType.Action, instance, new(result), parameters, Encoding.Unicode.GetString(data));
                }
                else throw new InvalidDataException("No supported scheme");
            }
            finally { if (Semaphores.TryGetValue(stream.Identifier, out SemaphoreSlim? semaphore)) semaphore.Release(); }
        }
        public static Task<ExchangeResult> RecieveAsync(IdentifiedStream stream) => RecieveAsync(stream, CancellationToken.None);
        public static ExchangeResult Recieve(IdentifiedStream stream, CancellationToken? token) => RecieveAsync(stream, token).GetAwaiter().GetResult();
        public static ExchangeResult Recieve(IdentifiedStream stream) => Recieve(stream, CancellationToken.None);

        private static async Task SendObject(IdentifiedStream stream, BurcatInstance instance, CancellationToken token)
        {
            await stream.WriteAsync(GetClassIdentity<StreamScheme>().ToByteArray(), token);
            token.ThrowIfCancellationRequested();

            await stream.WriteAsync(stream.Identifier.ToByteArray(), token);
            token.ThrowIfCancellationRequested();

            await stream.WriteAsync(GetClassIdentity<StreamScheme>().ToByteArray(), token);
            token.ThrowIfCancellationRequested();

            await stream.WriteAsync(GetClassIdentity<VersionScheme>().ToByteArray(), token);
            token.ThrowIfCancellationRequested();

            await stream.WriteAsync(GetClassIdentity(instance.Type).ToByteArray(), token);
            token.ThrowIfCancellationRequested();

            await stream.WriteAsync(instance.Value?.Identifier.ToByteArray() ?? Guid.AllBitsSet.ToByteArray(), token);
            token.ThrowIfCancellationRequested();

            await stream.WriteAsync(GetClassIdentity<VersionScheme>().ToByteArray(), token);
            token.ThrowIfCancellationRequested();

            if (instance.Type == typeof(BurcatTranslation))
            {
                BurcatTranslation translation = instance.ForceValue<BurcatTranslation>();

                await stream.WriteAsync(GetClassIdentity<RawScheme>().ToByteArray(), token);
                token.ThrowIfCancellationRequested();

                await stream.WriteAsync(translation.ClassID.ToByteArray(), token);
                token.ThrowIfCancellationRequested();

                await stream.WriteAsync(BitConverter.GetBytes(translation.Data.Length), token);
                await stream.WriteAsync(translation.Data, token);
                token.ThrowIfCancellationRequested();

                await stream.WriteAsync(GetClassIdentity<RawScheme>().ToByteArray(), token);
                token.ThrowIfCancellationRequested();
            }
            else if (instance.Value is IBurcatObject objectBDP)
            {
                await stream.WriteAsync(GetClassIdentity<RefinedScheme>().ToByteArray(), token);
                token.ThrowIfCancellationRequested();

                byte[] hasIdentifer = [0];
                if (objectBDP.Identifier != Guid.Empty)
                {
                    if (!await RecieveScheme<InstanceScheme>(stream, token)) throw new InvalidDataException($"Expected scheme with identifier {GetClassIdentity<InstanceScheme>()}, but data read doesn't correspond to");
                    await stream.ReadExactlyAsync(hasIdentifer, token);
                    if (!await RecieveScheme<InstanceScheme>(stream, token)) throw new InvalidDataException($"Expected scheme with identifier {GetClassIdentity<InstanceScheme>()}, but data read doesn't correspond to");
                    token.ThrowIfCancellationRequested();
                }

                if (hasIdentifer[0] == 0)
                {
                    BurcatField[] fields = objectBDP.GetBurcatFields();
                    await stream.WriteAsync(GetClassIdentity<FieldScheme>().ToByteArray(), token);
                    await stream.WriteAsync(BitConverter.GetBytes(fields.Length), token);
                    foreach (BurcatField field in fields)
                    {
                        token.ThrowIfCancellationRequested();
                        await SendObject(stream, field, token);
                        token.ThrowIfCancellationRequested();
                    }
                    await stream.WriteAsync(GetClassIdentity<FieldScheme>().ToByteArray(), token);

                    IBurcatObject?[] values = objectBDP.GetBurcatConstructionValues();
                    await stream.WriteAsync(GetClassIdentity<ConstructorScheme>().ToByteArray(), token);
                    await stream.WriteAsync(BitConverter.GetBytes(values.Length), token);
                    foreach (IBurcatObject? value in values)
                    {
                        token.ThrowIfCancellationRequested();

                        if (value is null) await SendObject(stream, NothingChart.Instance, token);
                        else await SendObject(stream, new(value), token);

                        token.ThrowIfCancellationRequested();
                    }
                    await stream.WriteAsync(GetClassIdentity<ConstructorScheme>().ToByteArray(), token);
                }

                await stream.WriteAsync(GetClassIdentity<RefinedScheme>().ToByteArray(), token);
                token.ThrowIfCancellationRequested();
            }

            token.ThrowIfCancellationRequested();
        }
        private static Task SendObject<T>(IdentifiedStream stream, T? objectBDP, CancellationToken token) where T : IBurcatObject => SendObject(stream, BurcatInstance.Build(objectBDP), token);

        private static async Task<Guid> RecieveScheme(IdentifiedStream stream, CancellationToken token)
        {
            byte[] version = new byte[16];
            await stream.ReadExactlyAsync(version, token);
            token.ThrowIfCancellationRequested();

            return new(version);
        }
        private static async Task<bool> RecieveScheme<T>(IdentifiedStream stream, CancellationToken token) => await RecieveScheme(stream, token) == GetClassIdentity<T>();

        private static async Task<BurcatInstance> RecieveObject(IdentifiedStream stream, CancellationToken token)
        {
            if (!await RecieveScheme<StreamScheme>(stream, token)) throw new InvalidDataException($"Expected scheme with identifier {GetClassIdentity<VersionScheme>()}, but data read doesn't correspond to");
            token.ThrowIfCancellationRequested();

            byte[] guid = new byte[16];
            await stream.ReadExactlyAsync(guid, token); Guid streamID = new(guid);
            token.ThrowIfCancellationRequested();

            if (!await RecieveScheme<StreamScheme>(stream, token)) throw new InvalidDataException($"Expected scheme with identifier {GetClassIdentity<VersionScheme>()}, but data read doesn't correspond to");
            token.ThrowIfCancellationRequested();

            if (!await RecieveScheme<VersionScheme>(stream, token)) throw new InvalidDataException($"Expected scheme with identifier {GetClassIdentity<VersionScheme>()}, but data read doesn't correspond to");
            token.ThrowIfCancellationRequested();

            await stream.ReadExactlyAsync(guid, token); Guid classID = new(guid);
            token.ThrowIfCancellationRequested();

            await stream.ReadExactlyAsync(guid, token); Guid objectID = new(guid);
            token.ThrowIfCancellationRequested();

            if (!await RecieveScheme<VersionScheme>(stream, token)) throw new InvalidDataException($"Expected scheme with identifier {GetClassIdentity<VersionScheme>()}, but data read doesn't correspond to");

            if (AcceptedClasses.TryGetValue(classID, out Type? referenceType))
            {
                if (objectID == Guid.AllBitsSet) return new(referenceType, null);
                else
                {
                    byte[] length = new byte[4];

                    Guid scheme = await RecieveScheme(stream, token);
                    if (scheme == GetClassIdentity<RawScheme>())
                    {
                        await stream.ReadExactlyAsync(guid, token); Guid translationID = new(guid);
                        token.ThrowIfCancellationRequested();

                        await stream.ReadExactlyAsync(length, token);
                        token.ThrowIfCancellationRequested();

                        byte[] data = new byte[BitConverter.ToInt32(length)];
                        await stream.ReadExactlyAsync(data, token);
                        token.ThrowIfCancellationRequested();

                        if (!await RecieveScheme<RawScheme>(stream, token)) throw new InvalidDataException($"Expected scheme with identifier {GetClassIdentity<RawScheme>()}, but data read doesn't correspond to");
                        return new(typeof(BurcatTranslation), new BurcatTranslation(translationID, data));
                    }
                    else if (scheme == GetClassIdentity<RefinedScheme>())
                    {
                        IBurcatObject? reference;
                        if (objectID != Guid.Empty)
                        {
                            reference = InternalProvider.GetObject(stream.Identifier, referenceType, objectID);
                            if (reference is null && ExternalProvider is not null) reference = await ExternalProvider.GetObject(stream.Identifier, referenceType, objectID, token);
                        }
                        else reference = null;

                        if (reference is null)
                        {
                            if (objectID != Guid.Empty)
                            {
                                await stream.WriteAsync(GetClassIdentity<InstanceScheme>().ToByteArray(), token);
                                await stream.WriteAsync(BitConverter.GetBytes(false), token);
                                await stream.WriteAsync(GetClassIdentity<InstanceScheme>().ToByteArray(), token);
                                token.ThrowIfCancellationRequested();
                            }

                            if (!await RecieveScheme<FieldScheme>(stream, token)) throw new InvalidDataException($"Expected scheme with identifier {GetClassIdentity<FieldScheme>()}, but data read doesn't correspond to");
                            await stream.ReadExactlyAsync(length, token);
                            BurcatField[] fieldValues = new BurcatField[BitConverter.ToInt32(length)];
                            for (int i = 0; i < fieldValues.Length; i++)
                            {
                                token.ThrowIfCancellationRequested();
                                fieldValues[i] = (await RecieveObject(stream, token)).ForceValue<BurcatField>();
                                token.ThrowIfCancellationRequested();
                            }
                            if (!await RecieveScheme<FieldScheme>(stream, token)) throw new InvalidDataException($"Expected scheme with identifier {GetClassIdentity<FieldScheme>()}, but data read doesn't correspond to");

                            if (!await RecieveScheme<ConstructorScheme>(stream, token)) throw new InvalidDataException($"Expected scheme with identifier {GetClassIdentity<ConstructorScheme>()}, but data read doesn't correspond to");
                            await stream.ReadExactlyAsync(length, token);
                            IBurcatObject?[] constructorValues = new IBurcatObject?[BitConverter.ToInt32(length)];
                            for (int i = 0; i < constructorValues.Length; i++)
                            {
                                token.ThrowIfCancellationRequested();
                                constructorValues[i] = (await RecieveObject(stream, token)).Value;
                                token.ThrowIfCancellationRequested();
                            }
                            if (!await RecieveScheme<ConstructorScheme>(stream, token)) throw new InvalidDataException($"Expected scheme with identifier {GetClassIdentity<ConstructorScheme>()}, but data read doesn't correspond to");

                            reference = InternalProvider.ConstructObject(stream.Identifier, referenceType, objectID, constructorValues, fieldValues);
                        }
                        else
                        {
                            await stream.WriteAsync(GetClassIdentity<InstanceScheme>().ToByteArray(), token);
                            await stream.WriteAsync(BitConverter.GetBytes(true), token);
                            await stream.WriteAsync(GetClassIdentity<InstanceScheme>().ToByteArray(), token);
                        }

                        if (!await RecieveScheme<RefinedScheme>(stream, token)) throw new InvalidDataException($"Expected scheme with identifier {GetClassIdentity<RefinedScheme>()}, but data read doesn't correspond to");
                        token.ThrowIfCancellationRequested();

                        return new(referenceType, reference);
                    }
                    else throw new InvalidDataException("Expected model type scheme, but data read doesn't correspond to");
                }
            }
            else throw new NotSupportedException($"Version with identifier {classID} is not supported.");
        }

        private abstract class Scheme : IBurcatObject
        {
            Guid IBurcatObject.Identifier { get; set => throw new InvalidOperationException(); } = Guid.Empty;

            BurcatField[] IBurcatObject.GetBurcatFields() => [];
            bool IBurcatObject.SetBurcatField(BurcatField field) => false;
            IBurcatObject?[] IBurcatObject.GetBurcatConstructionValues() => [];
        }
        [BurcatIdentity("00000000-0000-0000-0000-000000000001")]
        private sealed class StreamScheme : Scheme { }
        [BurcatIdentity("00000000-0000-0000-0000-000000000002")]
        private sealed class VersionScheme : Scheme { }
        [BurcatIdentity("00000000-0000-0000-0000-000000000003")]
        private sealed class RawScheme : Scheme { }
        [BurcatIdentity("00000000-0000-0000-0000-000000000004")]
        private sealed class RefinedScheme : Scheme { }
        [BurcatIdentity("00000000-0000-0000-0000-000000000005")]
        private sealed class InstanceScheme : Scheme { }
        [BurcatIdentity("00000000-0000-0000-0000-000000000006")]
        private sealed class FieldScheme : Scheme { }
        [BurcatIdentity("00000000-0000-0000-0000-000000000007")]
        private sealed class ConstructorScheme : Scheme { }
        [BurcatIdentity("00000000-0000-0000-0000-000000000008")]
        private sealed class ActionScheme : Scheme { }
        [BurcatIdentity("00000000-0000-0000-0000-000000000009")]
        private sealed class ParameterScheme : Scheme { }
        [BurcatIdentity("00000000-0000-0000-0000-000000000010")]
        private sealed class FieldUpdateScheme : Scheme { }

        [BurcatIdentity("00000000-0000-0000-0000-000000000100")]
        private sealed class BeginObjectSchematic : Scheme { }
        [BurcatIdentity("00000000-0000-0000-0000-000000000110")]
        private sealed class EndObjectSchematic : Scheme { }
        [BurcatIdentity("00000000-0000-0000-0000-000000000101")]
        private sealed class BeginRequestSchematic : Scheme { }
        [BurcatIdentity("00000000-0000-0000-0000-000000000111")]
        private sealed class EndRequestSchematic : Scheme { }
        [BurcatIdentity("00000000-0000-0000-0000-000000000102")]
        private sealed class BeginActionSchematic : Scheme { }
        [BurcatIdentity("00000000-0000-0000-0000-000000000112")]
        private sealed class EndActionSchematic : Scheme { }
        [BurcatIdentity("00000000-0000-0000-0000-000000000103")]
        private sealed class BeginConstructSchematic : Scheme { }
        [BurcatIdentity("00000000-0000-0000-0000-000000000113")]
        private sealed class EndConstructSchematic : Scheme { }
        [BurcatIdentity("00000000-0000-0000-0000-000000000104")]
        private sealed class BeginUpdateSchematic : Scheme { }
        [BurcatIdentity("00000000-0000-0000-0000-000000000114")]
        private sealed class EndUpdateSchematic : Scheme { }
        [BurcatIdentity("00000000-0000-0000-0000-000000000105")]
        private sealed class BeginDestructSchematic : Scheme { }
        [BurcatIdentity("00000000-0000-0000-0000-000000000115")]
        private sealed class EndDestructSchematic : Scheme { }
    }
}
