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
using System.Linq;
using System.Text;

namespace Erlang.NET
{
    /**
     * Provides a Java representation of Erlang tuples. Tuples are created from one
     * or more arbitrary Erlang terms.
     * 
     * <p>
     * The arity of the tuple is the number of elements it contains. Elements are
     * indexed from 0 to (arity-1) and can be retrieved individually by using the
     * appropriate index.
     */
    [Serializable]
    public class OtpErlangTuple : OtpErlangObject
    {
        private static readonly OtpErlangObject[] NO_ELEMENTS = new OtpErlangObject[0];
        private OtpErlangObject[] elems = NO_ELEMENTS;

        /**
         * Create a unary tuple containing the given element.
         * 
         * @param elem
         *                the element to create the tuple from.
         * 
         * @exception java.lang.IllegalArgumentException
         *                    if the element is null.
         */
        public OtpErlangTuple(OtpErlangObject elem)
        {
            if (elem == null)
                throw new ArgumentException("Tuple element cannot be null");
            elems = new OtpErlangObject[] { elem };
        }

        /**
         * Create a tuple from an array of terms.
         * 
         * @param elems
         *                the array of terms to create the tuple from.
         * 
         * @exception java.lang.IllegalArgumentException
         *                    if the array is empty (null) or contains null
         *                    elements.
         */
        public OtpErlangTuple(OtpErlangObject[] elems)
            : this(elems, 0, elems.Length)
        {
        }

        /**
         * Create a tuple from an array of terms.
         * 
         * @param elems
         *                the array of terms to create the tuple from.
         * @param start
         *                the offset of the first term to insert.
         * @param count
         *                the number of terms to insert.
         * 
         * @exception java.lang.IllegalArgumentException
         *                    if the array is empty (null) or contains null
         *                    elements.
         */
        public OtpErlangTuple(OtpErlangObject[] elems, int start, int count)
        {
            if (elems == null)
                throw new ArgumentException("Tuple content can't be null");

            if (count >= 1)
            {
                this.elems = new OtpErlangObject[count];
                for (int i = 0; i < count; i++)
                {
                    if (elems[start + i] == null)
                        throw new ArgumentException("Tuple element cannot be null (element" + (start + i) + ")");

                    this.elems[i] = elems[start + i];
                }
            }
        }

        /**
         * Create a tuple from a stream containing an tuple encoded in Erlang
         * external format.
         * 
         * @param buf
         *                the stream containing the encoded tuple.
         * 
         * @exception OtpErlangDecodeException
         *                    if the buffer does not contain a valid external
         *                    representation of an Erlang tuple.
         */
        public OtpErlangTuple(OtpInputStream buf)
        {
            int arity = buf.read_tuple_head();

            if (arity > 0)
            {
                elems = new OtpErlangObject[arity];
                for (int i = 0; i < arity; i++)
                    elems[i] = buf.ReadAny();
            }
        }

        /**
         * Get the arity of the tuple.
         * 
         * @return the number of elements contained in the tuple.
         */
        public int Arity => elems.Length;

        /**
         * Get the specified element from the tuple.
         * 
         * @param i
         *                the index of the requested element. Tuple elements are
         *                numbered as array elements, starting at 0.
         * 
         * @return the requested element, of null if i is not a valid element index.
         */
        public OtpErlangObject elementAt(int i)
        {
            if (i >= Arity || i < 0)
                return null;
            return elems[i];
        }

        /**
         * Get all the elements from the tuple as an array.
         * 
         * @return an array containing all of the tuple's elements.
         */
        public OtpErlangObject[] elements()
        {
            OtpErlangObject[] res = new OtpErlangObject[Arity];
            Array.Copy(elems, 0, res, 0, res.Length);
            return res;
        }

        /**
         * Get the string representation of the tuple.
         * 
         * @return the string representation of the tuple.
         */
        public override string ToString()
        {
            int i;
            StringBuilder s = new StringBuilder();
            int arity = elems.Length;

            s.Append("{");

            for (i = 0; i < arity; i++)
            {
                if (i > 0)
                    s.Append(",");
                s.Append(elems[i].ToString());
            }

            s.Append("}");

            return s.ToString();
        }

        /**
         * Convert this tuple to the equivalent Erlang external representation.
         * 
         * @param buf
         *                an output stream to which the encoded tuple should be
         *                written.
         */
        public override void Encode(OtpOutputStream buf)
        {
            int arity = elems.Length;

            buf.write_tuple_head(arity);

            for (int i = 0; i < arity; i++)
                buf.write_any(elems[i]);
        }

        /**
         * Determine if two tuples are equal. Tuples are equal if they have the same
         * arity and all of the elements are equal.
         * 
         * @param o
         *                the tuple to compare to.
         * 
         * @return true if the tuples have the same arity and all the elements are
         *         equal.
         */
        public override bool Equals(object o) => Equals(o as OtpErlangTuple);

        public bool Equals(OtpErlangTuple o)
        {
            if (o == null)
                return false;
            if (Arity != o.Arity)
                return false;
            return elems.SequenceEqual(o.elems);
        }

        public override int GetHashCode() => base.GetHashCode();

        protected override int DoHashCode()
        {
            OtpErlangObject.Hash hash = new OtpErlangObject.Hash(9);
            hash.Combine(Arity);
            foreach(var elem in elems)
                hash.Combine(elem.GetHashCode());
            return hash.ValueOf();
        }


        public override object Clone()
        {
            OtpErlangTuple newTuple = (OtpErlangTuple)base.Clone();
            newTuple.elems = (OtpErlangObject[])elems.Clone();
            return newTuple;
        }
    }
}
