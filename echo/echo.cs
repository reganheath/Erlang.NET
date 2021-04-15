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
using System;
using System.Reflection;
using System.Threading;

namespace Erlang.NET.Test
{
    public class Echo
    {
        private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        static Echo()
        {
            XmlConfigurator.Configure();
        }

        public static void Main(string[] args)
        {
            OtpNode b = new OtpNode("b");
            var echo = b.CreateMbox("echo");
            echo.Received += (e) =>
            {
                OtpErlangTuple t = (OtpErlangTuple)e.Msg.Payload;
                OtpErlangPid sender = (OtpErlangPid)t.ElementAt(0);
                log.Debug($"-> ECHO {t.ElementAt(1)} from {sender}");
                t[0] = e.Mbox.Self;
                e.Mbox.Send(sender, t);
            };

            OtpNode a = new OtpNode("a");
            OtpMbox echoback = a.CreateMbox("echoback");
            
            echoback.Send(echo.Self, new OtpErlangTuple(echoback.Self, new OtpErlangString("Hello, World!")));
            log.Debug($"<- ECHO (back) {echoback.ReceiveMsg()}");

            a.Close();
            b.Close();
        }
    }
}
