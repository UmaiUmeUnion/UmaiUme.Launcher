using System;
using System.IO;
using ExIni;
using ReiPatcher;
using UmaiUme.Launcher.Utils;
using static UmaiUme.Launcher.Utils.Helpers;

namespace UmaiUme.Launcher
{
    public static class Configuration
    {
        public const string DEFAULT_CONFIG_NAME = "UULauncher.ini";
        public static string AssembliesDir { get; private set; }
        public static IniFile ConfigFile { get; private set; }
        public static string ConfigFilePath { get; private set; }
        public static bool ContinueWithErrors { get; private set; }
        public static string ExecArgs { get; private set; }
        public static string Executable { get; private set; }
        public static string GameExecutableName { get; private set; }
        public static bool HideWhileGameRuns { get; private set; }
        public static string PatchesDir { get; private set; }
        public static bool PauseOnError { get; private set; }
        public static string ReiPatcherDir { get; private set; }
        public static string WorkingDirectory { get; private set; }

        public static void Load(string path)
        {
            ConfigFilePath = path;
            IniFile configFile = IniFile.FromFile(ConfigFilePath);
            ReiPatcherDir = configFile["Directories"]["ReiPatcherDir"].Value;
            Assert(
            () => ReiPatcherDir.IsNullOrWhiteSpace() || !Directory.Exists(ReiPatcherDir),
            "Could not locate ReiPatcher directory. Make sure the directory specified in the configuration file is valid.");

            PatchesDir = configFile["Directories"]["PatchesDir"].Value;
            Assert(
            () => ReiPatcherDir.IsNullOrWhiteSpace() || !Directory.Exists(PatchesDir),
            "Could not locate Patches directory. Make sure the directory specified in the configuration file is valid.");

            AssembliesDir = configFile["Directories"]["AssembliesDir"].Value;
            Assert(
            () => ReiPatcherDir.IsNullOrWhiteSpace() || !Directory.Exists(AssembliesDir),
            "Could not locate Assemblies directory. Make sure the directory specified in the configuration file is valid.");

            Executable = configFile["Launch"]["Executable"].Value;
            Assert(
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

            configFile.Save(ConfigFilePath);

            AppDomain.CurrentDomain.AssemblyResolve += OnResolveAssembly;

            RPConfig.ConfigFilePath = ConfigFilePath;
            ConfigFile = RPConfig.ConfigFile;
            RPConfig.SetConfig("ReiPatcher", "AssembliesDir", AssembliesDir);
        }

        public static void CreateDefaultConfiguration()
        {
            IniFile file = new IniFile();
            IniSection section = file["UULauncher"];
            section.Comments.Append(
            "UmaiUme Launcher Default Configuration File",
            "",
            "In this file, you can configure where UULauncher loads the game and other tools from.",
            "By default, UULauncher assumes that it has been installed into the game's root directory.",
            "If that is the case, you can only edit the lines commented with \"EDIT ME\" prefix.",
            "",
            "",
            "EDIT ME: Complete the comment below this one by specifying the name of the game's executable without the .exe extension.",
            "@GAME=",
            "",
            "EDIT ME: Specify the path to the game root",
            "@GAME_PATH=",
            "",
            "EDIT ME: Edit the section below to configure UULauncher to your taste");
            IniKey key = section["PauseOnError"];
            key.Value = "True";
            key.Comments.Append("If true, pauses and shows \"Press any key to exit...\" message after an error occurs");
            key = section["ContinueWithErrors"];
            key.Value = "False";
            key.Comments.Append(
            "If true, UULauncher will skip patches that cause errors. If false, UULauncher will exit if an error occurs during patching.");
            key = section["HideWhileGameRuns"];
            key.Value = "False";
            key.Comments.Append("If true, will hide the console window while the game is running.");

            section = file["Directories"];
            key = section["ReiPatcherDir"];
            key.RawValue = "%GAME_PATH%\\ReiPatcher";
            key.Comments.Append("Directory where ReiPatcher.exe is installed.");
            key = section["PatchesDir"];
            key.RawValue = "%GAME_PATH%\\ReiPatcher\\Patches";
            key.Comments.Append("Directory where ReiPatcher patches are located.");
            key = section["AssembliesDir"];
            key.RawValue = "%GAME_PATH%\\%GAME%_Data\\Managed";
            key.Comments.Append("Directory that contains the game's managed assemblies.");

            section = file["Launch"];
            key = section["Executable"];
            key.Comments.Append("EDIT ME: Specify the path to the game's (or locale emulator's) executable.");
            key = section["Arguments"];
            key.Comments.Append(
            "EDIT ME: Specify the arguments that are passed to the executable. If no arguments are needed/wanted, leave empty.");
            key = section["WorkingDirectory"];
            key.Comments.Append(
            "EDIT ME: If needed, specify the working directory of the executable. Usually the same as the game's root directory.");

            file.Save(DEFAULT_CONFIG_NAME);
        }
    }
}