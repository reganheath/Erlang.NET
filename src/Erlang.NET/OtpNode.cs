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
using System.Collections.Generic;
using System.IO;

namespace Erlang.NET
{
    /**
     * <p>
     * Represents a local OTP node. This class is used when you do not wish to
     * manage connections yourself - outgoing connections are established as needed,
     * and incoming connections accepted automatically. This class supports the use
     * of a mailbox API for communication, while management of the underlying
     * communication mechanism is automatic and hidden from the application
     * programmer.
     * </p>
     * 
     * <p>
     * Once an instance of this class has been created, obtain one or more mailboxes
     * in order to send or receive messages. The first message sent to a given node
     * will cause a connection to be set up to that node. Any messages received will
     * be delivered to the appropriate mailboxes.
     * </p>
     * 
     * <p>
     * To shut down the node, call {@link #close close()}. This will prevent the
     * node from accepting additional connections and it will cause all existing
     * connections to be closed. Any unread messages in existing mailboxes can still
     * be read, however no new messages will be delivered to the mailboxes.
     * </p>
     * 
     * <p>
     * Note that the use of this class requires that Epmd (Erlang Port Mapper
     * Daemon) is running on each cooperating host. This class does not start Epmd
     * automatically as Erlang does, you must start it manually or through some
     * other means. See the Erlang documentation for more information about this.
     * </p>
     */
    public class OtpNode : OtpLocalNode
    {
        private readonly object lockObj = new object();

        // thread to manage incoming connections
        private Acceptor acceptor = null;

        // thread to schedule actors
        private OtpActorSched sched = null;

        // keep track of all mailboxes
        private Mailboxes mboxes = null;

        // handle status changes
        private OtpNodeStatus handler;

        public Dictionary<string, OtpCookedConnection> Connections { get; private set; } = null;

        public new int Flags { get; set; } = 0;

        /**
         * <p>
         * Create a node using the default cookie. The default cookie is found by
         * reading the first line of the .erlang.cookie file in the user's home
         * directory. The home directory is obtained from the System property
         * "user.home".
         * </p>
         * 
         * <p>
         * If the file does not exist, an empty string is used. This method makes no
         * attempt to create the file.
         * </p>
         * 
         * @param node
         *            the name of this node.
         * 
         * @exception IOException
         *                if communication could not be initialized.
         * 
         */
        public OtpNode(string node)
            : base(node)
        {
            Init(0);
        }

        public OtpNode(string node, OtpTransportFactory transportFactory)
            : base(node, transportFactory)
        {
            Init(0);
        }

        /**
         * Create a node.
         * 
         * @param node
         *            the name of this node.
         * 
         * @param cookie
         *            the authorization cookie that will be used by this node when
         *            it communicates with other nodes.
         * 
         * @exception IOException
         *                if communication could not be initialized.
         * 
         */
        public OtpNode(string node, string cookie)
            : base(node, cookie)
        {
            Init(0);
        }

        public OtpNode(string node, string cookie, OtpTransportFactory transportFactory)
            : base(node, cookie, transportFactory)
        {
            Init(0);
        }

        /**
         * Create a node.
         * 
         * @param node
         *            the name of this node.
         * 
         * @param cookie
         *            the authorization cookie that will be used by this node when
         *            it communicates with other nodes.
         * 
         * @param port
         *            the port number you wish to use for incoming connections.
         *            Specifying 0 lets the system choose an available port.
         * 
         * @exception IOException
         *                if communication could not be initialized.
         * 
         */
        public OtpNode(string node, string cookie, int port, OtpTransportFactory transportFactory)
            : base(node, cookie, transportFactory)
        {
            Init(port);
        }

        private void Init(int port)
        {
            Connections = new Dictionary<string, OtpCookedConnection>();

            sched = new OtpActorSched();
            mboxes = new Mailboxes(this, sched);
            acceptor = new Acceptor(this, port);
        }

        /**
         * Close the node. Unpublish the node from Epmd (preventing new connections)
         * and close all existing connections.
         */
        public void Close()
        {
            lock (lockObj)
            {
                acceptor.quit();
                mboxes.clear();

                lock (Connections)
                {
                    OtpCookedConnection[] conns = new OtpCookedConnection[Connections.Count];
                    int i = 0;
                    foreach (OtpCookedConnection conn in Connections.Values)
                        conns[i++] = conn;
                    Connections.Clear();
                    foreach (OtpCookedConnection conn in conns)
                        conn.Close();
                }
            }
        }

        /**
         * Create an unnamed {@link OtpMbox mailbox} that can be used to send and
         * receive messages with other, similar mailboxes and with Erlang processes.
         * Messages can be sent to this mailbox by using its associated
         * {@link OtpMbox#self() pid}.
         * 
         * @return a mailbox.
         */
        public OtpMbox CreateMbox(bool sync)
        {
            return mboxes.create(sync);
        }

        /**
         * Close the specified mailbox with reason 'normal'.
         * 
         * @param mbox
         *            the mailbox to close.
         * 
         *            <p>
         *            After this operation, the mailbox will no longer be able to
         *            receive messages. Any delivered but as yet unretrieved
         *            messages can still be retrieved however.
         *            </p>
         * 
         *            <p>
         *            If there are links from the mailbox to other
         *            {@link OtpErlangPid pids}, they will be broken when this
         *            method is called and exit signals with reason 'normal' will be
         *            sent.
         *            </p>
         * 
         */
        public void CloseMbox(OtpMbox mbox)
        {
            CloseMbox(mbox, new OtpErlangAtom("normal"));
        }

        /**
         * Close the specified mailbox with the given reason.
         * 
         * @param mbox
         *            the mailbox to close.
         * @param reason
         *            an Erlang term describing the reason for the termination.
         * 
         *            <p>
         *            After this operation, the mailbox will no longer be able to
         *            receive messages. Any delivered but as yet unretrieved
         *            messages can still be retrieved however.
         *            </p>
         * 
         *            <p>
         *            If there are links from the mailbox to other
         *            {@link OtpErlangPid pids}, they will be broken when this
         *            method is called and exit signals with the given reason will
         *            be sent.
         *            </p>
         * 
         */
        public void CloseMbox(OtpMbox mbox, OtpErlangObject reason)
        {
            if (mbox != null)
            {
                mboxes.remove(mbox);
                mbox.Name = null;
                mbox.BreakLinks(reason);
            }
        }

        /**
         * Create an named mailbox that can be used to send and receive messages
         * with other, similar mailboxes and with Erlang processes. Messages can be
         * sent to this mailbox by using its registered name or the associated
         * {@link OtpMbox#self pid}.
         * 
         * @param name
         *            a name to register for this mailbox. The name must be unique
         *            within this OtpNode.
         * 
         * @return a mailbox, or null if the name was already in use.
         * 
         */
        public OtpMbox CreateMbox(string name, bool sync)
        {
            return mboxes.create(name, sync);
        }

        /**
         * <p>
         * Register or remove a name for the given mailbox. Registering a name for a
         * mailbox enables others to send messages without knowing the
         * {@link OtpErlangPid pid} of the mailbox. A mailbox can have at most one
         * name; if the mailbox already had a name, calling this method will
         * supercede that name.
         * </p>
         * 
         * @param name
         *            the name to register for the mailbox. Specify null to
         *            unregister the existing name from this mailbox.
         * 
         * @param mbox
         *            the mailbox to associate with the name.
         * 
         * @return true if the name was available, or false otherwise.
         */
        public bool RegisterName(string name, OtpMbox mbox)
        {
            return mboxes.register(name, mbox);
        }

        /**
         * Get a list of all known registered names on this node.
         * 
         * @return an array of Strings, containins all known registered names on
         *         this node.
         */
        public string[] Names() => mboxes.names();

        /**
         * Determine the {@link OtpErlangPid pid} corresponding to a registered name
         * on this node.
         * 
         * @return the {@link OtpErlangPid pid} corresponding to the registered
         *         name, or null if the name is not known on this node.
         */
        public OtpErlangPid WhereIs(string name)
        {
            OtpMbox m = mboxes.get(name);
            if (m != null)
                return m.Self;
            return null;
        }

        /**
         * Register interest in certain system events. The {@link OtpNodeStatus
         * OtpNodeStatus} handler object contains callback methods, that will be
         * called when certain events occur.
         * 
         * @param handler
         *            the callback object to register. To clear the handler, specify
         *            null as the handler to use.
         * 
         */
        public void RegisterStatusHandler(OtpNodeStatus handler)
        {
            lock (lockObj)
                this.handler = handler;
        }

        /**
         * <p>
         * Determine if another node is alive. This method has the side effect of
         * setting up a connection to the remote node (if possible). Only a single
         * outgoing message is sent; the timeout is how long to wait for a response.
         * </p>
         * 
         * <p>
         * Only a single attempt is made to connect to the remote node, so for
         * example it is not possible to specify an extremely long timeout and
         * expect to be notified when the node eventually comes up. If you wish to
         * wait for a remote node to be started, the following construction may be
         * useful:
         * </p>
         * 
         * <pre>
         * // ping every 2 seconds until positive response
         * while (!me.ping(him, 2000))
         *     ;
         * </pre>
         * 
         * @param node
         *            the name of the node to ping.
         * 
         * @param timeout
         *            the time, in milliseconds, to wait for response before
         *            returning false.
         * 
         * @return true if the node was alive and the correct ping response was
         *         returned. false if the correct response was not returned on time.
         */
        /*
         * internal info about the message formats...
         * 
         * the request: -> REG_SEND {6,#Pid<bingo@aule.1.0>,'',net_kernel}
         * {'$gen_call',{#Pid<bingo@aule.1.0>,#Ref<bingo@aule.2>},{is_auth,bingo@aule}}
         * 
         * the reply: <- SEND {2,'',#Pid<bingo@aule.1.0>} {#Ref<bingo@aule.2>,yes}
         */
        public bool Ping(string node, long timeout)
        {
            if (node.Equals(this.Node))
            {
                return true;
            }
            else if (node.IndexOf('@', 0) < 0 && node.Equals(this.Node.Substring(0, this.Node.IndexOf('@', 0))))
            {
                return true;
            }

            // other node
            OtpMbox mbox = null;
            try
            {
                mbox = CreateMbox(true);
                mbox.Send("net_kernel", node, GetPingTuple(mbox));
                OtpErlangObject reply = mbox.Receive(timeout);
                OtpErlangTuple t = (OtpErlangTuple)reply;
                OtpErlangAtom a = (OtpErlangAtom)t.elementAt(1);
                return "yes".Equals(a.atomValue());
            }
            catch (Exception)
            {
            }
            finally
            {
                CloseMbox(mbox);
            }
            return false;
        }

        /* create the outgoing ping message */
        private OtpErlangTuple GetPingTuple(OtpMbox mbox)
        {
            OtpErlangObject[] ping = new OtpErlangObject[3];
            OtpErlangObject[] pid = new OtpErlangObject[2];
            OtpErlangObject[] node = new OtpErlangObject[2];

            pid[0] = mbox.Self;
            pid[1] = createRef();

            node[0] = new OtpErlangAtom("is_auth");
            node[1] = new OtpErlangAtom(Node);

            ping[0] = new OtpErlangAtom("$gen_call");
            ping[1] = new OtpErlangTuple(pid);
            ping[2] = new OtpErlangTuple(node);

            return new OtpErlangTuple(ping);
        }

        /*
         * this method simulates net_kernel only for the purpose of replying to
         * pings.
         */
        private bool NetKernel(OtpMsg m)
        {
            OtpMbox mbox = null;
            try
            {
                OtpErlangTuple t = (OtpErlangTuple)m.getMsg();
                OtpErlangTuple req = (OtpErlangTuple)t.elementAt(1); // actual
                // request

                OtpErlangPid pid = (OtpErlangPid)req.elementAt(0); // originating
                // pid

                OtpErlangObject[] pong = new OtpErlangObject[2];
                pong[0] = req.elementAt(1); // his #Ref
                pong[1] = new OtpErlangAtom("yes");

                mbox = CreateMbox(true);
                mbox.Send(pid, new OtpErlangTuple(pong));
                return true;
            }
            catch (Exception)
            {
            }
            finally
            {
                CloseMbox(mbox);
            }
            return false;
        }

        /*
         * OtpCookedConnection delivers messages here return true if message was
         * delivered successfully, or false otherwise.
         */
        public bool Deliver(OtpMsg m)
        {
            OtpMbox mbox = null;

            try
            {
                int t = m.type();

                if (t == OtpMsg.regSendTag)
                {
                    string name = m.getRecipientName();
                    /* special case for netKernel requests */
                    if (name.Equals("net_kernel"))
                    {
                        return NetKernel(m);
                    }
                    else
                    {
                        mbox = mboxes.get(name);
                    }
                }
                else
                {
                    mbox = mboxes.get(m.getRecipientPid());
                }

                if (mbox == null)
                {
                    return false;
                }
                mbox.Deliver(m);
            }
            catch (Exception)
            {
                return false;
            }

            return true;
        }

        /*
         * OtpCookedConnection delivers errors here, we send them on to the handler
         * specified by the application
         */
        public void DeliverError(OtpCookedConnection conn, Exception e)
        {
            RemoveConnection(conn);
            RemoteStatus(conn.Name, false, e);
        }

        public void React(OtpActor actor)
        {
            sched.React(actor);
        }

        /*
         * find or create a connection to the given node
         */
        public OtpCookedConnection GetConnection(string node)
        {
            OtpCookedConnection conn = null;

            lock (Connections)
            {
                // first just try looking up the name as-is

                if (Connections.ContainsKey(node))
                    return Connections[node];

                // in case node had no '@' add localhost info and try again
                OtpPeer peer = new OtpPeer(node);

                if (Connections.ContainsKey(peer.Node))
                    return Connections[peer.Node];

                try
                {
                    conn = new OtpCookedConnection(this, peer);
                    conn.Flags = Flags;
                    AddConnection(conn);
                }
                catch (Exception e)
                {
                    /* false = outgoing */
                    ConnAttempt(peer.Node, false, e);
                }

                return conn;
            }
        }

        void AddConnection(OtpCookedConnection conn)
        {
            if (conn != null && conn.Name != null)
            {
                Connections.Add(conn.Name, conn);
                RemoteStatus(conn.Name, true, null);
            }
        }

        private void RemoveConnection(OtpCookedConnection conn)
        {
            if (conn != null && conn.Name != null)
                Connections.Remove(conn.Name);
        }

        /* use these wrappers to call handler functions */
        private void RemoteStatus(string node, bool up, object info)
        {
            lock (lockObj)
            {
                if (handler == null)
                    return;

                try { handler.RemoteStatus(node, up, info); }
                catch (Exception) { }
            }
        }

        internal void LocalStatus(string node, bool up, object info)
        {
            lock (lockObj)
            {
                if (handler == null)
                    return;

                try { handler.LocalStatus(node, up, info); }
                catch (Exception) { }
            }
        }

        internal void ConnAttempt(string node, bool incoming, object info)
        {
            lock (lockObj)
            {
                if (handler == null)
                    return;

                try { handler.ConnAttempt(node, incoming, info); }
                catch (Exception) { }
            }
        }

        /*
         * this class used to wrap the mailbox hashtables so we can use weak
         * references
         */
        public class Mailboxes
        {
            private readonly object lockObj = new object();
            private readonly OtpNode node;
            private readonly OtpActorSched sched;

            // mbox pids here
            private Dictionary<OtpErlangPid, WeakReference> byPid = null;
            // mbox names here
            private Dictionary<string, WeakReference> byName = null;

            public Mailboxes(OtpNode node, OtpActorSched sched)
            {
                this.node = node;
                this.sched = sched;
                byPid = new Dictionary<OtpErlangPid, WeakReference>();
                byName = new Dictionary<string, WeakReference>();
            }

            public OtpMbox create(string name, bool sync)
            {
                OtpMbox m = null;

                lock (lockObj)
                {
                    if (get(name) != null)
                    {
                        return null;
                    }
                    OtpErlangPid pid = node.createPid();
                    m = sync ? new OtpMbox(node, pid, name) : new OtpActorMbox(sched, node, pid, name);
                    byPid.Add(pid, new WeakReference(m));
                    byName.Add(name, new WeakReference(m));
                }
                return m;
            }

            public OtpMbox create(bool sync)
            {
                OtpErlangPid pid = node.createPid();
                OtpMbox m = sync ? new OtpMbox(node, pid) : new OtpActorMbox(sched, node, pid);
                lock (lockObj)
                {
                    byPid.Add(pid, new WeakReference(m));
                }
                return m;
            }

            public void clear()
            {
                lock (lockObj)
                {
                    byPid.Clear();
                    byName.Clear();
                }
            }

            public string[] names()
            {
                string[] allnames = null;

                lock (lockObj)
                {
                    int n = byName.Count;
                    allnames = new string[n];

                    int i = 0;
                    foreach (string key in byName.Keys)
                    {
                        allnames[i++] = key;
                    }
                }
                return allnames;
            }

            public bool register(string name, OtpMbox mbox)
            {
                lock (lockObj)
                {
                    if (name == null)
                    {
                        if (mbox.Name != null)
                        {
                            byName.Remove(mbox.Name);
                            mbox.Name = null;
                        }
                    }
                    else
                    {
                        if (get(name) != null)
                        {
                            return false;
                        }
                        byName.Add(name, new WeakReference(mbox));
                        mbox.Name = name;
                    }
                }
                return true;
            }

            /*
             * look up a mailbox based on its name. If the mailbox has gone out of
             * scope we also remove the reference from the hashtable so we don't
             * find it again.
             */
            public OtpMbox get(string name)
            {
                lock (lockObj)
                {
                    if (byName.ContainsKey(name))
                    {
                        WeakReference wr = byName[name];
                        OtpMbox m = (OtpMbox)wr.Target;

                        if (m != null)
                        {
                            return m;
                        }
                        byName.Remove(name);
                    }
                    return null;
                }
            }

            /*
             * look up a mailbox based on its pid. If the mailbox has gone out of
             * scope we also remove the reference from the hashtable so we don't
             * find it again.
             */
            public OtpMbox get(OtpErlangPid pid)
            {
                lock (lockObj)
                {
                    if (byPid.ContainsKey(pid))
                    {
                        WeakReference wr = byPid[pid];
                        OtpMbox m = (OtpMbox)wr.Target;

                        if (m != null)
                        {
                            return m;
                        }
                        byPid.Remove(pid);
                    }
                    return null;
                }
            }

            public void remove(OtpMbox mbox)
            {
                lock (lockObj)
                {
                    byPid.Remove(mbox.Self);
                    if (mbox.Name != null)
                    {
                        byName.Remove(mbox.Name);
                    }
                }
            }
        }

        /*
         * this thread simply listens for incoming connections
         */
        public class Acceptor : ThreadBase
        {
            private readonly OtpNode node;
            private readonly OtpServerTransport sock;
            private readonly int acceptorPort;
            private volatile bool done = false;

            public Acceptor(OtpNode node, int port)
                : base("OtpNode.Acceptor", true)
            {
                this.node = node;

                sock = node.createServerTransport(port);
                this.acceptorPort = sock.getLocalPort();
                node.port = this.acceptorPort;
                publishPort();
                base.Start();
            }

            private bool publishPort()
            {
                if (node.getEpmd() != null)
                    return false; // already published
                OtpEpmd.publishPort(node);
                return true;
            }

            private void unPublishPort()
            {
                // unregister with epmd
                OtpEpmd.unPublishPort(node);

                // close the local descriptor (if we have one)
                closeSock(node.getEpmd());
                node.setEpmd(null);
            }

            public void quit()
            {
                unPublishPort();
                done = true;
                closeSock(sock);
                node.LocalStatus(node.Node, false, null);
            }

            private void closeSock(OtpTransport s)
            {
                try
                {
                    if (s != null)
                        s.close();
                }
                catch (Exception)
                {
                }
            }

            private void closeSock(OtpServerTransport s)
            {
                try
                {
                    if (s != null)
                        s.close();
                }
                catch (Exception)
                {
                }
            }

            public int Port
            {
                get { return acceptorPort; }
            }

            public override void Run()
            {
                OtpTransport newsock = null;
                OtpCookedConnection conn = null;

                node.LocalStatus(node.Node, true, null);

                while (!done)
                {
                    conn = null;

                    try
                    {
                        newsock = sock.accept();
                    }
                    catch (Exception e)
                    {
                        // Problem in java1.2.2: accept throws SocketException
                        // when socket is closed. This will happen when
                        // acceptor.quit()
                        // is called. acceptor.quit() will call localStatus(...), so
                        // we have to check if that's where we come from.
                        if (!done)
                            node.LocalStatus(node.Node, false, e);

                        continue;
                    }

                    try
                    {
                        lock (node.Connections)
                        {
                            conn = new OtpCookedConnection(node, newsock);
                            conn.Flags = node.Flags;
                            node.AddConnection(conn);
                        }
                    }
                    catch (OtpAuthException e)
                    {
                        if (conn != null && conn.Name != null)
                            node.ConnAttempt(conn.Name, true, e);
                        else
                            node.ConnAttempt("unknown", true, e);
                        closeSock(newsock);
                    }
                    catch (IOException e)
                    {
                        if (conn != null && conn.Name != null)
                            node.ConnAttempt(conn.Name, true, e);
                        else
                            node.ConnAttempt("unknown", true, e);
                        closeSock(newsock);
                    }
                    catch (Exception e)
                    {
                        closeSock(newsock);
                        closeSock(sock);
                        node.LocalStatus(node.Node, false, e);
                    }
                }

                // if we have exited loop we must do this too
                unPublishPort();
            }
        }
    }
}
