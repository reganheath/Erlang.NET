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
using System.Collections.Generic;
using System.Reflection;

namespace Erlang.NET.Test
{
    public class Test
    {
        private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        static Test()
        {
            XmlConfigurator.Configure();
        }

        public static void Main(string[] args)
        {
            OtpNode self = new OtpNode("vaplug", "edsrv_cookie")
            {
                Flags = OtpInputStream.StreamFlags.DecodeIntListsAsStrings
            };
            OtpMbox mbox = self.CreateMbox("test", true);
            OtpErlangTuple tuple = new OtpErlangTuple(new IOtpErlangObject[2]
            {
                mbox.Self,
                new OtpErlangTuple(new IOtpErlangObject[] {
                    new OtpErlangAtom("echo"),
                    new OtpErlangString(OtpErlangString.FromCodePoints(new int[] { 127744,32,69,108,32,78,105,241,111 })) // 🌀 El Niño
                })
            });

            //send message to registered process hello_server on node one @grannysmith
            //> { hello_server, 'one@grannysmith'} ! test.

            mbox.Send("player_srv", "edsrv@GAMING", tuple);
            log.Debug("<- " + mbox.Receive());
        }
    }
}
