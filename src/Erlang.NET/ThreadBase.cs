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
