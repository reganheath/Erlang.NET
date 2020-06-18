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
     * Provides a Java representation of Erlang PIDs. PIDs represent Erlang
     * processes and consist of a nodename and a number of integers.
     */
    [Serializable]
    public class OtpErlangPid : OtpErlangObject, IEquatable<OtpErlangPid>, IComparable<OtpErlangPid>, IComparable
    {
        public int Tag {  get { return OtpExternal.newPidTag; } }
        public string Node { get; private set; }
        public int Id { get; private set; }
        public int Serial { get; private set; }
        public int Creation { get; private set; }

        /**
         * Create a unique Erlang PID belonging to the local node.
         * 
         * @param self
         *                the local node.
         * 
         * @deprecated use OtpLocalNode:createPid() instead
         */
        [Obsolete]
        public OtpErlangPid(OtpLocalNode self)
        {
            OtpErlangPid p = self.createPid();

            Id = p.Id;
            Serial = p.Serial;
            Creation = p.Creation;
            Node = p.Node;
        }

        /**
         * Create an Erlang PID from a stream containing a PID encoded in Erlang
         * external format.
         * 
         * @param buf
         *                the stream containing the encoded PID.
         * 
         * @exception OtpErlangDecodeException
         *                    if the buffer does not contain a valid external
         *                    representation of an Erlang PID.
         */
        public OtpErlangPid(OtpInputStream buf)
        {
            OtpErlangPid p = buf.read_pid();

            Node = p.Node;
            Id = p.Id;
            Serial = p.Serial;
            Creation = p.Creation;
        }

        /**
         * Create an Erlang pid from its components.
         * 
         * @param node
         *                the nodename.
         * 
         * @param id
         *                an arbitrary number. Only the low order 15 bits will be
         *                used.
         * 
         * @param serial
         *                another arbitrary number. Only the low order 13 bits will
         *                be used.
         * 
         * @param creation
         *                yet another arbitrary number. Only the low order 2 bits
         *                will be used.
         */
        public OtpErlangPid(string node, int id, int serial, int creation)
            : this(OtpExternal.pidTag, node, id, serial, creation)
        {
        }

        /**
         * Create an Erlang pid from its components.
         *
         * @param tag
         *            the external format to be compliant with
         *            OtpExternal.pidTag where only a subset of the bits are significant (see other constructor).
         *            OtpExternal.newPidTag where all 32 bits of id,serial and creation are significant.
         *            newPidTag can only be decoded by OTP-19 and newer.
         * @param node
         *            the nodename.
         *
         * @param id
         *            an arbitrary number.
         *
         * @param serial
         *            another arbitrary number.
         *
         * @param creation
         *            yet another arbitrary number.
         */
        public OtpErlangPid(int tag, String node, int id, int serial, int creation)
        {
            Node = node;
            if (tag == OtpExternal.pidTag)
            {
                Id = id & 0x7fff; // 15 bits
                Serial = serial & 0x1fff; // 13 bits
                Creation = creation & 0x03; // 2 bits
            }
            else
            {  // allow all 32 bits for newPidTag
                Id = id;
                Serial = serial;
                Creation = creation;
            }
        }

        /**
         * Get the string representation of the PID. Erlang PIDs are printed as
         * #Pid&lt;node.id.serial&gt;
         * 
         * @return the string representation of the PID.
         */
        public override string ToString()
        {
            return "#Pid<" + Node.ToString() + "." + Id + "." + Serial + ">";
        }

        /**
         * Convert this PID to the equivalent Erlang external representation.
         * 
         * @param buf
         *                an output stream to which the encoded PID should be
         *                written.
         */
        public override void Encode(OtpOutputStream buf)
        {
            buf.write_pid(this);
        }

        /**
         * Determine if two PIDs are equal. PIDs are equal if their components are
         * equal.
         * 
         * @param port
         *                the other PID to compare to.
         * 
         * @return true if the PIDs are equal, false otherwise.
         */
        public override bool Equals(object o) => Equals(o as OtpErlangPid);

        public bool Equals(OtpErlangPid o)
        {
            if (o == null)
                return false;
            if (ReferenceEquals(this, o))
                return true;
            return Creation == o.Creation 
                && Serial == o.Serial 
                && Id == o.Id
                && Node == o.Node;
        }

        public override int GetHashCode() => base.GetHashCode();

        protected override int DoHashCode()
        {
            OtpErlangObject.Hash hash = new OtpErlangObject.Hash(5);
            hash.Combine(Creation, Serial);
            hash.Combine(Id, Node.GetHashCode());
            return hash.ValueOf();
        }

        public int CompareTo(object o)
        {
            if (o == null)
                return -1;
            if (!(o is OtpErlangPid))
                return -1;
            return CompareTo(o as OtpErlangPid);
        }

        public int CompareTo(OtpErlangPid o)
        {
            if (o == null)
                return -1;
            if (Creation == o.Creation)
            {
                if (Serial == o.Serial)
                {
                    if (Id == o.Id)
                        return Node.CompareTo(o.Node);

                    return Id - o.Id;
                }

                return Serial - o.Serial;
            }

            return Creation - o.Creation;
        }
    }
}
