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
using System.Collections.Generic;
using System.Linq;

namespace Erlang.NET
{
    // package scope
    public class Links
    {
        private readonly object lockObj = new object();
        private readonly List<Link> links;

        public Links()
        {
            links = new List<Link>(10);
        }

        public Links(int initialSize)
        {
            links = new List<Link>(initialSize);
        }

        public int Count => links.Count;

        /* all local pids get notified about broken connection */
        public OtpErlangPid[] LocalPids
        {
            get
            {
                lock (lockObj)
                    return links.Select((l) => l.Local).ToArray();
            }
        }

        /* all remote pids get notified about failed pid */
        public OtpErlangPid[] RemotePids
        {
            get
            {
                lock (lockObj)
                    return links.Select((l) => l.Remote).ToArray();
            }
        }

        public void AddLink(OtpErlangPid local, OtpErlangPid remote)
        {
            lock (lockObj)
            {
                Link l = Find(local, remote);
                if (l == null)
                    links.Add(new Link(local, remote));
            }
        }

        public void RemoveLink(OtpErlangPid local, OtpErlangPid remote)
        {
            lock (lockObj)
            {
                Link l = Find(local, remote);
                if (l != null)
                    links.Remove(l);
            }
        }

        public bool Exists(OtpErlangPid local, OtpErlangPid remote)
        {
            lock (lockObj)
                return Find(local, remote) != null;
        }

        public Link Find(OtpErlangPid local, OtpErlangPid remote)
        {
            lock (lockObj)
                return links.FirstOrDefault((l) => l.Equals(local, remote));
        }

        /* clears the link table, returns a copy */
        public Link[] ClearLinks()
        {
            lock (lockObj)
            {
                Link[] ret = links.ToArray();
                links.Clear();
                return ret;
            }
        }
    }
}
