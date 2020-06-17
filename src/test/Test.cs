using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using Erlang.NET;
using log4net;
using log4net.Config;

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
            OtpNode self = new OtpNode("vaplug", "edsrv_cookie");
            OtpMbox mbox = self.createMbox("test", true);
            OtpErlangTuple tuple = new OtpErlangTuple(new OtpErlangObject[2]
            {
                mbox.Self,
                new OtpErlangAtom("hello, world")
            });
            mbox.send("player_srv", "edsrv@GAMING", tuple);
        }
    }
}
