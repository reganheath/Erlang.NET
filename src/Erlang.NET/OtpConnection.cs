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
     * Maintains a connection between a Java process and a remote Erlang, Java or C
     * node. The object maintains connection state and allows data to be sent to and
     * received from the peer.
     * 
     * <p>
     * Once a connection is established between the local node and a remote node,
     * the connection object can be used to send and receive messages between the
     * nodes and make rpc calls (assuming that the remote node is a real Erlang
     * node).
     * 
     * <p>
     * The various receive methods are all blocking and will return only when a
     * valid message has been received or an exception is raised.
     * 
     * <p>
     * If an exception occurs in any of the methods in this class, the connection
     * will be closed and must be explicitely reopened in order to resume
     * communication with the peer.
     * 
     * <p>
     * It is not possible to create an instance of this class directly.
     * OtpConnection objects are returned by {@link OtpSelf#connect(OtpPeer)
     * OtpSelf.connect()} and {@link OtpSelf#accept() OtpSelf.accept()}.
     */
    public class OtpConnection : AbstractConnection
    {
        protected readonly GenericQueue queue; // messages get delivered here

        /*
         * Accept an incoming connection from a remote node. Used by {@link
         * OtpSelf#accept() OtpSelf.accept()} to create a connection based on data
         * received when handshaking with the peer node, when the remote node is the
         * connection intitiator.
         * 
         * @exception java.io.IOException if it was not possible to connect to the
         * peer.
         * 
         * @exception OtpAuthException if handshake resulted in an authentication
         * error
         */
        // package scope
        internal OtpConnection(OtpSelf self, OtpTransport s)
            : base(self, s)
        {
            Self = self;
            queue = new GenericQueue();
            Start();
        }

        /*
         * Intiate and open a connection to a remote node.
         * 
         * @exception java.io.IOException if it was not possible to connect to the
         * peer.
         * 
         * @exception OtpAuthException if handshake resulted in an authentication
         * error.
         */
        // package scope
        internal OtpConnection(OtpSelf self, OtpPeer other)
            : base(self, other)
        {
            Self = self;
            queue = new GenericQueue();
            Start();
        }

        public override void Deliver(Exception e)
        {
            queue.Put(e);
        }

        public override void Deliver(OtpMsg msg)
        {
            queue.Put(msg);
        }

        /**
         * Get information about the node at the peer end of this connection.
         * 
         * @return the {@link OtpPeer Node} representing the peer node.
         */
        public OtpPeer Peer { get; protected set; }

        /**
         * Get information about the node at the local end of this connection.
         * 
         * @return the {@link OtpSelf Node} representing the local node.
         */
        public OtpSelf Self { get; protected set; }

        /**
         * Return the number of messages currently waiting in the receive queue for
         * this connection.
         */
        public int MsgCount() => queue.GetCount();

        /**
         * Receive a message from a remote process. This method blocks until a valid
         * message is received or an exception is raised.
         * 
         * <p>
         * If the remote node sends a message that cannot be decoded properly, the
         * connection is closed and the method throws an exception.
         * 
         * @return an object containing a single Erlang term.
         * 
         * @exception java.io.IOException
         *                    if the connection is not active or a communication
         *                    error occurs.
         * 
         * @exception OtpErlangExit
         *                    if an exit signal is received from a process on the
         *                    peer node.
         * 
         * @exception OtpAuthException
         *                    if the remote node sends a message containing an
         *                    invalid cookie.
         */
        public OtpErlangObject Receive()
        {
            try
            {
                return ReceiveMsg().getMsg();
            }
            catch (OtpErlangDecodeException e)
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
         * <p>
         * If the remote node sends a message that cannot be decoded properly, the
         * connection is closed and the method throws an exception.
         * 
         * @param timeout
         *                the time in milliseconds that this operation will block.
         *                Specify 0 to poll the queue.
         * 
         * @return an object containing a single Erlang term.
         * 
         * @exception java.io.IOException
         *                    if the connection is not active or a communication
         *                    error occurs.
         * 
         * @exception OtpErlangExit
         *                    if an exit signal is received from a process on the
         *                    peer node.
         * 
         * @exception OtpAuthException
         *                    if the remote node sends a message containing an
         *                    invalid cookie.
         * 
         * @exception InterruptedException
         *                    if no message if the method times out before a message
         *                    becomes available.
         */
        public OtpErlangObject Receive(long timeout)
        {
            try
            {
                return ReceiveMsg(timeout).getMsg();
            }
            catch (OtpErlangDecodeException e)
            {
                Close();
                throw new IOException("Receive failed", e);
            }
        }

        /**
         * Receive a raw (still encoded) message from a remote process. This message
         * blocks until a valid message is received or an exception is raised.
         * 
         * <p>
         * If the remote node sends a message that cannot be decoded properly, the
         * connection is closed and the method throws an exception.
         * 
         * @return an object containing a raw (still encoded) Erlang term.
         * 
         * @exception java.io.IOException
         *                    if the connection is not active or a communication
         *                    error occurs.
         * 
         * @exception OtpErlangExit
         *                    if an exit signal is received from a process on the
         *                    peer node, or if the connection is lost for any
         *                    reason.
         * 
         * @exception OtpAuthException
         *                    if the remote node sends a message containing an
         *                    invalid cookie.
         */
        public OtpInputStream ReceiveBuf() => ReceiveMsg().getMsgBuf();

        /**
         * Receive a raw (still encoded) message from a remote process. This message
         * blocks at most for the specified time until a valid message is received
         * or an exception is raised.
         * 
         * <p>
         * If the remote node sends a message that cannot be decoded properly, the
         * connection is closed and the method throws an exception.
         * 
         * @param timeout
         *                the time in milliseconds that this operation will block.
         *                Specify 0 to poll the queue.
         * 
         * @return an object containing a raw (still encoded) Erlang term.
         * 
         * @exception java.io.IOException
         *                    if the connection is not active or a communication
         *                    error occurs.
         * 
         * @exception OtpErlangExit
         *                    if an exit signal is received from a process on the
         *                    peer node, or if the connection is lost for any
         *                    reason.
         * 
         * @exception OtpAuthException
         *                    if the remote node sends a message containing an
         *                    invalid cookie.
         * 
         * @exception InterruptedException
         *                    if no message if the method times out before a message
         *                    becomes available.
         */
        public OtpInputStream ReceiveBuf(long timeout) => ReceiveMsg(timeout).getMsgBuf();

        /**
         * Receive a messge complete with sender and recipient information.
         * 
         * @return an {@link OtpMsg OtpMsg} containing the header information about
         *         the sender and recipient, as well as the actual message contents.
         * 
         * @exception java.io.IOException
         *                    if the connection is not active or a communication
         *                    error occurs.
         * 
         * @exception OtpErlangExit
         *                    if an exit signal is received from a process on the
         *                    peer node, or if the connection is lost for any
         *                    reason.
         * 
         * @exception OtpAuthException
         *                    if the remote node sends a message containing an
         *                    invalid cookie.
         */
        public OtpMsg ReceiveMsg()
        {
            object o = queue.Get();
            if (o is OtpMsg msg)
                return msg;
            if (o is IOException ioex)
                throw ioex;
            if (o is OtpErlangExit exit)
                throw exit;
            if (o is OtpAuthException ex)
                throw ex;
            return null;
        }

        /**
         * Receive a messge complete with sender and recipient information. This
         * method blocks at most for the specified time.
         * 
         * @param timeout
         *                the time in milliseconds that this operation will block.
         *                Specify 0 to poll the queue.
         * 
         * @return an {@link OtpMsg OtpMsg} containing the header information about
         *         the sender and recipient, as well as the actual message contents.
         * 
         * @exception java.io.IOException
         *                    if the connection is not active or a communication
         *                    error occurs.
         * 
         * @exception OtpErlangExit
         *                    if an exit signal is received from a process on the
         *                    peer node, or if the connection is lost for any
         *                    reason.
         * 
         * @exception OtpAuthException
         *                    if the remote node sends a message containing an
         *                    invalid cookie.
         * 
         * @exception InterruptedException
         *                    if no message if the method times out before a message
         *                    becomes available.
         */
        public OtpMsg ReceiveMsg(long timeout)
        {
            object o = queue.Get(timeout);
            if (o is OtpMsg msg)
                return msg;
            if (o is IOException ioex)
                throw ioex;
            if (o is OtpErlangExit exit)
                throw exit;
            if (o is OtpAuthException ex)
                throw ex;
            return null;
        }

        /**
         * Send a message to a process on a remote node.
         * 
         * @param dest
         *                the Erlang PID of the remote process.
         * @param msg
         *                the message to send.
         * 
         * @exception java.io.IOException
         *                    if the connection is not active or a communication
         *                    error occurs.
         */
        public void Send(OtpErlangPid dest, OtpErlangObject msg) => SendBuf(Self.Pid, dest, new OtpOutputStream(msg));

        /**
         * Send a message to a named process on a remote node.
         * 
         * @param dest
         *                the name of the remote process.
         * @param msg
         *                the message to send.
         * 
         * @exception java.io.IOException
         *                    if the connection is not active or a communication
         *                    error occurs.
         */
        public void Send(string dest, OtpErlangObject msg) => SendBuf(Self.Pid, dest, new OtpOutputStream(msg));

        /**
         * Send a pre-encoded message to a named process on a remote node.
         * 
         * @param dest
         *                the name of the remote process.
         * @param payload
         *                the encoded message to send.
         * 
         * @exception java.io.IOException
         *                    if the connection is not active or a communication
         *                    error occurs.
         */
        public void sendBuf(string dest, OtpOutputStream payload) => SendBuf(Self.Pid, dest, payload);

        /**
         * Send a pre-encoded message to a process on a remote node.
         * 
         * @param dest
         *                the Erlang PID of the remote process.
         * @param msg
         *                the encoded message to send.
         * 
         * @exception java.io.IOException
         *                    if the connection is not active or a communication
         *                    error occurs.
         */
        public void SendBuf(OtpErlangPid dest, OtpOutputStream payload) => SendBuf(Self.Pid, dest, payload);

        /**
         * Send an RPC request to the remote Erlang node. This convenience function
         * creates the following message and sends it to 'rex' on the remote node:
         * 
         * <pre>
         * { self, { call, Mod, Fun, Args, user } }
         * </pre>
         * 
         * <p>
         * Note that this method has unpredicatble results if the remote node is not
         * an Erlang node.
         * </p>
         * 
         * @param mod
         *                the name of the Erlang module containing the function to
         *                be called.
         * @param fun
         *                the name of the function to call.
         * @param args
         *                an array of Erlang terms, to be used as arguments to the
         *                function.
         * 
         * @exception java.io.IOException
         *                    if the connection is not active or a communication
         *                    error occurs.
         */
        public void SendRPC(string mod, string fun, OtpErlangObject[] args) => SendRPC(mod, fun, new OtpErlangList(args));

        /**
         * Send an RPC request to the remote Erlang node. This convenience function
         * creates the following message and sends it to 'rex' on the remote node:
         * 
         * <pre>
         * { self, { call, Mod, Fun, Args, user } }
         * </pre>
         * 
         * <p>
         * Note that this method has unpredicatble results if the remote node is not
         * an Erlang node.
         * </p>
         * 
         * @param mod
         *                the name of the Erlang module containing the function to
         *                be called.
         * @param fun
         *                the name of the function to call.
         * @param args
         *                a list of Erlang terms, to be used as arguments to the
         *                function.
         * 
         * @exception java.io.IOException
         *                    if the connection is not active or a communication
         *                    error occurs.
         */
        public void SendRPC(string mod, string fun, OtpErlangList args)
        {
            OtpErlangObject[] rpc = new OtpErlangObject[2];
            OtpErlangObject[] call = new OtpErlangObject[5];

            /* {self, { call, Mod, Fun, Args, user}} */

            call[0] = new OtpErlangAtom("call");
            call[1] = new OtpErlangAtom(mod);
            call[2] = new OtpErlangAtom(fun);
            call[3] = args;
            call[4] = new OtpErlangAtom("user");

            rpc[0] = Self.Pid;
            rpc[1] = new OtpErlangTuple(call);

            Send("rex", new OtpErlangTuple(rpc));
        }

        /**
         * Receive an RPC reply from the remote Erlang node. This convenience
         * function receives a message from the remote node, and expects it to have
         * the following format:
         * 
         * <pre>
         * { rex, Term }
         * </pre>
         * 
         * @return the second element of the tuple if the received message is a
         *         two-tuple, otherwise null. No further error checking is
         *         performed.
         * 
         * @exception java.io.IOException
         *                    if the connection is not active or a communication
         *                    error occurs.
         * 
         * @exception OtpErlangExit
         *                    if an exit signal is received from a process on the
         *                    peer node.
         * 
         * @exception OtpAuthException
         *                    if the remote node sends a message containing an
         *                    invalid cookie.
         */
        public OtpErlangObject ReceiveRPC()
        {
            OtpErlangObject msg = Receive();

            if (msg is OtpErlangTuple)
            {
                OtpErlangTuple t = (OtpErlangTuple)msg;
                if (t.Arity == 2)
                    return t.elementAt(1); // obs: second element
            }

            return null;
        }

        /**
         * Create a link between the local node and the specified process on the
         * remote node. If the link is still active when the remote process
         * terminates, an exit signal will be sent to this connection. Use
         * {@link #unlink unlink()} to remove the link.
         * 
         * @param dest
         *                the Erlang PID of the remote process.
         * 
         * @exception java.io.IOException
         *                    if the connection is not active or a communication
         *                    error occurs.
         */
        public void Link(OtpErlangPid dest) => SendLink(Self.Pid, dest);

        /**
         * Remove a link between the local node and the specified process on the
         * remote node. This method deactivates links created with
         * {@link #link link()}.
         * 
         * @param dest
         *                the Erlang PID of the remote process.
         * 
         * @exception java.io.IOException
         *                    if the connection is not active or a communication
         *                    error occurs.
         */
        public void Unlink(OtpErlangPid dest) => SendUnlink(Self.Pid, dest);

        /**
         * Send an exit signal to a remote process.
         * 
         * @param dest
         *                the Erlang PID of the remote process.
         * @param reason
         *                an Erlang term describing the exit reason.
         * 
         * @exception java.io.IOException
         *                    if the connection is not active or a communication
         *                    error occurs.
         */
        public void Exit(OtpErlangPid dest, OtpErlangObject reason) => SendExit2(Self.Pid, dest, reason);
    }
}
