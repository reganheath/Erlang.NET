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
using System;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Erlang.NET
{
    /**
     * Provides methods for registering, unregistering and looking up nodes with the
     * Erlang portmapper daemon (Epmd). For each registered node, Epmd maintains
     * information about the port on which incoming connections are accepted, as
     * well as which versions of the Erlang communication protocol the node
     * supports.
     * 
     * Nodes wishing to contact other nodes must first request information from Epmd
     * before a connection can be set up, however this is done automatically by
     * {@link OtpSelf#connect(OtpPeer) OtpSelf.connect()} when necessary.
     * 
     * The methods {@link #publishPort(OtpLocalNode) publishPort()} and
     * {@link #unPublishPort(OtpLocalNode) unPublishPort()} will fail if an Epmd
     * process is not running on the localhost. Additionally
     * {@link #lookupPort(AbstractNode) lookupPort()} will fail if there is no Epmd
     * process running on the host where the specified node is running. See the
     * Erlang documentation for information about starting Epmd.
     * 
     * This class contains only static methods, there are no constructors.
     */
    public partial class OtpEpmd
    {
        private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        private static int epmdPort = 0;

        public static int EpmdPort
        {
            set => epmdPort = value;
            get
            {
                if (epmdPort == 0)
                {
                    try
                    {
                        if (!int.TryParse(Environment.GetEnvironmentVariable("ERL_EPMD_PORT"), out epmdPort))
                            epmdPort = 4369;
                    }
                    catch (Exception)
                    {
                        epmdPort = 4369;
                    }
                }

                return epmdPort;
            }
        }

        // common values
        private const byte stopReq = 115;

        // version specific value
        private const byte port4req = 122;
        private const byte port4resp = 119;
        private const byte ALIVE2_REQ = 120;
        private const byte ALIVE2_RESP = 121;
        private const byte ALIVE2_X_RESP = 118;
        private const byte names4req = 110;

        private static readonly int traceLevel = 0;
        private const int traceThreshold = 4;

        static OtpEpmd()
        {
            // debug this connection?
            string trace = ConfigurationManager.AppSettings["OtpConnection.trace"];

            try
            {
                if (trace != null)
                    traceLevel = int.Parse(trace);
            }
            catch (FormatException)
            {
                traceLevel = 0;
            }
        }

        /**
         * Determine what port a node listens for incoming connections on.
         * 
         * @return the listen port for the specified node, or 0 if the node was not
         *         registered with Epmd.
         * 
         * @exception java.io.IOException
         *                if there was no response from the name server.
         */
        public static async Task<int> LookupPortAsync(AbstractNode node)
        {
            try { return await LookupPort_R4Async(node); }
            catch (Exception) { throw; }
        }

        public static int LookupPort(AbstractNode node)
        {
            return LookupPortAsync(node).Result;
        }

        /**
         * Register with Epmd, so that other nodes are able to find and connect to
         * it.
         */
        public static async Task<IOtpTransport> PublishPortAsync(OtpLocalNode node)
        {
            try { return await Publish_R4Async(node); }
            catch (Exception) { throw; }
        }

        public static IOtpTransport PublishPort(OtpLocalNode node)
        {
            return PublishPortAsync(node).Result;
        }

        /**
         * Unregister from Epmd. Other nodes wishing to connect will no longer be
         * able to. Caller should close his epmd socket.
         * This method does not report any failures.
         */
        public static async Task UnPublishPortAsync(OtpLocalNode node)
        {
            // don't even wait for a response (is there one?)
            if (traceLevel >= traceThreshold)
            {
                log.Debug($"-> UNPUBLISH {node} port={node.Port} [{node.Epmd}]");
                log.Debug("<- OK (assumed)");
            }
            try
            {
                OtpOutputStream obuf = new OtpOutputStream();
                obuf.Write2BE(node.Alive.Length + 1);
                obuf.Write1(stopReq);
                obuf.WriteN(Encoding.GetEncoding("ISO-8859-1").GetBytes(node.Alive));
                await obuf.WriteToAsync(node.Epmd.OutputStream);
                //// don't even wait for a response (is there one?)
                //if (traceLevel >= traceThreshold)
                //{
                //    log.Debug($"-> UNPUBLISH {node} port={node.Port}");
                //    log.Debug("<- OK (assumed)");
                //}
            }
            catch (Exception) { /* ignore all failures */ }
        }

        public static void UnPublishPort(OtpLocalNode node) => UnPublishPortAsync(node).Wait();

        private static async Task<int> LookupPort_R4Async(AbstractNode node)
        {
            int port = 0;

            try
            {
                OtpOutputStream obuf = new OtpOutputStream();
                using (IOtpTransport s = node.CreateTransport(node.Host, EpmdPort))
                {
                    // build and send epmd request
                    // length[2], tag[1], alivename[n] (length = n+1)
                    obuf.Write2BE(node.Alive.Length + 1);
                    obuf.Write1(port4req);
                    obuf.WriteN(Encoding.GetEncoding("ISO-8859-1").GetBytes(node.Alive));

                    // send request
                    await obuf.WriteToAsync(s.OutputStream);

                    if (traceLevel >= traceThreshold)
                        log.Debug($"-> LOOKUP (r4) {node}");

                    // receive and decode reply
                    // resptag[1], result[1], port[2], ntype[1], proto[1],
                    // disthigh[2], distlow[2], nlen[2], alivename[n],
                    // elen[2], edata[m]
                    byte[] tmpbuf = new byte[100];

                    int n = await s.InputStream.ReadAsync(tmpbuf, 0, tmpbuf.Length);

                    if (n < 0)
                    {
                        OtpTransport.Close(s);
                        throw new IOException($"Nameserver not responding on {node.Host} when looking up {node.Alive}");
                    }

                    OtpInputStream ibuf = new OtpInputStream(tmpbuf);

                    int response = ibuf.Read1();
                    if (response == port4resp)
                    {
                        int result = ibuf.Read1();
                        if (result == 0)
                        {
                            port = ibuf.Read2BE();

                            node.Type = ibuf.Read1();
                            node.Proto = ibuf.Read1();
                            node.DistHigh = ibuf.Read2BE();
                            node.DistLow = ibuf.Read2BE();
                            // ignore rest of fields
                        }
                    }
                }
            }
            catch (SocketException e)
            {
                if (traceLevel >= traceThreshold)
                    log.Debug("<- (no response)");
                throw new IOException($"Nameserver not responding on {node.Host}", e);
            }
            catch (IOException e)
            {
                if (traceLevel >= traceThreshold)
                    log.Debug("<- (no response)");
                throw new IOException($"Nameserver not responding on {node.Host} when looking up {node.Alive}", e);
            }
            catch (OtpDecodeException e)
            {
                if (traceLevel >= traceThreshold)
                    log.Debug("<- (invalid response)");
                throw new IOException($"Nameserver invalid response on {node.Host} when looking up {node.Alive}", e);
            }

            if (traceLevel >= traceThreshold)
            {
                if (port == 0)
                    log.Debug("<- NOT FOUND");
                else
                    log.Debug($"<- PORT {port}");
            }

            return port;
        }

        /*
         * this function will get an exception if it tries to talk to a
         * very old epmd, or if something else happens that it cannot
         * forsee. In both cases we return an exception. We no longer
         * support r3, so the exception is fatal. If we manage to
         * successfully communicate with an r4 epmd, we return either the
         * socket, or null, depending on the result.
         */
        private static async Task<IOtpTransport> Publish_R4Async(OtpLocalNode node)
        {
            IOtpTransport s = null;

            try
            {
                OtpOutputStream obuf = new OtpOutputStream();
                s = node.CreateTransport(new IPEndPoint(IPAddress.Loopback, EpmdPort));

                obuf.Write2BE(node.Alive.Length + 13);

                obuf.Write1(ALIVE2_REQ);
                obuf.Write2BE(node.Port);

                obuf.Write1(node.Type);

                obuf.Write1(node.Proto);
                obuf.Write2BE(node.DistHigh);
                obuf.Write2BE(node.DistLow);

                obuf.Write2BE(node.Alive.Length);
                obuf.WriteN(Encoding.GetEncoding("ISO-8859-1").GetBytes(node.Alive));
                obuf.Write2BE(0); // No extra

                // send request
                await obuf.WriteToAsync(s.OutputStream);

                if (traceLevel >= traceThreshold)
                    log.Debug($"-> PUBLISH (r4) {node} port={node.Port}");

                // get reply
                byte[] tmpbuf = new byte[100];
                int n = await s.InputStream.ReadAsync(tmpbuf, 0, tmpbuf.Length);

                if (n < 0)
                {
                    OtpTransport.Close(s);
                    throw new IOException($"Nameserver not responding on {node.Host} when publishing {node.Alive}");
                }

                OtpInputStream ibuf = new OtpInputStream(tmpbuf);

                int response = ibuf.Read1();
                if (response == ALIVE2_RESP || response == ALIVE2_X_RESP)
                {
                    int result = ibuf.Read1();
                    if (result == 0)
                    {
                        node.Creation = (response == ALIVE2_RESP ? ibuf.Read2BE() : ibuf.Read4BE());
                        if (traceLevel >= traceThreshold)
                            log.Debug("<- OK");
                        return s; // success
                    }
                }
            }
            catch (SocketException e)
            {
                if (traceLevel >= traceThreshold)
                    log.Debug("<- (no response)");
                throw new IOException($"Nameserver not responding on {node.Host}", e);
            }
            catch (IOException e)
            {
                // epmd closed the connection = fail
                OtpTransport.Close(s);
                if (traceLevel >= traceThreshold)
                    log.Debug("<- (no response)");
                throw new IOException($"Nameserver not responding on {node.Host} when publishing {node.Alive}", e);
            }
            catch (OtpDecodeException e)
            {
                OtpTransport.Close(s);
                if (traceLevel >= traceThreshold)
                    log.Debug("<- (invalid response)");
                throw new IOException($"Nameserver invalid response on {node.Host} when publishing {node.Alive}", e);
            }

            OtpTransport.Close(s);
            return null;
        }

        public static NodeDetails[] LookupNames()
        {
            return LookupNames(Dns.GetHostAddresses(Dns.GetHostName())[0], new OtpSocketTransportFactory());
        }

        public static NodeDetails[] LookupNames(IPAddress address, IOtpTransportFactory transportFactory)
        {
            try
            {
                OtpOutputStream obuf = new OtpOutputStream();

                using (IOtpTransport s = transportFactory.CreateTransport(address.ToString(), EpmdPort))
                {
                    obuf.Write2BE(1);
                    obuf.Write1(names4req);
                    obuf.WriteTo(s.OutputStream);

                    if (traceLevel >= traceThreshold)
                        log.Debug("-> NAMES (r4) ");

                    OtpInputStream ibuf = new OtpInputStream(256);
                    byte[] buffer = new byte[256];
                    while (true)
                    {
                        int bytesRead = s.InputStream.Read(buffer, 0, buffer.Length);
                        if (bytesRead == 0)
                            break;
                        ibuf.Write(buffer, 0, bytesRead);
                    }
                    // Skip epmdport
                    ibuf.Position = 4;
                    // Remaining data is list of nodes
                    string nodes = ibuf.ReadStringData((int)(ibuf.Length - ibuf.Position));
                    var results = nodes.Split('\n').Where(r => !string.IsNullOrEmpty(r)).Select(response =>
                    {
                        var match = Regex.Match(response, @"name ([^ ]+) at port (\d+)");
                        if (!match.Success)
                            throw new OtpDecodeException("names response format invalid");
                        return new NodeDetails()
                        {
                            Alive = match.Success ? match.Groups[1].Value : "",
                            Port = int.Parse(match.Success ? match.Groups[2].Value : "0")
                        };
                    });
                    return results.ToArray();
                }
            }
            catch (SocketException e)
            {
                if (traceLevel >= traceThreshold)
                    log.Debug("<- (no response)");
                throw new IOException($"Nameserver not responding on {address}", e);
            }
            catch (IOException e)
            {
                if (traceLevel >= traceThreshold)
                    log.Debug("<- (no response)");
                throw new IOException("Nameserver not responding when requesting names", e);
            }
            catch (OtpDecodeException e)
            {
                if (traceLevel >= traceThreshold)
                    log.Debug("<- (invalid response)");
                throw new IOException("Nameserver invalid response when requesting names", e);
            }
        }
    }
}
