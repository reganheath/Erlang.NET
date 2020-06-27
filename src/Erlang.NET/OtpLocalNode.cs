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

namespace Erlang.NET
{
    /**
     * This class represents local node types. It is used to group the node types
     * {@link OtpNode OtpNode} and {@link OtpSelf OtpSelf}.
     */
    public class OtpLocalNode : AbstractNode, IDisposable
    {
        private readonly object lockObj = new object();
        private readonly int[] refId = new int[3] { 1, 0, 0 };
        private int serial = 0;
        private int pidCount = 1;
        private int portCount = 1;

        public IOtpTransport Epmd { get; protected set; }
        private bool disposedValue;

        /**
         * Make public the information needed by remote nodes that may wish to
         * connect to this one. This method establishes a connection to the Erlang
         * port mapper (Epmd) and registers the server node's name and port so that
         * remote nodes are able to connect.
         * 
         * This method will fail if an Epmd process is not running on the localhost.
         * See the Erlang documentation for information about starting Epmd.
         * 
         * Note that once this method has been called, the node is expected to be
         * available to accept incoming connections. For that reason you should make
         * sure that you call {@link #accept()} shortly after calling
         * {@link #publishPort()}. When you no longer intend to accept connections
         * you should call {@link #unPublishPort()}.
         */
        public bool PublishPort()
        {
            if (Epmd is null)
                Epmd = OtpEpmd.PublishPort(this);
            return (Epmd != null);
        }

        /**
         * Unregister the server node's name and port number from the Erlang port
         * mapper, thus preventing any new connections from remote nodes.
         */
        public void UnPublishPort()
        {
            if (Epmd == null)
                return;

            // Unregister
            OtpEpmd.UnPublishPort(this);

            // Close and ignore errors
            try { Epmd.Close(); }
            catch (Exception) { }
            Epmd = null;
        }

        /**
         * Create an Erlang {@link OtpErlangPid pid}. Erlang pids are based upon
         * some node specific information; this method creates a pid using the
         * information in this node. Each call to this method produces a unique pid.
         */
        public OtpErlangPid CreatePid()
        {
            lock (lockObj)
            {
                OtpErlangPid newPid = new OtpErlangPid(OtpExternal.newPidTag, Node, pidCount, serial, Creation);

                pidCount++;
                if (pidCount > 0x7fff)
                {
                    pidCount = 0;

                    serial++;
                    if (serial > 0x1fff) /* 13 bits */
                        serial = 0;
                }

                return newPid;
            }
        }

        /**
         * Create an Erlang {@link OtpErlangPort port}. Erlang ports are based upon
         * some node specific information; this method creates a port using the
         * information in this node. Each call to this method produces a unique
         * port. It may not be meaningful to create a port in a non-Erlang
         * environment, but this method is provided for completeness.
         */
        public OtpErlangPort CreatePort()
        {
            lock (lockObj)
            {
                OtpErlangPort newPort = new OtpErlangPort(OtpExternal.newPortTag, Node, portCount, Creation);

                portCount++;
                if (portCount > 0xfffffff) /* 28 bits */
                    portCount = 0;

                return newPort;
            }
        }

        /**
         * Create an Erlang {@link OtpErlangRef reference}. Erlang references are
         * based upon some node specific information; this method creates a
         * reference using the information in this node. Each call to this method
         * produces a unique reference.
         */
        public OtpErlangRef CreateRef()
        {
            lock (lockObj)
            {
                OtpErlangRef newRef = new OtpErlangRef(OtpExternal.newerRefTag, Node, refId, Creation);

                // increment ref ids (3 ints: 18 + 32 + 32 bits)
                refId[0]++;
                if (refId[0] > 0x3ffff)
                {
                    refId[0] = 0;

                    refId[1]++;
                    if (refId[1] == 0)
                        refId[2]++;
                }

                return newRef;
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                    UnPublishPort();
                disposedValue = true;
            }
        }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
