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

namespace Erlang.NET
{
    public class OtpSocketTransportFactory : OtpTransportFactory
    {
        public IOtpTransport CreateTransport(string addr, int port) => new OtpSocketTransport(addr, port);

        public IOtpTransport CreateTransport(IPEndPoint addr) => new OtpSocketTransport(addr);

        public IOtpServerTransport CreateServerTransport(int port) => new OtpServerSocketTransport(port);
    }
}
