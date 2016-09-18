using System.Collections.Generic;
using System.IO;
using UmaiUme.Launcher.Logging;

namespace UmaiUme.Launcher.Utils
{
    public static class FileUtils
    {
        public static void BackupAssemblies(string assembliesDir, List<string> assembliesToBackup)
        {
            string asmTempFolder = Path.Combine(assembliesDir, Program.TMP_DIR);
            if (!Program.IsBackUpping && Directory.Exists(asmTempFolder))
            {
                Logger.Log(LogLevel.Warning, "Found unrestored assemblies! Restoring before proceeding...");
                RestoreAssemblies(assembliesDir);
            }

            Logger.Log(LogLevel.Info, "Temporarily backing-up the original assemblies");
            Directory.CreateDirectory(asmTempFolder);
            foreach (string assembly in assembliesToBackup)
            {
                File.Copy(Path.Combine(assembliesDir, assembly), Path.Combine(asmTempFolder, assembly));
            }
        }

        public static void RestoreAssemblies(string assembliesDir)
        {
            string asmTempFolder = Path.Combine(assembliesDir, Program.TMP_DIR);
            if (Directory.Exists(asmTempFolder))
            {
                foreach (string file in Directory.GetFiles(asmTempFolder))
                {
                    MoveFile(file, Path.Combine(assembliesDir, Path.GetFileName(file)));
                }
                Directory.Delete(asmTempFolder, true);
            }
        }

        public static bool MoveFile(string source, string dest)
        {
            if (!File.Exists(source)) return false;

            using (FileStream sourceStream = File.Open(source, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                using (FileStream destStream = File.Open(dest, FileMode.Create, FileAccess.Write, FileShare.ReadWrite))
                {
                    byte[] buffer = new byte[1024];

                    int length;
                    while ((length = sourceStream.Read(buffer, 0, 1024)) > 0)
                    {
                        destStream.Write(buffer, 0, length);
                        destStream.Flush();
                    }
                }
            }

            return true;
        }
    }
}