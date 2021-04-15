using System;
using System.ServiceProcess;

namespace Erlang.NET
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        static void Main(string[] args)
        {
#if DEBUG
            var service = new Epmd();
            service.OnDebug(args);
            Console.WriteLine("Press escape to exit..");
            while (Console.ReadKey(true).Key != ConsoleKey.Escape) { }
            service.StopDebug();
#else
            ServiceBase[] ServicesToRun;
            ServicesToRun = new ServiceBase[]
            {
                new Epmd()
            };
            ServiceBase.Run(ServicesToRun);
#endif
        }
    }
}
