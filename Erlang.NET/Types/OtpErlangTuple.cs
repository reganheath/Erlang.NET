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
     * Provides a Java representation of Erlang tuples. Tuples are created from one
     * or more arbitrary Erlang terms.
     * 
     * <p>
     * The arity of the tuple is the number of elements it contains. Elements are
     * indexed from 0 to (arity-1) and can be retrieved individually by using the
     * appropriate index.
     */
    [Serializable]
    public class OtpErlangTuple : List<IOtpErlangObject>, IOtpErlangObject, IEquatable<OtpErlangTuple>, IComparable<OtpErlangTuple>
    {
        public int Arity => Count;

        public OtpErlangTuple() { }

        /**
         * Create a unary tuple containing the given element.
         */
        public OtpErlangTuple(IOtpErlangObject elem) => Add(elem ?? throw new ArgumentNullException(nameof(elem)));

        /**
         * Create a tuple from an array of terms.
         */
        public OtpErlangTuple(IEnumerable<IOtpErlangObject> elems) => AddRange(elems ?? throw new ArgumentNullException(nameof(elems)));

        public OtpErlangTuple(params IOtpErlangObject[] elems) => AddRange(elems ?? throw new ArgumentNullException(nameof(elems)));

        /**
         * Create a tuple from an array of terms.
         */
        public OtpErlangTuple(IEnumerable<IOtpErlangObject> elems, int start, int count)
        {
            if (elems == null)
                throw new ArgumentNullException(nameof(elems));
            if (start < 0)
                throw new ArgumentOutOfRangeException(nameof(start), start, $"Start {start} must be >= 0");
            if (count <= 0)
                throw new ArgumentOutOfRangeException(nameof(count), count, $"Count {count} must be > 0");

            int i = 0;
            foreach (var elem in elems.Skip(start).Take(count))
            {
                if (elem == null)
                    throw new ArgumentException($"Tuple element {i} cannot be null");
                Add(elem);
                i++;
            }
        }

        /**
         * Create a tuple from a stream containing an tuple encoded in Erlang
         * external format.
         */
        public OtpErlangTuple(OtpInputStream buf)
        {
            int arity = buf.ReadTupleHead();
            if (arity == 0)
                return;
            for (int i = 0; i < arity; i++)
                Add(buf.ReadAny());
        }

        public IOtpErlangObject ElementAt(int i) => (i < Arity ? this[i] : null);

        /**
         * Convert this tuple to the equivalent Erlang external representation.
         */
        public void Encode(OtpOutputStream buf)
        {
            buf.WriteTupleHead(Arity);
            foreach (var item in this)
                buf.WriteAny(item);
        }

        /**
         * Get the string representation of the tuple.
         */
        public override string ToString() => "{" + string.Join(",", this) + "}";

        public int CompareTo(object obj) => CompareTo(obj as OtpErlangTuple);

        public int CompareTo(OtpErlangTuple other)
        {
            if (other is null)
                return 1;
            return OtpErlangObject.CompareTo(this, other);
        }

        /**
         * Determine if two tuples are equal. Tuples are equal if they have the same
         * arity and all of the elements are equal.
         */
        public override bool Equals(object o) => Equals(o as OtpErlangTuple);

        public bool Equals(OtpErlangTuple o)
        {
            if (o is null)
                return false;
            if (ReferenceEquals(this, o))
                return true;
            return Arity == o.Arity &&
                this.SequenceEqual(o);
        }

        public override int GetHashCode()
        {
            int hashCode = 1961346695;
            hashCode = hashCode * -1521134295 + base.GetHashCode();
            return hashCode;
        }

        public object Clone() => new OtpErlangTuple(this, 0, Arity);
    }
}
