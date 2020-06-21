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
