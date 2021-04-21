using log4net;
using log4net.Config;
using System;
using System.Configuration;

namespace Erlang.NET
{
    public class LogEventArgs : EventArgs
    {
        public enum LogEventType { Debug, Info, Warn, Error, Fatal };

        public LogEventArgs(LogEventType type, string message, Exception exception = null)
        {
            Timestamp = DateTime.Now;
            Type = type;
            Message = message;
            Exception = exception;
        }

        public DateTime Timestamp { get; }
        public LogEventType Type { get; }
        public string Message { get; }
        public Exception Exception { get; }
    }

    public delegate void LogEvent(LogEventArgs e);

    public static class Logger
    {
        private static readonly ILog log = LogManager.GetLogger("Erlang.NET");

        static Logger()
        {
            XmlConfigurator.Configure();

            string trace = ConfigurationManager.AppSettings["OtpConnection.trace"];
            if (int.TryParse(trace, out int level))
                DefaultTraceLevel = level;
        }

        public static LogEvent LogLine;

        public static int DefaultTraceLevel { get; set; }

        public static void Debug(string message)
        {
            if (LogLine != null)
                LogLine(new LogEventArgs(LogEventArgs.LogEventType.Debug, message));
            else
                log.Debug(message);
        }
        public static void Debug(string message, Exception exception)
        {
            if (LogLine != null)
                LogLine(new LogEventArgs(LogEventArgs.LogEventType.Debug, message, exception));
            else
                log.Debug(message, exception);
        }

        public static void Info(string message)
        {
            if (LogLine != null)
                LogLine(new LogEventArgs(LogEventArgs.LogEventType.Info, message));
            else
                log.Info(message);
        }
        public static void Info(string message, Exception exception)
        {
            if (LogLine != null)
                LogLine(new LogEventArgs(LogEventArgs.LogEventType.Info, message, exception));
            else
                log.Info(message, exception);
        }

        public static void Warn(string message)
        {
            if (LogLine != null)
                LogLine(new LogEventArgs(LogEventArgs.LogEventType.Warn, message));
            else
                log.Warn(message);
        }
        public static void Warn(string message, Exception exception)
        {
            if (LogLine != null)
                LogLine(new LogEventArgs(LogEventArgs.LogEventType.Warn, message, exception));
            else
                log.Warn(message, exception);
        }

        public static void Error(string message)
        {
            if (LogLine != null)
                LogLine(new LogEventArgs(LogEventArgs.LogEventType.Error, message));
            else
                log.Error(message);
        }
        public static void Error(string message, Exception exception)
        {
            if (LogLine != null)
                LogLine(new LogEventArgs(LogEventArgs.LogEventType.Error, message, exception));
            else
                log.Error(message, exception);
        }

        public static void Fatal(string message)
        {
            if (LogLine != null)
                LogLine(new LogEventArgs(LogEventArgs.LogEventType.Fatal, message));
            else
                log.Fatal(message);
        }
        public static void Fatal(string message, Exception exception)
        {
            if (LogLine != null)
                LogLine(new LogEventArgs(LogEventArgs.LogEventType.Fatal, message, exception));
            else
                log.Fatal(message, exception);
        }
    }
}
