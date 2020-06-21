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
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Erlang.NET
{
    /**
     * Provides a Java representation of Erlang lists. Lists are created from zero
     * or more arbitrary Erlang terms.
     * 
     * <p>
     * The arity of the list is the number of elements it contains.
     */
    [Serializable]
    public class OtpErlangList : OtpErlangObject, IEquatable<OtpErlangList>, IEnumerable<OtpErlangObject>
    {
        private readonly List<OtpErlangObject> items = new List<OtpErlangObject>();

        /**
         * Create an empty list.
         */
        public OtpErlangList()
        {
        }

        /**
         * Create a list of Erlang integers representing Unicode codePoints.
         * This method does not check if the string contains valid code points.
         * 
         * @param str
         *            the characters from which to create the list.
         */
        public OtpErlangList(string str)
        {
            if (string.IsNullOrWhiteSpace(str))
                return;
            items.AddRange(OtpErlangString.ToCodePoints(str).Select((cp) => new OtpErlangInt(cp)));
        }

        /**
         * Create a list containing one element.
         * 
         * @param elem
         *            the elememet to make the list from.
         */
        public OtpErlangList(OtpErlangObject elem) => items.Add(elem);

        /**
         * Create a list from an array of arbitrary Erlang terms.
         * 
         * @param elems
         *            the array of terms from which to create the list.
         */
        public OtpErlangList(IEnumerable<OtpErlangObject> elems) => items.AddRange(elems);

        /**
         * Create a list from an array of arbitrary Erlang terms. Tail can be
         * specified, if not null, the list will not be proper.
         * 
         * @param elems
         *            array of terms from which to create the list
         * @param lastTail
         * @throws OtpErlangException
         */
        public OtpErlangList(IEnumerable<OtpErlangObject> elems, OtpErlangObject lastTail)
        {
            if (elems.Count() == 0 && lastTail != null)
                throw new OtpException("Bad list, empty head, non-empty tail");
            items.AddRange(elems);
            LastTail = lastTail;
        }

        /**
         * Create a list from an array of arbitrary Erlang terms.
         * 
         * @param elems
         *            the array of terms from which to create the list.
         * @param start
         *            the offset of the first term to insert.
         * @param count
         *            the number of terms to insert.
         */
        public OtpErlangList(IEnumerable<OtpErlangObject> elems, int start, int count)
        {
            if (elems == null)
                return;
            if (count <= 0)
                return;
            items.AddRange(elems.Skip(start).Take(count));
        }

        /**
         * Create a list from a stream containing an list encoded in Erlang external
         * format.
         * 
         * @param buf
         *            the stream containing the encoded list.
         * 
         * @exception OtpErlangDecodeException
         *                if the buffer does not contain a valid external
         *                representation of an Erlang list.
         */
        public OtpErlangList(OtpInputStream buf)
        {
            int arity = buf.ReadListHead();
            if (arity == 0)
                return;

            items = new List<OtpErlangObject>(arity);
            for (int i = 0; i < arity; i++)
                items.Add(buf.ReadAny());

            /* discard the terminating nil (empty list) or read tail */
            if (buf.Peek1() == OtpExternal.nilTag)
                buf.ReadNil();
            else
                LastTail = buf.ReadAny();
        }

        /**
         * Get all the elements from the list as an array.
         * 
         * @return an array containing all of the list's elements.
         */
        public IEnumerable<OtpErlangObject> Elements => items;

        /**
         * Get the arity of the list.
         * 
         * @return the number of elements contained in the list.
         */
        public int Arity => items.Count;

        public OtpErlangObject LastTail { get; protected set; }

        /**
         * @return true if the list is proper, i.e. the last tail is nil
         */
        public bool IsProper => LastTail == null;

        // Get head object
        public OtpErlangObject Head => ElementAt(0);

        // Get tail (list minus head)
        public OtpErlangObject Tail => GetTail(1);

        // Get element from list (throws)
        public OtpErlangObject this[int i]
        {
            get => items[i];
            set => items[i] = value;
        }

        /**
         * Get the specified element from the list or null.
         * 
         * @param i
         *            the index of the requested element. List elements are numbered
         *            as array elements, starting at 0.
         * 
         * @return the requested element, of null if i is not a valid element index.
         */
        public OtpErlangObject ElementAt(int i)
        {
            if (i >= Arity || i < 0)
                return null;
            return items[i];
        }

        // Get tail of list 'from' index specified.
        public OtpErlangObject GetTail(int from)
        {
            if (Arity < from)
                return null;
            if (Arity == from && LastTail != null)
                return LastTail;
            return new OtpErlangList(items.Skip(from), LastTail);
        }

        /**
         * Convert a list of integers into a Unicode string,
         * interpreting each integer as a Unicode code point value.
         * 
         * @return A java.lang.string object created through its
         *         constructor string(int[], int, int).
         *
         * @exception OtpErlangException
         *                    for non-proper and non-integer lists.
         *
         * @exception OtpErlangRangeException
         *                    if any integer does not fit into a Java int.
         *
         * @exception java.security.InvalidParameterException
         *                    if any integer is not within the Unicode range.
         *
         * @see string#string(int[], int, int)
         *
         */
        public string StringValue()
        {
            if (!IsProper)
                throw new OtpException("Non-proper list: " + this);

            OtpErlangObject o = items.FirstOrDefault((e) => !(e is OtpErlangLong));
            if (o != null)
                throw new OtpException("Non-integer term: " + o);

            return OtpErlangString.FromCodePoints(items.Select((e) => ((OtpErlangLong)e).IntValue()));
        }

        /**
         * Get the string representation of the list.
         * 
         * @return the string representation of the list.
         */
        public override string ToString() => ToString(0);

        protected string ToString(int start)
        {
            StringBuilder s = new StringBuilder("[");
            s.Append(string.Join(",", items.Skip(start)));
            if (LastTail != null)
                s.Append("|").Append(LastTail.ToString());
            s.Append("]");
            return s.ToString();
        }

        /**
         * Convert this list to the equivalent Erlang external representation. Note
         * that this method never encodes lists as strings, even when it is possible
         * to do so.
         * 
         * @param buf
         *            An output stream to which the encoded list should be written.
         * 
         */
        public override void Encode(OtpOutputStream buf) => Encode(buf, 0);

        protected void Encode(OtpOutputStream buf, int start)
        {
            int arity = items.Count - start;
            if (arity > 0)
            {
                buf.WriteListHead(arity);
                foreach (OtpErlangObject item in items.Skip(start))
                    buf.WriteAny(item);
            }

            if (LastTail != null)
                buf.WriteAny(LastTail);
            else
                buf.WriteNil();
        }

        /**
         * Determine if two lists are equal. Lists are equal if they have the same
         * arity and all of the elements are equal.
         * 
         * @param o
         *            the list to compare to.
         * 
         * @return true if the lists have the same arity and all the elements are
         *         equal.
         */
        public override bool Equals(object o) => Equals(o as OtpErlangList);

        public bool Equals(OtpErlangList o)
        {
            if (o == null)
                return false;
            if (ReferenceEquals(this, o))
                return true;
            if (Arity != o.Arity)
                return false;
            if (!items.SequenceEqual(o.items))
                return false;
            if (LastTail == null && o.LastTail == null)
                return true;
            if (LastTail == null)
                return false;
            return LastTail.Equals(o.LastTail);
        }

        public override int GetHashCode() => base.GetHashCode();

        protected override int HashCode()
        {
            Hash hash = new Hash(4);
            if (Arity == 0)
                return unchecked((int)3468870702L);

            foreach (OtpErlangObject item in items)
                hash.Combine(item.GetHashCode());

            if (LastTail != null)
            {
                int h = LastTail.GetHashCode();
                hash.Combine(h, h);
            }

            return hash.ValueOf();
        }

        public override object Clone()
        {
            return new OtpErlangList(Elements, LastTail);
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public IEnumerator<OtpErlangObject> GetEnumerator() => Iterator(0);

        protected IEnumerator<OtpErlangObject> Iterator(int start) => items.Skip(start).GetEnumerator();
    }
}
