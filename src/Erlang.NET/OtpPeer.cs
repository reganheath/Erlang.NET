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
        /*
         * this is set by OtpConnection and is the highest
         * common protocol version we both support
         */
        public int DistChoose { get; set; } = 0;

        /*
         * Get the port number used by the remote node.
         */
        private int peerPort = 0;
        public override int Port
        {
            get
            {
                if (peerPort == 0)
                    peerPort = OtpEpmd.LookupPort(this);
                return peerPort;
            }
        }
    }
}
