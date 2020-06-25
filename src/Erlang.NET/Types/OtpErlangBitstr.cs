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
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;

namespace Erlang.NET
{
    /**
     * Provides a representation of Erlang bitstrs. An Erlang bitstr is an
     * Erlang binary with a length not an integral number of bytes (8-bit). Anything
     * can be represented as a sequence of bytes can be made into an Erlang bitstr.
     */
    [Serializable]
    public class OtpErlangBitstr : IOtpErlangObject, IEquatable<OtpErlangBitstr>, IComparable<OtpErlangBitstr>
    {
        public byte[] Bin { get; protected set; }

        public int PadBits { get; protected set; }

        protected OtpErlangBitstr() { }

        /**
         * Create a bitstr from a byte array
         */
        public OtpErlangBitstr(byte[] bin)
        {
            Bin = (byte[])bin.Clone();
            PadBits = 0;
        }

        /**
         * Create a bitstr with pad bits from a byte array.
         */
        public OtpErlangBitstr(byte[] bin, int padBits)
        {
            Bin = (byte[])bin.Clone();
            PadBits = padBits;
            CheckBitstr(Bin, PadBits);
        }

        /**
         * Create a bitstr from a stream containing a bitstr encoded in Erlang
         * external format.
         */
        public OtpErlangBitstr(OtpInputStream buf)
        {
            Bin = buf.ReadBitstr(out int padBits);
            PadBits = padBits;
            CheckBitstr(Bin, PadBits);
        }

        /**
         * Create a bitstr from an arbitrary Object.
         */
        public OtpErlangBitstr(object o)
        {
            try
            {
                Bin = ToByteArray(o);
                PadBits = 0;
            }
            catch (SerializationException e)
            {
                throw new ArgumentException("Object must implement Serializable", e);
            }
        }

        /**
         * Get the size in whole bytes of the bitstr, rest bits in the last byte not
         * counted.
         */
        public int Size()
        {
            if (PadBits == 0)
                return Bin.Length;
            if (Bin.Length == 0)
                throw new SystemException("Impossible length");
            return Bin.Length - 1;
        }

        /**
         * Get the Object from the bitstr. If the bitstr contains a serialized
         * object, then this method will recreate the object.
         */
        public object GetObject()
        {
            if (PadBits != 0)
                return null;
            return FromByteArray(Bin);
        }

        /**
         * Convert this bitstr to the equivalent Erlang external representation.
         */
        public virtual void Encode(OtpOutputStream buf) => buf.WriteBitstr(Bin, PadBits);

        /**
         * Get the string representation of this bitstr object. A bitstr is printed
         * as #Bin&lt;N&gt;, where N is the number of bytes contained in the object
         * or #bin&lt;N-M&gt; if there are M pad bits.
         */
        public override string ToString()
        {
            if (PadBits == 0)
                return $"#Bin<{Bin.Length}>";
            if (Bin.Length == 0)
                throw new SystemException("Impossible length");
            return $"#Bin<{Bin.Length}-{PadBits}>";
        }

        public int CompareTo(object obj) => CompareTo(obj as OtpErlangBitstr);

        public int CompareTo(OtpErlangBitstr other)
        {
            if (other is null)
                return 1;
            int res = PadBits.CompareTo(other.PadBits);
            if (res == 0)
                res = Bin.CompareTo(other.Bin);
            return res;
        }

        public override bool Equals(object obj) => Equals(obj as OtpErlangBitstr);

        public bool Equals(OtpErlangBitstr o)
        {
            if (o is null)
                return false;
            if (ReferenceEquals(this, o))
                return true;
            return PadBits == o.PadBits &&
                   Bin.SequenceEqual(o.Bin);
        }

        public override int GetHashCode()
        {
            int hashCode = -681471680;
            hashCode = hashCode * -1521134295 + EqualityComparer<byte[]>.Default.GetHashCode(Bin);
            hashCode = hashCode * -1521134295 + PadBits.GetHashCode();
            return hashCode;
        }

        public virtual object Clone() => new OtpErlangBitstr(Bin, PadBits);

        #region Private Helper Methods
        private static void CheckBitstr(byte[] bin, int padBits)
        {
            if (padBits < 0 || 7 < padBits)
                throw new ArgumentException("Padding must be in range 0..7");

            if (padBits != 0 && bin.Length == 0)
                throw new ArgumentException("Padding on zero length bitstr");

            if (bin.Length != 0) // Make sure padding is zero
                bin[bin.Length - 1] &= (byte)~((1 << padBits) - 1);
        }

        private static byte[] ToByteArray(object o)
        {
            if (o == null)
                return null;

            BinaryFormatter bf = new BinaryFormatter();
            MemoryStream ms = new MemoryStream();

            try
            {
                bf.Serialize(ms, o);
                ms.Flush();
                ms.Position = 0;

                return ms.ToArray();
            }
            finally
            {
                ms.Close();
            }
        }

        private static object FromByteArray(byte[] buf)
        {
            if (buf == null)
                return null;

            BinaryFormatter bf = new BinaryFormatter();
            MemoryStream ms = new MemoryStream(buf);

            try
            {
                return bf.Deserialize(ms);
            }
            catch (SerializationException)
            {
            }

            return null;
        }
        #endregion
    }
}
