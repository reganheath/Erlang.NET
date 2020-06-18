using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using Erlang.NET;
using log4net;
using log4net.Config;

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
            OtpErlangTuple tuple = new OtpErlangTuple(new OtpErlangObject[2]
            {
                mbox.Self,
                new OtpErlangTuple(new OtpErlangObject[] { 
                    new OtpErlangAtom("echo"),
                    new OtpErlangString(OtpErlangString.FromCodePoints(new int[] { 127744,32,69,108,32,78,105,241,111 })) // 🌀 El Niño
                })
            });
            mbox.Send("player_srv", "edsrv@GAMING", tuple);
            log.Debug("<- " + mbox.Receive());
        }
    }
}
