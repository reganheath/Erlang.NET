using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;

namespace Erlang.NET
{
    public class NodeDetails
    {
        // Implicit construction
        public static implicit operator NodeDetails(string node) => new NodeDetails() { Node = node };

        // Default cookie and hostname
        protected static readonly string defaultCookie = "";
        protected static readonly string localHost;

        // Populate defaults
        static NodeDetails()
        {
            try
            {
                // Make sure it's a short name, i.e. strip of everything after first '.'
                localHost = Dns.GetHostName()?.Split('.')?.FirstOrDefault() ?? "localhost";
            }
            catch (SocketException)
            {
                localHost = "localhost";
            }

            string userHome;
            switch (Environment.OSVersion.Platform)
            {
                case PlatformID.Unix:
                    userHome = Environment.GetEnvironmentVariable("HOME");
                    break;
                default:
                    userHome = Environment.ExpandEnvironmentVariables("%HOMEDRIVE%%HOMEPATH%");
                    break;
            }

            string dotCookieFilename = Path.Combine(userHome, ".erlang.cookie");
            if (File.Exists(dotCookieFilename))
            {
                using (StreamReader sr = new StreamReader(dotCookieFilename))
                    defaultCookie = sr.ReadLine()?.Trim() ?? "";
            }
        }

        // Transport factory for connections
        private IOtpTransportFactory transportFactory;
        public IOtpTransportFactory TransportFactory
        {
            get => transportFactory ?? new OtpSocketTransportFactory();
            set => transportFactory = value;
        }

        // Node alivename
        public string Alive { get; set; }

        // Node hostname
        public string Host { get; set; } = localHost;

        // Complete node name alive@host
        public string Node
        {
            get => $"{Alive}@{Host}";
            set
            {
                (Alive, Host) = value.Split('@');
                if (Alive.Length > 0xff)
                    Alive = Alive.Substring(0, 0xff);
                if (Host is null)
                    Host = localHost;
            }
        }

        // Node cookie
        public string Cookie { get; set; } = defaultCookie;

        // Node port
        public virtual int Port { get; set; }

        // Communication stream flags
        public OtpInputStream.StreamFlags Flags { get; set; } = 0;

        public void Initialise(NodeDetails other)
        {
            TransportFactory = other.TransportFactory;
            Node = other.Node;
            Cookie = other.Cookie;
            Port = other.Port;
            Flags = other.Flags;
        }

        public override string ToString() => Node;

        public override bool Equals(object obj) => Equals(obj as NodeDetails);

        public override int GetHashCode() => Node.GetHashCode();

        public bool Equals(NodeDetails o) => string.Equals(Node, o.Node);
    }
}
