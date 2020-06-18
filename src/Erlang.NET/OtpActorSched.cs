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
using log4net;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;

namespace Erlang.NET
{
    public class OtpActorSched : ThreadBase
    {
        private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        public class OtpActorSchedTask
        {
            public bool Active { get; set; }

            public OtpActor Actor { get; private set; }

            public IEnumerator<OtpActor.Continuation> Enumerator { get; }

            public OtpActorSchedTask(OtpActor actor)
            {
                actor.Task = this;
                Actor = actor;
                Enumerator = actor.GetEnumerator();
            }
        }

        private readonly Queue<OtpActorSchedTask> runnable = new Queue<OtpActorSchedTask>();

        public OtpActorSched()
            : base("OtpActorSched", true)
        {
            base.Start();
        }

        public void React(OtpActor actor)
        {
            OtpActorSchedTask task = new OtpActorSchedTask(actor);
            IEnumerator<OtpActor.Continuation> enumerator = task.Enumerator;

            if (!enumerator.MoveNext())
            {
                task.Active = false;
                return;
            }

            lock (runnable)
            {
                task.Active = true;
                runnable.Enqueue(task);
                if (runnable.Count == 1)
                    Monitor.Pulse(runnable);
            }
        }

        public void Cancel(OtpActorMbox mbox)
        {
            OtpActorSchedTask task = mbox.Task;

            lock (runnable)
            {
                lock (task)
                    task.Active = false;
            }
        }

        public void Notify(OtpActorMbox mbox)
        {
            OtpActorSchedTask task = mbox.Task;

            lock (runnable)
            {
                lock (task)
                {
                    if (mbox.Task.Active)
                    {
                        runnable.Enqueue(mbox.Task);
                        if (runnable.Count == 1)
                            Monitor.Pulse(runnable);
                    }
                }
            }
        }

        public override void Run()
        {
            while (true)
                Schedule();
        }

        private void Schedule()
        {
            lock (runnable)
            {
                while (runnable.Count == 0)
                    Monitor.Wait(runnable);

                OtpActorSchedTask task = runnable.Dequeue();
                OtpActor actor = task.Actor;
                IEnumerator<OtpActor.Continuation> enumerator = task.Enumerator;

                lock (task)
                {
                    if (task.Active)
                    {
                        OtpMsg msg = actor.Mbox.ReceiveMsg();
                        if (msg == null)
                            return;

                        ThreadPool.QueueUserWorkItem((state) =>
                        {
                            lock (task)
                            {
                                try
                                {
                                    OtpActor.Continuation cont = enumerator.Current;

                                    cont(msg);

                                    if (!enumerator.MoveNext())
                                        task.Active = false;
                                }
                                catch (Exception e)
                                {
                                    log.Info("Exception was thrown from running actor: " + e.Message);
                                }
                            }
                        });
                    }
                }
            }
        }
    }
}
