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
using System.Linq;

namespace Erlang.NET
{
    /**
     * Provides a Java representation of Erlang refs. There are two styles of Erlang
     * refs, old style (one id value) and new style (array of id values). This class
     * manages both types.
     */
    [Serializable]
    public class OtpErlangRef : OtpErlangObject, IEquatable<OtpErlangRef>
    {
        public string Node { get; private set; }
        public int Creation { get; private set; }

        // old style refs have one 18-bit id
        // r6 "new" refs have array of ids, first one is only 18 bits however
        public int[] Ids { get; private set; }

        /**
         * Get the id number from the ref. Old style refs have only one id number.
         * If this is a new style ref, the first id number is returned.
         * 
         * @return the id number from the ref.
         */
        public int Id => Ids[0];

        /**
         * Determine whether this is a new style ref.
         * 
         * @return true if this ref is a new style ref, false otherwise.
         */
        public bool IsNewRef() => Ids.Length > 1;

        /**
         * Create an Erlang ref from a stream containing a ref encoded in Erlang
         * external format.
         * 
         * @param buf
         *                the stream containing the encoded ref.
         * 
         * @exception OtpErlangDecodeException
         *                    if the buffer does not contain a valid external
         *                    representation of an Erlang ref.
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
         * 
         * @param node
         *                the nodename.
         * 
         * @param id
         *                an arbitrary number. Only the low order 18 bits will be
         *                used.
         * 
         * @param creation
         *                another arbitrary number. Only the low order 2 bits will
         *                be used.
         */
        public OtpErlangRef(string node, int id, int creation)
            : this(OtpExternal.newRefTag, node, new int[1] { id }, creation)
        {
        }

        /**
         * Create a new style Erlang ref from its components.
         * 
         * @param node
         *                the nodename.
         * 
         * @param ids
         *                an array of arbitrary numbers. Only the low order 18 bits
         *                of the first number will be used. If the array contains
         *                only one number, an old style ref will be written instead.
         *                At most three numbers will be read from the array.
         * 
         * @param creation
         *                another arbitrary number. Only the low order 2 bits will
         *                be used.
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
         * Get the string representation of the ref. Erlang refs are printed as
         * #Ref&lt;node.id&gt;
         * 
         * @return the string representation of the ref.
         */
        public override string ToString() => "#Ref<" + Node + "." + string.Join(".", Ids) + ">";

        /**
         * Convert this ref to the equivalent Erlang external representation.
         * 
         * @param buf
         *                an output stream to which the encoded ref should be
         *                written.
         */
        public override void Encode(OtpOutputStream buf) => buf.WriteRef(this);

        /**
         * Determine if two refs are equal. Refs are equal if their components are
         * equal. New refs and old refs are considered equal if the node, creation
         * and first id numnber are equal.
         * 
         * @param o
         *                the other ref to compare to.
         * 
         * @return true if the refs are equal, false otherwise.
         */
        public override bool Equals(object o) => Equals(o as OtpErlangRef);

        public bool Equals(OtpErlangRef o)
        {
            if (o == null)
                return false;
            if (ReferenceEquals(this, o))
                return true;
            if (!(Node.Equals(o.Node) && Creation == o.Creation))
                return false;
            if (IsNewRef() && o.IsNewRef())
                return Ids.SequenceEqual(o.Ids);
            return Ids[0] == o.Ids[0];
        }

        public override int GetHashCode() => base.GetHashCode();

        /**
         * Compute the hashCode value for a given ref. This function is compatible
         * with equal.
         *
         * @return the hashCode of the node.
         **/
        protected override int HashCode()
        {
            Hash hash = new Hash(7);
            hash.Combine(Creation, Ids[0]);
            if (IsNewRef())
                hash.Combine(Ids[1], Ids[2]);
            return hash.ValueOf();
        }

        public override object Clone()
        {
            OtpErlangRef newRef = (OtpErlangRef)base.Clone();
            newRef.Ids = (int[])Ids.Clone();
            return newRef;
        }
    }
}
