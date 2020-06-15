﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace Erlang.NET
{
    public class OtpSocketTransport : OtpTransport
    {
        private readonly TcpClient client;
        private readonly Stream inputStream;
        private readonly Stream outputStream;

        public OtpSocketTransport(String addr, int port)
        {
            this.client = new TcpClient()
            {
                NoDelay = true
            };
            this.client.Connect(addr, port);
            this.inputStream = new BufferedStream(this.client.GetStream());
            this.outputStream = this.client.GetStream();
            KeepAlive = true;
        }

        public OtpSocketTransport(IPEndPoint addr)
        {
            this.client = new TcpClient()
            {
                NoDelay = true
            };
            this.client.Connect(addr);
            this.inputStream = new BufferedStream(this.client.GetStream());
            this.outputStream = this.client.GetStream();
            KeepAlive = true;
        }

        public OtpSocketTransport(TcpClient s)
        {
            this.client = s;
            this.inputStream = new BufferedStream(this.client.GetStream());
            this.outputStream = this.client.GetStream();
            KeepAlive = true;
        }

        public bool NoDelay
        {
            get
            {
                return (bool)client.Client.GetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.NoDelay);
            }
            set
            {
                client.Client.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.NoDelay, value);
            }
        }

        public bool KeepAlive
        {
            get
            {
                return (bool)client.Client.GetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive);
            }
            set
            {
                client.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, value);
            }
        }

        public Stream getInputStream()
        {
            return inputStream;
        }

        public Stream getOutputStream()
        {
            return outputStream;
        }

        public void close()
        {
            client.GetStream().Close();
            client.Close();
        }

        public override string ToString()
        {
            return client.Client.RemoteEndPoint.ToString();
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                close();
            }
        }
    }
}
