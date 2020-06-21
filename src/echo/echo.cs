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
using log4net;
using log4net.Config;
using System.Collections.Generic;
using System.Reflection;

namespace Erlang.NET.Test
{
    public class Echo
    {
        private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        static Echo()
        {
            XmlConfigurator.Configure();
        }

        public class OtpEchoActor : OtpActor
        {
            public OtpEchoActor(OtpActorMbox mbox)
                : base(mbox)
            {
            }

            public override IEnumerator<Continuation> GetEnumerator()
            {
                OtpMbox mbox = base.Mbox;
                OtpMsg msg = null;

                while (true)
                {
                    yield return (delegate (OtpMsg m) { msg = m; });
                    log.Debug("-> ECHO " + msg.GetMsg());
                    OtpErlangTuple t = (OtpErlangTuple)msg.GetMsg();
                    OtpErlangPid sender = (OtpErlangPid)t.ElementAt(0);
                    OtpErlangObject[] v = { mbox.Self, t.ElementAt(1) };
                    mbox.Send(sender, new OtpErlangTuple(v));
                }
            }
        }

        public static void Main(string[] args)
        {
            OtpNode b = new OtpNode("b");
            OtpActorMbox echo = (OtpActorMbox)b.CreateMbox("echo", false);
            b.React(new OtpEchoActor(echo));

            OtpNode a = new OtpNode("a");
            OtpMbox echoback = a.CreateMbox("echoback", true);
            OtpErlangObject[] v = { echoback.Self, new OtpErlangString("Hello, World!") };
            echoback.Send(echo.Self, new OtpErlangTuple(v));
            log.Debug("<- ECHO (back) " + echoback.Receive());

            a.Close();
            b.Close();
        }
    }
}
