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
    public class OtpErlangFun : IOtpErlangObject, IEquatable<OtpErlangFun>, IComparable<OtpErlangFun>
    {
        public OtpErlangPid Pid { get; private set; }
        public string Module { get; private set; }
        public long Index { get; private set; }
        public long OldIndex { get; private set; }
        public long Uniq { get; private set; }
        public IOtpErlangObject[] FreeVars { get; private set; }
        public int Arity { get; private set; }
        public byte[] Md5 { get; private set; }

        public OtpErlangFun(OtpInputStream buf)
        {
            OtpErlangFun f = buf.ReadFun();
            Pid = f.Pid;
            Module = f.Module;
            Arity = f.Arity;
            Md5 = f.Md5;
            Index = f.Index;
            OldIndex = f.OldIndex;
            Uniq = f.Uniq;
            FreeVars = f.FreeVars;
        }

        public OtpErlangFun(OtpErlangPid pid, string module, long index, long uniq, IOtpErlangObject[] freeVars)
        {
            Pid = pid;
            Module = module;
            Arity = -1;
            Md5 = null;
            Index = index;
            OldIndex = 0;
            Uniq = uniq;
            FreeVars = freeVars;
        }

        public OtpErlangFun(OtpErlangPid pid, string module, int arity, byte[] md5, long index, long old_index, long uniq, IOtpErlangObject[] freeVars)
        {
            Pid = pid;
            Module = module;
            Arity = arity;
            Md5 = md5;
            Index = index;
            OldIndex = old_index;
            Uniq = uniq;
            FreeVars = freeVars;
        }

        public void Encode(OtpOutputStream buf) => buf.WriteFun(Pid, Module, OldIndex, Arity, Md5, Index, Uniq, FreeVars);

        public override string ToString() => $"#Fun<{Module}.{OldIndex}.{Uniq}>";

        public int CompareTo(object obj) => CompareTo(obj as OtpErlangFun);

        public int CompareTo(OtpErlangFun other)
        {
            if (other is null)
                return 1;
            int res = Pid.CompareTo(other.Pid);
            if (res == 0)
                res = Module.CompareTo(other.Module);
            if (res == 0)
                res = Arity.CompareTo(other.Arity);
            if (res == 0)
                res = Md5.CompareTo(other.Md5);
            if (res == 0)
                res = Index.CompareTo(other.Index);
            if (res == 0)
                res = OldIndex.CompareTo(other.OldIndex);
            if (res == 0)
                res = Uniq.CompareTo(other.Uniq);
            if (res == 0)
                res = FreeVars.CompareTo(other.FreeVars);
            return res;
        }

        public override bool Equals(object obj) => Equals(obj as OtpErlangFun);

        public bool Equals(OtpErlangFun o)
        {
            if (o is null)
                return false;
            if (ReferenceEquals(this, o))
                return true;
            return Pid == o.Pid &&
                   Module == o.Module &&
                   Index == o.Index &&
                   OldIndex == o.OldIndex &&
                   Uniq == o.Uniq &&
                   Arity == o.Arity &&
                   OtpErlangObject.SequenceEqual(FreeVars, o.FreeVars) &&
                   OtpErlangObject.SequenceEqual(Md5, o.Md5);
        }
        public override int GetHashCode()
        {
            int hashCode = -1018114159;
            hashCode = hashCode * -1521134295 + EqualityComparer<OtpErlangPid>.Default.GetHashCode(Pid);
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(Module);
            hashCode = hashCode * -1521134295 + Index.GetHashCode();
            hashCode = hashCode * -1521134295 + OldIndex.GetHashCode();
            hashCode = hashCode * -1521134295 + Uniq.GetHashCode();
            hashCode = hashCode * -1521134295 + EqualityComparer<IOtpErlangObject[]>.Default.GetHashCode(FreeVars);
            hashCode = hashCode * -1521134295 + Arity.GetHashCode();
            hashCode = hashCode * -1521134295 + EqualityComparer<byte[]>.Default.GetHashCode(Md5);
            return hashCode;
        }

        public object Clone() => new OtpErlangFun(Pid, Module, Arity, Md5, Index, OldIndex, Uniq, FreeVars);
    }
}
