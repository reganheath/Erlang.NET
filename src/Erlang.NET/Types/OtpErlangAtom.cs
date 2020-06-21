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
using System.Text;

namespace Erlang.NET
{
    /**
     * Provides a Java representation of Erlang atoms. Atoms can be created from
     * strings whose length is not more than {@link #maxAtomLength maxAtomLength}
     * characters.
     */
    [Serializable]
    public class OtpErlangAtom : OtpErlangObject, IEquatable<OtpErlangAtom>
    {
        public string Value { get; private set; }

        /**
         * Create an atom from the given string.
         * 
         * @param atom
         *                the string to create the atom from.
         * 
         * @exception java.lang.IllegalArgumentException
         *                    if the string is null or contains more than
         *                    {@link #maxAtomLength maxAtomLength} characters.
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
         * 
         * @param buf
         *                the stream containing the encoded atom.
         * 
         * @exception OtpErlangDecodeException
         *                    if the buffer does not contain a valid external
         *                    representation of an Erlang atom.
         */
        public OtpErlangAtom(OtpInputStream buf) => Value = buf.ReadAtom();

        /**
         * Create an atom whose value is "true" or "false".
         */
        public OtpErlangAtom(bool t) => Value = t.ToString();

        /**
         * The boolean value of this atom.
         * 
         * @return the value of this atom expressed as a boolean value. If the atom
         *         consists of the characters "true" (independent of case) the value
         *         will be true. For any other values, the value will be false.
         * 
         */
        public bool BoolValue() => bool.Parse(Value);

        /**
         * Get the printname of the atom represented by this object. The difference
         * between this method and {link #atomValue atomValue()} is that the
         * printname is quoted and escaped where necessary, according to the Erlang
         * rules for atom naming.
         * 
         * @return the printname representation of this atom object.
         * 
         * @see #atomValue
         */
        public override string ToString() => ShouldQuote(Value) ? "'" + Escape(Value) + "'" : Value;

        /**
         * Determine if two atoms are equal.
         * 
         * @param o
         *                the other object to compare to.
         * 
         * @return true if the atoms are equal, false otherwise.
         */
        public override bool Equals(object o) => Equals(o as OtpErlangAtom);

        public bool Equals(OtpErlangAtom o)
        {
            if (o == null)
                return false;
            if (ReferenceEquals(this, o))
                return true;
            return Value.Equals(o.Value);
        }

        public override int GetHashCode() => base.GetHashCode();

        protected override int HashCode() => Value.GetHashCode();

        /**
         * Convert this atom to the equivalent Erlang external representation.
         * 
         * @param buf
         *                an output stream to which the encoded atom should be
         *                written.
         */
        public override void Encode(OtpOutputStream buf) => buf.WriteAtom(Value);

        /* the following four predicates are helpers for the toString() method */
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

            foreach (char c in s)
            {
                if (!IsErlangLetter(c) && !char.IsDigit(c) && c != '@')
                    return true;
            }

            return false;
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
    }
}
