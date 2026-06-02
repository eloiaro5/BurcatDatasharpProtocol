using System.Net.Security;
using System.Net.Sockets;

namespace BurcatProtocol.Connection
{
    /// <summary>
    /// Base TCP client connection that establishes an SSL stream to a Burcat server.
    /// </summary>
    public abstract class ClientConnectionTCP : IDisposable
    {
        /// <summary>
        /// Gets the default Burcat TCP server port.
        /// </summary>
        public const int DefaultServerPort = 5555;

        private TcpClient Client { get; } = new();
        private bool Disposed { get; set; } = false;
        private Stream? Stream { get; set; }

        /// <summary>
        /// Gets the SSL client authentication options used when connecting.
        /// </summary>
        protected abstract SslClientAuthenticationOptions SslOptions { get; }

        /// <summary>
        /// Initializes a TCP client connection.
        /// </summary>
        public ClientConnectionTCP() { }

        /// <summary>
        /// Connects to a server and authenticates an SSL stream.
        /// </summary>
        /// <param name="server">The server host name or address.</param>
        /// <param name="port">The server port.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns>The authenticated stream.</returns>
        public async Task<Stream> StartAsync(string server, int port, CancellationToken token)
        {
            if (Disposed) throw new InvalidOperationException("Cannot start a disposed TCP Client");
            else if (Client.Connected) throw new InvalidOperationException("The TCP Client is alredy connected");
            else
            {
                SslStream? stream = null;
                try
                {
                    await Client.ConnectAsync(server, port, token);
                    token.Register(Client.Close);
                    token.ThrowIfCancellationRequested();
                    stream = GetSslStream(Client.GetStream());
                    await stream.AuthenticateAsClientAsync(SslOptions, token);
                    token.ThrowIfCancellationRequested();

                    Stream = stream;
                    return stream;
                }
                catch (Exception) { stream?.Close(); throw; }
            }
        }

        /// <summary>
        /// Connects to a server and authenticates an SSL stream.
        /// </summary>
        /// <param name="server">The server host name or address.</param>
        /// <param name="port">The server port.</param>
        /// <returns>The authenticated stream.</returns>
        public async Task<Stream> StartAsync(string server, int port) => await StartAsync(server, port, CancellationToken.None);

        /// <summary>
        /// Connects to a server on the default port and authenticates an SSL stream.
        /// </summary>
        /// <param name="server">The server host name or address.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns>The authenticated stream.</returns>
        public async Task<Stream> StartAsync(string server, CancellationToken token) => await StartAsync(server, DefaultServerPort, token);

        /// <summary>
        /// Connects to a server on the default port and authenticates an SSL stream.
        /// </summary>
        /// <param name="server">The server host name or address.</param>
        /// <returns>The authenticated stream.</returns>
        public async Task<Stream> StartAsync(string server) => await StartAsync(server, CancellationToken.None);

        /// <summary>
        /// Connects to a server and authenticates an SSL stream.
        /// </summary>
        /// <param name="server">The server host name or address.</param>
        /// <param name="port">The server port.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns>The authenticated stream.</returns>
        public Stream Start(string server, int port, CancellationToken token) => StartAsync(server, port, token).GetAwaiter().GetResult();

        /// <summary>
        /// Connects to a server and authenticates an SSL stream.
        /// </summary>
        /// <param name="server">The server host name or address.</param>
        /// <param name="port">The server port.</param>
        /// <returns>The authenticated stream.</returns>
        public Stream Start(string server, int port) => Start(server, port, CancellationToken.None);

        /// <summary>
        /// Connects to a server on the default port and authenticates an SSL stream.
        /// </summary>
        /// <param name="server">The server host name or address.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns>The authenticated stream.</returns>
        public Stream Start(string server, CancellationToken token) => StartAsync(server, token).GetAwaiter().GetResult();

        /// <summary>
        /// Connects to a server on the default port and authenticates an SSL stream.
        /// </summary>
        /// <param name="server">The server host name or address.</param>
        /// <returns>The authenticated stream.</returns>
        public Stream Start(string server) => Start(server, CancellationToken.None);

        /// <summary>
        /// Stops the current connection and closes the underlying streams.
        /// </summary>
        public void Stop() { Stream?.Close(); Client.Close(); Stream = null; }

        /// <summary>
        /// Wraps a raw stream in an SSL stream.
        /// </summary>
        /// <param name="stream">The raw stream to wrap.</param>
        /// <returns>The SSL stream.</returns>
        protected virtual SslStream GetSslStream(Stream stream) => new(stream);

        /// <inheritdoc/>
        public void Dispose()
        {
            if (Client.Connected) throw new InvalidOperationException("Cannot dispose a connected TCP Client; Cancel it's opertaion and the dispose");
            else if (!Disposed)
            {
                Disposed = true;
                Client.Dispose();
                GC.SuppressFinalize(this);
            }
        }
    }
}
