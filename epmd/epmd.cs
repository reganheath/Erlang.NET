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
using log4net.Config;

namespace Erlang.NET
{
#if WIN32
    public class Epmd : ServiceBase
    {
        static Epmd()
        {
            XmlConfigurator.Configure();
        }

        private OtpEpmd m_epmd;
        private bool m_started = false;

        public Epmd()
        {
            base.AutoLog = false;
            base.CanPauseAndContinue = false;
            base.CanStop = true;
            base.ServiceName = "Erlang Port Mapper Daemon";
        }

        protected override void OnStart(string[] args)
        {
            if (!m_started)
            {
                m_epmd = new OtpEpmd();
                m_epmd.start();
                m_started = true;
            }
        }

        protected override void OnStop()
        {
            if (m_started)
            {
                m_epmd.quit();
                m_started = false;
            }
        }

        public static void Main(string[] args)
        {
            ServiceBase.Run(new Epmd());
        }
    }

    [RunInstaller(true)]
    public class LoginServiceInstaller : Installer
    {
        public LoginServiceInstaller()
        {
            throw new NotSupportedException();
        }
    }
#else
    public class Epmd
    {
        static Epmd()
        {
            XmlConfigurator.Configure();
        }

        public static void Main(string[] args)
        {
            OtpEpmd epmd = new OtpEpmd();
            epmd.Start();
            epmd.Join();
        }
    }
#endif
}
