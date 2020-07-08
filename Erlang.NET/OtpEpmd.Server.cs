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
using System.Text;
using System.Threading;

namespace Erlang.NET
{
    public partial class OtpEpmd : ThreadBase
    {
        private readonly object lockObj = new object();
        private readonly IOtpServerTransport sock;
        private int creation = 0;

        public Dictionary<string, AbstractNode> Portmap { get; } = new Dictionary<string, AbstractNode>();

        public OtpEpmd()
              : base("OtpEpmd", true)
        {
            sock = new OtpServerSocketTransport(EpmdPort, false);
        }

        private int NextCreation()
        {
            lock (lockObj)
                return (creation++ % 3) + 1;
        }

        public override void Start()
        {
            sock.Start();
            base.Start();
        }

        public void Quit()
        {
            Stop();
            OtpTransport.Close(sock);
        }

        public override void Run()
        {
            log.InfoFormat($"[OtpEpmd] start at port {EpmdPort}");

            while (!Stopping)
            {
                try
                {
                    IOtpTransport newsock = sock.Accept();
                    OtpEpmdConnection conn = new OtpEpmdConnection(this, newsock);
                    conn.Start();
                }
                catch (Exception)
                {
                }
            }
            return;
        }

        private class OtpEpmdConnection : ThreadBase
        {
            private readonly OtpEpmd epmd;
            private readonly Dictionary<string, AbstractNode> portmap;
            private readonly List<string> publishedPort = new List<string>();
            private readonly IOtpTransport sock;

            public OtpEpmdConnection(OtpEpmd epmd, IOtpTransport sock)
                : base("OtpEpmd.OtpEpmdConnection", true)
            {
                this.epmd = epmd;
                portmap = epmd.Portmap;
                this.sock = sock;
            }

            private int ReadSock(IOtpTransport s, byte[] b)
            {
                int got = 0;
                int len = b.Length;
                int i;
                Stream st = s.InputStream;

                while (got < len)
                {
                    try
                    {
                        i = st.Read(b, got, len - got);
                        if (i < 0)
                            throw new IOException($"expected {len} bytes, got EOF after {got} bytes");
                        if (i == 0 && len != 0)
                            throw new IOException("Remote connection closed");
                        got += i;
                    }
                    catch (IOException e)
                    {
                        throw new IOException("Read failed", e);
                    }
                    catch (ObjectDisposedException e)
                    {
                        throw new IOException("Read failed", e);
                    }
                }

                return got;
            }

            private void Quit()
            {
                Stop();
                OtpTransport.Close(sock);

                foreach (string name in publishedPort)
                {
                    lock (portmap)
                    {
                        if (portmap.ContainsKey(name))
                            portmap.Remove(name);
                    }
                }
                publishedPort.Clear();
            }

            private void Publish_R4(IOtpTransport s, OtpInputStream ibuf)
            {
                try
                {
                    int port = ibuf.Read2BE();
                    int type = ibuf.Read1();
                    int proto = ibuf.Read1();
                    int distHigh = ibuf.Read2BE();
                    int distLow = ibuf.Read2BE();
                    int len = ibuf.Read2BE();
                    byte[] alive = new byte[len];
                    ibuf.ReadN(alive);
                    int elen = ibuf.Read2BE();
                    byte[] extra = new byte[elen];
                    ibuf.ReadN(extra);
                    string name = OtpErlangString.FromEncoding(alive);
                    AbstractNode node = new AbstractNode()
                    {
                        Node = name,
                        Port = port,
                        Type = type,
                        DistHigh = distHigh,
                        DistLow = distLow,
                        Proto = proto
                    };

                    if (traceLevel >= traceThreshold)
                        log.Debug($"<- PUBLISH (r4) {name} port={node.Port}");

                    OtpOutputStream obuf = new OtpOutputStream();
                    obuf.Write1(ALIVE2_RESP);
                    obuf.Write1(0);
                    obuf.Write2BE(epmd.NextCreation());
                    obuf.WriteTo(s.OutputStream);

                    lock (portmap)
                        portmap.Add(name, node);
                    publishedPort.Add(name);
                }
                catch (IOException e)
                {
                    if (traceLevel >= traceThreshold)
                        log.Debug("<- (no response)");
                    throw new IOException("Request not responding", e);
                }
                return;
            }

            private void Port_R4(IOtpTransport s, OtpInputStream ibuf)
            {
                try
                {
                    int len = (int)(ibuf.Length - 1);
                    byte[] alive = new byte[len];
                    ibuf.ReadN(alive);
                    string name = OtpErlangString.FromEncoding(alive);
                    AbstractNode node = null;

                    if (traceLevel >= traceThreshold)
                        log.Debug($"<- PORT (r4) {name}");

                    lock (portmap)
                    {
                        if (portmap.ContainsKey(name))
                            node = portmap[name];
                    }

                    OtpOutputStream obuf = new OtpOutputStream();
                    if (node != null)
                    {
                        obuf.Write1(port4resp);
                        obuf.Write1(0);
                        obuf.Write2BE(node.Port);
                        obuf.Write1(node.Type);
                        obuf.Write1(node.Proto);
                        obuf.Write2BE(node.DistHigh);
                        obuf.Write2BE(node.DistLow);
                        obuf.Write2BE(len);
                        obuf.WriteN(alive);
                        obuf.Write2BE(0);
                    }
                    else
                    {
                        obuf.Write1(port4resp);
                        obuf.Write1(1);
                    }
                    obuf.WriteTo(s.OutputStream);
                }
                catch (IOException e)
                {
                    if (traceLevel >= traceThreshold)
                        log.Debug("<- (no response)");
                    throw new IOException("Request not responding", e);
                }
                return;
            }

            private void Names_R4(IOtpTransport s, OtpInputStream ibuf)
            {
                try
                {
                    if (traceLevel >= traceThreshold)
                    {
                        log.Debug("<- NAMES(r4) ");
                    }

                    OtpOutputStream obuf = new OtpOutputStream();
                    obuf.Write4BE(EpmdPort);
                    lock (portmap)
                    {
                        foreach (KeyValuePair<string, AbstractNode> pair in portmap)
                        {
                            AbstractNode node = pair.Value;
                            string info = $"name {node.Alive} at port {node.Port}\n";
                            byte[] bytes = Encoding.GetEncoding("ISO-8859-1").GetBytes(info);
                            obuf.WriteN(bytes);
                        }
                    }
                    obuf.WriteTo(s.OutputStream);
                }
                catch (IOException e)
                {
                    if (traceLevel >= traceThreshold)
                        log.Debug("<- (no response)");
                    throw new IOException("Request not responding", e);
                }
                return;
            }

            public override void Run()
            {
                byte[] lbuf = new byte[2];
                OtpInputStream ibuf;
                int len;

                try
                {
                    while (!Stopping)
                    {
                        ReadSock(sock, lbuf);
                        ibuf = new OtpInputStream(lbuf);
                        len = ibuf.Read2BE();
                        byte[] tmpbuf = new byte[len];
                        ReadSock(sock, tmpbuf);
                        ibuf = new OtpInputStream(tmpbuf);

                        int request = ibuf.Read1();
                        switch (request)
                        {
                            case ALIVE2_REQ:
                                Publish_R4(sock, ibuf);
                                break;

                            case port4req:
                                Port_R4(sock, ibuf);
                                this.Stop();
                                break;

                            case names4req:
                                Names_R4(sock, ibuf);
                                this.Stop();
                                break;

                            case stopReq:
                                break;

                            default:
                                log.InfoFormat($"[OtpEpmd] Unknown request (request={request}, length={len}) from {sock}");
                                break;
                        }
                    }
                }
                catch (IOException)
                {
                }
                finally
                {
                    Quit();
                }
            }
        }
    }
}
