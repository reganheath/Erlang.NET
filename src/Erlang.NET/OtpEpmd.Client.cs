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
using System.Configuration;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using log4net;

namespace Erlang.NET
{
    /**
     * Provides methods for registering, unregistering and looking up nodes with the
     * Erlang portmapper daemon (Epmd). For each registered node, Epmd maintains
     * information about the port on which incoming connections are accepted, as
     * well as which versions of the Erlang communication protocol the node
     * supports.
     * 
     * <p>
     * Nodes wishing to contact other nodes must first request information from Epmd
     * before a connection can be set up, however this is done automatically by
     * {@link OtpSelf#connect(OtpPeer) OtpSelf.connect()} when necessary.
     * 
     * <p>
     * The methods {@link #publishPort(OtpLocalNode) publishPort()} and
     * {@link #unPublishPort(OtpLocalNode) unPublishPort()} will fail if an Epmd
     * process is not running on the localhost. Additionally
     * {@link #lookupPort(AbstractNode) lookupPort()} will fail if there is no Epmd
     * process running on the host where the specified node is running. See the
     * Erlang documentation for information about starting Epmd.
     * 
     * <p>
     * This class contains only static methods, there are no constructors.
     */
    public partial class OtpEpmd : ThreadBase
    {
        private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private static class EpmdPort
        {
            private static int epmdPort = 0;

            public static int get()
            {
                if (epmdPort == 0)
                {
                    String env = null;
                    try
                    {
                        env = System.Environment.GetEnvironmentVariable("ERL_EPMD_PORT");
                        epmdPort = (env != null) ? Int32.Parse(env) : 4369;
                    }
                    catch (System.Security.SecurityException)
                    {
                        env = null;
                    }
                    catch (FormatException)
                    {
                        env = null;
                    }
                }
                return epmdPort;
            }

            public static void set(int port)
            {
                epmdPort = port;
            }
        }
        // common values
        private const byte stopReq = (byte)115;

        // version specific value
        private const byte port4req = (byte)122;
        private const byte port4resp = (byte)119;
        private const byte ALIVE2_REQ = (byte)120;
        private const byte ALIVE2_RESP = (byte)121;
        private const byte ALIVE2_X_RESP = (byte)118;
        private const byte names4req = (byte)110;

        private static int traceLevel = 0;
        private const int traceThreshold = 4;

        static OtpEpmd()
        {
            // debug this connection?
            String trace = ConfigurationManager.AppSettings["OtpConnection.trace"];

            try
            {
                if (trace != null)
                {
                    traceLevel = Int32.Parse(trace);
                }
            }
            catch (FormatException)
            {
                traceLevel = 0;
            }
        }

        /**
         * Set the port number to be used to contact the epmd process.
         * Only needed when the default port is not desired and system environment
         * variable ERL_EPMD_PORT can not be read (applet).
         */
        public static void useEpmdPort(int port)
        {
            EpmdPort.set(port);
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
        public static int lookupPort(AbstractNode node)
        {
            return r4_lookupPort(node);
        }

        /**
         * Register with Epmd, so that other nodes are able to find and connect to
         * it.
         * 
         * @param node
         *            the server node that should be registered with Epmd.
         * 
         * @return true if the operation was successful. False if the node was
         *         already registered.
         * 
         * @exception java.io.IOException
         *                if there was no response from the name server.
         */
        public static bool publishPort(OtpLocalNode node)
        {
            OtpTransport s = r4_publish(node);
            node.setEpmd(s);
            return s != null;
        }

        // Ask epmd to close his end of the connection.
        // Caller should close his epmd socket as well.
        // This method is pretty forgiving...
        /**
         * Unregister from Epmd. Other nodes wishing to connect will no longer be
         * able to.
         * 
         * <p>
         * This method does not report any failures.
         */
        public static void unPublishPort(OtpLocalNode node)
        {
            try
            {
                using (OtpTransport s = node.createTransport(Dns.GetHostName(), EpmdPort.get()))
                {
                    OtpOutputStream obuf = new OtpOutputStream();
                    obuf.write2BE(node.Alive.Length + 1);
                    obuf.write1(stopReq);
                    obuf.writeN(Encoding.GetEncoding("iso-8859-1").GetBytes(node.Alive));
                    obuf.WriteTo(s.getOutputStream());
                    // don't even wait for a response (is there one?)
                    if (traceLevel >= traceThreshold)
                    {
                        log.Debug("-> UNPUBLISH " + node + " port=" + node.Port);
                        log.Debug("<- OK (assumed)");
                    }
                }
            }
            catch (Exception) /* ignore all failures */
            {
            }
        }

        private static int r4_lookupPort(AbstractNode node)
        {
            int port = 0;

            try
            {
                OtpOutputStream obuf = new OtpOutputStream();
                using (TcpClient s = new TcpClient(node.Host, EpmdPort.get()))
                {
                    // build and send epmd request
                    // length[2], tag[1], alivename[n] (length = n+1)
                    obuf.write2BE(node.Alive.Length + 1);
                    obuf.write1(port4req);
                    obuf.writeN(Encoding.GetEncoding("iso-8859-1").GetBytes(node.Alive));

                    // send request
                    obuf.WriteTo(s.GetStream());

                    if (traceLevel >= traceThreshold)
                        log.Debug("-> LOOKUP (r4) " + node);

                    // receive and decode reply
                    // resptag[1], result[1], port[2], ntype[1], proto[1],
                    // disthigh[2], distlow[2], nlen[2], alivename[n],
                    // elen[2], edata[m]
                    byte[] tmpbuf = new byte[100];

                    int n = s.GetStream().Read(tmpbuf, 0, tmpbuf.Length);

                    if (n < 0)
                    {
                        s.Close();
                        throw new IOException("Nameserver not responding on " + node.Host + " when looking up " + node.Alive);
                    }

                    OtpInputStream ibuf = new OtpInputStream(tmpbuf, 0);

                    int response = ibuf.read1();
                    if (response == port4resp)
                    {
                        int result = ibuf.read1();
                        if (result == 0)
                        {
                            port = ibuf.read2BE();

                            node.Type = ibuf.read1();
                            node.Proto = ibuf.read1();
                            node.DistHigh = ibuf.read2BE();
                            node.DistLow = ibuf.read2BE();
                            // ignore rest of fields
                        }
                    }
                }
            }
            catch (SocketException e)
            {
                if (traceLevel >= traceThreshold)
                    log.Debug("<- (no response)");
                throw new IOException("Nameserver not responding on " + node.Host, e);
            }
            catch (IOException e)
            {
                if (traceLevel >= traceThreshold)
                    log.Debug("<- (no response)");
                throw new IOException("Nameserver not responding on " + node.Host + " when looking up " + node.Alive, e);
            }
            catch (OtpErlangDecodeException e)
            {
                if (traceLevel >= traceThreshold)
                    log.Debug("<- (invalid response)");
                throw new IOException("Nameserver invalid response on " + node.Host + " when looking up " + node.Alive, e);
            }

            if (traceLevel >= traceThreshold)
            {
                if (port == 0)
                    log.Debug("<- NOT FOUND");
                else
                    log.Debug("<- PORT " + port);
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
        private static OtpTransport r4_publish(OtpLocalNode node)
        {
            OtpTransport s = null;

            try
            {
                OtpOutputStream obuf = new OtpOutputStream();
                s = node.createTransport(new IPEndPoint(IPAddress.Loopback, EpmdPort.get()));

                obuf.write2BE(node.Alive.Length + 13);

                obuf.write1(ALIVE2_REQ);
                obuf.write2BE(node.Port);

                obuf.write1(node.Type);

                obuf.write1(node.Proto);
                obuf.write2BE(node.DistHigh);
                obuf.write2BE(node.DistLow);

                obuf.write2BE(node.Alive.Length);
                obuf.writeN(Encoding.GetEncoding("iso-8859-1").GetBytes(node.Alive));
                obuf.write2BE(0); // No extra

                // send request
                obuf.WriteTo(s.getOutputStream());

                if (traceLevel >= traceThreshold)
                    log.Debug("-> PUBLISH (r4) " + node + " port=" + node.Port);

                // get reply
                byte[] tmpbuf = new byte[100];
                int n = s.getInputStream().Read(tmpbuf, 0, tmpbuf.Length);

                if (n < 0)
                {
                    s.close();
                    throw new IOException("Nameserver not responding on " + node.Host + " when publishing " + node.Alive);
                }

                OtpInputStream ibuf = new OtpInputStream(tmpbuf, 0);

                int response = ibuf.read1();
                if (response == ALIVE2_RESP || response == ALIVE2_X_RESP)
                {
                    int result = ibuf.read1();
                    if (result == 0)
                    {
                        node.Creation = (response == ALIVE2_RESP ? ibuf.read2BE() : ibuf.read4BE());
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
                throw new IOException("Nameserver not responding on " + node.Host, e);
            }
            catch (IOException e)
            {
                // epmd closed the connection = fail
                if (s != null)
                    s.close();
                if (traceLevel >= traceThreshold)
                    log.Debug("<- (no response)");
                throw new IOException("Nameserver not responding on " + node.Host + " when publishing " + node.Alive, e);
            }
            catch (OtpErlangDecodeException e)
            {
                s.close();
                if (traceLevel >= traceThreshold)
                    log.Debug("<- (invalid response)");
                throw new IOException("Nameserver invalid response on " + node.Host + " when publishing " + node.Alive, e);
            }

            s.close();
            return null;
        }

        public static String[] lookupNames()
        {
            return lookupNames(Dns.GetHostAddresses(Dns.GetHostName())[0], new OtpSocketTransportFactory());
        }

        public static String[] lookupNames(IPAddress address, OtpTransportFactory transportFactory)
        {

            try
            {
                OtpOutputStream obuf = new OtpOutputStream();

                using (OtpTransport s = transportFactory.createTransport(address.ToString(), EpmdPort.get()))
                {
                    obuf.write2BE(1);
                    obuf.write1(names4req);
                    // send request
                    obuf.WriteTo(s.getOutputStream());

                    if (traceLevel >= traceThreshold)
                        log.Debug("-> NAMES (r4) ");

                    // get reply
                    byte[] buffer = new byte[256];
                    MemoryStream ms = new MemoryStream(256);
                    while (true)
                    {
                        int bytesRead = s.getInputStream().Read(buffer, 0, buffer.Length);
                        if (bytesRead == -1)
                        {
                            break;
                        }
                        ms.Write(buffer, 0, bytesRead);
                    }
                    byte[] tmpbuf = ms.GetBuffer();
                    OtpInputStream ibuf = new OtpInputStream(tmpbuf, 0);
                    ibuf.read4BE(); // read port int
                    // int port = ibuf.read4BE();
                    // check if port = epmdPort

                    int n = tmpbuf.Length;
                    byte[] buf = new byte[n - 4];
                    Array.Copy(tmpbuf, 4, buf, 0, n - 4);
                    String all = OtpErlangString.newString(buf);
                    return all.Split(new char[] { '\n' });
                }
            }
            catch (SocketException e) 
            {
                if (traceLevel >= traceThreshold)
                    log.Debug("<- (no response)");
                throw new IOException("Nameserver not responding on " + address, e);
            }
            catch (IOException e)
            {
                if (traceLevel >= traceThreshold)
                    log.Debug("<- (no response)");
                throw new IOException("Nameserver not responding when requesting names", e);
            }
            catch (OtpErlangDecodeException e)
            {
                if (traceLevel >= traceThreshold)
                    log.Debug("<- (invalid response)");
                throw new IOException("Nameserver invalid response when requesting names", e);
            }
        }
    }
}
