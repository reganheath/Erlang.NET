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

        public StreamFlags Flags { get; set; }

        private long mark;

        public OtpInputStream(int capacity) : base(capacity) { }

        /**
         * Create a stream from a buffer containing encoded Erlang terms.
         */
        public OtpInputStream(byte[] buf) : base(buf) { }

        /**
         * Create a stream from a buffer containing encoded Erlang terms at the
         * given offset and length.
         */
        public OtpInputStream(byte[] buf, int offset, int length) : base(buf, offset, length) { }

        public void Mark() => mark = base.Position;

        public void Reset() => base.Position = mark;

        /**
         * Read an array of bytes from the stream. The method reads at most
         * len bytes from the input stream.
         */
        public byte[] ReadN(int len)
        {
            byte[] buf = new byte[len];
            ReadN(buf, 0, buf.Length);
            return buf;
        }

        /**
         * Read an array of bytes from the stream. The method reads at most
         * buf.length bytes from the input stream.
         */
        public int ReadN(byte[] buf) => ReadN(buf, 0, buf.Length);

        /**
         * Read an array of bytes from the stream. The method reads at most len
         * bytes from the input stream into offset off of the buffer.
         */
        public int ReadN(byte[] buf, int off, int len)
        {
            if (len == 0 && (base.Length - base.Position) == 0)
                return 0;
            int i = base.Read(buf, off, len);
            if (i < len)
                throw new OtpDecodeException("Cannot read from input stream");
            return i;
        }

        /**
         * Alias for peek1()
         */
        public int Peek() => Peek1();

        /**
         * Look ahead one position in the stream without consuming the byte found
         * there.
         */
        public int Peek1()
        {
            long cur = base.Position;
            int i = base.ReadByte();
            base.Position = cur;
            if (i == -1)
                throw new OtpDecodeException("Cannot read from input stream");
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
         */
        public int Read1()
        {
            int i = base.ReadByte();
            if (i == -1)
                throw new OtpDecodeException("Cannot read from input stream");
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
         */
        public int Read2BE() => (int)ReadBE(2);

        /**
         * Read a four byte big endian integer from the stream.
         */
        public int Read4BE() => (int)ReadBE(4);

        /**
         * Read a eight byte big endian integer from the stream.
         */
        public long Read8BE() => ReadBE(8);

        /**
         * Read a bigendian integer from the stream.
         */
        public long ReadBE(int n)
        {
            byte[] b = new byte[n];

            if (base.Read(b, 0, b.Length) < b.Length)
                throw new OtpDecodeException("Cannot read from input stream");

            if (BitConverter.IsLittleEndian)
                Array.Reverse(b, 0, b.Length);

            switch (n)
            {
                case 2: return BitConverter.ToUInt16(b, 0);
                case 4: return BitConverter.ToUInt32(b, 0);
                case 8: return BitConverter.ToInt64(b, 0);
                default:
                    throw new Exception("unsupported");
            }
        }

        /**
         * Read an Erlang atom from the stream and interpret the value as a boolean.
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
         */
        public string ReadAtom()
        {
            string atom;

            int tag = Read1SkipVersion();
            switch (tag)
            {
                case OtpExternal.atomTag:
                    atom = ReadStringData();
                    break;
                case OtpExternal.smallAtomUtf8Tag:
                case OtpExternal.atomUtf8Tag:
                    int len = (tag == OtpExternal.smallAtomUtf8Tag ? Read1() : Read2BE());
                    atom = ReadStringData(len, "UTF-8");
                    break;
                default:
                    throw new OtpDecodeException("wrong tag encountered, expected " + OtpExternal.atomTag
                        + ", or " + OtpExternal.atomUtf8Tag + ", got " + tag);
            }

            if (atom.Length > OtpExternal.MAX_ATOM_LENGTH)
                atom = atom.Substring(0, OtpExternal.MAX_ATOM_LENGTH);

            return atom;
        }

        /**
         * Read an Erlang binary from the stream.
         */
        public byte[] ReadBinary()
        {
            int tag = Read1SkipVersion();

            if (tag != OtpExternal.binTag)
                throw new OtpDecodeException("Wrong tag encountered, expected " + OtpExternal.binTag + ", got " + tag);

            return ReadN(Read4BE());
        }

        /**
         * Read an Erlang bitstr from the stream.
         */
        public byte[] ReadBitstr(out int padBits)
        {
            int tag = Read1SkipVersion();

            if (tag != OtpExternal.bitBinTag)
                throw new OtpDecodeException("Wrong tag encountered, expected " + OtpExternal.bitBinTag + ", got " + tag);

            int len = Read4BE();
            int tailBits = Read1();
            if (tailBits < 0 || 7 < tailBits)
                throw new OtpDecodeException("Wrong tail bit count in bitstr: " + tailBits);
            if (len == 0 && tailBits != 0)
                throw new OtpDecodeException("Length 0 on bitstr with tail bit count: " + tailBits);
            byte[] bin = ReadN(len);

            padBits = 8 - tailBits;
            return bin;
        }

        /**
         * Read an Erlang float from the stream.
         */
        public float ReadFloat()
        {
            double d = ReadDouble();
            return (float)d;
        }

        /**
         * Read an Erlang float from the stream.
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
                    string str = ReadStringData(31);
                    if (!double.TryParse(str, out val))
                        throw new OtpDecodeException("Invalid float format: '" + str + "'");
                    break;

                default:
                    throw new OtpDecodeException("Wrong tag encountered, expected " + OtpExternal.newFloatTag + ", got " + tag);
            }

            return val;
        }

        /**
         * Read one byte from the stream.
         */
        public new byte ReadByte()
        {
            long l = ReadLong(false);
            if (l > byte.MaxValue)
                throw new OtpDecodeException("Value does not fit in byte: " + l);
            return (byte)l;
        }

        /**
         * Read a character from the stream.
         */
        public char ReadChar()
        {
            long l = ReadLong(true);
            if (l > char.MaxValue)
                throw new OtpDecodeException("Value does not fit in char: " + l);
            return (char)l;
        }

        /**
         * Read an unsigned integer from the stream.
         */
        public uint ReadUInt()
        {
            long l = ReadLong(true);
            if (l > uint.MaxValue)
                throw new OtpDecodeException("Value does not fit in uint: " + l);
            return (uint)l;
        }

        /**
         * Read an integer from the stream.
         */
        public int ReadInt()
        {
            long l = ReadLong(false);
            if (l > int.MaxValue)
                throw new OtpDecodeException("Value does not fit in int: " + l);
            return (int)l;
        }

        /**
         * Read an unsigned short from the stream.
         */
        public ushort ReadUShort()
        {
            long l = ReadLong(true);
            if (l > ushort.MaxValue)
                throw new OtpDecodeException("Value does not fit in ushort: " + l);
            return (ushort)l;
        }

        /**
         * Read a short from the stream.
         */
        public short ReadShort()
        {
            long l = ReadLong(false);
            if (l > short.MaxValue)
                throw new OtpDecodeException("Value does not fit in short: " + l);
            return (short)l;
        }

        /**
         * Read an unsigned long from the stream.
         */
        public long ReadULong() => ReadLong(true);

        /**
         * Read a long from the stream.
         */
        public long ReadLong() => ReadLong(false);

        public long ReadLong(bool unsigned)
        {
            byte[] b = ReadIntegerByteArray();
            return ByteArrayToLong(b, unsigned);
        }

        /**
         * Read an integer from the stream.
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
                    if (ReadN(nb) != 4) // Big endian
                        throw new OtpDecodeException("Cannot read from intput stream");
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
                            throw new OtpDecodeException("Value of largeBig does not fit in BigInteger, arity " + arity + " sign " + sign);
                    }
                    nb = new byte[arity + 1];
                    // Value is read as little endian. The big end is augumented
                    // with one zero byte to make the value 2's complement positive.
                    if (ReadN(nb, 0, arity) != arity)
                        throw new OtpDecodeException("Cannot read from intput stream");

                    // Reverse the array to make it big endian.
                    Array.Reverse(nb, 0, nb.Length);

                    if (sign != 0)
                    {
                        // 2's complement negate the big endian value in the array
                        int c = 1; // Carry
                        for (int j = nb.Length; j-- > 0;)
                        {
                            c = (~nb[j] & 0xFF) + c;
                            nb[j] = (byte)c;
                            c >>= 8;
                        }
                    }
                    break;

                default:
                    throw new OtpDecodeException("Not valid integer tag: " + tag);
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
                        throw new OtpDecodeException("Value not unsigned: " + v);
                    break;
                case 4:
                    v = ((b[0] & 0xFF) << 24) + ((b[1] & 0xFF) << 16) + ((b[2] & 0xFF) << 8) + (b[3] & 0xFF);
                    v = (int)v; // Sign extend
                    if (v < 0 && unsigned)
                        throw new OtpDecodeException("Value not unsigned: " + v);
                    break;
                default:
                    int i = 0;
                    byte c = b[i];
                    // Skip non-essential leading bytes
                    if (unsigned)
                    {
                        if (c < 0)
                            throw new OtpDecodeException("Value not unsigned: " + b);
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
                    if (b.Length - i > 8) // More than 64 bits of value
                        throw new OtpDecodeException("Value does not fit in long: " + b);
                    // Convert the necessary bytes
                    for (v = c < 0 ? -1 : 0; i < b.Length; i++)
                        v = (v << 8) | (uint)(b[i] & 0xFF);
                    break;
            }
            return v;
        }

        /**
         * Read a list header from the stream.
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
                    throw new OtpDecodeException("Not valid list tag: " + tag);
            }

            return arity;
        }

        /**
         * Read a tuple header from the stream.
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
                    throw new OtpDecodeException("Not valid tuple tag: " + tag);
            }

            return arity;
        }

        /**
         * Read a map header from the stream.
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
                    throw new OtpDecodeException("Not valid map tag: " + tag);
            }

            return arity;
        }

        /**
         * Read an empty list from the stream.
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
                    throw new OtpDecodeException("Not valid nil tag: " + tag);
            }

            return arity;
        }

        /**
         * Read an Erlang PID from the stream.
         */
        public OtpErlangPid ReadPid()
        {
            int tag = Read1SkipVersion();
            if (tag != OtpExternal.pidTag && tag != OtpExternal.newPidTag)
                throw new OtpDecodeException("Wrong tag encountered, expected " + OtpExternal.pidTag + " or " + OtpExternal.newPidTag + ", got " + tag);

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
         */
        public OtpErlangPort ReadPort()
        {
            int tag = Read1SkipVersion();
            if (tag != OtpExternal.portTag && tag != OtpExternal.newPortTag)
                throw new OtpDecodeException("Wrong tag encountered, expected " + OtpExternal.portTag + " or " + OtpExternal.newPortTag + ", got " + tag);

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
                        throw new OtpDecodeException("Ref arity " + arity + " too large");
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
                    throw new OtpDecodeException("Wrong tag encountered, expected ref, got " + tag);
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
                IOtpErlangObject[] freeVars = new IOtpErlangObject[nFreeVars];
                for (int i = 0; i < nFreeVars; ++i)
                    freeVars[i] = ReadAny();

                return new OtpErlangFun(pid, module, index, uniq, freeVars);
            }
            else if (tag == OtpExternal.newFunTag)
            {
                Read4BE();
                int arity = Read1();
                byte[] md5 = ReadN(16);
                int index = Read4BE();
                int nFreeVars = Read4BE();
                string module = ReadAtom();
                long oldIndex = ReadLong();
                long uniq = ReadLong();
                OtpErlangPid pid = ReadPid();
                IOtpErlangObject[] freeVars = new IOtpErlangObject[nFreeVars];
                for (int i = 0; i < nFreeVars; ++i)
                    freeVars[i] = ReadAny();

                return new OtpErlangFun(pid, module, arity, md5, index, oldIndex, uniq, freeVars);
            }
            else
            {
                throw new OtpDecodeException("Wrong tag encountered, expected fun, got " + tag);
            }
        }

        public OtpErlangExternalFun ReadExternalFun()
        {
            int tag = Read1SkipVersion();
            if (tag != OtpExternal.externalFunTag)
                throw new OtpDecodeException("Wrong tag encountered, expected external fun, got " + tag);
            string module = ReadAtom();
            string function = ReadAtom();
            int arity = (int)ReadLong();
            return new OtpErlangExternalFun(module, function, arity);
        }

        /**
         * Read a string from the stream.
         */
        public string ReadString()
        {
            int len;
            int tag = Read1SkipVersion();
            switch (tag)
            {
                case OtpExternal.stringTag:
                    return ReadStringData();
                case OtpExternal.nilTag:
                    return "";
                case OtpExternal.listTag: // List when unicode +
                    len = Read4BE();
                    int[] cps = new int[len];
                    for (int i = 0; i < len; i++)
                        cps[i] = ReadInt();
                    string s = OtpErlangString.FromCodePoints(cps);
                    ReadNil();
                    return s;
                default:
                    throw new OtpDecodeException("Wrong tag encountered, expected " + OtpExternal.stringTag + " or " + OtpExternal.listTag + ", got " + tag);
            }
        }

        /**
         * Read length prefixed string data from stream
         */
        public string ReadStringData(string encoding = "ISO-8859-1") => ReadStringData(Read2BE(), encoding);

        /**
         * Read string data of len from stream
         */
        public string ReadStringData(int len, string encoding = "ISO-8859-1")
        {
            byte[] strbuf = ReadN(len);
            return OtpErlangString.FromEncoding(strbuf, encoding);
        }

        /**
         * Read a compressed term from the stream
         */
        public IOtpErlangObject ReadCompressed()
        {
            int tag = Read1SkipVersion();

            if (tag != OtpExternal.compressedTag)
                throw new OtpDecodeException("Wrong tag encountered, expected " + OtpExternal.compressedTag + ", got " + tag);

            int size = Read4BE();
            byte[] buf = new byte[size];
            DeflateStream dos = new DeflateStream(this, CompressionMode.Decompress, true);
            try
            {
                int dsize = dos.Read(buf, 0, size);
                if (dsize != size)
                    throw new OtpDecodeException("Decompression gave " + dsize + " bytes, not " + size);
            }
            catch (OtpDecodeException)
            {
                throw;
            }
            catch (InvalidDataException e)
            {
                throw new OtpDecodeException("Failed to decode compressed", e);
            }

            OtpInputStream ois = new OtpInputStream(buf) { Flags = Flags };
            return ois.ReadAny();
        }

        /**
         * Read an arbitrary Erlang term from the stream.
         */
        public IOtpErlangObject ReadAny()
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
                        catch (OtpDecodeException) { }
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
                    throw new OtpDecodeException("Uknown data type: " + tag);
            }
        }
    }
}
