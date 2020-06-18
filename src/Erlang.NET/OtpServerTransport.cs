using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Erlang.NET
{
    public interface OtpServerTransport
    {
        void Start();
        int GetLocalPort();
        OtpTransport Accept();
        void Close();
    }
}
