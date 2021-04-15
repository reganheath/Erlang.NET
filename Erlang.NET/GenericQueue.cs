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
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;

namespace Erlang.NET
{
    /**
     * This class implements a generic FIFO queue. There is no upper bound on the
     * length of the queue, items are linked.
     */

    public class GenericQueue<T> : ConcurrentQueue<T>
    {
        private readonly AutoResetEvent wakeEvent = new AutoResetEvent(false);

        /**
         * Empty the queue
         */
        public void Clear()
        {
            while (TryDequeue(out _)) { }
        }

        /**
         * Enqueue and wake any waiting Dequeue
         */
        public new void Enqueue(T o)
        {
            base.Enqueue(o);
            wakeEvent.Set();
        }

        /**
         * Blocking dequeue
         */
        public bool Dequeue(out T o)
        {
            while (!TryDequeue(out o))
            {
                try { wakeEvent.WaitOne(); }
                catch (ThreadInterruptedException)
                {
                    return false;
                }
            }

            return true;
        }

        /**
         * Blocking dequeue with timeout
         */
        public bool Dequeue(long timeout, out T o)
        {
            Stopwatch stopwatch = new Stopwatch();

            stopwatch.Start();
            while (!TryDequeue(out o))
            {
                long elapsed = stopwatch.ElapsedMilliseconds;
                if (elapsed > timeout)
                    return false;

                try { wakeEvent.WaitOne((int)(timeout - elapsed)); }
                catch (ThreadInterruptedException)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
