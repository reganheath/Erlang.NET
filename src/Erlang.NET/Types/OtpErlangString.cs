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
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Erlang.NET
{
    /**
     * Provides a Java representation of Erlang strings.
     */
    [Serializable]
    public class OtpErlangString : OtpErlangObject, IEquatable<OtpErlangString>
    {
        /**
         * Create an Erlang string from the given string.
         */
        public OtpErlangString(string str)
        {
            Value = str;
        }

        /**
         * Create an Erlang string from a list of integers.
         * 
         * @return an Erlang string with Unicode code units.
         *
         * @throws OtpErlangException
         *                for non-proper and non-integer lists.
         * @throws OtpErlangRangeException
         *                if an integer in the list is not
         *                a valid Unicode code point according to Erlang.
         */
        public OtpErlangString(OtpErlangList list)
        {
            Value = list.StringValue();
        }

        /**
         * Create an Erlang string from a stream containing a string encoded in
         * Erlang external format.
         * 
         * @param buf
         *            the stream containing the encoded string.
         * 
         * @exception OtpErlangDecodeException
         *                if the buffer does not contain a valid external
         *                representation of an Erlang string.
         */
        public OtpErlangString(OtpInputStream buf)
        {
            Value = buf.ReadString();
        }

        /**
         * Get the actual string contained in this object.
         * 
         * @return the raw string contained in this object, without regard to Erlang
         *         quoting rules.
         * 
         * @see #toString
         */
        public string Value { get; private set; }

        /**
         * Get the printable version of the string contained in this object.
         * 
         * @return the string contained in this object, quoted.
         * 
         * @see #stringValue
         */
        public override string ToString() => "\"" + Value + "\"";

        /**
         * Convert this string to the equivalent Erlang external representation.
         * 
         * @param buf
         *            an output stream to which the encoded string should be
         *            written.
         */
        public override void Encode(OtpOutputStream buf) => buf.WriteString(Value);

        /**
         * Determine if two strings are equal. They are equal if they represent the
         * same sequence of characters. This method can be used to compare
         * OtpErlangStrings with each other and with Strings.
         * 
         * @param o
         *            the OtpErlangString or string to compare to.
         * 
         * @return true if the strings consist of the same sequence of characters,
         *         false otherwise.
         */
        public override bool Equals(object o)
        {
            if (o is string s)
                return Value.Equals(s);
            return Equals(o as OtpErlangString);
        }

        public bool Equals(OtpErlangString o) => Value.Equals(o.Value);

        public override int GetHashCode() => base.GetHashCode();

        protected override int HashCode() => Value.GetHashCode();

        /**
         * Create Unicode code points from a string.
         * 
         * @param  s
         *             a string to convert to an Unicode code point array
         *
         * @return the corresponding array of integers representing
         *         Unicode code points
         */
        public static int[] ToCodePoints(string s)
        {
            if (string.IsNullOrEmpty(s))
                return null;

            if (!s.IsNormalized())
                s = s.Normalize();

            List<int> cps = new List<int>((s.Length * 3) / 2);

            TextElementEnumerator ee = StringInfo.GetTextElementEnumerator(s);
            while (ee.MoveNext())
            {
                string e = ee.GetTextElement();
                cps.Add(char.ConvertToUtf32(e, 0));
            }

            return cps.ToArray();
        }

        /**
         * Create Unicode code points into a string.
         * 
         * @param  cps
         *             a Unicode code point array
         *
         * @return the corresponding string equivalent to the
         *         Unicode code points
         */
        public static string FromCodePoints(IEnumerable<int> cps)
        {
            StringBuilder sb = new StringBuilder();

            foreach (int cp in cps)
            {
                if (!IsValidCodePoint(cp))
                    throw new OtpRangeException("Invalid CodePoint: " + cp);
                sb.Append(char.ConvertFromUtf32(cp));
            }

            return sb.ToString();
        }

        /**
         * Validate a code point according to Erlang definition; Unicode 3.0.
         * That is; valid in the range U+0..U+10FFFF, but not in the range
         * U+D800..U+DFFF (surrogat pairs)
         *
         * @param  cp
         *             the code point value to validate
         *
         * @return true if the code point is valid,
         *         false otherwise.
         */
        private static bool IsValidCodePoint(int cp)
        {
            // Erlang definition of valid Unicode code points; 
            // Unicode 3.0, XML, et.al.
            return (((uint)cp) >> 16) <= 0x10 // in 0..10FFFF; Unicode range
                && (cp & ~0x7FF) != 0xD800;   // not in D800..DFFF; surrogate range
        }

        /**
         * Construct a string from encoded byte array
         *
         */
        public static string FromEncoding(byte[] bytes, string encoding = "ISO-8859-1")
        {
            try
            {
                return Encoding.GetEncoding(encoding).GetString(bytes);
            }
            catch (ArgumentException)
            {
            }
            return Encoding.ASCII.GetString(bytes);
        }
    }
}
