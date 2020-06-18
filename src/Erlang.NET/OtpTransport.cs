using System;
using System.IO;

namespace Erlang.NET
{
    public interface OtpTransport : IDisposable
    {
        Stream GetInputStream();
        Stream GetOutputStream();
        void Close();
    }
}
