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
     * The System property OtpConnection.trace can be used to change the initial
     * trace level setting for all connections. Normally the initial trace level is
     * 0 and connections are not traced unless {@link #setTraceLevel
     * setTraceLevel()} is used to change the setting for a particular connection.
     * OtpConnection.trace can be used to turn on tracing by default for all
     * connections.
     */
    public abstract class AbstractConnection : ThreadBase
    {
        private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        protected const int headerLen = 2048; // more than enough

        protected const byte passThrough = 0x70;
        protected const byte version = 0x83;

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

        protected readonly object objRead = new object();
        protected readonly object objWrite = new object();
        protected IOtpTransport socket; // communication channel
        public bool Connected { get; protected set; }

        protected OtpLocalNode Local { get; set; }
        public OtpPeer Peer { get; private set; }
        public string Name => Peer.Node;

        protected bool cookieOk = false; // already checked the cookie for this connection
        protected bool sendCookie = true; // Send cookies in messages?

        private int traceLevel;
        public int TraceLevel
        {
            get => traceLevel;
            protected set => traceLevel = Math.Min(Math.Max(value, 0), 4);
        }
        
        protected static int defaultLevel = 0;
        protected static int sendThreshold = 1;
        protected static int ctrlThreshold = 2;
        protected static int handshakeThreshold = 3;

        static AbstractConnection()
        {
            XmlConfigurator.Configure();

            // trace this connection?
            string trace = ConfigurationManager.AppSettings["OtpConnection.trace"];
            try
            {
                if (trace != null)
                    defaultLevel = int.Parse(trace);
            }
            catch (FormatException)
            {
                defaultLevel = 0;
            }
        }

        /**
         * Accept an incoming connection from a remote node. Used by {@link
         * OtpSelf#accept() OtpSelf.accept()} to create a connection based on data
         * received when handshaking with the peer node, when the remote node is the
         * connection initiator.
         */
        protected AbstractConnection(OtpLocalNode self, IOtpTransport s)
            : base("accept", true)
        {
            TraceLevel = defaultLevel;
            Local = self;
            socket = s;

            if (TraceLevel >= handshakeThreshold)
                log.Debug("<- ACCEPT FROM " + s);

            Accept();
        }

        /**
         * Intiate and open a connection to a remote node.
         */
        protected AbstractConnection(OtpLocalNode self, OtpPeer other)
            : base("connect", true)
        {
            TraceLevel = defaultLevel;
            Peer = other;
            Local = self;

            // Locate peer
            if (Peer.Port == 0)
                throw new IOException("No remote node found - cannot connect");

            // compatible?
            if (Peer.Proto != self.Proto || self.DistHigh < Peer.DistLow || self.DistLow > Peer.DistHigh)
                throw new IOException("No common protocol found - cannot connect");

            // highest common version
            Peer.DistChoose = Math.Min(Peer.DistHigh, self.DistHigh);

            Connect();
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
            if (!Connected)
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
                header.WriteAtom(Local.Cookie);
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
         */
        protected void SendBuf(OtpErlangPid from, OtpErlangPid dest, OtpOutputStream payload)
        {
            if (!Connected)
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
                header.WriteAtom(Local.Cookie);
            else
                header.WriteAtom("");
            header.WriteAny(dest);

            // version for payload
            header.Write1(version);

            // fix up length in preamble
            header.Poke4BE(0, header.Length + payload.Length - 4);

            DoSend(header, payload);
        }

        /**
         * Send an auth error to peer because he sent a bad cookie. The auth error
         * uses his cookie (not revealing ours). This is just like send_reg
         * otherwise
         */
        protected void CookieError(OtpLocalNode local, OtpErlangAtom cookie)
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
                header.WriteAny(local.CreatePid()); // disposable pid
                header.WriteAtom(cookie.Value); // important: his cookie,
                // not mine...
                header.WriteAtom("auth");

                // version for payload
                header.Write1(version);

                // the payload

                // the no_auth message (copied from Erlang) Don't change this
                // (Erlang will crash)
                // {$gen_cast, {print, "~n** Unauthorized cookie ~w **~n",
                // [foo@aule]}}
                IOtpErlangObject[] msg = new IOtpErlangObject[2];
                IOtpErlangObject[] msgbody = new IOtpErlangObject[3];

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

            throw new OtpAuthException("Remote cookie not authorized: " + cookie.Value);
        }

        /**
         * Create a link between the local node and the specified process on the
         * remote node. If the link is still active when the remote process
         * terminates, an exit signal will be sent to this connection. Use
         * {@link #sendUnlink unlink()} to remove the link.
         */
        protected void SendLink(OtpErlangPid from, OtpErlangPid dest)
        {
            if (!Connected)
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
         */
        protected void SendUnlink(OtpErlangPid from, OtpErlangPid dest)
        {
            if (!Connected)
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

        /**
         * Send an exit signal to a remote process.
         * Used internally when "processes" terminate
         */
        protected void SendExit(OtpErlangPid from, OtpErlangPid dest, IOtpErlangObject reason)
        {
            SendExit(exitTag, from, dest, reason);
        }

        /**
         * Send an exit signal to a remote process.
         */
        protected void SendExit2(OtpErlangPid from, OtpErlangPid dest, IOtpErlangObject reason)
        {
            SendExit(exit2Tag, from, dest, reason);
        }

        /**
         * Send an exit signal to a remote process.
         * Used internally when "processes" terminate
         */
        private void SendExit(int tag, OtpErlangPid from, OtpErlangPid dest, IOtpErlangObject reason)
        {
            if (!Connected)
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
            if (!Connected)
            {
                Deliver(new IOException("Not connected"));
                return;
            }

            byte[] lbuf = new byte[4];
            OtpInputStream ibuf;
            IOtpErlangObject traceobj;
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
                    ReadSock(lbuf);
                    ibuf = new OtpInputStream(lbuf) { Flags = Local.Flags };
                    len = ibuf.Read4BE();

                    // received tick? send tock!
                    if (len == 0)
                    {
                        lock (objWrite)
                        {
                            socket.GetOutputStream().Write(tock, 0, tock.Length);
                            socket.GetOutputStream().Flush();
                        }

                        continue;
                    }

                    // got a real message (maybe) - read len bytes
                    byte[] tmpbuf = new byte[len];
                    ReadSock(tmpbuf);
                    ibuf = new OtpInputStream(tmpbuf) { Flags = Local.Flags };

                    if (ibuf.Read1() != passThrough)
                        continue;

                    // got a real message (really)
                    IOtpErlangObject reason = null;
                    OtpErlangAtom cookie = null;
                    IOtpErlangObject tmp = null;
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
                    if (!(head.ElementAt(0) is OtpErlangLong))
                        continue;

                    // lets see what kind of message this is
                    tag = (int)((OtpErlangLong)head.ElementAt(0)).LongValue();

                    switch (tag)
                    {
                        case sendTag:   // { SEND, Cookie, ToPid }
                        case sendTTTag: // { SEND, Cookie, ToPid, TraceToken }
                            if (!cookieOk)
                            {
                                // we only check this once, he can send us bad cookies later if he likes
                                if (!(head.ElementAt(1) is OtpErlangAtom))
                                    continue;

                                cookie = (OtpErlangAtom)head.ElementAt(1);
                                if (sendCookie)
                                {
                                    if (cookie.Value != Local.Cookie)
                                        CookieError(Local, cookie);
                                }
                                else
                                {
                                    if (cookie.Value != "")
                                        CookieError(Local, cookie);
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

                            to = (OtpErlangPid)head.ElementAt(2);

                            Deliver(new OtpMsg(to, ibuf));
                            break;

                        case regSendTag:   // { REG_SEND, FromPid, Cookie, ToName }
                        case regSendTTTag: // { REG_SEND, FromPid, Cookie, ToName, TraceToken }
                            if (!cookieOk)
                            {
                                // we only check this once, he can send us bad cookies later if he likes
                                if (!(head.ElementAt(2) is OtpErlangAtom))
                                    continue;

                                cookie = (OtpErlangAtom)head.ElementAt(2);
                                if (sendCookie)
                                {
                                    if (cookie.Value != Local.Cookie)
                                        CookieError(Local, cookie);
                                }
                                else
                                {
                                    if (cookie.Value != "")
                                        CookieError(Local, cookie);
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

                            from = (OtpErlangPid)head.ElementAt(1);
                            toName = (OtpErlangAtom)head.ElementAt(3);

                            Deliver(new OtpMsg(from, toName.Value, ibuf));
                            break;

                        case exitTag:  // { EXIT, FromPid, ToPid, Reason }
                        case exit2Tag: // { EXIT2, FromPid, ToPid, Reason }
                            if (head.ElementAt(3) == null)
                                continue;

                            if (traceLevel >= ctrlThreshold)
                                log.Debug("<- " + HeaderType(head) + " " + head);

                            from = (OtpErlangPid)head.ElementAt(1);
                            to = (OtpErlangPid)head.ElementAt(2);
                            reason = head.ElementAt(3);

                            Deliver(new OtpMsg(tag, from, to, reason));
                            break;

                        case exitTTTag:  // { EXIT, FromPid, ToPid, TraceToken, Reason }
                        case exit2TTTag: // { EXIT2, FromPid, ToPid, TraceToken, Reason }
                            // as above, but bifferent element number
                            if (head.ElementAt(4) == null)
                                continue;

                            if (traceLevel >= ctrlThreshold)
                                log.Debug("<- " + HeaderType(head) + " " + head);

                            from = (OtpErlangPid)head.ElementAt(1);
                            to = (OtpErlangPid)head.ElementAt(2);
                            reason = head.ElementAt(4);

                            Deliver(new OtpMsg(tag, from, to, reason));
                            break;

                        case linkTag:   // { LINK, FromPid, ToPid}
                        case unlinkTag: // { UNLINK, FromPid, ToPid}
                            if (traceLevel >= ctrlThreshold)
                                log.Debug("<- " + HeaderType(head) + " " + head);

                            from = (OtpErlangPid)head.ElementAt(1);
                            to = (OtpErlangPid)head.ElementAt(2);

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
                Deliver(new OtpExit("Remote is sending garbage"));

            }
            catch (OtpAuthException e)
            {
                Deliver(e);
            }
            catch (OtpDecodeException)
            {
                Deliver(new OtpExit("Remote is sending garbage"));
            }
            catch (IOException)
            {
                Deliver(new OtpExit("Remote has closed connection"));
            }
            finally
            {
                Close();
            }
        }



        /**
         * Close the connection to the remote node.
         */
        public virtual void Close()
        {
            Stop();
            Connected = false;
            lock (objRead)
            {
                try
                {
                    if (socket != null)
                    {
                        if (TraceLevel >= ctrlThreshold)
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

        // used by send and send_reg (message types with payload)
        protected void DoSend(OtpOutputStream header, OtpOutputStream payload)
        {
            lock (objWrite)
            {
                try
                {
                    if (TraceLevel >= sendThreshold)
                    {
                        // Need to decode header and output buffer to show trace
                        // message!
                        // First make OtpInputStream, then decode.
                        try
                        {
                            IOtpErlangObject h = header.GetOtpInputStream(5).ReadAny();
                            log.Debug("-> " + HeaderType(h) + " " + h);

                            IOtpErlangObject o = payload.GetOtpInputStream(0).ReadAny();
                            log.Debug("   " + o);
                        }
                        catch (OtpDecodeException e)
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
            lock (objWrite)
            {
                try
                {
                    if (TraceLevel >= ctrlThreshold)
                    {
                        try
                        {
                            IOtpErlangObject h = header.GetOtpInputStream(5).ReadAny();
                            log.Debug("-> " + HeaderType(h) + " " + h);
                        }
                        catch (OtpDecodeException e)
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

        protected string HeaderType(IOtpErlangObject h)
        {
            int tag = -1;

            if (h is OtpErlangTuple tuple)
                tag = (int)((OtpErlangLong)tuple.ElementAt(0)).LongValue();

            switch (tag)
            {
                case linkTag:        return "LINK";
                case sendTag:        return "SEND";
                case exitTag:        return "EXIT";
                case unlinkTag:      return "UNLINK";
                case regSendTag:     return "REG_SEND";
                case groupLeaderTag: return "GROUP_LEADER";
                case exit2Tag:       return "EXIT2";
                case sendTTTag:      return "SEND_TT";
                case exitTTTag:      return "EXIT_TT";
                case regSendTTTag:   return "REG_SEND_TT";
                case exit2TTTag:     return "EXIT2_TT";
            }

            return "(unknown type)";
        }

        /* this method now throws exception if we don't get full read */
        protected int ReadSock(byte[] b)
        {
            int got = 0;
            int len = b.Length;
            int i;
            Stream st = null;

            lock (objRead)
            {
                if (socket == null)
                    throw new IOException("expected " + len + " bytes, socket was closed");
                st = socket.GetInputStream();
            }

            while (got < len)
            {
                try
                {
                    i = st.Read(b, got, len - got);
                    if (i < 0)
                        throw new IOException("expected " + len + " bytes, got EOF after " + got + " bytes");

                    if (i == 0 && len != 0)
                        throw new IOException("Remote connection closed");

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

        protected void Accept()
        {
            Peer = new OtpPeer() { TransportFactory = Local.TransportFactory };
            int nameTag = RecvName();
            try
            {
                SendStatus("ok");
                int our_challenge = GenChallenge();
                SendChallenge(Peer.CapFlags, Local.CapFlags, our_challenge);
                RecvComplement(nameTag);
                int her_challenge = RecvChallengeReply(our_challenge);
                byte[] our_digest = GenDigest(her_challenge, Local.Cookie);
                SendChallengeAck(our_digest);
                Connected = true;
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
                string nn = Peer.Node;
                Close();
                throw new IOException("Error accepting connection from " + nn, e);
            }

            if (TraceLevel >= handshakeThreshold)
                log.Debug("<- MD5 ACCEPTED " + Peer.Host);
        }

        protected void Connect()
        {
            try
            {
                socket = Peer.CreateTransport(Peer.Host, Peer.Port);

                if (TraceLevel >= handshakeThreshold)
                    log.Debug("-> MD5 CONNECT TO " + Peer.Host + ":" + Peer.Port);

                int nameTag = SendName(Peer.DistChoose, Local.CapFlags, Local.Creation);
                RecvStatus();
                int her_challenge = RecvChallenge();
                byte[] our_digest = GenDigest(her_challenge, Local.Cookie);
                int our_challenge = GenChallenge();
                SendComplement(nameTag);
                SendChallengeReply(our_challenge, our_digest);
                RecvChallengeAck(our_challenge);
                Connected = true;
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

        // A better challenge?
        protected static int GenChallenge() => Guid.NewGuid().GetHashCode();

        // Used to debug print a message digest
        private static string Hex0(byte x)
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

        private static string Hex(byte[] b)
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
            string str = Local.Node;
            int nameTag = (dist == 5 ? 'n' : 'N');
            if (dist == 5)
            {
                obuf.Write2BE(1 + 2 + 4 + str.Length);
                obuf.Write1(nameTag);
                obuf.Write2BE(dist);
                obuf.Write4BE(aflags);
                obuf.Write(Encoding.GetEncoding("ISO-8859-1").GetBytes(str));
            }
            else
            {
                obuf.Write2BE(1 + 8 + 4 + 2 + str.Length);
                obuf.Write1(nameTag);
                obuf.Write8BE(aflags);
                obuf.Write4BE(creation);
                obuf.Write2BE(str.Length);
                obuf.Write(Encoding.GetEncoding("ISO-8859-1").GetBytes(str));
            }

            obuf.WriteTo(socket.GetOutputStream());

            if (TraceLevel >= handshakeThreshold)
                log.Debug("-> " + "HANDSHAKE sendName" + " flags=" + aflags + " dist=" + dist + " local=" + Local);

            return nameTag;
        }

        protected void SendComplement(int nameTag)
        {

            if (nameTag == 'n' && (Peer.CapFlags & AbstractNode.dFlagHandshake23) != 0)
            {
                OtpOutputStream obuf = new OtpOutputStream();
                obuf.Write2BE(1 + 4 + 4);
                obuf.Write1('c');
                int flagsHigh = (int)(Local.CapFlags >> 32);
                obuf.Write4BE(flagsHigh);
                obuf.Write4BE(Local.Creation);

                obuf.WriteTo(socket.GetOutputStream());

                if (TraceLevel >= handshakeThreshold)
                    log.Debug("-> " + "HANDSHAKE sendComplement" + " flagsHigh=" + flagsHigh + " creation=" + Local.Creation);
            }
        }

        protected void SendChallenge(long her_flags, long our_flags, int challenge)
        {
            OtpOutputStream obuf = new OtpOutputStream();
            string str = Local.Node;
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
                obuf.Write4BE(Local.Creation);
                obuf.Write2BE(str.Length);
                obuf.Write(Encoding.GetEncoding("ISO-8859-1").GetBytes(str));
            }

            obuf.WriteTo(socket.GetOutputStream());

            if (TraceLevel >= handshakeThreshold)
                log.Debug("-> " + "HANDSHAKE sendChallenge" + " flags=" + our_flags + " challenge=" + challenge + " local=" + Local);
        }

        protected byte[] Read2BytePackage()
        {
            byte[] lbuf = new byte[2];
            byte[] tmpbuf;

            ReadSock(lbuf);
            OtpInputStream ibuf = new OtpInputStream(lbuf);
            int len = ibuf.Read2BE();
            tmpbuf = new byte[len];
            ReadSock(tmpbuf);
            return tmpbuf;
        }

        protected int RecvName()
        {
            int nameTag;
            string hisname;

            try
            {
                byte[] tmpbuf = Read2BytePackage();
                OtpInputStream ibuf = new OtpInputStream(tmpbuf);
                byte[] tmpname;
                int len = tmpbuf.Length;
                nameTag = ibuf.Read1();
                switch (nameTag)
                {
                    case 'n':
                        Peer.DistLow = Peer.DistHigh = ibuf.Read2BE();
                        if (Peer.DistLow != 5)
                            throw new IOException("Invalid handshake version");
                        Peer.CapFlags = ibuf.Read4BE();
                        tmpname = new byte[len - 7];
                        ibuf.ReadN(tmpname);
                        hisname = OtpErlangString.FromEncoding(tmpname);
                        break;
                    case 'N':
                        Peer.DistLow = Peer.DistHigh = 6;
                        Peer.CapFlags = ibuf.Read8BE();
                        if ((Peer.CapFlags & AbstractNode.dFlagHandshake23) == 0)
                            throw new IOException("Missing DFLAG_HANDSHAKE_23");
                        Peer.Creation = ibuf.Read4BE();
                        int namelen = ibuf.Read2BE();
                        tmpname = new byte[namelen];
                        ibuf.ReadN(tmpname);
                        hisname = OtpErlangString.FromEncoding(tmpname);
                        break;
                    default:
                        throw new IOException("Unknown remote node type");
                }

                if ((Peer.CapFlags & AbstractNode.dFlagExtendedReferences) == 0)
                    throw new IOException("Handshake failed - peer cannot handle extended references");

                if ((Peer.CapFlags & AbstractNode.dFlagExtendedPidsPorts) == 0)
                    throw new IOException("Handshake failed - peer cannot handle extended pids and ports");
            }
            catch (OtpDecodeException)
            {
                throw new IOException("Handshake failed - not enough data");
            }

            Peer.Node = hisname;
            if (Peer.Alive == null || Peer.Host == null)
                throw new IOException("Handshake failed - peer name invalid: " + hisname);

            if (TraceLevel >= handshakeThreshold)
                log.Debug("<- " + "HANDSHAKE" + " ntype=" + Peer.Type + " dist=" + Peer.DistHigh + " remote=" + Peer);

            return nameTag;
        }

        protected int RecvChallenge()
        {
            int challenge;

            try
            {
                byte[] buf = Read2BytePackage();
                OtpInputStream ibuf = new OtpInputStream(buf);
                int namelen;
                switch (ibuf.Read1())
                {
                    case 'n':
                        if (Peer.DistChoose != 5)
                            throw new IOException("Old challenge wrong version");
                        Peer.DistLow = Peer.DistHigh = ibuf.Read2BE();
                        Peer.CapFlags = ibuf.Read4BE();
                        if ((Peer.CapFlags & AbstractNode.dFlagHandshake23) != 0)
                            throw new IOException("Old challenge unexpected DFLAG_HANDHAKE_23");
                        challenge = ibuf.Read4BE();
                        namelen = buf.Length - (1 + 2 + 4 + 4);
                        break;

                    case 'N':
                        Peer.DistLow = Peer.DistHigh = Peer.DistChoose = 6;
                        Peer.CapFlags = ibuf.Read8BE();
                        if ((Peer.CapFlags & AbstractNode.dFlagHandshake23) == 0)
                            throw new IOException("New challenge missing DFLAG_HANDHAKE_23");
                        challenge = ibuf.Read4BE();
                        Peer.Creation = ibuf.Read4BE();
                        namelen = ibuf.Read2BE();
                        break;

                    default:
                        throw new IOException("Unexpected peer type");
                }

                byte[] tmpname = new byte[namelen];
                ibuf.ReadN(tmpname);
                string hisname = OtpErlangString.FromEncoding(tmpname);
                if (!hisname.Equals(Peer.Node))
                    throw new IOException("Handshake failed - peer has wrong name: " + hisname);

                if ((Peer.CapFlags & AbstractNode.dFlagExtendedReferences) == 0)
                    throw new IOException("Handshake failed - peer cannot handle extended references");

                if ((Peer.CapFlags & AbstractNode.dFlagExtendedPidsPorts) == 0)
                    throw new IOException("Handshake failed - peer cannot handle extended pids and ports");
            }
            catch (OtpDecodeException e)
            {
                throw new IOException("Handshake failed - not enough data", e);
            }

            if (TraceLevel >= handshakeThreshold)
                log.Debug("<- " + "HANDSHAKE recvChallenge" + " from=" + Peer.Node + " challenge=" + challenge + " local=" + Local);

            return challenge;
        }

        protected void RecvComplement(int send_name_tag)
        {

            if (send_name_tag == 'n' && (Peer.CapFlags & AbstractNode.dFlagHandshake23) != 0)
            {
                try
                {
                    byte[] tmpbuf = Read2BytePackage();
                    OtpInputStream ibuf = new OtpInputStream(tmpbuf);
                    if (ibuf.Read1() != 'c')
                        throw new IOException("Not a complement tag");

                    long flagsHigh = ibuf.Read4BE();
                    Peer.CapFlags |= flagsHigh << 32;
                    Peer.Creation = ibuf.Read4BE();

                }
                catch (OtpDecodeException e)
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

            if (TraceLevel >= handshakeThreshold)
            {
                log.Debug("-> " + "HANDSHAKE sendChallengeReply"
                             + " challenge=" + challenge + " digest=" + Hex(digest)
                             + " local=" + Local);
            }
        }

        protected int RecvChallengeReply(int our_challenge)
        {
            int challenge;
            byte[] her_digest = new byte[16];

            try
            {
                byte[] buf = Read2BytePackage();
                OtpInputStream ibuf = new OtpInputStream(buf);
                int tag = ibuf.Read1();
                if (tag != ChallengeReply)
                    throw new IOException("Handshake protocol error");
                challenge = ibuf.Read4BE();
                ibuf.ReadN(her_digest);
                byte[] our_digest = GenDigest(our_challenge, Local.Cookie);
                if (!her_digest.SequenceEqual(our_digest))
                    throw new OtpAuthException("Peer authentication error.");
            }
            catch (OtpDecodeException e)
            {
                throw new IOException("Handshake failed - not enough data", e);
            }

            if (TraceLevel >= handshakeThreshold)
            {
                log.Debug("<- " + "HANDSHAKE recvChallengeReply"
                             + " from=" + Peer.Node + " challenge=" + challenge
                             + " digest=" + Hex(her_digest) + " local=" + Local);
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

            if (TraceLevel >= handshakeThreshold)
            {
                log.Debug("-> " + "HANDSHAKE sendChallengeAck"
                             + " digest=" + Hex(digest) + " local=" + Local);
            }
        }

        protected void RecvChallengeAck(int our_challenge)
        {
            byte[] her_digest = new byte[16];
            try
            {
                byte[] buf = Read2BytePackage();
                OtpInputStream ibuf = new OtpInputStream(buf);
                int tag = ibuf.Read1();
                if (tag != ChallengeAck)
                    throw new IOException("Handshake protocol error");
                ibuf.ReadN(her_digest);
                byte[] our_digest = GenDigest(our_challenge, Local.Cookie);
                if (!her_digest.SequenceEqual(our_digest))
                    throw new OtpAuthException("Peer authentication error.");
            }
            catch (OtpDecodeException e)
            {
                throw new IOException("Handshake failed - not enough data", e);
            }
            catch (Exception e)
            {
                log.Error("Peer authentication error", e);
                throw new OtpAuthException("Peer authentication error.", e);
            }

            if (TraceLevel >= handshakeThreshold)
            {
                log.Debug("<- " + "HANDSHAKE recvChallengeAck" + " from="
                             + Peer.Node + " digest=" + Hex(her_digest) + " local=" + Local);
            }
        }

        protected void SendStatus(string status)
        {
            OtpOutputStream obuf = new OtpOutputStream();
            obuf.Write2BE(status.Length + 1);
            obuf.Write1(ChallengeStatus);
            obuf.Write(Encoding.GetEncoding("ISO-8859-1").GetBytes(status));

            obuf.WriteTo(socket.GetOutputStream());

            if (TraceLevel >= handshakeThreshold)
            {
                log.Debug("-> " + "HANDSHAKE sendStatus" + " status="
                             + status + " local=" + Local);
            }
        }

        protected void RecvStatus()
        {
            try
            {
                byte[] buf = Read2BytePackage();
                OtpInputStream ibuf = new OtpInputStream(buf);
                int tag = ibuf.Read1();
                if (tag != ChallengeStatus)
                    throw new IOException("Handshake protocol error");
                byte[] tmpbuf = new byte[buf.Length - 1];
                ibuf.ReadN(tmpbuf);
                string status = OtpErlangString.FromEncoding(tmpbuf);

                if (status.CompareTo("ok") != 0)
                    throw new IOException("Peer replied with status '" + status + "' instead of 'ok'");
            }
            catch (OtpDecodeException e)
            {
                throw new IOException("Handshake failed - not enough data", e);
            }

            if (TraceLevel >= handshakeThreshold)
                log.Debug("<- " + "HANDSHAKE recvStatus (ok)" + " local=" + Local);
        }
    }
}
