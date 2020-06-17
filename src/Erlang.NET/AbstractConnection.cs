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
using System.Configuration;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading;
using log4net;
using log4net.Config;
using System.Security.Cryptography;

namespace Erlang.NET
{
    /**
     * Maintains a connection between a Java process and a remote Erlang, Java or C
     * node. The object maintains connection state and allows data to be sent to and
     * received from the peer.
     * 
     * <p>
     * This abstract class provides the necessary methods to maintain the actual
     * connection and encode the messages and headers in the proper format according
     * to the Erlang distribution protocol. Subclasses can use these methods to
     * provide a more or less transparent communication channel as desired.
     * </p>
     * 
     * <p>
     * Note that no receive methods are provided. Subclasses must provide methods
     * for message delivery, and may implement their own receive methods.
     * <p>
     * 
     * <p>
     * If an exception occurs in any of the methods in this class, the connection
     * will be closed and must be reopened in order to resume communication with the
     * peer. This will be indicated to the subclass by passing the exception to its
     * delivery() method.
     * </p>
     * 
     * <p>
     * The System property OtpConnection.trace can be used to change the initial
     * trace level setting for all connections. Normally the initial trace level is
     * 0 and connections are not traced unless {@link #setTraceLevel
     * setTraceLevel()} is used to change the setting for a particular connection.
     * OtpConnection.trace can be used to turn on tracing by default for all
     * connections.
     * </p>
     */
    public abstract class AbstractConnection : ThreadBase
    {
        private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        protected const int headerLen = 2048; // more than enough

        protected const byte passThrough = (byte)0x70;
        protected const byte version = (byte)0x83;

        // Erlang message header tags
        protected const int linkTag = 1;
        protected const int sendTag = 2;
        protected const int exitTag = 3;
        protected const int unlinkTag = 4;
        protected const int regSendTag = 6;
        protected const int groupLeaderTag = 7;
        protected const int exit2Tag = 8;

        protected const int sendTTTag = 12;
        protected const int exitTTTag = 13;
        protected const int regSendTTTag = 16;
        protected const int exit2TTTag = 18;

        // MD5 challenge messsage tags
        protected const int ChallengeReply = 'r';
        protected const int ChallengeAck = 'a';
        protected const int ChallengeStatus = 's';

        private volatile bool done = false;
        private object runLock = new object();

        protected bool connected = false; // connection status
        protected OtpTransport socket; // communication channel
        protected OtpPeer peer; // who are we connected to
        protected OtpLocalNode localNode; // this nodes id
        string name; // local name of this connection

        public string Name
        {
            get { return name; }
            set { name = value; }
        }

        protected bool cookieOk = false; // already checked the cookie for this connection
        protected bool sendCookie = true; // Send cookies in messages?

        // tracelevel constants
        protected int traceLevel = 0;

        protected static int defaultLevel = 0;
        protected static int sendThreshold = 1;
        protected static int ctrlThreshold = 2;
        protected static int handshakeThreshold = 3;

        protected static readonly Random random;

        private int connFlags = 0;

        static AbstractConnection()
        {
            XmlConfigurator.Configure();

            // trace this connection?
            string trace = ConfigurationManager.AppSettings["OtpConnection.trace"];
            try
            {
                if (trace != null)
                    defaultLevel = Int32.Parse(trace);
            }
            catch (FormatException)
            {
                defaultLevel = 0;
            }

            random = new Random();
        }

        /**
         * Accept an incoming connection from a remote node. Used by {@link
         * OtpSelf#accept() OtpSelf.accept()} to create a connection based on data
         * received when handshaking with the peer node, when the remote node is the
         * connection initiator.
         * 
         * @exception java.io.IOException if it was not possible to connect to the
         * peer.
         * 
         * @exception OtpAuthException if handshake resulted in an authentication
         * error
         */
        protected AbstractConnection(OtpLocalNode self, OtpTransport s)
            : base("receive", true)
        {
            this.localNode = self;
            peer = new OtpPeer(self.transportFactory);
            socket = s;

            traceLevel = defaultLevel;

            if (traceLevel >= handshakeThreshold)
                log.Debug("<- ACCEPT FROM " + s);

            doAccept();
            name = peer.Node;
        }

        /**
         * Intiate and open a connection to a remote node.
         * 
         * @exception java.io.IOException if it was not possible to connect to the
         * peer.
         * 
         * @exception OtpAuthException if handshake resulted in an authentication
         * error.
         */
        protected AbstractConnection(OtpLocalNode self, OtpPeer other)
            : base("receive", true)
        {
            peer = other;
            this.localNode = self;
            socket = null;
            int port;

            traceLevel = defaultLevel;

            // now get a connection between the two...
            port = OtpEpmd.lookupPort(peer);
            if (port == 0)
                throw new IOException("No remote node found - cannot connect");

            // now find highest common dist value
            if (peer.Proto != self.Proto || self.DistHigh < peer.DistLow || self.DistLow > peer.DistHigh)
                throw new IOException("No common protocol found - cannot connect");

            // highest common version: min(peer.distHigh, self.distHigh)
            peer.DistChoose = peer.DistHigh > self.DistHigh ? self.DistHigh : peer.DistHigh;

            doConnect(port);

            name = peer.Node;
            connected = true;
        }

        /**
         * Deliver communication exceptions to the recipient.
         */
        public abstract void deliver(Exception e);

        /**
         * Deliver messages to the recipient.
         */
        public abstract void deliver(OtpMsg msg);

        /**
         * Send a pre-encoded message to a named process on a remote node.
         * 
         * @param dest
         *            the name of the remote process.
         * @param payload
         *            the encoded message to send.
         * 
         * @exception java.io.IOException
         *                if the connection is not active or a communication error
         *                occurs.
         */
        protected void sendBuf(OtpErlangPid from, string dest, OtpOutputStream payload)
        {
            if (!connected)
                throw new IOException("Not connected");

            OtpOutputStream header = new OtpOutputStream(headerLen);

            // preamble: 4 byte length + "passthrough" tag + version
            header.write4BE(0); // reserve space for length
            header.write1(passThrough);
            header.write1(version);

            // header info
            header.write_tuple_head(4);
            header.write_long(regSendTag);
            header.write_any(from);
            if (sendCookie)
                header.write_atom(localNode.Cookie);
            else
                header.write_atom("");
            header.write_atom(dest);

            // version for payload
            header.write1(version);

            // fix up length in preamble
            header.poke4BE(0, header.size() + payload.size() - 4);

            do_send(header, payload);
        }

        /**
         * Send a pre-encoded message to a process on a remote node.
         * 
         * @param dest
         *            the Erlang PID of the remote process.
         * @param msg
         *            the encoded message to send.
         * 
         * @exception java.io.IOException
         *                if the connection is not active or a communication error
         *                occurs.
         */
        protected void sendBuf(OtpErlangPid from, OtpErlangPid dest, OtpOutputStream payload)
        {
            if (!connected)
                throw new IOException("Not connected");

            OtpOutputStream header = new OtpOutputStream(headerLen);

            // preamble: 4 byte length + "passthrough" tag + version
            header.write4BE(0); // reserve space for length
            header.write1(passThrough);
            header.write1(version);

            // header info
            header.write_tuple_head(3);
            header.write_long(sendTag);
            if (sendCookie)
                header.write_atom(localNode.Cookie);
            else
                header.write_atom("");
            header.write_any(dest);

            // version for payload
            header.write1(version);

            // fix up length in preamble
            header.poke4BE(0, header.size() + payload.size() - 4);

            do_send(header, payload);
        }

        /*
         * Send an auth error to peer because he sent a bad cookie. The auth error
         * uses his cookie (not revealing ours). This is just like send_reg
         * otherwise
         */
        private void cookieError(OtpLocalNode local, OtpErlangAtom cookie)
        {
            try
            {
                OtpOutputStream header = new OtpOutputStream(headerLen);

                // preamble: 4 byte length + "passthrough" tag + version
                header.write4BE(0); // reserve space for length
                header.write1(passThrough);
                header.write1(version);

                header.write_tuple_head(4);
                header.write_long(regSendTag);
                header.write_any(local.createPid()); // disposable pid
                header.write_atom(cookie.atomValue()); // important: his cookie,
                // not mine...
                header.write_atom("auth");

                // version for payload
                header.write1(version);

                // the payload

                // the no_auth message (copied from Erlang) Don't change this
                // (Erlang will crash)
                // {$gen_cast, {print, "~n** Unauthorized cookie ~w **~n",
                // [foo@aule]}}
                OtpErlangObject[] msg = new OtpErlangObject[2];
                OtpErlangObject[] msgbody = new OtpErlangObject[3];

                msgbody[0] = new OtpErlangAtom("print");
                msgbody[1] = new OtpErlangString("~n** Bad cookie sent to " + local + " **~n");

                // Erlang will crash and burn if there is no third argument here...
                msgbody[2] = new OtpErlangList(); // empty list

                msg[0] = new OtpErlangAtom("$gen_cast");
                msg[1] = new OtpErlangTuple(msgbody);

                OtpOutputStream payload = new OtpOutputStream(new OtpErlangTuple(msg));

                // fix up length in preamble
                header.poke4BE(0, header.size() + payload.size() - 4);

                try { do_send(header, payload); }
                catch (IOException) { } // ignore

            }
            finally
            {
                close();
            }

            throw new OtpAuthException("Remote cookie not authorized: " + cookie.atomValue());
        }

        // link to pid

        /**
         * Create a link between the local node and the specified process on the
         * remote node. If the link is still active when the remote process
         * terminates, an exit signal will be sent to this connection. Use
         * {@link #sendUnlink unlink()} to remove the link.
         * 
         * @param dest
         *            the Erlang PID of the remote process.
         * 
         * @exception java.io.IOException
         *                if the connection is not active or a communication error
         *                occurs.
         */
        protected void sendLink(OtpErlangPid from, OtpErlangPid dest)
        {
            if (!connected)
                throw new IOException("Not connected");

            OtpOutputStream header = new OtpOutputStream(headerLen);

            // preamble: 4 byte length + "passthrough" tag
            header.write4BE(0); // reserve space for length
            header.write1(passThrough);
            header.write1(version);

            // header
            header.write_tuple_head(3);
            header.write_long(linkTag);
            header.write_any(from);
            header.write_any(dest);

            // fix up length in preamble
            header.poke4BE(0, header.size() - 4);

            do_send(header);
        }

        /**
         * Remove a link between the local node and the specified process on the
         * remote node. This method deactivates links created with {@link #sendLink
         * link()}.
         * 
         * @param dest
         *            the Erlang PID of the remote process.
         * 
         * @exception java.io.IOException
         *                if the connection is not active or a communication error
         *                occurs.
         */
        protected void sendUnlink(OtpErlangPid from, OtpErlangPid dest)
        {
            if (!connected)
                throw new IOException("Not connected");

            OtpOutputStream header = new OtpOutputStream(headerLen);

            // preamble: 4 byte length + "passthrough" tag
            header.write4BE(0); // reserve space for length
            header.write1(passThrough);
            header.write1(version);

            // header
            header.write_tuple_head(3);
            header.write_long(unlinkTag);
            header.write_any(from);
            header.write_any(dest);

            // fix up length in preamble
            header.poke4BE(0, header.size() - 4);

            do_send(header);
        }

        /* used internally when "processes" terminate */
        protected void sendExit(OtpErlangPid from, OtpErlangPid dest, OtpErlangObject reason)
        {
            sendExit(exitTag, from, dest, reason);
        }

        /**
         * Send an exit signal to a remote process.
         * 
         * @param dest
         *            the Erlang PID of the remote process.
         * @param reason
         *            an Erlang term describing the exit reason.
         * 
         * @exception java.io.IOException
         *                if the connection is not active or a communication error
         *                occurs.
         */
        protected void sendExit2(OtpErlangPid from, OtpErlangPid dest, OtpErlangObject reason)
        {
            sendExit(exit2Tag, from, dest, reason);
        }

        private void sendExit(int tag, OtpErlangPid from, OtpErlangPid dest, OtpErlangObject reason)
        {
            if (!connected)
                throw new IOException("Not connected");

            OtpOutputStream header = new OtpOutputStream(headerLen);

            // preamble: 4 byte length + "passthrough" tag
            header.write4BE(0); // reserve space for length
            header.write1(passThrough);
            header.write1(version);

            // header
            header.write_tuple_head(4);
            header.write_long(tag);
            header.write_any(from);
            header.write_any(dest);
            header.write_any(reason);

            // fix up length in preamble
            header.poke4BE(0, header.size() - 4);

            do_send(header);
        }

        public override void run()
        {
            if (!connected)
            {
                deliver(new IOException("Not connected"));
                return;
            }

            byte[] lbuf = new byte[4];
            OtpInputStream ibuf;
            OtpErlangObject traceobj;
            byte[] tock = { 0, 0, 0, 0 };
            int len;

            try
            {
                while (!done)
                {
                    // don't return until we get a real message
                    // or a failure of some kind (e.g. EXIT)
                    // read length and read buffer must be atomic!

                    // read 4 bytes - get length of incoming packet
                    readSock(socket, lbuf);
                    ibuf = new OtpInputStream(lbuf, connFlags);
                    len = ibuf.read4BE();

                    // received tick? send tock!
                    if (len == 0)
                    {
                        lock (runLock)
                        {
                            socket.getOutputStream().Write(tock, 0, tock.Length);
                            socket.getOutputStream().Flush();
                        }
                        
                        continue;
                    }

                    // got a real message (maybe) - read len bytes
                    byte[] tmpbuf = new byte[len];
                    readSock(socket, tmpbuf);
                    ibuf = new OtpInputStream(tmpbuf, connFlags);

                    if (ibuf.read1() != passThrough)
                        continue;

                    // got a real message (really)
                    OtpErlangObject reason = null;
                    OtpErlangAtom cookie = null;
                    OtpErlangObject tmp = null;
                    OtpErlangTuple head = null;
                    OtpErlangAtom toName;
                    OtpErlangPid to;
                    OtpErlangPid from;
                    int tag;

                    // decode the header
                    tmp = ibuf.read_any();
                    if (!(tmp is OtpErlangTuple))
                        continue;

                    head = (OtpErlangTuple)tmp;
                    if (!(head.elementAt(0) is OtpErlangLong))
                        continue;

                    // lets see what kind of message this is
                    tag = (int)((OtpErlangLong)head.elementAt(0)).longValue();

                    switch (tag)
                    {
                        case sendTag:   // { SEND, Cookie, ToPid }
                        case sendTTTag: // { SEND, Cookie, ToPid, TraceToken }
                            if (!cookieOk)
                            {
                                // we only check this once, he can send us bad cookies later if he likes
                                if (!(head.elementAt(1) is OtpErlangAtom))
                                    continue;
                                cookie = (OtpErlangAtom)head.elementAt(1);
                                if (sendCookie)
                                {
                                    if (!cookie.atomValue().Equals(localNode.Cookie))
                                        cookieError(localNode, cookie);
                                }
                                else
                                {
                                    if (!cookie.atomValue().Equals(""))
                                        cookieError(localNode, cookie);
                                }
                                cookieOk = true;
                            }

                            if (traceLevel >= sendThreshold)
                            {
                                log.Debug("<- " + headerType(head) + " " + head);

                                /* show received payload too */
                                ibuf.Mark(0);
                                traceobj = ibuf.read_any();

                                if (traceobj != null)
                                    log.Debug("   " + traceobj);
                                else
                                    log.Debug("   (null)");

                                ibuf.Reset();
                            }

                            to = (OtpErlangPid)head.elementAt(2);

                            deliver(new OtpMsg(to, ibuf));
                            break;

                        case regSendTag:   // { REG_SEND, FromPid, Cookie, ToName }
                        case regSendTTTag: // { REG_SEND, FromPid, Cookie, ToName, TraceToken }
                            if (!cookieOk)
                            {
                                // we only check this once, he can send us bad cookies later if he likes
                                if (!(head.elementAt(2) is OtpErlangAtom))
                                    continue;

                                cookie = (OtpErlangAtom)head.elementAt(2);
                                if (sendCookie)
                                {
                                    if (!cookie.atomValue().Equals(localNode.Cookie))
                                        cookieError(localNode, cookie);
                                }
                                else
                                {
                                    if (!cookie.atomValue().Equals(""))
                                        cookieError(localNode, cookie);
                                }

                                cookieOk = true;
                            }

                            if (traceLevel >= sendThreshold)
                            {
                                log.Debug("<- " + headerType(head) + " " + head);

                                /* show received payload too */
                                ibuf.Mark(0);
                                traceobj = ibuf.read_any();

                                if (traceobj != null)
                                    log.Debug("   " + traceobj);
                                else
                                    log.Debug("   (null)");

                                ibuf.Reset();
                            }

                            from = (OtpErlangPid)head.elementAt(1);
                            toName = (OtpErlangAtom)head.elementAt(3);

                            deliver(new OtpMsg(from, toName.atomValue(), ibuf));
                            break;

                        case exitTag:  // { EXIT, FromPid, ToPid, Reason }
                        case exit2Tag: // { EXIT2, FromPid, ToPid, Reason }
                            if (head.elementAt(3) == null)
                                continue;

                            if (traceLevel >= ctrlThreshold)
                                log.Debug("<- " + headerType(head) + " " + head);

                            from = (OtpErlangPid)head.elementAt(1);
                            to = (OtpErlangPid)head.elementAt(2);
                            reason = head.elementAt(3);

                            deliver(new OtpMsg(tag, from, to, reason));
                            break;

                        case exitTTTag:  // { EXIT, FromPid, ToPid, TraceToken, Reason }
                        case exit2TTTag: // { EXIT2, FromPid, ToPid, TraceToken, Reason }
                            // as above, but bifferent element number
                            if (head.elementAt(4) == null)
                                continue;

                            if (traceLevel >= ctrlThreshold)
                                log.Debug("<- " + headerType(head) + " " + head);

                            from = (OtpErlangPid)head.elementAt(1);
                            to = (OtpErlangPid)head.elementAt(2);
                            reason = head.elementAt(4);

                            deliver(new OtpMsg(tag, from, to, reason));
                            break;

                        case linkTag:   // { LINK, FromPid, ToPid}
                        case unlinkTag: // { UNLINK, FromPid, ToPid}
                            if (traceLevel >= ctrlThreshold)
                                log.Debug("<- " + headerType(head) + " " + head);

                            from = (OtpErlangPid)head.elementAt(1);
                            to = (OtpErlangPid)head.elementAt(2);

                            deliver(new OtpMsg(tag, from, to));
                            break;

                        // absolutely no idea what to do with these, so we ignore them...
                        case groupLeaderTag: // { GROUPLEADER, FromPid, ToPid}
                            // (just show trace)
                            if (traceLevel >= ctrlThreshold)
                                log.Debug("<- " + headerType(head) + " " + head);
                            break;

                        default:
                            // garbage?
                            break;
                    }
                }

                // this section reachable only with break
                // we have received garbage from peer
                deliver(new OtpErlangExit("Remote is sending garbage"));

            }
            catch (OtpAuthException e)
            {
                deliver(e);
            }
            catch (OtpErlangDecodeException)
            {
                deliver(new OtpErlangExit("Remote is sending garbage"));
            }
            catch (IOException)
            {
                deliver(new OtpErlangExit("Remote has closed connection"));
            }
            finally
            {
                close();
            }
        }

        /**
         * <p>
         * Set the trace level for this connection. Normally tracing is off by
         * default unless System property OtpConnection.trace was set.
         * </p>
         * 
         * <p>
         * The following levels are valid: 0 turns off tracing completely, 1 shows
         * ordinary send and receive messages, 2 shows control messages such as link
         * and unlink, 3 shows handshaking at connection setup, and 4 shows
         * communication with Epmd. Each level includes the information shown by the
         * lower ones.
         * </p>
         * 
         * @param level
         *            the level to set.
         * 
         * @return the previous trace level.
         */
        public int setTraceLevel(int level)
        {
            int oldLevel = traceLevel;

            // pin the value
            if (level < 0)
            {
                level = 0;
            }
            else if (level > 4)
            {
                level = 4;
            }

            traceLevel = level;

            return oldLevel;
        }

        /**
         * Get the trace level for this connection.
         * 
         * @return the current trace level.
         */
        public int getTraceLevel()
        {
            return traceLevel;
        }

        /**
         * Close the connection to the remote node.
         */
        public virtual void close()
        {
            done = true;
            connected = false;
            lock (runLock)
            {
                try
                {
                    if (socket != null)
                    {
                        if (traceLevel >= ctrlThreshold)
                        {
                            log.Debug("-> CLOSE");
                        }
                        socket.close();
                    }
                }
                catch (SocketException) /* ignore socket close errors */
                {
                }
                catch (InvalidOperationException)
                {
                }
                finally
                {
                    socket = null;
                }
            }
        }

        /**
         * Determine if the connection is still alive. Note that this method only
         * reports the status of the connection, and that it is possible that there
         * are unread messages waiting in the receive queue.
         * 
         * @return true if the connection is alive.
         */
        public bool isConnected()
        {
            return connected;
        }

        // used by send and send_reg (message types with payload)
        protected void do_send(OtpOutputStream header, OtpOutputStream payload)
        {
            lock (runLock)
            {
                try
                {
                    if (traceLevel >= sendThreshold)
                    {
                        // Need to decode header and output buffer to show trace
                        // message!
                        // First make OtpInputStream, then decode.
                        try
                        {
                            OtpErlangObject h = header.getOtpInputStream(5).read_any();

                            log.Debug("-> " + headerType(h) + " " + h);

                            OtpErlangObject o = payload.getOtpInputStream(0).read_any();
                            log.Debug("   " + o);
                            o = null;
                        }
                        catch (OtpErlangDecodeException e)
                        {
                            log.Debug("   " + "can't decode output buffer:" + e);
                        }
                    }

                    header.WriteTo(socket.getOutputStream());
                    payload.WriteTo(socket.getOutputStream());
                }
                catch (IOException)
                {
                    close();
                    throw;
                }
            }
        }

        // used by the other message types
        protected void do_send(OtpOutputStream header)
        {
            lock (runLock)
            {
                try
                {
                    if (traceLevel >= ctrlThreshold)
                    {
                        try
                        {
                            OtpErlangObject h = header.getOtpInputStream(5).read_any();
                            log.Debug("-> " + headerType(h) + " " + h);
                        }
                        catch (OtpErlangDecodeException e)
                        {
                            log.Debug("   " + "can't decode output buffer: " + e);
                        }
                    }
                    header.WriteTo(socket.getOutputStream());
                }
                catch (IOException)
                {
                    close();
                    throw;
                }
            }
        }

        protected string headerType(OtpErlangObject h)
        {
            int tag = -1;

            if (h is OtpErlangTuple)
            {
                tag = (int)((OtpErlangLong)((OtpErlangTuple)h).elementAt(0)).longValue();
            }

            switch (tag)
            {
                case linkTag:
                    return "LINK";

                case sendTag:
                    return "SEND";

                case exitTag:
                    return "EXIT";

                case unlinkTag:
                    return "UNLINK";

                case regSendTag:
                    return "REG_SEND";

                case groupLeaderTag:
                    return "GROUP_LEADER";

                case exit2Tag:
                    return "EXIT2";

                case sendTTTag:
                    return "SEND_TT";

                case exitTTTag:
                    return "EXIT_TT";

                case regSendTTTag:
                    return "REG_SEND_TT";

                case exit2TTTag:
                    return "EXIT2_TT";
            }

            return "(unknown type)";
        }

        /* this method now throws exception if we don't get full read */
        protected int readSock(OtpTransport s, byte[] b)
        {
            int got = 0;
            int len = b.Length;
            int i;
            Stream st = null;

            lock (runLock)
            {
                if (s == null)
                    throw new IOException("expected " + len + " bytes, socket was closed");
                st = s.getInputStream();
            }

            while (got < len)
            {
                try
                {
                    i = st.Read(b, got, len - got);
                    if (i < 0)
                        throw new IOException("expected " + len + " bytes, got EOF after " + got + " bytes");

                    if (i == 0 && len != 0)
                    {
                        /*
                         * This is a corner case. According to
                         * http://java.sun.com/j2se/1.4.2/docs/api/ class InputStream
                         * is.read(,,l) can only return 0 if l==0. In other words it
                         * should not happen, but apparently did.
                         */
                        throw new IOException("Remote connection closed");
                    }

                    got += i;
                }
                catch (IOException)
                {
                    throw;
                }
                catch (ObjectDisposedException e)
                {
                    throw new IOException("Failed to read from socket", e);
                }
            }

            return got;
        }

        protected void doAccept()
        {
            int send_name_tag = recvName(peer);
            try
            {
                sendStatus("ok");
                int our_challenge = genChallenge();
                sendChallenge(peer.Flags, localNode.Flags, our_challenge);
                recvComplement(send_name_tag);
                int her_challenge = recvChallengeReply(our_challenge);
                byte[] our_digest = genDigest(her_challenge, localNode.Cookie);
                sendChallengeAck(our_digest);
                connected = true;
                cookieOk = true;
                sendCookie = false;
            }
            catch (IOException)
            {
                close();
                throw;
            }
            catch (OtpAuthException)
            {
                close();
                throw;
            }
            catch (Exception e)
            {
                string nn = peer.Node;
                close();
                throw new IOException("Error accepting connection from " + nn, e);
            }

            if (traceLevel >= handshakeThreshold)
                log.Debug("<- MD5 ACCEPTED " + peer.Host);
        }

        protected void doConnect(int port)
        {
            try
            {
                socket = peer.createTransport(peer.Host, port);

                if (traceLevel >= handshakeThreshold)
                    log.Debug("-> MD5 CONNECT TO " + peer.Host + ":" + port);

                int send_name_tag = sendName(peer.DistChoose, localNode.Flags, localNode.Creation);
                recvStatus();
                int her_challenge = recvChallenge();
                byte[] our_digest = genDigest(her_challenge, localNode.Cookie);
                int our_challenge = genChallenge();
                sendComplement(send_name_tag);
                sendChallengeReply(our_challenge, our_digest);
                recvChallengeAck(our_challenge);
                cookieOk = true;
                sendCookie = false;
            }
            catch (IOException)
            {
                throw;
            }
            catch (OtpAuthException)
            {
                close();
                throw;
            }
            catch (Exception e)
            {
                close();
                throw new IOException("Cannot connect to peer node", e);
            }
        }

        // This is nooo good as a challenge,
        // XXX fix me.
        static protected int genChallenge()
        {
            return random.Next();
        }

        // Used to debug print a message digest
        static string hex0(byte x)
        {
            char[] tab = { '0', '1', '2', '3', '4', '5', '6', '7', '8', '9',
			    'a', 'b', 'c', 'd', 'e', 'f' };
            uint u;
            if (x < 0)
            {
                u = ((uint)x) & 0x7F;
                u |= 1 << 7;
            }
            else
            {
                u = x;
            }
            return "" + tab[u >> 4] + tab[u & 0xF];
        }

        static string hex(byte[] b)
        {
            StringBuilder sb = new StringBuilder();
            try
            {
                int i;
                for (i = 0; i < b.Length; ++i)
                {
                    sb.Append(hex0(b[i]));
                }
            }
            catch (Exception)
            {
                // Debug function, ignore errors.
            }
            return sb.ToString();
        }

        protected byte[] genDigest(int challenge, string cookie)
        {
            long ch2;

            if (challenge < 0)
            {
                ch2 = 1L << 31;
                ch2 |= challenge & 0x7FFFFFFFL;
            }
            else
            {
                ch2 = challenge;
            }
            using (MD5 context = MD5.Create())
            {
                byte[] tmp = Encoding.ASCII.GetBytes(cookie);
                context.TransformBlock(tmp, 0, tmp.Length, tmp, 0);

                tmp = BitConverter.GetBytes(ch2);
                context.TransformFinalBlock(tmp, 0, tmp.Length);

                return context.Hash;
            }
        }

        protected int sendName(int dist, long aflags, int creation)
        {
            OtpOutputStream obuf = new OtpOutputStream();
            string str = localNode.Node;
            int send_name_tag;
            if (dist == 5)
            {
                obuf.write2BE(1 + 2 + 4 + str.Length);
                send_name_tag = 'n';
                obuf.write1(send_name_tag);
                obuf.write2BE(dist);
                obuf.write4BE(aflags);
                obuf.write(Encoding.GetEncoding("ISO-8859-1").GetBytes(str));
            }
            else
            {
                obuf.write2BE(1 + 8 + 4 + 2 + str.Length);
                send_name_tag = 'N';
                obuf.write1(send_name_tag);
                obuf.write8BE(aflags);
                obuf.write4BE(creation);
                obuf.write2BE(str.Length);
                obuf.write(Encoding.GetEncoding("ISO-8859-1").GetBytes(str));
            }

            obuf.WriteTo(socket.getOutputStream());

            if (traceLevel >= handshakeThreshold)
                log.Debug("-> " + "HANDSHAKE sendName" + " flags=" + aflags + " dist=" + dist + " local=" + localNode);

            return send_name_tag;
        }

        protected void sendComplement(int send_name_tag)
        {

            if (send_name_tag == 'n' && (peer.Flags & AbstractNode.dFlagHandshake23) != 0)
            {
                OtpOutputStream obuf = new OtpOutputStream();
                obuf.write2BE(1 + 4 + 4);
                obuf.write1('c');
                int flagsHigh = (int)(localNode.Flags >> 32);
                obuf.write4BE(flagsHigh);
                obuf.write4BE(localNode.Creation);

                obuf.WriteTo(socket.getOutputStream());

                if (traceLevel >= handshakeThreshold)
                    log.Debug("-> " + "HANDSHAKE sendComplement" + " flagsHigh=" + flagsHigh + " creation=" + localNode.Creation);
            }
        }

        protected void sendChallenge(long her_flags, long our_flags, int challenge)
        {
            OtpOutputStream obuf = new OtpOutputStream();
            string str = localNode.Node;
            if ((her_flags & AbstractNode.dFlagHandshake23) == 0)
            {
                obuf.write2BE(1 + 2 + 4 + 4 + str.Length);
                obuf.write1('n');
                obuf.write2BE(5);
                obuf.write4BE(our_flags & 0xffffffff);
                obuf.write4BE(challenge);
                obuf.write(Encoding.GetEncoding("ISO-8859-1").GetBytes(str));
            }
            else
            {
                obuf.write2BE(1 + 8 + 4 + 4 + 2 + str.Length);
                obuf.write1('N');
                obuf.write8BE(our_flags);
                obuf.write4BE(challenge);
                obuf.write4BE(localNode.Creation);
                obuf.write2BE(str.Length);
                obuf.write(Encoding.GetEncoding("ISO-8859-1").GetBytes(str));
            }

            obuf.WriteTo(socket.getOutputStream());

            if (traceLevel >= handshakeThreshold)
            {
                log.Debug("-> " + "HANDSHAKE sendChallenge" + " flags=" + our_flags + " challenge=" + challenge + " local=" + localNode);
            }
        }

        protected byte[] read2BytePackage()
        {
            byte[] lbuf = new byte[2];
            byte[] tmpbuf;

            readSock(socket, lbuf);
            OtpInputStream ibuf = new OtpInputStream(lbuf, 0);
            int len = ibuf.read2BE();
            tmpbuf = new byte[len];
            readSock(socket, tmpbuf);
            return tmpbuf;
        }

        protected int recvName(OtpPeer apeer)
        {
            int send_name_tag;
            string hisname = "";

            try
            {
                byte[] tmpbuf = read2BytePackage();
                OtpInputStream ibuf = new OtpInputStream(tmpbuf, 0);
                byte[] tmpname;
                int len = tmpbuf.Length;
                send_name_tag = ibuf.read1();
                switch (send_name_tag)
                {
                    case 'n':
                        apeer.DistLow = apeer.DistHigh = ibuf.read2BE();
                        if (apeer.DistLow != 5)
                            throw new IOException("Invalid handshake version");
                        apeer.Flags = ibuf.read4BE();
                        tmpname = new byte[len - 7];
                        ibuf.readN(tmpname);
                        hisname = OtpErlangString.newString(tmpname);
                        break;
                    case 'N':
                        apeer.DistLow = apeer.DistHigh = 6;
                        apeer.Flags = ibuf.read8BE();
                        if ((apeer.Flags & AbstractNode.dFlagHandshake23) == 0)
                            throw new IOException("Missing DFLAG_HANDSHAKE_23");
                        apeer.Creation = ibuf.read4BE();
                        int namelen = ibuf.read2BE();
                        tmpname = new byte[namelen];
                        ibuf.readN(tmpname);
                        hisname = OtpErlangString.newString(tmpname);
                        break;
                    default:
                        throw new IOException("Unknown remote node type");
                }

                if ((apeer.Flags & AbstractNode.dFlagExtendedReferences) == 0)
                    throw new IOException("Handshake failed - peer cannot handle extended references");

                if ((apeer.Flags & AbstractNode.dFlagExtendedPidsPorts) == 0)
                    throw new IOException("Handshake failed - peer cannot handle extended pids and ports");
            }
            catch (OtpErlangDecodeException)
            {
                throw new IOException("Handshake failed - not enough data");
            }

            string[] parts = hisname.Split(new char[] { '@' }, 2);
            apeer.Node = hisname;
            apeer.Alive = parts[0];
            apeer.Host = parts[1];

            if (traceLevel >= handshakeThreshold)
                log.Debug("<- " + "HANDSHAKE" + " ntype=" + apeer.Type + " dist=" + apeer.DistHigh + " remote=" + apeer);

            return send_name_tag;
        }

        protected int recvChallenge()
        {
            int challenge;

            try
            {
                byte[] buf = read2BytePackage();
                OtpInputStream ibuf = new OtpInputStream(buf, 0);
                int namelen;
                switch (ibuf.read1())
                {
                    case 'n':
                        if (peer.DistChoose != 5)
                            throw new IOException("Old challenge wrong version");
                        peer.DistLow = peer.DistHigh = ibuf.read2BE();
                        peer.Flags = ibuf.read4BE();
                        if ((peer.Flags & AbstractNode.dFlagHandshake23) != 0)
                            throw new IOException("Old challenge unexpected DFLAG_HANDHAKE_23");
                        challenge = ibuf.read4BE();
                        namelen = buf.Length - (1+2+4+4);
                        break;

                    case 'N':
                        peer.DistLow = peer.DistHigh = peer.DistChoose = 6;
                        peer.Flags = ibuf.read8BE();
                        if ((peer.Flags & AbstractNode.dFlagHandshake23) == 0)
                            throw new IOException("New challenge missing DFLAG_HANDHAKE_23");
                        challenge = ibuf.read4BE();
                        peer.Creation = ibuf.read4BE();
                        namelen = ibuf.read2BE();
                        break;

                    default:
                        throw new IOException("Unexpected peer type");
                }

                byte[] tmpname = new byte[namelen];
                ibuf.readN(tmpname);
                string hisname = OtpErlangString.newString(tmpname);
                if (!hisname.Equals(peer.Node))
                    throw new IOException("Handshake failed - peer has wrong name: " + hisname);

                if ((peer.Flags & AbstractNode.dFlagExtendedReferences) == 0)
                    throw new IOException("Handshake failed - peer cannot handle extended references");

                if ((peer.Flags & AbstractNode.dFlagExtendedPidsPorts) == 0)
                    throw new IOException("Handshake failed - peer cannot handle extended pids and ports");

            }
            catch (OtpErlangDecodeException e)
            {
                throw new IOException("Handshake failed - not enough data", e);
            }

            if (traceLevel >= handshakeThreshold)
                log.Debug("<- " + "HANDSHAKE recvChallenge" + " from=" + peer.Node + " challenge=" + challenge + " local=" + localNode);

            return challenge;
        }

        protected void recvComplement(int send_name_tag)
        {

            if (send_name_tag == 'n' && (peer.Flags & AbstractNode.dFlagHandshake23) != 0)
            {
                try
                {
                    byte[] tmpbuf = read2BytePackage();
                    OtpInputStream ibuf = new OtpInputStream(tmpbuf, 0);
                    if (ibuf.read1() != 'c')
                        throw new IOException("Not a complement tag");

                    long flagsHigh = ibuf.read4BE();
                    peer.Flags |= flagsHigh << 32;
                    peer.Creation = ibuf.read4BE();

                }
                catch (OtpErlangDecodeException e)
                {
                    throw new IOException("Handshake failed - not enough data", e);
                }
            }
        }

        protected void sendChallengeReply(int challenge, byte[] digest)
        {
            OtpOutputStream obuf = new OtpOutputStream();
            obuf.write2BE(21);
            obuf.write1(ChallengeReply);
            obuf.write4BE(challenge);
            obuf.write(digest);
            obuf.WriteTo(socket.getOutputStream());

            if (traceLevel >= handshakeThreshold)
            {
                log.Debug("-> " + "HANDSHAKE sendChallengeReply"
                             + " challenge=" + challenge + " digest=" + hex(digest)
                             + " local=" + localNode);
            }
        }

        // Would use Array.equals in newer JDK...
        private bool digests_equals(byte[] a, byte[] b)
        {
            int i;
            for (i = 0; i < 16; ++i)
            {
                if (a[i] != b[i])
                {
                    return false;
                }
            }
            return true;
        }

        protected int recvChallengeReply(int our_challenge)
        {
            int challenge;
            byte[] her_digest = new byte[16];

            try
            {
                byte[] buf = read2BytePackage();
                OtpInputStream ibuf = new OtpInputStream(buf, 0);
                int tag = ibuf.read1();
                if (tag != ChallengeReply)
                    throw new IOException("Handshake protocol error");
                challenge = ibuf.read4BE();
                ibuf.readN(her_digest);
                byte[] our_digest = genDigest(our_challenge, localNode.Cookie);
                if (!digests_equals(her_digest, our_digest))
                    throw new OtpAuthException("Peer authentication error.");
            }
            catch (OtpErlangDecodeException e)
            {
                throw new IOException("Handshake failed - not enough data", e);
            }

            if (traceLevel >= handshakeThreshold)
            {
                log.Debug("<- " + "HANDSHAKE recvChallengeReply"
                             + " from=" + peer.Node + " challenge=" + challenge
                             + " digest=" + hex(her_digest) + " local=" + localNode);
            }

            return challenge;
        }

        protected void sendChallengeAck(byte[] digest)
        {
            OtpOutputStream obuf = new OtpOutputStream();
            obuf.write2BE(17);
            obuf.write1(ChallengeAck);
            obuf.write(digest);

            obuf.WriteTo(socket.getOutputStream());

            if (traceLevel >= handshakeThreshold)
            {
                log.Debug("-> " + "HANDSHAKE sendChallengeAck"
                             + " digest=" + hex(digest) + " local=" + localNode);
            }
        }

        protected void recvChallengeAck(int our_challenge)
        {
            byte[] her_digest = new byte[16];
            try
            {
                byte[] buf = read2BytePackage();
                OtpInputStream ibuf = new OtpInputStream(buf, 0);
                int tag = ibuf.read1();
                if (tag != ChallengeAck)
                    throw new IOException("Handshake protocol error");
                ibuf.readN(her_digest);
                byte[] our_digest = genDigest(our_challenge, localNode.Cookie);
                if (!digests_equals(her_digest, our_digest))
                    throw new OtpAuthException("Peer authentication error.");
            }
            catch (OtpErlangDecodeException e)
            {
                throw new IOException("Handshake failed - not enough data", e);
            }
            catch (Exception e)
            {
                log.Error("Peer authentication error", e);
                throw new OtpAuthException("Peer authentication error.", e);
            }

            if (traceLevel >= handshakeThreshold)
            {
                log.Debug("<- " + "HANDSHAKE recvChallengeAck" + " from="
                             + peer.Node + " digest=" + hex(her_digest) + " local=" + localNode);
            }
        }

        protected void sendStatus(string status)
        {
            OtpOutputStream obuf = new OtpOutputStream();
            obuf.write2BE(status.Length + 1);
            obuf.write1(ChallengeStatus);
            obuf.write(Encoding.GetEncoding("iso-8859-1").GetBytes(status));

            obuf.WriteTo(socket.getOutputStream());

            if (traceLevel >= handshakeThreshold)
            {
                log.Debug("-> " + "HANDSHAKE sendStatus" + " status="
                             + status + " local=" + localNode);
            }
        }

        protected void recvStatus()
        {
            try
            {
                byte[] buf = read2BytePackage();
                OtpInputStream ibuf = new OtpInputStream(buf, 0);
                int tag = ibuf.read1();
                if (tag != ChallengeStatus)
                    throw new IOException("Handshake protocol error");
                byte[] tmpbuf = new byte[buf.Length - 1];
                ibuf.readN(tmpbuf);
                string status = OtpErlangString.newString(tmpbuf);

                if (status.CompareTo("ok") != 0)
                    throw new IOException("Peer replied with status '" + status + "' instead of 'ok'");
            }
            catch (OtpErlangDecodeException e)
            {
                throw new IOException("Handshake failed - not enough data", e);
            }
            if (traceLevel >= handshakeThreshold)
            {
                log.Debug("<- " + "HANDSHAKE recvStatus (ok)" + " local=" + localNode);
            }
        }

        public void setFlags(int flags)
        {
            this.connFlags = flags;
        }

        public int getFlags()
        {
            return connFlags;
        }
    }
}
