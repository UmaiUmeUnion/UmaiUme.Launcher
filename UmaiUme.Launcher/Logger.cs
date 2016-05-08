using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace UmaiUme.Launcher
{
    public enum LogLevel
    {
        Message,
        Info,
        Warning,
        Error
    }

    public static class Logger
    {
        private const string LOG_NAME = "UULauncher.log";
        private static readonly ConsoleColor[] levelColors =
        {
            ConsoleColor.Gray,
            ConsoleColor.Cyan,
            ConsoleColor.Yellow,
            ConsoleColor.Red
        };

        private static readonly string[] levelNames = {"MSG", "INF", "WRN", "ERR"};

        public static LogWriter LogWriter { get; }

        private static bool timerStarted = false;

        static Logger()
        {
            startTime = DateTime.Now;
            timerStarted = true;
            StreamWriter sw = new StreamWriter(Console.OpenStandardOutput()) {AutoFlush = true};
            LogWriter = new LogWriter(sw, LOG_NAME);
            Console.SetOut(LogWriter);
        }

        private static DateTime startTime;

        private static TimeSpan TimePassed => DateTime.Now - startTime;

        public static void Init()
        {
            // Dummy
        }

        public static void StartTime()
        {
            startTime = DateTime.Now;
            timerStarted = true;
        }

        public static void StopTime()
        {
            timerStarted = false;
        }

        public static void Log(string message)
        {
            if(timerStarted)
                Console.Write($"[{TimePassed.TotalSeconds.ToString("###0.0000", CultureInfo.InvariantCulture)}]");
            Console.WriteLine($"[{levelNames[(int)LogLevel.Message]}] {message}");
        }

        private static readonly Regex pattern = new Regex(@"\$\((?<color>\w+)\)(?<message>[^\$]+)\$");
        public static void LogColor(LogLevel logLevel, string message)
        {
            if(timerStarted)
                Console.Write($"[{TimePassed.TotalSeconds.ToString("###0.0000", CultureInfo.InvariantCulture)}]");
            ConsoleColor prev = Console.ForegroundColor;
            Console.ForegroundColor = levelColors[(int)logLevel];
            Console.Write($"[{levelNames[(int)logLevel]}] ");
            Console.ForegroundColor = prev;
            MatchCollection matches = pattern.Matches(message);

            if (matches.Count == 0)
            {
                Console.WriteLine(message);
                return;
            }

            foreach (Match match in matches)
            {
                int index = message.IndexOf(match.Value, StringComparison.Ordinal);
                string part = message.Substring(0, index);

                Console.Write(part);
                WriteColoredMessage(match);

                message = message.Remove(0, index + match.Length);
            }

            if(message != string.Empty)
                Console.Write(message);
            Console.WriteLine();
        }

        private static void WriteColoredMessage(Match m)
        {
            ConsoleColor color;
            try
            {
                color = (ConsoleColor)Enum.Parse(typeof(ConsoleColor), m.Groups["color"].Value, true);
            }
            catch (Exception)
            {
                color = ConsoleColor.Gray;
            }

            ConsoleColor old = Console.ForegroundColor;
            Console.ForegroundColor = color;
            Console.Write(m.Groups["message"].Value);
            Console.ForegroundColor = old;
        }

        public static void Log(LogLevel logLevel, string message)
        {
            if(timerStarted)
                Console.Write($"[{TimePassed.TotalSeconds.ToString("###0.0000", CultureInfo.InvariantCulture)}]");
            ConsoleColor prev = Console.ForegroundColor;
            Console.ForegroundColor = levelColors[(int)logLevel];
            Console.Write($"[{levelNames[(int)logLevel]}] {(logLevel > LogLevel.Info ? message : "")}");
            Console.ForegroundColor = prev;
            Console.WriteLine(logLevel <= LogLevel.Info ? message : "");
        }
    }
}
