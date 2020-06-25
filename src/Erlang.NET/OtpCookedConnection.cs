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
     * <p>
     * Maintains a connection between a Java process and a remote Erlang, Java or C
     * node. The object maintains connection state and allows data to be sent to and
     * received from the peer.
     * </p>
     * 
     * <p>
     * Once a connection is established between the local node and a remote node,
     * the connection object can be used to send and receive messages between the
     * nodes.
     * </p>
     * 
     * <p>
     * The various receive methods are all blocking and will return only when a
     * valid message has been received or an exception is raised.
     * </p>
     * 
     * <p>
     * If an exception occurs in any of the methods in this class, the connection
     * will be closed and must be reopened in order to resume communication with the
     * peer.
     * </p>
     * 
     * <p>
     * The message delivery methods in this class deliver directly to
     * {@link OtpMbox mailboxes} in the {@link OtpNode OtpNode} class.
     * </p>
     * 
     * <p>
     * It is not possible to create an instance of this class directly.
     * OtpCookedConnection objects are created as needed by the underlying mailbox
     * mechanism.
     * </p>
     */
    public class OtpCookedConnection : AbstractConnection
    {
        private readonly object lockObj = new object();
        protected OtpNode self;

        /*
         * The connection needs to know which local pids have links that pass
         * through here, so that they can be notified in case of connection failure
         */
        protected Links links = new Links(25);

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
        internal OtpCookedConnection(OtpNode self, IOtpTransport s)
            : base(self, s)
        {
            this.self = self;
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
        internal OtpCookedConnection(OtpNode self, OtpPeer other)
            : base(self, other)
        {
            this.self = self;
            Start();
        }

        // pass the error to the node
        public override void Deliver(Exception e)
        {
            self.DeliverError(this, e);
            return;
        }

        /*
         * pass the message to the node for final delivery. Note that the connection
         * itself needs to know about links (in case of connection failure), so we
         * snoop for link/unlink too here.
         */
        public override void Deliver(OtpMsg msg)
        {
            bool delivered = self.Deliver(msg);

            switch (msg.Type())
            {
                case OtpMsg.linkTag:
                    if (delivered)
                    {
                        links.AddLink(msg.GetRecipientPid(), msg.GetSenderPid());
                        break;
                    }

                    try
                    {
                        // no such pid - send exit to sender
                        base.SendExit(msg.GetRecipientPid(), msg.GetSenderPid(), new OtpErlangAtom("noproc"));
                    }
                    catch (IOException)
                    {
                    }
                    break;

                case OtpMsg.unlinkTag:
                case OtpMsg.exitTag:
                    links.RemoveLink(msg.GetRecipientPid(), msg.GetSenderPid());
                    break;

                case OtpMsg.exit2Tag:
                    break;
            }

            return;
        }

        /*
         * send to pid
         */
        public void Send(OtpErlangPid from, OtpErlangPid dest, IOtpErlangObject msg)
        {
            // encode and send the message
            SendBuf(from, dest, new OtpOutputStream(msg));
        }

        /*
         * send to remote name dest is recipient's registered name, the nodename is
         * implied by the choice of connection.
         */
        public void Send(OtpErlangPid from, string dest, IOtpErlangObject msg)
        {
            // encode and send the message
            SendBuf(from, dest, new OtpOutputStream(msg));
        }

        public override void Close()
        {
            try
            {
                base.Close();
            }
            finally
            {
                BreakLinks();
            }
        }

        /*
         * this one called by dying/killed process
         */
        public void Exit(OtpErlangPid from, OtpErlangPid to, IOtpErlangObject reason)
        {
            try
            {
                base.SendExit(from, to, reason);
            }
            catch (Exception)
            {
            }
        }

        /*
         * this one called explicitely by user code => use exit2
         */
        public void Exit2(OtpErlangPid from, OtpErlangPid to, IOtpErlangObject reason)
        {
            try
            {
                base.SendExit2(from, to, reason);
            }
            catch (Exception)
            {
            }
        }

        /*
         * snoop for outgoing links and update own table
         */
        public void Link(OtpErlangPid from, OtpErlangPid to)
        {
            lock (lockObj)
            {
                try
                {
                    base.SendLink(from, to);
                    links.AddLink(from, to);
                }
                catch (IOException)
                {
                    throw new OtpExit("noproc", to);
                }
            }
        }

        /*
         * snoop for outgoing unlinks and update own table
         */
        public void Unlink(OtpErlangPid from, OtpErlangPid to)
        {
            lock (lockObj)
            {
                links.RemoveLink(from, to);
                try
                {
                    base.SendUnlink(from, to);
                }
                catch (IOException)
                {
                }
            }
        }

        /*
         * When the connection fails - send exit to all local pids with links
         * through this connection
         */
        private void BreakLinks()
        {
            lock (lockObj)
            {
                foreach (Link link in links.ClearLinks())
                {
                    // send exit "from" remote pids to local ones
                    self.Deliver(new OtpMsg(OtpMsg.exitTag, link.Remote, link.Local, new OtpErlangAtom("noconnection")));
                }
            }
        }
    }
}
