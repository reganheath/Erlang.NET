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
using System.Linq;

namespace Erlang.NET
{
    /**
     * Provides a Java representation of Erlang refs. There are two styles of Erlang
     * refs, old style (one id value) and new style (array of id values). This class
     * manages both types.
     */
    [Serializable]
    public class OtpErlangRef : IOtpErlangObject, IEquatable<OtpErlangRef>, IComparable<OtpErlangRef>
    {
        public string Node { get; private set; }
        public int Creation { get; private set; }

        /**
         * Determine whether this is a new style ref.
         */
        public bool IsNewRef() => Ids.Length > 1;

        /**
         * old style refs have one 18-bit id
         * r6 "new" refs have array of ids, first one is only 18 bits however
         */
        public int[] Ids { get; private set; }

        /**
         * Get the id number from the ref. Old style refs have only one id number.
         * If this is a new style ref, the first id number is returned.
         */
        public int Id => Ids[0];


        /**
         * Create an Erlang ref from a stream containing a ref encoded in Erlang
         * external format.
         */
        public OtpErlangRef(OtpInputStream buf)
        {
            OtpErlangRef r = buf.ReadRef();

            Node = r.Node;
            Creation = r.Creation;
            Ids = r.Ids;
        }

        /**
         * Create an old style Erlang ref from its components.
         */
        public OtpErlangRef(string node, int id, int creation)
            : this(OtpExternal.newRefTag, node, new int[1] { id }, creation)
        {
        }

        /**
         * Create a new style Erlang ref from its components.
         */
        public OtpErlangRef(string node, int[] ids, int creation)
            : this(OtpExternal.newRefTag, node, ids, creation)
        {
        }

        public OtpErlangRef(int tag, string node, int[] ids, int creation)
        {
            Node = node;

            // use at most 3 words
            int len = Math.Min(ids.Length, 3);
            if (len < 1)
                throw new Exception("Ref must contain at least 1 id");
            Ids = new int[len];

            Array.Copy(ids, 0, Ids, 0, len);
            if (tag == OtpExternal.newRefTag)
            {
                Creation = creation & 0x3;
                Ids[0] &= 0x3ffff; // only 18 significant bits in first number
            }
            else
            {
                Creation = creation;
            }
        }

        /**
         * Convert this ref to the equivalent Erlang external representation.
         */
        public void Encode(OtpOutputStream buf) => buf.WriteRef(this);

        /**
         * Get the string representation of the ref. 
         */
        public override string ToString() => $"#Ref<{Node}.{string.Join(".", Ids)}>";

        public int CompareTo(object obj) => CompareTo(obj as OtpErlangPort);

        public int CompareTo(OtpErlangRef other)
        {
            if (other is null)
                return 1;
            int res = Node.CompareTo(other.Node);
            if (res == 0)
                res = Ids.CompareTo(other.Ids);
            if (res == 0)
                res = Node.CompareTo(other.Node);
            return res;
        }

        /**
         * Determine if two refs are equal. Refs are equal if their components are
         * equal. New refs and old refs are considered equal if the node, creation
         * and first id numnber are equal.
         */
        public override bool Equals(object o) => Equals(o as OtpErlangRef);

        public bool Equals(OtpErlangRef o)
        {
            if (o == null)
                return false;
            if (ReferenceEquals(this, o))
                return true;
            //This was the jinterface implementation .. but it just seems wrong?!
            //if (!(Node.Equals(o.Node) && Creation == o.Creation))
            //    return false;
            //  replacement
            if (!Node.Equals(o.Node))
                return false;
            if (Creation != o.Creation)
                return false;
            // /replacement
            if (IsNewRef() && o.IsNewRef())
                return Ids.SequenceEqual(o.Ids);
            return Ids[0] == o.Ids[0];
        }

        /**
         * Compute the hashCode value for a given ref. This function is compatible
         * with equal.
         **/
        public override int GetHashCode()
        {
            int hashCode = -1483511190;
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(Node);
            hashCode = hashCode * -1521134295 + Creation.GetHashCode();
            hashCode = hashCode * -1521134295 + EqualityComparer<int[]>.Default.GetHashCode(Ids);
            return hashCode;
        }

        public object Clone() => new OtpErlangRef(OtpExternal.newerRefTag, Node, Ids, Creation);
    }
}
