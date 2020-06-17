using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Erlang.NET
{
    public interface OtpServerTransport
    {
        void start();
        int getLocalPort();
        OtpTransport accept();
        void close();
    }
}
