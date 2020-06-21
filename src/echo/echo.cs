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
