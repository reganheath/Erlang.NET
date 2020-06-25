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

namespace Erlang.NET
{
    /**
     * This class represents local node types. It is used to group the node types
     * {@link OtpNode OtpNode} and {@link OtpSelf OtpSelf}.
     */
    public class OtpLocalNode : AbstractNode
    {
        private readonly object lockObj = new object();
        private readonly int[] refId = new int[3] { 1, 0, 0 };
        private int serial = 0;
        private int pidCount = 1;
        private int portCount = 1;

        public OtpInputStream.StreamFlags Flags { get; set; } = 0;


        /**
         * Create a node with the given name and the default cookie.
         */
        protected OtpLocalNode(string node)
            : base(node)
        {
        }

        /**
         * Create a node with the given name, transport factory and the default cookie.
         */
        protected OtpLocalNode(string node, OtpTransportFactory transportFactory)
            : base(node, transportFactory)
        {
        }

        /**
         * Create a node with the given name and cookie.
         */
        protected OtpLocalNode(string node, string cookie)
            : base(node, cookie)
        {
        }

        /**
         * Create a node with the given name, cookie and transport factory.
         */
        protected OtpLocalNode(string node, string cookie, OtpTransportFactory transportFactory)
            : base(node, cookie, transportFactory)
        {
        }

        public IOtpTransport Epmd { get; set; }

        /**
         * Close the Epmd socket.
         * 
         * @param s
         *                The socket connecting this node to Epmd.
         */
        public void CloseEpmd()
        {
            if (Epmd != null)
            {
                try
                {
                    Epmd.Close();
                    Epmd = null;
                }
                catch (Exception e)
                {
                    Epmd = null;
                    throw new IOException(e.Message);
                }
            }
        }

        /**
         * Create an Erlang {@link OtpErlangPid pid}. Erlang pids are based upon
         * some node specific information; this method creates a pid using the
         * information in this node. Each call to this method produces a unique pid.
         * 
         * @return an Erlang pid.
         */
        public OtpErlangPid CreatePid()
        {
            lock (lockObj)
            {
                OtpErlangPid p = new OtpErlangPid(OtpExternal.newPidTag, base.Node, pidCount, serial, base.Creation);

                pidCount++;
                if (pidCount > 0x7fff)
                {
                    pidCount = 0;

                    serial++;
                    if (serial > 0x1fff) /* 13 bits */
                        serial = 0;
                }

                return p;
            }
        }

        /**
         * Create an Erlang {@link OtpErlangPort port}. Erlang ports are based upon
         * some node specific information; this method creates a port using the
         * information in this node. Each call to this method produces a unique
         * port. It may not be meaningful to create a port in a non-Erlang
         * environment, but this method is provided for completeness.
         * 
         * @return an Erlang port.
         */
        public OtpErlangPort CreatePort()
        {
            lock (lockObj)
            {
                OtpErlangPort p = new OtpErlangPort(OtpExternal.newPortTag, Node, portCount, Creation);

                portCount++;
                if (portCount > 0xfffffff) /* 28 bits */
                    portCount = 0;

                return p;
            }
        }

        /**
         * Create an Erlang {@link OtpErlangRef reference}. Erlang references are
         * based upon some node specific information; this method creates a
         * reference using the information in this node. Each call to this method
         * produces a unique reference.
         * 
         * @return an Erlang reference.
         */
        public OtpErlangRef CreateRef()
        {
            lock (lockObj)
            {
                OtpErlangRef r = new OtpErlangRef(OtpExternal.newerRefTag, Node, refId, Creation);

                // increment ref ids (3 ints: 18 + 32 + 32 bits)
                refId[0]++;
                if (refId[0] > 0x3ffff)
                {
                    refId[0] = 0;

                    refId[1]++;
                    if (refId[1] == 0)
                        refId[2]++;
                }

                return r;
            }
        }
    }
}
