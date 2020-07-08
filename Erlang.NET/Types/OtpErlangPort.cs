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
     * Provides a Java representation of Erlang ports.
     */
    [Serializable]
    public class OtpErlangPort : IOtpErlangObject, IEquatable<OtpErlangPort>, IComparable<OtpErlangPid>
    {
        public string Node { get; private set; }
        public int Id { get; private set; }
        public int Creation { get; private set; }

        /**
         * Create an Erlang port from a stream containing a port encoded in Erlang
         * external format.
         */
        public OtpErlangPort(OtpInputStream buf)
        {
            OtpErlangPort p = buf.ReadPort();

            Node = p.Node;
            Id = p.Id;
            Creation = p.Creation;
        }

        /**
         * Create an Erlang port from its components.
         */
        public OtpErlangPort(string node, int id, int creation)
            : this(OtpExternal.portTag, node, id, creation)
        {
        }

        /**
         * Create an Erlang port from its components.
         */
        public OtpErlangPort(int tag, string node, int id, int creation)
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
         * Convert this port to the equivalent Erlang external representation.
         */
        public void Encode(OtpOutputStream buf) => buf.WritePort(this);

        /**
         * Get the string representation of the port. 
         */
        public override string ToString() => $"#Port<{Node}.{Id}>";

        public int CompareTo(object obj) => CompareTo(obj as OtpErlangPort);

        public int CompareTo(OtpErlangPid other)
        {
            if (other is null)
                return 1;
            int res = Node.CompareTo(other.Node);
            if (res == 0)
                res = Id.CompareTo(other.Id);
            if (res == 0)
                res = Node.CompareTo(other.Node);
            return res;
        }

        /**
         * Determine if two ports are equal. Ports are equal if their components are
         * equal.
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

        public override int GetHashCode()
        {
            int hashCode = 2047075521;
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(Node);
            hashCode = hashCode * -1521134295 + Id.GetHashCode();
            hashCode = hashCode * -1521134295 + Creation.GetHashCode();
            return hashCode;
        }

        public object Clone() => new OtpErlangPort(OtpExternal.newPortTag, Node, Id, Creation);
    }
}
