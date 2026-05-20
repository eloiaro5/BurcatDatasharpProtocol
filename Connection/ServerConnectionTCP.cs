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
    public abstract class ServerConnectionTCP : IDisposable
    {
        private TcpListener Server { get; }
        private ConcurrentQueue<Stream> Streams { get; set; } = [];

        protected abstract SslServerAuthenticationOptions SslOptions { get; }

        public ServerConnectionTCP(IPAddress ip, int port) { Server = new(ip, port); }

        public void Start() => Server.Start();
        public void Stop()
        {
            ConcurrentQueue<Stream> current = Streams;
            Streams = [];

            while (current.TryDequeue(out Stream? stream)) stream.Close();
            Server.Stop();
        }

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
        public async Task<Stream> AcceptAsync() => await AcceptAsync(CancellationToken.None);
        public Stream Accept(CancellationToken token) => AcceptAsync(token).GetAwaiter().GetResult();
        public Stream Accept() => Accept(CancellationToken.None);

        public async IAsyncEnumerable<Stream> AcceptMultipleAsync([EnumeratorCancellation] CancellationToken token)
        {
            while (true)
            {
                yield return await AcceptAsync(token);
                token.ThrowIfCancellationRequested();
            }
        }
        public async IAsyncEnumerable<Stream> AcceptMultipleAsync()
        {
            await foreach (Stream stream in AcceptMultipleAsync(CancellationToken.None))
                yield return stream;
        }
        public IEnumerable<Stream> AcceptMultiple(CancellationToken token) => AcceptMultipleAsync(token).ToBlockingEnumerable(token);
        public IEnumerable<Stream> AcceptMultiple() => AcceptMultiple(CancellationToken.None);

        protected virtual void StartsWaitingClient() { }
        protected virtual void StopsWaitingClient() { }

        protected virtual bool ValidateAddress(IPAddress address) => true;

        protected virtual SslStream GetSslStream(Stream stream) => new(stream);

        public void Dispose() { Stop(); Server.Dispose(); GC.SuppressFinalize(this); }
    }
}
