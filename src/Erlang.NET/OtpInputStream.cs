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
using System.IO;
using System.IO.Compression;
using System.Text;

namespace Erlang.NET
{
    /**
     * Provides a stream for decoding Erlang terms from external format.
     * 
     * <p>
     * Note that this class is not synchronized, if you need synchronization you
     * must provide it yourself.
     */
    public class OtpInputStream : MemoryStream
    {
        [Flags] public enum StreamFlags { DecodeIntListsAsStrings };

        public StreamFlags Flags { get; private set; }

        private long mark;

        public OtpInputStream(int capacity, StreamFlags flags)
            : base(capacity)
        {
            Flags = flags;
        }

        /**
         * @param buf
         */
        public OtpInputStream(byte[] buf)
            : base(buf)
        {
        }

        /**
         * Create a stream from a buffer containing encoded Erlang terms.
         * 
         * @param flags
         */
        public OtpInputStream(byte[] buf, StreamFlags flags)
            : base(buf)
        {
            Flags = flags;
        }

        /**
         * Create a stream from a buffer containing encoded Erlang terms at the
         * given offset and length.
         * 
         * @param flags
         */
        public OtpInputStream(byte[] buf, int offset, int length, StreamFlags flags)
            : base(buf, offset, length)
        {
            Flags = flags;
        }

        public void Mark(int readlimit) => mark = base.Position;

        public void Reset() => base.Position = mark;


        /**
         * Read an array of bytes from the stream. The method reads at most
         * buf.length bytes from the input stream.
         * 
         * @return the number of bytes read.
         * 
         * @exception OtpErlangDecodeException
         *                if the next byte cannot be read.
         */
        public int ReadN(byte[] buf)
        {
            return ReadN(buf, 0, buf.Length);
        }

        /**
         * Read an array of bytes from the stream. The method reads at most len
         * bytes from the input stream into offset off of the buffer.
         * 
         * @return the number of bytes read.
         * 
         * @exception OtpErlangDecodeException
         *                if the next byte cannot be read.
         */
        public int ReadN(byte[] buf, int off, int len)
        {
            if (len == 0 && (base.Length - base.Position) == 0)
                return 0;
            int i = base.Read(buf, off, len);
            if (i < len)
                throw new OtpErlangDecodeException("Cannot read from input stream");
            return i;
        }

        /**
         * Alias for peek1()
         */
        public int Peek() => Peek1();

        /**
         * Look ahead one position in the stream without consuming the byte found
         * there.
         * 
         * @return the next byte in the stream, as an integer.
         * 
         * @exception OtpErlangDecodeException
         *                if the next byte cannot be read.
         */
        public int Peek1()
        {
            long cur = base.Position;
            int i = base.ReadByte();
            base.Position = cur;
            //base.Seek(cur, SeekOrigin.Begin);
            if (i == -1)
                throw new OtpErlangDecodeException("Cannot read from input stream");
            return i;
        }

        public int Peek1SkipVersion()
        {
            int i = Peek1();
            if (i == OtpExternal.versionTag)
            {
                _ = Read1();
                i = Peek1();
            }
            return i;
        }

        /**
         * Read a one byte integer from the stream.
         * 
         * @return the byte read, as an integer.
         * 
         * @exception OtpErlangDecodeException
         *                if the next byte cannot be read.
         */
        public int Read1()
        {
            int i = base.ReadByte();
            if (i == -1)
                throw new OtpErlangDecodeException("Cannot read from input stream");
            return i;
        }

        public int Read1SkipVersion()
        {
            int tag = Read1();
            if (tag == OtpExternal.versionTag)
                tag = Read1();
            return tag;
        }

        /**
         * Read a two byte big endian integer from the stream.
         * 
         * @return the bytes read, converted from big endian to an integer.
         * 
         * @exception OtpErlangDecodeException
         *                if the next byte cannot be read.
         */
        public int Read2BE()
        {
            byte[] b = new byte[2];

            if (base.Read(b, 0, b.Length) < b.Length)
                throw new OtpErlangDecodeException("Cannot read from input stream");

            return (b[0] << 8 & 0xff00) + (b[1] & 0xff);
        }

        /**
         * Read a four byte big endian integer from the stream.
         * 
         * @return the bytes read, converted from big endian to an integer.
         * 
         * @exception OtpErlangDecodeException
         *                if the next byte cannot be read.
         */
        public int Read4BE()
        {
            byte[] b = new byte[4];

            if (base.Read(b, 0, b.Length) < b.Length)
                throw new OtpErlangDecodeException("Cannot read from input stream");

            return (int)((b[0] << 24 & 0xff000000) + (b[1] << 16 & 0xff0000) + (b[2] << 8 & 0xff00) + (b[3] & 0xff));
        }

        /**
         * Read a eight byte big endian integer from the stream.
         *
         * @return the bytes read, converted from big endian to a long integer.
         *
         * @exception OtpErlangDecodeException
         *                if the next byte cannot be read.
         */
        public long Read8BE()
        {
            long high = Read4BE();
            long low = Read4BE();
            return (high << 32) | (low & 0xffffffff);
        }

        /**
         * Read a two byte little endian integer from the stream.
         * 
         * @return the bytes read, converted from little endian to an integer.
         * 
         * @exception OtpErlangDecodeException
         *                if the next byte cannot be read.
         */
        public int Read2LE()
        {
            byte[] b = new byte[2];

            if (base.Read(b, 0, b.Length) < b.Length)
                throw new OtpErlangDecodeException("Cannot read from input stream");

            return (b[1] << 8 & 0xff00) + (b[0] & 0xff);
        }

        /**
         * Read a four byte little endian integer from the stream.
         * 
         * @return the bytes read, converted from little endian to an integer.
         * 
         * @exception OtpErlangDecodeException
         *                if the next byte cannot be read.
         */
        public int Read4LE()
        {
            byte[] b = new byte[4];

            if (base.Read(b, 0, b.Length) < b.Length)
                throw new OtpErlangDecodeException("Cannot read from input stream");

            return (int)((b[3] << 24 & 0xff000000) + (b[2] << 16 & 0xff0000) + (b[1] << 8 & 0xff00) + (b[0] & 0xff));
        }

        /**
         * Read a little endian integer from the stream.
         * 
         * @param n
         *            the number of bytes to read
         * 
         * @return the bytes read, converted from little endian to an integer.
         * 
         * @exception OtpErlangDecodeException
         *                if the next byte cannot be read.
         */
        public long ReadLE(int n)
        {
            byte[] b = new byte[n];

            if (base.Read(b, 0, b.Length) < b.Length)
                throw new OtpErlangDecodeException("Cannot read from input stream");

            long v = 0;
            while (n-- > 0)
            {
                v = v << 8 | (long)b[n] & 0xff;
            }

            return v;
        }

        /**
         * Read a bigendian integer from the stream.
         * 
         * @param n
         *            the number of bytes to read
         * 
         * @return the bytes read, converted from big endian to an integer.
         * 
         * @exception OtpErlangDecodeException
         *                if the next byte cannot be read.
         */
        public long ReadBE(int n)
        {
            byte[] b = new byte[n];

            if (base.Read(b, 0, b.Length) < b.Length)
                throw new OtpErlangDecodeException("Cannot read from input stream");

            long v = 0;
            for (int i = 0; i < n; i++)
            {
                v = v << 8 | (long)b[i] & 0xff;
            }

            return v;
        }

        /**
         * Read an Erlang atom from the stream and interpret the value as a boolean.
         * 
         * @return true if the atom at the current position in the stream contains
         *         the value 'true' (ignoring case), false otherwise.
         * 
         * @exception OtpErlangDecodeException
         *                if the next term in the stream is not an atom.
         */
        public bool ReadBoolean()
        {
            try { return bool.Parse(ReadAtom()); }
            catch (FormatException)
            {
                return false;
            }
        }

        /**
         * Read an Erlang atom from the stream.
         * 
         * @return a string containing the value of the atom.
         * 
         * @exception OtpErlangDecodeException
         *                if the next term in the stream is not an atom.
         */
        public string ReadAtom()
        {
            int len;
            byte[] strbuf;
            string atom;

            int tag = Read1SkipVersion();
            switch (tag)
            {
                case OtpExternal.atomTag:
                    len = Read2BE();
                    strbuf = new byte[len];
                    ReadN(strbuf);
                    atom = OtpErlangString.FromEncoding(strbuf);
                    break;
                case OtpExternal.smallAtomUtf8Tag:
                case OtpExternal.atomUtf8Tag:
                    len = (tag == OtpExternal.smallAtomUtf8Tag ? Read1() : Read2BE());
	                strbuf = new byte[len];
	                ReadN(strbuf);
                    atom = OtpErlangString.FromEncoding(strbuf, "UTF-8");
	                break;
                default:
                    throw new OtpErlangDecodeException("wrong tag encountered, expected " + OtpExternal.atomTag 
                        + ", or " + OtpExternal.atomUtf8Tag + ", got " + tag);
            }

            if (atom.Length > OtpExternal.maxAtomLength)
                atom = atom.Substring(0, OtpExternal.maxAtomLength);

            return atom;
        }

        /**
         * Read an Erlang binary from the stream.
         * 
         * @return a byte array containing the value of the binary.
         * 
         * @exception OtpErlangDecodeException
         *                if the next term in the stream is not a binary.
         */
        public byte[] ReadBinary()
        {
            int tag = Read1SkipVersion();

            if (tag != OtpExternal.binTag)
                throw new OtpErlangDecodeException("Wrong tag encountered, expected " + OtpExternal.binTag + ", got " + tag);

            int len = Read4BE();
            byte[] bin = new byte[len];
            ReadN(bin);

            return bin;
        }

        /**
         * Read an Erlang bitstr from the stream.
         * 
         * @param pad_bits
         *            an int array whose first element will be set to the number of
         *            pad bits in the last byte.
         * 
         * @return a byte array containing the value of the bitstr.
         * 
         * @exception OtpErlangDecodeException
         *                if the next term in the stream is not a bitstr.
         */
        public byte[] ReadBitstr(out int pad_bits)
        {
            int tag = Read1SkipVersion();

            if (tag != OtpExternal.bitBinTag)
                throw new OtpErlangDecodeException("Wrong tag encountered, expected " + OtpExternal.bitBinTag + ", got " + tag);

            int len = Read4BE();
            byte[] bin = new byte[len];
            int tail_bits = Read1();
            if (tail_bits < 0 || 7 < tail_bits)
                throw new OtpErlangDecodeException("Wrong tail bit count in bitstr: " + tail_bits);
            if (len == 0 && tail_bits != 0)
                throw new OtpErlangDecodeException("Length 0 on bitstr with tail bit count: " + tail_bits);
            ReadN(bin);

            pad_bits = 8 - tail_bits;
            return bin;
        }

        /**
         * Read an Erlang float from the stream.
         * 
         * @return the float value.
         * 
         * @exception OtpErlangDecodeException
         *                if the next term in the stream is not a float.
         */
        public float ReadFloat()
        {
            double d = ReadDouble();
            return (float)d;
        }

        /**
         * Read an Erlang float from the stream.
         * 
         * @return the float value, as a double.
         * 
         * @exception OtpErlangDecodeException
         *                if the next term in the stream is not a float.
         */
        public double ReadDouble()
        {
            double val;
            int tag;

            // parse the stream
            tag = Read1SkipVersion();

            switch (tag)
            {
                case OtpExternal.newFloatTag:
                    val = BitConverter.ToDouble(BitConverter.GetBytes(ReadBE(8)), 0);
                    break;

                case OtpExternal.floatTag:
                    // get the string
                    byte[] strbuf = new byte[31];
                    this.ReadN(strbuf);
                    string str = OtpErlangString.FromEncoding(strbuf);
                    if (!double.TryParse(str, out val))
                        throw new OtpErlangDecodeException("Invalid float format: '" + str + "'");
                    break;

                default:
                    throw new OtpErlangDecodeException("Wrong tag encountered, expected " + OtpExternal.newFloatTag + ", got " + tag);
            }

            return val;
        }

        /**
         * Read one byte from the stream.
         * 
         * @return the byte read.
         * 
         * @exception OtpErlangDecodeException
         *                if the next byte cannot be read.
         */
        public new byte ReadByte()
        {
            long l = ReadLong(false);
            if (l > byte.MaxValue)
                throw new OtpErlangDecodeException("Value does not fit in byte: " + l);
            return (byte)l;
        }

        /**
         * Read a character from the stream.
         * 
         * @return the character value.
         * 
         * @exception OtpErlangDecodeException
         *                if the next term in the stream is not an integer that can
         *                be represented as a char.
         */
        public char ReadChar()
        {
            long l = ReadLong(true);
            if (l > char.MaxValue)
                throw new OtpErlangDecodeException("Value does not fit in char: " + l);
            return (char)l;
        }

        /**
         * Read an unsigned integer from the stream.
         * 
         * @return the integer value.
         * 
         * @exception OtpErlangDecodeException
         *                if the next term in the stream can not be represented as a
         *                positive integer.
         */
        public uint ReadUInt()
        {
            long l = ReadLong(true);
            if (l > uint.MaxValue)
                throw new OtpErlangDecodeException("Value does not fit in uint: " + l);
            return (uint)l;
        }

        /**
         * Read an integer from the stream.
         * 
         * @return the integer value.
         * 
         * @exception OtpErlangDecodeException
         *                if the next term in the stream can not be represented as
         *                an integer.
         */
        public int ReadInt()
        {
            long l = ReadLong(false);
            if (l > int.MaxValue)
                throw new OtpErlangDecodeException("Value does not fit in int: " + l);
            return (int)l;
        }

        /**
         * Read an unsigned short from the stream.
         * 
         * @return the short value.
         * 
         * @exception OtpErlangDecodeException
         *                if the next term in the stream can not be represented as a
         *                positive short.
         */
        public ushort ReadUShort()
        {
            long l = this.ReadLong(true);
            if (l > ushort.MaxValue)
                throw new OtpErlangDecodeException("Value does not fit in ushort: " + l);
            return (ushort)l;
        }

        /**
         * Read a short from the stream.
         * 
         * @return the short value.
         * 
         * @exception OtpErlangDecodeException
         *                if the next term in the stream can not be represented as a
         *                short.
         */
        public short ReadShort()
        {
            long l = ReadLong(false);
            if (l > short.MaxValue)
                throw new OtpErlangDecodeException("Value does not fit in short: " + l);
            return (short)l;
        }

        /**
         * Read an unsigned long from the stream.
         * 
         * @return the long value.
         * 
         * @exception OtpErlangDecodeException
         *                if the next term in the stream can not be represented as a
         *                positive long.
         */
        public long ReadULong() => ReadLong(true);

        /**
         * Read a long from the stream.
         * 
         * @return the long value.
         * 
         * @exception OtpErlangDecodeException
         *                if the next term in the stream can not be represented as a
         *                long.
         */
        public long ReadLong() => ReadLong(false);

        public long ReadLong(bool unsigned)
        {
            byte[] b = ReadIntegerByteArray();
            return ByteArrayToLong(b, unsigned);
        }

        /**
         * Read an integer from the stream.
         * 
         * @return the value as a big endian 2's complement byte array.
         * 
         * @exception OtpErlangDecodeException
         *                if the next term in the stream is not an integer.
         */
        public byte[] ReadIntegerByteArray()
        {
            int tag;
            byte[] nb;

            tag = Read1SkipVersion();

            switch (tag)
            {
                case OtpExternal.smallIntTag:
                    nb = new byte[2];
                    nb[0] = 0;
                    nb[1] = (byte)Read1();
                    break;

                case OtpExternal.intTag:
                    nb = new byte[4];
                    if (this.ReadN(nb) != 4) // Big endian
                        throw new OtpErlangDecodeException("Cannot read from intput stream");
                    break;

                case OtpExternal.smallBigTag:
                case OtpExternal.largeBigTag:
                    int arity;
                    int sign;
                    if (tag == OtpExternal.smallBigTag)
                    {
                        arity = Read1();
                        sign = Read1();
                    }
                    else
                    {
                        arity = Read4BE();
                        sign = Read1();
                        if (arity + 1 < 0)
                            throw new OtpErlangDecodeException("Value of largeBig does not fit in BigInteger, arity " + arity + " sign " + sign);
                    }
                    nb = new byte[arity + 1];
                    // Value is read as little endian. The big end is augumented
                    // with one zero byte to make the value 2's complement positive.
                    if (ReadN(nb, 0, arity) != arity)
                        throw new OtpErlangDecodeException("Cannot read from intput stream");
                    
                    // Reverse the array to make it big endian.
                    for (int i = 0, j = nb.Length; i < j--; i++)
                        (nb[i], nb[j]) = (nb[j], nb[i]);

                    if (sign != 0)
                    {
                        // 2's complement negate the big endian value in the array
                        int c = 1; // Carry
                        for (int j = nb.Length; j-- > 0; )
                        {
                            c = (~nb[j] & 0xFF) + c;
                            nb[j] = (byte)c;
                            c >>= 8;
                        }
                    }
                    break;

                default:
                    throw new OtpErlangDecodeException("Not valid integer tag: " + tag);
            }

            return nb;
        }

        public static long ByteArrayToLong(byte[] b, bool unsigned)
        {
            long v;
            switch (b.Length)
            {
                case 0:
                    v = 0;
                    break;
                case 2:
                    v = ((b[0] & 0xFF) << 8) + (b[1] & 0xFF);
                    v = (short)v; // Sign extend
                    if (v < 0 && unsigned)
                        throw new OtpErlangDecodeException("Value not unsigned: " + v);
                    break;
                case 4:
                    v = ((b[0] & 0xFF) << 24) + ((b[1] & 0xFF) << 16)
                        + ((b[2] & 0xFF) << 8) + (b[3] & 0xFF);
                    v = (int)v; // Sign extend
                    if (v < 0 && unsigned)
                        throw new OtpErlangDecodeException("Value not unsigned: " + v);
                    break;
                default:
                    int i = 0;
                    byte c = b[i];
                    // Skip non-essential leading bytes
                    if (unsigned)
                    {
                        if (c < 0)
                            throw new OtpErlangDecodeException("Value not unsigned: " + b);
                        while (b[i] == 0)
                            i++; // Skip leading zero sign bytes
                    }
                    else
                    {
                        if (c == 0 || c == 0xFF) // Leading sign byte
                        {
                            i = 1;
                            // Skip all leading sign bytes
                            while (i < b.Length && b[i] == c)
                                i++;
                            if (i < b.Length)
                            {
                                // Check first non-sign byte to see if its sign
                                // matches the whole number's sign. If not one more
                                // byte is needed to represent the value.
                                if (((c ^ b[i]) & 0x80) != 0)
                                {
                                    i--;
                                }
                            }
                        }
                    }
                    if (b.Length - i > 8)
                    {
                        // More than 64 bits of value
                        throw new OtpErlangDecodeException("Value does not fit in long: " + b);
                    }
                    // Convert the necessary bytes
                    for (v = c < 0 ? -1 : 0; i < b.Length; i++)
                    {
                        v = (long)((((ulong)v) << 8) | ((ulong)(b[i] & 0xFF)));
                    }
                    break;
            }
            return v;
        }

        /**
         * Read a list header from the stream.
         * 
         * @return the arity of the list.
         * 
         * @exception OtpErlangDecodeException
         *                if the next term in the stream is not a list.
         */
        public int ReadListHead()
        {
            int arity;

            int tag = Read1SkipVersion();
            switch (tag)
            {
                case OtpExternal.nilTag:
                    arity = 0;
                    break;

                case OtpExternal.stringTag:
                    arity = Read2BE();
                    break;

                case OtpExternal.listTag:
                    arity = Read4BE();
                    break;

                default:
                    throw new OtpErlangDecodeException("Not valid list tag: " + tag);
            }

            return arity;
        }

        /**
         * Read a tuple header from the stream.
         * 
         * @return the arity of the tuple.
         * 
         * @exception OtpErlangDecodeException
         *                if the next term in the stream is not a tuple.
         */
        public int ReadTupleHead()
        {
            int arity;

            int tag = Read1SkipVersion();
            switch (tag)
            {
                case OtpExternal.smallTupleTag:
                    arity = Read1();
                    break;

                case OtpExternal.largeTupleTag:
                    arity = Read4BE();
                    break;

                default:
                    throw new OtpErlangDecodeException("Not valid tuple tag: " + tag);
            }

            return arity;
        }

        /**
         * Read a map header from the stream.
         * 
         * @return the arity of the map.
         * 
         * @exception OtpErlangDecodeException
         *                if the next term in the stream is not a map.
         */
        public int ReadMapHead()
        {
            int arity;

            int tag = Read1SkipVersion();
            switch (tag)
            {
                case OtpExternal.mapTag:
                    arity = Read4BE();
                    break;

                default:
                    throw new OtpErlangDecodeException("Not valid map tag: " + tag);
            }

            return arity;
        }

        /**
         * Read an empty list from the stream.
         * 
         * @return zero (the arity of the list).
         * 
         * @exception OtpErlangDecodeException
         *                if the next term in the stream is not an empty list.
         */
        public int ReadNil()
        {
            int arity;

            int tag = Read1SkipVersion();
            switch (tag)
            {
                case OtpExternal.nilTag:
                    arity = 0;
                    break;

                default:
                    throw new OtpErlangDecodeException("Not valid nil tag: " + tag);
            }

            return arity;
        }

        /**
         * Read an Erlang PID from the stream.
         * 
         * @return the value of the PID.
         * 
         * @exception OtpErlangDecodeException
         *                if the next term in the stream is not an Erlang PID.
         */
        public OtpErlangPid ReadPid()
        {
            int tag = Read1SkipVersion();
            if (tag != OtpExternal.pidTag && tag != OtpExternal.newPidTag)
                throw new OtpErlangDecodeException("Wrong tag encountered, expected " + OtpExternal.pidTag + " or " + OtpExternal.newPidTag + ", got " + tag);

            string node = ReadAtom();
            int id = Read4BE();
            int serial = Read4BE();
            int creation;
            if (tag == OtpExternal.pidTag)
                creation = Read1();
            else
                creation = Read4BE();

            return new OtpErlangPid(tag, node, id, serial, creation);
        }

        /**
         * Read an Erlang port from the stream.
         * 
         * @return the value of the port.
         * 
         * @exception OtpErlangDecodeException
         *                if the next term in the stream is not an Erlang port.
         */
        public OtpErlangPort ReadPort()
        {
            int tag = Read1SkipVersion();
            if (tag != OtpExternal.portTag && tag != OtpExternal.newPortTag)
                throw new OtpErlangDecodeException("Wrong tag encountered, expected " + OtpExternal.portTag + " or " + OtpExternal.newPortTag + ", got " + tag);

            string node = ReadAtom();
            int id = Read4BE();
            int creation;
            if (tag == OtpExternal.portTag)
                creation = Read1();
            else
                creation = Read4BE();

            return new OtpErlangPort(tag, node, id, creation);
        }

        /**
         * Read an Erlang reference from the stream.
         * 
         * @return the value of the reference
         * 
         * @exception OtpErlangDecodeException
         *                if the next term in the stream is not an Erlang reference.
         */
        public OtpErlangRef ReadRef()
        {
            string node;
            int creation;

            int tag = Read1SkipVersion();
            switch (tag)
            {
                case OtpExternal.refTag:
                    node = ReadAtom();
                    int id = Read4BE() & 0x3ffff;
                    creation = Read1() & 0x03; // 2 bits
                    return new OtpErlangRef(node, id, creation);

                case OtpExternal.newRefTag:
                case OtpExternal.newerRefTag:
                    int arity = Read2BE();
                    if (arity > 3)
                        throw new OtpErlangDecodeException("Ref arity " + arity + " too large");
                    node = ReadAtom();
                    if (tag == OtpExternal.newRefTag)
                        creation = Read1();
                    else
                        creation = Read4BE();

                    int[] ids = new int[arity];
                    for (int i = 0; i < arity; i++)
                        ids[i] = Read4BE();

                    return new OtpErlangRef(tag, node, ids, creation);

                default:
                    throw new OtpErlangDecodeException("Wrong tag encountered, expected ref, got " + tag);
            }
        }

        public OtpErlangFun ReadFun()
        {
            int tag = Read1SkipVersion();
            if (tag == OtpExternal.funTag)
            {
                int nFreeVars = Read4BE();
                OtpErlangPid pid = ReadPid();
                string module = ReadAtom();
                long index = ReadLong();
                long uniq = ReadLong();
                OtpErlangObject[] freeVars = new OtpErlangObject[nFreeVars];
                for (int i = 0; i < nFreeVars; ++i)
                    freeVars[i] = ReadAny();

                return new OtpErlangFun(pid, module, index, uniq, freeVars);
            }
            else if (tag == OtpExternal.newFunTag)
            {
                Read4BE();
                int arity = Read1();
                byte[] md5 = new byte[16];
                ReadN(md5);
                int index = Read4BE();
                int nFreeVars = Read4BE();
                string module = ReadAtom();
                long oldIndex = ReadLong();
                long uniq = ReadLong();
                OtpErlangPid pid = ReadPid();
                OtpErlangObject[] freeVars = new OtpErlangObject[nFreeVars];
                for (int i = 0; i < nFreeVars; ++i)
                    freeVars[i] = ReadAny();

                return new OtpErlangFun(pid, module, arity, md5, index, oldIndex, uniq, freeVars);
            }
            else
            {
                throw new OtpErlangDecodeException("Wrong tag encountered, expected fun, got " + tag);
            }
        }

        public OtpErlangExternalFun ReadExternalFun()
        {
            int tag = Read1SkipVersion();
            if (tag != OtpExternal.externalFunTag)
                throw new OtpErlangDecodeException("Wrong tag encountered, expected external fun, got " + tag);
            string module = ReadAtom();
            string function = ReadAtom();
            int arity = (int)ReadLong();
            return new OtpErlangExternalFun(module, function, arity);
        }

        /**
         * Read a string from the stream.
         * 
         * @return the value of the string.
         * 
         * @exception OtpErlangDecodeException
         *                if the next term in the stream is not a string.
         */
        public string ReadString()
        {
            int len;
            int tag = Read1SkipVersion();
            switch (tag)
            {
                case OtpExternal.stringTag:
                    len = Read2BE();
                    byte[] strbuf = new byte[len];
                    ReadN(strbuf);
                    return OtpErlangString.FromEncoding(strbuf);
                case OtpExternal.nilTag:
                    return "";
                case OtpExternal.listTag: // List when unicode +
                    len = Read4BE();
                    int[] cps = new int[len];
                    for (int i = 0; i < len; i++)
                        cps[i] = ReadInt();
                    var s = OtpErlangString.FromCodePoints(cps);
                    ReadNil();
                    return s;
                default:
                    throw new OtpErlangDecodeException("Wrong tag encountered, expected " + OtpExternal.stringTag + " or " + OtpExternal.listTag + ", got " + tag);
            }
        }

        /**
         * Read a compressed term from the stream
         * 
         * @return the resulting uncompressed term.
         * 
         * @exception OtpErlangDecodeException
         *                if the next term in the stream is not a compressed term.
         */
        public OtpErlangObject ReadCompressed()
        {
            int tag = Read1SkipVersion();

            if (tag != OtpExternal.compressedTag)
                throw new OtpErlangDecodeException("Wrong tag encountered, expected " + OtpExternal.compressedTag + ", got " + tag);

            int size = Read4BE();
            byte[] buf = new byte[size];
            DeflateStream dos = new DeflateStream(this, CompressionMode.Decompress, true);
            try
            {
                int dsize = dos.Read(buf, 0, size);
                if (dsize != size)
                    throw new OtpErlangDecodeException("Decompression gave " + dsize + " bytes, not " + size);
            }
            catch (OtpErlangDecodeException)
            {
                throw;
            }
            catch (InvalidDataException e)
            {
                throw new OtpErlangDecodeException("Failed to decode compressed", e);
            }

            OtpInputStream ois = new OtpInputStream(buf, Flags);
            return ois.ReadAny();
        }

        /**
         * Read an arbitrary Erlang term from the stream.
         * 
         * @return the Erlang term.
         * 
         * @exception OtpErlangDecodeException
         *                if the stream does not contain a known Erlang type at the
         *                next position.
         */
        public OtpErlangObject ReadAny()
        {
            // calls one of the above functions, depending on o
            int tag = Peek1SkipVersion();

            switch (tag)
            {
                case OtpExternal.smallIntTag:
                case OtpExternal.intTag:
                case OtpExternal.smallBigTag:
                case OtpExternal.largeBigTag:
                    return new OtpErlangLong(this);

                case OtpExternal.atomTag:
                case OtpExternal.smallAtomUtf8Tag:
                case OtpExternal.atomUtf8Tag:
                    return new OtpErlangAtom(this);

                case OtpExternal.floatTag:
                case OtpExternal.newFloatTag:
                    return new OtpErlangDouble(this);

                case OtpExternal.refTag:
                case OtpExternal.newRefTag:
                case OtpExternal.newerRefTag:
                    return new OtpErlangRef(this);

                case OtpExternal.mapTag:
                    return new OtpErlangMap(this);

                case OtpExternal.portTag:
                case OtpExternal.newPortTag:
                    return new OtpErlangPort(this);

                case OtpExternal.pidTag:
                case OtpExternal.newPidTag:
                    return new OtpErlangPid(this);

                case OtpExternal.stringTag:
                    return new OtpErlangString(this);

                case OtpExternal.listTag:
                case OtpExternal.nilTag:
                    if (Flags.HasFlag(StreamFlags.DecodeIntListsAsStrings))
                    {
                        long savePos = Position;
                        try { return new OtpErlangString(this); }
                        catch (OtpErlangDecodeException) { }
                        Position = savePos;
                    }
                    return new OtpErlangList(this);

                case OtpExternal.smallTupleTag:
                case OtpExternal.largeTupleTag:
                    return new OtpErlangTuple(this);

                case OtpExternal.binTag:
                    return new OtpErlangBinary(this);

                case OtpExternal.bitBinTag:
                    return new OtpErlangBitstr(this);

                case OtpExternal.compressedTag:
                    return ReadCompressed();

                case OtpExternal.newFunTag:
                case OtpExternal.funTag:
                    return new OtpErlangFun(this);

                case OtpExternal.externalFunTag:
                    return new OtpErlangExternalFun(this);

                default:
                    throw new OtpErlangDecodeException("Uknown data type: " + tag);
            }
        }
    }
}
