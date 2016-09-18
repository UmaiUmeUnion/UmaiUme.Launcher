using System;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
using UmaiUme.Launcher.Logging;

namespace UmaiUme.Launcher.Utils
{
    public static class Helpers
    {
        public static bool SearchAssembly(string name, string path, bool loadIntoMemory, out Assembly result)
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
            if (!func()) return;
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
    }
}