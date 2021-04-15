using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Erlang.NET
{
    public class RemoteStatusEventArgs : EventArgs
    {
        internal RemoteStatusEventArgs(string node, bool up, object info = null)
        {
            Node = node;
            Up = up;
            Info = info;
        }

        public string Node { get; private set; }
        public bool Up { get; private set; }
        public object Info { get; private set; }
    }

    public delegate void RemoteStatusEvent(RemoteStatusEventArgs e);

    public class LocalStatusEventArgs : EventArgs
    {
        internal LocalStatusEventArgs(string node, bool up, object info = null)
        {
            Node = node;
            Up = up;
            Info = info;
        }

        public string Node { get; private set; }
        public bool Up { get; private set; }
        public object Info { get; private set; }
    }

    public delegate void LocalStatusEvent(LocalStatusEventArgs e);

    public class ConnAttemptEventArgs : EventArgs
    {
        internal ConnAttemptEventArgs(string node, bool incoming, object info = null)
        {
            Node = node;
            Incoming = incoming;
            Info = info;
        }

        public string Node { get; private set; }
        public bool Incoming { get; private set; }
        public object Info { get; private set; }
    }

    public delegate void ConnAttemptEvent(ConnAttemptEventArgs e);

    public class MessageEventArgs : EventArgs
    {
        internal MessageEventArgs(OtpMbox mbox, OtpMsg msg)
        {
            Mbox = mbox;
            Msg = msg;
        }

        public OtpMbox Mbox { get; private set; }
        public OtpMsg Msg { get; private set; }
    }

    public delegate void MessageEvent(MessageEventArgs e);
}
