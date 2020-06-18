/*
 * %CopyrightBegin%
 * 
 * Copyright Takayuki Usui 2009. All Rights Reserved.
 * Copyright Ericsson AB 2000-2009. All Rights Reserved.
 * 
 * The contents of this file are subject to the Erlang Public License,
 * Version 1.1, (the "License"); you may not use this file except in
 * compliance with the License. You should have received a copy of the
 * Erlang Public License along with this software. If not, it can be
 * retrieved online at http://www.erlang.org/.
 * 
 * Software distributed under the License is distributed on an "AS IS"
 * basis, WITHOUT WARRANTY OF ANY KIND, either express or implied. See
 * the License for the specific language governing rights and limitations
 * under the License.
 * 
 * %CopyrightEnd%
 */
using System;
using System.IO;
using System.Net.Sockets;

namespace Erlang.NET
{
    /**
     * This class represents local node types. It is used to group the node types
     * {@link OtpNode OtpNode} and {@link OtpSelf OtpSelf}.
     */
    public class OtpLocalNode : AbstractNode
    {
        private readonly object lockObj = new object();
        private int serial = 0;
        private int pidCount = 1;
        private int portCount = 1;
        private int[] refId;

        protected int port;
        protected OtpTransport epmd;

        /**
         * Create a node with the given name and the default cookie.
         */
        protected OtpLocalNode(string node)
            : base(node)
        {
            init();
        }

        /**
         * Create a node with the given name, transport factory and the default cookie.
         */
        protected OtpLocalNode(string node, OtpTransportFactory transportFactory)
            : base(node, transportFactory)
        {
            init();
        }

        /**
         * Create a node with the given name and cookie.
         */
        protected OtpLocalNode(string node, string cookie)
            : base(node, cookie)
        {
            init();
        }

        /**
         * Create a node with the given name, cookie and transport factory.
         */
        protected OtpLocalNode(string node, string cookie, OtpTransportFactory transportFactory)
            : base(node, cookie, transportFactory)
        {
            init();
        }

        private void init()
        {
            serial = 0;
            pidCount = 1;
            portCount = 1;
            refId = new int[3];
            refId[0] = 1;
            refId[1] = 0;
            refId[2] = 0;
        }

        /**
         * Get the port number used by this node.
         * 
         * @return the port number this server node is accepting connections on.
         */
        public int Port
        {
            get { return port; }
        }

        /**
         * Set the Epmd socket after publishing this nodes listen port to Epmd.
         * 
         * @param s
         *                The socket connecting this node to Epmd.
         */
        public void setEpmd(OtpTransport s)
        {
            epmd = s;
        }

        /**
         * Get the Epmd socket.
         * 
         * @return The socket connecting this node to Epmd.
         */
        public OtpTransport getEpmd()
        {
            return epmd;
        }

        /**
         * Close the Epmd socket.
         * 
         * @param s
         *                The socket connecting this node to Epmd.
         */
        public void closeEpmd()
        {
            if (epmd != null)
            {
                try
                {
                    epmd.close();
                    epmd = null;
                }
                catch (Exception e)
                {
                    epmd = null;
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
        public OtpErlangPid createPid()
        {
            lock (lockObj)
            {
                OtpErlangPid p = new OtpErlangPid(base.Node, pidCount, serial, base.Creation);

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
        public OtpErlangPort createPort()
        {
            lock (lockObj)
            {
                OtpErlangPort p = new OtpErlangPort(base.Node, portCount, base.Creation);

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
        public OtpErlangRef createRef()
        {
            lock (lockObj)
            {
                OtpErlangRef r = new OtpErlangRef(base.Node, refId, base.Creation);

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
