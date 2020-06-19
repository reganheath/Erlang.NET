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
using log4net;
using log4net.Config;
using System;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;

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

        private readonly object objLock = new object();

        protected bool connected = false; // connection status
        protected OtpTransport socket; // communication channel
        protected OtpPeer peer; // who are we connected to
        protected OtpLocalNode localNode; // this nodes id

        public string Name { get; set; }

        protected bool cookieOk = false; // already checked the cookie for this connection
        protected bool sendCookie = true; // Send cookies in messages?

        // tracelevel constants
        protected int traceLevel = 0;

        protected static int defaultLevel = 0;
        protected static int sendThreshold = 1;
        protected static int ctrlThreshold = 2;
        protected static int handshakeThreshold = 3;

        protected static readonly Random random;

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
            localNode = self;
            peer = new OtpPeer(self.transportFactory);
            socket = s;

            traceLevel = defaultLevel;

            if (traceLevel >= handshakeThreshold)
                log.Debug("<- ACCEPT FROM " + s);

            DoAccept();
            Name = peer.Node;
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
            localNode = self;
            socket = null;
            traceLevel = defaultLevel;

            // Locate peer
            int port = OtpEpmd.LookupPort(peer);
            if (port == 0)
                throw new IOException("No remote node found - cannot connect");

            // compatible?
            if (peer.Proto != self.Proto || self.DistHigh < peer.DistLow || self.DistLow > peer.DistHigh)
                throw new IOException("No common protocol found - cannot connect");

            // highest common version
            peer.DistChoose = Math.Min(peer.DistHigh, self.DistHigh);

            DoConnect(port);
            Name = peer.Node;
        }

        /**
         * Deliver communication exceptions to the recipient.
         */
        public abstract void Deliver(Exception e);

        /**
         * Deliver messages to the recipient.
         */
        public abstract void Deliver(OtpMsg msg);

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
        protected void SendBuf(OtpErlangPid from, string dest, OtpOutputStream payload)
        {
            if (!connected)
                throw new IOException("Not connected");

            OtpOutputStream header = new OtpOutputStream(headerLen);

            // preamble: 4 byte length + "passthrough" tag + version
            header.Write4BE(0); // reserve space for length
            header.Write1(passThrough);
            header.Write1(version);

            // header info
            header.WriteTupleHead(4);
            header.WriteLong(regSendTag);
            header.WriteAny(from);
            if (sendCookie)
                header.WriteAtom(localNode.Cookie);
            else
                header.WriteAtom("");
            header.WriteAtom(dest);

            // version for payload
            header.Write1(version);

            // fix up length in preamble
            header.Poke4BE(0, header.Length + payload.Length - 4);

            DoSend(header, payload);
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
        protected void SendBuf(OtpErlangPid from, OtpErlangPid dest, OtpOutputStream payload)
        {
            if (!connected)
                throw new IOException("Not connected");

            OtpOutputStream header = new OtpOutputStream(headerLen);

            // preamble: 4 byte length + "passthrough" tag + version
            header.Write4BE(0); // reserve space for length
            header.Write1(passThrough);
            header.Write1(version);

            // header info
            header.WriteTupleHead(3);
            header.WriteLong(sendTag);
            if (sendCookie)
                header.WriteAtom(localNode.Cookie);
            else
                header.WriteAtom("");
            header.WriteAny(dest);

            // version for payload
            header.Write1(version);

            // fix up length in preamble
            header.Poke4BE(0, header.Length + payload.Length - 4);

            DoSend(header, payload);
        }

        /*
         * Send an auth error to peer because he sent a bad cookie. The auth error
         * uses his cookie (not revealing ours). This is just like send_reg
         * otherwise
         */
        private void CookieError(OtpLocalNode local, OtpErlangAtom cookie)
        {
            try
            {
                OtpOutputStream header = new OtpOutputStream(headerLen);

                // preamble: 4 byte length + "passthrough" tag + version
                header.Write4BE(0); // reserve space for length
                header.Write1(passThrough);
                header.Write1(version);

                header.WriteTupleHead(4);
                header.WriteLong(regSendTag);
                header.WriteAny(local.createPid()); // disposable pid
                header.WriteAtom(cookie.atomValue()); // important: his cookie,
                // not mine...
                header.WriteAtom("auth");

                // version for payload
                header.Write1(version);

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
                header.Poke4BE(0, header.Length + payload.Length - 4);

                try { DoSend(header, payload); }
                catch (IOException) { } // ignore

            }
            finally
            {
                Close();
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
        protected void SendLink(OtpErlangPid from, OtpErlangPid dest)
        {
            if (!connected)
                throw new IOException("Not connected");

            OtpOutputStream header = new OtpOutputStream(headerLen);

            // preamble: 4 byte length + "passthrough" tag
            header.Write4BE(0); // reserve space for length
            header.Write1(passThrough);
            header.Write1(version);

            // header
            header.WriteTupleHead(3);
            header.WriteLong(linkTag);
            header.WriteAny(from);
            header.WriteAny(dest);

            // fix up length in preamble
            header.Poke4BE(0, header.Length - 4);

            DoSend(header);
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
        protected void SendUnlink(OtpErlangPid from, OtpErlangPid dest)
        {
            if (!connected)
                throw new IOException("Not connected");

            OtpOutputStream header = new OtpOutputStream(headerLen);

            // preamble: 4 byte length + "passthrough" tag
            header.Write4BE(0); // reserve space for length
            header.Write1(passThrough);
            header.Write1(version);

            // header
            header.WriteTupleHead(3);
            header.WriteLong(unlinkTag);
            header.WriteAny(from);
            header.WriteAny(dest);

            // fix up length in preamble
            header.Poke4BE(0, header.Length - 4);

            DoSend(header);
        }

        /* used internally when "processes" terminate */
        protected void SendExit(OtpErlangPid from, OtpErlangPid dest, OtpErlangObject reason)
        {
            SendExit(exitTag, from, dest, reason);
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
        protected void SendExit2(OtpErlangPid from, OtpErlangPid dest, OtpErlangObject reason)
        {
            SendExit(exit2Tag, from, dest, reason);
        }

        private void SendExit(int tag, OtpErlangPid from, OtpErlangPid dest, OtpErlangObject reason)
        {
            if (!connected)
                throw new IOException("Not connected");

            OtpOutputStream header = new OtpOutputStream(headerLen);

            // preamble: 4 byte length + "passthrough" tag
            header.Write4BE(0); // reserve space for length
            header.Write1(passThrough);
            header.Write1(version);

            // header
            header.WriteTupleHead(4);
            header.WriteLong(tag);
            header.WriteAny(from);
            header.WriteAny(dest);
            header.WriteAny(reason);

            // fix up length in preamble
            header.Poke4BE(0, header.Length - 4);

            DoSend(header);
        }

        public override void Run()
        {
            if (!connected)
            {
                Deliver(new IOException("Not connected"));
                return;
            }

            byte[] lbuf = new byte[4];
            OtpInputStream ibuf;
            OtpErlangObject traceobj;
            byte[] tock = { 0, 0, 0, 0 };
            int len;

            try
            {
                while (!Stopping)
                {
                    // don't return until we get a real message
                    // or a failure of some kind (e.g. EXIT)
                    // read length and read buffer must be atomic!

                    // read 4 bytes - get length of incoming packet
                    ReadSock(socket, lbuf);
                    ibuf = new OtpInputStream(lbuf, localNode.Flags);
                    len = ibuf.Read4BE();

                    // received tick? send tock!
                    if (len == 0)
                    {
                        lock (objLock)
                        {
                            socket.GetOutputStream().Write(tock, 0, tock.Length);
                            socket.GetOutputStream().Flush();
                        }

                        continue;
                    }

                    // got a real message (maybe) - read len bytes
                    byte[] tmpbuf = new byte[len];
                    ReadSock(socket, tmpbuf);
                    ibuf = new OtpInputStream(tmpbuf, localNode.Flags);

                    if (ibuf.Read1() != passThrough)
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
                    tmp = ibuf.ReadAny();
                    if (!(tmp is OtpErlangTuple))
                        continue;

                    head = (OtpErlangTuple)tmp;
                    if (!(head.elementAt(0) is OtpErlangLong))
                        continue;

                    // lets see what kind of message this is
                    tag = (int)((OtpErlangLong)head.elementAt(0)).LongValue();

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
                                        CookieError(localNode, cookie);
                                }
                                else
                                {
                                    if (!cookie.atomValue().Equals(""))
                                        CookieError(localNode, cookie);
                                }
                                cookieOk = true;
                            }

                            if (traceLevel >= sendThreshold)
                            {
                                log.Debug("<- " + HeaderType(head) + " " + head);

                                /* show received payload too */
                                ibuf.Mark();
                                traceobj = ibuf.ReadAny();

                                if (traceobj != null)
                                    log.Debug("   " + traceobj);
                                else
                                    log.Debug("   (null)");

                                ibuf.Reset();
                            }

                            to = (OtpErlangPid)head.elementAt(2);

                            Deliver(new OtpMsg(to, ibuf));
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
                                        CookieError(localNode, cookie);
                                }
                                else
                                {
                                    if (!cookie.atomValue().Equals(""))
                                        CookieError(localNode, cookie);
                                }

                                cookieOk = true;
                            }

                            if (traceLevel >= sendThreshold)
                            {
                                log.Debug("<- " + HeaderType(head) + " " + head);

                                /* show received payload too */
                                ibuf.Mark();
                                traceobj = ibuf.ReadAny();

                                if (traceobj != null)
                                    log.Debug("   " + traceobj);
                                else
                                    log.Debug("   (null)");

                                ibuf.Reset();
                            }

                            from = (OtpErlangPid)head.elementAt(1);
                            toName = (OtpErlangAtom)head.elementAt(3);

                            Deliver(new OtpMsg(from, toName.atomValue(), ibuf));
                            break;

                        case exitTag:  // { EXIT, FromPid, ToPid, Reason }
                        case exit2Tag: // { EXIT2, FromPid, ToPid, Reason }
                            if (head.elementAt(3) == null)
                                continue;

                            if (traceLevel >= ctrlThreshold)
                                log.Debug("<- " + HeaderType(head) + " " + head);

                            from = (OtpErlangPid)head.elementAt(1);
                            to = (OtpErlangPid)head.elementAt(2);
                            reason = head.elementAt(3);

                            Deliver(new OtpMsg(tag, from, to, reason));
                            break;

                        case exitTTTag:  // { EXIT, FromPid, ToPid, TraceToken, Reason }
                        case exit2TTTag: // { EXIT2, FromPid, ToPid, TraceToken, Reason }
                            // as above, but bifferent element number
                            if (head.elementAt(4) == null)
                                continue;

                            if (traceLevel >= ctrlThreshold)
                                log.Debug("<- " + HeaderType(head) + " " + head);

                            from = (OtpErlangPid)head.elementAt(1);
                            to = (OtpErlangPid)head.elementAt(2);
                            reason = head.elementAt(4);

                            Deliver(new OtpMsg(tag, from, to, reason));
                            break;

                        case linkTag:   // { LINK, FromPid, ToPid}
                        case unlinkTag: // { UNLINK, FromPid, ToPid}
                            if (traceLevel >= ctrlThreshold)
                                log.Debug("<- " + HeaderType(head) + " " + head);

                            from = (OtpErlangPid)head.elementAt(1);
                            to = (OtpErlangPid)head.elementAt(2);

                            Deliver(new OtpMsg(tag, from, to));
                            break;

                        // absolutely no idea what to do with these, so we ignore them...
                        case groupLeaderTag: // { GROUPLEADER, FromPid, ToPid}
                            // (just show trace)
                            if (traceLevel >= ctrlThreshold)
                                log.Debug("<- " + HeaderType(head) + " " + head);
                            break;

                        default:
                            // garbage?
                            break;
                    }
                }

                // this section reachable only with break
                // we have received garbage from peer
                Deliver(new OtpErlangExit("Remote is sending garbage"));

            }
            catch (OtpAuthException e)
            {
                Deliver(e);
            }
            catch (OtpErlangDecodeException)
            {
                Deliver(new OtpErlangExit("Remote is sending garbage"));
            }
            catch (IOException)
            {
                Deliver(new OtpErlangExit("Remote has closed connection"));
            }
            finally
            {
                Close();
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
        public int SetTraceLevel(int level)
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
        public int GetTraceLevel()
        {
            return traceLevel;
        }

        /**
         * Close the connection to the remote node.
         */
        public virtual void Close()
        {
            Stop();
            connected = false;
            lock (objLock)
            {
                try
                {
                    if (socket != null)
                    {
                        if (traceLevel >= ctrlThreshold)
                            log.Debug("-> CLOSE");
                        socket.Close();
                    }
                }
                catch (SocketException) { }
                catch (InvalidOperationException) { }
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
        public bool IsConnected()
        {
            return connected;
        }

        // used by send and send_reg (message types with payload)
        protected void DoSend(OtpOutputStream header, OtpOutputStream payload)
        {
            lock (objLock)
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
                            OtpErlangObject h = header.GetOtpInputStream(5).ReadAny();

                            log.Debug("-> " + HeaderType(h) + " " + h);

                            OtpErlangObject o = payload.GetOtpInputStream(0).ReadAny();
                            log.Debug("   " + o);
                            o = null;
                        }
                        catch (OtpErlangDecodeException e)
                        {
                            log.Debug("   " + "can't decode output buffer:" + e);
                        }
                    }

                    header.WriteTo(socket.GetOutputStream());
                    payload.WriteTo(socket.GetOutputStream());
                }
                catch (IOException)
                {
                    Close();
                    throw;
                }
            }
        }

        // used by the other message types
        protected void DoSend(OtpOutputStream header)
        {
            lock (objLock)
            {
                try
                {
                    if (traceLevel >= ctrlThreshold)
                    {
                        try
                        {
                            OtpErlangObject h = header.GetOtpInputStream(5).ReadAny();
                            log.Debug("-> " + HeaderType(h) + " " + h);
                        }
                        catch (OtpErlangDecodeException e)
                        {
                            log.Debug("   " + "can't decode output buffer: " + e);
                        }
                    }
                    header.WriteTo(socket.GetOutputStream());
                }
                catch (IOException)
                {
                    Close();
                    throw;
                }
            }
        }

        protected string HeaderType(OtpErlangObject h)
        {
            int tag = -1;

            if (h is OtpErlangTuple)
            {
                tag = (int)((OtpErlangLong)((OtpErlangTuple)h).elementAt(0)).LongValue();
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
        protected int ReadSock(OtpTransport s, byte[] b)
        {
            int got = 0;
            int len = b.Length;
            int i;
            Stream st = null;

            lock (objLock)
            {
                if (s == null)
                    throw new IOException("expected " + len + " bytes, socket was closed");
                st = s.GetInputStream();
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

        protected void DoAccept()
        {
            int send_name_tag = RecvName(peer);
            try
            {
                SendStatus("ok");
                int our_challenge = GenChallenge();
                SendChallenge(peer.CapFlags, localNode.CapFlags, our_challenge);
                RecvComplement(send_name_tag);
                int her_challenge = RecvChallengeReply(our_challenge);
                byte[] our_digest = GenDigest(her_challenge, localNode.Cookie);
                SendChallengeAck(our_digest);
                connected = true;
                cookieOk = true;
                sendCookie = false;
            }
            catch (IOException)
            {
                Close();
                throw;
            }
            catch (OtpAuthException)
            {
                Close();
                throw;
            }
            catch (Exception e)
            {
                string nn = peer.Node;
                Close();
                throw new IOException("Error accepting connection from " + nn, e);
            }

            if (traceLevel >= handshakeThreshold)
                log.Debug("<- MD5 ACCEPTED " + peer.Host);
        }

        protected void DoConnect(int port)
        {
            try
            {
                socket = peer.CreateTransport(peer.Host, port);

                if (traceLevel >= handshakeThreshold)
                    log.Debug("-> MD5 CONNECT TO " + peer.Host + ":" + port);

                int send_name_tag = SendName(peer.DistChoose, localNode.CapFlags, localNode.Creation);
                RecvStatus();
                int her_challenge = RecvChallenge();
                byte[] our_digest = GenDigest(her_challenge, localNode.Cookie);
                int our_challenge = GenChallenge();
                SendComplement(send_name_tag);
                SendChallengeReply(our_challenge, our_digest);
                RecvChallengeAck(our_challenge);
                connected = true;
                cookieOk = true;
                sendCookie = false;
            }
            catch (IOException)
            {
                throw;
            }
            catch (OtpAuthException)
            {
                Close();
                throw;
            }
            catch (Exception e)
            {
                Close();
                throw new IOException("Cannot connect to peer node", e);
            }
        }

        // This is nooo good as a challenge,
        // XXX fix me.
        static protected int GenChallenge()
        {
            return random.Next();
        }

        // Used to debug print a message digest
        static string Hex0(byte x)
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

        static string Hex(byte[] b)
        {
            StringBuilder sb = new StringBuilder();
            try
            {
                int i;
                for (i = 0; i < b.Length; ++i)
                {
                    sb.Append(Hex0(b[i]));
                }
            }
            catch (Exception)
            {
                // Debug function, ignore errors.
            }
            return sb.ToString();
        }

        protected byte[] GenDigest(int challenge, string cookie)
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

                tmp = Encoding.ASCII.GetBytes(ch2.ToString());
                context.TransformFinalBlock(tmp, 0, tmp.Length);

                return context.Hash;
            }
        }

        protected int SendName(int dist, long aflags, int creation)
        {
            OtpOutputStream obuf = new OtpOutputStream();
            string str = localNode.Node;
            int send_name_tag;
            if (dist == 5)
            {
                obuf.Write2BE(1 + 2 + 4 + str.Length);
                send_name_tag = 'n';
                obuf.Write1(send_name_tag);
                obuf.Write2BE(dist);
                obuf.Write4BE(aflags);
                obuf.Write(Encoding.GetEncoding("ISO-8859-1").GetBytes(str));
            }
            else
            {
                obuf.Write2BE(1 + 8 + 4 + 2 + str.Length);
                send_name_tag = 'N';
                obuf.Write1(send_name_tag);
                obuf.Write8BE(aflags);
                obuf.Write4BE(creation);
                obuf.Write2BE(str.Length);
                obuf.Write(Encoding.GetEncoding("ISO-8859-1").GetBytes(str));
            }

            obuf.WriteTo(socket.GetOutputStream());

            if (traceLevel >= handshakeThreshold)
                log.Debug("-> " + "HANDSHAKE sendName" + " flags=" + aflags + " dist=" + dist + " local=" + localNode);

            return send_name_tag;
        }

        protected void SendComplement(int send_name_tag)
        {

            if (send_name_tag == 'n' && (peer.CapFlags & AbstractNode.dFlagHandshake23) != 0)
            {
                OtpOutputStream obuf = new OtpOutputStream();
                obuf.Write2BE(1 + 4 + 4);
                obuf.Write1('c');
                int flagsHigh = (int)(localNode.CapFlags >> 32);
                obuf.Write4BE(flagsHigh);
                obuf.Write4BE(localNode.Creation);

                obuf.WriteTo(socket.GetOutputStream());

                if (traceLevel >= handshakeThreshold)
                    log.Debug("-> " + "HANDSHAKE sendComplement" + " flagsHigh=" + flagsHigh + " creation=" + localNode.Creation);
            }
        }

        protected void SendChallenge(long her_flags, long our_flags, int challenge)
        {
            OtpOutputStream obuf = new OtpOutputStream();
            string str = localNode.Node;
            if ((her_flags & AbstractNode.dFlagHandshake23) == 0)
            {
                obuf.Write2BE(1 + 2 + 4 + 4 + str.Length);
                obuf.Write1('n');
                obuf.Write2BE(5);
                obuf.Write4BE(our_flags & 0xffffffff);
                obuf.Write4BE(challenge);
                obuf.Write(Encoding.GetEncoding("ISO-8859-1").GetBytes(str));
            }
            else
            {
                obuf.Write2BE(1 + 8 + 4 + 4 + 2 + str.Length);
                obuf.Write1('N');
                obuf.Write8BE(our_flags);
                obuf.Write4BE(challenge);
                obuf.Write4BE(localNode.Creation);
                obuf.Write2BE(str.Length);
                obuf.Write(Encoding.GetEncoding("ISO-8859-1").GetBytes(str));
            }

            obuf.WriteTo(socket.GetOutputStream());

            if (traceLevel >= handshakeThreshold)
            {
                log.Debug("-> " + "HANDSHAKE sendChallenge" + " flags=" + our_flags + " challenge=" + challenge + " local=" + localNode);
            }
        }

        protected byte[] Read2BytePackage()
        {
            byte[] lbuf = new byte[2];
            byte[] tmpbuf;

            ReadSock(socket, lbuf);
            OtpInputStream ibuf = new OtpInputStream(lbuf, 0);
            int len = ibuf.Read2BE();
            tmpbuf = new byte[len];
            ReadSock(socket, tmpbuf);
            return tmpbuf;
        }

        protected int RecvName(OtpPeer apeer)
        {
            int send_name_tag;
            string hisname;

            try
            {
                byte[] tmpbuf = Read2BytePackage();
                OtpInputStream ibuf = new OtpInputStream(tmpbuf, 0);
                byte[] tmpname;
                int len = tmpbuf.Length;
                send_name_tag = ibuf.Read1();
                switch (send_name_tag)
                {
                    case 'n':
                        apeer.DistLow = apeer.DistHigh = ibuf.Read2BE();
                        if (apeer.DistLow != 5)
                            throw new IOException("Invalid handshake version");
                        apeer.CapFlags = ibuf.Read4BE();
                        tmpname = new byte[len - 7];
                        ibuf.ReadN(tmpname);
                        hisname = OtpErlangString.FromEncoding(tmpname);
                        break;
                    case 'N':
                        apeer.DistLow = apeer.DistHigh = 6;
                        apeer.CapFlags = ibuf.Read8BE();
                        if ((apeer.CapFlags & AbstractNode.dFlagHandshake23) == 0)
                            throw new IOException("Missing DFLAG_HANDSHAKE_23");
                        apeer.Creation = ibuf.Read4BE();
                        int namelen = ibuf.Read2BE();
                        tmpname = new byte[namelen];
                        ibuf.ReadN(tmpname);
                        hisname = OtpErlangString.FromEncoding(tmpname);
                        break;
                    default:
                        throw new IOException("Unknown remote node type");
                }

                if ((apeer.CapFlags & AbstractNode.dFlagExtendedReferences) == 0)
                    throw new IOException("Handshake failed - peer cannot handle extended references");

                if ((apeer.CapFlags & AbstractNode.dFlagExtendedPidsPorts) == 0)
                    throw new IOException("Handshake failed - peer cannot handle extended pids and ports");
            }
            catch (OtpErlangDecodeException)
            {
                throw new IOException("Handshake failed - not enough data");
            }

            (apeer.Alive, apeer.Host) = hisname.Split('@');
            if (apeer.Alive == null || apeer.Host == null)
                throw new IOException("Handshake failed - peer name invalid: " + hisname);
            apeer.Node = hisname;

            if (traceLevel >= handshakeThreshold)
                log.Debug("<- " + "HANDSHAKE" + " ntype=" + apeer.Type + " dist=" + apeer.DistHigh + " remote=" + apeer);

            return send_name_tag;
        }

        protected int RecvChallenge()
        {
            int challenge;

            try
            {
                byte[] buf = Read2BytePackage();
                OtpInputStream ibuf = new OtpInputStream(buf, 0);
                int namelen;
                switch (ibuf.Read1())
                {
                    case 'n':
                        if (peer.DistChoose != 5)
                            throw new IOException("Old challenge wrong version");
                        peer.DistLow = peer.DistHigh = ibuf.Read2BE();
                        peer.CapFlags = ibuf.Read4BE();
                        if ((peer.CapFlags & AbstractNode.dFlagHandshake23) != 0)
                            throw new IOException("Old challenge unexpected DFLAG_HANDHAKE_23");
                        challenge = ibuf.Read4BE();
                        namelen = buf.Length - (1 + 2 + 4 + 4);
                        break;

                    case 'N':
                        peer.DistLow = peer.DistHigh = peer.DistChoose = 6;
                        peer.CapFlags = ibuf.Read8BE();
                        if ((peer.CapFlags & AbstractNode.dFlagHandshake23) == 0)
                            throw new IOException("New challenge missing DFLAG_HANDHAKE_23");
                        challenge = ibuf.Read4BE();
                        peer.Creation = ibuf.Read4BE();
                        namelen = ibuf.Read2BE();
                        break;

                    default:
                        throw new IOException("Unexpected peer type");
                }

                byte[] tmpname = new byte[namelen];
                ibuf.ReadN(tmpname);
                string hisname = OtpErlangString.FromEncoding(tmpname);
                if (!hisname.Equals(peer.Node))
                    throw new IOException("Handshake failed - peer has wrong name: " + hisname);

                if ((peer.CapFlags & AbstractNode.dFlagExtendedReferences) == 0)
                    throw new IOException("Handshake failed - peer cannot handle extended references");

                if ((peer.CapFlags & AbstractNode.dFlagExtendedPidsPorts) == 0)
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

        protected void RecvComplement(int send_name_tag)
        {

            if (send_name_tag == 'n' && (peer.CapFlags & AbstractNode.dFlagHandshake23) != 0)
            {
                try
                {
                    byte[] tmpbuf = Read2BytePackage();
                    OtpInputStream ibuf = new OtpInputStream(tmpbuf, 0);
                    if (ibuf.Read1() != 'c')
                        throw new IOException("Not a complement tag");

                    long flagsHigh = ibuf.Read4BE();
                    peer.CapFlags |= flagsHigh << 32;
                    peer.Creation = ibuf.Read4BE();

                }
                catch (OtpErlangDecodeException e)
                {
                    throw new IOException("Handshake failed - not enough data", e);
                }
            }
        }

        protected void SendChallengeReply(int challenge, byte[] digest)
        {
            OtpOutputStream obuf = new OtpOutputStream();
            obuf.Write2BE(21);
            obuf.Write1(ChallengeReply);
            obuf.Write4BE(challenge);
            obuf.Write(digest);
            obuf.WriteTo(socket.GetOutputStream());

            if (traceLevel >= handshakeThreshold)
            {
                log.Debug("-> " + "HANDSHAKE sendChallengeReply"
                             + " challenge=" + challenge + " digest=" + Hex(digest)
                             + " local=" + localNode);
            }
        }

        protected int RecvChallengeReply(int our_challenge)
        {
            int challenge;
            byte[] her_digest = new byte[16];

            try
            {
                byte[] buf = Read2BytePackage();
                OtpInputStream ibuf = new OtpInputStream(buf, 0);
                int tag = ibuf.Read1();
                if (tag != ChallengeReply)
                    throw new IOException("Handshake protocol error");
                challenge = ibuf.Read4BE();
                ibuf.ReadN(her_digest);
                byte[] our_digest = GenDigest(our_challenge, localNode.Cookie);
                if (!her_digest.SequenceEqual(our_digest))
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
                             + " digest=" + Hex(her_digest) + " local=" + localNode);
            }

            return challenge;
        }

        protected void SendChallengeAck(byte[] digest)
        {
            OtpOutputStream obuf = new OtpOutputStream();
            obuf.Write2BE(17);
            obuf.Write1(ChallengeAck);
            obuf.Write(digest);

            obuf.WriteTo(socket.GetOutputStream());

            if (traceLevel >= handshakeThreshold)
            {
                log.Debug("-> " + "HANDSHAKE sendChallengeAck"
                             + " digest=" + Hex(digest) + " local=" + localNode);
            }
        }

        protected void RecvChallengeAck(int our_challenge)
        {
            byte[] her_digest = new byte[16];
            try
            {
                byte[] buf = Read2BytePackage();
                OtpInputStream ibuf = new OtpInputStream(buf, 0);
                int tag = ibuf.Read1();
                if (tag != ChallengeAck)
                    throw new IOException("Handshake protocol error");
                ibuf.ReadN(her_digest);
                byte[] our_digest = GenDigest(our_challenge, localNode.Cookie);
                if (!her_digest.SequenceEqual(our_digest))
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
                             + peer.Node + " digest=" + Hex(her_digest) + " local=" + localNode);
            }
        }

        protected void SendStatus(string status)
        {
            OtpOutputStream obuf = new OtpOutputStream();
            obuf.Write2BE(status.Length + 1);
            obuf.Write1(ChallengeStatus);
            obuf.Write(Encoding.GetEncoding("ISO-8859-1").GetBytes(status));

            obuf.WriteTo(socket.GetOutputStream());

            if (traceLevel >= handshakeThreshold)
            {
                log.Debug("-> " + "HANDSHAKE sendStatus" + " status="
                             + status + " local=" + localNode);
            }
        }

        protected void RecvStatus()
        {
            try
            {
                byte[] buf = Read2BytePackage();
                OtpInputStream ibuf = new OtpInputStream(buf, 0);
                int tag = ibuf.Read1();
                if (tag != ChallengeStatus)
                    throw new IOException("Handshake protocol error");
                byte[] tmpbuf = new byte[buf.Length - 1];
                ibuf.ReadN(tmpbuf);
                string status = OtpErlangString.FromEncoding(tmpbuf);

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
    }
}
