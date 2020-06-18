/*
 * %CopyrightBegin%
 * 
 * Copyright Takayuki Usui 2009. All Rights Reserved.
 * Copyright Ericsson AB 2009. All Rights Reserved.
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

namespace Erlang.NET
{
    [Serializable]
    public class OtpErlangExternalFun : OtpErlangObject, IEquatable<OtpErlangExternalFun>
    {
        private string module;
        private string function;
        private int arity;

        public OtpErlangExternalFun(string module, string function, int arity)
            : base()
        {
            this.module = module;
            this.function = function;
            this.arity = arity;
        }

        public OtpErlangExternalFun(OtpInputStream buf)
        {
            OtpErlangExternalFun f = buf.ReadExternalFun();
            module = f.module;
            function = f.function;
            arity = f.arity;
        }

        public override void Encode(OtpOutputStream buf)
        {
            buf.WriteExternalFun(module, function, arity);
        }

        public override bool Equals(object o) => Equals(o as OtpErlangExternalFun);

        public bool Equals(OtpErlangExternalFun o)
        {
            if (o == null)
                return false;
            if (ReferenceEquals(this, o))
                return true;
            return module.Equals(o.module)
                && function.Equals(o.function)
                && arity == o.arity;
        }

        public override int GetHashCode() => base.GetHashCode();

        protected override int DoHashCode()
        {
            OtpErlangObject.Hash hash = new OtpErlangObject.Hash(14);
            hash.Combine(module.GetHashCode(), function.GetHashCode());
            hash.Combine(arity);
            return hash.ValueOf();
        }

        public override string ToString()
        {
            return "#Fun<" + module + "." + function + "." + arity + ">";
        }
    }
}