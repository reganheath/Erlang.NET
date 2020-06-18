/*
 * %CopyrightBegin%
 * 
 * Copyright Takayuki Usui 2009. All Rights Reserved.
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
using System.Threading;

namespace Erlang.NET
{
    public abstract class ThreadBase
    {
        protected readonly Thread thread;
        protected volatile bool done = false;

        public ThreadBase(string name, bool isBackground)
        {
            thread = new Thread(new ThreadStart(Run))
            {
                IsBackground = isBackground,
                Name = name
            };
        }

        public bool Stopping => done;

        public virtual void Start()
        {
            thread.Start();
        }

        public virtual void Stop()
        {
            done = true;
        }

        public virtual void Join()
        {
            thread.Join();
        }

        public virtual void Join(TimeSpan timeout)
        {
            thread.Join(timeout);
        }

        public abstract void Run();
    }
}
