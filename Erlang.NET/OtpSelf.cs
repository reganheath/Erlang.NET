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
using System.IO;
using System.Net.Sockets;

namespace Erlang.NET
{
    /**
     * Represents an OTP node. It is used to connect to remote nodes or accept
     * incoming connections from remote nodes.
     * 
     * When the C# node will be connecting to a remote Erlang, C# or C node, it
     * must first identify itself as a node by creating an instance of this class,
     * after which it may connect to the remote node.
     * 
     * When you create an instance of this class, it will bind a socket to a port so
     * that incoming connections can be accepted. However the port number will not
     * be made available to other nodes wishing to connect until you explicitely
     * register with the port mapper daemon by calling {@link #publishPort()}.
     */
    public class OtpSelf : OtpLocalNode
    {
        private IOtpServerTransport serverSocket;

        /**
         * Get the Erlang PID that will be used as the sender id in all "anonymous"
         * messages sent by this node. Anonymous messages are those sent via send
         * methods in {@link OtpConnection OtpConnection} that do not specify a
         * sender.
         */
        private OtpErlangPid pid;
        public OtpErlangPid Pid
        {
            get
            {
                if (pid is null)
                    pid = CreatePid();
                return pid;
            }
        }

        /**
         * Open a connection to a remote node.
         */
        public OtpConnection Connect(OtpPeer other) => new OtpConnection(this, other);

        /**
         * Start listening for incoming connections
         */
        public void Listen()
        {
            serverSocket = CreateServerTransport(Port);
            Port = serverSocket.LocalPort;
        }

        /**
         * Accept an incoming connection from a remote node. A call to this method
         * will block until an incoming connection is at least attempted.
         */
        public OtpConnection Accept()
        {
            IOtpTransport newsock = null;

            while (true)
            {
                try
                {
                    newsock = serverSocket.Accept();
                    return new OtpConnection(this, newsock);
                }
                catch (SocketException e)
                {
                    newsock?.Dispose();
                    throw new IOException("Failed to accept connection", e);
                }
            }
        }
    }
}
