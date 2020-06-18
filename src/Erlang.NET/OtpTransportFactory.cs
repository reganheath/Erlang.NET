using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace Erlang.NET
{
    public interface OtpTransportFactory
    {
        OtpTransport CreateTransport(string addr, int port);
        OtpTransport CreateTransport(IPEndPoint addr);
        OtpServerTransport CreateServerTransport(int port);
    }
}
