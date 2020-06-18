/*
 * %CopyrightBegin%
 * 
 * Copyright Takayuki Usui 2009. All Rights Reserved.
 * Copyright Ericsson AB 2007-2009. All Rights Reserved.
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
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters;
using System.Runtime.Serialization.Formatters.Binary;

namespace Erlang.NET
{
    /**
     * Provides a Java representation of Erlang bitstrs. An Erlang bitstr is an
     * Erlang binary with a length not an integral number of bytes (8-bit). Anything
     * can be represented as a sequence of bytes can be made into an Erlang bitstr.
     */
    [Serializable]
    public class OtpErlangBitstr : OtpErlangObject
    {
        /**
         * Create a bitstr from a byte array
         * 
         * @param bin
         *                the array of bytes from which to create the bitstr.
         */
        public OtpErlangBitstr(byte[] bin)
        {
            Bin = new byte[bin.Length];
            Array.Copy(bin, 0, Bin, 0, bin.Length);
            PadBits = 0;
        }

        /**
         * Create a bitstr with pad bits from a byte array.
         * 
         * @param bin
         *                the array of bytes from which to create the bitstr.
         * @param pad_bits
         *                the number of unused bits in the low end of the last byte.
         */
        public OtpErlangBitstr(byte[] bin, int pad_bits)
        {
            Bin = new byte[bin.Length];
            Array.Copy(bin, 0, Bin, 0, bin.Length);
            PadBits = pad_bits;
            CheckBitstr(Bin, PadBits);
        }

        private static void CheckBitstr(byte[] bin, int pad_bits)
        {
            if (pad_bits < 0 || 7 < pad_bits)
                throw new ArgumentException("Padding must be in range 0..7");

            if (pad_bits != 0 && bin.Length == 0)
                throw new ArgumentException("Padding on zero length bitstr");

            if (bin.Length != 0)
            {
                // Make sure padding is zero
                bin[bin.Length - 1] &= (byte)~((1 << pad_bits) - 1);
            }
        }

        /**
         * Create a bitstr from a stream containing a bitstr encoded in Erlang
         * external format.
         * 
         * @param buf
         *                the stream containing the encoded bitstr.
         * 
         * @exception OtpErlangDecodeException
         *                    if the buffer does not contain a valid external
         *                    representation of an Erlang bitstr.
         */
        public OtpErlangBitstr(OtpInputStream buf)
        {
            int pad_bits;
            Bin = buf.ReadBitstr(out pad_bits);
            PadBits = pad_bits;
            CheckBitstr(Bin, PadBits);
        }

        /**
         * Create a bitstr from an arbitrary Java Object. The object must implement
         * java.io.Serializable or java.io.Externalizable.
         * 
         * @param o
         *                the object to serialize and create this bitstr from.
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

        /**
         * Get the byte array from a bitstr, padded with zero bits in the little end
         * of the last byte.
         * 
         * @return the byte array containing the bytes for this bitstr.
         */
        public byte[] Bin { get; protected set; }

        /**
         * Get the size in whole bytes of the bitstr, rest bits in the last byte not
         * counted.
         * 
         * @return the number of bytes contained in the bintstr.
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
         * Get the number of pad bits in the last byte of the bitstr. The pad bits
         * are zero and in the little end.
         * 
         * @return the number of pad bits in the bitstr.
         */
        public int PadBits { get; protected set; }

        /**
         * Get the java Object from the bitstr. If the bitstr contains a serialized
         * Java object, then this method will recreate the object.
         * 
         * 
         * @return the java Object represented by this bitstr, or null if the bitstr
         *         does not represent a Java Object.
         */
        public object getObject()
        {
            if (PadBits != 0)
                return null;
            return FromByteArray(Bin);
        }

        /**
         * Get the string representation of this bitstr object. A bitstr is printed
         * as #Bin&lt;N&gt;, where N is the number of bytes contained in the object
         * or #bin&lt;N-M&gt; if there are M pad bits.
         * 
         * @return the Erlang string representation of this bitstr.
         */
        public override string ToString()
        {
            if (PadBits == 0)
                return "#Bin<" + Bin.Length + ">";
            if (Bin.Length == 0)
                throw new SystemException("Impossible length");
            return "#Bin<" + Bin.Length + "-" + PadBits + ">";
        }

        /**
         * Convert this bitstr to the equivalent Erlang external representation.
         * 
         * @param buf
         *                an output stream to which the encoded bitstr should be
         *                written.
         */
        public override void Encode(OtpOutputStream buf)
        {
            buf.WriteBitstr(Bin, PadBits);
        }

        /**
         * Determine if two bitstrs are equal. Bitstrs are equal if they have the
         * same byte length and tail length, and the array of bytes is identical.
         * 
         * @param o
         *                the bitstr to compare to.
         * 
         * @return true if the bitstrs contain the same bits, false otherwise.
         */
        public override bool Equals(object o) => Equals(o as OtpErlangBitstr);

        public bool Equals(OtpErlangBitstr o)
        {
            if (o == null)
                return false;
            if (ReferenceEquals(this, o))
                return true;
            if (PadBits != o.PadBits)
                return false;
            if (Bin.Length != o.Bin.Length)
                return false;
            return Bin.SequenceEqual(o.Bin);
        }

        public override int GetHashCode() => base.GetHashCode();

        protected override int DoHashCode()
        {
            OtpErlangObject.Hash hash = new OtpErlangObject.Hash(15);
            hash.Combine(Bin);
            hash.Combine(PadBits);
            return hash.ValueOf();
        }

        public override object Clone()
        {
            OtpErlangBitstr that = (OtpErlangBitstr)base.Clone();
            that.Bin = new byte[Bin.Length];
            Array.Copy(Bin, 0, that.Bin, 0, Bin.Length);
            PadBits = PadBits;
            return that;
        }
    }
}
