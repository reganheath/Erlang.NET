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
    public class Link : IEquatable<Link>, IComparable, IComparable<Link>
    {
        public OtpErlangPid Local { get; private set; }
        public OtpErlangPid Remote { get; private set; }

        public Link(OtpErlangPid local, OtpErlangPid remote)
        {
            Local = local;
            Remote = remote;
        }

        public bool Contains(OtpErlangPid pid) => Local.Equals(pid) || Remote.Equals(pid);

        public bool Equals(OtpErlangPid local, OtpErlangPid remote)
        {
            return Local.Equals(local) && Remote.Equals(remote)
                || Local.Equals(remote) && Remote.Equals(local);
        }

        public override int GetHashCode()
        {
            int hashCode = -691572389;
            hashCode = hashCode * -1521134295 + EqualityComparer<OtpErlangPid>.Default.GetHashCode(Local);
            hashCode = hashCode * -1521134295 + EqualityComparer<OtpErlangPid>.Default.GetHashCode(Remote);
            return hashCode;
        }

        public override bool Equals(object obj) => Equals(obj as Link);

        public bool Equals(Link o)
        {
            if (o is null)
                return false;
            if (ReferenceEquals(this, o))
                return true;
            return Local.Equals(o.Local)
                && Remote.Equals(o.Remote);
        }

        public int CompareTo(object obj) => CompareTo(obj as Link);

        public int CompareTo(Link other)
        {
            if (other is null)
                return 1;
            int res = Local.CompareTo(other.Local);
            if (res == 0)
                res = Remote.CompareTo(other.Remote);
            return res;
        }
    }
}
