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
        public string Module { get; private set; }
        public string Function { get; private set; }
        public int Arity { get; private set; }

        public OtpErlangExternalFun(string module, string function, int arity)
            : base()
        {
            Module = module;
            Function = function;
            Arity = arity;
        }

        public OtpErlangExternalFun(OtpInputStream buf)
        {
            OtpErlangExternalFun f = buf.ReadExternalFun();
            Module = f.Module;
            Function = f.Function;
            Arity = f.Arity;
        }

        public override void Encode(OtpOutputStream buf)
        {
            buf.WriteExternalFun(Module, Function, Arity);
        }

        public override bool Equals(object o) => Equals(o as OtpErlangExternalFun);

        public bool Equals(OtpErlangExternalFun o)
        {
            if (o == null)
                return false;
            if (ReferenceEquals(this, o))
                return true;
            return Module.Equals(o.Module)
                && Function.Equals(o.Function)
                && Arity == o.Arity;
        }

        public override int GetHashCode() => base.GetHashCode();

        protected override int HashCode()
        {
            Hash hash = new Hash(14);
            hash.Combine(Module.GetHashCode(), Function.GetHashCode());
            hash.Combine(Arity);
            return hash.ValueOf();
        }

        public override string ToString() => "#Fun<" + Module + "." + Function + "." + Arity + ">";
    }
}