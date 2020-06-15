using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace Erlang.NET
{
    public class OtpSocketTransportFactory : OtpTransportFactory
    {
        public OtpTransport createTransport(String addr, int port)
        {
            return new OtpSocketTransport(addr, port);
        }

        public OtpTransport createTransport(IPEndPoint addr)
        {
            return new OtpSocketTransport(addr);
        }

        public OtpServerTransport createServerTransport(int port)
        {
            return new OtpServerSocketTransport(port);
        }
    }
}
