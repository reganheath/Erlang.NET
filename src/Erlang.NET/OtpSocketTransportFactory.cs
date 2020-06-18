using System.Net;

namespace Erlang.NET
{
    public class OtpSocketTransportFactory : OtpTransportFactory
    {
        public OtpTransport CreateTransport(string addr, int port) => new OtpSocketTransport(addr, port);

        public OtpTransport CreateTransport(IPEndPoint addr) => new OtpSocketTransport(addr);

        public OtpServerTransport CreateServerTransport(int port) => new OtpServerSocketTransport(port);
    }
}
