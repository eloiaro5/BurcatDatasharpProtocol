using BurcatProtocol.Providers;
using BurcatProtocol.Transactions;
using System.Collections.Concurrent;
using System.Data;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Numerics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;

namespace BurcatProtocol
{
    /// <summary>
    /// Communicates <see cref="IBurcatObject"/> instances between applications through Burcat protocol streams.
    /// </summary>
    /// <remarks>
    /// This class manages supported Burcat classes and their identities, object sending,
    /// object and revision requests, explicit cache coupling and decoupling, action
    /// requests, received exchange processing, and stream purging after invalid data.
    /// </remarks>
    public static class BurcatChat
    {
        /// <summary>
        /// Gets or sets the default timeout used when an operation is called without a cancellation token.
        /// </summary>
        public static TimeSpan DefaultTimeOut { get; set; } = new(TimeSpan.TicksPerSecond * 5);

        /// <summary>
        /// Gets or sets whether operations on the same identified stream are serialized with a semaphore.
        /// </summary>
        public static bool ControlAsyncAccess { get; set; } = true;

        /// <summary>
        /// Gets or sets whether generated protocol exceptions include CLR stack trace text.
        /// </summary>
        public static bool IncludeStackTraceOnException { get; set; } = false;

        /// <summary>
        /// Tries to get the Burcat class identity for a CLR type.
        /// </summary>
        /// <param name="type">The CLR type to inspect.</param>
        /// <param name="identity">The Burcat class identity when the type is supported.</param>
        /// <returns><see langword="true"/> when an identity was found; otherwise, <see langword="false"/>.</returns>
        public static bool TryGetClassIdentity(Type type, out Guid identity)
        {
            if (type.GetCustomAttribute<BurcatIdentityAttribute>() is BurcatIdentityAttribute identityAttribute) { identity = identityAttribute.Identity; return true; }
            else if (BurcatTranslator.CanTranslate(type, out identity)) return true;
            else { identity = Guid.Empty; return false; }
        }

        /// <summary>
        /// Tries to get the Burcat class identity for a CLR type.
        /// </summary>
        /// <typeparam name="T">The CLR type to inspect.</typeparam>
        /// <param name="identifier">The Burcat class identity when the type is supported.</param>
        /// <returns><see langword="true"/> when an identity was found; otherwise, <see langword="false"/>.</returns>
        public static bool TryGetClassIdentity<T>(out Guid identifier) => TryGetClassIdentity(typeof(T), out identifier);

        /// <summary>
        /// Tries to get the Burcat class identity for an object's runtime type.
        /// </summary>
        /// <param name="objectBDP">The object whose runtime type is inspected.</param>
        /// <param name="identifier">The Burcat class identity when the type is supported.</param>
        /// <returns><see langword="true"/> when an identity was found; otherwise, <see langword="false"/>.</returns>
        public static bool TryGetClassIdentity(object objectBDP, out Guid identifier) => TryGetClassIdentity(objectBDP.GetType(), out identifier);

        /// <summary>
        /// Gets the Burcat class identity for a CLR type.
        /// </summary>
        /// <param name="type">The CLR type to inspect.</param>
        /// <returns>The Burcat class identity.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the type has no Burcat identity or translator.</exception>
        public static Guid GetClassIdentity(Type type)
        {
            if (TryGetClassIdentity(type, out Guid identifier)) return identifier;
            else throw new InvalidOperationException($"To get the BDP class identity, a class needs to be a BDP object and implement {nameof(BurcatIdentityAttribute)}.");
        }

        /// <summary>
        /// Gets the Burcat class identity for a CLR type.
        /// </summary>
        /// <typeparam name="T">The CLR type to inspect.</typeparam>
        /// <returns>The Burcat class identity.</returns>
        public static Guid GetClassIdentity<T>() => GetClassIdentity(typeof(T));

        /// <summary>
        /// Gets the Burcat class identity for an object's runtime type.
        /// </summary>
        /// <param name="objectBDP">The object whose runtime type is inspected.</param>
        /// <returns>The Burcat class identity.</returns>
        public static Guid GetClassIdentity(object objectBDP) => GetClassIdentity(objectBDP.GetType());

        private static SortedDictionary<Guid, Type> AcceptedClasses { get; } = [];

        /// <summary>
        /// Registers a type as accepted by this application for protocol communication.
        /// </summary>
        /// <param name="type">The type to register.</param>
        /// <returns><see langword="true"/> when the class was registered; otherwise, <see langword="false"/>.</returns>
        public static bool AcceptClass(Type type)
        {
            if (TryGetClassIdentity(type, out Guid identity)) return AcceptedClasses.TryAdd(identity, type);
            else return false;
        }

        /// <summary>
        /// Registers all Burcat-identifiable types from an assembly.
        /// </summary>
        /// <param name="assembly">The assembly to scan.</param>
        public static void AcceptClasses(Assembly assembly)
        {
            foreach (Type type in assembly.GetTypes())
                AcceptClass(type);
        }

        /// <summary>
        /// Registers all Burcat-identifiable types from the assemblies loaded in the current application domain.
        /// </summary>
        public static void AcceptClasses()
        {
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
                AcceptClasses(assembly);
        }

        /// <summary>
        /// Determines whether a Burcat class identity is accepted by this application.
        /// </summary>
        /// <param name="identity">The Burcat class identity to test.</param>
        /// <returns><see langword="true"/> when the identity is accepted; otherwise, <see langword="false"/>.</returns>
        public static bool AcceptsClass(Guid identity)
        {
            if (!AcceptedClasses.ContainsKey(GetClassIdentity<NothingChart>())) AcceptClasses(typeof(BurcatChat).Assembly);
            return AcceptedClasses.ContainsKey(identity);
        }

        /// <summary>
        /// Determines whether a CLR type is accepted by this application.
        /// </summary>
        /// <param name="type">The type to test.</param>
        /// <returns><see langword="true"/> when the type is accepted; otherwise, <see langword="false"/>.</returns>
        public static bool AcceptsClass(Type type)
        {
            if (AcceptedClasses.TryGetValue(GetClassIdentity(type), out Type? accepted)) return accepted.IsAssignableFrom(type);
            else return false;
        }

        /// <summary>
        /// Determines whether a CLR type is accepted by this application.
        /// </summary>
        /// <typeparam name="T">The type to test.</typeparam>
        /// <returns><see langword="true"/> when the type is accepted; otherwise, <see langword="false"/>.</returns>
        public static bool AcceptsClass<T>() => AcceptsClass(typeof(T));

        /// <summary>
        /// Determines whether all Burcat-identifiable types in an assembly are accepted by this application.
        /// </summary>
        /// <param name="assembly">The assembly to test.</param>
        /// <returns><see langword="true"/> when all types are accepted; otherwise, <see langword="false"/>.</returns>
        public static bool AcceptsClasses(Assembly assembly)
        {
            foreach (Type type in assembly.GetTypes())
                if (!AcceptsClass(type)) return false;

            return true;
        }

        /// <summary>
        /// Tries to get the CLR type registered for a Burcat class identity.
        /// </summary>
        /// <param name="classID">The Burcat class identity to resolve.</param>
        /// <param name="type">The registered CLR type when found.</param>
        /// <returns><see langword="true"/> when the type was found; otherwise, <see langword="false"/>.</returns>
        public static bool TryGetType(Guid classID, [MaybeNullWhen(false)] out Type type) => AcceptedClasses.TryGetValue(classID, out type);

        /// <summary>
        /// Gets the CLR type registered for a Burcat class identity.
        /// </summary>
        /// <param name="classID">The Burcat class identity to resolve.</param>
        /// <returns>The registered CLR type.</returns>
        /// <exception cref="InvalidOperationException">Thrown when no type is registered for the identity.</exception>
        public static Type GetType(Guid classID)
        {
            if (TryGetType(classID, out Type? type)) return type;
            else throw new InvalidOperationException($"There's no type with the specified identifier.");
        }

        /// <summary>
        /// Gets the CLR types accepted by this application.
        /// </summary>
        /// <returns>The accepted CLR types.</returns>
        public static IEnumerable<Type> GetAcceptedClasses() => AcceptedClasses.Values;

        private static ConcurrentDictionary<Guid, SemaphoreSlim> Semaphores { get; } = [];

        public static BurcatHeaderCollection Headers { get; } = [];

        /// <summary>
        /// Gets or sets the provider used for local object construction, lookup, cache updates, deletes, and actions.
        /// </summary>
        public static IInternalProvider InternalProvider { get; set; } = new NothingProvider();

        /// <summary>
        /// Gets or sets the provider used to forward object operations to an external source.
        /// </summary>
        public static IExternalProvider? ExternalProvider { get; set; }

        public static async Task<BurcatHeaderCollection> TestHeadersAsync(IdentifiedStream stream, CancellationToken? token = null)
        {
            SemaphoreSlim? semaphore = null;

            try
            {
                CancellationToken cancellation = token ?? new CancellationTokenSource(DefaultTimeOut).Token;
                semaphore = await TryWaitSemaphore(stream, cancellation);
                cancellation.ThrowIfCancellationRequested();

                await stream.WriteAsync(GetClassIdentity<BeginCommunicationSchematic>().ToByteArray(), cancellation);
                cancellation.ThrowIfCancellationRequested();

                await stream.WriteAsync(GetClassIdentity<BeginTestHeadersSchematic>().ToByteArray(), cancellation);
                cancellation.ThrowIfCancellationRequested();

                await stream.WriteAsync(GetClassIdentity<StreamScheme>().ToByteArray(), cancellation);
                cancellation.ThrowIfCancellationRequested();

                await stream.WriteAsync(stream.Identifier.ToByteArray(), cancellation);
                cancellation.ThrowIfCancellationRequested();

                await stream.WriteAsync(GetClassIdentity<StreamScheme>().ToByteArray(), cancellation);
                cancellation.ThrowIfCancellationRequested();

                await stream.WriteAsync(GetClassIdentity<HeadersScheme>().ToByteArray(), cancellation);
                cancellation.ThrowIfCancellationRequested();

                await SendObject(stream, Headers, cancellation);
                cancellation.ThrowIfCancellationRequested();

                await stream.WriteAsync(GetClassIdentity<HeadersScheme>().ToByteArray(), cancellation);
                cancellation.ThrowIfCancellationRequested();

                BurcatHeaderCollection result = (await RecieveObject(stream, cancellation)).ForceValue<BurcatHeaderCollection>();
                cancellation.ThrowIfCancellationRequested();

                await stream.WriteAsync(GetClassIdentity<EndTestHeadersSchematic>().ToByteArray(), cancellation);
                cancellation.ThrowIfCancellationRequested();

                await stream.WriteAsync(GetClassIdentity<EndCommunicationSchematic>().ToByteArray(), cancellation);
                cancellation.ThrowIfCancellationRequested();

                await stream.FlushAsync();
                cancellation.ThrowIfCancellationRequested();

                return result;
            }
            finally { semaphore?.Release(); }
        }

        /// <summary>
        /// Advances a stream until the next known protocol ending marker is found.
        /// </summary>
        /// <param name="stream">The identified stream to purge.</param>
        /// <param name="token">The optional cancellation token.</param>
        public static async Task PurgeAsync(IdentifiedStream stream, CancellationToken? token = null)
        {
            SemaphoreSlim? semaphore = null;

            try
            {
                CancellationToken cancellation = token ?? new CancellationTokenSource(DefaultTimeOut).Token;
                semaphore = await TryWaitSemaphore(stream, cancellation);
                cancellation.ThrowIfCancellationRequested();

                byte[] data = new byte[16];
                await stream.ReadExactlyAsync(data, cancellation);
                cancellation.ThrowIfCancellationRequested();

                Guid expected = GetClassIdentity<EndCommunicationSchematic>(), actual = new(data);
                while (actual != expected)
                {
                    byte[] d = new byte[1];
                    await stream.ReadExactlyAsync(d, cancellation);
                    cancellation.ThrowIfCancellationRequested();

                    for (int i = 0; i < 15; i++) data[i] = data[i + 1];
                    data[15] = d[0];
                    actual = new(data);

                    cancellation.ThrowIfCancellationRequested();
                }
            }
            finally { semaphore?.Release(); }
        }

        /// <summary>
        /// Advances a stream until the next known protocol ending marker is found.
        /// </summary>
        /// <param name="stream">The identified stream to purge.</param>
        /// <param name="token">The optional cancellation token.</param>
        public static void Purge(IdentifiedStream stream, CancellationToken? token = null) => PurgeAsync(stream, token).GetAwaiter().GetResult();


        /// <summary>
        /// Sends a Burcat instance through a stream.
        /// </summary>
        /// <param name="stream">The destination stream.</param>
        /// <param name="instance">The instance metadata and value to send.</param>
        /// <param name="token">The optional cancellation token.</param>
        public async static Task SendAsync(IdentifiedStream stream, BurcatInstance instance, CancellationToken? token = null)
        {
            SemaphoreSlim? semaphore = null;

            try
            {
                CancellationToken cancellation = token ?? new CancellationTokenSource(DefaultTimeOut).Token;
                semaphore = await TryWaitSemaphore(stream, cancellation);
                cancellation.ThrowIfCancellationRequested();

                await stream.WriteAsync(GetClassIdentity<BeginCommunicationSchematic>().ToByteArray(), cancellation);
                cancellation.ThrowIfCancellationRequested();

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

                await stream.WriteAsync(GetClassIdentity<EndCommunicationSchematic>().ToByteArray(), cancellation);
                cancellation.ThrowIfCancellationRequested();

                await stream.FlushAsync();
                cancellation.ThrowIfCancellationRequested();
            }
            finally { semaphore?.Release(); }
        }

        /// <summary>
        /// Sends a Burcat object through a stream.
        /// </summary>
        /// <typeparam name="T">The object type.</typeparam>
        /// <param name="stream">The destination stream.</param>
        /// <param name="objectBDP">The object to send.</param>
        /// <param name="token">The optional cancellation token.</param>
        public static Task SendAsync<T>(IdentifiedStream stream, T objectBDP, CancellationToken? token = null) where T : IBurcatObject => SendAsync(stream, new(objectBDP), token);

        /// <summary>
        /// Sends a null Burcat object value for a type through a stream.
        /// </summary>
        /// <typeparam name="T">The object type.</typeparam>
        /// <param name="stream">The destination stream.</param>
        /// <param name="token">The optional cancellation token.</param>
        public static Task SendAsync<T>(IdentifiedStream stream, CancellationToken? token = null) where T : IBurcatObject => SendAsync(stream, BurcatInstance.Build<T>(), token);

        /// <summary>
        /// Sends a Burcat instance through a stream.
        /// </summary>
        /// <param name="stream">The destination stream.</param>
        /// <param name="instance">The instance metadata and value to send.</param>
        /// <param name="token">The optional cancellation token.</param>
        public static void Send(IdentifiedStream stream, BurcatInstance instance, CancellationToken? token = null) => SendAsync(stream, instance, token).GetAwaiter().GetResult();

        /// <summary>
        /// Sends a Burcat object through a stream.
        /// </summary>
        /// <typeparam name="T">The object type.</typeparam>
        /// <param name="stream">The destination stream.</param>
        /// <param name="objectBDP">The object to send.</param>
        /// <param name="token">The optional cancellation token.</param>
        public static void Send<T>(IdentifiedStream stream, T objectBDP, CancellationToken? token = null) where T : IBurcatObject => SendAsync(stream, objectBDP, token).GetAwaiter().GetResult();

        /// <summary>
        /// Sends a null Burcat object value for a type through a stream.
        /// </summary>
        /// <typeparam name="T">The object type.</typeparam>
        /// <param name="stream">The destination stream.</param>
        /// <param name="token">The optional cancellation token.</param>
        public static void Send<T>(IdentifiedStream stream, CancellationToken? token = null) where T : IBurcatObject => SendAsync<T>(stream, token).GetAwaiter().GetResult();

        private static async Task<Guid> RelayRevisionRequestAsync(Guid? streamID, Guid classID, Guid objectID, bool ignoreInternal, CancellationToken? token)
        {
            CancellationToken cancellation = token ?? new CancellationTokenSource(DefaultTimeOut).Token;
            Guid version;

            if (AcceptedClasses.TryGetValue(classID, out Type? type))
            {
                version = ignoreInternal ? Guid.Empty : InternalProvider.GetRevision(streamID, type, objectID);
                if (version == Guid.Empty && ExternalProvider is not null) version = await ExternalProvider.GetRevision(streamID, type, objectID, cancellation);
                cancellation.ThrowIfCancellationRequested();
            }
            else throw new NotSupportedException($"Version with identifier {classID} is not supported.");

            return version;
        }
        /// <summary>
        /// Resolves the current revision for an object reference through the configured providers.
        /// </summary>
        /// <param name="classID">The Burcat class identity of the referenced object.</param>
        /// <param name="objectID">The object reference identity.</param>
        /// <param name="ignoreInternal">Whether to skip the internal provider and query only the external provider.</param>
        /// <param name="token">The optional cancellation token.</param>
        /// <returns>The current revision, or <see cref="Guid.Empty"/> when no revision is available.</returns>
        public static Task<Guid> RelayRevisionRequestAsync(Guid classID, Guid objectID, bool ignoreInternal = false, CancellationToken? token = null) => RelayRevisionRequestAsync(null, classID, objectID, ignoreInternal, token);

        /// <summary>
        /// Resolves the current revision for an object reference through the configured providers.
        /// </summary>
        /// <param name="classID">The Burcat class identity of the referenced object.</param>
        /// <param name="objectID">The object reference identity.</param>
        /// <param name="ignoreInternal">Whether to skip the internal provider and query only the external provider.</param>
        /// <param name="token">The optional cancellation token.</param>
        /// <returns>The current revision, or <see cref="Guid.Empty"/> when no revision is available.</returns>
        public static Guid RelayRevisionRequest(Guid classID, Guid objectID, bool ignoreInternal = false, CancellationToken? token = null) => RelayRevisionRequestAsync(classID, objectID, ignoreInternal, token).GetAwaiter().GetResult();

        /// <summary>
        /// Sends a revision request to another application through a stream.
        /// </summary>
        /// <param name="stream">The stream connected to the remote application.</param>
        /// <param name="classID">The Burcat class identity of the referenced object.</param>
        /// <param name="objectID">The object reference identity.</param>
        /// <param name="token">The optional cancellation token.</param>
        /// <returns>The revision returned by the remote application.</returns>
        public static async Task<Guid> SendRevisionRequestAsync(IdentifiedStream stream, Guid classID, Guid objectID, CancellationToken? token = null)
        {
            SemaphoreSlim? semaphore = null;

            try
            {
                CancellationToken cancellation = token ?? new CancellationTokenSource(DefaultTimeOut).Token;
                semaphore = await TryWaitSemaphore(stream, cancellation);
                cancellation.ThrowIfCancellationRequested();

                await stream.WriteAsync(GetClassIdentity<BeginCommunicationSchematic>().ToByteArray(), cancellation);
                cancellation.ThrowIfCancellationRequested();

                await stream.WriteAsync(GetClassIdentity<BeginRevisionRequestSchematic>().ToByteArray(), cancellation);
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

                if (!await RecieveScheme<RevisionScheme>(stream, cancellation)) throw new InvalidDataException($"Expected scheme with identifier {GetClassIdentity<RevisionScheme>()}, but data read doesn't correspond to.");
                cancellation.ThrowIfCancellationRequested();

                byte[] guid = new byte[16];
                await stream.ReadExactlyAsync(guid, cancellation); Guid revision = new(guid);
                cancellation.ThrowIfCancellationRequested();

                if (!await RecieveScheme<RevisionScheme>(stream, cancellation)) throw new InvalidDataException($"Expected scheme with identifier {GetClassIdentity<RevisionScheme>()}, but data read doesn't correspond to.");
                cancellation.ThrowIfCancellationRequested();

                await stream.WriteAsync(GetClassIdentity<EndRevisionRequestSchematic>().ToByteArray(), cancellation);
                cancellation.ThrowIfCancellationRequested();

                await stream.WriteAsync(GetClassIdentity<EndCommunicationSchematic>().ToByteArray(), cancellation);
                cancellation.ThrowIfCancellationRequested();

                await stream.FlushAsync();
                cancellation.ThrowIfCancellationRequested();

                return revision;
            }
            finally { semaphore?.Release(); }
        }

        /// <summary>
        /// Sends a revision request to another application through a stream.
        /// </summary>
        /// <param name="stream">The stream connected to the remote application.</param>
        /// <param name="classID">The Burcat class identity of the referenced object.</param>
        /// <param name="objectID">The object reference identity.</param>
        /// <param name="token">The optional cancellation token.</param>
        /// <returns>The revision returned by the remote application.</returns>
        public static Guid SendRevisionRequest(IdentifiedStream stream, Guid classID, Guid objectID, CancellationToken? token = null) => SendRevisionRequestAsync(stream, classID, objectID, token).GetAwaiter().GetResult();

        private static async Task<IBurcatObject?> RelayObjectRequestAsync(Guid? streamID, Guid classID, Guid objectID, bool ignoreInternal, CancellationToken? token)
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
        /// <summary>
        /// Resolves an object reference through the configured providers.
        /// </summary>
        /// <param name="classID">The Burcat class identity of the referenced object.</param>
        /// <param name="objectID">The object reference identity.</param>
        /// <param name="ignoreInternal">Whether to skip the internal provider and query only the external provider.</param>
        /// <param name="token">The optional cancellation token.</param>
        /// <returns>The resolved object, or <see langword="null"/> when it is unavailable.</returns>
        public static Task<IBurcatObject?> RelayObjectRequestAsync(Guid classID, Guid objectID, bool ignoreInternal = false, CancellationToken? token = null) => RelayObjectRequestAsync(null, classID, objectID, ignoreInternal, token);

        /// <summary>
        /// Resolves an object reference through the configured providers.
        /// </summary>
        /// <typeparam name="T">The expected object type.</typeparam>
        /// <param name="objectID">The object reference identity.</param>
        /// <param name="ignoreInternal">Whether to skip the internal provider and query only the external provider.</param>
        /// <param name="token">The optional cancellation token.</param>
        /// <returns>The resolved object, or <see langword="null"/> when it is unavailable.</returns>
        public static async Task<T?> RelayObjectRequestAsync<T>(Guid objectID, bool ignoreInternal = false, CancellationToken? token = null) where T : IBurcatObject { T? result = (T?)await RelayObjectRequestAsync(GetClassIdentity<T>(), objectID, ignoreInternal, token); return result; }

        /// <summary>
        /// Resolves an object reference through the configured providers.
        /// </summary>
        /// <typeparam name="T">The expected object type.</typeparam>
        /// <param name="objectID">The typed object reference identity.</param>
        /// <param name="ignoreInternal">Whether to skip the internal provider and query only the external provider.</param>
        /// <param name="token">The optional cancellation token.</param>
        /// <returns>The resolved object, or <see langword="null"/> when it is unavailable.</returns>
        public static Task<T?> RelayObjectRequestAsync<T>(BurcatIdentifier<T> objectID, bool ignoreInternal = false, CancellationToken? token = null) where T : IBurcatObject => RelayObjectRequestAsync<T>((Guid)objectID, ignoreInternal, token);

        /// <summary>
        /// Resolves an object reference through the configured providers.
        /// </summary>
        /// <param name="classID">The Burcat class identity of the referenced object.</param>
        /// <param name="objectID">The object reference identity.</param>
        /// <param name="ignoreInternal">Whether to skip the internal provider and query only the external provider.</param>
        /// <param name="token">The optional cancellation token.</param>
        /// <returns>The resolved object, or <see langword="null"/> when it is unavailable.</returns>
        public static IBurcatObject? RelayObjectRequest(Guid classID, Guid objectID, bool ignoreInternal = false, CancellationToken? token = null) => RelayObjectRequestAsync(classID, objectID, ignoreInternal, token).GetAwaiter().GetResult();

        /// <summary>
        /// Resolves an object reference through the configured providers.
        /// </summary>
        /// <typeparam name="T">The expected object type.</typeparam>
        /// <param name="objectID">The object reference identity.</param>
        /// <param name="ignoreInternal">Whether to skip the internal provider and query only the external provider.</param>
        /// <param name="token">The optional cancellation token.</param>
        /// <returns>The resolved object, or <see langword="null"/> when it is unavailable.</returns>
        public static T? RelayObjectRequest<T>(Guid objectID, bool ignoreInternal = false, CancellationToken? token = null) where T : IBurcatObject => RelayObjectRequestAsync<T>(objectID, ignoreInternal, token).GetAwaiter().GetResult();

        /// <summary>
        /// Resolves an object reference through the configured providers.
        /// </summary>
        /// <typeparam name="T">The expected object type.</typeparam>
        /// <param name="objectID">The typed object reference identity.</param>
        /// <param name="ignoreInternal">Whether to skip the internal provider and query only the external provider.</param>
        /// <param name="token">The optional cancellation token.</param>
        /// <returns>The resolved object, or <see langword="null"/> when it is unavailable.</returns>
        public static T? RelayObjectRequest<T>(BurcatIdentifier<T> objectID, bool ignoreInternal = false, CancellationToken? token = null) where T : IBurcatObject => RelayObjectRequestAsync<T>(objectID, ignoreInternal, token).GetAwaiter().GetResult();

        /// <summary>
        /// Sends an object request to another application through a stream.
        /// </summary>
        /// <param name="stream">The stream connected to the remote application.</param>
        /// <param name="classID">The Burcat class identity of the referenced object.</param>
        /// <param name="objectID">The object reference identity.</param>
        /// <param name="token">The optional cancellation token.</param>
        /// <returns>The object returned by the remote application, or <see langword="null"/>.</returns>
        public static async Task<IBurcatObject?> SendObjectRequestAsync(IdentifiedStream stream, Guid classID, Guid objectID, CancellationToken? token = null)
        {
            SemaphoreSlim? semaphore = null;

            try
            {
                CancellationToken cancellation = token ?? new CancellationTokenSource(DefaultTimeOut).Token;
                semaphore = await TryWaitSemaphore(stream, cancellation);
                cancellation.ThrowIfCancellationRequested();

                await stream.WriteAsync(GetClassIdentity<BeginCommunicationSchematic>().ToByteArray(), cancellation);
                cancellation.ThrowIfCancellationRequested();

                await stream.WriteAsync(GetClassIdentity<BeginObjectRequestSchematic>().ToByteArray(), cancellation);
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

                await stream.WriteAsync(GetClassIdentity<EndObjectRequestSchematic>().ToByteArray(), cancellation);
                cancellation.ThrowIfCancellationRequested();

                await stream.WriteAsync(GetClassIdentity<EndCommunicationSchematic>().ToByteArray(), cancellation);
                cancellation.ThrowIfCancellationRequested();

                await stream.FlushAsync();
                cancellation.ThrowIfCancellationRequested();

                return result;
            }
            finally { semaphore?.Release(); }
        }
        /// <summary>
        /// Sends an object request to another application through a stream.
        /// </summary>
        /// <typeparam name="T">The expected object type.</typeparam>
        /// <param name="stream">The stream connected to the remote application.</param>
        /// <param name="objectID">The object reference identity.</param>
        /// <param name="token">The optional cancellation token.</param>
        /// <returns>The object returned by the remote application, or <see langword="null"/>.</returns>
        public static async Task<T?> SendObjectRequestAsync<T>(IdentifiedStream stream, Guid objectID, CancellationToken? token = null) where T : IBurcatObject { T? result = (T?)await SendObjectRequestAsync(stream, GetClassIdentity<T>(), objectID, token); return result; }

        /// <summary>
        /// Sends an object request to another application through a stream.
        /// </summary>
        /// <typeparam name="T">The expected object type.</typeparam>
        /// <param name="stream">The stream connected to the remote application.</param>
        /// <param name="objectID">The typed object reference identity.</param>
        /// <param name="token">The optional cancellation token.</param>
        /// <returns>The object returned by the remote application, or <see langword="null"/>.</returns>
        public static Task<T?> SendObjectRequestAsync<T>(IdentifiedStream stream, BurcatIdentifier<T> objectID, CancellationToken? token = null) where T : IBurcatObject => SendObjectRequestAsync<T>(stream, (Guid)objectID, token);

        /// <summary>
        /// Sends an object request to another application through a stream.
        /// </summary>
        /// <param name="stream">The stream connected to the remote application.</param>
        /// <param name="classID">The Burcat class identity of the referenced object.</param>
        /// <param name="objectID">The object reference identity.</param>
        /// <param name="token">The optional cancellation token.</param>
        /// <returns>The object returned by the remote application, or <see langword="null"/>.</returns>
        public static IBurcatObject? SendObjectRequest(IdentifiedStream stream, Guid classID, Guid objectID, CancellationToken? token = null) => SendObjectRequestAsync(stream, classID, objectID, token).GetAwaiter().GetResult();

        /// <summary>
        /// Sends an object request to another application through a stream.
        /// </summary>
        /// <typeparam name="T">The expected object type.</typeparam>
        /// <param name="stream">The stream connected to the remote application.</param>
        /// <param name="objectID">The object reference identity.</param>
        /// <param name="token">The optional cancellation token.</param>
        /// <returns>The object returned by the remote application, or <see langword="null"/>.</returns>
        public static T? SendObjectRequest<T>(IdentifiedStream stream, Guid objectID, CancellationToken? token = null) where T : IBurcatObject => SendObjectRequestAsync<T>(stream, objectID, token).GetAwaiter().GetResult();

        /// <summary>
        /// Sends an object request to another application through a stream.
        /// </summary>
        /// <typeparam name="T">The expected object type.</typeparam>
        /// <param name="stream">The stream connected to the remote application.</param>
        /// <param name="objectID">The typed object reference identity.</param>
        /// <param name="token">The optional cancellation token.</param>
        /// <returns>The object returned by the remote application, or <see langword="null"/>.</returns>
        public static T? SendObjectRequest<T>(IdentifiedStream stream, BurcatIdentifier<T> objectID, CancellationToken? token = null) where T : IBurcatObject => SendObjectRequestAsync<T>(stream, objectID, token).GetAwaiter().GetResult();

        private static async Task<BurcatException?> RelayCoupleAsync(Guid? streamID, BurcatInstance instance, bool ignoreInternal, CancellationToken? token)
        {
            if (instance.Value is null) throw new InvalidOperationException("Cannot couple a null object.");
            else
            {
                CancellationToken cancellation = token ?? new CancellationTokenSource(DefaultTimeOut).Token;
                cancellation.ThrowIfCancellationRequested();

                if (ignoreInternal && ExternalProvider is null) return new("No external provider is configured.");
                else if (ignoreInternal && ExternalProvider is not null) return await ExternalProvider.CoupleCache(streamID, instance.Value, true, cancellation);
                else if (ExternalProvider is not null) return InternalProvider.CoupleCache(streamID, instance.Value, true) ?? await ExternalProvider.CoupleCache(streamID, instance.Value, true, cancellation);
                else return InternalProvider.CoupleCache(streamID, instance.Value, true);
            }
        }

        /// <summary>
        /// Requests that the configured providers add or update a Burcat instance in cache or storage.
        /// </summary>
        /// <param name="instance">The instance metadata and value to couple.</param>
        /// <param name="ignoreInternal">Whether to skip the internal provider and send only to the external provider.</param>
        /// <param name="token">The optional cancellation token.</param>
        /// <returns><see langword="null"/> on success; otherwise, the exception reported by a provider.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the instance has a <see langword="null"/> value.</exception>
        public static Task<BurcatException?> RelayCoupleAsync(BurcatInstance instance, bool ignoreInternal = false, CancellationToken? token = null) => RelayCoupleAsync(null, instance, ignoreInternal, token);

        /// <summary>
        /// Requests that the configured providers add or update an object in cache or storage.
        /// </summary>
        /// <typeparam name="T">The object type.</typeparam>
        /// <param name="objectBDP">The object to couple.</param>
        /// <param name="ignoreInternal">Whether to skip the internal provider and send only to the external provider.</param>
        /// <param name="token">The optional cancellation token.</param>
        /// <returns><see langword="null"/> on success; otherwise, the exception reported by a provider.</returns>
        public static Task<BurcatException?> RelayCoupleAsync<T>(T objectBDP, bool ignoreInternal = false, CancellationToken? token = null) where T : IBurcatObject => RelayCoupleAsync(null, BurcatInstance.Build(objectBDP), ignoreInternal, token);

        /// <summary>
        /// Requests that the configured providers add or update a Burcat instance in cache or storage.
        /// </summary>
        /// <param name="instance">The instance metadata and value to couple.</param>
        /// <param name="ignoreInternal">Whether to skip the internal provider and send only to the external provider.</param>
        /// <param name="token">The optional cancellation token.</param>
        /// <returns><see langword="null"/> on success; otherwise, the exception reported by a provider.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the instance has a <see langword="null"/> value.</exception>
        public static BurcatException? RelayCouple(BurcatInstance instance, bool ignoreInternal = false, CancellationToken? token = null) => RelayCoupleAsync(instance, ignoreInternal, token).GetAwaiter().GetResult();

        /// <summary>
        /// Requests that the configured providers add or update an object in cache or storage.
        /// </summary>
        /// <typeparam name="T">The object type.</typeparam>
        /// <param name="objectBDP">The object to couple.</param>
        /// <param name="ignoreInternal">Whether to skip the internal provider and send only to the external provider.</param>
        /// <param name="token">The optional cancellation token.</param>
        /// <returns><see langword="null"/> on success; otherwise, the exception reported by a provider.</returns>
        public static BurcatException? RelayCouple<T>(T objectBDP, bool ignoreInternal = false, CancellationToken? token = null) where T : IBurcatObject => RelayCoupleAsync(objectBDP, ignoreInternal, token).GetAwaiter().GetResult();

        /// <summary>
        /// Sends an explicit cache add or update request for a Burcat instance to another application.
        /// </summary>
        /// <param name="stream">The stream connected to the remote application.</param>
        /// <param name="instance">The instance metadata and value to couple.</param>
        /// <param name="token">The optional cancellation token.</param>
        /// <returns><see langword="null"/> on success; otherwise, the exception returned by the remote application.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the instance has a <see langword="null"/> value.</exception>
        public static async Task<BurcatException?> SendCoupleAsync(IdentifiedStream stream, BurcatInstance instance, CancellationToken? token = null)
        {
            if (instance.Value is null) throw new InvalidOperationException("Cannot couple a null object.");
            else
            {
                SemaphoreSlim? semaphore = null;

                try
                {
                    CancellationToken cancellation = token ?? new CancellationTokenSource(DefaultTimeOut).Token;
                    semaphore = await TryWaitSemaphore(stream, cancellation);
                    cancellation.ThrowIfCancellationRequested();

                    await stream.WriteAsync(GetClassIdentity<BeginCommunicationSchematic>().ToByteArray(), cancellation);
                    cancellation.ThrowIfCancellationRequested();

                    await stream.WriteAsync(GetClassIdentity<BeginCoupleSchematic>().ToByteArray(), cancellation);
                    cancellation.ThrowIfCancellationRequested();

                    await stream.WriteAsync(GetClassIdentity<StreamScheme>().ToByteArray(), cancellation);
                    cancellation.ThrowIfCancellationRequested();

                    await stream.WriteAsync(stream.Identifier.ToByteArray(), cancellation);
                    cancellation.ThrowIfCancellationRequested();

                    await stream.WriteAsync(GetClassIdentity<StreamScheme>().ToByteArray(), cancellation);
                    cancellation.ThrowIfCancellationRequested();

                    await SendObject(stream, instance, cancellation);
                    cancellation.ThrowIfCancellationRequested();

                    BurcatException? result = (await RecieveObject(stream, cancellation)).ForceValue<BurcatException?>();
                    cancellation.ThrowIfCancellationRequested();

                    await stream.WriteAsync(GetClassIdentity<EndCoupleSchematic>().ToByteArray(), cancellation);
                    cancellation.ThrowIfCancellationRequested();

                    await stream.WriteAsync(GetClassIdentity<EndCommunicationSchematic>().ToByteArray(), cancellation);
                    cancellation.ThrowIfCancellationRequested();

                    await stream.FlushAsync();
                    cancellation.ThrowIfCancellationRequested();

                    return result;
                }
                finally { semaphore?.Release(); }
            }
        }

        /// <summary>
        /// Sends an explicit cache add or update request to another application.
        /// </summary>
        /// <typeparam name="T">The object type.</typeparam>
        /// <param name="stream">The stream connected to the remote application.</param>
        /// <param name="objectBDP">The object to couple.</param>
        /// <param name="token">The optional cancellation token.</param>
        /// <returns><see langword="null"/> on success; otherwise, the exception returned by the remote application.</returns>
        public static Task<BurcatException?> SendCoupleAsync<T>(IdentifiedStream stream, T objectBDP, CancellationToken? token = null) where T : IBurcatObject => SendCoupleAsync(stream, BurcatInstance.Build(objectBDP), token);

        /// <summary>
        /// Sends an explicit cache add or update request for a Burcat instance to another application.
        /// </summary>
        /// <param name="stream">The stream connected to the remote application.</param>
        /// <param name="instance">The instance metadata and value to couple.</param>
        /// <param name="token">The optional cancellation token.</param>
        /// <returns><see langword="null"/> on success; otherwise, the exception returned by the remote application.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the instance has a <see langword="null"/> value.</exception>
        public static BurcatException? SendCouple(IdentifiedStream stream, BurcatInstance instance, CancellationToken? token = null) => SendCoupleAsync(stream, instance, token).GetAwaiter().GetResult();

        /// <summary>
        /// Sends an explicit cache add or update request to another application.
        /// </summary>
        /// <typeparam name="T">The object type.</typeparam>
        /// <param name="stream">The stream connected to the remote application.</param>
        /// <param name="objectBDP">The object to couple.</param>
        /// <param name="token">The optional cancellation token.</param>
        /// <returns><see langword="null"/> on success; otherwise, the exception returned by the remote application.</returns>
        public static BurcatException? SendCouple<T>(IdentifiedStream stream, T objectBDP, CancellationToken? token = null) where T : IBurcatObject => SendCoupleAsync(stream, objectBDP, token).GetAwaiter().GetResult();

        private static async Task<BurcatException?> RelayDecoupleAsync(Guid? streamID, BurcatInstance instance, bool ignoreInternal, CancellationToken? token)
        {
            if (instance.Value is null) throw new InvalidOperationException("Cannot couple a null object.");
            else
            {
                CancellationToken cancellation = token ?? new CancellationTokenSource(DefaultTimeOut).Token;
                cancellation.ThrowIfCancellationRequested();

                if (ignoreInternal && ExternalProvider is null) return new("No external provider is configured.");
                else if (ignoreInternal && ExternalProvider is not null) return await ExternalProvider.DecoupleCache(streamID, instance.Value, cancellation);
                else if (ExternalProvider is not null) return InternalProvider.DecoupleCache(streamID, instance.Value) ?? await ExternalProvider.DecoupleCache(streamID, instance.Value, cancellation);
                else return InternalProvider.DecoupleCache(streamID, instance.Value);
            }
        }

        /// <summary>
        /// Requests that the configured providers delete a Burcat instance from cache or storage.
        /// </summary>
        /// <param name="instance">The instance metadata and value to decouple.</param>
        /// <param name="ignoreInternal">Whether to skip the internal provider and send only to the external provider.</param>
        /// <param name="token">The optional cancellation token.</param>
        /// <returns><see langword="null"/> on success; otherwise, the exception reported by a provider.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the instance has a <see langword="null"/> value.</exception>
        public static Task<BurcatException?> RelayDecoupleAsync(BurcatInstance instance, bool ignoreInternal = false, CancellationToken? token = null) => RelayDecoupleAsync(null, instance, ignoreInternal, token);

        /// <summary>
        /// Requests that the configured providers delete an object from cache or storage.
        /// </summary>
        /// <typeparam name="T">The object type.</typeparam>
        /// <param name="objectBDP">The object to decouple.</param>
        /// <param name="ignoreInternal">Whether to skip the internal provider and send only to the external provider.</param>
        /// <param name="token">The optional cancellation token.</param>
        /// <returns><see langword="null"/> on success; otherwise, the exception reported by a provider.</returns>
        public static Task<BurcatException?> RelayDecoupleAsync<T>(T objectBDP, bool ignoreInternal = false, CancellationToken? token = null) where T : IBurcatObject => RelayDecoupleAsync(null, BurcatInstance.Build(objectBDP), ignoreInternal, token);

        /// <summary>
        /// Requests that the configured providers delete a Burcat instance from cache or storage.
        /// </summary>
        /// <param name="instance">The instance metadata and value to decouple.</param>
        /// <param name="ignoreInternal">Whether to skip the internal provider and send only to the external provider.</param>
        /// <param name="token">The optional cancellation token.</param>
        /// <returns><see langword="null"/> on success; otherwise, the exception reported by a provider.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the instance has a <see langword="null"/> value.</exception>
        public static BurcatException? RelayDecouple(BurcatInstance instance, bool ignoreInternal = false, CancellationToken? token = null) => RelayDecoupleAsync(instance, ignoreInternal, token).GetAwaiter().GetResult();

        /// <summary>
        /// Requests that the configured providers delete an object from cache or storage.
        /// </summary>
        /// <typeparam name="T">The object type.</typeparam>
        /// <param name="objectBDP">The object to decouple.</param>
        /// <param name="ignoreInternal">Whether to skip the internal provider and send only to the external provider.</param>
        /// <param name="token">The optional cancellation token.</param>
        /// <returns><see langword="null"/> on success; otherwise, the exception reported by a provider.</returns>
        public static BurcatException? RelayDecouple<T>(T objectBDP, bool ignoreInternal = false, CancellationToken? token = null) where T : IBurcatObject => RelayDecoupleAsync(objectBDP, ignoreInternal, token).GetAwaiter().GetResult();

        /// <summary>
        /// Sends an explicit cache delete request for a Burcat instance to another application.
        /// </summary>
        /// <param name="stream">The stream connected to the remote application.</param>
        /// <param name="instance">The instance metadata and value to decouple.</param>
        /// <param name="token">The optional cancellation token.</param>
        /// <returns><see langword="null"/> on success; otherwise, the exception returned by the remote application.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the instance has a <see langword="null"/> value.</exception>
        public static async Task<BurcatException?> SendDecoupleAsync(IdentifiedStream stream, BurcatInstance instance, CancellationToken? token = null)
        {
            if (instance.Value is null) throw new InvalidOperationException("Cannot decouple a null object.");
            else
            {
                SemaphoreSlim? semaphore = null;

                try
                {
                    CancellationToken cancellation = token ?? new CancellationTokenSource(DefaultTimeOut).Token;
                    semaphore = await TryWaitSemaphore(stream, cancellation);
                    cancellation.ThrowIfCancellationRequested();

                    await stream.WriteAsync(GetClassIdentity<BeginCommunicationSchematic>().ToByteArray(), cancellation);
                    cancellation.ThrowIfCancellationRequested();

                    await stream.WriteAsync(GetClassIdentity<BeginDecoupleSchematic>().ToByteArray(), cancellation);
                    cancellation.ThrowIfCancellationRequested();

                    await stream.WriteAsync(GetClassIdentity<StreamScheme>().ToByteArray(), cancellation);
                    cancellation.ThrowIfCancellationRequested();

                    await stream.WriteAsync(stream.Identifier.ToByteArray(), cancellation);
                    cancellation.ThrowIfCancellationRequested();

                    await stream.WriteAsync(GetClassIdentity<StreamScheme>().ToByteArray(), cancellation);
                    cancellation.ThrowIfCancellationRequested();

                    await SendObject(stream, instance, cancellation);
                    cancellation.ThrowIfCancellationRequested();

                    BurcatException? result = (await RecieveObject(stream, cancellation)).ForceValue<BurcatException?>();
                    cancellation.ThrowIfCancellationRequested();

                    await stream.WriteAsync(GetClassIdentity<EndDecoupleSchematic>().ToByteArray(), cancellation);
                    cancellation.ThrowIfCancellationRequested();

                    await stream.WriteAsync(GetClassIdentity<EndCommunicationSchematic>().ToByteArray(), cancellation);
                    cancellation.ThrowIfCancellationRequested();

                    await stream.FlushAsync();
                    cancellation.ThrowIfCancellationRequested();

                    return result;
                }
                finally { semaphore?.Release(); }
            }
        }

        /// <summary>
        /// Sends an explicit cache delete request to another application.
        /// </summary>
        /// <typeparam name="T">The object type.</typeparam>
        /// <param name="stream">The stream connected to the remote application.</param>
        /// <param name="objectBDP">The object to decouple.</param>
        /// <param name="token">The optional cancellation token.</param>
        /// <returns><see langword="null"/> on success; otherwise, the exception returned by the remote application.</returns>
        public static Task<BurcatException?> SendDecoupleAsync<T>(IdentifiedStream stream, T objectBDP, CancellationToken? token = null) where T : IBurcatObject => SendDecoupleAsync(stream, BurcatInstance.Build(objectBDP), token);

        /// <summary>
        /// Sends an explicit cache delete request for a Burcat instance to another application.
        /// </summary>
        /// <param name="stream">The stream connected to the remote application.</param>
        /// <param name="instance">The instance metadata and value to decouple.</param>
        /// <param name="token">The optional cancellation token.</param>
        /// <returns><see langword="null"/> on success; otherwise, the exception returned by the remote application.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the instance has a <see langword="null"/> value.</exception>
        public static BurcatException? SendDecouple(IdentifiedStream stream, BurcatInstance instance, CancellationToken? token = null) => SendDecoupleAsync(stream, instance, token).GetAwaiter().GetResult();

        /// <summary>
        /// Sends an explicit cache delete request to another application.
        /// </summary>
        /// <typeparam name="T">The object type.</typeparam>
        /// <param name="stream">The stream connected to the remote application.</param>
        /// <param name="objectBDP">The object to decouple.</param>
        /// <param name="token">The optional cancellation token.</param>
        /// <returns><see langword="null"/> on success; otherwise, the exception returned by the remote application.</returns>
        public static BurcatException? SendDecouple<T>(IdentifiedStream stream, T objectBDP, CancellationToken? token = null) where T : IBurcatObject => SendDecoupleAsync(stream, objectBDP, token).GetAwaiter().GetResult();

        private static async Task<ActionResult> RelayActionAsync(Guid? streamID, BurcatInstance instance, string action, object?[]? parameters, bool ignoreInternal, CancellationToken? token)
        {
            if (action.Length != 0 && !char.IsLetterOrDigit(action[0])) throw new ArgumentException("An action must start with a letter or number", nameof(action));
            else
            {
                CancellationToken cancellation = token ?? new CancellationTokenSource(DefaultTimeOut).Token;

                ActionResult result = ignoreInternal ? ActionResult.Unsuccessful : InternalProvider.ExecuteAction(streamID, instance.Type, instance.Value, action, parameters);
                cancellation.ThrowIfCancellationRequested();

                if (!result.SuccessfulExecution && ExternalProvider is not null) result = await ExternalProvider.ExecuteAction(streamID, instance.Type, instance.Value, action, parameters, cancellation);
                cancellation.ThrowIfCancellationRequested();

                return result;
            }
        }
        /// <summary>
        /// Executes an action through the configured providers.
        /// </summary>
        /// <param name="instance">The target instance or type-level action target.</param>
        /// <param name="action">The protocol-visible action name.</param>
        /// <param name="parameters">The action parameters.</param>
        /// <param name="ignoreInternal">Whether to skip the internal provider and query only the external provider.</param>
        /// <param name="token">The optional cancellation token.</param>
        /// <returns>The action result.</returns>
        public static Task<ActionResult> RelayActionAsync(BurcatInstance instance, string action, object?[]? parameters = null, bool ignoreInternal = false, CancellationToken? token = null) => RelayActionAsync(null, instance, action, parameters, ignoreInternal, token);

        /// <summary>
        /// Executes an instance action through the configured providers.
        /// </summary>
        /// <typeparam name="T">The target object type.</typeparam>
        /// <param name="objectBDP">The target object.</param>
        /// <param name="action">The protocol-visible action name.</param>
        /// <param name="parameters">The action parameters.</param>
        /// <param name="ignoreInternal">Whether to skip the internal provider and query only the external provider.</param>
        /// <param name="token">The optional cancellation token.</param>
        /// <returns>The action result.</returns>
        public static Task<ActionResult> RelayActionAsync<T>(T objectBDP, string action, object?[]? parameters = null, bool ignoreInternal = false, CancellationToken? token = null) where T : IBurcatObject => RelayActionAsync(null, new(objectBDP), action, parameters, ignoreInternal, token);

        /// <summary>
        /// Executes a type-level action through the configured providers.
        /// </summary>
        /// <typeparam name="T">The target object type.</typeparam>
        /// <param name="action">The protocol-visible action name.</param>
        /// <param name="parameters">The action parameters.</param>
        /// <param name="ignoreInternal">Whether to skip the internal provider and query only the external provider.</param>
        /// <param name="token">The optional cancellation token.</param>
        /// <returns>The action result.</returns>
        public static Task<ActionResult> RelayActionAsync<T>(string action, object?[]? parameters = null, bool ignoreInternal = false, CancellationToken? token = null) where T : IBurcatObject => RelayActionAsync(null, BurcatInstance.Build<T>(), action, parameters, ignoreInternal, token);

        /// <summary>
        /// Executes an action through the configured providers.
        /// </summary>
        /// <param name="instance">The target instance or type-level action target.</param>
        /// <param name="action">The protocol-visible action name.</param>
        /// <param name="parameters">The action parameters.</param>
        /// <param name="ignoreInternal">Whether to skip the internal provider and query only the external provider.</param>
        /// <param name="token">The optional cancellation token.</param>
        /// <returns>The action result.</returns>
        public static ActionResult RelayAction(BurcatInstance instance, string action, object?[]? parameters = null, bool ignoreInternal = false, CancellationToken? token = null) => RelayActionAsync(instance, action, parameters, ignoreInternal, token).GetAwaiter().GetResult();

        /// <summary>
        /// Executes an instance action through the configured providers.
        /// </summary>
        /// <typeparam name="T">The target object type.</typeparam>
        /// <param name="objectBDP">The target object.</param>
        /// <param name="action">The protocol-visible action name.</param>
        /// <param name="parameters">The action parameters.</param>
        /// <param name="ignoreInternal">Whether to skip the internal provider and query only the external provider.</param>
        /// <param name="token">The optional cancellation token.</param>
        /// <returns>The action result.</returns>
        public static ActionResult RelayAction<T>(T objectBDP, string action, object?[]? parameters = null, bool ignoreInternal = false, CancellationToken? token = null) where T : IBurcatObject => RelayAction(new(objectBDP), action, parameters, ignoreInternal, token);

        /// <summary>
        /// Executes a type-level action through the configured providers.
        /// </summary>
        /// <typeparam name="T">The target object type.</typeparam>
        /// <param name="action">The protocol-visible action name.</param>
        /// <param name="parameters">The action parameters.</param>
        /// <param name="ignoreInternal">Whether to skip the internal provider and query only the external provider.</param>
        /// <param name="token">The optional cancellation token.</param>
        /// <returns>The action result.</returns>
        public static ActionResult RelayAction<T>(string action, object?[]? parameters = null, bool ignoreInternal = false, CancellationToken? token = null) where T : IBurcatObject => RelayAction(BurcatInstance.Build<T>(), action, parameters, ignoreInternal, token);


        /// <summary>
        /// Sends an action request to another application through a stream.
        /// </summary>
        /// <param name="stream">The stream connected to the remote application.</param>
        /// <param name="instance">The target instance or type-level action target.</param>
        /// <param name="action">The protocol-visible action name.</param>
        /// <param name="parameters">The action parameters.</param>
        /// <param name="token">The optional cancellation token.</param>
        /// <returns>The action result returned by the remote application.</returns>
        public static async Task<ActionResult> SendActionAsync(IdentifiedStream stream, BurcatInstance instance, string action, object?[]? parameters = null, CancellationToken? token = null)
        {
            IBurcatObject?[] burcatParameters = BurcatTranslator.ObjectsTranslate(parameters);

            if (action.Length != 0 && !char.IsLetterOrDigit(action[0])) throw new ArgumentException("An action must start with a letter or number", nameof(action));
            else
            {
                SemaphoreSlim? semaphore = null;

                try
                {
                    CancellationToken cancellation = token ?? new CancellationTokenSource(DefaultTimeOut).Token;
                    semaphore = await TryWaitSemaphore(stream, cancellation);
                    cancellation.ThrowIfCancellationRequested();

                    await stream.WriteAsync(GetClassIdentity<BeginCommunicationSchematic>().ToByteArray(), cancellation);
                    cancellation.ThrowIfCancellationRequested();

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
                    await stream.WriteAsync(BitConverter.GetBytes(burcatParameters.Length), cancellation);
                    foreach (IBurcatObject? parameter in burcatParameters)
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

                    await stream.WriteAsync(GetClassIdentity<EndCommunicationSchematic>().ToByteArray(), cancellation);
                    cancellation.ThrowIfCancellationRequested();

                    await stream.FlushAsync();
                    cancellation.ThrowIfCancellationRequested();

                    return result;
                }
                finally { semaphore?.Release(); }
            }
        }

        /// <summary>
        /// Sends an instance action request to another application through a stream.
        /// </summary>
        /// <typeparam name="T">The target object type.</typeparam>
        /// <param name="stream">The stream connected to the remote application.</param>
        /// <param name="objectBDP">The target object.</param>
        /// <param name="action">The protocol-visible action name.</param>
        /// <param name="parameters">The action parameters.</param>
        /// <param name="token">The optional cancellation token.</param>
        /// <returns>The action result returned by the remote application.</returns>
        public static Task<ActionResult> SendActionAsync<T>(IdentifiedStream stream, T objectBDP, string action, object?[]? parameters = null, CancellationToken? token = null) where T : IBurcatObject => SendActionAsync(stream, new(objectBDP), action, parameters, token);

        /// <summary>
        /// Sends a type-level action request to another application through a stream.
        /// </summary>
        /// <typeparam name="T">The target object type.</typeparam>
        /// <param name="stream">The stream connected to the remote application.</param>
        /// <param name="action">The protocol-visible action name.</param>
        /// <param name="parameters">The action parameters.</param>
        /// <param name="token">The optional cancellation token.</param>
        /// <returns>The action result returned by the remote application.</returns>
        public static Task<ActionResult> SendActionAsync<T>(IdentifiedStream stream, string action, object?[]? parameters = null, CancellationToken? token = null) where T : IBurcatObject => SendActionAsync(stream, BurcatInstance.Build<T>(), action, parameters, token);

        /// <summary>
        /// Sends an action request to another application through a stream.
        /// </summary>
        /// <param name="stream">The stream connected to the remote application.</param>
        /// <param name="instance">The target instance or type-level action target.</param>
        /// <param name="action">The protocol-visible action name.</param>
        /// <param name="parameters">The action parameters.</param>
        /// <param name="token">The optional cancellation token.</param>
        /// <returns>The action result returned by the remote application.</returns>
        public static ActionResult SendAction(IdentifiedStream stream, BurcatInstance instance, string action, object?[]? parameters = null, CancellationToken? token = null) => SendActionAsync(stream, instance, action, parameters, token).GetAwaiter().GetResult();

        /// <summary>
        /// Sends an instance action request to another application through a stream.
        /// </summary>
        /// <typeparam name="T">The target object type.</typeparam>
        /// <param name="stream">The stream connected to the remote application.</param>
        /// <param name="objectBDP">The target object.</param>
        /// <param name="action">The protocol-visible action name.</param>
        /// <param name="parameters">The action parameters.</param>
        /// <param name="token">The optional cancellation token.</param>
        /// <returns>The action result returned by the remote application.</returns>
        public static ActionResult SendAction<T>(IdentifiedStream stream, T objectBDP, string action, object?[]? parameters = null, CancellationToken? token = null) where T : IBurcatObject => SendAction(stream, new(objectBDP), action, parameters, token);

        /// <summary>
        /// Sends a type-level action request to another application through a stream.
        /// </summary>
        /// <typeparam name="T">The target object type.</typeparam>
        /// <param name="stream">The stream connected to the remote application.</param>
        /// <param name="action">The protocol-visible action name.</param>
        /// <param name="parameters">The action parameters.</param>
        /// <param name="token">The optional cancellation token.</param>
        /// <returns>The action result returned by the remote application.</returns>
        public static ActionResult SendAction<T>(IdentifiedStream stream, string action, object?[]? parameters = null, CancellationToken? token = null) where T : IBurcatObject => SendAction(stream, BurcatInstance.Build<T>(), action, parameters, token);

        /// <summary>
        /// Receives and processes the next Burcat protocol exchange from a stream.
        /// </summary>
        /// <param name="stream">The source stream.</param>
        /// <param name="token">The optional cancellation token.</param>
        /// <returns>The result of the received exchange.</returns>
        public static async Task<ExchangeResult> ReceiveAsync(IdentifiedStream stream, CancellationToken? token = null)
        {
            SemaphoreSlim? semaphore = null;

            try
            {
                CancellationToken cancellation = token ?? new CancellationTokenSource(DefaultTimeOut).Token;
                semaphore = await TryWaitSemaphore(stream, cancellation);
                cancellation.ThrowIfCancellationRequested();

                if (!await RecieveScheme<BeginCommunicationSchematic>(stream, cancellation)) throw new InvalidDataException($"Expected scheme with identifier {GetClassIdentity<BeginCommunicationSchematic>()}, but data read doesn't correspond to");
                cancellation.ThrowIfCancellationRequested();

                Guid scheme = await RecieveScheme(stream, cancellation);
                cancellation.ThrowIfCancellationRequested();

                if (scheme == GetClassIdentity<BeginObjectSchematic>())
                {
                    if (!await RecieveScheme<StreamScheme>(stream, cancellation)) throw new InvalidDataException($"Expected scheme with identifier {GetClassIdentity<StreamScheme>()}, but data read doesn't correspond to");
                    cancellation.ThrowIfCancellationRequested();

                    byte[] guid = new byte[16];
                    await stream.ReadExactlyAsync(guid, cancellation); Guid streamID = new(guid);
                    cancellation.ThrowIfCancellationRequested();

                    if (!await RecieveScheme<StreamScheme>(stream, cancellation)) throw new InvalidDataException($"Expected scheme with identifier {GetClassIdentity<StreamScheme>()}, but data read doesn't correspond to");
                    cancellation.ThrowIfCancellationRequested();

                    BurcatInstance instance = await RecieveObject(stream, cancellation);
                    cancellation.ThrowIfCancellationRequested();

                    if (!await RecieveScheme<EndObjectSchematic>(stream, cancellation)) throw new InvalidDataException($"Expected scheme with identifier {GetClassIdentity<EndObjectSchematic>()}, but data read doesn't correspond to");
                    cancellation.ThrowIfCancellationRequested();

                    if (!await RecieveScheme<EndCommunicationSchematic>(stream, cancellation)) throw new InvalidDataException($"Expected scheme with identifier {GetClassIdentity<EndCommunicationSchematic>()}, but data read doesn't correspond to");
                    cancellation.ThrowIfCancellationRequested();

                    await stream.FlushAsync();
                    cancellation.ThrowIfCancellationRequested();

                    return new(BurcatExchangeType.Object, instance);
                }
                else if (scheme == GetClassIdentity<BeginRevisionRequestSchematic>())
                {
                    if (!await RecieveScheme<StreamScheme>(stream, cancellation)) throw new InvalidDataException($"Expected scheme with identifier {GetClassIdentity<StreamScheme>()}, but data read doesn't correspond to");
                    cancellation.ThrowIfCancellationRequested();

                    byte[] guid = new byte[16];
                    await stream.ReadExactlyAsync(guid, cancellation); Guid streamID = new(guid);
                    cancellation.ThrowIfCancellationRequested();

                    if (!await RecieveScheme<StreamScheme>(stream, cancellation)) throw new InvalidDataException($"Expected scheme with identifier {GetClassIdentity<StreamScheme>()}, but data read doesn't correspond to");
                    cancellation.ThrowIfCancellationRequested();

                    if (!await RecieveScheme<VersionScheme>(stream, cancellation)) throw new InvalidDataException($"Expected scheme with identifier {GetClassIdentity<VersionScheme>()}, but data read doesn't correspond to");
                    cancellation.ThrowIfCancellationRequested();

                    await stream.ReadExactlyAsync(guid, cancellation); Guid classID = new(guid);
                    cancellation.ThrowIfCancellationRequested();

                    await stream.ReadExactlyAsync(guid, cancellation); Guid objectID = new(guid);
                    cancellation.ThrowIfCancellationRequested();

                    if (!await RecieveScheme<VersionScheme>(stream, cancellation)) throw new InvalidDataException($"Expected scheme with identifier {GetClassIdentity<VersionScheme>()}, but data read doesn't correspond to");
                    cancellation.ThrowIfCancellationRequested();

                    Guid revision = AcceptedClasses.TryGetValue(classID, out Type? objectType) ? await RelayRevisionRequestAsync(streamID, classID, objectID, false, cancellation) : Guid.Empty;
                    cancellation.ThrowIfCancellationRequested();

                    await stream.WriteAsync(GetClassIdentity<RevisionScheme>().ToByteArray(), cancellation);
                    cancellation.ThrowIfCancellationRequested();

                    await stream.WriteAsync(revision.ToByteArray(), cancellation);
                    cancellation.ThrowIfCancellationRequested();

                    await stream.WriteAsync(GetClassIdentity<RevisionScheme>().ToByteArray(), cancellation);
                    cancellation.ThrowIfCancellationRequested();

                    if (!await RecieveScheme<EndRevisionRequestSchematic>(stream, cancellation)) throw new InvalidDataException($"Expected scheme with identifier {GetClassIdentity<EndObjectRequestSchematic>()}, but data read doesn't correspond to");
                    cancellation.ThrowIfCancellationRequested();

                    if (!await RecieveScheme<EndCommunicationSchematic>(stream, cancellation)) throw new InvalidDataException($"Expected scheme with identifier {GetClassIdentity<EndCommunicationSchematic>()}, but data read doesn't correspond to");
                    cancellation.ThrowIfCancellationRequested();

                    await stream.FlushAsync();
                    cancellation.ThrowIfCancellationRequested();

                    if (objectType is null) return new(BurcatExchangeType.RevisionRequest, BurcatInstance.Build(new UnsupportedBurcatObjectException()));
                    else return new(BurcatExchangeType.RevisionRequest, new(objectType));
                }
                else if(scheme == GetClassIdentity<BeginObjectRequestSchematic>())
                {
                    if (!await RecieveScheme<StreamScheme>(stream, cancellation)) throw new InvalidDataException($"Expected scheme with identifier {GetClassIdentity<StreamScheme>()}, but data read doesn't correspond to");
                    cancellation.ThrowIfCancellationRequested();

                    byte[] guid = new byte[16];
                    await stream.ReadExactlyAsync(guid, cancellation); Guid streamID = new(guid);
                    cancellation.ThrowIfCancellationRequested();

                    if (!await RecieveScheme<StreamScheme>(stream, cancellation)) throw new InvalidDataException($"Expected scheme with identifier {GetClassIdentity<StreamScheme>()}, but data read doesn't correspond to");
                    cancellation.ThrowIfCancellationRequested();

                    if (!await RecieveScheme<VersionScheme>(stream, cancellation)) throw new InvalidDataException($"Expected scheme with identifier {GetClassIdentity<VersionScheme>()}, but data read doesn't correspond to");
                    cancellation.ThrowIfCancellationRequested();

                    await stream.ReadExactlyAsync(guid, cancellation); Guid classID = new(guid);
                    cancellation.ThrowIfCancellationRequested();

                    await stream.ReadExactlyAsync(guid, cancellation); Guid objectID = new(guid);
                    cancellation.ThrowIfCancellationRequested();

                    if (!await RecieveScheme<VersionScheme>(stream, cancellation)) throw new InvalidDataException($"Expected scheme with identifier {GetClassIdentity<VersionScheme>()}, but data read doesn't correspond to");
                    cancellation.ThrowIfCancellationRequested();

                    BurcatInstance instance = AcceptedClasses.TryGetValue(classID, out Type? objectType) ? new(objectType, await RelayObjectRequestAsync(streamID, classID, objectID, false, cancellation)) : BurcatInstance.Build<NothingChart>();
                    cancellation.ThrowIfCancellationRequested();

                    await SendObject(stream, instance, cancellation);
                    cancellation.ThrowIfCancellationRequested();

                    if (!await RecieveScheme<EndObjectRequestSchematic>(stream, cancellation)) throw new InvalidDataException($"Expected scheme with identifier {GetClassIdentity<EndObjectRequestSchematic>()}, but data read doesn't correspond to");
                    cancellation.ThrowIfCancellationRequested();

                    if (!await RecieveScheme<EndCommunicationSchematic>(stream, cancellation)) throw new InvalidDataException($"Expected scheme with identifier {GetClassIdentity<EndCommunicationSchematic>()}, but data read doesn't correspond to");
                    cancellation.ThrowIfCancellationRequested();

                    await stream.FlushAsync();
                    cancellation.ThrowIfCancellationRequested();

                    if (objectType is null) return new(BurcatExchangeType.ObjectRequest, BurcatInstance.Build(new UnsupportedBurcatObjectException()));
                    else return new(BurcatExchangeType.ObjectRequest, new(objectType), instance);
                }
                else if (scheme == GetClassIdentity<BeginCoupleSchematic>())
                {
                    if (!await RecieveScheme<StreamScheme>(stream, cancellation)) throw new InvalidDataException($"Expected scheme with identifier {GetClassIdentity<StreamScheme>()}, but data read doesn't correspond to");
                    cancellation.ThrowIfCancellationRequested();

                    byte[] guid = new byte[16];
                    await stream.ReadExactlyAsync(guid, cancellation); Guid streamID = new(guid);
                    cancellation.ThrowIfCancellationRequested();

                    if (!await RecieveScheme<StreamScheme>(stream, cancellation)) throw new InvalidDataException($"Expected scheme with identifier {GetClassIdentity<StreamScheme>()}, but data read doesn't correspond to");
                    cancellation.ThrowIfCancellationRequested();

                    BurcatInstance instance = await RecieveObject(stream, cancellation);
                    cancellation.ThrowIfCancellationRequested();

                    IBurcatObject reference = instance.Value ?? throw new NullReferenceException("Cannot construct an empty object.");
                    cancellation.ThrowIfCancellationRequested();

                    BurcatException? coupleException = await RelayCoupleAsync(streamID, BurcatInstance.Build(reference), false, cancellation);
                    cancellation.ThrowIfCancellationRequested();

                    await SendObject(stream, coupleException is BurcatException exception ? new(exception) : BurcatInstance.Build<BurcatException>(), cancellation);
                    cancellation.ThrowIfCancellationRequested();

                    if (!await RecieveScheme<EndCoupleSchematic>(stream, cancellation)) throw new InvalidDataException($"Expected scheme with identifier {GetClassIdentity<EndCoupleSchematic>()}, but data read doesn't correspond to");
                    cancellation.ThrowIfCancellationRequested();

                    if (!await RecieveScheme<EndCommunicationSchematic>(stream, cancellation)) throw new InvalidDataException($"Expected scheme with identifier {GetClassIdentity<EndCommunicationSchematic>()}, but data read doesn't correspond to");
                    cancellation.ThrowIfCancellationRequested();

                    await stream.FlushAsync();
                    cancellation.ThrowIfCancellationRequested();

                    return new(BurcatExchangeType.Couple, instance, BurcatInstance.Build(coupleException));
                }
                else if (scheme == GetClassIdentity<BeginDecoupleSchematic>())
                {
                    if (!await RecieveScheme<StreamScheme>(stream, cancellation)) throw new InvalidDataException($"Expected scheme with identifier {GetClassIdentity<StreamScheme>()}, but data read doesn't correspond to");
                    cancellation.ThrowIfCancellationRequested();

                    byte[] guid = new byte[16];
                    await stream.ReadExactlyAsync(guid, cancellation); Guid streamID = new(guid);
                    cancellation.ThrowIfCancellationRequested();

                    if (!await RecieveScheme<StreamScheme>(stream, cancellation)) throw new InvalidDataException($"Expected scheme with identifier {GetClassIdentity<StreamScheme>()}, but data read doesn't correspond to");
                    cancellation.ThrowIfCancellationRequested();

                    BurcatInstance instance = await RecieveObject(stream, cancellation);
                    cancellation.ThrowIfCancellationRequested();

                    IBurcatObject reference = instance.Value ?? throw new NullReferenceException("Cannot construct an empty object.");
                    cancellation.ThrowIfCancellationRequested();

                    BurcatException? decoupleException = await RelayDecoupleAsync(streamID, BurcatInstance.Build(reference), false, cancellation);
                    cancellation.ThrowIfCancellationRequested();

                    await SendObject(stream, decoupleException is BurcatException exception ? new(exception) : BurcatInstance.Build<BurcatException>(), cancellation);
                    cancellation.ThrowIfCancellationRequested();

                    if (!await RecieveScheme<EndDecoupleSchematic>(stream, cancellation)) throw new InvalidDataException($"Expected scheme with identifier {GetClassIdentity<EndDecoupleSchematic>()}, but data read doesn't correspond to");
                    cancellation.ThrowIfCancellationRequested();

                    if (!await RecieveScheme<EndCommunicationSchematic>(stream, cancellation)) throw new InvalidDataException($"Expected scheme with identifier {GetClassIdentity<EndCommunicationSchematic>()}, but data read doesn't correspond to");
                    cancellation.ThrowIfCancellationRequested();

                    await stream.FlushAsync();
                    cancellation.ThrowIfCancellationRequested();

                    return new(BurcatExchangeType.Decouple, instance, BurcatInstance.Build(decoupleException));
                }
                else if (scheme == GetClassIdentity<BeginActionSchematic>())
                {
                    if (!await RecieveScheme<StreamScheme>(stream, cancellation)) throw new InvalidDataException($"Expected scheme with identifier {GetClassIdentity<StreamScheme>()}, but data read doesn't correspond to");
                    cancellation.ThrowIfCancellationRequested();

                    byte[] guid = new byte[16];
                    await stream.ReadExactlyAsync(guid, cancellation); Guid streamID = new(guid);
                    cancellation.ThrowIfCancellationRequested();

                    if (!await RecieveScheme<StreamScheme>(stream, cancellation)) throw new InvalidDataException($"Expected scheme with identifier {GetClassIdentity<StreamScheme>()}, but data read doesn't correspond to");
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

                    if (!await RecieveScheme<EndCommunicationSchematic>(stream, cancellation)) throw new InvalidDataException($"Expected scheme with identifier {GetClassIdentity<EndCommunicationSchematic>()}, but data read doesn't correspond to");
                    cancellation.ThrowIfCancellationRequested();

                    await stream.FlushAsync();
                    cancellation.ThrowIfCancellationRequested();

                    return new(BurcatExchangeType.Action, instance, BurcatInstance.Build(result), Encoding.Unicode.GetString(data), parameters);
                }
                else throw new InvalidDataException("No supported scheme");
            }
            finally { semaphore?.Release(); }
        }
        /// <summary>
        /// Receives and processes the next Burcat protocol exchange from a stream.
        /// </summary>
        /// <param name="stream">The source stream.</param>
        /// <param name="token">The optional cancellation token.</param>
        /// <returns>The result of the received exchange.</returns>
        public static ExchangeResult Receive(IdentifiedStream stream, CancellationToken? token = null) => ReceiveAsync(stream, token).GetAwaiter().GetResult();

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
                    await stream.WriteAsync(GetClassIdentity<RevisionScheme>().ToByteArray(), token);
                    token.ThrowIfCancellationRequested();

                    await stream.WriteAsync(objectBDP.Revision.ToByteArray(), token);
                    token.ThrowIfCancellationRequested();

                    await stream.WriteAsync(GetClassIdentity<RevisionScheme>().ToByteArray(), token);
                    token.ThrowIfCancellationRequested();

                    if (!await RecieveScheme<InstanceScheme>(stream, token)) throw new InvalidDataException($"Expected scheme with identifier {GetClassIdentity<InstanceScheme>()}, but data read doesn't correspond to");
                    token.ThrowIfCancellationRequested();

                    await stream.ReadExactlyAsync(hasIdentifer, token);
                    token.ThrowIfCancellationRequested();

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
            token.ThrowIfCancellationRequested();

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
                        Guid revisionID = Guid.Empty;

                        if (objectID != Guid.Empty)
                        {
                            if (!await RecieveScheme<RevisionScheme>(stream, token)) throw new InvalidDataException($"Expected scheme with identifier {GetClassIdentity<RevisionScheme>()}, but data read doesn't correspond to");
                            token.ThrowIfCancellationRequested();

                            await stream.ReadExactlyAsync(guid, token); revisionID = new(guid);
                            token.ThrowIfCancellationRequested();

                            if (!await RecieveScheme<RevisionScheme>(stream, token)) throw new InvalidDataException($"Expected scheme with identifier {GetClassIdentity<RevisionScheme>()}, but data read doesn't correspond to");
                            token.ThrowIfCancellationRequested();

                            reference = InternalProvider.GetObject(streamID, referenceType, objectID) is IBurcatObject objectInternal && objectInternal.Revision == revisionID ? objectInternal : null;
                            if (reference is null && ExternalProvider is not null) reference = (await ExternalProvider.GetObject(streamID, referenceType, objectID, token)) is IBurcatObject objectExternal && objectExternal.Revision == revisionID ? objectExternal : null;
                        }
                        else reference = null;

                        if (reference is null)
                        {
                            if (objectID != Guid.Empty)
                            {
                                await stream.WriteAsync(GetClassIdentity<InstanceScheme>().ToByteArray(), token);
                                token.ThrowIfCancellationRequested();

                                await stream.WriteAsync(BitConverter.GetBytes(false), token);
                                token.ThrowIfCancellationRequested();

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

                            reference = InternalProvider.ConstructObject(streamID, referenceType, objectID, revisionID, constructorValues, fieldValues);
                            token.ThrowIfCancellationRequested();

                            if (reference is not null && objectID != Guid.Empty) InternalProvider.CoupleCache(streamID, reference, false);
                        }
                        else
                        {
                            await stream.WriteAsync(GetClassIdentity<InstanceScheme>().ToByteArray(), token);
                            token.ThrowIfCancellationRequested();

                            await stream.WriteAsync(BitConverter.GetBytes(true), token);
                            token.ThrowIfCancellationRequested();

                            await stream.WriteAsync(GetClassIdentity<InstanceScheme>().ToByteArray(), token);
                            token.ThrowIfCancellationRequested();
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

        private async static Task<SemaphoreSlim?> TryWaitSemaphore(IdentifiedStream stream, CancellationToken token)
        {
            if (ControlAsyncAccess)
            {
                SemaphoreSlim candidate = Semaphores.GetOrAdd(stream.Identifier, static _ => new SemaphoreSlim(1, 1));
                await candidate.WaitAsync(token);
                return candidate;
            }
            else return null;
        }

        private abstract class Scheme : IBurcatObject
        {
            Guid IBurcatObject.Identifier { get; set => throw new InvalidOperationException(); } = Guid.Empty;
            Guid IBurcatObject.Revision { get; set => throw new InvalidOperationException(); } = Guid.Empty;

            BurcatField[] IBurcatObject.GetBurcatFields() => [];
            void IBurcatObject.SetBurcatFields(BurcatField[] fields) { }
            IBurcatObject?[] IBurcatObject.GetBurcatConstructionValues() => [];
        }
        [BurcatIdentity("00000000-0000-0000-0000-000000000001")]
        private sealed class StreamScheme : Scheme { }
        [BurcatIdentity("00000000-0000-0000-0000-000000000002")]
        private sealed class HeadersScheme : Scheme { }
        [BurcatIdentity("00000000-0000-0000-0000-000000000003")]
        private sealed class VersionScheme : Scheme { }
        [BurcatIdentity("00000000-0000-0000-0000-000000000004")]
        private sealed class RevisionScheme : Scheme { }
        [BurcatIdentity("00000000-0000-0000-0000-000000000005")]
        private sealed class RawScheme : Scheme { }
        [BurcatIdentity("00000000-0000-0000-0000-000000000006")]
        private sealed class RefinedScheme : Scheme { }
        [BurcatIdentity("00000000-0000-0000-0000-000000000007")]
        private sealed class InstanceScheme : Scheme { }
        [BurcatIdentity("00000000-0000-0000-0000-000000000008")]
        private sealed class FieldScheme : Scheme { }
        [BurcatIdentity("00000000-0000-0000-0000-000000000009")]
        private sealed class ConstructorScheme : Scheme { }
        [BurcatIdentity("00000000-0000-0000-0000-000000000010")]
        private sealed class ActionScheme : Scheme { }
        [BurcatIdentity("00000000-0000-0000-0000-000000000011")]
        private sealed class ParameterScheme : Scheme { }
        [BurcatIdentity("00000000-0000-0000-0000-000000000012")]
        private sealed class FieldUpdateScheme : Scheme { }

        [BurcatIdentity("00000000-0000-0000-0000-000000001000")]
        private sealed class BeginCommunicationSchematic : Scheme { }
        [BurcatIdentity("00000000-0000-0000-0000-000000001100")]
        private sealed class EndCommunicationSchematic : Scheme { }
        [BurcatIdentity("00000000-0000-0000-0000-000000001001")]
        private sealed class BeginTestHeadersSchematic : Scheme { }
        [BurcatIdentity("00000000-0000-0000-0000-000000001101")]
        private sealed class EndTestHeadersSchematic : Scheme { }
        [BurcatIdentity("00000000-0000-0000-0000-000000001002")]
        private sealed class BeginObjectSchematic : Scheme { }
        [BurcatIdentity("00000000-0000-0000-0000-000000001102")]
        private sealed class EndObjectSchematic : Scheme { }
        [BurcatIdentity("00000000-0000-0000-0000-000000001003")]
        private sealed class BeginRevisionRequestSchematic : Scheme { }
        [BurcatIdentity("00000000-0000-0000-0000-000000001103")]
        private sealed class EndRevisionRequestSchematic : Scheme { }
        [BurcatIdentity("00000000-0000-0000-0000-000000001004")]
        private sealed class BeginObjectRequestSchematic : Scheme { }
        [BurcatIdentity("00000000-0000-0000-0000-000000001104")]
        private sealed class EndObjectRequestSchematic : Scheme { }
        [BurcatIdentity("00000000-0000-0000-0000-000000001005")]
        private sealed class BeginActionSchematic : Scheme { }
        [BurcatIdentity("00000000-0000-0000-0000-000000001105")]
        private sealed class EndActionSchematic : Scheme { }
        [BurcatIdentity("00000000-0000-0000-0000-000000001006")]
        private sealed class BeginCoupleSchematic : Scheme { }
        [BurcatIdentity("00000000-0000-0000-0000-000000001106")]
        private sealed class EndCoupleSchematic : Scheme { }
        [BurcatIdentity("00000000-0000-0000-0000-000000001007")]
        private sealed class BeginDecoupleSchematic : Scheme { }
        [BurcatIdentity("00000000-0000-0000-0000-000000001107")]
        private sealed class EndDecoupleSchematic : Scheme { }
        [BurcatIdentity("00000000-0000-0000-0000-000000001008")]
        private sealed class BeginUpgradeSchematic : Scheme { }
        [BurcatIdentity("00000000-0000-0000-0000-000000001108")]
        private sealed class EndUpgradeSchematic : Scheme { }
    }
}
