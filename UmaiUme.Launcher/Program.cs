using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
using ReiPatcher.Patch;
using UmaiUme.Launcher.Logging;
using UmaiUme.Launcher.Utils;
using static UmaiUme.Launcher.Utils.Helpers;

namespace UmaiUme.Launcher
{
    public class Program
    {
        private const string LOGO =
        @"  _    _                 _ _    _                " + "\r\n"
        + @" | |  | |               (_) |  | |               " + "\r\n"
        + @" | |  | |_ __ ___   __ _ _| |  | |_ __ ___   ___ " + "\r\n"
        + @" | |  | | '_ ` _ \ / _` | | |  | | '_ ` _ \ / _ \" + "\r\n"
        + @" | |__| | | | | | | (_| | | |__| | | | | | |  __/" + "\r\n"
        + @"  \____/|_| |_| |_|\__,_|_|\____/|_| |_| |_|\___|";

        private static readonly string PROCESS_NAME = Process.GetCurrentProcess().ProcessName;
        private static string configFilePath;
        public static Process GameProcess;
        private static Version Version => Assembly.GetExecutingAssembly().GetName().Version;

        public static void Main(string[] args)
        {
            Logger.Init();
            Console.ForegroundColor = ConsoleColor.DarkGreen;
            Console.WriteLine(LOGO);
            Console.ForegroundColor = ConsoleColor.Gray;
            Console.WriteLine($"{new string(' ', 30)} Launcher v. {Version}");
            Console.WriteLine();
            Console.WriteLine($"Started on {DateTime.Now.ToString("F", CultureInfo.InvariantCulture)}");
            Logger.StartTime();
            configFilePath = Configuration.DEFAULT_CONFIG_NAME;
            if (args.Length > 0)
                ParseArguments(args);
            if (configFilePath == Configuration.DEFAULT_CONFIG_NAME && !File.Exists(Configuration.DEFAULT_CONFIG_NAME))
            {
                DialogResult result = MessageBox.Show(
                "No default configuration file found. Create one?",
                "Create configuration file?",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

                if (result == DialogResult.Yes)
                {
                    CreateDefaultIni();
                    ShowInfo(
                    "Configuration file created.",
                    $"Created configuration file {Configuration.DEFAULT_CONFIG_NAME}. Edit it before running {PROCESS_NAME}.exe again.");
                    Environment.Exit(0);
                }
                else
                {
                    Logger.Log(LogLevel.Error, $"No configuration file specified! Use {PROCESS_NAME}.exe -h for help.");
                    Environment.Exit(-1);
                }
            }
            Configuration.Load(configFilePath);
            Logger.Log(LogLevel.Info, $"Loaded configuration file from {configFilePath}");

            Assert(() => Patcher.LoadPatches(), "An error occurred while loading patches!");
            Assert(() => Patcher.PrePatch(), "An error occurred during pre-patching!");
            Assert(() => Patcher.Patch(), "An error occurred during patching!");
            Assert(() => Patcher.PostPatch(), "An error occurred during post-patching!");
            Assert(() => RunGame(), "An error occurred when running the game!");

            Logger.LogWriter.Close();
            Logger.LogWriter.Dispose();
        }

        private static void RunGame()
        {
            Logger.Log(LogLevel.Info, "Patching is complete. Launching the game...");


            if (!Configuration.GameExecutableName.IsNullOrWhiteSpace())
            {
                Logger.Log("Starting main process");

                Process.Start(Configuration.Executable, Configuration.ExecArgs);

                Logger.Log(
                $"Searching for process {Configuration.GameExecutableName}. Press CTRL+C to stop and close UULauncher...");

                Process[] processes;
                do
                {
                    processes = Process.GetProcessesByName(Configuration.GameExecutableName);
                } while (processes.Length == 0);

                Logger.Log(LogLevel.Info, "Found process! Binding...");
                GameProcess = Process.GetProcessById(processes[0].Id);
                Logger.Log(LogLevel.Info, "Done!");
            }
            else
            {
                GameProcess = Process.Start(Configuration.Executable, Configuration.ExecArgs);

                if (GameProcess == null)
                {
                    Logger.Log(LogLevel.Error, "The process could not be started. Exiting...");
                    Environment.Exit(-1);
                }
                Logger.Log(LogLevel.Info, "Game process launched!");
            }
            ConsoleUtils.SetConsoleCtrlHandler(HandleConsoleCtrl, true);
            Logger.StopTime();
            Logger.Log(
            LogLevel.Warning,
            "NOTE: DO NOT close this window while the game is running. UULauncher will perform clean-up after the game is closed.");
            IntPtr consoleHandle = ConsoleUtils.GetConsoleWindow();
            if (Configuration.HideWhileGameRuns)
                ConsoleUtils.ShowWindow(consoleHandle, ConsoleUtils.SW_HIDE);

            GameProcess.WaitForExit();
            if (Configuration.HideWhileGameRuns)
                ConsoleUtils.ShowWindow(consoleHandle, ConsoleUtils.SW_SHOW);
            Logger.Log(LogLevel.Info, "Game exited");
            Patcher.RestoreAssemblies();
        }

        private static bool HandleConsoleCtrl(int eventType)
        {
            switch (eventType)
            {
                case ConsoleUtils.CTRL_C_EVENT:
                case ConsoleUtils.CTRL_BREAK_EVENT:
                case ConsoleUtils.CTRL_CLOSE_EVENT:
                    Logger.Log(LogLevel.Warning, "UULauncher has been closed suddenly! Closing game...");
                    GameProcess.Kill();
                    Patcher.RestoreAssemblies();
                    break;
            }

            return true;
        }

        private static void ParseArguments(string[] args)
        {
            switch (args[0])
            {
                case "-h":
                    PrintHelp();
                    Environment.Exit(0);
                    break;
                case "-c":
                    if (args.Length != 2)
                    {
                        ShowInfo(
                        "UmaiUme Launcher: Usage of -h",
                        $"Usage:\n\n{PROCESS_NAME}.exe -c <PATH>\n\nwhere <PATH> is the path to the INI configuration file.");
                        Environment.Exit(0);
                    }

                    if (!File.Exists(args[1]))
                    {
                        ShowInfo(
                        "UmaiUme Launcher: No configuration found.",
                        $"Could not locate configuration file {args[1]}. Make sure the file exists and that it is a valid INI configuration file.");
                        Environment.Exit(-1);
                    }
                    configFilePath = args[1];
                    break;
            }
        }

        private static void PrintHelp()
        {
            ShowInfo(
            "UmaiUme Launcher Help Box",
            $"UmaiUme Launcher (UULauncher) v. {Version}\n© 2016 UmaiUme\nLicensed under the MIT licence\n\nA tool to patch and run Unity Games.\n\nUsage: {PROCESS_NAME}.exe [ARGUMENTS]\n\nARGUMENTS:\n(No arguments)\tRuns UULauncher with the default configuration file.\n-h\t\tDisplays this help box.\n-c <PATH>\tRuns UULauncher with custom configuration file specified by <PATH>.");
        }
    }
}