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
     * Provides a Java representation of Erlang floats and doubles.
     */
    [Serializable]
    public class OtpErlangFloat : OtpErlangDouble
    {
        public new float Value { get => ToFloat(); }

        /**
         * Create an Erlang float from the given float value.
         */
        public OtpErlangFloat(float f) : base(f) { }

        /**
         * Create an Erlang float from a stream containing a float encoded in Erlang
         * external format.
         * 
         * @param buf
         *                the stream containing the encoded value.
         * 
         * @exception OtpErlangDecodeException
         *                    if the buffer does not contain a valid external
         *                    representation of an Erlang float.
         * 
         * @exception OtpErlangRangeException
         *                    if the value cannot be represented as a Java float.
         */
        public OtpErlangFloat(OtpInputStream buf) : base(buf) { }
    }
}
