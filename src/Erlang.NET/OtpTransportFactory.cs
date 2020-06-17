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
        OtpTransport createTransport(string addr, int port);
        OtpTransport createTransport(IPEndPoint addr);
        OtpServerTransport createServerTransport(int port);
    }
}
