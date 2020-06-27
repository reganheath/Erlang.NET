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
using System.Net;

namespace Erlang.NET
{
    /**
     * Represents an OTP node.
     * 
     * About nodenames: Erlang nodenames consist of two components, an alivename and
     * a hostname separated by '@'. Additionally, there are two nodename formats:
     * short and long. Short names are of the form "alive@hostname", while long
     * names are of the form "alive@host.fully.qualified.domainname". Erlang has
     * special requirements regarding the use of the short and long formats, in
     * particular they cannot be mixed freely in a network of communicating nodes,
     * however Jinterface makes no distinction. See the Erlang documentation for
     * more information about nodenames.
     * 
     * The constructors for the AbstractNode classes will create names exactly as
     * you provide them as long as the name contains '@'. If the string you provide
     * contains no '@', it will be treated as an alivename and the name of the local
     * host will be appended, resulting in a shortname. Nodenames longer than 255
     * characters will be truncated without warning.
     * 
     * Upon initialization, this class attempts to read the file .erlang.cookie in
     * the user's home directory, and uses the trimmed first line of the file as the
     * default cookie by those constructors lacking a cookie argument. If for any
     * reason the file cannot be found or read, the default cookie will be set to
     * the empty string (""). The location of a user's home directory is determined
     * using the system property "user.home", which may not be automatically set on
     * all platforms.
     * 
     * Instances of this class cannot be created directly, use one of the subclasses
     * instead.
     */
    public class AbstractNode : NodeDetails, IOtpTransportFactory
    {
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

        // Node type and distribution protocol etc
        internal int Type { get; set; } = NTYPE_R6;
        internal int DistHigh { get; set; } = 6;
        internal int DistLow { get; set; } = 5;
        internal int Proto { get; set; } = 0;
        internal int Creation { get; set; } = 0;

        public long CapFlags { get; set; } = dFlagExtendedReferences | dFlagExtendedPidsPorts
            | dFlagBitBinaries | dFlagNewFloats | dFlagFunTags
            | dflagNewFunTags | dFlagUtf8Atoms | dFlagMapTag
            | dFlagExportPtrTag
            | dFlagBigCreation
            | dFlagHandshake23;

        public override string ToString() => Node;

        public IOtpTransport CreateTransport(string addr, int port) => TransportFactory.CreateTransport(addr, port);

        public IOtpTransport CreateTransport(IPEndPoint addr) => TransportFactory.CreateTransport(addr);

        public IOtpServerTransport CreateServerTransport(int port) => TransportFactory.CreateServerTransport(port);
    }
}
