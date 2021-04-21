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
using System.Threading;

namespace Erlang.NET
{
    /**
     * <p>
     * Provides a simple mechanism for exchanging messages with Erlang processes or
     * other instances of this class.
     * </p>
     * 
     * <p>
     * Each mailbox is associated with a unique {@link OtpErlangPid pid} that
     * contains information necessary for delivery of messages. When sending
     * messages to named processes or mailboxes, the sender pid is made available to
     * the recipient of the message. When sending messages to other mailboxes, the
     * recipient can only respond if the sender includes the pid as part of the
     * message contents. The sender can determine his own pid by calling
     * {@link #self() self()}.
     * </p>
     * 
     * <p>
     * Mailboxes can be named, either at creation or later. Messages can be sent to
     * named mailboxes and named Erlang processes without knowing the
     * {@link OtpErlangPid pid} that identifies the mailbox. This is neccessary in
     * order to set up initial communication between parts of an application. Each
     * mailbox can have at most one name.
     * </p>
     * 
     * <p>
     * Since this class was intended for communication with Erlang, all of the send
     * methods take {@link IOtpErlangObject IOtpErlangObject} arguments. However this
     * class can also be used to transmit arbitrary Java objects (as long as they
     * implement one of java.io.Serializable or java.io.Externalizable) by
     * encapsulating the object in a {@link OtpErlangBinary OtpErlangBinary}.
     * </p>
     * 
     * <p>
     * Messages to remote nodes are externalized for transmission, and as a result
     * the recipient receives a <b>copy</b> of the original Java object. To ensure
     * consistent behaviour when messages are sent between local mailboxes, such
     * messages are cloned before delivery.
     * </p>
     * 
     * <p>
     * Additionally, mailboxes can be linked in much the same way as Erlang
     * processes. If a link is active when a mailbox is {@link #close closed}, any
     * linked Erlang processes or OtpMboxes will be sent an exit signal. As well,
     * exit signals will be (eventually) sent if a mailbox goes out of scope and its
     * {@link #finalize finalize()} method called. However due to the nature of
     * finalization (i.e. Java makes no guarantees about when {@link #finalize
     * finalize()} will be called) it is recommended that you always explicitly
     * close mailboxes if you are using links instead of relying on finalization to
     * notify other parties in a timely manner.
     * </p>
     * 
     * When retrieving messages from a mailbox that has received an exit signal, an
     * {@link OtpErlangExit OtpErlangExit} exception will be raised. Note that the
     * exception is queued in the mailbox along with other messages, and will not be
     * raised until it reaches the head of the queue and is about to be retrieved.
     * </p>
     * 
     */
    public class OtpMbox : IDisposable
    {
        private readonly object lockObj = new object();
        private readonly OtpNode home;
        private readonly Links links;

        protected GenericQueue<OtpMsg> Queue { get; private set; } = new GenericQueue<OtpMsg>();

        public event MessageEvent Received;

        /**
         * Get the registered name of this mailbox.
         */
        public string Name { get; set; }

        /**
         * Get the identifying {@link OtpErlangPid pid} associated with this mailbox.
         * 
         * The {@link OtpErlangPid pid} associated with this mailbox uniquely
         * identifies the mailbox and can be used to address the mailbox. You can
         * send the {@link OtpErlangPid pid} to a remote communicating part so that
         * he can know where to send his response.
         */
        public OtpErlangPid Self { get; private set; }

        /**
         * package constructor: called by OtpNode:createMbox(name) to create a named mbox
         */ 
        internal OtpMbox(OtpNode home, OtpErlangPid self, string name)
        {
            Name = name;
            Self = self;
            this.home = home;
            links = new Links(10);
        }

        /**
         * package constructor: called by OtpNode:createMbox() to create an anonymous mbox
         */
        internal OtpMbox(OtpNode home, OtpErlangPid self)
            : this(home, self, null)
        {
        }

        /**
         * Register or remove a name for this mailbox. Registering a name for a
         * mailbox enables others to send messages without knowing the
         * {@link OtpErlangPid pid} of the mailbox. A mailbox can have at most one
         * name; if the mailbox already had a name, calling this method will
         * supercede that name.
         */
        public bool RegisterName(string name)
        {
            lock (lockObj)
                return home.RegisterName(name, this);
        }

        /**
         * Block until a message arrives for this mailbox.
         */
        public IOtpErlangObject Receive()
        {
            return ReceiveMsg().Payload;
        }

        /**
         * Wait for a message to arrive for this mailbox.
         */
        public IOtpErlangObject Receive(long timeout)
        {
            OtpMsg m = ReceiveMsg(timeout);
            return m?.Payload;
        }

        /**
         * Block until a message arrives for this mailbox.
         */
        public OtpInputStream ReceiveBuf() => ReceiveMsg().Stream;

        /**
         * Wait for a message to arrive for this mailbox.
         */
        public OtpInputStream ReceiveBuf(long timeout)
        {
            OtpMsg m = ReceiveMsg(timeout);
            return m?.Stream;
        }

        /**
         * Block until a message arrives for this mailbox.
         */
        public virtual OtpMsg ReceiveMsg()
        {
            if (!Queue.Dequeue(out OtpMsg m))
                return null;
            return ProcessMsg(m);
        }

        /**
         * Wait for a message to arrive for this mailbox.
         */
        public virtual OtpMsg ReceiveMsg(long timeout)
        {
            if (!Queue.Dequeue(timeout, out OtpMsg m))
                return null;
            return ProcessMsg(m);
        }

        /*
         * Process EXIT by throwing OtpExit, otherwise return message
         */
        private OtpMsg ProcessMsg(OtpMsg m)
        {
            switch (m.Type)
            {
                case OtpMsg.exitTag:
                case OtpMsg.exit2Tag:
                    try
                    {
                        throw new OtpExit(m.Payload, m.FromPid);
                    }
                    catch (OtpDecodeException)
                    {
                        throw new OtpExit("unknown", m.FromPid);
                    }

                default:
                    return m;
            }
        }

        /**
         * Send a message to a remote {@link OtpErlangPid pid}, representing either
         * another {@link OtpMbox mailbox} or an Erlang process.
         */
        public void Send(OtpErlangPid to, IOtpErlangObject msg)
        {
            string node = to.Node;
            if (node.Equals(home.Node))
            {
                home.Deliver(new OtpMsg(to, (IOtpErlangObject)msg.Clone()));
                return;
            }

            OtpCookedConnection conn = home.GetConnection(node);
            if (conn == null)
                return;

            conn.Send(Self, to, msg);
        }

        /**
         * Send a message to a named mailbox created from the same node as this
         * mailbox.
         */
        public void Send(string name, IOtpErlangObject msg) => home.Deliver(new OtpMsg(Self, name, (IOtpErlangObject)msg.Clone()));

        /**
         * Send a message to a named mailbox created from another node.
         */
        public void Send(string node, string name, IOtpErlangObject msg)
        {
            // this node?
            if (node.Equals(home.Node) || node.Equals(home.Alive))
            {
                Send(name, msg);
                return;
            }

            // other node
            OtpCookedConnection conn = home.GetConnection(node);
            if (conn == null)
                return;

            conn.Send(Self, name, msg);
        }

        /**
         * Close this mailbox with the given reason.
         * 
         * After this operation, the mailbox will no longer be able to receive
         * messages. Any delivered but as yet unretrieved messages can still be
         * retrieved however.
         * 
         * If there are links from this mailbox to other {@link OtpErlangPid pids},
         * they will be broken when this method is called and exit signals will be
         * sent.
         */
        public void Exit(IOtpErlangObject reason) => home.CloseMbox(this, reason);

        /**
         * Equivalent to <code>exit(new OtpErlangAtom(reason))</code>.
         */
        public void Exit(string reason) => Exit(new OtpErlangAtom(reason));

        /**
         * Send an exit2 signal to a remote {@link OtpErlangPid pid}. This method
         * does not cause any links to be broken, except indirectly if the remote
         * {@link OtpErlangPid pid} exits as a result of this exit signal.
         */
        public void Exit(OtpErlangPid to, IOtpErlangObject reason) => Exit(2, to, reason);

        /**
         * Equivalent to <code>exit(to, new
         * OtpErlangAtom(reason))</code>.
         */
        public void Exit(OtpErlangPid to, string reason) => Exit(to, new OtpErlangAtom(reason));

        // this function used internally when "process" dies
        // since Erlang discerns between exit and exit/2.
        private void Exit(int arity, OtpErlangPid to, IOtpErlangObject reason)
        {
            try
            {
                string node = to.Node;
                if (node.Equals(home.Node))
                {
                    home.Deliver(new OtpMsg(OtpMsg.exitTag, Self, to, reason));
                    return;
                }

                OtpCookedConnection conn = home.GetConnection(node);
                if (conn == null)
                    return;

                switch (arity)
                {
                    case 1:
                        conn.Exit(Self, to, reason);
                        break;
                    case 2:
                        conn.Exit2(Self, to, reason);
                        break;
                }
            }
            catch (Exception) { }
        }

        /**
         * Link to a remote mailbox or Erlang process. Links are idempotent, calling
         * this method multiple times will not result in more than one link being
         * created.
         * 
         * If the remote process subsequently exits or the mailbox is closed, a
         * subsequent attempt to retrieve a message through this mailbox will cause
         * an {@link OtpErlangExit OtpErlangExit} exception to be raised. Similarly,
         * if the sending mailbox is closed, the linked mailbox or process will
         * receive an exit signal.
         * 
         * If the remote process cannot be reached in order to set the link, the
         * exception is raised immediately.
         */
        public void Link(OtpErlangPid to)
        {
            try
            {
                string node = to.Node;
                if (node.Equals(home.Node))
                {
                    if (!home.Deliver(new OtpMsg(OtpMsg.linkTag, Self, to)))
                        throw new OtpExit("noproc", to);
                }
                else
                {
                    OtpCookedConnection conn = home.GetConnection(node);
                    if (conn == null)
                        throw new OtpExit("noproc", to);
                    conn.Link(Self, to);
                }
            }
            catch (Exception) { }

            links.AddLink(Self, to);
        }

        /**
         * Remove a link to a remote mailbox or Erlang process. This method removes
         * a link created with {@link #link link()}. Links are idempotent; calling
         * this method once will remove all links between this mailbox and the
         * remote {@link OtpErlangPid pid}.
         */
        public void Unlink(OtpErlangPid to)
        {
            links.RemoveLink(Self, to);

            try
            {
                string node = to.Node;
                if (node.Equals(home.Node))
                {
                    home.Deliver(new OtpMsg(OtpMsg.unlinkTag, Self, to));
                }
                else
                {
                    OtpCookedConnection conn = home.GetConnection(node);
                    if (conn != null)
                        conn.Unlink(Self, to);
                }
            }
            catch (Exception) { }
        }

        /* ping remote node */
        public bool Ping(string node, long timeout)
        {
            try
            {
                Send(node, "net_kernel", PingTuple());
                var reply = (OtpErlangTuple)Receive(timeout);
                if (reply != null && reply.Arity >= 2)
                {
                    OtpErlangAtom a = (OtpErlangAtom)reply.ElementAt(1);
                    return "yes".Equals(a?.Value);
                }
            }
            catch (Exception) { }
            return false;
        }

        public OtpErlangTuple PingTuple()
        {
            // { $gen_call, { self, ref }, { is_auth, node } }
            return CallTuple(new OtpErlangTuple(
                new OtpErlangAtom("is_auth"),
                new OtpErlangAtom(home.Node))
            );
        }

        /* RPC to remote node */
        public IOtpErlangObject RPC(string node, long timeout, string module, string function, params IOtpErlangObject[] args)
        {
            Send(node, "rex", RPCTuple(module, function, args));
            var reply = (OtpErlangTuple)Receive(timeout);
            if (reply != null)
            {
                (var rex, var result) = reply;
                if (((OtpErlangAtom)rex)?.Value != "rex")
                    throw new IOException($"badrpc: {reply}");
                return result;
            }
            return null;
        }

        public OtpErlangTuple RPCTuple(string module, string function, params IOtpErlangObject[] args)
        {
            return new OtpErlangTuple(
                Self,
                new OtpErlangTuple(
                    new OtpErlangAtom("call"),
                    new OtpErlangAtom(module),
                    new OtpErlangAtom(function),
                    args != null ? new OtpErlangList(args) : new OtpErlangList(),
                    new OtpErlangAtom("user")
                ));
        }

        public IOtpErlangObject Call(string node, string module, IOtpErlangObject msg, long timeout)
        {
            Send(node, module, CallTuple(msg));
            var reply = (OtpErlangTuple)Receive(timeout);
            if (reply != null && reply.Arity >= 2)
                return reply.ElementAt(1);
            return null;
        }

        public OtpErlangTuple CallTuple(IOtpErlangObject msg)
        {
            // { $gen_call, { self, ref }, <msg> }
            return new OtpErlangTuple(
                new OtpErlangAtom("$gen_call"),
                new OtpErlangTuple(
                    Self,
                    home.CreateRef()),
                msg);
        }

        /* Perform a gen_cast to a node and module */
        public void Cast(string node, string module, IOtpErlangObject msg)
        {
            // { $gen_cast, { self, <msg> } }
            Send(node, module, new OtpErlangTuple(new OtpErlangAtom("$gen_cast"), msg));
        }

        /* Perform a gen server info to a node and module */
        public void Info(string node, string module, IOtpErlangObject msg)
        {
            // { self, <msg> }
            Send(node, module, new OtpErlangTuple(Self, msg));
        }

        /**
         * Get a list of all known registered names on the same {@link OtpNode node}
         * as this mailbox.
         * 
         * This method calls a method with the same name in {@link OtpNode#getNames
         * Otpnode} but is provided here for convenience.
         */
        public string[] GetNames() => home.Names();

        /**
         * Determine the {@link OtpErlangPid pid} corresponding to a registered name
         * on this {@link OtpNode node}.
         * 
         * This method calls a method with the same name in {@link OtpNode#whereis
         * Otpnode} but is provided here for convenience.
         */
        public OtpErlangPid WhereIs(string name) => home.WhereIs(name);

        /**
         * Close this mailbox.
         * 
         * After this operation, the mailbox will no longer be able to receive
         * messages. Any delivered but as yet unretrieved messages can still be
         * retrieved however.
         * 
         * If there are links from this mailbox to other {@link OtpErlangPid pids},
         * they will be broken when this method is called and exit signals with
         * reason 'normal' will be sent.
         * 
         * This is equivalent to {@link #exit(string) exit("normal")}.
         */
        public virtual void Close() => home.CloseMbox(this);

        /**
         * Determine if two mailboxes are equal.
         */
        public override bool Equals(object o) => Equals(o as OtpMbox);

        public bool Equals(OtpMbox o)
        {
            if (o is null)
                return false;
            if (ReferenceEquals(this, o))
                return true;
            return Self.Equals(o.Self);
        }

        public override int GetHashCode() => Self.GetHashCode();

        public override string ToString()
        {
            return $"{Name} {Self}";
        }

        /**
         * called by OtpNode to deliver message to this mailbox.
         * 
         * About exit and exit2: both cause exception to be raised upon receive().
         * However exit (not 2) causes any link to be removed as well, while exit2
         * leaves any links intact.
         */
        public virtual void Deliver(OtpMsg m)
        {
            switch (m.Type)
            {
                case OtpMsg.linkTag:
                    links.AddLink(Self, m.FromPid);
                    break;

                case OtpMsg.unlinkTag:
                    links.RemoveLink(Self, m.FromPid);
                    break;

                case OtpMsg.exitTag:
                    links.RemoveLink(Self, m.FromPid);
                    if (Received != null)
                        Received?.Invoke(new MessageEventArgs(this, m));
                    else
                        Queue.Enqueue(m);
                    break;

                case OtpMsg.exit2Tag:
                default:
                    if (Received != null)
                        Received?.Invoke(new MessageEventArgs(this, m));
                    else
                        Queue.Enqueue(m);
                    break;
            }
        }

        // used to break all known links to this mbox
        public void BreakLinks(IOtpErlangObject reason)
        {
            foreach (var link in links.ClearLinks())
                Exit(1, link.Remote, reason);
        }

        #region IDisposable
        private bool disposedValue;
        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    try
                    {
                        Close();
                        Queue.Clear();
                    }
                    catch (Exception) { }
                }
                disposedValue = true;
            }
        }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
        #endregion
    }
}
