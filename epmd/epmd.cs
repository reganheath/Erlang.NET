using log4net.Config;
using System;
using System.ServiceProcess;
using System.Threading.Tasks;

namespace Erlang.NET
{
    [System.ComponentModel.DesignerCategory("")]
    public partial class Epmd : ServiceBase
    {
        static Epmd()
        {
            XmlConfigurator.Configure();
        }

        private OtpEpmd epmd;

        public Epmd()
        {
            InitializeComponent();
        }

#if DEBUG
        public void OnDebug(string[] args) => OnStart(args);
        public void StopDebug() => OnStop();
#endif

        protected override void OnStart(string[] args)
        {
            epmd = new OtpEpmd();
            epmd.ServeAsync();
        }

        protected override void OnStop() => epmd.Stop();
    }
}
