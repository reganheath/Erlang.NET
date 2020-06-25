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

namespace Erlang.NET
{
    /**
     * Provides a representation of Erlang integral types. Erlang does not
     * distinguish between different integral types, however this class and its
     * subclasses {@link OtpErlangByte}, {@link OtpErlangChar},
     * {@link OtpErlangInt}, and {@link OtpErlangShort} attempt to map the Erlang
     * types onto the various integral types. Two additional classes,
     * {@link OtpErlangUInt} and {@link OtpErlangUShort} are provided for Corba
     * compatibility. See the documentation for IC for more information.
     */
    [Serializable]
    public class OtpErlangLong : IOtpErlangObject, IEquatable<OtpErlangLong>, IComparable<OtpErlangLong>
    {
        private readonly long value;
        private readonly BigInteger bigVal = null;

        /**
         * Create an Erlang integer from the given value.
         */
        public OtpErlangLong(long l) => value = l;

        /**
         * Create an Erlang integer from the given value.
         */
        public OtpErlangLong(BigInteger v)
        {
            if (v == null)
                throw new NullReferenceException();
            if (v.bitCount() < 64)
                value = v.LongValue();
            else
                bigVal = v;
        }

        /**
         * Create an Erlang integer from a stream containing an integer encoded in
         * Erlang external format.
         */
        public OtpErlangLong(OtpInputStream buf)
        {
            byte[] b = buf.ReadIntegerByteArray();

            try { value = OtpInputStream.ByteArrayToLong(b, false); }
            catch (OtpDecodeException)
            {
                bigVal = new BigInteger(b);
            }
        }

        /**
         * Get this number as a BigInteger.
         */
        public BigInteger BigIntegerValue() => bigVal ?? new BigInteger(value);

        /**
         * Get this number as a long, or rather truncate all but the least
         * significant 64 bits from the 2's complement representation of this number
         * and return them as a long.
         */
        public long LongValue() => (bigVal != null ? bigVal.LongValue() : value);

        /**
         * Determine if this value can be represented as a long without truncation.
         * 
         * To just check this.bigVal is a wee bit to simple, since
         * there just might have be a mean bignum that arrived on
         * a stream, and was a long disguised as more than 8 byte integer.
         */
        public bool IsLong() => (bigVal == null || bigVal.bitCount() < 64);

        /**
         * Determine if this value can be represented as an unsigned long without
         * truncation, that is if the value is non-negative and its bit pattern
         * completely fits in a long.
         * 
         * Here we have the same problem as for isLong(), plus
         * the whole range 1<<63 .. (1<<64-1) is allowed.
         */
        public bool IsULong()
        {
            if (bigVal != null)
                return bigVal >= 0 && bigVal.bitCount() <= 64;
            return value >= 0;
        }

        /**
         * Returns the number of bits in the minimal two's-complement representation
         * of this BigInteger, excluding a sign bit.
         */
        public int BitLength()
        {
            if (bigVal != null)
                return bigVal.bitCount();

            if (value == 0 || value == -1)
                return 0;

            // Binary search for bit length
            int i = 32; // mask length
            long m = (1L << i) - 1; // AND mask with ones in little end
            if (value < 0)
            {
                m = ~m; // OR mask with ones in big end
                for (int j = i >> 1; j > 0; j >>= 1) // mask delta
                {
                    if ((value | m) == value) // mask >= enough
                    {
                        i -= j;
                        m >>= j; // try less bits
                    }
                    else
                    {
                        i += j;
                        m <<= j; // try more bits
                    }
                }
                if ((value | m) != value)
                {
                    i++; // mask < enough
                }
            }
            else
            {
                for (int j = i >> 1; j > 0; j >>= 1) // mask delta
                {
                    if ((value & m) == value) // mask >= enough
                    {
                        i -= j;
                        m >>= j; // try less bits
                    }
                    else
                    {
                        i += j;
                        m = m << j | m; // try more bits
                    }
                }
                if ((value & m) != value)
                {
                    i++; // mask < enough
                }
            }

            return i;
        }

        /**
         * Return the signum function of this object.
         */
        public int Signum()
        {
            if (bigVal != null)
                return (bigVal > 0) ? 1 : (bigVal < 0) ? -1 : 0;
            return Math.Sign(value);
        }

        /**
         * Get this number as an int.
         */
        public int IntValue()
        {
            long l = LongValue();
            if (l > int.MaxValue)
                throw new OtpRangeException("Value too large for int: " + value);
            return (int)l;
        }

        /**
         * Get this number as a non-negative int.
         */
        public uint UIntValue()
        {
            long l = LongValue();
            if (l < 0)
                throw new OtpRangeException("Value not positive: " + value);
            if (l > uint.MaxValue)
                throw new OtpRangeException("Value too large for uint: " + value);
            return (uint)l;
        }

        /**
         * Get this number as a short.
         */
        public short ShortValue()
        {
            long l = LongValue();
            if (l > short.MaxValue)
                throw new OtpRangeException("Value too large for short: " + value);
            return (short)l;
        }

        /**
         * Get this number as a non-negative short.
         */
        public ushort UShortValue()
        {
            long l = LongValue();
            if (l < 0)
                throw new OtpRangeException("Value not positive: " + value);
            if (l > ushort.MaxValue)
                throw new OtpRangeException("Value too large for ushort: " + value);
            return (ushort)l;
        }

        /**
         * Get this number as a char.
         */
        public char CharValue()
        {
            long l = LongValue();
            if (l > char.MaxValue)
                throw new OtpRangeException("Value too large for char: " + value);
            return (char)l;
        }

        /**
         * Get this number as a byte.
         */
        public byte ByteValue()
        {
            long l = LongValue();
            if (l < 0)
                throw new OtpRangeException("Value not positive: " + value);
            if (l > byte.MaxValue)
                throw new OtpRangeException("Value too large for byte: " + value);
            return (byte)l;
        }

        /**
         * Get this number as a sbyte.
         */
        public sbyte SByteValue()
        {
            long l = LongValue();
            if (l > sbyte.MaxValue)
                throw new OtpRangeException("Value too large for sbyte: " + value);
            return (sbyte)l;
        }

        /**
         * Convert this number to the equivalent Erlang external representation.
         */
        public void Encode(OtpOutputStream buf)
        {
            if (bigVal != null)
                buf.WriteBigInteger(bigVal);
            buf.WriteLong(value);
        }

        /**
         * Get the string representation of this number.
         */
        public override string ToString() => (bigVal != null ? bigVal.ToString() : value.ToString());

        public override bool Equals(object obj) => Equals(obj as OtpErlangLong);

        public bool Equals(OtpErlangLong other)
        {
            if (other is null)
                return false;
            if (ReferenceEquals(this, other))
                return true;
            if (bigVal != null && other.bigVal != null)
                return bigVal.Equals(other.bigVal);
            if (bigVal == null && other.bigVal == null)
                return value == other.value;
            return false;
        }

        public int CompareTo(object other) => CompareTo(other as OtpErlangLong);

        public int CompareTo(OtpErlangLong other)
        {
            if (other is null)
                return 1;
            if (bigVal != null && other.bigVal != null)
                return bigVal.CompareTo(other.bigVal);
            return value.CompareTo(other.value);
        }

        public override int GetHashCode() => bigVal?.GetHashCode() ?? value.GetHashCode();

        public object Clone() => bigVal != null ? new OtpErlangLong(bigVal) : new OtpErlangLong(value);
    }
}
