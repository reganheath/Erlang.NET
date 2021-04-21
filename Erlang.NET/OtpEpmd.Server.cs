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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace Erlang.NET
{
    public partial class OtpEpmd
    {
        private readonly object lockObj = new object();
        private readonly ConcurrentDictionary<string, AbstractNode> portmap = new ConcurrentDictionary<string, AbstractNode>();

        private IOtpServerTransport listen;
        private volatile bool stopping;
        private int creation = 0;

        public OtpEpmd()
        {
        }

        private int NextCreation()
        {
            lock (lockObj)
                return (creation++ % 3) + 1;
        }

        public async void ServeAsync()
        {
            stopping = false;
            listen = new OtpServerSocketTransport(EpmdPort, false);
            listen.Start();
            while (!stopping)
            {
                try
                {
                    var socket = await listen.AcceptAsync();
                    HandleConnectionAsync(socket);
                }
                catch(Exception) { }
            }
        }

        public void Stop()
        {
            stopping = true;
            OtpTransport.Close(listen);
        }

        private async void HandleConnectionAsync(IOtpTransport socket)
        {
            Logger.Info($"[OtpEpmd] connected {socket}");
            List<string> publishedNodes = new List<string>();

            try
            {
                while (true)
                {
                    var ibuf = await ReadRequestAsync(socket);
                    int request = ibuf.Read1();

                    // This request retains the connection
                    if (request == ALIVE2_REQ)
                    {
                        publishedNodes.Add(await Publish_R4(socket, ibuf));
                        continue;
                    }

                    // These requests terminate the connection
                    if (request == stopReq) { }
                    else if (request == port4req)
                        await Port_R4(socket, ibuf);
                    else if (request == names4req)
                        await Names_R4(socket);
                    else
                        Logger.Info($"[OtpEpmd] Unknown request (request={request}, length={ibuf.Length}) from {socket}");
                    break;
                }
            }
            catch (Exception e)
            {
                Logger.Error($"[OtpEpmd] socket {socket} error {e}");
            }

            Logger.Info($"[OtpEpmd] closing {socket}");
            OtpTransport.Close(socket);

            foreach (var name in publishedNodes)
                portmap.TryRemove(name, out _);
        }

        internal async Task<OtpInputStream> ReadRequestAsync(IOtpTransport sock)
        {
            OtpInputStream ibuf = await sock.ReadAsync(2);
            ibuf = await sock.ReadAsync(ibuf.Read2BE());
            return ibuf;
        }

        private async Task<string> Publish_R4(IOtpTransport socket, OtpInputStream ibuf)
        {
            try
            {
                int port = ibuf.Read2BE();
                int type = ibuf.Read1();
                int proto = ibuf.Read1();
                int distHigh = ibuf.Read2BE();
                int distLow = ibuf.Read2BE();
                string name = ibuf.ReadStringData();
                ibuf.ReadStringData(); // extra
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
                    Logger.Debug($"<- PUBLISH (r4) {name} port={node.Port}");

                OtpOutputStream obuf = new OtpOutputStream();
                obuf.Write1(ALIVE2_RESP);
                obuf.Write1(0);
                obuf.Write2BE(NextCreation());
                await obuf.WriteToAsync(socket.OutputStream);

                portmap.TryAdd(name, node);
                return name;
            }
            catch (IOException e)
            {
                if (traceLevel >= traceThreshold)
                    Logger.Debug("<- (no response)");
                throw new IOException("Request not responding", e);
            }
        }

        private async Task Port_R4(IOtpTransport socket, OtpInputStream ibuf)
        {
            try
            {
                int len = (int)(ibuf.Length - 1);
                byte[] alive = ibuf.ReadN(len);
                
                string name = OtpErlangString.FromEncoding(alive);                
                if (traceLevel >= traceThreshold)
                    Logger.Debug($"<- PORT (r4) {name}");

                if (!portmap.TryGetValue(name, out AbstractNode node))
                    node = null;

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
                    if (traceLevel >= traceThreshold)
                        Logger.Debug("-> 0 (success)");
                }
                else
                {
                    obuf.Write1(port4resp);
                    obuf.Write1(1);
                    if (traceLevel >= traceThreshold)
                        Logger.Debug("-> 1 (failure)");
                }

                await obuf.WriteToAsync(socket.OutputStream);
            }
            catch (IOException e)
            {
                if (traceLevel >= traceThreshold)
                    Logger.Debug("<- (no response)");
                throw new IOException("Request not responding", e);
            }
        }

        private async Task Names_R4(IOtpTransport socket)
        {
            try
            {
                if (traceLevel >= traceThreshold)
                    Logger.Debug("<- NAMES(r4) ");

                OtpOutputStream obuf = new OtpOutputStream();
                obuf.Write4BE(EpmdPort);
                foreach (var node in portmap.Values)
                {
                    byte[] bytes = Encoding.GetEncoding("ISO-8859-1").GetBytes($"name {node.Alive} at port {node.Port}\n");
                    obuf.WriteN(bytes);
                    Logger.Debug($"-> name {node.Alive} at port {node.Port}");
                }

                await obuf.WriteToAsync(socket.OutputStream);
            }
            catch (IOException e)
            {
                if (traceLevel >= traceThreshold)
                    Logger.Debug("<- (no response)");
                throw new IOException("Request not responding", e);
            }
        }
    }
}
