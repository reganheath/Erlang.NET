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
        public OtpTransport CreateTransport(string addr, int port)
        {
            return new OtpSocketTransport(addr, port);
        }

        public OtpTransport CreateTransport(IPEndPoint addr)
        {
            return new OtpSocketTransport(addr);
        }

        public OtpServerTransport CreateServerTransport(int port)
        {
            return new OtpServerSocketTransport(port);
        }
    }
}
