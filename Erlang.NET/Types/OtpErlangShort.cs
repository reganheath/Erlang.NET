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
     * Provides a Java representation of Erlang integral types.
     */
    [Serializable]
    public class OtpErlangShort : OtpErlangLong
    {
        public short Value { get => ShortValue(); }

        /**
         * Create an Erlang integer from the given value.
         */
        public OtpErlangShort(short s) : base(s) { }

        /**
         * Create an Erlang integer from a stream containing an integer encoded in
         * Erlang external format.
         */
        public OtpErlangShort(OtpInputStream buf) : base(buf) { }
    }
}
