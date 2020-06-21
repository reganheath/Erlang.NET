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
            : base()
        {
            Bin = buf.ReadBinary();
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
        public override void Encode(OtpOutputStream buf) => buf.WriteBinary(Bin);

        public override object Clone() => (OtpErlangBinary)base.Clone();
    }
}
