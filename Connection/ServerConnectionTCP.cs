using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace BurcatProtocol.Connection
{
    /// <summary>
    /// Base TCP server connection that accepts SSL-authenticated Burcat client streams.
    /// </summary>
    public abstract class ServerConnectionTCP : IDisposable
    {
        private TcpListener Server { get; }
        private ConcurrentQueue<Stream> Streams { get; set; } = [];

        /// <summary>
        /// Gets the SSL server authentication options used for accepted clients.
        /// </summary>
        protected abstract SslServerAuthenticationOptions SslOptions { get; }

        /// <summary>
        /// Initializes a TCP server connection.
        /// </summary>
        /// <param name="ip">The local IP address to bind.</param>
        /// <param name="port">The local port to bind.</param>
        public ServerConnectionTCP(IPAddress ip, int port) { Server = new(ip, port); }

        /// <summary>
        /// Starts listening for clients.
        /// </summary>
        public void Start() => Server.Start();

        /// <summary>
        /// Stops listening and closes accepted streams.
        /// </summary>
        public void Stop()
        {
            ConcurrentQueue<Stream> current = Streams;
            Streams = [];

            while (current.TryDequeue(out Stream? stream)) stream.Close();
            Server.Stop();
        }

        /// <summary>
        /// Accepts one client and authenticates an SSL stream.
        /// </summary>
        /// <param name="token">The cancellation token.</param>
        /// <returns>The authenticated client stream.</returns>
        public async Task<Stream> AcceptAsync(CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            StartsWaitingClient();
            TcpClient client = await Server.AcceptTcpClientAsync(token);
            StopsWaitingClient();
            token.ThrowIfCancellationRequested();

            if (ValidateAddress(((IPEndPoint)client.Client.RemoteEndPoint!).Address))
            {
                token.ThrowIfCancellationRequested();
                SslStream stream = GetSslStream(client.GetStream());
                await stream.AuthenticateAsServerAsync(SslOptions, token);
                token.ThrowIfCancellationRequested();

                return stream;
            }
            else throw new InvalidOperationException("The server has denied the communication to the client's address");
        }

        /// <summary>
        /// Accepts one client and authenticates an SSL stream.
        /// </summary>
        /// <returns>The authenticated client stream.</returns>
        public async Task<Stream> AcceptAsync() => await AcceptAsync(CancellationToken.None);

        /// <summary>
        /// Accepts one client and authenticates an SSL stream.
        /// </summary>
        /// <param name="token">The cancellation token.</param>
        /// <returns>The authenticated client stream.</returns>
        public Stream Accept(CancellationToken token) => AcceptAsync(token).GetAwaiter().GetResult();

        /// <summary>
        /// Accepts one client and authenticates an SSL stream.
        /// </summary>
        /// <returns>The authenticated client stream.</returns>
        public Stream Accept() => Accept(CancellationToken.None);

        /// <summary>
        /// Accepts clients continuously as an asynchronous stream.
        /// </summary>
        /// <param name="token">The cancellation token.</param>
        /// <returns>The accepted authenticated streams.</returns>
        public async IAsyncEnumerable<Stream> AcceptMultipleAsync([EnumeratorCancellation] CancellationToken token)
        {
            while (true)
            {
                yield return await AcceptAsync(token);
                token.ThrowIfCancellationRequested();
            }
        }

        /// <summary>
        /// Accepts clients continuously as an asynchronous stream.
        /// </summary>
        /// <returns>The accepted authenticated streams.</returns>
        public async IAsyncEnumerable<Stream> AcceptMultipleAsync()
        {
            await foreach (Stream stream in AcceptMultipleAsync(CancellationToken.None))
                yield return stream;
        }

        /// <summary>
        /// Accepts clients continuously as a blocking enumerable.
        /// </summary>
        /// <param name="token">The cancellation token.</param>
        /// <returns>The accepted authenticated streams.</returns>
        public IEnumerable<Stream> AcceptMultiple(CancellationToken token) => AcceptMultipleAsync(token).ToBlockingEnumerable(token);

        /// <summary>
        /// Accepts clients continuously as a blocking enumerable.
        /// </summary>
        /// <returns>The accepted authenticated streams.</returns>
        public IEnumerable<Stream> AcceptMultiple() => AcceptMultiple(CancellationToken.None);

        /// <summary>
        /// Called immediately before waiting for a client.
        /// </summary>
        protected virtual void StartsWaitingClient() { }

        /// <summary>
        /// Called after a client has been accepted.
        /// </summary>
        protected virtual void StopsWaitingClient() { }

        /// <summary>
        /// Validates whether a client address may connect.
        /// </summary>
        /// <param name="address">The remote address.</param>
        /// <returns><see langword="true"/> when the address is allowed; otherwise, <see langword="false"/>.</returns>
        protected virtual bool ValidateAddress(IPAddress address) => true;

        /// <summary>
        /// Wraps a raw stream in an SSL stream.
        /// </summary>
        /// <param name="stream">The raw stream to wrap.</param>
        /// <returns>The SSL stream.</returns>
        protected virtual SslStream GetSslStream(Stream stream) => new(stream);

        /// <inheritdoc/>
        public void Dispose() { Stop(); Server.Dispose(); GC.SuppressFinalize(this); }
    }
}
