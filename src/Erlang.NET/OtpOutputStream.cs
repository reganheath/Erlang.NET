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
using System.IO;
using System.IO.Compression;
using System.Text;

namespace Erlang.NET
{
    /**
     * Provides a stream for encoding Erlang terms to external format, for
     * transmission or storage.
     * 
     * Note that this class is not synchronized, if you need synchronization you
     * must provide it yourself.
     */
    public class OtpOutputStream : MemoryStream
    {
        /** The default initial size of the stream. */
        public const int defaultInitialSize = 2048;

        /**
         * Create a stream with the default initial size (2048 bytes).
         */
        public OtpOutputStream() : this(defaultInitialSize) { }

        /**
         * Create a stream with the specified initial size.
         */
        public OtpOutputStream(int size) : base(size) { }

        /**
         * Create a stream containing the encoded version of the given Erlang term.
         */
        public OtpOutputStream(IOtpErlangObject o) : base() => WriteAny(o);

        /**
         * Get the contents of the output stream as an input stream instead. This is
         * used internally in {@link OtpCconnection} for tracing outgoing packages.
         */
        internal OtpInputStream GetOtpInputStream(int offset) => new OtpInputStream(base.GetBuffer(), offset, (int)(base.Length - offset));

        /**
         * Write one byte to the stream.
         */
        public void Write(byte b) => base.WriteByte(b);
        public void Write(int b) => base.WriteByte((byte)b);

        /**
         * Write an array of bytes to the stream.
         */
        public void Write(byte[] buf) => base.Write(buf, 0, buf.Length);

        public override void WriteTo(Stream stream)
        {
            try
            {
                base.WriteTo(stream);
                stream.Flush();
            }
            catch (ObjectDisposedException e)
            {
                throw new IOException(e.Message);
            }
        }

        /**
         * Write the low byte of a value to the stream.
         */
        public void Write1(long n) => Write((byte)(n & 0xff));

        /**
         * Write an array of bytes to the stream.
         */
        public void WriteN(byte[] bytes) => Write(bytes);

        /**
         * Write the low two bytes of a value to the stream in big endian order.
         */
        public void Write2BE(long n)
        {
            Write((byte)((n & 0xff00) >> 8));
            Write((byte)(n & 0xff));
        }

        /**
         * Write the low four bytes of a value to the stream in big endian order.
         */
        public void Write4BE(long n)
        {
            Write((byte)((n & 0xff000000) >> 24));
            Write((byte)((n & 0xff0000) >> 16));
            Write((byte)((n & 0xff00) >> 8));
            Write((byte)(n & 0xff));
        }

        /**
         * Write the low eight (all) bytes of a value to the stream in big endian
         * order.
         */
        public void Write8BE(long n)
        {
            Write((byte)(n >> 56 & 0xff));
            Write((byte)(n >> 48 & 0xff));
            Write((byte)(n >> 40 & 0xff));
            Write((byte)(n >> 32 & 0xff));
            Write((byte)(n >> 24 & 0xff));
            Write((byte)(n >> 16 & 0xff));
            Write((byte)(n >> 8 & 0xff));
            Write((byte)(n & 0xff));
        }

        /**
         * Write any number of bytes in little endian format.
         */
        public void WriteLE(long n, int b)
        {
            for (int i = 0; i < b; i++)
            {
                Write((byte)(n & 0xff));
                n >>= 8;
            }
        }

        /**
         * Write the low two bytes of a value to the stream in little endian order.
         */
        public void Write2LE(long n)
        {
            Write((byte)(n & 0xff));
            Write((byte)((n & 0xff00) >> 8));
        }

        /**
         * Write the low four bytes of a value to the stream in little endian order.
         */
        public void Write4LE(long n)
        {
            Write((byte)(n & 0xff));
            Write((byte)((n & 0xff00) >> 8));
            Write((byte)((n & 0xff0000) >> 16));
            Write((byte)((n & 0xff000000) >> 24));
        }

        /**
         * Write the low eight bytes of a value to the stream in little endian
         * order.
         */
        public void Write8LE(long n)
        {
            Write((byte)(n & 0xff));
            Write((byte)(n >> 8 & 0xff));
            Write((byte)(n >> 16 & 0xff));
            Write((byte)(n >> 24 & 0xff));
            Write((byte)(n >> 32 & 0xff));
            Write((byte)(n >> 40 & 0xff));
            Write((byte)(n >> 48 & 0xff));
            Write((byte)(n >> 56 & 0xff));
        }

        /**
         * Write the low four bytes of a value to the stream in bif endian order, at
         * the specified position. If the position specified is beyond the end of
         * the stream, this method will have no effect.
         * 
         * Normally this method should be used in conjunction with {@link #size()
         * size()}, when is is necessary to insert data into the stream before it is
         * known what the actual value should be. For example:
         * 
         * <pre>
         * int pos = s.size();
         *    s.write4BE(0); // make space for length data,
         *                   // but final value is not yet known
         *     [ ...more write statements...]
         *    // later... when we know the length value
         *    s.poke4BE(pos, length);
         * </pre>
         */
        public void Poke4BE(int offset, long n)
        {
            long cur = base.Position;

            base.Position = offset;
            Write((byte)((n & 0xff000000) >> 24));
            Write((byte)((n & 0xff0000) >> 16));
            Write((byte)((n & 0xff00) >> 8));
            Write((byte)(n & 0xff));

            base.Position = cur;
        }

        /**
         * Write an arbitrary Erlang term to the stream.
         */
        public void WriteAny(IOtpErlangObject o) => o.Encode(this);

        /**
         * Write a string to the stream as an Erlang atom.
         */
        public void WriteAtom(string atom)
        {
            if (atom.Length > OtpExternal.MAX_ATOM_LENGTH)
                atom = atom.Substring(0, OtpExternal.MAX_ATOM_LENGTH);

            byte[] bytes = Encoding.GetEncoding("UTF-8", EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback).GetBytes(atom);
            if (bytes.Length < 256)
            {
                Write1(OtpExternal.smallAtomUtf8Tag);
                Write1(bytes.Length);
            }
            else
            {
                Write1(OtpExternal.atomUtf8Tag);
                Write2BE(bytes.Length);
            }

            WriteN(bytes);
        }

        /**
         * Write an array of bytes to the stream as an Erlang binary.
         */
        public void WriteBinary(byte[] bin)
        {
            Write1(OtpExternal.binTag);
            Write4BE(bin.Length);
            WriteN(bin);
        }

        /**
         * Write an array of bytes to the stream as an Erlang bitstr.
         */
        public void WriteBitstr(byte[] bin, int padBits)
        {
            if (padBits == 0)
            {
                WriteBinary(bin);
                return;
            }
            Write1(OtpExternal.bitBinTag);
            Write4BE(bin.Length);
            Write1(8 - padBits);
            WriteN(bin);
        }

        /**
         * Write a boolean value to the stream as the Erlang atom 'true' or 'false'.
         */
        public void WriteBoolean(bool b) => WriteAtom(b.ToString());

        /**
         * Write a single byte to the stream as an Erlang integer. The byte is
         * really an IDL 'octet', that is, unsigned.
         */
        public new void WriteByte(byte b) => WriteLong(b & 0xffL, true);

        /**
         * Write a character to the stream as an Erlang integer. The character may
         * be a 16 bit character, kind of IDL 'wchar'. It is up to the Erlang side
         * to take care of souch, if they should be used.
         * 
         * @param c
         *            the character to use.
         */
        public void WriteChar(char c) => WriteLong(c & 0xffffL, true);

        /**
         * Write a double value to the stream.
         */
        public void WriteDouble(double d)
        {
            Write1(OtpExternal.newFloatTag);
            Write8BE(BitConverter.ToInt64(BitConverter.GetBytes(d), 0));
        }

        /**
         * Write a float value to the stream.
         */
        public void WriteFloat(float f) => WriteDouble(f);

        public void WriteBigInteger(BigInteger v)
        {
            if (v.bitCount() < 64)
            {
                WriteLong(v.LongValue(), true);
                return;
            }

            int signum = (v > 0) ? 1 : (v < 0) ? -1 : 0;
            if (signum < 0)
                v = -v;

            byte[] magnitude = v.getBytes();
            int n = magnitude.Length;

            // Reverse the array to make it little endian.
            for (int i = 0, j = n; i < j--; i++)
                (magnitude[i], magnitude[j]) = (magnitude[j], magnitude[i]);

            if ((n & 0xFF) == n)
            {
                Write1(OtpExternal.smallBigTag);
                Write1(n); // length
            }
            else
            {
                Write1(OtpExternal.largeBigTag);
                Write4BE(n); // length
            }
            Write1(signum < 0 ? 1 : 0); // sign
            // Write the array
            WriteN(magnitude);
        }

        private void WriteLong(long v, bool unsigned)
        {
            /*
             * If v<0 and unsigned==true the value
             * java.lang.Long.MAX_VALUE-java.lang.Long.MIN_VALUE+1+v is written, i.e
             * v is regarded as unsigned two's complement.
             */
            if ((v & 0xffL) == v)
            {
                // will fit in one byte
                Write1(OtpExternal.smallIntTag);
                Write1(v);
                return;
            }

            // note that v != 0L
            if (v < 0 && unsigned || v < OtpExternal.erlMin || v > OtpExternal.erlMax)
            {
                // some kind of bignum
                long abs = unsigned ? v : v < 0 ? -v : v;
                int sign = unsigned ? 0 : v < 0 ? 1 : 0;
                int n;
                long mask;
                for (mask = 0xFFFFffffL, n = 4; (abs & mask) != abs; n++, mask = mask << 8 | 0xffL)
                {
                    ; // count nonzero bytes
                }
                Write1(OtpExternal.smallBigTag);
                Write1(n); // length
                Write1(sign); // sign
                WriteLE(abs, n); // value. obs! little endian
            }
            else
            {
                Write1(OtpExternal.intTag);
                Write4BE(v);
            }
        }

        /**
         * Write a long to the stream.
         */
        public void WriteLong(long l) => WriteLong(l, false);

        /**
         * Write a positive long to the stream. The long is interpreted as a two's
         * complement unsigned long even if it is negative.
         */
        public void WriteULong(long ul) => WriteLong(ul, true);

        /**
         * Write an integer to the stream.
         */
        public void WriteInt(int i) => WriteLong(i, false);

        /**
         * Write a positive integer to the stream. The integer is interpreted as a
         * two's complement unsigned integer even if it is negative.
         */
        public void WriteUInt(uint ui) => WriteLong(ui & 0xFFFFffffL, true);

        /**
         * Write a short to the stream.
         */
        public void WriteShort(short s) => WriteLong(s, false);

        /**
         * Write a positive short to the stream. The short is interpreted as a two's
         * complement unsigned short even if it is negative.
         */
        public void WriteUShort(ushort us) => WriteLong(us & 0xffffL, true);

        /**
         * Write an Erlang list header to the stream. After calling this method, you
         * must write 'arity' elements to the stream followed by nil, or it will not
         * be possible to decode it later.
         */
        public void WriteListHead(int arity)
        {
            if (arity == 0)
            {
                WriteNil();
            }
            else
            {
                Write1(OtpExternal.listTag);
                Write4BE(arity);
            }
        }

        /**
         * Write an empty Erlang list to the stream.
         */
        public void WriteNil() => Write1(OtpExternal.nilTag);

        /**
         * Write an Erlang tuple header to the stream. After calling this method,
         * you must write 'arity' elements to the stream or it will not be possible
         * to decode it later.
         */
        public void WriteTupleHead(int arity)
        {
            if (arity < 0xff)
            {
                Write1(OtpExternal.smallTupleTag);
                Write1(arity);
            }
            else
            {
                Write1(OtpExternal.largeTupleTag);
                Write4BE(arity);
            }
        }

        /**
         * Write an Erlang PID to the stream.
         */
        public void WritePid(string node, int id, int serial, int creation)
        {
            Write1(OtpExternal.newPidTag);
            WriteAtom(node);
            Write4BE(id & 0x7fff); // 15 bits
            Write4BE(serial & 0x1fff); // 13 bits
            Write1(creation & 0x3); // 2 bits
        }

        /**
         * Write an Erlang PID to the stream.
         */
        public void WritePid(OtpErlangPid pid)
        {
            Write1(OtpExternal.newPidTag);
            WriteAtom(pid.Node);
            Write4BE(pid.Id);
            Write4BE(pid.Serial);
            Write4BE(pid.Creation);
        }

        /**
         * Write an Erlang port to the stream.
         */
        public void WritePort(string node, int id, int creation)
        {
            Write1(OtpExternal.newPortTag);
            WriteAtom(node);
            Write4BE(id & 0xfffffff); // 28 bits
            Write1(creation & 0x3); // 2 bits
        }

        /**
         * Write an Erlang port to the stream.
         */
        public void WritePort(OtpErlangPort port)
        {
            Write1(OtpExternal.newPortTag);
            WriteAtom(port.Node);
            Write4BE(port.Id);
            Write4BE(port.Creation);
        }

        /**
         * Write an old style Erlang ref to the stream.
         */
        public void WriteRef(string node, int id, int creation)
        {
            /* Always encode as an extended reference; all
               participating parties are now expected to be
               able to decode extended references. */
            WriteRef(node, new int[1] { id }, creation);
        }

        /**
         * Write an Erlang ref to the stream.
         */
        public void WriteRef(string node, int[] ids, int creation)
        {
            int arity = ids.Length;
            if (arity > 3)
                arity = 3; // max 3 words in ref

            // r6 ref
            Write1(OtpExternal.newerRefTag);

            // how many id values
            Write2BE(arity);

            WriteAtom(node);

            Write1(creation & 0x3); // 2 bits

            // first int gets truncated to 18 bits
            Write4BE(ids[0] & 0x3ffff);

            // remaining ones are left as is
            for (int i = 1; i < arity; i++)
                Write4BE(ids[i]);
        }

        /**
         * Write an Erlang ref to the stream.
         */
        public void WriteRef(OtpErlangRef r)
        {
            int arity = r.Ids.Length;

            Write1(OtpExternal.newerRefTag);
            Write2BE(arity);
            WriteAtom(r.Node);
            Write4BE(r.Creation);
            for (int i = 0; i < arity; i++)
                Write4BE(r.Ids[i]);
        }

        /**
         * Write a string to the stream.
         */
        public void WriteString(string s)
        {
            int len = s.Length;

            switch (len)
            {
                case 0:
                    WriteNil();
                    break;
                default:
                    if (len <= 65535 && Is8bitString(s)) // 8-bit string
                    {
                        try
                        {
                            byte[] bytebuf = Encoding.GetEncoding("ISO-8859-1").GetBytes(s);
                            Write1(OtpExternal.stringTag);
                            Write2BE(len);
                            WriteN(bytebuf);
                        }
                        catch (EncoderFallbackException)
                        {
                            WriteNil(); // it should never ever get here...
                        }
                    }
                    else // unicode or longer, must code as list
                    {
                        int[] codePoints = OtpErlangString.ToCodePoints(s);
                        WriteListHead(codePoints.Length);
                        foreach (int codePoint in codePoints)
                        {
                            WriteInt(codePoint);
                        }
                        WriteNil();
                    }
                    break;
            }
        }

        private bool Is8bitString(string s)
        {
            for (int i = 0; i < s.Length; ++i)
            {
                char c = s[i];
                if (c < 0 || c > 255)
                {
                    return false;
                }
            }
            return true;
        }

        /**
         * Write an arbitrary Erlang term to the stream in compressed format.
         */
        public void WriteCompressed(IOtpErlangObject o) => WriteCompressed(o, CompressionLevel.Optimal);

        public void WriteCompressed(IOtpErlangObject o, CompressionLevel level)
        {
            try
            {
                OtpOutputStream oos = new OtpOutputStream(o);
                if (oos.Length < 5)
                {
                    oos.WriteTo(this);
                }
                else
                {
                    Write1(OtpExternal.compressedTag);
                    Write4BE(oos.Length);
                    DeflateStream dos = new DeflateStream(this, level, true);
                    oos.WriteTo(dos);
                    dos.Close();
                }
            }
            catch (ObjectDisposedException)
            {
                throw new ArgumentException("Intermediate stream failed for Erlang object " + o);
            }
            catch (IOException)
            {
                throw new ArgumentException("Intermediate stream failed for Erlang object " + o);
            }
        }

        public void WriteFun(OtpErlangPid pid, string module,
                      long old_index, int arity, byte[] md5,
                      long index, long uniq, IOtpErlangObject[] freeVars)
        {
            if (arity == -1)
            {
                Write1(OtpExternal.funTag);
                Write4BE(freeVars.Length);
                pid.Encode(this);
                WriteAtom(module);
                WriteLong(index);
                WriteLong(uniq);
                foreach (IOtpErlangObject fv in freeVars)
                {
                    fv.Encode(this);
                }
            }
            else
            {
                Write1(OtpExternal.newFunTag);
                long saveSizePos = Position;
                Write4BE(0); // this is where we patch in the size
                Write1(arity);
                WriteN(md5);
                Write4BE(index);
                Write4BE(freeVars.Length);
                WriteAtom(module);
                WriteLong(old_index);
                WriteLong(uniq);
                pid.Encode(this);
                foreach (IOtpErlangObject fv in freeVars)
                {
                    fv.Encode(this);
                }
                Poke4BE((int)saveSizePos, Position - saveSizePos);
            }
        }

        public void WriteExternalFun(string module, string function, int arity)
        {
            Write1(OtpExternal.externalFunTag);
            WriteAtom(module);
            WriteAtom(function);
            WriteLong(arity);
        }

        public void WriteMapHead(int arity)
        {
            Write1(OtpExternal.mapTag);
            Write4BE(arity);
        }
    }
}
