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
using System.Text;

namespace Erlang.NET
{
    /**
     * Provides a representation of Erlang lists. Lists are created from zero
     * or more arbitrary Erlang terms.
     * 
     * The arity of the list is the number of elements it contains.
     */
    [Serializable]
    public class OtpErlangList : List<IOtpErlangObject>, IOtpErlangObject, IEquatable<OtpErlangList>, IComparable<OtpErlangList>
    {
        public IOtpErlangObject LastTail { get; protected set; }

        public int Arity => Count;

        public bool IsProper => LastTail == null;

        public IOtpErlangObject Head => this.FirstOrDefault();

        public IOtpErlangObject Tail => GetTail(1);


        /**
         * Create an empty list.
         */
        public OtpErlangList() { }

        /**
         * Create a list of Erlang integers representing Unicode codePoints.
         * This method does not check if the string contains valid code points.
         */
        public OtpErlangList(string str)
            : base(str.Length)
        {
            if (string.IsNullOrWhiteSpace(str))
                return;
            AddRange(OtpErlangString.ToCodePoints(str).Select((cp) => new OtpErlangInt(cp)));
        }

        /**
         * Create a list containing one element.
         */
        public OtpErlangList(IOtpErlangObject elem) => Add(elem ?? throw new ArgumentNullException(nameof(elem)));

        /**
         * Create a list from an array of arbitrary Erlang terms.
         */
        public OtpErlangList(IEnumerable<IOtpErlangObject> elems) => AddRange(elems ?? throw new ArgumentNullException(nameof(elems)));

        /**
         * Create a list from an array of arbitrary Erlang terms. Tail can be
         * specified, if not null, the list will not be proper.
         */
        public OtpErlangList(IEnumerable<IOtpErlangObject> elems, IOtpErlangObject lastTail)
        {
            if (elems == null)
                throw new ArgumentNullException(nameof(elems));
            if (elems.Count() == 0 && lastTail != null)
                throw new OtpException("Bad list, empty head, non-empty tail");
            AddRange(elems);
            LastTail = lastTail;
        }

        /**
         * Create a list from an array of arbitrary Erlang terms.
         */
        public OtpErlangList(IEnumerable<IOtpErlangObject> elems, int start, int count)
        {
            if (elems == null)
                throw new ArgumentNullException(nameof(elems));
            if (start < 0)
                throw new ArgumentOutOfRangeException(nameof(start), start, $"Start {start} must be >= 0");
            if (count <= 0)
                throw new ArgumentOutOfRangeException(nameof(count), count, $"Count {count} must be > 0");
            AddRange(elems.Skip(start).Take(count));
        }

        /**
         * Create a list from a stream containing an list encoded in Erlang external
         * format.
         */
        public OtpErlangList(OtpInputStream buf)
        {
            int arity = buf.ReadListHead();
            if (arity == 0)
                return;

            Capacity = arity;
            for (int i = 0; i < arity; i++)
                Add(buf.ReadAny());

            /* discard the terminating nil (empty list) or read tail */
            if (buf.Peek1() == OtpExternal.nilTag)
                buf.ReadNil();
            else
                LastTail = buf.ReadAny();
        }

        // Get tail of list 'from' index specified.
        public IOtpErlangObject GetTail(int from)
        {
            if (Arity < from)
                return null;
            if (Arity == from && LastTail != null)
                return LastTail;
            return new OtpErlangList(this.Skip(from), LastTail);
        }

        /**
         * Convert a list of integers into a Unicode string,
         * interpreting each integer as a Unicode code point value.
         */
        public string StringValue()
        {
            if (!IsProper)
                throw new OtpException("Non-proper list: " + this);

            IOtpErlangObject o = this.FirstOrDefault((e) => !(e is OtpErlangLong));
            if (o != null)
                throw new OtpException("Non-integer term: " + o);

            return OtpErlangString.FromCodePoints(this.Select((e) => ((OtpErlangLong)e).IntValue()));
        }

        /**
         * Convert this list to the equivalent Erlang external representation. Note
         * that this method never encodes lists as strings, even when it is possible
         * to do so.
         */
        public void Encode(OtpOutputStream buf) => Encode(buf, 0);

        /**
         * Get the string representation of the list.
         */
        public override string ToString() => ToString(0);

        protected string ToString(int start)
        {
            StringBuilder s = new StringBuilder("[");
            s.Append(string.Join(",", this.Skip(start)));
            if (LastTail != null)
                s.Append("|").Append(LastTail.ToString());
            s.Append("]");
            return s.ToString();
        }

        protected void Encode(OtpOutputStream buf, int start)
        {
            int arity = Count - start;
            if (arity > 0)
            {
                buf.WriteListHead(arity);
                foreach (IOtpErlangObject item in this.Skip(start))
                    buf.WriteAny(item);
            }

            if (LastTail != null)
                buf.WriteAny(LastTail);
            else
                buf.WriteNil();
        }

        public int CompareTo(object obj) => CompareTo(obj as OtpErlangList);

        public int CompareTo(OtpErlangList other)
        {
            if (other is null)
                return 1;
            int res = OtpErlangObject.CompareTo(this, other);
            if (res == 0)
                res = LastTail?.CompareTo(other.LastTail) ?? 0;
            return res;
        }

        /**
         * Determine if two lists are equal. Lists are equal if they have the same
         * arity and all of the elements are equal.
         */
        public override bool Equals(object o) => Equals(o as OtpErlangList);

        public bool Equals(OtpErlangList o)
        {
            if (o is null)
                return false;
            if (ReferenceEquals(this, o))
                return true;
            return Arity == o.Arity &&
                this.SequenceEqual(o) &&
                Equals(LastTail, o.LastTail);
        }

        public override int GetHashCode()
        {
            int hashCode = 749125771;
            hashCode = hashCode * -1521134295 + base.GetHashCode();
            hashCode = hashCode * -1521134295 + LastTail.GetHashCode();
            return hashCode;
        }

        public object Clone() => new OtpErlangList(this, LastTail);
    }
}
