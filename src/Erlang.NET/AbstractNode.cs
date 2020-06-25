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
using System.Linq;
using System.Net;
using System.Net.Sockets;

namespace Erlang.NET
{
    /**
     * <p>
     * Represents an OTP node.
     * </p>
     * 
     * <p>
     * About nodenames: Erlang nodenames consist of two components, an alivename and
     * a hostname separated by '@'. Additionally, there are two nodename formats:
     * short and long. Short names are of the form "alive@hostname", while long
     * names are of the form "alive@host.fully.qualified.domainname". Erlang has
     * special requirements regarding the use of the short and long formats, in
     * particular they cannot be mixed freely in a network of communicating nodes,
     * however Jinterface makes no distinction. See the Erlang documentation for
     * more information about nodenames.
     * </p>
     * 
     * <p>
     * The constructors for the AbstractNode classes will create names exactly as
     * you provide them as long as the name contains '@'. If the string you provide
     * contains no '@', it will be treated as an alivename and the name of the local
     * host will be appended, resulting in a shortname. Nodenames longer than 255
     * characters will be truncated without warning.
     * </p>
     * 
     * <p>
     * Upon initialization, this class attempts to read the file .erlang.cookie in
     * the user's home directory, and uses the trimmed first line of the file as the
     * default cookie by those constructors lacking a cookie argument. If for any
     * reason the file cannot be found or read, the default cookie will be set to
     * the empty string (""). The location of a user's home directory is determined
     * using the system property "user.home", which may not be automatically set on
     * all platforms.
     * </p>
     * 
     * <p>
     * Instances of this class cannot be created directly, use one of the subclasses
     * instead.
     * </p>
     */
    public class AbstractNode : OtpTransportFactory
    {
        private static readonly string localHost;
        protected static readonly string defaultCookie;
        internal readonly OtpTransportFactory transportFactory;

        // Node types
        public const int NTYPE_R6 = 110; // 'n' post-r5, all nodes

        // Node capability flags
        public const int dFlagPublished = 1;
        public const int dFlagAtomCache = 2;
        public const int dFlagExtendedReferences = 4;
        public const int dFlagDistMonitor = 8;
        public const int dFlagFunTags = 0x10;
        public const int dFlagDistMonitorName = 0x20; // NOT USED
        public const int dFlagHiddenAtomCache = 0x40; // NOT SUPPORTED
        public const int dflagNewFunTags = 0x80;
        public const int dFlagExtendedPidsPorts = 0x100;
        public const int dFlagExportPtrTag = 0x200;
        public const int dFlagBitBinaries = 0x400;
        public const int dFlagNewFloats = 0x800;
        public const int dFlagUnicodeIo = 0x1000;
        public const int dFlagUtf8Atoms = 0x10000;
        public const int dFlagMapTag = 0x20000;
        public const int dFlagBigCreation = 0x40000;
        public const int dFlagHandshake23 = 0x1000000;
        private const int V = 6;

        /* initialize hostname and default cookie */
        static AbstractNode()
        {
            try
            {
                localHost = Dns.GetHostName();
                // Make sure it's a short name, i.e. strip of everything after first '.'
                localHost = localHost.Split('.').FirstOrDefault() ?? localHost;
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

            try
            {
                string dotCookieFilename = Path.Combine(userHome, ".erlang.cookie");
                using (StreamReader sr = new StreamReader(dotCookieFilename))
                    defaultCookie = sr.ReadLine()?.Trim() ?? "";
            }
            catch (DirectoryNotFoundException)
            {
                defaultCookie = "";
            }
            catch (FileNotFoundException)
            {
                defaultCookie = "";
            }
        }

        protected AbstractNode(OtpTransportFactory transportFactory)
        {
            this.transportFactory = transportFactory;
        }

        /**
         * Create a node with the given name and the default cookie and transport factory.
         */
        protected AbstractNode(string name)
            : this(name, defaultCookie, new OtpSocketTransportFactory())
        {
        }

        /**
         * Create a node with the given name, transport factory and the default cookie.
         */
        protected AbstractNode(string name, OtpTransportFactory transportFactory)
            : this(name, defaultCookie, transportFactory)
        {
        }


        /**
         * Create a node with the given name, cookie, and default transport factory.
         */
        protected AbstractNode(string name, string cookie)
            : this(name, cookie, new OtpSocketTransportFactory())
        {
        }

        /**
         * Create a node with the given name, cookie, and transport factory.
         */
        protected AbstractNode(string name, string cookie, OtpTransportFactory transportFactory)
        {
            Cookie = cookie;
            this.transportFactory = transportFactory;

            (Alive, Host) = name.Split('@');
            if (Host == null)
                Host = localHost;

            if (Alive.Length > 0xff)
                Alive = Alive.Substring(0, 0xff);

            Node = Alive + "@" + Host;
        }

        public long CapFlags { get; set; } = dFlagExtendedReferences | dFlagExtendedPidsPorts
            | dFlagBitBinaries | dFlagNewFloats | dFlagFunTags
            | dflagNewFunTags | dFlagUtf8Atoms | dFlagMapTag
            | dFlagExportPtrTag
            | dFlagBigCreation
            | dFlagHandshake23;

        /**
         * Get the name of this node.
         * 
         * @return the name of the node represented by this object.
         */
        public string Node { get; set; }

        /**
         * Get the hostname part of the nodename. Nodenames are composed of two
         * parts, an alivename and a hostname, separated by '@'. This method returns
         * the part of the nodename following the '@'.
         * 
         * @return the hostname component of the nodename.
         */
        public string Host { get; set; }

        /**
         * Get the alivename part of the hostname. Nodenames are composed of two
         * parts, an alivename and a hostname, separated by '@'. This method returns
         * the part of the nodename preceding the '@'.
         * 
         * @return the alivename component of the nodename.
         */
        public string Alive { get; set; }

        /**
         * Get the authorization cookie used by this node.
         * 
         * @return the authorization cookie used by this node.
         */
        public string Cookie { get; set; }

        public virtual int Port { get; protected set; } = 0;

        // package scope
        internal int Type { get; set; } = NTYPE_R6;

        // package scope
        internal int DistHigh { get; set; } = V;

        // package scope
        internal int DistLow { get; set; } = 5;

        // package scope: useless information?
        internal int Proto { get; set; } = 0;

        // package scope
        internal int Creation { get; set; } = 0;

        public override string ToString() => Node;

        public IOtpTransport CreateTransport(string addr, int port) => transportFactory.CreateTransport(addr, port);

        public IOtpTransport CreateTransport(IPEndPoint addr) => transportFactory.CreateTransport(addr);

        public IOtpServerTransport CreateServerTransport(int port) => transportFactory.CreateServerTransport(port);
    }
}
