using System.IO;

namespace UmaiUme.Launcher.Utils
{
    public static class FileUtils
    {
        public static bool MoveFile(string source, string dest)
        {
            if (!File.Exists(source))
                return false;

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