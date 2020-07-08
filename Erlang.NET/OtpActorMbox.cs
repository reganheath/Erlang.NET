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
namespace Erlang.NET
{
    public class OtpActorMbox : OtpMbox
    {
        protected readonly OtpActorSched sched;

        public OtpActorSched.OtpActorSchedTask Task { get; set; }

        internal OtpActorMbox(OtpActorSched sched, OtpNode home, OtpErlangPid self, string name)
            : base(home, self, name)
        {
            this.sched = sched;
        }

        internal OtpActorMbox(OtpActorSched sched, OtpNode home, OtpErlangPid self)
            : base(home, self, null)
        {
            this.sched = sched;
        }

        public override void Close()
        {
            base.Close();
            sched.Cancel(this);
        }

        public override OtpMsg ReceiveMsg() => Queue.TryDequeue(out OtpMsg m) ? m : null;

        public override OtpMsg ReceiveMsg(long timeout) => Queue.Dequeue(timeout, out OtpMsg m) ? m : null;

        public override void Deliver(OtpMsg m)
        {
            base.Deliver(m);
            sched.Notify(this);
        }
    }
}
