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
     * Provides a Java representation of Erlang floats and doubles. Erlang defines
     * only one floating point numeric type, however this class and its subclass
     * {@link OtpErlangFloat} are used to provide representations corresponding to
     * the Java types Double and Float.
     */
    [Serializable]
    public class OtpErlangDouble : OtpErlangObject, IEquatable<OtpErlangDouble>
    {
        public double Value { get; private set; }

        /**
         * Create an Erlang float from the given double value.
         */
        public OtpErlangDouble(double d) => Value = d;

        /**
         * Create an Erlang float from a stream containing a double encoded in
         * Erlang external format.
         * 
         * @param buf
         *                the stream containing the encoded value.
         * 
         * @exception OtpErlangDecodeException
         *                    if the buffer does not contain a valid external
         *                    representation of an Erlang float.
         */
        public OtpErlangDouble(OtpInputStream buf) => Value = buf.ReadDouble();

        /**
         * Get the value, as a float.
         * 
         * @return the value of this object, as a float.
         * 
         * @exception OtpErlangRangeException
         *                    if the value cannot be represented as a float.
         */
        public float FloatValue()
        {
            float f = (float)Value;
            if (f != Value)
                throw new OtpRangeException("Value too large for float: " + Value);
            return f;
        }

        /**
         * Get the string representation of this double.
         * 
         * @return the string representation of this double.
         */
        public override string ToString() => Value.ToString();

        /**
         * Convert this double to the equivalent Erlang external representation.
         * 
         * @param buf
         *                an output stream to which the encoded value should be
         *                written.
         */
        public override void Encode(OtpOutputStream buf) => buf.WriteDouble(Value);

        /**
         * Determine if two floats are equal. Floats are equal if they contain the
         * same value.
         * 
         * @param o
         *                the float to compare to.
         * 
         * @return true if the floats have the same value.
         */
        public override bool Equals(object o) => Equals(o as OtpErlangDouble);

        public bool Equals(OtpErlangDouble o)
        {
            if (o == null)
                return false;
            if (ReferenceEquals(this, o))
                return true;
            return Value == o.Value;
        }

        public override int GetHashCode() => base.GetHashCode();

        protected override int HashCode() => Value.GetHashCode();
    }
}
