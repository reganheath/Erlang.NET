﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Erlang.NET
{
    public interface OtpTransport : IDisposable
    {
        Stream GetInputStream();
        Stream GetOutputStream();
        void Close();
    }
}
