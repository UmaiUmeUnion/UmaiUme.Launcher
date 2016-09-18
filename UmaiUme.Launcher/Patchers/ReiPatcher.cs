using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using ExIni;
using Mono.Cecil;
using ReiPatcher;
using ReiPatcher.Patch;
using UmaiUme.Launcher.Logging;
using UmaiUme.Launcher.Utils;
using static UmaiUme.Launcher.Utils.Helpers;

namespace UmaiUme.Launcher.Patchers
{
    public class ReiPatcher : Patcher
    {
        private static string TempConfigPath = string.Empty;
        public override string Name => "ReiPatcher";
        public override string Version => "0.9.0.8";
        public string AssembliesDir { get; private set; }
        public string PatchesDir { get; private set; }
        public string ReiPatcherDir { get; private set; }
        public static List<PatcherArguments> Assemblies { get; set; }
        public static object Patches { get; set; }

        public override void Initialize()
        {
            TempConfigPath = Path.Combine(ReiPatcherDir, "ReiPatcher_temp.ini");

            Logger.Log(LogLevel.Info, $"Creating temporary configuration file to {TempConfigPath}");
            Configuration.SaveConfig(TempConfigPath);

            RPConfig.ConfigFilePath = TempConfigPath;
            RPConfig.SetConfig("ReiPatcher", "AssembliesDir", AssembliesDir);
        }

        public override void LoadConfiguration()
        {
            Logger.Log(LogLevel.Info, "Parsing patcher configuration");
            ReiPatcherDir = Configuration.GetValue("Directories", "ReiPatcherDir");
            Assert(() => ReiPatcherDir.IsNullOrWhiteSpace() || !Directory.Exists(ReiPatcherDir),
                "Could not locate ReiPatcher directory. Make sure the directory specified in the configuration file is valid.");

            PatchesDir = Configuration.GetValue("Directories", "PatchesDir");
            Assert(() => ReiPatcherDir.IsNullOrWhiteSpace() || !Directory.Exists(PatchesDir),
                "Could not locate Patches directory. Make sure the directory specified in the configuration file is valid.");

            AssembliesDir = Configuration.GetValue("Directories", "AssembliesDir");
            Assert(() => ReiPatcherDir.IsNullOrWhiteSpace() || !Directory.Exists(AssembliesDir),
                "Could not locate Assemblies directory. Make sure the directory specified in the configuration file is valid.");

            AppDomain.CurrentDomain.AssemblyResolve += OnResolveAssembly;
        }

        public Assembly OnResolveAssembly(object sender, ResolveEventArgs args)
        {
            string name = new AssemblyName(args.Name).Name;
            Assembly result;
            if (SearchAssembly(name, ReiPatcherDir, false, out result)
                || SearchAssembly(name, PatchesDir, true, out result)
                || SearchAssembly(name, AssembliesDir, true, out result)) return result;
            Logger.Log(LogLevel.Error, $"Could not locate {name}!");
            return null;
        }

        public override void LoadPatches()
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

                if (patchClasses.Count == 0) continue;
                Logger.Log(LogLevel.Info, $"Loading patches from {Path.GetFileNameWithoutExtension(dllFile)}:");

                foreach (Type patchClass in patchClasses)
                {
                    try
                    {
                        PatchBase patch = (PatchBase) Activator.CreateInstance(patchClass);
                        Logger.LogColor(LogLevel.Info,
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

            Logger.Log(LogLevel.Info, $"Loaded {patches.Count} patches");

            Patches = patches;
        }

        public override void PrePatch()
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
                    if (Configuration.ContinueWithErrors)
                    {
                        patches.Remove(patch);
                        index--;
                    }
                    else
                    {
                        if (Configuration.PauseOnError)
                        {
                            Console.WriteLine("Press any key to exit...");
                            Console.ReadKey(true);
                        }
                        Environment.Exit(-1);
                    }
                }
            }

            Patches = patches;

            if (!RPConfig.ConfigFile.HasSection("Assemblies")) return;

            Logger.Log(LogLevel.Info, "Requesting assemblies to patch:");
            List<string> assemblies = new List<string>();
            Assemblies = new List<PatcherArguments>();
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

            FileUtils.BackupAssemblies(AssembliesDir, assemblies);
            Logger.Log(LogLevel.Info, "Done");
        }

        public override void RestoreAssemblies()
        {
            FileUtils.RestoreAssemblies(AssembliesDir);
        }

        public override void Patch()
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
                        else Logger.LogColor(LogLevel.Info, $"[$(Yellow)PATCHED$] {patch.Name} {patch.Version}");
                    }
                    catch (Exception e)
                    {
                        Logger.Log(LogLevel.Error,
                            $"An error occurred in {patch.Name} {patch.Version} ({patch.GetType().Assembly.GetName().Name} {patch.GetType().Assembly.GetName().Version}):\n {e}");
                        if (Configuration.PauseOnError)
                        {
                            Console.WriteLine("Press any key to exit...");
                            Console.ReadKey(true);
                        }
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

        public override void PostPatch()
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
                    Logger.Log(LogLevel.Error,
                        $"An error occurred in {patch.Name} {patch.Version} ({patch.GetType().Assembly.GetName().Name} {patch.GetType().Assembly.GetName().Version}):\n {e}");
                    if (Configuration.PauseOnError)
                    {
                        Console.WriteLine("Press any key to exit...");
                        Console.ReadKey(true);
                    }
                    Environment.Exit(-1);
                }
            }

            Logger.Log(LogLevel.Info, "Done");

            Logger.Log(LogLevel.Info, "Removing temporary configuration file");
            try
            {
                if (TempConfigPath != string.Empty) File.Delete(TempConfigPath);
            }
            catch (Exception e)
            {
                Logger.Log(LogLevel.Warning,
                    $"Failed to remove because of {e.GetType().Name}. Leaving the file for now...");
            }

            Logger.Log(LogLevel.Info, "Done");
        }
    }
}