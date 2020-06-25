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

namespace Erlang.NET
{
    [Serializable]
    public class OtpErlangExternalFun : IOtpErlangObject, IEquatable<OtpErlangExternalFun>, IComparable<OtpErlangExternalFun>
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

        public void Encode(OtpOutputStream buf) => buf.WriteExternalFun(Module, Function, Arity);

        public override string ToString() => $"#Fun<{Module}.{Function}.{Arity}>";

        public int CompareTo(object obj) => CompareTo(obj as OtpErlangExternalFun);

        public int CompareTo(OtpErlangExternalFun other)
        {
            if (other is null)
                return 1;
            int res = Module.CompareTo(other.Module);
            if (res == 0)
                res = Function.CompareTo(other.Function);
            if (res == 0)
                res = Arity.CompareTo(other.Arity);
            return res;
        }

        public override bool Equals(object obj) => Equals(obj as OtpErlangExternalFun);

        public bool Equals(OtpErlangExternalFun o)
        {
            if (o is null)
                return false;
            if (ReferenceEquals(this, o))
                return true;
            return Module == o.Module &&
                   Function == o.Function &&
                   Arity == o.Arity;
        }

        public override int GetHashCode()
        {
            int hashCode = -479099400;
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(Module);
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(Function);
            hashCode = hashCode * -1521134295 + Arity.GetHashCode();
            return hashCode;
        }

        public object Clone() => new OtpErlangExternalFun(Module, Function, Arity);
    }
}