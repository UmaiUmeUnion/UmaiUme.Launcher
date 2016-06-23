using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using ExIni;
using Microsoft.Win32;
using ReiPatcher;
using UmaiUme.Launcher.Logging;
using UmaiUme.Launcher.Utils;
using static UmaiUme.Launcher.Utils.Helpers;

namespace UmaiUme.Launcher
{
    public static class Configuration
    {
        public const string DEFAULT_CONFIG_NAME = "UULauncher.ini";
        public static string ConfigFilePath { get; private set; }
        public static bool ContinueWithErrors { get; private set; }
        public static string ExecArgs { get; private set; }
        public static string Executable { get; private set; }
        public static string GameExecutableName { get; private set; }
        public static bool HideWhileGameRuns { get; private set; }
        public static bool PauseOnError { get; private set; }
        public static string WorkingDirectory { get; private set; }

        private static readonly Regex patternKeyVal = new Regex(@"(?<key>[^;]+)\=(?<value>[^;]*)(;.*)?");
        private static readonly Regex patternSection = new Regex(@"\[(?<section>.*)\]");
        private static readonly Regex patternRegSearch = new Regex(@"\$\((?<regPath>[^\(\)]*)\)");
        private static readonly Regex patternVariable = new Regex(@"\%(?<variable>[^\%]*)\%");

        private static Dictionary<string, Dictionary<string, string>> configuration = new Dictionary<string, Dictionary<string, string>>();

        public static string GetValue(string section, string key)
        {
            Dictionary<string, string> sec;
            string result;
            return configuration.TryGetValue(section, out sec) && sec.TryGetValue(key, out result) ? result : string.Empty;
        }

        public static void SaveConfig(string path)
        {
            using (StreamWriter sw = new StreamWriter(File.Create(path)))
            {
                sw.WriteLine(";DO NOT EDIT");
                sw.WriteLine(";This is a temoporary configuration file created by UULauncher for patching purposes.");
                sw.WriteLine(";Editing this file will not affect UULauncher's configuration.");
                sw.WriteLine(";To edit UULauncher's configuration, edit the following configuration file:");
                sw.WriteLine(";");
                sw.WriteLine(";{0}", ConfigFilePath);
                sw.WriteLine();
                foreach (var section in configuration)
                {
                    sw.WriteLine("[{0}]", section.Key);
                    foreach (var variable in section.Value)
                    {
                        sw.WriteLine("{0}={1}", variable.Key, variable.Value);
                    }
                    sw.WriteLine();
                }
            }
        }

        public static void ParseConfig(string path)
        {
            ConfigFilePath = path;
            string[] lines = File.ReadAllLines(path);

            Dictionary<string, string> currentSection = new Dictionary<string, string>();
            configuration.Add("DEFINE", currentSection);

            foreach (string configLine in lines) {
                string line = configLine.Trim();

                if(line.StartsWith(";"))
                    continue;

                Match sectionMatch = patternSection.Match(line);

                if (sectionMatch.Success)
                {
                    string sec = sectionMatch.Groups["section"].Value.Trim();

                    if (!configuration.ContainsKey(sec))
                        configuration.Add(sec, new Dictionary<string, string>());
                    currentSection = configuration[sec];
                    continue;
                }

                Match keyValMatch = patternKeyVal.Match(line);

                if (!keyValMatch.Success)
                    continue;
                string key = keyValMatch.Groups["key"].Value.Trim();
                string value = keyValMatch.Groups["value"].Value.Trim();

                value = patternVariable.Replace(value,
                match =>
                {
                    string var = match.Groups["variable"].Value.Trim();
                    Dictionary<string, string> defines;
                    if (!configuration.TryGetValue("DEFINE", out defines))
                        return Environment.GetEnvironmentVariable(var) ?? string.Empty;
                    string result;
                    return defines.TryGetValue(var, out result) ? result : string.Empty;
                });

                value = patternRegSearch.Replace(value,
                match =>
                {
                    string regKey = match.Groups["regPath"].Value.Trim();
                    int index = regKey.LastIndexOf('\\');
                    string keyName = index != -1 ? regKey.Substring(0, index) : regKey;
                    string valueName = index != -1 ? regKey.Remove(0, index + 1) : string.Empty;

                    return (string) Registry.GetValue(keyName, valueName, string.Empty);
                });

                currentSection[key] = value;
            }
            LoadValues();
        }

        private static void LoadValues()
        {
            Executable = GetValue("Launch", "Executable");
            Assert(
            () => Executable.IsNullOrWhiteSpace() || !File.Exists(Executable),
            "Could not locate the executable to launch. Make sure the launching executable is specified!");

            ExecArgs = GetValue("Launch", "Arguments");

            WorkingDirectory = GetValue("Launch", "WorkingDirectory");

            GameExecutableName = GetValue("Launch", "GameExecutableName");

            string pauseOnError = GetValue("UULauncher", "PauseOnError");
            bool bPauseOnError = true;
            if (pauseOnError.IsNullOrWhiteSpace() || !bool.TryParse(pauseOnError, out bPauseOnError))
                Logger.Log(LogLevel.Warning, $"Value PauseOnError could not be parsed! Setting to {bPauseOnError}...");
            PauseOnError = bPauseOnError;

            string continueWithErrors = GetValue("UULauncher", "ContinueWithErrors");
            bool bContinueWithErrors = false;
            if (continueWithErrors.IsNullOrWhiteSpace()
                || !bool.TryParse(continueWithErrors, out bContinueWithErrors))
                Logger.Log(LogLevel.Warning, $"Value ContinueWithErrors could not be parsed! Setting to {bContinueWithErrors}...");
            ContinueWithErrors = bContinueWithErrors;

            string hideWhileGameRuns = GetValue("UULauncher", "HideWhileGameRuns");
            bool bHideWhileGameRuns = false;
            if (hideWhileGameRuns.IsNullOrWhiteSpace()
                || !bool.TryParse(hideWhileGameRuns, out bHideWhileGameRuns))
                Logger.Log(LogLevel.Warning, $"Value HideWhileGameRuns could not be parsed! Setting to {bHideWhileGameRuns}...");
            HideWhileGameRuns = bHideWhileGameRuns;
        }

        public static void CreateDefaultConfiguration()
        {
            using (Stream s = Assembly.GetExecutingAssembly().GetManifestResourceStream(typeof(Configuration), "UULauncher.ini"))
            {
                using (FileStream fs = File.Open(DEFAULT_CONFIG_NAME, FileMode.Create))
                {
                    byte[] buffer = new byte[1024];
                    int len;
                    while ((len = s.Read(buffer, 0, 1024)) > 0)
                        fs.Write(buffer, 0, len);
                }
            }
        }
    }
}