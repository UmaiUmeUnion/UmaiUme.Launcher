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

namespace UmaiUme.Launcher
{
    public static class Patcher
    {
        public static List<PatcherArguments> Assemblies { get; set; }
        public static object Patches { get; set; }

        public static void PostPatch()
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
                    if (Configuration.PauseOnError)
                    {
                        Console.WriteLine("Press any key to exit...");
                        Console.ReadKey(true);
                    }
                    Environment.Exit(-1);
                }
            }

            Logger.Log(LogLevel.Info, "Done");
        }

        public static void RestoreAssemblies()
        {
            Logger.Log(LogLevel.Info, "Restoring assemblies...");

            string asmTempFolder = Path.Combine(Configuration.AssembliesDir, "asm_tmp");
            if (Directory.Exists(asmTempFolder))
            {
                foreach (string file in Directory.GetFiles(asmTempFolder))
                {
                    FileUtils.MoveFile(file, Path.Combine(Configuration.AssembliesDir, Path.GetFileName(file)));
                }
                Directory.Delete(asmTempFolder, true);
            }

            Logger.Log(LogLevel.Info, "Done");
        }

        public static void Patch()
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

        public static void PrePatch()
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

            if (!RPConfig.ConfigFile.HasSection("Assemblies"))
                return;

            Logger.Log(LogLevel.Info, "Requesting assemblies to patch:");
            List<string> assemblies = new List<string>();
            Assemblies = new List<PatcherArguments>();
            foreach (IniKey key in RPConfig.ConfigFile["Assemblies"].Keys)
            {
                if (!assemblies.Contains(key.Value))
                {
                    assemblies.Add(key.Value);

                    string path = Path.Combine(Configuration.AssembliesDir, key.Value);
                    DefaultAssemblyResolver assemblyResolver = new DefaultAssemblyResolver();
                    assemblyResolver.AddSearchDirectory(Configuration.AssembliesDir);
                    assemblyResolver.AddSearchDirectory(Configuration.PatchesDir);
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

            string asmTempFolder = Path.Combine(Configuration.AssembliesDir, "asm_tmp");
            if (Directory.Exists(asmTempFolder))
            {
                Logger.Log(LogLevel.Warning, "Found unrestored assemblies! Restoring before proceeding...");
                RestoreAssemblies();
            }

            Logger.Log(LogLevel.Info, "Temporarily backing-up the original assemblies");
            Directory.CreateDirectory(asmTempFolder);
            foreach (string assembly in assemblies)
            {
                File.Copy(Path.Combine(Configuration.AssembliesDir, assembly), Path.Combine(asmTempFolder, assembly));
            }

            Logger.Log(LogLevel.Info, "Done");
        }

        public static void LoadPatches()
        {
            string[] dlls = Directory.GetFiles(Configuration.PatchesDir, "*.dll", SearchOption.AllDirectories);
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

                if (!Configuration.ContinueWithErrors)
                {
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
    }
}