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

namespace Erlang.NET.Test
{
    public class Ping
    {
        public static void Main(string[] args)
        {
            OtpNode pingNode = new OtpNode("ping");
            OtpNode pongNode = new OtpNode("pong");
            bool ok = pingNode.Ping("pong", 10000);
            pingNode.Close();
            pongNode.Close();
            Environment.Exit(ok ? 0 : 1);
        }
    }
}
