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
namespace Erlang.NET
{
    /**
     * Represents a remote OTP node. It acts only as a container for the nodename
     * and other node-specific information that is needed by the
     * {@link OtpConnection} class.
     */
    public class OtpPeer : AbstractNode
    {
        public int DistChoose { get; set; } = 0;

        /*
         * this is set by OtpConnection and is the highest
         * common protocol version we both support
         */

        public OtpPeer(OtpTransportFactory transportFactory)
            : base(transportFactory)
        {
        }

        /**
         * Create a peer node.
         * 
         * @param node
         *                the name of the node.
         */
        public OtpPeer(string node)
            : base(node)
        {
        }

        /**
         * Create a peer node.
         * 
         * @param node
         *                the name of the node.
         */
        public OtpPeer(string node, OtpTransportFactory transportFactory)
            : base(node, transportFactory)
        {
        }

        // package
        /*
         * Get the port number used by the remote node.
         * 
         * @return the port number used by the remote node, or 0 if the node was not
         * registered with the port mapper.
         * 
         * @exception java.io.IOException if the port mapper could not be contacted.
         */
        public override int Port
        {
            get
            {
                if (base.Port == 0)
                    base.Port = OtpEpmd.LookupPort(this);
                return base.Port;
            }
        }
    }
}
