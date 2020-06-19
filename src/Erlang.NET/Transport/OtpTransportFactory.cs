using System.Net;

namespace Erlang.NET
{
    public interface OtpTransportFactory
    {
        OtpTransport CreateTransport(string addr, int port);
        OtpTransport CreateTransport(IPEndPoint addr);
        OtpServerTransport CreateServerTransport(int port);
    }
}
