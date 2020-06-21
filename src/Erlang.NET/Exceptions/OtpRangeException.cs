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
namespace Erlang.NET
{
    /**
     * Exception raised when an attempt is made to create an Erlang term with data
     * that is out of range for the term in question.
     * 
     * @see OtpErlangByte
     * @see OtpErlangChar
     * @see OtpErlangInt
     * @see OtpErlangUInt
     * @see OtpErlangShort
     * @see OtpErlangUShort
     * @see OtpErlangLong
     */
    public class OtpRangeException : OtpException
    {
        /**
         * Provides a detailed message.
         */
        public OtpRangeException(string msg)
            : base(msg)
        {
        }
    }
}
