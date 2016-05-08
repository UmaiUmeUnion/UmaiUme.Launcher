using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using ExIni;
using Mono.Cecil;
using ReiPatcher;
using ReiPatcher.Patch;

namespace UmaiUme.Launcher
{
    public class Program
    {
        public const string DEFAULT_CONFIG_NAME = "UULauncher.ini";

        private const string LOGO =
        @"  _    _                 _ _    _                " + "\r\n"
        + @" | |  | |               (_) |  | |               " + "\r\n"
        + @" | |  | |_ __ ___   __ _ _| |  | |_ __ ___   ___ " + "\r\n"
        + @" | |  | | '_ ` _ \ / _` | | |  | | '_ ` _ \ / _ \" + "\r\n"
        + @" | |__| | | | | | | (_| | | |__| | | | | | |  __/" + "\r\n"
        + @"  \____/|_| |_| |_|\__,_|_|\____/|_| |_| |_|\___|";

        private static readonly string PROCESS_NAME = Process.GetCurrentProcess().ProcessName;
        private static string configFilePath;
        private static object Patches;
        private static readonly List<PatcherArguments> Assemblies = new List<PatcherArguments>();
        public static Process GameProcess;
        public static string AssembliesDir { get; private set; }
        public static bool ContinueWithErrors { get; private set; }
        public static string ExecArgs { get; private set; }
        public static string Executable { get; private set; }
        public static string GameExecutableName { get; private set; }
        public static string PatchesDir { get; private set; }
        public static bool PauseOnError { get; private set; }
        public static string ReiPatcherDir { get; private set; }
        private static Version Version => Assembly.GetExecutingAssembly().GetName().Version;
        public static string WorkingDirectory { get; private set; }

        public static bool HideWhileGameRuns { get; private set; }

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
            configFilePath = DEFAULT_CONFIG_NAME;
            if (args.Length > 0)
                ParseArguments(args);
            if (configFilePath == DEFAULT_CONFIG_NAME && !File.Exists(DEFAULT_CONFIG_NAME))
            {
                DialogResult result = MessageBox.Show(
                "No default configuration file found. Create one?",
                "Create configuration file?",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

                if (result == DialogResult.Yes)
                {
                    Utils.CreateDefaultIni();
                    Utils.ShowInfo(
                    "Configuration file created.",
                    $"Created configuration file {DEFAULT_CONFIG_NAME}. Edit it before running {PROCESS_NAME}.exe again.");
                    Environment.Exit(0);
                }
                else
                {
                    Logger.Log(LogLevel.Error, $"No configuration file specified! Use {PROCESS_NAME}.exe -h for help.");
                    Environment.Exit(-1);
                }
            }
            LoadConfiguration();
            Logger.Log(LogLevel.Info, $"Loaded configuration file from {configFilePath}");

            Utils.Assert(() => LoadPatches(), "An error occurred while loading patches!");
            Utils.Assert(() => PrePatch(), "An error occurred during pre-patching!");
            Utils.Assert(() => Patch(), "An error occurred during patching!");
            Utils.Assert(() => PostPatch(), "An error occurred during post-patching!");
            Utils.Assert(() => RunGame(), "An error occurred when running the game!");

            Logger.LogWriter.Close();
            Logger.LogWriter.Dispose();
        }

        private static void RunGame()
        {
            Logger.Log(LogLevel.Info, "Patching is complete. Launching the game...");


            if (!GameExecutableName.IsNullOrWhiteSpace())
            {
                Logger.Log("Starting main process");

                Process.Start(Executable, ExecArgs);

                Logger.Log($"Searching for process {GameExecutableName}. Press CTRL+C to stop and close UULauncher...");

                Process[] processes;
                do
                {
                    processes = Process.GetProcessesByName(GameExecutableName);
                } while (processes.Length == 0);

                Logger.Log(LogLevel.Info, "Found process! Binding...");
                GameProcess = Process.GetProcessById(processes[0].Id);
                Logger.Log(LogLevel.Info, "Done!");
            }
            else
            {
                GameProcess = Process.Start(Executable, ExecArgs);

                if (GameProcess == null)
                {
                    Logger.Log(LogLevel.Error, "The process could not be started. Exiting...");
                    Environment.Exit(-1);
                }
                Logger.Log(LogLevel.Info, "Game process launched!");
            }
            Utils.SetConsoleCtrlHandler(HandleConsoleCtrl, true);
            Logger.StopTime();
            Logger.Log(
            LogLevel.Warning,
            "NOTE: DO NOT close this window while the game is running. UULauncher will perform clean-up after the game is closed.");
            IntPtr consoleHandle = Utils.GetConsoleWindow();
            if (HideWhileGameRuns)
            {
                Utils.ShowWindow(consoleHandle, Utils.SW_HIDE);
            }

            GameProcess.WaitForExit();
            if (HideWhileGameRuns)
            {
                Utils.ShowWindow(consoleHandle, Utils.SW_SHOW);
            }
            Logger.Log(LogLevel.Info, "Game exited");
            RestoreAssemblies();
        }

        private static bool HandleConsoleCtrl(int eventType)
        {
            switch (eventType)
            {
                case Utils.CTRL_C_EVENT:
                case Utils.CTRL_BREAK_EVENT:
                case Utils.CTRL_CLOSE_EVENT:
                    Logger.Log(LogLevel.Warning, "UULauncher has been closed suddenly! Closing game...");
                    GameProcess.Kill();
                    RestoreAssemblies();
                    break;
            }

            return true;
        }

        private static void RestoreAssemblies()
        {
            Logger.Log(LogLevel.Info, "Restoring assemblies...");

            string asmTempFolder = Path.Combine(AssembliesDir, "asm_tmp");
            if (Directory.Exists(asmTempFolder))
            {
                foreach (string file in Directory.GetFiles(asmTempFolder))
                {
                    Utils.MoveFile(file, Path.Combine(AssembliesDir, Path.GetFileName(file)));
                }
                Directory.Delete(asmTempFolder, true);
            }

            Logger.Log(LogLevel.Info, "Done");
        }

        private static void PostPatch()
        {
            List<PatchBase> patches = (List<PatchBase>) Patches;

            Logger.Log(LogLevel.Info, "Post-patch:");

            foreach (PatchBase patch in patches)
            {
                try
                {
                    patch.PostPatch();
                    Logger.LogColor(LogLevel.Info, $"[$(Green)POST-PATCH$] {patch.Name} {patch.Version}");
                }
                catch (Exception e)
                {
                    Logger.LogColor(LogLevel.Info, $"[$(Red)POST-PATCH$] {patch.Name} {patch.Version}");
                    Logger.Log(
                    LogLevel.Error,
                    $"An error occurred in {patch.Name} {patch.Version} ({patch.GetType().Assembly.GetName().Name} {patch.GetType().Assembly.GetName().Version}):\n {e}");
                    Environment.Exit(-1);
                }
            }

            Logger.Log(LogLevel.Info, "Done");
        }

        private static void Patch()
        {
            List<PatchBase> patches = (List<PatchBase>) Patches;

            Logger.Log(LogLevel.Info, "Performing patching:");

            foreach (PatchBase patch in patches)
            {
                foreach (PatcherArguments patcherArguments in Assemblies)
                {
                    try
                    {
                        if (patch.CanPatch(patcherArguments))
                        {
                            Logger.LogColor(LogLevel.Info, $"[$(Green)PATCH$]   {patch.Name} {patch.Version}");
                            patch.Patch(patcherArguments);
                            patcherArguments.WasPatched = true;
                        }
                        else
                            Logger.LogColor(LogLevel.Info, $"[$(Yellow)PATCHED$] {patch.Name} {patch.Version}");
                    }
                    catch (Exception e)
                    {
                        Logger.Log(
                        LogLevel.Error,
                        $"An error occurred in {patch.Name} {patch.Version} ({patch.GetType().Assembly.GetName().Name} {patch.GetType().Assembly.GetName().Version}):\n {e}");
                        Environment.Exit(-1);
                    }
                }
            }

            Logger.Log(LogLevel.Info, "Patching complete. Saving assemblies...");
            foreach (PatcherArguments patcherArguments in Assemblies)
            {
                patcherArguments.Assembly.Write(patcherArguments.Location);
            }
            Logger.Log(LogLevel.Info, "Done");
        }

        private static void PrePatch()
        {
            List<PatchBase> patches = (List<PatchBase>) Patches;
            Logger.Log(LogLevel.Info, "Pre-patch:");

            for (int index = 0; index < patches.Count; index++)
            {
                PatchBase patch = patches[index];
                try
                {
                    patch.PrePatch();
                    Logger.LogColor(LogLevel.Info, $"[$(Green)PRE-PATCH$] {patch.Name}");
                }
                catch (Exception e)
                {
                    Logger.LogColor(LogLevel.Info, $"[$(Red)PRE-PATCH$] {patch.Name}");
                    Logger.Log(LogLevel.Error, $"Failed to run pre-patch on {patch.Name}! Error info:\n{e}");
                    if (ContinueWithErrors)
                    {
                        patches.Remove(patch);
                        index--;
                    }
                    else
                    {
                        if (PauseOnError)
                        {
                            Console.WriteLine("Press any key to exit...");
                            Console.ReadKey(true);
                        }
                        Environment.Exit(-1);
                    }
                }
            }

            Patches = patches;

            if (!RPConfig.ConfigFile.HasSection("Assemblies"))
                return;

            Logger.Log(LogLevel.Info, "Requesting assemblies to patch:");
            List<string> assemblies = new List<string>();


            foreach (IniKey key in RPConfig.ConfigFile["Assemblies"].Keys)
            {
                if (!assemblies.Contains(key.Value))
                {
                    assemblies.Add(key.Value);

                    string path = Path.Combine(AssembliesDir, key.Value);
                    DefaultAssemblyResolver assemblyResolver = new DefaultAssemblyResolver();
                    assemblyResolver.AddSearchDirectory(AssembliesDir);
                    assemblyResolver.AddSearchDirectory(PatchesDir);
                    assemblyResolver.AddSearchDirectory(Path.GetDirectoryName(path));
                    ReaderParameters rp = new ReaderParameters {AssemblyResolver = assemblyResolver};
                    AssemblyDefinition ass;
                    using (FileStream fs = File.OpenRead(path))
                    {
                        ass = AssemblyDefinition.ReadAssembly(fs, rp);
                    }
                    Assemblies.Add(new PatcherArguments(ass, path, false));
                    Logger.Log(LogLevel.Info, $"{key.Value}");
                }
            }

            string asmTempFolder = Path.Combine(AssembliesDir, "asm_tmp");
            if (Directory.Exists(asmTempFolder))
            {
                Logger.Log(LogLevel.Warning, "Found unrestored assemblies! Restoring before proceeding...");
                string[] files = Directory.GetFiles(asmTempFolder);
                foreach (string file in files)
                {
                    Utils.MoveFile(file, Path.Combine(AssembliesDir, Path.GetFileName(file)));
                }
                Directory.Delete(asmTempFolder, true);
            }

            Logger.Log(LogLevel.Info, "Temporarily backing-up the original assemblies");
            Directory.CreateDirectory(asmTempFolder);
            foreach (string assembly in assemblies)
            {
                File.Copy(Path.Combine(AssembliesDir, assembly), Path.Combine(asmTempFolder, assembly));
            }

            Logger.Log(LogLevel.Info, "Done");
        }

        private static void LoadPatches()
        {
            string[] dlls = Directory.GetFiles(PatchesDir, "*.dll", SearchOption.AllDirectories);
            Logger.Log(LogLevel.Info, $"Found {dlls.Length} DLLs");
            Logger.Log(LogLevel.Info, "Loading patches...");

            List<PatchBase> patches = new List<PatchBase>();
            Dictionary<string, Exception> exceptions = new Dictionary<string, Exception>();
            foreach (string dllFile in dlls)
            {
                Assembly assembly = Assembly.LoadFile(dllFile);

                List<Type> patchClasses =
                assembly.GetTypes()
                        .Where(
                        type =>
                        !type.IsAbstract && type.IsClass && !type.IsInterface
                        && typeof (PatchBase).IsAssignableFrom(type))
                        .ToList();

                if (patchClasses.Count == 0)
                    continue;
                Logger.Log(LogLevel.Info, $"Loading patches from {Path.GetFileNameWithoutExtension(dllFile)}:");

                foreach (Type patchClass in patchClasses)
                {
                    try
                    {
                        PatchBase patch = (PatchBase) Activator.CreateInstance(patchClass);
                        Logger.LogColor(
                        LogLevel.Info,
                        $"  + [$(Green)OK$]   {patchClass.Name} -- {patch.Name} {patch.Version}");
                        patches.Add(patch);
                    }
                    catch (Exception e)
                    {
                        Logger.LogColor(LogLevel.Info, $"  + [$(Red)FAIL$] {patchClass.Name}");
                        exceptions.Add($"{patchClass.Name} from {dllFile}", e);
                    }
                }
            }

            if (exceptions.Count != 0)
            {
                Logger.Log(LogLevel.Error, "One or more errors occurred while attempting to load patches:");

                foreach (KeyValuePair<string, Exception> exception in exceptions)
                {
                    Logger.Log(LogLevel.Error, $"Exception at {exception.Key}:\n{exception.Value}");
                }

                if (!ContinueWithErrors)
                {
                    if (PauseOnError)
                    {
                        Console.WriteLine("Press any key to exit...");
                        Console.ReadKey(true);
                    }
                    Environment.Exit(-1);
                }
            }

            Logger.Log(LogLevel.Info, $"Loaded {patches.Count} patches");

            Patches = patches;
        }

        private static void LoadConfiguration()
        {
            IniFile configFile = IniFile.FromFile(configFilePath);
            ReiPatcherDir = configFile["Directories"]["ReiPatcherDir"].Value;
            Utils.Assert(
            () => ReiPatcherDir.IsNullOrWhiteSpace() || !Directory.Exists(ReiPatcherDir),
            "Could not locate ReiPatcher directory. Make sure the directory specified in the configuration file is valid.");

            PatchesDir = configFile["Directories"]["PatchesDir"].Value;
            Utils.Assert(
            () => ReiPatcherDir.IsNullOrWhiteSpace() || !Directory.Exists(PatchesDir),
            "Could not locate Patches directory. Make sure the directory specified in the configuration file is valid.");

            AssembliesDir = configFile["Directories"]["AssembliesDir"].Value;
            Utils.Assert(
            () => ReiPatcherDir.IsNullOrWhiteSpace() || !Directory.Exists(AssembliesDir),
            "Could not locate Assemblies directory. Make sure the directory specified in the configuration file is valid.");

            Executable = configFile["Launch"]["Executable"].Value;
            Utils.Assert(
            () => ReiPatcherDir.IsNullOrWhiteSpace() || !File.Exists(Executable),
            "Could not locate the executable to launch. Make sure the launching executable is specified!");

            ExecArgs = configFile["Launch"]["Arguments"].Value;

            WorkingDirectory = configFile["Launch"]["WorkingDirectory"].Value;

            GameExecutableName = configFile["Launch"]["GameExecutableName"].Value;

            IniKey pauseOnError = configFile["UULauncher"]["PauseOnError"];
            bool bPauseOnError = true;
            if (pauseOnError.Value.IsNullOrWhiteSpace() || !bool.TryParse(pauseOnError.Value, out bPauseOnError))
                pauseOnError.Value = bPauseOnError.ToString();
            PauseOnError = bPauseOnError;

            IniKey continueWithErrors = configFile["UULauncher"]["ContinueWithErrors"];
            bool bContinueWithErrors = false;
            if (continueWithErrors.Value.IsNullOrWhiteSpace()
                || !bool.TryParse(continueWithErrors.Value, out bContinueWithErrors))
                continueWithErrors.Value = bContinueWithErrors.ToString();
            ContinueWithErrors = bContinueWithErrors;

            IniKey hideWhileGameRuns = configFile["UULauncher"]["HideWhileGameRuns"];
            bool bHideWhileGameRuns = false;
            if (hideWhileGameRuns.Value.IsNullOrWhiteSpace()
                || !bool.TryParse(hideWhileGameRuns.Value, out bHideWhileGameRuns))
                hideWhileGameRuns.Value = bHideWhileGameRuns.ToString();
            HideWhileGameRuns = bHideWhileGameRuns;

            configFile.Save(configFilePath);

            AppDomain.CurrentDomain.AssemblyResolve += Utils.OnResolveAssembly;

            RPConfig.ConfigFilePath = configFilePath;
            RPConfig.SetConfig("ReiPatcher", "AssembliesDir", AssembliesDir);
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
                        Utils.ShowInfo(
                        "UmaiUme Launcher: Usage of -h",
                        $"Usage:\n\n{PROCESS_NAME}.exe -c <PATH>\n\nwhere <PATH> is the path to the INI configuration file.");
                        Environment.Exit(0);
                    }

                    if (!File.Exists(args[1]))
                    {
                        Utils.ShowInfo(
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
            Utils.ShowInfo(
            "UmaiUme Launcher Help Box",
            $"UmaiUme Launcher (UULauncher) v. {Version}\n© 2016 UmaiUme\nLicensed under the MIT licence\n\nA tool to patch and run Unity Games.\n\nUsage: {PROCESS_NAME}.exe [ARGUMENTS]\n\nARGUMENTS:\n(No arguments)\tRuns UULauncher with the default configuration file.\n-h\t\tDisplays this help box.\n-c <PATH>\tRuns UULauncher with custom configuration file specified by <PATH>.");
        }
    }
}