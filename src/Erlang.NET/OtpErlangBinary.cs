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

namespace Erlang.NET
{
    /**
     * Provides a Java representation of Erlang binaries. Anything that can be
     * represented as a sequence of bytes can be made into an Erlang binary.
     */
    [Serializable]
    public class OtpErlangBinary : OtpErlangBitstr
    {
        /**
         * Create a binary from a byte array
         * 
         * @param bin
         *                the array of bytes from which to create the binary.
         */
        public OtpErlangBinary(byte[] bin)
            : base(bin)
        {
        }

        /**
         * Create a binary from a stream containing a binary encoded in Erlang
         * external format.
         * 
         * @param buf
         *                the stream containing the encoded binary.
         * 
         * @exception OtpErlangDecodeException
         *                    if the buffer does not contain a valid external
         *                    representation of an Erlang binary.
         */
        public OtpErlangBinary(OtpInputStream buf)
            : base(new byte[0])
        {
            Bin = buf.read_binary();
            PadBits = 0;
        }

        /**
         * Create a binary from an arbitrary Java Object. The object must implement
         * java.io.Serializable or java.io.Externalizable.
         * 
         * @param o
         *                the object to serialize and create this binary from.
         */
        public OtpErlangBinary(object o)
            : base(o)
        {
        }

        /**
         * Convert this binary to the equivalent Erlang external representation.
         * 
         * @param buf
         *                an output stream to which the encoded binary should be
         *                written.
         */
        public override void Encode(OtpOutputStream buf)
        {
            buf.write_binary(Bin);
        }

        public override object Clone()
        {
            OtpErlangBinary that = (OtpErlangBinary)base.Clone();
            return that;
        }
    }
}
