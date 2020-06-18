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
    public class OtpErlangList : OtpErlangObject, IEnumerable<OtpErlangObject>
    {
        private static readonly OtpErlangObject[] NO_ELEMENTS = new OtpErlangObject[0];
        private readonly OtpErlangObject[] elems = NO_ELEMENTS;

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
            if (!String.IsNullOrWhiteSpace(str))
            {
                int[] codePoints = OtpErlangString.ToCodePoints(str);
                elems = new OtpErlangObject[codePoints.Length];
                for (int i = 0; i < elems.Length; i++)
                {
                    elems[i] = new OtpErlangInt(codePoints[i]);
                }
            }
        }

        /**
         * Create a list containing one element.
         * 
         * @param elem
         *            the elememet to make the list from.
         */
        public OtpErlangList(OtpErlangObject elem)
        {
            elems = new OtpErlangObject[] { elem };
        }

        /**
         * Create a list from an array of arbitrary Erlang terms.
         * 
         * @param elems
         *            the array of terms from which to create the list.
         */
        public OtpErlangList(OtpErlangObject[] elems)
            : this(elems, 0, elems.Length)
        {
        }

        /**
         * Create a list from an array of arbitrary Erlang terms. Tail can be
         * specified, if not null, the list will not be proper.
         * 
         * @param elems
         *            array of terms from which to create the list
         * @param lastTail
         * @throws OtpErlangException
         */
        public OtpErlangList(OtpErlangObject[] elems, OtpErlangObject lastTail)
            : this(elems, 0, elems.Length)
        {
            if (elems.Length == 0 && lastTail != null)
                throw new OtpErlangException("Bad list, empty head, non-empty tail");
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
        public OtpErlangList(OtpErlangObject[] elems, int start, int count)
        {
            if (elems != null && count > 0)
            {
                this.elems = new OtpErlangObject[count];
                Array.Copy(elems, start, this.elems, 0, count);
            }
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
            int arity = buf.read_list_head();
            if (arity == 0)
                return;

            elems = new OtpErlangObject[arity];
            for (int i = 0; i < arity; i++)
                elems[i] = buf.ReadAny();

            /* discard the terminating nil (empty list) or read tail */
            if (buf.peek1() == OtpExternal.nilTag)
                buf.read_nil();
            else
                LastTail = buf.ReadAny();
        }

        /**
         * Get the arity of the list.
         * 
         * @return the number of elements contained in the list.
         */
        public virtual int Arity => elems.Length;

        /**
         * Get the specified element from the list.
         * 
         * @param i
         *            the index of the requested element. List elements are numbered
         *            as array elements, starting at 0.
         * 
         * @return the requested element, of null if i is not a valid element index.
         */
        public virtual OtpErlangObject ElementAt(int i)
        {
            if (i >= Arity || i < 0)
                return null;
            return elems[i];
        }

        /**
         * Get all the elements from the list as an array.
         * 
         * @return an array containing all of the list's elements.
         */
        public virtual OtpErlangObject[] Elements()
        {
            if (Arity == 0)
                return NO_ELEMENTS;

            OtpErlangObject[] res = new OtpErlangObject[Arity];
            Array.Copy(elems, 0, res, 0, res.Length);
            return res;
        }

        /**
         * Get the string representation of the list.
         * 
         * @return the string representation of the list.
         */
        public override string ToString()
        {
            return ToString(0);
        }

        protected string ToString(int start)
        {
            StringBuilder s = new StringBuilder();
            s.Append("[");
            s.Append(string.Join(",", elems.Skip(start)));
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
        public override void Encode(OtpOutputStream buf)
        {
            Encode(buf, 0);
        }

        protected void Encode(OtpOutputStream buf, int start)
        {
            int arity = this.Arity - start;

            if (arity > 0)
            {
                buf.write_list_head(arity);

                for (int i = start; i < arity + start; i++)
                {
                    buf.write_any(elems[i]);
                }
            }
            if (LastTail == null)
            {
                buf.write_nil();
            }
            else
            {
                buf.write_any(LastTail);
            }
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
            /*
             * Be careful to use methods even for "this", so that equals work also
             * for sublists
             */
            if (o == null)
                return false;
            if (ReferenceEquals(this, o))
                return true;

            if (Arity != o.Arity)
                return false;

            for (int i = 0; i < Arity; i++)
            {
                if (!ElementAt(i).Equals(o.ElementAt(i)))
                    return false; // early exit
            }

            if (LastTail == null && o.LastTail == null)
                return true;
            if (LastTail == null)
                return false;

            return LastTail.Equals(o.LastTail);
        }

        public virtual OtpErlangObject LastTail { get; protected set; }

        public override int GetHashCode() => base.GetHashCode();

        protected override int DoHashCode()
        {
            OtpErlangObject.Hash hash = new OtpErlangObject.Hash(4);
            if (Arity == 0)
                return unchecked((int)3468870702L);
            for (int i = 0; i < Arity; i++)
            {
                hash.Combine(ElementAt(i).GetHashCode());
            }
            OtpErlangObject t = LastTail;
            if (t != null)
            {
                int h = t.GetHashCode();
                hash.Combine(h, h);
            }
            return hash.ValueOf();
        }

        public override object Clone()
        {
            try
            {
                return new OtpErlangList(Elements(), LastTail);
            }
            catch (OtpErlangException)
            {
                throw new Exception("List clone failed");
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public IEnumerator<OtpErlangObject> GetEnumerator()
        {
            return Iterator(0);
        }

        protected virtual IEnumerator<OtpErlangObject> Iterator(int start)
        {
            return new Itr(start, elems);
        }

        /**
         * @return true if the list is proper, i.e. the last tail is nil
         */
        public virtual bool IsProper() => LastTail == null;

        public virtual OtpErlangObject GetHead()
        {
            if (Arity > 0)
                return elems[0];
            return null;
        }

        public virtual OtpErlangObject GetTail()
        {
            return GetNthTail(1);
        }

        public virtual OtpErlangObject GetNthTail(int n)
        {
            if (Arity >= n)
            {
                if (Arity == n && LastTail != null)
                    return LastTail;
                return new SubList(this, n);
            }
            return null;
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
            if (!IsProper())
                throw new OtpErlangException("Non-proper list: " + this);

            var o = elems.FirstOrDefault((e) => !(e is OtpErlangLong));
            if (o != null)
                throw new OtpErlangException("Non-integer term: " + o);

            return OtpErlangString.FromCodePoints(elems.Select((e) => ((OtpErlangLong)e).IntValue()));
        }

        private class SubList : OtpErlangList
        {
            private readonly OtpErlangList parent;
            private readonly int start;

            public SubList(OtpErlangList parent, int start)
                : base()
            {
                this.parent = parent;
                this.start = start;
            }

            public override int Arity => parent.Arity - start;

            public override OtpErlangObject ElementAt(int i) => parent.ElementAt(i + start);

            public override OtpErlangObject[] Elements()
            {
                int n = parent.Arity - start;
                OtpErlangObject[] res = new OtpErlangObject[n];
                for (int i = 0; i < res.Length; i++)
                {
                    res[i] = parent.ElementAt(i + start);
                }
                return res;
            }

            public override bool IsProper() => parent.IsProper();

            public override OtpErlangObject GetHead() => parent.ElementAt(start);

            public override OtpErlangObject GetNthTail(int n) => parent.GetNthTail(n + start);

            public override string ToString() => parent.ToString(start);

            public override void Encode(OtpOutputStream stream) => parent.Encode(stream, start);

            public override OtpErlangObject LastTail => parent.LastTail;

            protected override IEnumerator<OtpErlangObject> Iterator(int start) => parent.Iterator(start);
        }

        private class Itr : IEnumerator<OtpErlangObject>
        {
            /**
             * Index of element to be returned by subsequent call to next.
             */
            private readonly int start;
            private readonly OtpErlangObject[] elems;
            private int cursor;

            public Itr(int start, OtpErlangObject[] elems)
            {
                this.start = start;
                this.cursor = -1;
                this.elems = elems;
            }

            public bool MoveNext()
            {
                if (cursor == -1)
                    cursor = start;
                else
                    cursor++;
                
                return cursor < elems.Length;
            }

            public void Reset()
            {
                cursor = -1;
            }

            object IEnumerator.Current => Current;

            public OtpErlangObject Current
            {
                get
                {
                    try
                    {
                        return elems[cursor];
                    }
                    catch (IndexOutOfRangeException)
                    {
                        return null;
                    }
                }
            }

            public void Dispose()
            {
            }
        }
    }
}
