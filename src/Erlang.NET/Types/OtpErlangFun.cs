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
using System.Linq;

namespace Erlang.NET
{
    [Serializable]
    public class OtpErlangFun : OtpErlangObject, IEquatable<OtpErlangFun>
    {
        private readonly OtpErlangPid pid;
        private readonly string module;
        private readonly long index;
        private readonly long old_index;
        private readonly long uniq;
        private readonly OtpErlangObject[] freeVars;
        private readonly int arity;
        private readonly byte[] md5;

        public OtpErlangFun(OtpInputStream buf)
        {
            OtpErlangFun f = buf.ReadFun();
            pid = f.pid;
            module = f.module;
            arity = f.arity;
            md5 = f.md5;
            index = f.index;
            old_index = f.old_index;
            uniq = f.uniq;
            freeVars = f.freeVars;
        }

        public OtpErlangFun(OtpErlangPid pid, string module,
                    long index, long uniq, OtpErlangObject[] freeVars)
        {
            this.pid = pid;
            this.module = module;
            arity = -1;
            md5 = null;
            this.index = index;
            old_index = 0;
            this.uniq = uniq;
            this.freeVars = freeVars;
        }

        public OtpErlangFun(OtpErlangPid pid, string module,
                    int arity, byte[] md5, int index,
                    long old_index, long uniq,
                    OtpErlangObject[] freeVars)
        {
            this.pid = pid;
            this.module = module;
            this.arity = arity;
            this.md5 = md5;
            this.index = index;
            this.old_index = old_index;
            this.uniq = uniq;
            this.freeVars = freeVars;
        }

        public override void Encode(OtpOutputStream buf)
        {
            buf.WriteFun(pid, module, old_index, arity, md5, index, uniq, freeVars);
        }

        public override bool Equals(object o) => Equals(o as OtpErlangFun);

        public bool Equals(OtpErlangFun o)
        {
            if (o == null)
                return false;
            if (ReferenceEquals(this, o))
                return true;
            if (!pid.Equals(o.pid) || !module.Equals(o.module) || arity != o.arity)
                return false;
            if (md5 == null)
            {
                if (o.md5 != null)
                    return false;
            }
            else
            {
                if (!md5.SequenceEqual(o.md5))
                    return false;
            }
            if (index != o.index || uniq != o.uniq)
                return false;
            if (freeVars == null)
                return o.freeVars == null;
            return freeVars.SequenceEqual(o.freeVars);
        }

        public override int GetHashCode() => base.GetHashCode();

        protected override int HashCode()
        {
            Hash hash = new Hash(1);
            hash.Combine(pid.GetHashCode(), module.GetHashCode());
            hash.Combine(arity);
            if (md5 != null)
                hash.Combine(md5);
            hash.Combine(index);
            hash.Combine(uniq);
            if (freeVars != null)
            {
                foreach (OtpErlangObject o in freeVars)
                    hash.Combine(o.GetHashCode(), 1);
            }
            return hash.ValueOf();
        }

        public override string ToString() => "#Fun<" + module + "." + old_index + "." + uniq + ">";
    }
}
