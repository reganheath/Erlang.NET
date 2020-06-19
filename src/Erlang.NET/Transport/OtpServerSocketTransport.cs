using System.Net;
using System.Net.Sockets;

namespace Erlang.NET
{
    public class OtpServerSocketTransport : OtpServerTransport
    {
        private readonly TcpListener socket;

        public OtpServerSocketTransport(int port, bool start = true)
        {
            socket = new TcpListener(IPAddress.Any, port);
            if (start)
                socket.Start();
        }

        public void Start()
        {
            socket.Start();
        }

        public int GetLocalPort()
        {
            return ((IPEndPoint)socket.LocalEndpoint).Port;
        }

        public OtpTransport Accept()
        {
            TcpClient sock = socket.AcceptTcpClient();
            sock.NoDelay = true;
            return new OtpSocketTransport(sock);
        }

        public void Close()
        {
            socket.Stop();
        }
    }
}
