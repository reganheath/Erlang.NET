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

namespace Erlang.NET
{
    // package scope
    public class Link : IEquatable<Link>, IComparable<Link>, IComparable
    {
        public OtpErlangPid Local { get; private set; }
        public OtpErlangPid Remote { get; private set; }

        private int hashCodeValue = 0;

        public Link(OtpErlangPid local, OtpErlangPid remote)
        {
            Local = local;
            Remote = remote;
        }

        public bool Contains(OtpErlangPid pid)
        {
            return Local.Equals(pid) || Remote.Equals(pid);
        }

        public bool Equals(OtpErlangPid local, OtpErlangPid remote)
        {
            return Local.Equals(local) && Remote.Equals(remote)
                || Local.Equals(remote) && Remote.Equals(local);
        }

        public override int GetHashCode()
        {
            if (hashCodeValue == 0)
            {
                OtpErlangObject.Hash hash = new OtpErlangObject.Hash(5);
                hash.Combine(Local.GetHashCode() + Remote.GetHashCode());
                hashCodeValue = hash.ValueOf();
            }
            return hashCodeValue;
        }

        public override bool Equals(object o) => Equals(o as Link);

        public bool Equals(Link o)
        {
            if (o == null)
                return false;
            if (ReferenceEquals(this, o))
                return true;
            return Local.Equals(o.Local)
                && Remote.Equals(o.Remote);
        }

        public int CompareTo(object other)
        {
            if (other == null)
                return -1;
            if (!(other is Link))
                return -1;
            return CompareTo(other as Link);
        }

        public int CompareTo(Link other)
        {
            int res = Local.CompareTo(other.Local);
            if (res == 0)
                res = Remote.CompareTo(other.Remote);
            return res;
        }
    }
}
