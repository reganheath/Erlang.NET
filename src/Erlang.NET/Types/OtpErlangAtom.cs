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
using System.Linq;
using System.Text;

namespace Erlang.NET
{
    /**
     * Provides a representation of Erlang atoms. Atoms can be created from
     * strings whose length is not more than {@link #maxAtomLength maxAtomLength}
     * characters.
     */
    [Serializable]
    public class OtpErlangAtom : IOtpErlangObject, IComparable<OtpErlangAtom>, IEquatable<OtpErlangAtom>
    {
        public string Value { get; private set; }

        public bool BoolValue => bool.Parse(Value);

        /**
         * Create an atom from the given string.
         */
        public OtpErlangAtom(string atom)
        {
            if (atom == null)
                throw new ArgumentException("Atom cannot be null");
            if (atom.Length > OtpExternal.MAX_ATOM_LENGTH)
                throw new ArgumentException($"Atom may not exceed {OtpExternal.MAX_ATOM_LENGTH} characters: {atom}");
            Value = atom;
        }

        /**
         * Create an atom from a stream containing an atom encoded in Erlang
         * external format.
         */
        public OtpErlangAtom(OtpInputStream buf) => Value = buf.ReadAtom();

        /**
         * Create an atom whose value is "true" or "false".
         */
        public OtpErlangAtom(bool t) => Value = t.ToString();

        /**
         * Convert this atom to the equivalent Erlang external representation.
         * 
         * @param buf
         *                an output stream to which the encoded atom should be
         *                written.
         */
        public void Encode(OtpOutputStream buf) => buf.WriteAtom(Value);

        /**
         * Get the printname of the atom represented by this object. The difference
         * between this method and {link #atomValue atomValue()} is that the
         * printname is quoted and escaped where necessary, according to the Erlang
         * rules for atom naming.
         */
        public override string ToString() => ShouldQuote(Value) ? $"'{Escape(Value)}'" : Value;

        public int CompareTo(object obj) => CompareTo(obj as OtpErlangAtom);

        public int CompareTo(OtpErlangAtom other)
        {
            if (other is null)
                return 1;
            return Value.CompareTo(other.Value);
        }

        public override bool Equals(object obj) => Equals(obj as OtpErlangAtom);

        public bool Equals(OtpErlangAtom o)
        {
            if (o is null)
                return false;
            if (ReferenceEquals(this, o))
                return true;
            return Value == o.Value;
        }

        public override int GetHashCode()
        {
            int hashCode = -1406514820;
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(Value);
            return hashCode;
        }

        public object Clone() => new OtpErlangAtom(Value);

        #region ToString Helper Functions
        /* the following three predicates are helpers for the toString() method */
        private bool IsErlangUpper(char c) => (c >= 'A' && c <= 'Z') || c == '_';

        private bool IsErlangLower(char c) => c >= 'a' && c <= 'z';

        private bool IsErlangLetter(char c) => IsErlangLower(c) || IsErlangUpper(c);

        // true if the atom should be displayed with quotation marks
        private bool ShouldQuote(string s)
        {
            if (s.Length == 0)
                return true;
            if (!IsErlangLower(s[0]))
                return true;
            return s.Any((c) => !IsErlangLetter(c) && !char.IsDigit(c) && c != '@');
        }

        /*
         * Get the atom string, with special characters escaped. Note that this
         * function currently does not consider any characters above 127 to be
         * printable.
         */
        private string Escape(string s)
        {
            StringBuilder so = new StringBuilder();
            foreach(var c in s)
            {
                /*
                 * note that some of these escape sequences are unique to Erlang,
                 * which is why the corresponding 'case' values use octal. The
                 * resulting string is, of course, in Erlang format.
                 */
                switch (c)
                {
                    // some special escape sequences
                    case '\b':      so.Append(@"\b"); break;
                    case (char)127: so.Append(@"\d"); break;
                    case (char)27:  so.Append(@"\e"); break;
                    case '\f':      so.Append(@"\f"); break;
                    case '\n':      so.Append(@"\n"); break;
                    case '\r':      so.Append(@"\r"); break;
                    case '\t':      so.Append(@"\t"); break;
                    case (char)11:  so.Append(@"\v"); break;
                    case '\\':      so.Append(@"\\"); break;
                    case '\'':      so.Append(@"\'"); break;
                    case '\"':      so.Append(@"\"""); break;
                    case char n when n < 027:
                        // control chars show as "\^@", "\^A" etc
                        so.Append("\\^" + (char)('A' - 1 + n));
                        break;
                    case char m when m > 126:
                        // 8-bit chars show as \345 \344 \366 etc
                        so.Append("\\" + Convert.ToString(c, 8));
                        break;
                    default:
                        // character is printable without modification!
                        so.Append(c);
                        break;
                }
            }
            return so.ToString();
        }
        #endregion
    }
}
