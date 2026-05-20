using System.Net.Security;
using System.Net.Sockets;

namespace BurcatProtocol.Connection
{
    public abstract class ClientConnectionTCP : IDisposable
    {
        public const int DefaultServerPort = 5555;

        private TcpClient Client { get; } = new();
        private bool Disposed { get; set; } = false;
        private Stream? Stream { get; set; }

        protected abstract SslClientAuthenticationOptions SslOptions { get; }

        public ClientConnectionTCP() { }

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
        public async Task<Stream> StartAsync(string server, int port) => await StartAsync(server, port, CancellationToken.None);
        public async Task<Stream> StartAsync(string server, CancellationToken token) => await StartAsync(server, DefaultServerPort, token);
        public async Task<Stream> StartAsync(string server) => await StartAsync(server, CancellationToken.None);
        public Stream Start(string server, int port, CancellationToken token) => StartAsync(server, port, token).GetAwaiter().GetResult();
        public Stream Start(string server, int port) => Start(server, port, CancellationToken.None);
        public Stream Start(string server, CancellationToken token) => StartAsync(server, token).GetAwaiter().GetResult();
        public Stream Start(string server) => Start(server, CancellationToken.None);

        public void Stop() { Stream?.Close(); Client.Close(); Stream = null; }

        protected virtual SslStream GetSslStream(Stream stream) => new(stream);

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
