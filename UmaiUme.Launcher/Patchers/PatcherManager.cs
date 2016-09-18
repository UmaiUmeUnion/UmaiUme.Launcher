using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UmaiUme.Launcher.Logging;
using UmaiUme.Launcher.Utils;
using static UmaiUme.Launcher.Utils.Helpers;

namespace UmaiUme.Launcher.Patchers
{
    public static class PatcherManager
    {
        private static List<Patcher> loadedPatchers;

        public static void LoadPatchers()
        {
            loadedPatchers = new List<Patcher>();
            Dictionary<string, Exception> exceptions = new Dictionary<string, Exception>();
            string patchersDir = Configuration.GetValue("Directories", "PatchersDir").Trim();
            bool hasPatchersDir = patchersDir.IsNullOrWhiteSpace() && Directory.Exists(patchersDir);
            if (!hasPatchersDir)
            {
                Logger.Log(LogLevel.Warning,
                    "Key PatchersDir in section Directories is not specified in the configuration file! Skipping patcher search...");
            }
            Logger.Log(LogLevel.Info, "Loading patchers:");
            Logger.Log(LogLevel.Info, $"{Program.ProcessName}.exe");
            foreach (
                Type type in
                    Assembly.GetExecutingAssembly()
                            .GetTypes()
                            .Where(t => typeof (Patcher).IsAssignableFrom(t) && !t.IsAbstract))
            {
                try
                {
                    Patcher patcher = (Patcher) Activator.CreateInstance(type);
                    loadedPatchers.Add(patcher);
                    Logger.LogColor(LogLevel.Info, $"    +[$(Green)OK$]   {patcher.Name} {patcher.Version}");
                }
                catch (Exception e)
                {
                    Logger.LogColor(LogLevel.Info, $"    +[$(Green)FAIL$] {type.Name}");
                    exceptions.Add($"{type.Name} from {Program.ProcessName}.exe", e);
                }
            }
            if (hasPatchersDir)
            {
                string[] dlls = Directory.GetFiles(patchersDir, "*.dll");
                foreach (string dll in dlls)
                {
                    Assembly ass = Assembly.LoadFile(dll);
                    List<Type> assPatcherTypes =
                        ass.GetTypes().Where(t => typeof (Patcher).IsAssignableFrom(t) && !t.IsAbstract).ToList();

                    if (assPatcherTypes.Count == 0) continue;

                    Logger.Log(LogLevel.Info, $"{Path.GetFileName(dll)}");

                    foreach (Type patcherType in assPatcherTypes)
                    {
                        try
                        {
                            Patcher patcher = (Patcher) Activator.CreateInstance(patcherType);
                            loadedPatchers.Add(patcher);
                            Logger.LogColor(LogLevel.Info, $"    +[$(Green)OK$]   {patcher.Name} {patcher.Version}");
                        }
                        catch (Exception e)
                        {
                            Logger.LogColor(LogLevel.Info, $"    +[$(Green)FAIL$] {patcherType.Name}");
                            exceptions.Add($"{patcherType.Name} from {Path.GetFileName(dll)}", e);
                        }
                    }
                }
            }

            if (exceptions.Count != 0)
            {
                Logger.Log(LogLevel.Error, "Failed to load some patchers! Here is exception list:");
                foreach (KeyValuePair<string, Exception> exception in exceptions)
                {
                    Logger.Log(LogLevel.Error, $"Exception at {exception.Key}: {exception.Value}");
                }

                if (!Configuration.ContinueWithErrors)
                {
                    Logger.Log(LogLevel.Error, "Closing UULauncher because of the above errors");
                    if (Configuration.PauseOnError)
                    {
                        Console.WriteLine("Press any key to exit...");
                        Console.ReadKey(true);
                    }
                    Environment.Exit(-1);
                }
            }
        }

        public static void RunPatchers()
        {
            foreach (Patcher patcher in loadedPatchers)
            {
                Logger.Log(LogLevel.Info, $"Running {patcher.Name} {patcher.Version}:");
                Assert(() => patcher.LoadConfiguration(),
                    $"[{patcher.Name} {patcher.Version}] An error occurred while initializing the patcher configurations!");
                Assert(() => patcher.Initialize(),
                    $"[{patcher.Name} {patcher.Version}] An error occurred while initializing the patcher!");
                Assert(() => patcher.LoadPatches(),
                    $"[{patcher.Name} {patcher.Version}] An error occurred while loading patches!");
                Assert(() => patcher.PrePatch(),
                    $"[{patcher.Name} {patcher.Version}] An error occurred during pre-patching!");
                Assert(() => patcher.Patch(), $"[{patcher.Name} {patcher.Version}] An error occurred during patching!");
                Assert(() => patcher.PostPatch(),
                    $"[{patcher.Name} {patcher.Version}] An error occurred during post-patching!");
                Logger.Log(LogLevel.Info, $"Done running {patcher.Name} {patcher.Version}");
            }
        }

        public static void RunRestoreAssemblies()
        {
            Logger.Log(LogLevel.Info, "Resotring assemblies");
            foreach (Patcher patcher in loadedPatchers)
            {
                Assert(() => patcher.RestoreAssemblies(),
                    $"[{patcher.Name} {patcher.Version}] Failed to restore assemblies!");
            }
            Logger.Log(LogLevel.Info, "Done");
        }

        public static void InitializePatchers()
        {
            foreach (Patcher loadedPatcher in loadedPatchers)
            {
                loadedPatcher.LoadConfiguration();
                loadedPatcher.Initialize();
            }
        }
    }
}