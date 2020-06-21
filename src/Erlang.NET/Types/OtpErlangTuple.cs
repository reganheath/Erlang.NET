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
using System.Collections;
using System.Collections.Generic;
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
    public class OtpErlangTuple : OtpErlangObject, IEquatable<OtpErlangTuple>, IEnumerable<OtpErlangObject>
    {
        private readonly List<OtpErlangObject> items = new List<OtpErlangObject>();

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
            items.Add(elem);
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
        public OtpErlangTuple(IEnumerable<OtpErlangObject> elems)
        {
            items.AddRange(elems);
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
        public OtpErlangTuple(IEnumerable<OtpErlangObject> elems, int start, int count)
        {
            if (elems == null)
                throw new ArgumentException("Tuple content can't be null");

            if (count <= 0)
                return;

            int i = 0;
            foreach (var elem in elems.Skip(start).Take(count))
            {
                if (elem == null)
                    throw new ArgumentException("Tuple element cannot be null (element" + i + ")");

                items.Add(elem);
                i++;
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
            int arity = buf.ReadTupleHead();
            if (arity == 0)
                return;
            for (int i = 0; i < arity; i++)
                items.Add(buf.ReadAny());
        }

        /**
         * Get all the elements from the list as an array.
         * 
         * @return an array containing all of the list's elements.
         */
        public IEnumerable<OtpErlangObject> Elements => items;

        /**
         * Get the arity of the tuple.
         * 
         * @return the number of elements contained in the tuple.
         */
        public int Arity => items.Count;

        // Get element from list (throws)
        public OtpErlangObject this[int i]
        {
            get => items[i];
            set => items[i] = value;
        }

        /**
         * Get the specified element from the tuple.
         * 
         * @param i
         *                the index of the requested element. Tuple elements are
         *                numbered as array elements, starting at 0.
         * 
         * @return the requested element, of null if i is not a valid element index.
         */
        public OtpErlangObject ElementAt(int i)
        {
            if (i >= Arity || i < 0)
                return null;
            return items[i];
        }

        /**
         * Get the string representation of the tuple.
         * 
         * @return the string representation of the tuple.
         */
        public override string ToString()
        {
            StringBuilder s = new StringBuilder("{");
            s.Append(string.Join(",", items));
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
            buf.WriteTupleHead(Arity);
            foreach(var item in items)
                buf.WriteAny(item);
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
            return items.SequenceEqual(o.items);
        }

        public override int GetHashCode() => base.GetHashCode();

        protected override int HashCode()
        {
            Hash hash = new Hash(9);
            hash.Combine(Arity);
            foreach (var item in items)
                hash.Combine(item.GetHashCode());
            return hash.ValueOf();
        }


        public override object Clone() => new OtpErlangTuple(items);

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public IEnumerator<OtpErlangObject> GetEnumerator() => Iterator(0);

        protected IEnumerator<OtpErlangObject> Iterator(int start) => items.Skip(start).GetEnumerator();
    }
}
