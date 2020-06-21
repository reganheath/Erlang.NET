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

namespace Erlang.NET
{
    /**
     * Provides a Java representation of Erlang PIDs. PIDs represent Erlang
     * processes and consist of a nodename and a number of integers.
     */
    [Serializable]
    public class OtpErlangPid : OtpErlangObject, IEquatable<OtpErlangPid>, IComparable<OtpErlangPid>, IComparable
    {
        public string Node { get; private set; }
        public int Id { get; private set; }
        public int Serial { get; private set; }
        public int Creation { get; private set; }


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
            OtpErlangPid p = buf.ReadPid();

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
        public OtpErlangPid(int tag, string node, int id, int serial, int creation)
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
        public override string ToString() => "#Pid<" + Node.ToString() + "." + Id + "." + Serial + ">";

        /**
         * Convert this PID to the equivalent Erlang external representation.
         * 
         * @param buf
         *                an output stream to which the encoded PID should be
         *                written.
         */
        public override void Encode(OtpOutputStream buf) => buf.WritePid(this);

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

        protected override int HashCode()
        {
            Hash hash = new Hash(5);
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
