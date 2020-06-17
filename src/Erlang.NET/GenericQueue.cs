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
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

namespace Erlang.NET
{
    /**
     * This class implements a generic FIFO queue. There is no upper bound on the
     * length of the queue, items are linked.
     */

    public class GenericQueue
    {
        private enum QueueState {  Open, Closing, Closed };

        private QueueState status;
        private readonly object lockObj = new object();
        private LinkedList<object> queue = new LinkedList<object>();

        /** Create an empty queue */
        public GenericQueue()
        {
            status = QueueState.Open;
        }

        /** Clear a queue */
        public void flush()
        {
            lock(lockObj)
                queue = new LinkedList<object>();
        }

        public void close()
        {
            status = QueueState.Closing;
        }

        /**
         * Add an object to the tail of the queue.
         * 
         * @param o
         *                Object to insert in the queue
         */
        public void put(object o)
        {
            lock (lockObj)
            {
                queue.AddLast(o);
                Monitor.Pulse(lockObj);
            }
        }

        /**
         * Retrieve an object from the head of the queue, or block until one
         * arrives.
         * 
         * @return The object at the head of the queue.
         */
        public object get()
        {
            lock (lockObj)
            {
                object o = null;

                while ((o = tryGet()) == null)
                {
                    try
                    {
                        Monitor.Wait(lockObj);
                    }
                    catch (ThreadInterruptedException)
                    {
                    }
                }

                return o;
            }
        }

        /**
         * Retrieve an object from the head of the queue, blocking until one arrives
         * or until timeout occurs.
         * 
         * @param timeout
         *                Maximum time to block on queue, in ms. Use 0 to poll the
         *                queue.
         * 
         * @exception InterruptedException
         *                    if the operation times out.
         * 
         * @return The object at the head of the queue, or null if none arrived in
         *         time.
         */
        public object get(long timeout)
        {
            if (status == QueueState.Closed)
                return null;

            var stopwatch = new Stopwatch();

            lock (lockObj)
            {
                object o = null;

                stopwatch.Start();
                while (true)
                {
                    if ((o = tryGet()) != null)
                        return o;

                    long elapsed = stopwatch.ElapsedMilliseconds;
                    if (elapsed > timeout)
                        throw new ThreadInterruptedException("Get operation timed out");

                    try
                    {
                        Monitor.Wait(lockObj, (int)(timeout - elapsed));
                    }
                    catch (ThreadInterruptedException)
                    {
                        // ignore, but really should retry operation instead
                    }
                }
            }
        }

        // attempt to retrieve message from queue head
        public object tryGet()
        {
            lock (lockObj)
            {
                var o = queue.First;
                if (o != null)
                    queue.RemoveFirst();
                return o?.Value ?? null;
            }
        }

        public int getCount() => queue.Count;
    }
}
