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
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Erlang.NET
{
    /**
     * Represents a local OTP node. This class is used when you do not wish to
     * manage connections yourself - outgoing connections are established as needed,
     * and incoming connections accepted automatically. This class supports the use
     * of a mailbox API for communication, while management of the underlying
     * communication mechanism is automatic and hidden from the application
     * programmer.
     * 
     * Once an instance of this class has been created, obtain one or more mailboxes
     * in order to send or receive messages. The first message sent to a given node
     * will cause a connection to be set up to that node. Any messages received will
     * be delivered to the appropriate mailboxes.
     * 
     * To shut down the node, call {@link #close close()}. This will prevent the
     * node from accepting additional connections and it will cause all existing
     * connections to be closed. Any unread messages in existing mailboxes can still
     * be read, however no new messages will be delivered to the mailboxes.
     * 
     * Note that the use of this class requires that Epmd (Erlang Port Mapper
     * Daemon) is running on each cooperating host. This class does not start Epmd
     * automatically as Erlang does, you must start it manually or through some
     * other means. See the Erlang documentation for more information about this.
     */
    public class OtpNode : OtpLocalNode
    {
        private readonly object lockObj = new object();

        // handle incomming connections
        private readonly Dictionary<string, OtpCookedConnection> connections = new Dictionary<string, OtpCookedConnection>();
        private readonly IOtpServerTransport listen;
        private readonly Task serveTask;
        private bool stopping;

        // keep track of all mailboxes
        private readonly Mailboxes mboxes = null;

        public event RemoteStatusEvent RemoteStatus;
        public event LocalStatusEvent LocalStatus;
        public event ConnAttemptEvent ConnAttempt;        

        public OtpNode(NodeDetails details)
        {
            Initialise(details);
            mboxes = new Mailboxes(this);

            // Create listening socket
            listen = CreateServerTransport(Port);
            Port = listen.LocalPort;

            // Publish
            PublishPort();

            // Listen..
            serveTask = Task.Run(() => Serve());
        }

        /**
         * Close the node. Unpublish the node from Epmd (preventing new connections)
         * and close all existing connections.
         */
        public void Close()
        {
            stopping = true;
            OtpTransport.Close(listen);
            serveTask?.Wait();

            lock (lockObj)
                mboxes.Clear();

            lock (connections)
            {
                foreach (var conn in connections.Values)
                    conn.Close();
                connections.Clear();
            }
        }

        /**
         * Create an unnamed {@link OtpMbox mailbox} that can be used to send and
         * receive messages with other, similar mailboxes and with Erlang processes.
         * Messages can be sent to this mailbox by using its associated
         * {@link OtpMbox#self() pid}.
         */
        public OtpMbox CreateMbox() => mboxes.Create();

        /**
         * Create an named mailbox that can be used to send and receive messages
         * with other, similar mailboxes and with Erlang processes. Messages can be
         * sent to this mailbox by using its registered name or the associated
         * {@link OtpMbox#self pid}.
         */
        //public OtpMbox CreateMbox(string name, bool sync) => mboxes.Create(name, sync);
        public OtpMbox CreateMbox(string name) => mboxes.Create(name);

        /*
         * Get a pre-existing named mailbox.
         */
        public OtpMbox GetMbox(string name) => mboxes.Get(name);

        /**
         * Close the specified mailbox with reason 'normal'.
         * 
         * After this operation, the mailbox will no longer be able to
         * receive messages. Any delivered but as yet unretrieved
         * messages can still be retrieved however.
         *
         * If there are links from the mailbox to other
         * {@link OtpErlangPid pids}, they will be broken when this
         * method is called and exit signals with reason 'normal' will be
         * sent.
         */
        public void CloseMbox(OtpMbox mbox) => CloseMbox(mbox, new OtpErlangAtom("normal"));

        /**
         * Close the specified mailbox with the given reason.
         * 
         * After this operation, the mailbox will no longer be able to
         * receive messages. Any delivered but as yet unretrieved
         * messages can still be retrieved however.
         *
         * If there are links from the mailbox to other
         * {@link OtpErlangPid pids}, they will be broken when this
         * method is called and exit signals with the given reason will
         * be sent.
         */
        public void CloseMbox(OtpMbox mbox, IOtpErlangObject reason)
        {
            if (mbox == null)
                return;
            mboxes.Remove(mbox);
            mbox.Name = null;
            mbox.BreakLinks(reason);
        }

        /**
         * Register or remove a name for the given mailbox. Registering a name for a
         * mailbox enables others to send messages without knowing the
         * {@link OtpErlangPid pid} of the mailbox. A mailbox can have at most one
         * name; if the mailbox already had a name, calling this method will
         * supercede that name.
         */
        public bool RegisterName(string name, OtpMbox mbox) => mboxes.Register(name, mbox);

        /**
         * Get a list of all known registered names on this node.
         */
        public string[] Names() => mboxes.Names();

        /**
         * Determine the {@link OtpErlangPid pid} corresponding to a registered name
         * on this node.
         */
        public OtpErlangPid WhereIs(string name)
        {
            OtpMbox m = mboxes.Get(name);
            if (m != null)
                return m.Self;
            return null;
        }

        /**
         * Determine if another node is alive. This method has the side effect of
         * setting up a connection to the remote node (if possible). Only a single
         * outgoing message is sent; the timeout is how long to wait for a response.
         * 
         * Only a single attempt is made to connect to the remote node, so for
         * example it is not possible to specify an extremely long timeout and
         * expect to be notified when the node eventually comes up. If you wish to
         * wait for a remote node to be started, the following construction may be
         * useful:
         * 
         * // ping every 2 seconds until positive response
         * while (!me.ping(him, 2000))
         *     ;
         *     
         * Internal info about the message formats...
         * 
         * the request: -> REG_SEND {6,#Pid<bingo@aule.1.0>,'',net_kernel}
         * {'$gen_call',{#Pid<bingo@aule.1.0>,#Ref<bingo@aule.2>},{is_auth,bingo@aule}}
         * 
         * the reply: <- SEND {2,'',#Pid<bingo@aule.1.0>} {#Ref<bingo@aule.2>,yes}
         */
        public bool Ping(string node, long timeout)
        {
            if (((NodeDetails)node).Equals(this))
                return true;
            using (var mbox = CreateMbox())
                return mbox.Ping(node, timeout);
        }

        /* RPC to remote node */
        public IOtpErlangObject RPC(string node, long timeout, string module, string function, params IOtpErlangObject[] args)
        {
            using (var mbox = CreateMbox())
                return mbox.RPC(node, timeout, module, function, args);
        }

        /* Perform a gen_call to a node and module */
        public IOtpErlangObject Call(string node, string module, IOtpErlangObject msg, long timeout)
        {
            using (var mbox = CreateMbox())
                return mbox.Call(node, module, msg, timeout);
        }

        /* Perform a gen_cast to a node and module */
        public void Cast(string node, string module, IOtpErlangObject msg)
        {
            using (var mbox = CreateMbox())
                mbox.Cast(node, module, msg);
        }

        /* Perform a gen server info to a node and module */
        public void Info(string node, string module, IOtpErlangObject msg)
        {
            using (var mbox = CreateMbox())
                mbox.Info(node, module, msg);
        }

        /*
         * This method simulates net_kernel only for the purpose of replying to pings.
         */
        private bool NetKernel(OtpMsg m)
        {
            try
            {
                OtpErlangTuple t = (OtpErlangTuple)m.Payload;
                OtpErlangTuple req = (OtpErlangTuple)t.ElementAt(1); // actual request
                OtpErlangPid pid = (OtpErlangPid)req.ElementAt(0);   // originating pid
                using (var mbox = CreateMbox())
                {
                    mbox.Send(pid, new OtpErlangTuple(
                        req.ElementAt(1), // his #Ref
                        new OtpErlangAtom("yes")));
                    return true;
                }
            }
            catch (Exception) { }
            return false;
        }

        /*
         * OtpCookedConnection delivers messages here return true if message was
         * delivered successfully, or false otherwise.
         */
        public bool Deliver(OtpMsg m)
        {
            try
            {
                OtpMbox mbox;
                if (m.Type == OtpMsg.regSendTag)
                {
                    /* special case for netKernel requests */
                    if (m.ToName.Equals("net_kernel"))
                        return NetKernel(m);
                    mbox = mboxes.Get(m.ToName);
                }
                else
                {
                    mbox = mboxes.Get(m.ToPid);
                }

                if (mbox == null)
                    return false;
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
        public void Deliver(OtpCookedConnection conn, Exception e)
        {
            RemoveConnection(conn);
            try { RemoteStatus?.Invoke(new RemoteStatusEventArgs(conn.Name, false, e)); }
            catch (Exception) { }
        }

        //public void React(OtpActor actor)
        //{
        //    sched.React(actor);
        //}

        /*
         * find or create a connection to the given node
         */
        public OtpCookedConnection GetConnection(string node)
        {
            lock (connections)
            {
                if (connections.ContainsKey(node))
                    return connections[node];
            }

            try
            {
                OtpCookedConnection c = new OtpCookedConnection(this, new OtpPeer() { Node = node });
                AddConnection(c);
                return c;
            }
            catch (Exception e)
            {
                /* false = outgoing */
                try { ConnAttempt?.Invoke(new ConnAttemptEventArgs(node, false, e)); }
                catch (Exception) { }
                throw;
            }
        }

        private void AddConnection(OtpCookedConnection conn)
        {
            if (conn?.Name != null)
            {
                lock (connections)
                    connections.Add(conn.Name, conn);
                try { RemoteStatus?.Invoke(new RemoteStatusEventArgs(conn.Name, true)); }
                catch (Exception) { }
            }
        }

        private void RemoveConnection(OtpCookedConnection conn)
        {
            if (conn?.Name != null)
            {
                lock (connections)
                    connections.Remove(conn.Name);
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

            // mbox pids here
            private readonly Dictionary<OtpErlangPid, WeakReference> byPid = null;
            // mbox names here
            private readonly Dictionary<string, WeakReference> byName = null;

            public Mailboxes(OtpNode node)
            {
                this.node = node;
                byPid = new Dictionary<OtpErlangPid, WeakReference>();
                byName = new Dictionary<string, WeakReference>();
            }

            public OtpMbox Create(string name)
            {
                OtpMbox m = null;

                lock (lockObj)
                {
                    if (Get(name) != null)
                        return null;

                    OtpErlangPid pid = node.CreatePid();
                    m = new OtpMbox(node, pid, name);
                    byPid.Add(pid, new WeakReference(m));
                    byName.Add(name, new WeakReference(m));
                }
                return m;
            }

            public OtpMbox Create()
            {
                OtpErlangPid pid = node.CreatePid();
                OtpMbox m = new OtpMbox(node, pid);
                lock (lockObj)
                    byPid.Add(pid, new WeakReference(m));
                return m;
            }

            public void Clear()
            {
                lock (lockObj)
                {
                    byPid.Clear();
                    byName.Clear();
                }
            }

            public string[] Names()
            {
                lock (lockObj)
                    return byName.Keys.ToArray();
            }

            public bool Register(string name, OtpMbox mbox)
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
                        if (Get(name) != null)
                            return false;
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
            public OtpMbox Get(string name)
            {
                lock (lockObj)
                {
                    if (byName.TryGetValue(name, out WeakReference wr))
                    {
                        if (wr.Target is OtpMbox m)
                            return m;
                        byName.Remove(name);
                    }
                }
                return null;
            }

            /*
             * look up a mailbox based on its pid. If the mailbox has gone out of
             * scope we also remove the reference from the hashtable so we don't
             * find it again.
             */
            public OtpMbox Get(OtpErlangPid pid)
            {
                lock (lockObj)
                {
                    if (byPid.TryGetValue(pid, out WeakReference wr))
                    {
                        if (wr.Target is OtpMbox m)
                            return m;
                        byPid.Remove(pid);
                    }
                }
                return null;
            }

            public void Remove(OtpMbox mbox)
            {
                lock (lockObj)
                {
                    byPid.Remove(mbox.Self);
                    if (mbox.Name != null)
                        byName.Remove(mbox.Name);
                }
            }
        }
        public void Serve()
        {
            try { LocalStatus?.Invoke(new LocalStatusEventArgs(Node, true)); }
            catch (Exception) { }

            while (!stopping)
            {
                try
                {
                    OtpCookedConnection c = null;
                    var s = listen.Accept();

                    try
                    {
                        c = new OtpCookedConnection(this, s);
                        lock (connections)
                            AddConnection(c);
                    }
                    catch (OtpAuthException e)
                    {
                        try { ConnAttempt?.Invoke(new ConnAttemptEventArgs(c?.Name ?? "unknown", true, e)); }
                        catch (Exception) { }
                        s.Dispose();
                    }
                    catch (IOException e)
                    {
                        try { ConnAttempt?.Invoke(new ConnAttemptEventArgs(c?.Name ?? "unknown", true, e)); }
                        catch (Exception) { }
                        s.Dispose();
                    }
                }
                catch (Exception e)
                {
                    if (!stopping)
                        Console.WriteLine($"{Node} ERROR {e}");
                    break;
                }
            }

            try { LocalStatus?.Invoke(new LocalStatusEventArgs(Node, false)); }
            catch (Exception) { }

            UnPublishPort();
        }
    }
}
