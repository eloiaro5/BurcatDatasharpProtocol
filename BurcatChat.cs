using BurcatProtocol.Providers;
using System.Collections.Concurrent;
using System.Data;
using System.Linq.Expressions;
using System.Numerics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Transactions;
using static BurcatProtocol.BurcatHeaderSet;

namespace BurcatProtocol
{
    /// <summary>
    /// Communicates <see cref="IBurcatObject"/> instances and negotiated headers through Burcat protocol streams.
    /// </summary>
    /// <remarks>
    /// This class manages supported Burcat classes and their identities, object sending,
    /// object and revision requests, explicit cache coupling and decoupling, action
    /// requests, header negotiation and forwarding, received exchange processing, and
    /// stream purging after invalid data.
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

        /// <summary>
        /// Gets the Burcat identities and CLR types accepted by this application.
        /// </summary>
        public static BurcatIdentitySet AcceptedIdentities { get; } = [];

        /// <summary>
        /// Gets the application headers included in each header negotiation.
        /// </summary>
        public static BurcatHeaderSet Headers { get; } = [];

        private static ConcurrentDictionary<Guid, SemaphoreSlim> Semaphores { get; } = [];

        /// <summary>
        /// Gets or sets the provider used for header-aware local construction, lookup, cache updates, deletes, and actions.
        /// </summary>
        public static IInternalProvider InternalProvider { get; set; } = new NothingProvider();

        /// <summary>
        /// Gets or sets the provider used to forward object operations and their additional headers to an external source.
        /// </summary>
        public static IExternalProvider? ExternalProvider { get; set; }

        /// <summary>
        /// Asynchronously gets the identities supported by the configured external provider.
        /// </summary>
        /// <param name="token">The optional cancellation token.</param>
        /// <returns>The supported identities, or an empty set when no external provider is configured.</returns>
        public static async Task<BurcatIdentitySet> GetIdentitiesAsync(CancellationToken? token = null) => ExternalProvider is IExternalProvider provider ? await provider.GetIdentities(token ?? new CancellationTokenSource(DefaultTimeOut).Token) : [];

        /// <summary>
        /// Gets the identities supported by the configured external provider.
        /// </summary>
        /// <param name="token">The optional cancellation token.</param>
        /// <returns>The supported identities, or an empty set when no external provider is configured.</returns>
        public static BurcatIdentitySet GetIdentities(CancellationToken? token = null) => GetIdentitiesAsync(token).GetAwaiter().GetResult();

        /// <summary>
        /// Asynchronously requests the identities supported by the remote endpoint of an identified stream.
        /// </summary>
        /// <param name="stream">The stream over which to perform identity negotiation.</param>
        /// <param name="token">The optional cancellation token.</param>
        /// <returns>The identities reported by the remote endpoint.</returns>
        public static async Task<BurcatIdentitySet> GetIdentitiesAsync(IdentifiedStream stream, CancellationToken? token = null)
        {
            SemaphoreSlim? semaphore = null;

            try
            {
                CancellationToken cancellation = token ?? new CancellationTokenSource(DefaultTimeOut).Token;
                semaphore = await TryWaitSemaphore(stream, cancellation);
                cancellation.ThrowIfCancellationRequested();

                await stream.WriteAsync(GetClassIdentity<BeginCommunicationSchematic>().ToByteArray(), cancellation);
                cancellation.ThrowIfCancellationRequested();

                await stream.WriteAsync(GetClassIdentity<BeginIdentitiesSchematic>().ToByteArray(), cancellation);
                cancellation.ThrowIfCancellationRequested();

                BurcatHead head = await ExchangeHead(stream, Headers, cancellation);
                cancellation.ThrowIfCancellationRequested();

                BurcatIdentitySet result = (await RecieveObject(stream, head, cancellation)).ForceValue<BurcatIdentitySet>();
                cancellation.ThrowIfCancellationRequested();

                await stream.WriteAsync(GetClassIdentity<EndIdentitiesSchematic>().ToByteArray(), cancellation);
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
        /// Requests the identities supported by the remote endpoint of an identified stream.
        /// </summary>
        /// <param name="stream">The stream over which to perform identity negotiation.</param>
        /// <param name="token">The optional cancellation token.</param>
        /// <returns>The identities reported by the remote endpoint.</returns>
        public static BurcatIdentitySet GetIdentities(IdentifiedStream stream, CancellationToken? token = null) => GetIdentitiesAsync(stream, token).GetAwaiter().GetResult();

        /// <summary>
        /// Asynchronously gets the headers supported by the configured external provider.
        /// </summary>
        /// <param name="token">The optional cancellation token.</param>
        /// <returns>The supported headers, or an empty collection when no external provider is configured.</returns>
        public static async Task<BurcatHeaderSet> GetHeadersAsync(CancellationToken? token = null) => ExternalProvider is IExternalProvider provider ? await provider.GetHeaders(token ?? new CancellationTokenSource(DefaultTimeOut).Token) : [];

        /// <summary>
        /// Gets the headers supported by the configured external provider.
        /// </summary>
        /// <param name="token">The optional cancellation token.</param>
        /// <returns>The supported headers, or an empty collection when no external provider is configured.</returns>
        public static BurcatHeaderSet GetHeaders(CancellationToken? token = null) => GetHeadersAsync(token).GetAwaiter().GetResult();

        /// <summary>
        /// Asynchronously requests the headers supported by the remote endpoint of an identified stream.
        /// </summary>
        /// <param name="stream">The stream over which to perform header negotiation.</param>
        /// <param name="token">The optional cancellation token.</param>
        /// <returns>The headers reported by the remote endpoint.</returns>
        public static async Task<BurcatHeaderSet> GetHeadersAsync(IdentifiedStream stream, CancellationToken? token = null)
        {
            SemaphoreSlim? semaphore = null;

            try
            {
                CancellationToken cancellation = token ?? new CancellationTokenSource(DefaultTimeOut).Token;
                semaphore = await TryWaitSemaphore(stream, cancellation);
                cancellation.ThrowIfCancellationRequested();

                await stream.WriteAsync(GetClassIdentity<BeginCommunicationSchematic>().ToByteArray(), cancellation);
                cancellation.ThrowIfCancellationRequested();

                await stream.WriteAsync(GetClassIdentity<BeginHeadersSchematic>().ToByteArray(), cancellation);
                cancellation.ThrowIfCancellationRequested();

                BurcatHead head = await ExchangeHead(stream, Headers, cancellation);
                cancellation.ThrowIfCancellationRequested();

                BurcatHeaderSet result = (await RecieveObject(stream, head, cancellation)).ForceValue<BurcatHeaderSet>();
                cancellation.ThrowIfCancellationRequested();

                await stream.WriteAsync(GetClassIdentity<EndHeadersSchematic>().ToByteArray(), cancellation);
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
        /// Requests the headers supported by the remote endpoint of an identified stream.
        /// </summary>
        /// <param name="stream">The stream over which to perform header negotiation.</param>
        /// <param name="token">The optional cancellation token.</param>
        /// <returns>The headers reported by the remote endpoint.</returns>
        public static BurcatHeaderSet GetHeaders(IdentifiedStream stream, CancellationToken? token = null) => GetHeadersAsync(stream, token).GetAwaiter().GetResult();

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
        /// <param name="head">The destination stream and headers to send with the operation.</param>
        /// <param name="instance">The instance metadata and value to send.</param>
        /// <param name="token">The optional cancellation token.</param>
        public async static Task SendAsync(BurcatDirectionalHead head, BurcatInstance instance, CancellationToken? token = null)
        {
            SemaphoreSlim? semaphore = null;

            try
            {
                CancellationToken cancellation = token ?? new CancellationTokenSource(DefaultTimeOut).Token;
                semaphore = await TryWaitSemaphore(head.Stream, cancellation);
                cancellation.ThrowIfCancellationRequested();

                await head.Stream.WriteAsync(GetClassIdentity<BeginCommunicationSchematic>().ToByteArray(), cancellation);
                cancellation.ThrowIfCancellationRequested();

                await head.Stream.WriteAsync(GetClassIdentity<BeginObjectSchematic>().ToByteArray(), cancellation);
                cancellation.ThrowIfCancellationRequested();

                await ExchangeHead(head.Stream, head.Headers, cancellation);
                cancellation.ThrowIfCancellationRequested();

                await SendObject(head.Stream, instance, cancellation);
                cancellation.ThrowIfCancellationRequested();

                await head.Stream.WriteAsync(GetClassIdentity<EndObjectSchematic>().ToByteArray(), cancellation);
                cancellation.ThrowIfCancellationRequested();

                await head.Stream.WriteAsync(GetClassIdentity<EndCommunicationSchematic>().ToByteArray(), cancellation);
                cancellation.ThrowIfCancellationRequested();

                await head.Stream.FlushAsync();
                cancellation.ThrowIfCancellationRequested();
            }
            finally { semaphore?.Release(); }
        }

        /// <summary>
        /// Sends a Burcat object through a stream.
        /// </summary>
        /// <typeparam name="T">The object type.</typeparam>
        /// <param name="head">The destination stream and headers to send with the operation.</param>
        /// <param name="objectBDP">The object to send.</param>
        /// <param name="token">The optional cancellation token.</param>
        public static Task SendAsync<T>(BurcatDirectionalHead head, T objectBDP, CancellationToken? token = null) where T : IBurcatObject => SendAsync(head, new(objectBDP), token);

        /// <summary>
        /// Sends a null Burcat object value for a type through a stream.
        /// </summary>
        /// <typeparam name="T">The object type.</typeparam>
        /// <param name="head">The destination stream and headers to send with the operation.</param>
        /// <param name="token">The optional cancellation token.</param>
        public static Task SendAsync<T>(BurcatDirectionalHead head, CancellationToken? token = null) where T : IBurcatObject => SendAsync(head, BurcatInstance.Build<T>(), token);

        /// <summary>
        /// Sends a Burcat instance through a stream.
        /// </summary>
        /// <param name="head">The destination stream and headers to send with the operation.</param>
        /// <param name="instance">The instance metadata and value to send.</param>
        /// <param name="token">The optional cancellation token.</param>
        public static void Send(BurcatDirectionalHead head, BurcatInstance instance, CancellationToken? token = null) => SendAsync(head, instance, token).GetAwaiter().GetResult();

        /// <summary>
        /// Sends a Burcat object through a stream.
        /// </summary>
        /// <typeparam name="T">The object type.</typeparam>
        /// <param name="head">The destination stream and headers to send with the operation.</param>
        /// <param name="objectBDP">The object to send.</param>
        /// <param name="token">The optional cancellation token.</param>
        public static void Send<T>(BurcatDirectionalHead head, T objectBDP, CancellationToken? token = null) where T : IBurcatObject => SendAsync(head, objectBDP, token).GetAwaiter().GetResult();

        /// <summary>
        /// Sends a null Burcat object value for a type through a stream.
        /// </summary> 
        /// <typeparam name="T">The object type.</typeparam>
        /// <param name="head">The destination stream and headers to send with the operation.</param>
        /// <param name="token">The optional cancellation token.</param>
        public static void Send<T>(BurcatDirectionalHead head, CancellationToken? token = null) where T : IBurcatObject => SendAsync<T>(head, token).GetAwaiter().GetResult();

        /// <summary>
        /// Resolves the current revision for an object reference through the configured providers.
        /// </summary>
        /// <param name="classID">The Burcat class identity of the referenced object.</param>
        /// <param name="objectID">The object reference identity.</param>
        /// <param name="ignoreInternal">Whether to skip the internal provider and query only the external provider.</param>
        /// <param name="token">The optional cancellation token.</param>
        /// <returns>The current revision, or <see cref="Guid.Empty"/> when no revision is available.</returns>
        public static async Task<Guid> RelayRevisionRequestAsync(BurcatBoradcastHead head, Guid classID, Guid objectID, bool ignoreInternal = false, CancellationToken? token = null)
        {
            CancellationToken cancellation = token ?? new CancellationTokenSource(DefaultTimeOut).Token;
            Guid version;

            if (AcceptedIdentities.TryGetType(classID, out Type? type))
            {
                version = ignoreInternal ? Guid.Empty : InternalProvider.GetRevision(head, type, objectID);
                if (version == Guid.Empty && ExternalProvider is not null) version = await ExternalProvider.GetRevision(head, type, objectID, cancellation);
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
        public static Guid RelayRevisionRequest(BurcatBoradcastHead head, Guid classID, Guid objectID, bool ignoreInternal = false, CancellationToken? token = null) => RelayRevisionRequestAsync(head, classID, objectID, ignoreInternal, token).GetAwaiter().GetResult();

        /// <summary>
        /// Sends a revision request to another application through a stream.
        /// </summary>
        /// <param name="stream">The stream connected to the remote application.</param>
        /// <param name="classID">The Burcat class identity of the referenced object.</param>
        /// <param name="objectID">The object reference identity.</param>
        /// <param name="token">The optional cancellation token.</param>
        /// <returns>The revision returned by the remote application.</returns>
        public static async Task<Guid> SendRevisionRequestAsync(BurcatDirectionalHead head, Guid classID, Guid objectID, CancellationToken? token = null)
        {
            SemaphoreSlim? semaphore = null;

            try
            {
                CancellationToken cancellation = token ?? new CancellationTokenSource(DefaultTimeOut).Token;
                semaphore = await TryWaitSemaphore(head.Stream, cancellation);
                cancellation.ThrowIfCancellationRequested();

                await head.Stream.WriteAsync(GetClassIdentity<BeginCommunicationSchematic>().ToByteArray(), cancellation);
                cancellation.ThrowIfCancellationRequested();

                await head.Stream.WriteAsync(GetClassIdentity<BeginRevisionRequestSchematic>().ToByteArray(), cancellation);
                cancellation.ThrowIfCancellationRequested();

                BurcatHead otherHead = await ExchangeHead(head.Stream, head.Headers, cancellation);
                cancellation.ThrowIfCancellationRequested();

                await head.Stream.WriteAsync(GetClassIdentity<VersionScheme>().ToByteArray(), cancellation);
                cancellation.ThrowIfCancellationRequested();

                await head.Stream.WriteAsync(classID.ToByteArray(), cancellation);
                cancellation.ThrowIfCancellationRequested();

                await head.Stream.WriteAsync(objectID.ToByteArray(), cancellation);
                cancellation.ThrowIfCancellationRequested();

                await head.Stream.WriteAsync(GetClassIdentity<VersionScheme>().ToByteArray(), cancellation);
                cancellation.ThrowIfCancellationRequested();

                if (!await RecieveScheme<RevisionScheme>(head.Stream, cancellation)) throw new InvalidDataException($"Expected scheme with identifier {GetClassIdentity<RevisionScheme>()}, but data read doesn't correspond to.");
                cancellation.ThrowIfCancellationRequested();

                byte[] guid = new byte[16];
                await head.Stream.ReadExactlyAsync(guid, cancellation); Guid revision = new(guid);
                cancellation.ThrowIfCancellationRequested();

                if (!await RecieveScheme<RevisionScheme>(head.Stream, cancellation)) throw new InvalidDataException($"Expected scheme with identifier {GetClassIdentity<RevisionScheme>()}, but data read doesn't correspond to.");
                cancellation.ThrowIfCancellationRequested();

                await head.Stream.WriteAsync(GetClassIdentity<EndRevisionRequestSchematic>().ToByteArray(), cancellation);
                cancellation.ThrowIfCancellationRequested();

                await head.Stream.WriteAsync(GetClassIdentity<EndCommunicationSchematic>().ToByteArray(), cancellation);
                cancellation.ThrowIfCancellationRequested();

                await head.Stream.FlushAsync();
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
        public static Guid SendRevisionRequest(BurcatDirectionalHead head, Guid classID, Guid objectID, CancellationToken? token = null) => SendRevisionRequestAsync(head, classID, objectID, token).GetAwaiter().GetResult();

        /// <summary>
        /// Resolves an object reference through the configured providers.
        /// </summary>
        /// <param name="classID">The Burcat class identity of the referenced object.</param>
        /// <param name="objectID">The object reference identity.</param>
        /// <param name="ignoreInternal">Whether to skip the internal provider and query only the external provider.</param>
        /// <param name="token">The optional cancellation token.</param>
        /// <returns>The resolved object, or <see langword="null"/> when it is unavailable.</returns>
        public static async Task<IBurcatObject?> RelayObjectRequestAsync(BurcatBoradcastHead head, Guid classID, Guid objectID, bool ignoreInternal = false, CancellationToken? token = null)
        {
            CancellationToken cancellation = token ?? new CancellationTokenSource(DefaultTimeOut).Token;
            IBurcatObject? reference;

            if (AcceptedIdentities.TryGetType(classID, out Type? type))
            {
                reference = ignoreInternal ? null : InternalProvider.GetObject(head, type, objectID);
                if (reference is null && ExternalProvider is not null) reference = await ExternalProvider.GetObject(head, type, objectID, cancellation);
                cancellation.ThrowIfCancellationRequested();
            }
            else throw new NotSupportedException($"Version with identifier {classID} is not supported.");

            return reference;
        }

        /// <summary>
        /// Resolves an object reference through the configured providers.
        /// </summary>
        /// <typeparam name="T">The expected object type.</typeparam>
        /// <param name="objectID">The object reference identity.</param>
        /// <param name="ignoreInternal">Whether to skip the internal provider and query only the external provider.</param>
        /// <param name="token">The optional cancellation token.</param>
        /// <returns>The resolved object, or <see langword="null"/> when it is unavailable.</returns>
        public static async Task<T?> RelayObjectRequestAsync<T>(BurcatBoradcastHead head, Guid objectID, bool ignoreInternal = false, CancellationToken? token = null) where T : IBurcatObject { T? result = (T?)await RelayObjectRequestAsync(head, GetClassIdentity<T>(), objectID, ignoreInternal, token); return result; }

        /// <summary>
        /// Resolves an object reference through the configured providers.
        /// </summary>
        /// <typeparam name="T">The expected object type.</typeparam>
        /// <param name="objectID">The typed object reference identity.</param>
        /// <param name="ignoreInternal">Whether to skip the internal provider and query only the external provider.</param>
        /// <param name="token">The optional cancellation token.</param>
        /// <returns>The resolved object, or <see langword="null"/> when it is unavailable.</returns>
        public static Task<T?> RelayObjectRequestAsync<T>(BurcatBoradcastHead head, BurcatIdentifier<T> objectID, bool ignoreInternal = false, CancellationToken? token = null) where T : IBurcatObject => RelayObjectRequestAsync<T>(head, (Guid)objectID, ignoreInternal, token);

        /// <summary>
        /// Resolves an object reference through the configured providers.
        /// </summary>
        /// <param name="classID">The Burcat class identity of the referenced object.</param>
        /// <param name="objectID">The object reference identity.</param>
        /// <param name="ignoreInternal">Whether to skip the internal provider and query only the external provider.</param>
        /// <param name="token">The optional cancellation token.</param>
        /// <returns>The resolved object, or <see langword="null"/> when it is unavailable.</returns>
        public static IBurcatObject? RelayObjectRequest(BurcatBoradcastHead head, Guid classID, Guid objectID, bool ignoreInternal = false, CancellationToken? token = null) => RelayObjectRequestAsync(head, classID, objectID, ignoreInternal, token).GetAwaiter().GetResult();

        /// <summary>
        /// Resolves an object reference through the configured providers.
        /// </summary>
        /// <typeparam name="T">The expected object type.</typeparam>
        /// <param name="objectID">The object reference identity.</param>
        /// <param name="ignoreInternal">Whether to skip the internal provider and query only the external provider.</param>
        /// <param name="token">The optional cancellation token.</param>
        /// <returns>The resolved object, or <see langword="null"/> when it is unavailable.</returns>
        public static T? RelayObjectRequest<T>(BurcatBoradcastHead head, Guid objectID, bool ignoreInternal = false, CancellationToken? token = null) where T : IBurcatObject => RelayObjectRequestAsync<T>(head, objectID, ignoreInternal, token).GetAwaiter().GetResult();

        /// <summary>
        /// Resolves an object reference through the configured providers.
        /// </summary>
        /// <typeparam name="T">The expected object type.</typeparam>
        /// <param name="objectID">The typed object reference identity.</param>
        /// <param name="ignoreInternal">Whether to skip the internal provider and query only the external provider.</param>
        /// <param name="token">The optional cancellation token.</param>
        /// <returns>The resolved object, or <see langword="null"/> when it is unavailable.</returns>
        public static T? RelayObjectRequest<T>(BurcatBoradcastHead head, BurcatIdentifier<T> objectID, bool ignoreInternal = false, CancellationToken? token = null) where T : IBurcatObject => RelayObjectRequestAsync<T>(head, objectID, ignoreInternal, token).GetAwaiter().GetResult();

        /// <summary>
        /// Sends an object request to another application through a stream.
        /// </summary>
        /// <param name="stream">The stream connected to the remote application.</param>
        /// <param name="classID">The Burcat class identity of the referenced object.</param>
        /// <param name="objectID">The object reference identity.</param>
        /// <param name="token">The optional cancellation token.</param>
        /// <returns>The object returned by the remote application, or <see langword="null"/>.</returns>
        public static async Task<IBurcatObject?> SendObjectRequestAsync(BurcatDirectionalHead head, Guid classID, Guid objectID, CancellationToken? token = null)
        {
            SemaphoreSlim? semaphore = null;

            try
            {
                CancellationToken cancellation = token ?? new CancellationTokenSource(DefaultTimeOut).Token;
                semaphore = await TryWaitSemaphore(head.Stream, cancellation);
                cancellation.ThrowIfCancellationRequested();

                await head.Stream.WriteAsync(GetClassIdentity<BeginCommunicationSchematic>().ToByteArray(), cancellation);
                cancellation.ThrowIfCancellationRequested();

                await head.Stream.WriteAsync(GetClassIdentity<BeginObjectRequestSchematic>().ToByteArray(), cancellation);
                cancellation.ThrowIfCancellationRequested();

                BurcatHead otherHead = await ExchangeHead(head.Stream, head.Headers, cancellation);
                cancellation.ThrowIfCancellationRequested();

                await head.Stream.WriteAsync(GetClassIdentity<VersionScheme>().ToByteArray(), cancellation);
                cancellation.ThrowIfCancellationRequested();

                await head.Stream.WriteAsync(classID.ToByteArray(), cancellation);
                cancellation.ThrowIfCancellationRequested();

                await head.Stream.WriteAsync(objectID.ToByteArray(), cancellation);
                cancellation.ThrowIfCancellationRequested();

                await head.Stream.WriteAsync(GetClassIdentity<VersionScheme>().ToByteArray(), cancellation);
                cancellation.ThrowIfCancellationRequested();

                IBurcatObject? result = (await RecieveObject(head.Stream, otherHead, cancellation)).Value;
                cancellation.ThrowIfCancellationRequested();

                await head.Stream.WriteAsync(GetClassIdentity<EndObjectRequestSchematic>().ToByteArray(), cancellation);
                cancellation.ThrowIfCancellationRequested();

                await head.Stream.WriteAsync(GetClassIdentity<EndCommunicationSchematic>().ToByteArray(), cancellation);
                cancellation.ThrowIfCancellationRequested();

                await head.Stream.FlushAsync();
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
        public static async Task<T?> SendObjectRequestAsync<T>(BurcatDirectionalHead head, Guid objectID, CancellationToken? token = null) where T : IBurcatObject { T? result = (T?)await SendObjectRequestAsync(head, GetClassIdentity<T>(), objectID, token); return result; }

        /// <summary>
        /// Sends an object request to another application through a stream.
        /// </summary>
        /// <typeparam name="T">The expected object type.</typeparam>
        /// <param name="stream">The stream connected to the remote application.</param>
        /// <param name="objectID">The typed object reference identity.</param>
        /// <param name="token">The optional cancellation token.</param>
        /// <returns>The object returned by the remote application, or <see langword="null"/>.</returns>
        public static Task<T?> SendObjectRequestAsync<T>(BurcatDirectionalHead head, BurcatIdentifier<T> objectID, CancellationToken? token = null) where T : IBurcatObject => SendObjectRequestAsync<T>(head, (Guid)objectID, token);

        /// <summary>
        /// Sends an object request to another application through a stream.
        /// </summary>
        /// <param name="stream">The stream connected to the remote application.</param>
        /// <param name="classID">The Burcat class identity of the referenced object.</param>
        /// <param name="objectID">The object reference identity.</param>
        /// <param name="token">The optional cancellation token.</param>
        /// <returns>The object returned by the remote application, or <see langword="null"/>.</returns>
        public static IBurcatObject? SendObjectRequest(BurcatDirectionalHead head, Guid classID, Guid objectID, CancellationToken? token = null) => SendObjectRequestAsync(head, classID, objectID, token).GetAwaiter().GetResult();

        /// <summary>
        /// Sends an object request to another application through a stream.
        /// </summary>
        /// <typeparam name="T">The expected object type.</typeparam>
        /// <param name="stream">The stream connected to the remote application.</param>
        /// <param name="objectID">The object reference identity.</param>
        /// <param name="token">The optional cancellation token.</param>
        /// <returns>The object returned by the remote application, or <see langword="null"/>.</returns>
        public static T? SendObjectRequest<T>(BurcatDirectionalHead head, Guid objectID, CancellationToken? token = null) where T : IBurcatObject => SendObjectRequestAsync<T>(head, objectID, token).GetAwaiter().GetResult();

        /// <summary>
        /// Sends an object request to another application through a stream.
        /// </summary>
        /// <typeparam name="T">The expected object type.</typeparam>
        /// <param name="stream">The stream connected to the remote application.</param>
        /// <param name="objectID">The typed object reference identity.</param>
        /// <param name="token">The optional cancellation token.</param>
        /// <returns>The object returned by the remote application, or <see langword="null"/>.</returns>
        public static T? SendObjectRequest<T>(BurcatDirectionalHead head, BurcatIdentifier<T> objectID, CancellationToken? token = null) where T : IBurcatObject => SendObjectRequestAsync<T>(head, objectID, token).GetAwaiter().GetResult();

        /// <summary>
        /// Requests that the configured providers add or update a Burcat instance in cache or storage.
        /// </summary>
        /// <param name="instance">The instance metadata and value to couple.</param>
        /// <param name="ignoreInternal">Whether to skip the internal provider and send only to the external provider.</param>
        /// <param name="token">The optional cancellation token.</param>
        /// <returns><see langword="null"/> on success; otherwise, the exception reported by a provider.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the instance has a <see langword="null"/> value.</exception>
        public static async Task<BurcatException?> RelayCoupleAsync(BurcatBoradcastHead head, BurcatInstance instance, bool ignoreInternal = false, CancellationToken? token = null)
        {
            if (instance.Value is null) throw new InvalidOperationException("Cannot couple a null object.");
            else
            {
                CancellationToken cancellation = token ?? new CancellationTokenSource(DefaultTimeOut).Token;
                cancellation.ThrowIfCancellationRequested();

                if (ignoreInternal && ExternalProvider is null) return new("No external provider is configured.");
                else if (ignoreInternal && ExternalProvider is not null) return await ExternalProvider.CoupleCache(head, instance.Value, true, cancellation);
                else if (ExternalProvider is not null) return InternalProvider.CoupleCache(head, instance.Value, true) ?? await ExternalProvider.CoupleCache(head, instance.Value, true, cancellation);
                else return InternalProvider.CoupleCache(head, instance.Value, true);
            }
        }

        /// <summary>
        /// Requests that the configured providers add or update an object in cache or storage.
        /// </summary>
        /// <typeparam name="T">The object type.</typeparam>
        /// <param name="objectBDP">The object to couple.</param>
        /// <param name="ignoreInternal">Whether to skip the internal provider and send only to the external provider.</param>
        /// <param name="token">The optional cancellation token.</param>
        /// <returns><see langword="null"/> on success; otherwise, the exception reported by a provider.</returns>
        public static Task<BurcatException?> RelayCoupleAsync<T>(BurcatBoradcastHead head, T objectBDP, bool ignoreInternal = false, CancellationToken? token = null) where T : IBurcatObject => RelayCoupleAsync(head, BurcatInstance.Build(objectBDP), ignoreInternal, token);

        /// <summary>
        /// Requests that the configured providers add or update a Burcat instance in cache or storage.
        /// </summary>
        /// <param name="instance">The instance metadata and value to couple.</param>
        /// <param name="ignoreInternal">Whether to skip the internal provider and send only to the external provider.</param>
        /// <param name="token">The optional cancellation token.</param>
        /// <returns><see langword="null"/> on success; otherwise, the exception reported by a provider.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the instance has a <see langword="null"/> value.</exception>
        public static BurcatException? RelayCouple(BurcatBoradcastHead head, BurcatInstance instance, bool ignoreInternal = false, CancellationToken? token = null) => RelayCoupleAsync(head, instance, ignoreInternal, token).GetAwaiter().GetResult();

        /// <summary>
        /// Requests that the configured providers add or update an object in cache or storage.
        /// </summary>
        /// <typeparam name="T">The object type.</typeparam>
        /// <param name="objectBDP">The object to couple.</param>
        /// <param name="ignoreInternal">Whether to skip the internal provider and send only to the external provider.</param>
        /// <param name="token">The optional cancellation token.</param>
        /// <returns><see langword="null"/> on success; otherwise, the exception reported by a provider.</returns>
        public static BurcatException? RelayCouple<T>(BurcatBoradcastHead head, T objectBDP, bool ignoreInternal = false, CancellationToken? token = null) where T : IBurcatObject => RelayCoupleAsync(head, objectBDP, ignoreInternal, token).GetAwaiter().GetResult();

        /// <summary>
        /// Sends an explicit cache add or update request for a Burcat instance to another application.
        /// </summary>
        /// <param name="stream">The stream connected to the remote application.</param>
        /// <param name="instance">The instance metadata and value to couple.</param>
        /// <param name="token">The optional cancellation token.</param>
        /// <returns><see langword="null"/> on success; otherwise, the exception returned by the remote application.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the instance has a <see langword="null"/> value.</exception>
        public static async Task<BurcatException?> SendCoupleAsync(BurcatDirectionalHead head, BurcatInstance instance, CancellationToken? token = null)
        {
            if (instance.Value is null) throw new InvalidOperationException("Cannot couple a null object.");
            else
            {
                SemaphoreSlim? semaphore = null;

                try
                {
                    CancellationToken cancellation = token ?? new CancellationTokenSource(DefaultTimeOut).Token;
                    semaphore = await TryWaitSemaphore(head.Stream, cancellation);
                    cancellation.ThrowIfCancellationRequested();

                    await head.Stream.WriteAsync(GetClassIdentity<BeginCommunicationSchematic>().ToByteArray(), cancellation);
                    cancellation.ThrowIfCancellationRequested();

                    await head.Stream.WriteAsync(GetClassIdentity<BeginCoupleSchematic>().ToByteArray(), cancellation);
                    cancellation.ThrowIfCancellationRequested();

                    BurcatHead otherHead = await ExchangeHead(head.Stream, head.Headers, cancellation);
                    cancellation.ThrowIfCancellationRequested();

                    await SendObject(head.Stream, instance, cancellation);
                    cancellation.ThrowIfCancellationRequested();

                    BurcatException? result = (await RecieveObject(head.Stream, otherHead, cancellation)).ForceValue<BurcatException?>();
                    cancellation.ThrowIfCancellationRequested();

                    await head.Stream.WriteAsync(GetClassIdentity<EndCoupleSchematic>().ToByteArray(), cancellation);
                    cancellation.ThrowIfCancellationRequested();

                    await head.Stream.WriteAsync(GetClassIdentity<EndCommunicationSchematic>().ToByteArray(), cancellation);
                    cancellation.ThrowIfCancellationRequested();

                    await head.Stream.FlushAsync();
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
        public static Task<BurcatException?> SendCoupleAsync<T>(BurcatDirectionalHead head, T objectBDP, CancellationToken? token = null) where T : IBurcatObject => SendCoupleAsync(head, BurcatInstance.Build(objectBDP), token);

        /// <summary>
        /// Sends an explicit cache add or update request for a Burcat instance to another application.
        /// </summary>
        /// <param name="stream">The stream connected to the remote application.</param>
        /// <param name="instance">The instance metadata and value to couple.</param>
        /// <param name="token">The optional cancellation token.</param>
        /// <returns><see langword="null"/> on success; otherwise, the exception returned by the remote application.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the instance has a <see langword="null"/> value.</exception>
        public static BurcatException? SendCouple(BurcatDirectionalHead head, BurcatInstance instance, CancellationToken? token = null) => SendCoupleAsync(head, instance, token).GetAwaiter().GetResult();

        /// <summary>
        /// Sends an explicit cache add or update request to another application.
        /// </summary>
        /// <typeparam name="T">The object type.</typeparam>
        /// <param name="stream">The stream connected to the remote application.</param>
        /// <param name="objectBDP">The object to couple.</param>
        /// <param name="token">The optional cancellation token.</param>
        /// <returns><see langword="null"/> on success; otherwise, the exception returned by the remote application.</returns>
        public static BurcatException? SendCouple<T>(BurcatDirectionalHead head, T objectBDP, CancellationToken? token = null) where T : IBurcatObject => SendCoupleAsync(head, objectBDP, token).GetAwaiter().GetResult();

        /// <summary>
        /// Requests that the configured providers delete a Burcat instance from cache or storage.
        /// </summary>
        /// <param name="instance">The instance metadata and value to decouple.</param>
        /// <param name="ignoreInternal">Whether to skip the internal provider and send only to the external provider.</param>
        /// <param name="token">The optional cancellation token.</param>
        /// <returns><see langword="null"/> on success; otherwise, the exception reported by a provider.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the instance has a <see langword="null"/> value.</exception>
        public static async Task<BurcatException?> RelayDecoupleAsync(BurcatBoradcastHead head, BurcatInstance instance, bool ignoreInternal = false, CancellationToken? token = null)
        {
            if (instance.Value is null) throw new InvalidOperationException("Cannot couple a null object.");
            else
            {
                CancellationToken cancellation = token ?? new CancellationTokenSource(DefaultTimeOut).Token;
                cancellation.ThrowIfCancellationRequested();

                if (ignoreInternal && ExternalProvider is null) return new("No external provider is configured.");
                else if (ignoreInternal && ExternalProvider is not null) return await ExternalProvider.DecoupleCache(head, instance.Value, cancellation);
                else if (ExternalProvider is not null) return InternalProvider.DecoupleCache(head, instance.Value) ?? await ExternalProvider.DecoupleCache(head, instance.Value, cancellation);
                else return InternalProvider.DecoupleCache(head, instance.Value);
            }
        }

        /// <summary>
        /// Requests that the configured providers delete an object from cache or storage.
        /// </summary>
        /// <typeparam name="T">The object type.</typeparam>
        /// <param name="objectBDP">The object to decouple.</param>
        /// <param name="ignoreInternal">Whether to skip the internal provider and send only to the external provider.</param>
        /// <param name="token">The optional cancellation token.</param>
        /// <returns><see langword="null"/> on success; otherwise, the exception reported by a provider.</returns>
        public static Task<BurcatException?> RelayDecoupleAsync<T>(BurcatBoradcastHead head, T objectBDP, bool ignoreInternal = false, CancellationToken? token = null) where T : IBurcatObject => RelayDecoupleAsync(head, BurcatInstance.Build(objectBDP), ignoreInternal, token);

        /// <summary>
        /// Requests that the configured providers delete a Burcat instance from cache or storage.
        /// </summary>
        /// <param name="instance">The instance metadata and value to decouple.</param>
        /// <param name="ignoreInternal">Whether to skip the internal provider and send only to the external provider.</param>
        /// <param name="token">The optional cancellation token.</param>
        /// <returns><see langword="null"/> on success; otherwise, the exception reported by a provider.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the instance has a <see langword="null"/> value.</exception>
        public static BurcatException? RelayDecouple(BurcatBoradcastHead head, BurcatInstance instance, bool ignoreInternal = false, CancellationToken? token = null) => RelayDecoupleAsync(head, instance, ignoreInternal, token).GetAwaiter().GetResult();

        /// <summary>
        /// Requests that the configured providers delete an object from cache or storage.
        /// </summary>
        /// <typeparam name="T">The object type.</typeparam>
        /// <param name="objectBDP">The object to decouple.</param>
        /// <param name="ignoreInternal">Whether to skip the internal provider and send only to the external provider.</param>
        /// <param name="token">The optional cancellation token.</param>
        /// <returns><see langword="null"/> on success; otherwise, the exception reported by a provider.</returns>
        public static BurcatException? RelayDecouple<T>(BurcatBoradcastHead head, T objectBDP, bool ignoreInternal = false, CancellationToken? token = null) where T : IBurcatObject => RelayDecoupleAsync(head, objectBDP, ignoreInternal, token).GetAwaiter().GetResult();

        /// <summary>
        /// Sends an explicit cache delete request for a Burcat instance to another application.
        /// </summary>
        /// <param name="stream">The stream connected to the remote application.</param>
        /// <param name="instance">The instance metadata and value to decouple.</param>
        /// <param name="token">The optional cancellation token.</param>
        /// <returns><see langword="null"/> on success; otherwise, the exception returned by the remote application.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the instance has a <see langword="null"/> value.</exception>
        public static async Task<BurcatException?> SendDecoupleAsync(BurcatDirectionalHead head, BurcatInstance instance, CancellationToken? token = null)
        {
            if (instance.Value is null) throw new InvalidOperationException("Cannot decouple a null object.");
            else
            {
                SemaphoreSlim? semaphore = null;

                try
                {
                    CancellationToken cancellation = token ?? new CancellationTokenSource(DefaultTimeOut).Token;
                    semaphore = await TryWaitSemaphore(head.Stream, cancellation);
                    cancellation.ThrowIfCancellationRequested();

                    await head.Stream.WriteAsync(GetClassIdentity<BeginCommunicationSchematic>().ToByteArray(), cancellation);
                    cancellation.ThrowIfCancellationRequested();

                    await head.Stream.WriteAsync(GetClassIdentity<BeginDecoupleSchematic>().ToByteArray(), cancellation);
                    cancellation.ThrowIfCancellationRequested();

                    BurcatHead otherHead = await ExchangeHead(head.Stream, head.Headers, cancellation);
                    cancellation.ThrowIfCancellationRequested();

                    await SendObject(head.Stream, instance, cancellation);
                    cancellation.ThrowIfCancellationRequested();

                    BurcatException? result = (await RecieveObject(head.Stream, otherHead, cancellation)).ForceValue<BurcatException?>();
                    cancellation.ThrowIfCancellationRequested();

                    await head.Stream.WriteAsync(GetClassIdentity<EndDecoupleSchematic>().ToByteArray(), cancellation);
                    cancellation.ThrowIfCancellationRequested();

                    await head.Stream.WriteAsync(GetClassIdentity<EndCommunicationSchematic>().ToByteArray(), cancellation);
                    cancellation.ThrowIfCancellationRequested();

                    await head.Stream.FlushAsync();
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
        public static Task<BurcatException?> SendDecoupleAsync<T>(BurcatDirectionalHead head, T objectBDP, CancellationToken? token = null) where T : IBurcatObject => SendDecoupleAsync(head, BurcatInstance.Build(objectBDP), token);

        /// <summary>
        /// Sends an explicit cache delete request for a Burcat instance to another application.
        /// </summary>
        /// <param name="stream">The stream connected to the remote application.</param>
        /// <param name="instance">The instance metadata and value to decouple.</param>
        /// <param name="token">The optional cancellation token.</param>
        /// <returns><see langword="null"/> on success; otherwise, the exception returned by the remote application.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the instance has a <see langword="null"/> value.</exception>
        public static BurcatException? SendDecouple(BurcatDirectionalHead head, BurcatInstance instance, CancellationToken? token = null) => SendDecoupleAsync(head, instance, token).GetAwaiter().GetResult();

        /// <summary>
        /// Sends an explicit cache delete request to another application.
        /// </summary>
        /// <typeparam name="T">The object type.</typeparam>
        /// <param name="stream">The stream connected to the remote application.</param>
        /// <param name="objectBDP">The object to decouple.</param>
        /// <param name="token">The optional cancellation token.</param>
        /// <returns><see langword="null"/> on success; otherwise, the exception returned by the remote application.</returns>
        public static BurcatException? SendDecouple<T>(BurcatDirectionalHead head, T objectBDP, CancellationToken? token = null) where T : IBurcatObject => SendDecoupleAsync(head, objectBDP, token).GetAwaiter().GetResult();

        /// <summary>
        /// Executes an action through the configured providers.
        /// </summary>
        /// <param name="instance">The target instance or type-level action target.</param>
        /// <param name="action">The protocol-visible action name.</param>
        /// <param name="parameters">The action parameters.</param>
        /// <param name="ignoreInternal">Whether to skip the internal provider and query only the external provider.</param>
        /// <param name="token">The optional cancellation token.</param>
        /// <returns>The action result.</returns>
        public static async Task<ActionResult> RelayActionAsync(BurcatBoradcastHead head, BurcatInstance instance, string action, object?[]? parameters = null, bool ignoreInternal = false, CancellationToken? token = null)
        {
            if (action.Length != 0 && !char.IsLetterOrDigit(action[0])) throw new ArgumentException("An action must start with a letter or number", nameof(action));
            else
            {
                CancellationToken cancellation = token ?? new CancellationTokenSource(DefaultTimeOut).Token;

                ActionResult result = ignoreInternal ? ActionResult.Unsuccessful : InternalProvider.ExecuteAction(head, instance.Type, instance.Value, action, parameters);
                cancellation.ThrowIfCancellationRequested();

                if (!result.SuccessfulExecution && ExternalProvider is not null) result = await ExternalProvider.ExecuteAction(head, instance.Type, instance.Value, action, parameters, cancellation);
                cancellation.ThrowIfCancellationRequested();

                return result;
            }
        }

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
        public static Task<ActionResult> RelayActionAsync<T>(BurcatBoradcastHead head, T objectBDP, string action, object?[]? parameters = null, bool ignoreInternal = false, CancellationToken? token = null) where T : IBurcatObject => RelayActionAsync(head, new(objectBDP), action, parameters, ignoreInternal, token);

        /// <summary>
        /// Executes a type-level action through the configured providers.
        /// </summary>
        /// <typeparam name="T">The target object type.</typeparam>
        /// <param name="action">The protocol-visible action name.</param>
        /// <param name="parameters">The action parameters.</param>
        /// <param name="ignoreInternal">Whether to skip the internal provider and query only the external provider.</param>
        /// <param name="token">The optional cancellation token.</param>
        /// <returns>The action result.</returns>
        public static Task<ActionResult> RelayActionAsync<T>(BurcatBoradcastHead head, string action, object?[]? parameters = null, bool ignoreInternal = false, CancellationToken? token = null) where T : IBurcatObject => RelayActionAsync(head, BurcatInstance.Build<T>(), action, parameters, ignoreInternal, token);

        /// <summary>
        /// Executes an action through the configured providers.
        /// </summary>
        /// <param name="instance">The target instance or type-level action target.</param>
        /// <param name="action">The protocol-visible action name.</param>
        /// <param name="parameters">The action parameters.</param>
        /// <param name="ignoreInternal">Whether to skip the internal provider and query only the external provider.</param>
        /// <param name="token">The optional cancellation token.</param>
        /// <returns>The action result.</returns>
        public static ActionResult RelayAction(BurcatBoradcastHead head, BurcatInstance instance, string action, object?[]? parameters = null, bool ignoreInternal = false, CancellationToken? token = null) => RelayActionAsync(head, instance, action, parameters, ignoreInternal, token).GetAwaiter().GetResult();

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
        public static ActionResult RelayAction<T>(BurcatBoradcastHead head, T objectBDP, string action, object?[]? parameters = null, bool ignoreInternal = false, CancellationToken? token = null) where T : IBurcatObject => RelayAction(head, new(objectBDP), action, parameters, ignoreInternal, token);

        /// <summary>
        /// Executes a type-level action through the configured providers.
        /// </summary>
        /// <typeparam name="T">The target object type.</typeparam>
        /// <param name="action">The protocol-visible action name.</param>
        /// <param name="parameters">The action parameters.</param>
        /// <param name="ignoreInternal">Whether to skip the internal provider and query only the external provider.</param>
        /// <param name="token">The optional cancellation token.</param>
        /// <returns>The action result.</returns>
        public static ActionResult RelayAction<T>(BurcatBoradcastHead head, string action, object?[]? parameters = null, bool ignoreInternal = false, CancellationToken? token = null) where T : IBurcatObject => RelayAction(head, BurcatInstance.Build<T>(), action, parameters, ignoreInternal, token);


        /// <summary>
        /// Sends an action request to another application through a stream.
        /// </summary>
        /// <param name="stream">The stream connected to the remote application.</param>
        /// <param name="instance">The target instance or type-level action target.</param>
        /// <param name="action">The protocol-visible action name.</param>
        /// <param name="parameters">The action parameters.</param>
        /// <param name="token">The optional cancellation token.</param>
        /// <returns>The action result returned by the remote application.</returns>
        public static async Task<ActionResult> SendActionAsync(BurcatDirectionalHead head, BurcatInstance instance, string action, object?[]? parameters = null, CancellationToken? token = null)
        {
            IBurcatObject?[] burcatParameters = BurcatTranslator.ObjectsTranslate(parameters);

            if (action.Length != 0 && !char.IsLetterOrDigit(action[0])) throw new ArgumentException("An action must start with a letter or number", nameof(action));
            else
            {
                SemaphoreSlim? semaphore = null;

                try
                {
                    CancellationToken cancellation = token ?? new CancellationTokenSource(DefaultTimeOut).Token;
                    semaphore = await TryWaitSemaphore(head.Stream, cancellation);
                    cancellation.ThrowIfCancellationRequested();

                    await head.Stream.WriteAsync(GetClassIdentity<BeginCommunicationSchematic>().ToByteArray(), cancellation);
                    cancellation.ThrowIfCancellationRequested();

                    await head.Stream.WriteAsync(GetClassIdentity<BeginActionSchematic>().ToByteArray(), cancellation);
                    cancellation.ThrowIfCancellationRequested();

                    BurcatHead otherHead = await ExchangeHead(head.Stream, head.Headers, cancellation);
                    cancellation.ThrowIfCancellationRequested();

                    await SendObject(head.Stream, instance, cancellation);
                    cancellation.ThrowIfCancellationRequested();

                    await head.Stream.WriteAsync(GetClassIdentity<ActionScheme>().ToByteArray(), cancellation);
                    byte[] actionData = Encoding.Unicode.GetBytes(action);
                    await head.Stream.WriteAsync(BitConverter.GetBytes(actionData.Length), cancellation);
                    await head.Stream.WriteAsync(actionData, cancellation);
                    await head.Stream.WriteAsync(GetClassIdentity<ActionScheme>().ToByteArray(), cancellation);
                    cancellation.ThrowIfCancellationRequested();

                    await head.Stream.WriteAsync(GetClassIdentity<ParameterScheme>().ToByteArray(), cancellation);
                    await head.Stream.WriteAsync(BitConverter.GetBytes(burcatParameters.Length), cancellation);
                    foreach (IBurcatObject? parameter in burcatParameters)
                    {
                        cancellation.ThrowIfCancellationRequested();

                        if (parameter is null) await SendObject(head.Stream, NothingChart.Instance, cancellation);
                        else await SendObject(head.Stream, new(parameter), cancellation);

                        cancellation.ThrowIfCancellationRequested();
                    }
                    await head.Stream.WriteAsync(GetClassIdentity<ParameterScheme>().ToByteArray(), cancellation);

                    ActionResult result = (await RecieveObject(head.Stream, otherHead, cancellation)).ForceValue<ActionResult>();
                    cancellation.ThrowIfCancellationRequested();

                    await head.Stream.WriteAsync(GetClassIdentity<EndActionSchematic>().ToByteArray(), cancellation);
                    cancellation.ThrowIfCancellationRequested();

                    await head.Stream.WriteAsync(GetClassIdentity<EndCommunicationSchematic>().ToByteArray(), cancellation);
                    cancellation.ThrowIfCancellationRequested();

                    await head.Stream.FlushAsync();
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
        public static Task<ActionResult> SendActionAsync<T>(BurcatDirectionalHead head, T objectBDP, string action, object?[]? parameters = null, CancellationToken? token = null) where T : IBurcatObject => SendActionAsync(head, new(objectBDP), action, parameters, token);

        /// <summary>
        /// Sends a type-level action request to another application through a stream.
        /// </summary>
        /// <typeparam name="T">The target object type.</typeparam>
        /// <param name="stream">The stream connected to the remote application.</param>
        /// <param name="action">The protocol-visible action name.</param>
        /// <param name="parameters">The action parameters.</param>
        /// <param name="token">The optional cancellation token.</param>
        /// <returns>The action result returned by the remote application.</returns>
        public static Task<ActionResult> SendActionAsync<T>(BurcatDirectionalHead head, string action, object?[]? parameters = null, CancellationToken? token = null) where T : IBurcatObject => SendActionAsync(head, BurcatInstance.Build<T>(), action, parameters, token);

        /// <summary>
        /// Sends an action request to another application through a stream.
        /// </summary>
        /// <param name="stream">The stream connected to the remote application.</param>
        /// <param name="instance">The target instance or type-level action target.</param>
        /// <param name="action">The protocol-visible action name.</param>
        /// <param name="parameters">The action parameters.</param>
        /// <param name="token">The optional cancellation token.</param>
        /// <returns>The action result returned by the remote application.</returns>
        public static ActionResult SendAction(BurcatDirectionalHead head, BurcatInstance instance, string action, object?[]? parameters = null, CancellationToken? token = null) => SendActionAsync(head, instance, action, parameters, token).GetAwaiter().GetResult();

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
        public static ActionResult SendAction<T>(BurcatDirectionalHead head, T objectBDP, string action, object?[]? parameters = null, CancellationToken? token = null) where T : IBurcatObject => SendAction(head, new(objectBDP), action, parameters, token);

        /// <summary>
        /// Sends a type-level action request to another application through a stream.
        /// </summary>
        /// <typeparam name="T">The target object type.</typeparam>
        /// <param name="stream">The stream connected to the remote application.</param>
        /// <param name="action">The protocol-visible action name.</param>
        /// <param name="parameters">The action parameters.</param>
        /// <param name="token">The optional cancellation token.</param>
        /// <returns>The action result returned by the remote application.</returns>
        public static ActionResult SendAction<T>(BurcatDirectionalHead head, string action, object?[]? parameters = null, CancellationToken? token = null) where T : IBurcatObject => SendAction(head, BurcatInstance.Build<T>(), action, parameters, token);

        /// <summary>
        /// Receives and processes the next Burcat protocol exchange from a stream.
        /// </summary>
        /// <param name="stream">The source stream.</param>
        /// <param name="token">The optional cancellation token.</param>
        /// <returns>The result of the received exchange.</returns>
        public static async Task<ExchangeResult> ReceiveAsync(BurcatDirectionalHead head, CancellationToken? token = null)
        {
            SemaphoreSlim? semaphore = null;

            try
            {
                CancellationToken cancellation = token ?? new CancellationTokenSource(DefaultTimeOut).Token;
                semaphore = await TryWaitSemaphore(head.Stream, cancellation);
                cancellation.ThrowIfCancellationRequested();

                if (!await RecieveScheme<BeginCommunicationSchematic>(head.Stream, cancellation)) throw new InvalidDataException($"Expected scheme with identifier {GetClassIdentity<BeginCommunicationSchematic>()}, but data read doesn't correspond to");
                cancellation.ThrowIfCancellationRequested();

                Guid scheme = await RecieveScheme(head.Stream, cancellation);
                cancellation.ThrowIfCancellationRequested();

                if (scheme == GetClassIdentity<BeginIdentitiesSchematic>())
                {
                    BurcatHead otherHead = await InverseExchangeHead(head.Stream, head.Headers, cancellation);
                    cancellation.ThrowIfCancellationRequested();

                    BurcatIdentitySet identities = InternalProvider.GetIdentities(otherHead.StreamID);
                    cancellation.ThrowIfCancellationRequested();

                    await SendObject(head.Stream, identities, cancellation);
                    cancellation.ThrowIfCancellationRequested();

                    if (!await RecieveScheme<EndIdentitiesSchematic>(head.Stream, cancellation)) throw new InvalidDataException($"Expected scheme with identifier {GetClassIdentity<EndIdentitiesSchematic>()}, but data read doesn't correspond to");
                    cancellation.ThrowIfCancellationRequested();

                    if (!await RecieveScheme<EndCommunicationSchematic>(head.Stream, cancellation)) throw new InvalidDataException($"Expected scheme with identifier {GetClassIdentity<EndCommunicationSchematic>()}, but data read doesn't correspond to");
                    cancellation.ThrowIfCancellationRequested();

                    await head.Stream.FlushAsync();
                    cancellation.ThrowIfCancellationRequested();

                    return new(BurcatExchangeType.Identities, BurcatInstance.Build<NothingChart>(), new(identities));
                }
                else if (scheme == GetClassIdentity<BeginHeadersSchematic>())
                {
                    BurcatHead otherHead = await InverseExchangeHead(head.Stream, Headers, cancellation);
                    cancellation.ThrowIfCancellationRequested();

                    BurcatHeaderSet headers = InternalProvider.GetHeaders(otherHead.StreamID);
                    cancellation.ThrowIfCancellationRequested();

                    await SendObject(head.Stream, headers, cancellation);
                    cancellation.ThrowIfCancellationRequested();

                    if (!await RecieveScheme<EndHeadersSchematic>(head.Stream, cancellation)) throw new InvalidDataException($"Expected scheme with identifier {GetClassIdentity<EndHeadersSchematic>()}, but data read doesn't correspond to");
                    cancellation.ThrowIfCancellationRequested();

                    if (!await RecieveScheme<EndCommunicationSchematic>(head.Stream, cancellation)) throw new InvalidDataException($"Expected scheme with identifier {GetClassIdentity<EndCommunicationSchematic>()}, but data read doesn't correspond to");
                    cancellation.ThrowIfCancellationRequested();

                    await head.Stream.FlushAsync();
                    cancellation.ThrowIfCancellationRequested();

                    return new(BurcatExchangeType.Headers, BurcatInstance.Build<NothingChart>(), new(headers));
                }
                else if (scheme == GetClassIdentity<BeginObjectSchematic>())
                {
                    BurcatHead otherHead = await InverseExchangeHead(head.Stream, Headers, cancellation);
                    cancellation.ThrowIfCancellationRequested();

                    BurcatInstance instance = await RecieveObject(head.Stream, otherHead, cancellation);
                    cancellation.ThrowIfCancellationRequested();

                    if (!await RecieveScheme<EndObjectSchematic>(head.Stream, cancellation)) throw new InvalidDataException($"Expected scheme with identifier {GetClassIdentity<EndObjectSchematic>()}, but data read doesn't correspond to");
                    cancellation.ThrowIfCancellationRequested();

                    if (!await RecieveScheme<EndCommunicationSchematic>(head.Stream, cancellation)) throw new InvalidDataException($"Expected scheme with identifier {GetClassIdentity<EndCommunicationSchematic>()}, but data read doesn't correspond to");
                    cancellation.ThrowIfCancellationRequested();

                    await head.Stream.FlushAsync();
                    cancellation.ThrowIfCancellationRequested();

                    return new(BurcatExchangeType.Object, instance);
                }
                else if (scheme == GetClassIdentity<BeginRevisionRequestSchematic>())
                {
                    BurcatHead otherHead = await InverseExchangeHead(head.Stream, Headers, cancellation);
                    cancellation.ThrowIfCancellationRequested();

                    if (!await RecieveScheme<VersionScheme>(head.Stream, cancellation)) throw new InvalidDataException($"Expected scheme with identifier {GetClassIdentity<VersionScheme>()}, but data read doesn't correspond to");
                    cancellation.ThrowIfCancellationRequested();

                    byte[] guid = new byte[16];
                    await head.Stream.ReadExactlyAsync(guid, cancellation); Guid classID = new(guid);
                    cancellation.ThrowIfCancellationRequested();

                    await head.Stream.ReadExactlyAsync(guid, cancellation); Guid objectID = new(guid);
                    cancellation.ThrowIfCancellationRequested();

                    if (!await RecieveScheme<VersionScheme>(head.Stream, cancellation)) throw new InvalidDataException($"Expected scheme with identifier {GetClassIdentity<VersionScheme>()}, but data read doesn't correspond to");
                    cancellation.ThrowIfCancellationRequested();

                    Guid revision = AcceptedIdentities.TryGetType(classID, out Type? objectType) ? await RelayRevisionRequestAsync(new(otherHead.Headers), classID, objectID, false, cancellation) : Guid.Empty;
                    cancellation.ThrowIfCancellationRequested();

                    await head.Stream.WriteAsync(GetClassIdentity<RevisionScheme>().ToByteArray(), cancellation);
                    cancellation.ThrowIfCancellationRequested();

                    await head.Stream.WriteAsync(revision.ToByteArray(), cancellation);
                    cancellation.ThrowIfCancellationRequested();

                    await head.Stream.WriteAsync(GetClassIdentity<RevisionScheme>().ToByteArray(), cancellation);
                    cancellation.ThrowIfCancellationRequested();

                    if (!await RecieveScheme<EndRevisionRequestSchematic>(head.Stream, cancellation)) throw new InvalidDataException($"Expected scheme with identifier {GetClassIdentity<EndObjectRequestSchematic>()}, but data read doesn't correspond to");
                    cancellation.ThrowIfCancellationRequested();

                    if (!await RecieveScheme<EndCommunicationSchematic>(head.Stream, cancellation)) throw new InvalidDataException($"Expected scheme with identifier {GetClassIdentity<EndCommunicationSchematic>()}, but data read doesn't correspond to");
                    cancellation.ThrowIfCancellationRequested();

                    await head.Stream.FlushAsync();
                    cancellation.ThrowIfCancellationRequested();

                    if (objectType is null) return new(BurcatExchangeType.RevisionRequest, BurcatInstance.Build(new UnsupportedBurcatObjectException()));
                    else return new(BurcatExchangeType.RevisionRequest, new(objectType));
                }
                else if(scheme == GetClassIdentity<BeginObjectRequestSchematic>())
                {
                    BurcatHead otherHead = await InverseExchangeHead(head.Stream, Headers, cancellation);
                    cancellation.ThrowIfCancellationRequested();

                    if (!await RecieveScheme<VersionScheme>(head.Stream, cancellation)) throw new InvalidDataException($"Expected scheme with identifier {GetClassIdentity<VersionScheme>()}, but data read doesn't correspond to");
                    cancellation.ThrowIfCancellationRequested();

                    byte[] guid = new byte[16];
                    await head.Stream.ReadExactlyAsync(guid, cancellation); Guid classID = new(guid);
                    cancellation.ThrowIfCancellationRequested();

                    await head.Stream.ReadExactlyAsync(guid, cancellation); Guid objectID = new(guid);
                    cancellation.ThrowIfCancellationRequested();

                    if (!await RecieveScheme<VersionScheme>(head.Stream, cancellation)) throw new InvalidDataException($"Expected scheme with identifier {GetClassIdentity<VersionScheme>()}, but data read doesn't correspond to");
                    cancellation.ThrowIfCancellationRequested();

                    BurcatInstance instance = AcceptedIdentities.TryGetType(classID, out Type? objectType) ? new(objectType, await RelayObjectRequestAsync(new(otherHead.Headers), classID, objectID, false, cancellation)) : BurcatInstance.Build<NothingChart>();
                    cancellation.ThrowIfCancellationRequested();

                    await SendObject(head.Stream, instance, cancellation);
                    cancellation.ThrowIfCancellationRequested();

                    if (!await RecieveScheme<EndObjectRequestSchematic>(head.Stream, cancellation)) throw new InvalidDataException($"Expected scheme with identifier {GetClassIdentity<EndObjectRequestSchematic>()}, but data read doesn't correspond to");
                    cancellation.ThrowIfCancellationRequested();

                    if (!await RecieveScheme<EndCommunicationSchematic>(head.Stream, cancellation)) throw new InvalidDataException($"Expected scheme with identifier {GetClassIdentity<EndCommunicationSchematic>()}, but data read doesn't correspond to");
                    cancellation.ThrowIfCancellationRequested();

                    await head.Stream.FlushAsync();
                    cancellation.ThrowIfCancellationRequested();

                    if (objectType is null) return new(BurcatExchangeType.ObjectRequest, BurcatInstance.Build(new UnsupportedBurcatObjectException()));
                    else return new(BurcatExchangeType.ObjectRequest, new(objectType), instance);
                }
                else if (scheme == GetClassIdentity<BeginCoupleSchematic>())
                {
                    BurcatHead otherHead = await InverseExchangeHead(head.Stream, Headers, cancellation);
                    cancellation.ThrowIfCancellationRequested();

                    BurcatInstance instance = await RecieveObject(head.Stream, otherHead, cancellation);
                    cancellation.ThrowIfCancellationRequested();

                    IBurcatObject reference = instance.Value ?? throw new NullReferenceException("Cannot construct an empty object.");
                    cancellation.ThrowIfCancellationRequested();

                    BurcatException? coupleException = await RelayCoupleAsync(new(otherHead.Headers), BurcatInstance.Build(reference), false, cancellation);
                    cancellation.ThrowIfCancellationRequested();

                    await SendObject(head.Stream, coupleException is BurcatException exception ? new(exception) : BurcatInstance.Build<BurcatException>(), cancellation);
                    cancellation.ThrowIfCancellationRequested();

                    if (!await RecieveScheme<EndCoupleSchematic>(head.Stream, cancellation)) throw new InvalidDataException($"Expected scheme with identifier {GetClassIdentity<EndCoupleSchematic>()}, but data read doesn't correspond to");
                    cancellation.ThrowIfCancellationRequested();

                    if (!await RecieveScheme<EndCommunicationSchematic>(head.Stream, cancellation)) throw new InvalidDataException($"Expected scheme with identifier {GetClassIdentity<EndCommunicationSchematic>()}, but data read doesn't correspond to");
                    cancellation.ThrowIfCancellationRequested();

                    await head.Stream.FlushAsync();
                    cancellation.ThrowIfCancellationRequested();

                    return new(BurcatExchangeType.Couple, instance, BurcatInstance.Build(coupleException));
                }
                else if (scheme == GetClassIdentity<BeginDecoupleSchematic>())
                {
                    BurcatHead otherHead = await InverseExchangeHead(head.Stream, Headers, cancellation);
                    cancellation.ThrowIfCancellationRequested();

                    BurcatInstance instance = await RecieveObject(head.Stream, otherHead, cancellation);
                    cancellation.ThrowIfCancellationRequested();

                    IBurcatObject reference = instance.Value ?? throw new NullReferenceException("Cannot construct an empty object.");
                    cancellation.ThrowIfCancellationRequested();

                    BurcatException? decoupleException = await RelayDecoupleAsync(new(otherHead.Headers), BurcatInstance.Build(reference), false, cancellation);
                    cancellation.ThrowIfCancellationRequested();

                    await SendObject(head.Stream, decoupleException is BurcatException exception ? new(exception) : BurcatInstance.Build<BurcatException>(), cancellation);
                    cancellation.ThrowIfCancellationRequested();

                    if (!await RecieveScheme<EndDecoupleSchematic>(head.Stream, cancellation)) throw new InvalidDataException($"Expected scheme with identifier {GetClassIdentity<EndDecoupleSchematic>()}, but data read doesn't correspond to");
                    cancellation.ThrowIfCancellationRequested();

                    if (!await RecieveScheme<EndCommunicationSchematic>(head.Stream, cancellation)) throw new InvalidDataException($"Expected scheme with identifier {GetClassIdentity<EndCommunicationSchematic>()}, but data read doesn't correspond to");
                    cancellation.ThrowIfCancellationRequested();

                    await head.Stream.FlushAsync();
                    cancellation.ThrowIfCancellationRequested();

                    return new(BurcatExchangeType.Decouple, instance, BurcatInstance.Build(decoupleException));
                }
                else if (scheme == GetClassIdentity<BeginActionSchematic>())
                {
                    BurcatHead otherHead = await InverseExchangeHead(head.Stream, Headers, cancellation);
                    cancellation.ThrowIfCancellationRequested();

                    BurcatInstance instance = await RecieveObject(head.Stream, otherHead, cancellation);
                    cancellation.ThrowIfCancellationRequested();

                    if (!await RecieveScheme<ActionScheme>(head.Stream, cancellation)) throw new InvalidDataException($"Expected scheme with identifier {GetClassIdentity<ActionScheme>()}, but data read doesn't correspond to");
                    cancellation.ThrowIfCancellationRequested();

                    byte[] length = new byte[4];
                    await head.Stream.ReadExactlyAsync(length, cancellation);
                    cancellation.ThrowIfCancellationRequested();
                    byte[] data = new byte[BitConverter.ToInt32(length)];
                    await head.Stream.ReadExactlyAsync(data, cancellation);
                    cancellation.ThrowIfCancellationRequested();

                    if (!await RecieveScheme<ActionScheme>(head.Stream, cancellation)) throw new InvalidDataException($"Expected scheme with identifier {GetClassIdentity<ActionScheme>()}, but data read doesn't correspond to");
                    cancellation.ThrowIfCancellationRequested();

                    if (!await RecieveScheme<ParameterScheme>(head.Stream, cancellation)) throw new InvalidDataException($"Expected scheme with identifier {GetClassIdentity<ParameterScheme>()}, but data read doesn't correspond to");
                    cancellation.ThrowIfCancellationRequested();

                    await head.Stream.ReadExactlyAsync(length, cancellation);
                    IBurcatObject?[] parameters = new IBurcatObject?[BitConverter.ToInt32(length)];
                    for (int j = 0; j < parameters.Length; j++)
                    {
                        cancellation.ThrowIfCancellationRequested();
                        parameters[j] = (await RecieveObject(head.Stream, otherHead, cancellation)).Value;
                        cancellation.ThrowIfCancellationRequested();
                    }
                    if (!await RecieveScheme<ParameterScheme>(head.Stream, cancellation)) throw new InvalidDataException($"Expected scheme with identifier {GetClassIdentity<ParameterScheme>()}, but data read doesn't correspond to");
                    cancellation.ThrowIfCancellationRequested();

                    ActionResult result = await RelayActionAsync(new(otherHead.Headers), instance, Encoding.Unicode.GetString(data), parameters, false, cancellation);
                    cancellation.ThrowIfCancellationRequested();

                    await SendObject(head.Stream, result, cancellation);
                    cancellation.ThrowIfCancellationRequested();

                    if (!await RecieveScheme<EndActionSchematic>(head.Stream, cancellation)) throw new InvalidDataException($"Expected scheme with identifier {GetClassIdentity<EndActionSchematic>()}, but data read doesn't correspond to");
                    cancellation.ThrowIfCancellationRequested();

                    if (!await RecieveScheme<EndCommunicationSchematic>(head.Stream, cancellation)) throw new InvalidDataException($"Expected scheme with identifier {GetClassIdentity<EndCommunicationSchematic>()}, but data read doesn't correspond to");
                    cancellation.ThrowIfCancellationRequested();

                    await head.Stream.FlushAsync();
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
        /// <param name="head">The source stream.</param>
        /// <param name="token">The optional cancellation token.</param>
        /// <returns>The result of the received exchange.</returns>
        public static ExchangeResult Receive(BurcatDirectionalHead head, CancellationToken? token = null) => ReceiveAsync(head, token).GetAwaiter().GetResult();

        private static async Task<BurcatHead> ExchangeHead(IdentifiedStream stream, BurcatHeaderSet headers, CancellationToken token)
        {
            await stream.WriteAsync(GetClassIdentity<StreamScheme>().ToByteArray(), token);
            token.ThrowIfCancellationRequested();

            await stream.WriteAsync(stream.Identifier.ToByteArray(), token);
            token.ThrowIfCancellationRequested();

            await stream.WriteAsync(GetClassIdentity<StreamScheme>().ToByteArray(), token);
            token.ThrowIfCancellationRequested();

            if (!await RecieveScheme<StreamScheme>(stream, token)) throw new InvalidDataException($"Expected scheme with identifier {GetClassIdentity<StreamScheme>()}, but data read doesn't correspond to");
            token.ThrowIfCancellationRequested();

            byte[] guid = new byte[16];
            await stream.ReadExactlyAsync(guid, token); Guid otherStreamID = new(guid);
            token.ThrowIfCancellationRequested();

            if (!await RecieveScheme<StreamScheme>(stream, token)) throw new InvalidDataException($"Expected scheme with identifier {GetClassIdentity<StreamScheme>()}, but data read doesn't correspond to");
            token.ThrowIfCancellationRequested();

            if (headers is not null)
            {
                await stream.WriteAsync(GetClassIdentity<HeadersScheme>().ToByteArray(), token);
                token.ThrowIfCancellationRequested();

                await stream.WriteAsync(BitConverter.GetBytes(headers.Count), token);
                token.ThrowIfCancellationRequested();

                foreach (BurcatHeader header in headers)
                {
                    await stream.WriteAsync(header.Package.ToByteArray(), token);
                    token.ThrowIfCancellationRequested();

                    await stream.WriteAsync(BitConverter.GetBytes(header.Name.Length), token);
                    await stream.WriteAsync(Encoding.UTF8.GetBytes(header.Name), token);
                    token.ThrowIfCancellationRequested();

                    await stream.WriteAsync(BitConverter.GetBytes(header.Value?.Length ?? -1), token);
                    if (header.Value is not null) await stream.WriteAsync(Encoding.UTF8.GetBytes(header.Value), token);
                    token.ThrowIfCancellationRequested();
                }

                await stream.WriteAsync(GetClassIdentity<HeadersScheme>().ToByteArray(), token);
                token.ThrowIfCancellationRequested();

                if (!await RecieveScheme<HeadersScheme>(stream, token)) throw new InvalidDataException($"Expected scheme with identifier {GetClassIdentity<HeadersScheme>()}, but data read doesn't correspond to");
                token.ThrowIfCancellationRequested();

                byte[] length = new byte[4];
                await stream.ReadExactlyAsync(length, token);
                token.ThrowIfCancellationRequested();

                headers = [];
                int headerCount = BitConverter.ToInt32(length);
                for (int i = 0; i < headerCount; i++)
                {
                    await stream.ReadExactlyAsync(guid, token); Guid package = new(guid);
                    token.ThrowIfCancellationRequested();

                    await stream.ReadExactlyAsync(length, token);
                    token.ThrowIfCancellationRequested();

                    byte[] variable = new byte[BitConverter.ToInt32(length)];
                    await stream.ReadExactlyAsync(variable, token); string name = Encoding.UTF8.GetString(variable);
                    token.ThrowIfCancellationRequested();

                    await stream.ReadExactlyAsync(length, token); int valueLength = BitConverter.ToInt32(length);
                    token.ThrowIfCancellationRequested();

                    string? value = null;
                    if (valueLength >= 0)
                    {
                        variable = new byte[valueLength];
                        await stream.ReadExactlyAsync(variable, token); value = Encoding.UTF8.GetString(variable);
                        token.ThrowIfCancellationRequested();
                    }

                    headers.Add(new(package, name, value));
                }

                if (!await RecieveScheme<HeadersScheme>(stream, token)) throw new InvalidDataException($"Expected scheme with identifier {GetClassIdentity<HeadersScheme>()}, but data read doesn't correspond to");
                token.ThrowIfCancellationRequested();
            }

            return new(otherStreamID, headers ?? []);
        }
        private static async Task<BurcatHead> InverseExchangeHead(IdentifiedStream stream, BurcatHeaderSet headers, CancellationToken token)
        {
            if (!await RecieveScheme<StreamScheme>(stream, token)) throw new InvalidDataException($"Expected scheme with identifier {GetClassIdentity<StreamScheme>()}, but data read doesn't correspond to");
            token.ThrowIfCancellationRequested();

            byte[] guid = new byte[16];
            await stream.ReadExactlyAsync(guid, token); Guid otherStreamID = new(guid);
            token.ThrowIfCancellationRequested();

            if (!await RecieveScheme<StreamScheme>(stream, token)) throw new InvalidDataException($"Expected scheme with identifier {GetClassIdentity<StreamScheme>()}, but data read doesn't correspond to");
            token.ThrowIfCancellationRequested();

            await stream.WriteAsync(GetClassIdentity<StreamScheme>().ToByteArray(), token);
            token.ThrowIfCancellationRequested();

            await stream.WriteAsync(stream.Identifier.ToByteArray(), token);
            token.ThrowIfCancellationRequested();

            await stream.WriteAsync(GetClassIdentity<StreamScheme>().ToByteArray(), token);
            token.ThrowIfCancellationRequested();

            if (headers is not null)
            {
                if (!await RecieveScheme<HeadersScheme>(stream, token)) throw new InvalidDataException($"Expected scheme with identifier {GetClassIdentity<HeadersScheme>()}, but data read doesn't correspond to");
                token.ThrowIfCancellationRequested();

                byte[] length = new byte[4];
                await stream.ReadExactlyAsync(length, token);
                token.ThrowIfCancellationRequested();

                headers = [];
                int headerCount = BitConverter.ToInt32(length);
                for (int i = 0; i < headerCount; i++)
                {
                    await stream.ReadExactlyAsync(guid, token); Guid package = new(guid);
                    token.ThrowIfCancellationRequested();

                    await stream.ReadExactlyAsync(length, token);
                    token.ThrowIfCancellationRequested();

                    byte[] variable = new byte[BitConverter.ToInt32(length)];
                    await stream.ReadExactlyAsync(variable, token); string name = Encoding.UTF8.GetString(variable);
                    token.ThrowIfCancellationRequested();

                    await stream.ReadExactlyAsync(length, token); int valueLength = BitConverter.ToInt32(length);
                    token.ThrowIfCancellationRequested();

                    string? value = null;
                    if (valueLength >= 0)
                    {
                        variable = new byte[valueLength];
                        await stream.ReadExactlyAsync(variable, token); value = Encoding.UTF8.GetString(variable);
                        token.ThrowIfCancellationRequested();
                    }

                    headers.Add(new(package, name, value));
                }

                if (!await RecieveScheme<HeadersScheme>(stream, token)) throw new InvalidDataException($"Expected scheme with identifier {GetClassIdentity<HeadersScheme>()}, but data read doesn't correspond to");
                token.ThrowIfCancellationRequested();

                await stream.WriteAsync(GetClassIdentity<HeadersScheme>().ToByteArray(), token);
                token.ThrowIfCancellationRequested();

                await stream.WriteAsync(BitConverter.GetBytes(headers.Count), token);
                token.ThrowIfCancellationRequested();

                foreach (BurcatHeader header in headers)
                {
                    await stream.WriteAsync(header.Package.ToByteArray(), token);
                    token.ThrowIfCancellationRequested();

                    await stream.WriteAsync(BitConverter.GetBytes(header.Name.Length), token);
                    await stream.WriteAsync(Encoding.UTF8.GetBytes(header.Name), token);
                    token.ThrowIfCancellationRequested();

                    await stream.WriteAsync(BitConverter.GetBytes(header.Value?.Length ?? -1), token);
                    if (header.Value is not null) await stream.WriteAsync(Encoding.UTF8.GetBytes(header.Value), token);
                    token.ThrowIfCancellationRequested();
                }

                await stream.WriteAsync(GetClassIdentity<HeadersScheme>().ToByteArray(), token);
                token.ThrowIfCancellationRequested();
            }

            return new(otherStreamID, headers ?? []);
        }

        private static async Task SendObject(IdentifiedStream stream, BurcatInstance instance, CancellationToken token)
        {
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

        private static async Task<BurcatInstance> RecieveObject(IdentifiedStream stream, BurcatHead otherHead, CancellationToken token)
        {
            if (!await RecieveScheme<VersionScheme>(stream, token)) throw new InvalidDataException($"Expected scheme with identifier {GetClassIdentity<VersionScheme>()}, but data read doesn't correspond to");
            token.ThrowIfCancellationRequested();

            byte[] guid = new byte[16];
            await stream.ReadExactlyAsync(guid, token); Guid classID = new(guid);
            token.ThrowIfCancellationRequested();

            await stream.ReadExactlyAsync(guid, token); Guid objectID = new(guid);
            token.ThrowIfCancellationRequested();

            if (!await RecieveScheme<VersionScheme>(stream, token)) throw new InvalidDataException($"Expected scheme with identifier {GetClassIdentity<VersionScheme>()}, but data read doesn't correspond to");
            token.ThrowIfCancellationRequested();

            if (AcceptedIdentities.TryGetType(classID, out Type? referenceType))
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

                            reference = InternalProvider.GetObject(otherHead, referenceType, objectID) is IBurcatObject objectInternal && objectInternal.Revision == revisionID ? objectInternal : null;
                            if (reference is null && ExternalProvider is not null) reference = (await ExternalProvider.GetObject(new(otherHead.Headers), referenceType, objectID, token)) is IBurcatObject objectExternal && objectExternal.Revision == revisionID ? objectExternal : null;
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
                                fieldValues[i] = (await RecieveObject(stream, otherHead, token)).ForceValue<BurcatField>();
                                token.ThrowIfCancellationRequested();
                            }
                            if (!await RecieveScheme<FieldScheme>(stream, token)) throw new InvalidDataException($"Expected scheme with identifier {GetClassIdentity<FieldScheme>()}, but data read doesn't correspond to");

                            if (!await RecieveScheme<ConstructorScheme>(stream, token)) throw new InvalidDataException($"Expected scheme with identifier {GetClassIdentity<ConstructorScheme>()}, but data read doesn't correspond to");
                            await stream.ReadExactlyAsync(length, token);
                            IBurcatObject?[] constructorValues = new IBurcatObject?[BitConverter.ToInt32(length)];
                            for (int i = 0; i < constructorValues.Length; i++)
                            {
                                token.ThrowIfCancellationRequested();
                                constructorValues[i] = (await RecieveObject(stream, otherHead, token)).Value;
                                token.ThrowIfCancellationRequested();
                            }
                            if (!await RecieveScheme<ConstructorScheme>(stream, token)) throw new InvalidDataException($"Expected scheme with identifier {GetClassIdentity<ConstructorScheme>()}, but data read doesn't correspond to");

                            reference = InternalProvider.ConstructObject(otherHead, referenceType, objectID, revisionID, constructorValues, fieldValues);
                            token.ThrowIfCancellationRequested();

                            if (reference is not null && objectID != Guid.Empty) InternalProvider.CoupleCache(otherHead, reference, false);
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
        private sealed class BeginIdentitiesSchematic : Scheme { }
        [BurcatIdentity("00000000-0000-0000-0000-000000001101")]
        private sealed class EndIdentitiesSchematic : Scheme { }
        [BurcatIdentity("00000000-0000-0000-0000-000000001002")]
        private sealed class BeginHeadersSchematic : Scheme { }
        [BurcatIdentity("00000000-0000-0000-0000-000000001102")]
        private sealed class EndHeadersSchematic : Scheme { }
        [BurcatIdentity("00000000-0000-0000-0000-000000001003")]
        private sealed class BeginObjectSchematic : Scheme { }
        [BurcatIdentity("00000000-0000-0000-0000-000000001103")]
        private sealed class EndObjectSchematic : Scheme { }
        [BurcatIdentity("00000000-0000-0000-0000-000000001004")]
        private sealed class BeginRevisionRequestSchematic : Scheme { }
        [BurcatIdentity("00000000-0000-0000-0000-000000001104")]
        private sealed class EndRevisionRequestSchematic : Scheme { }
        [BurcatIdentity("00000000-0000-0000-0000-000000001005")]
        private sealed class BeginObjectRequestSchematic : Scheme { }
        [BurcatIdentity("00000000-0000-0000-0000-000000001105")]
        private sealed class EndObjectRequestSchematic : Scheme { }
        [BurcatIdentity("00000000-0000-0000-0000-000000001006")]
        private sealed class BeginActionSchematic : Scheme { }
        [BurcatIdentity("00000000-0000-0000-0000-000000001106")]
        private sealed class EndActionSchematic : Scheme { }
        [BurcatIdentity("00000000-0000-0000-0000-000000001007")]
        private sealed class BeginCoupleSchematic : Scheme { }
        [BurcatIdentity("00000000-0000-0000-0000-000000001107")]
        private sealed class EndCoupleSchematic : Scheme { }
        [BurcatIdentity("00000000-0000-0000-0000-000000001008")]
        private sealed class BeginDecoupleSchematic : Scheme { }
        [BurcatIdentity("00000000-0000-0000-0000-000000001108")]
        private sealed class EndDecoupleSchematic : Scheme { }
        [BurcatIdentity("00000000-0000-0000-0000-000000001009")]
        private sealed class BeginUpgradeSchematic : Scheme { }
        [BurcatIdentity("00000000-0000-0000-0000-000000001109")]
        private sealed class EndUpgradeSchematic : Scheme { }
    }
}
