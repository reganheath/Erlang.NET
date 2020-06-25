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

namespace Erlang.NET
{
    /**
     * Provides a representation of Erlang floats and doubles. Erlang defines
     * only one floating point numeric type, however this class and its subclass
     * {@link OtpErlangFloat} are used to provide representations corresponding to
     * the types Double and Float.
     */
    [Serializable]
    public class OtpErlangDouble : IOtpErlangObject, IEquatable<OtpErlangDouble>, IComparable<OtpErlangDouble>
    {
        public double Value { get; private set; }

        /**
         * Create an Erlang float from the given double value.
         */
        public OtpErlangDouble(double d) => Value = d;

        /**
         * Create an Erlang float from a stream containing a double encoded in
         * Erlang external format.
         */
        public OtpErlangDouble(OtpInputStream buf) => Value = buf.ReadDouble();

        /**
         * Get the value, as a float.
         */
        public float ToFloat()
        {
            float f = (float)Value;
            if (f != Value)
                throw new OtpRangeException("Value too large for float: " + Value);
            return f;
        }

        /**
         * Convert this double to the equivalent Erlang external representation.
         */
        public void Encode(OtpOutputStream buf) => buf.WriteDouble(Value);

        /**
         * Get the string representation of this double.
         * 
         * @return the string representation of this double.
         */
        public override string ToString() => Value.ToString();

        public int CompareTo(object obj) => CompareTo(obj as OtpErlangDouble);

        public int CompareTo(OtpErlangDouble other)
        {
            if (other is null)
                return 1;
            return Value.CompareTo(other.Value);
        }

        /**
         * Determine if two floats are equal. Floats are equal if they contain the
         * same value.
         */
        public override bool Equals(object o) => Equals(o as OtpErlangDouble);

        public bool Equals(OtpErlangDouble o)
        {
            if (o is null)
                return false;
            if (ReferenceEquals(this, o))
                return true;
            return Value == o.Value;
        }

        public override int GetHashCode() => Value.GetHashCode();

        public object Clone() => new OtpErlangDouble(Value);
    }
}
