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
     * Maintains a connection between a C# process and a remote Erlang, C# or C
     * node. The object maintains connection state and allows data to be sent to and
     * received from the peer.
     * 
     * Once a connection is established between the local node and a remote node,
     * the connection object can be used to send and receive messages between the
     * nodes and make rpc calls (assuming that the remote node is a real Erlang
     * node).
     * 
     * The various receive methods are all blocking and will return only when a
     * valid message has been received or an exception is raised.
     * 
     * If an exception occurs in any of the methods in this class, the connection
     * will be closed and must be explicitely reopened in order to resume
     * communication with the peer.
     * 
     * It is not possible to create an instance of this class directly.
     * OtpConnection objects are returned by {@link OtpSelf#connect(OtpPeer)
     * OtpSelf.connect()} and {@link OtpSelf#accept() OtpSelf.accept()}.
     */
    public class OtpConnection : AbstractConnection
    {
        protected readonly GenericQueue<object> queue = new GenericQueue<object>(); // messages get delivered here

        /**
         * Get information about the node at the local end of this connection.
         */
        public OtpSelf Self { get; protected set; }

        /**
         * Return the number of messages currently waiting in the receive queue for
         * this connection.
         */
        public int MsgCount() => queue.Count;

        /*
         * Accept an incoming connection from a remote node. Used by {@link
         * OtpSelf#accept() OtpSelf.accept()} to create a connection based on data
         * received when handshaking with the peer node, when the remote node is the
         * connection intitiator.
         */
        internal OtpConnection(OtpSelf self, IOtpTransport socket)
            : base(self, socket)
        {
            Self = self;
            Start();
        }

        /*
         * Intiate and open a connection to a remote node.
         */
        internal OtpConnection(OtpSelf self, OtpPeer peer)
            : base(self, peer)
        {
            Self = self;
            Start();
        }

        public override void Deliver(Exception e) => queue.Enqueue(e);


        public override void Deliver(OtpMsg msg) => queue.Enqueue(msg);

        /**
         * Receive a message from a remote process. This method blocks until a valid
         * message is received or an exception is raised.
         * 
         * If the remote node sends a message that cannot be decoded properly, the
         * connection is closed and the method throws an exception.
         */
        public IOtpErlangObject Receive()
        {
            try
            {
                return ReceiveMsg().Payload;
            }
            catch (OtpDecodeException e)
            {
                Close();
                throw new IOException("Receive failed", e);
            }
        }

        /**
         * Receive a message from a remote process. This method blocks at most for
         * the specified time, until a valid message is received or an exception is
         * raised.
         * 
         * If the remote node sends a message that cannot be decoded properly, the
         * connection is closed and the method throws an exception.
         */
        public IOtpErlangObject Receive(long timeout)
        {
            try
            {
                return ReceiveMsg(timeout).Payload;
            }
            catch (OtpDecodeException e)
            {
                Close();
                throw new IOException("Receive failed", e);
            }
        }

        /**
         * Receive a raw (still encoded) message from a remote process. This message
         * blocks until a valid message is received or an exception is raised.
         * 
         * If the remote node sends a message that cannot be decoded properly, the
         * connection is closed and the method throws an exception.
         */
        public OtpInputStream ReceiveBuf() => ReceiveMsg().Stream;

        /**
         * Receive a raw (still encoded) message from a remote process. This message
         * blocks at most for the specified time until a valid message is received
         * or an exception is raised.
         * 
         * If the remote node sends a message that cannot be decoded properly, the
         * connection is closed and the method throws an exception.
         */
        public OtpInputStream ReceiveBuf(long timeout) => ReceiveMsg(timeout).Stream;

        /**
         * Receive a messge complete with sender and recipient information.
         */
        public OtpMsg ReceiveMsg()
        {
            if (!queue.Dequeue(out object o))
                return null;
            switch (o)
            {
                case OtpMsg m: return m;
                case IOException ioex: throw ioex;
                case OtpExit exit: throw exit;
                case OtpAuthException ex: throw ex;
                default: return null;
            }
        }

        /**
         * Receive a messge complete with sender and recipient information. This
         * method blocks at most for the specified time.
         */
        public OtpMsg ReceiveMsg(long timeout)
        {
            if (!queue.Dequeue(timeout, out object o))
                return null;
            switch (o)
            {
                case OtpMsg m: return m;
                case IOException ioex: throw ioex;
                case OtpExit exit: throw exit;
                case OtpAuthException ex: throw ex;
                default: return null;
            }
        }

        /**
         * Send a message to a process on a remote node.
         */
        public void Send(OtpErlangPid dest, IOtpErlangObject msg) => SendBuf(Self.Pid, dest, new OtpOutputStream(msg));

        /**
         * Send a message to a named process on a remote node.
         */
        public void Send(string dest, IOtpErlangObject msg) => SendBuf(Self.Pid, dest, new OtpOutputStream(msg));

        /**
         * Send a pre-encoded message to a named process on a remote node.
         */
        public void SendBuf(string dest, OtpOutputStream payload) => SendBuf(Self.Pid, dest, payload);

        /**
         * Send a pre-encoded message to a process on a remote node.
         */
        public void SendBuf(OtpErlangPid dest, OtpOutputStream payload) => SendBuf(Self.Pid, dest, payload);

        /**
         * Create a link between the local node and the specified process on the
         * remote node. If the link is still active when the remote process
         * terminates, an exit signal will be sent to this connection. Use
         * {@link #unlink unlink()} to remove the link.
         */
        public void Link(OtpErlangPid dest) => SendLink(Self.Pid, dest);

        /**
         * Remove a link between the local node and the specified process on the
         * remote node. This method deactivates links created with
         * {@link #link link()}.
         */
        public void Unlink(OtpErlangPid dest) => SendUnlink(Self.Pid, dest);

        /**
         * Send an exit signal to a remote process.
         */
        public void Exit(OtpErlangPid dest, IOtpErlangObject reason) => SendExit2(Self.Pid, dest, reason);
    }
}
