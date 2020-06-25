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
    public class OtpErlangString : IOtpErlangObject, IComparable<OtpErlangString>, IEquatable<OtpErlangString>
    {
        public string Value { get; private set; }

        public static implicit operator string(OtpErlangString s) => s.Value;

        /**
         * Create an Erlang string from the given string.
         */
        public OtpErlangString(string str) => Value = str;

        /**
         * Create an Erlang string from a list of integers.
         */
        public OtpErlangString(OtpErlangList list) => Value = list.StringValue();

        /**
         * Create an Erlang string from a stream containing a string encoded in
         * Erlang external format.
         */
        public OtpErlangString(OtpInputStream buf) => Value = buf.ReadString();

        /**
         * Get the printable version of the string contained in this object.
         */
        public override string ToString() => $"\"{Value}\"";

        /**
         * Convert this string to the equivalent Erlang external representation.
         */
        public void Encode(OtpOutputStream buf) => buf.WriteString(Value);

        /**
         * Create Unicode code points from a string.
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
         */
        private static bool IsValidCodePoint(int cp)
        {
            return (((uint)cp) >> 16) <= 0x10 // in 0..10FFFF; Unicode range
                && (cp & ~0x7FF) != 0xD800;   // not in D800..DFFF; surrogate range
        }

        /**
         * Construct a string from encoded byte array
         */
        public static string FromEncoding(byte[] bytes, string encoding = "ISO-8859-1")
        {
            try { return Encoding.GetEncoding(encoding).GetString(bytes); }
            catch (ArgumentException) { }
            return Encoding.ASCII.GetString(bytes);
        }

        public int CompareTo(object obj) => CompareTo(obj as OtpErlangString);

        public int CompareTo(OtpErlangString other)
        {
            if (other is null)
                return 1;
            return Value.CompareTo(other.Value);
        }

        public override bool Equals(object obj) => Equals(obj as OtpErlangString);

        public bool Equals(OtpErlangString other) => string.Equals(Value, other?.Value);

        public static bool operator ==(OtpErlangString s1, OtpErlangString s2) => s1?.Value == s2?.Value;

        public static bool operator !=(OtpErlangString s1, OtpErlangString s2) => s1?.Value != s2?.Value;

        public override int GetHashCode() => Value.GetHashCode();

        public object Clone() => new OtpErlangString(Value);
    }
}
