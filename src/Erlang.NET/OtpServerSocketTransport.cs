﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace Erlang.NET
{
    public class OtpServerSocketTransport : OtpServerTransport
    {
        private TcpListener socket;

        public OtpServerSocketTransport(int port)
        {
            socket = new TcpListener(IPAddress.Any, port);
            socket.Start();
        }

        public int getLocalPort()
        {
            return ((IPEndPoint)socket.LocalEndpoint).Port;
        }

        public OtpTransport accept()
        {
            TcpClient sock = socket.AcceptTcpClient();
            sock.NoDelay = true;
            return new OtpSocketTransport(sock);
        }

        public void close()
        {
            socket.Stop();
        }
    }
}
