/*
 * Copyright 2020 Regan Heath
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */
using System;
using System.IO;
using System.Net;
using System.Net.Sockets;

namespace Erlang.NET
{
    public class OtpSocketTransport : IOtpTransport
    {
        private readonly TcpClient client;
        
        public Stream InputStream { get; private set; }
        public Stream OutputStream { get; private set; }

        public OtpSocketTransport(string addr, int port)
        {
            client = new TcpClient() { NoDelay = true };
            client.Connect(addr, port);
            InputStream = new BufferedStream(client.GetStream());
            OutputStream = client.GetStream();
            KeepAlive = true;
        }

        public OtpSocketTransport(IPEndPoint addr)
        {
            client = new TcpClient() { NoDelay = true };
            client.Connect(addr);
            InputStream = new BufferedStream(client.GetStream());
            OutputStream = client.GetStream();
            KeepAlive = true;
        }

        public OtpSocketTransport(TcpClient sock)
        {
            client = sock;
            InputStream = new BufferedStream(client.GetStream());
            OutputStream = client.GetStream();
            KeepAlive = true;
        }

        public bool NoDelay
        {
            get => (bool)client.Client.GetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.NoDelay);
            set => client.Client.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.NoDelay, value);
        }

        public bool KeepAlive
        {
            get => (bool)client.Client.GetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive);
            set => client.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, value);
        }

        public void Close() => OtpTransport.Close(client);

        public override string ToString() => client.Client.RemoteEndPoint.ToString();

        #region IDisposable
        private bool disposedValue;

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                    Close();
                disposedValue = true;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        #endregion
    }
}
