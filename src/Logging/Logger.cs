using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace NetworkSharing.Logging
{
    public enum LogLevel
    {
        Info,
        Warning,
        Error,
        Security
    }

    public class LogEntry
    {
        public DateTime Timestamp { get; set; }
        public LogLevel Level { get; set; }
        public string Message { get; set; }

        public override string ToString()
        {
            return string.Format("[{0:HH:mm:ss}] [{1}] {2}", Timestamp, Level.ToString().ToUpper(), Message);
        }
    }

    public static class Logger
    {
        private static readonly object _syncRoot = new object();
        private static readonly List<LogEntry> _entries = new List<LogEntry>();
        private const int MaxEntries = 1000;
        public static event Action<LogEntry> LogAppended;

        private static readonly string LogFilePath;

        static Logger()
        {
            try
            {
                string appData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NetworkSharing");
                if (!Directory.Exists(appData))
                {
                    Directory.CreateDirectory(appData);
                }
                LogFilePath = Path.Combine(appData, "network_sharing.log");
            }
            catch
            {
                LogFilePath = null;
            }
        }

        public static void Info(string message)
        {
            Log(LogLevel.Info, message);
        }

        public static void Warning(string message)
        {
            Log(LogLevel.Warning, message);
        }

        public static void Error(string message, Exception ex = null)
        {
            string full = ex != null ? string.Format("{0} -> Exception: {1}", message, ex.Message) : message;
            Log(LogLevel.Error, full);
        }

        public static void Security(string message)
        {
            Log(LogLevel.Security, message);
        }

        public static void Log(LogLevel level, string message)
        {
            string sanitized = Sanitize(message);
            LogEntry entry = new LogEntry
            {
                Timestamp = DateTime.Now,
                Level = level,
                Message = sanitized
            };

            lock (_syncRoot)
            {
                _entries.Add(entry);
                if (_entries.Count > MaxEntries)
                {
                    _entries.RemoveAt(0);
                }

                WriteToFile(entry);
            }

            try
            {
                if (LogAppended != null)
                {
                    LogAppended(entry);
                }
            }
            catch { }
        }

        public static List<LogEntry> GetRecentEntries()
        {
            lock (_syncRoot)
            {
                return new List<LogEntry>(_entries);
            }
        }

        public static void Clear()
        {
            lock (_syncRoot)
            {
                _entries.Clear();
            }
        }

        private static string Sanitize(string input)
        {
            if (string.IsNullOrEmpty(input)) return input;
            return SecretRedactor.Redact(input);
        }

        private static void WriteToFile(LogEntry entry)
        {
            if (string.IsNullOrEmpty(LogFilePath)) return;
            try
            {
                File.AppendAllText(LogFilePath, entry.ToString() + Environment.NewLine);
            }
            catch { }
        }
    }
}
