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

namespace Erlang.NET
{
    /**
     * Provides a Java representation of Erlang ports.
     */
    [Serializable]
    public class OtpErlangPort : OtpErlangObject, IEquatable<OtpErlangPort>
    {
        public int Tag { get { return OtpExternal.newPortTag; } }
        public string Node { get; private set; }
        public int Id { get; private set; }
        public int Creation { get; private set; }

        /**
         * Create an Erlang port from a stream containing a port encoded in Erlang
         * external format.
         * 
         * @param buf
         *                the stream containing the encoded port.
         * 
         * @exception OtpErlangDecodeException
         *                    if the buffer does not contain a valid external
         *                    representation of an Erlang port.
         */
        public OtpErlangPort(OtpInputStream buf)
        {
            OtpErlangPort p = buf.read_port();

            Node = p.Node;
            Id = p.Id;
            Creation = p.Creation;
        }

        /**
         * Create an Erlang port from its components.
         * 
         * @param node
         *                the nodename.
         * 
         * @param id
         *                an arbitrary number. Only the low order 28 bits will be
         *                used.
         * 
         * @param creation
         *                another arbitrary number. Only the low order 2 bits will
         *                be used.
         */
        public OtpErlangPort(string node, int id, int creation)
            : this(OtpExternal.portTag, node, id, creation)
        {
        }

        /**
         * Create an Erlang port from its components.
         *
         * @param tag
         *            the external format to be compliant with.
         *            OtpExternal.portTag where only a subset of the bits are used (see other constructor)
         *            OtpExternal.newPortTag where all 32 bits of id and creation are significant.
         *            newPortTag can only be decoded by OTP-19 and newer.
         * @param node
         *            the nodename.
         *
         * @param id
         *            an arbitrary number. Only the low order 28 bits will be used.
         *
         * @param creation
         *            another arbitrary number.
         */
        public OtpErlangPort(int tag, String node, int id, int creation)
        {
            Node = node;
            if (tag == OtpExternal.portTag)
            {
                Id = id & 0xfffffff; // 28 bits
                Creation = creation & 0x3; // 2 bits
            }
            else
            {
                Id = id;
                Creation = creation;
            }
        }

        /**
         * Get the string representation of the port. Erlang ports are printed as
         * #Port&lt;node.id&gt;.
         * 
         * @return the string representation of the port.
         */
        public override string ToString()
        {
            return "#Port<" + Node + "." + Id + ">";
        }

        /**
         * Convert this port to the equivalent Erlang external representation.
         * 
         * @param buf
         *                an output stream to which the encoded port should be
         *                written.
         */
        public override void Encode(OtpOutputStream buf)
        {
            buf.write_port(this);
        }

        /**
         * Determine if two ports are equal. Ports are equal if their components are
         * equal.
         * 
         * @param o
         *                the other port to compare to.
         * 
         * @return true if the ports are equal, false otherwise.
         */
        public override bool Equals(object o) => Equals(o as OtpErlangPort);

        public bool Equals(OtpErlangPort o)
        { 
            if (o == null)
                return false;
            if (ReferenceEquals(this, o))
                return true;
            return Creation == o.Creation 
                && Id == o.Id
                && Node.Equals(o.Node);
        }

        public override int GetHashCode() => base.GetHashCode();

        protected override int DoHashCode()
        {
            OtpErlangObject.Hash hash = new OtpErlangObject.Hash(6);
            hash.Combine(Creation);
            hash.Combine(Id, Node.GetHashCode());
            return hash.ValueOf();
        }
    }
}
