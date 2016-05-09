using System;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
using ExIni;
using UmaiUme.Launcher.Logging;

namespace UmaiUme.Launcher.Utils
{
    public static class Helpers
    {
        public static Assembly OnResolveAssembly(object sender, ResolveEventArgs args)
        {
            string name = new AssemblyName(args.Name).Name;
            Assembly result;
            if (SearchAssembly(name, Configuration.ReiPatcherDir, false, out result)
                || SearchAssembly(name, Configuration.PatchesDir, true, out result)
                || SearchAssembly(name, Configuration.AssembliesDir, true, out result))
                return result;
            Logger.Log($"Could not locate {name}!");
            return null;
        }

        private static bool SearchAssembly(string name, string path, bool loadIntoMemory, out Assembly result)
        {
            result = null;
            string fileDLL = Path.Combine(path, name + ".dll");
            string fileEXE = Path.Combine(path, name + ".exe");
            if (File.Exists(fileDLL))
            {
                result = loadIntoMemory ? Assembly.Load(File.ReadAllBytes(fileDLL)) : Assembly.LoadFrom(fileDLL);
                return true;
            }
            if (File.Exists(fileEXE))
            {
                result = loadIntoMemory ? Assembly.Load(File.ReadAllBytes(fileEXE)) : Assembly.LoadFrom(fileEXE);
                return true;
            }

            return false;
        }

        public static void Assert(Func<bool> func, string error)
        {
            if (!func())
                return;
            Logger.Log(LogLevel.Error, error);
            if (Configuration.PauseOnError)
            {
                Console.WriteLine("Press any key to exit...");
                Console.ReadKey(true);
            }
            Environment.Exit(-1);
        }

        public static void Assert(Action func, string error)
        {
            try
            {
                func();
            }
            catch (Exception e)
            {
                Logger.Log(LogLevel.Error, $"{error}\nInfo: {e}");
                if (Configuration.PauseOnError)
                {
                    Console.WriteLine("Press any key to exit...");
                    Console.ReadKey(true);
                }
                Environment.Exit(-1);
            }
        }

        public static void ShowError(string title, string error)
        {
            MessageBox.Show(error, title, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        public static void ShowInfo(string title, string message)
        {
            MessageBox.Show(message, title, MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        public static void CreateDefaultIni()
        {
            IniFile file = new IniFile();
            IniSection section = file.CreateSection("UULauncher");
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
            IniKey key = section.CreateKey("PauseOnError");
            key.Value = "True";
            key.Comments.Append("If true, pauses and shows \"Press any key to exit...\" message after an error occurs");
            key = section.CreateKey("ContinueWithErrors");
            key.Value = "False";
            key.Comments.Append(
            "If true, UULauncher will skip patches that cause errors. If false, UULauncher will exit if an error occurs during patching.");
            key = section.CreateKey("HideWhileGameRuns");
            key.Value = "False";
            key.Comments.Append("If true, will hide the console window while the game is running.");

            section = file.CreateSection("Directories");
            key = section.CreateKey("ReiPatcherDir");
            key.RawValue = "%GAME_PATH%\\ReiPatcher";
            key.Comments.Append("Directory where ReiPatcher.exe is installed.");
            key = section.CreateKey("PatchesDir");
            key.RawValue = "%GAME_PATH%\\ReiPatcher\\Patches";
            key.Comments.Append("Directory where ReiPatcher patches are located.");
            key = section.CreateKey("AssembliesDir");
            key.RawValue = "%GAME_PATH%\\%GAME%_Data\\Managed";
            key.Comments.Append("Directory that contains the game's managed assemblies.");

            section = file.CreateSection("Launch");
            key = section.CreateKey("Executable");
            key.Comments.Append("EDIT ME: Specify the path to the game's (or locale emulator's) executable.");
            key = section.CreateKey("Arguments");
            key.Comments.Append(
            "EDIT ME: Specify the arguments that are passed to the executable. If no arguments are needed/wanted, leave empty.");
            key = section.CreateKey("WorkingDirectory");
            key.Comments.Append(
            "EDIT ME: If needed, specify the working directory of the executable. Usually the same as the game's root directory.");


            file.Save(Configuration.DEFAULT_CONFIG_NAME);
        }
    }
}