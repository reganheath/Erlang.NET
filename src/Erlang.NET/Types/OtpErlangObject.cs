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
     * Base class of the Erlang data type classes. This class is used to represent
     * an arbitrary Erlang term.
     */
    [Serializable]
    public abstract class OtpErlangObject : ICloneable
    {
        protected int hashCodeValue = 0;

        /**
         * @return the printable representation of the object. This is usually
         *         similar to the representation used by Erlang for the same type of
         *         object.
         */
        public abstract override string ToString();

        /**
         * Convert the object according to the rules of the Erlang external format.
         * This is mainly used for sending Erlang terms in messages, however it can
         * also be used for storing terms to disk.
         * 
         * @param buf
         *                an output stream to which the encoded term should be
         *                written.
         */
        public abstract void Encode(OtpOutputStream buf);

        /**
         * Read binary data in the Erlang external format, and produce a
         * corresponding Erlang data type object. This method is normally used when
         * Erlang terms are received in messages, however it can also be used for
         * reading terms from disk.
         * 
         * @param buf
         *                an input stream containing one or more encoded Erlang
         *                terms.
         * 
         * @return an object representing one of the Erlang data types.
         * 
         * @exception OtpErlangDecodeException
         *                    if the stream does not contain a valid representation
         *                    of an Erlang term.
         */
        public static OtpErlangObject Decode(OtpInputStream buf) => buf.ReadAny();

        /**
         * Determine if two Erlang objects are equal. In general, Erlang objects are
         * equal if the components they consist of are equal.
         * 
         * @param o
         *                the object to compare to.
         * 
         * @return true if the objects are identical.
         */

        public abstract override bool Equals(object o);

        public override int GetHashCode()
        {
            if (hashCodeValue == 0)
                hashCodeValue = HashCode();
            return hashCodeValue;
        }

        protected virtual int HashCode()
        {
            return base.GetHashCode();
        }

        public virtual object Clone()
        {
            return MemberwiseClone();
        }
    }
}
