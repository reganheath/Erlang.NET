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

namespace Erlang.NET.Test
{
    public class Test
    {
        public static void Main(string[] args)
        {
            OtpNode self = new OtpNode(new NodeDetails()
            {
                Node = "vaplug",
                Cookie = "edsrv_cookie",
                Flags = OtpInputStream.StreamFlags.DecodeIntListsAsStrings
            });
            OtpMbox mbox = self.CreateMbox("test");
            OtpErlangTuple tuple = new OtpErlangTuple(
                mbox.Self,
                new OtpErlangTuple(
                    new OtpErlangAtom("echo"),
                    new OtpErlangString(OtpErlangString.FromCodePoints(new int[] { 127744, 32, 69, 108, 32, 78, 105, 241, 111 })) // 🌀 El Niño
                )
            );

            //send message to registered process hello_server on node one @grannysmith
            //> { hello_server, 'one@grannysmith'} ! test.

            mbox.Send("edsrv@GAMING", "player_srv", tuple);
            Logger.Debug("<- REPLY " + mbox.Receive());

            var reply = self.RPC("edsrv@GAMING", 1000, "edlib", "log_dir");
            Logger.Debug("<- LOG " + reply);

        }
    }
}
