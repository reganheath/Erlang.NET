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
     * Provides a representation of Erlang binaries. Anything that can be
     * represented as a sequence of bytes can be made into an Erlang binary.
     */
    [Serializable]
    public class OtpErlangBinary : OtpErlangBitstr
    {
        /**
         * Create a binary from a byte array
         */
        public OtpErlangBinary(byte[] bin) : base(bin) { }

        /**
         * Create a binary from a stream containing a binary encoded in Erlang
         * external format.
         */
        public OtpErlangBinary(OtpInputStream buf) : base() => Bin = buf.ReadBinary();

        /**
         * Create a binary from an arbitrary Object. The object must implement
         */
        public OtpErlangBinary(object o) : base(o) { }

        /**
         * Convert this binary to the equivalent Erlang external representation.
         */
        public override void Encode(OtpOutputStream buf) => buf.WriteBinary(Bin);
    }
}
