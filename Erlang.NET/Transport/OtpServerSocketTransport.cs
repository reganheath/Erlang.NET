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
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace Erlang.NET
{
    public class OtpServerSocketTransport : IOtpServerTransport
    {
        private readonly TcpListener socket;

        public OtpServerSocketTransport(int port, bool start = true)
        {
            socket = new TcpListener(IPAddress.Any, port);
            if (start)
                socket.Start();
        }

        public void Start() => socket.Start();

        public int LocalPort => ((IPEndPoint)socket.LocalEndpoint).Port;

        public IOtpTransport Accept()
        {
            TcpClient sock = socket.AcceptTcpClient();
            sock.NoDelay = true;
            return new OtpSocketTransport(sock);
        }

        public async Task<IOtpTransport> AcceptAsync()
        {
            TcpClient sock = await socket.AcceptTcpClientAsync();
            sock.NoDelay = true;
            return new OtpSocketTransport(sock);
        }

        public void Close() => OtpTransport.Close(socket);

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
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            System.GC.SuppressFinalize(this);
        }
        #endregion
    }
}
