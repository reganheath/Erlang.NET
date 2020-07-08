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

namespace Erlang.NET
{
    /**
     * Provides a Java representation of Erlang PIDs. PIDs represent Erlang
     * processes and consist of a nodename and a number of integers.
     */
    [Serializable]
    public class OtpErlangPid : IOtpErlangObject, IEquatable<OtpErlangPid>, IComparable<OtpErlangPid>
    {
        public string Node { get; private set; }
        public int Id { get; private set; }
        public int Serial { get; private set; }
        public int Creation { get; private set; }


        /**
         * Create an Erlang PID from a stream containing a PID encoded in Erlang
         * external format.
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
         */
        public OtpErlangPid(string node, int id, int serial, int creation)
            : this(OtpExternal.pidTag, node, id, serial, creation)
        {
        }

        /**
         * Create an Erlang pid from its components.
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
         * Convert this PID to the equivalent Erlang external representation.
         */
        public void Encode(OtpOutputStream buf) => buf.WritePid(this);

        /**
         * Get the string representation of the PID.
         */
        public override string ToString() => "#Pid<" + Node.ToString() + "." + Id + "." + Serial + ">";


        public int CompareTo(object o) => CompareTo(o as OtpErlangPid);

        public int CompareTo(OtpErlangPid other)
        {
            if (other is null)
                return 1;
            int res = Creation.CompareTo(other.Creation);
            if (res == 0)
                res = Serial.CompareTo(other.Serial);
            if (res == 0)
                res = Id.CompareTo(other.Id);
            if (res == 0)
                res = Node.CompareTo(other.Node);
            return res;
        }

        /**
         * Determine if two PIDs are equal. PIDs are equal if their components are
         * equal.
         */
        public override bool Equals(object o) => Equals(o as OtpErlangPid);

        public bool Equals(OtpErlangPid o)
        {
            if (o is null)
                return false;
            if (ReferenceEquals(this, o))
                return true;
            return Creation == o.Creation
                && Serial == o.Serial
                && Id == o.Id
                && Node == o.Node;
        }
        public override int GetHashCode()
        {
            int hashCode = 1695008970;
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(Node);
            hashCode = hashCode * -1521134295 + Id.GetHashCode();
            hashCode = hashCode * -1521134295 + Serial.GetHashCode();
            hashCode = hashCode * -1521134295 + Creation.GetHashCode();
            return hashCode;
        }

        public object Clone() => new OtpErlangPid(OtpExternal.newPidTag, Node, Id, Serial, Creation);
    }
}
