using System;
using System.IO;
using System.Net;
using System.Net.Sockets;

namespace Erlang.NET
{
    public class OtpSocketTransport : OtpTransport
    {
        private readonly TcpClient client;
        private readonly Stream inputStream;
        private readonly Stream outputStream;

        public OtpSocketTransport(string addr, int port)
        {
            client = new TcpClient()
            {
                NoDelay = true
            };
            client.Connect(addr, port);
            inputStream = new BufferedStream(client.GetStream());
            outputStream = client.GetStream();
            KeepAlive = true;
        }

        public OtpSocketTransport(IPEndPoint addr)
        {
            client = new TcpClient()
            {
                NoDelay = true
            };
            client.Connect(addr);
            inputStream = new BufferedStream(client.GetStream());
            outputStream = client.GetStream();
            KeepAlive = true;
        }

        public OtpSocketTransport(TcpClient s)
        {
            client = s;
            inputStream = new BufferedStream(client.GetStream());
            outputStream = client.GetStream();
            KeepAlive = true;
        }

        public bool NoDelay
        {
            get => (bool)client.Client.GetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.NoDelay);
            set => client.Client.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.NoDelay, value);
        }

        public bool KeepAlive
        {
            get => (bool)client.Client.GetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive);
            set => client.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, value);
        }

        public Stream GetInputStream() => inputStream;

        public Stream GetOutputStream() => outputStream;

        public void Close()
        {
            client.GetStream().Close();
            client.Close();
        }

        public override string ToString() => client.Client.RemoteEndPoint.ToString();

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
                Close();
        }
    }
}
