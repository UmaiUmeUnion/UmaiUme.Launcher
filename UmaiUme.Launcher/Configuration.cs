using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.Win32;
using UmaiUme.Launcher.Logging;
using UmaiUme.Launcher.Utils;
using static UmaiUme.Launcher.Utils.Helpers;

namespace UmaiUme.Launcher
{
    public static class Configuration
    {
        public const string DEFAULT_CONFIG_NAME = "UULauncher.ini";
        private static readonly Regex patternKeyVal = new Regex(@"(?<key>[^;]+)\=(?<value>[^;]*)(;.*)?");
        private static readonly Regex patternSection = new Regex(@"\[(?<section>.*)\]");
        private static readonly Regex patternRegSearch = new Regex(@"\$\((?<regPath>[^\(\)]*)\)");
        private static readonly Regex patternVariable = new Regex(@"\%(?<variable>[^\%]*)\%");

        private static readonly Dictionary<string, Section> configuration = new Dictionary<string, Section>();

        public static string ConfigFilePath { get; private set; }
        public static bool ContinueWithErrors { get; private set; }
        public static string ExecArgs { get; private set; }
        public static string Executable { get; private set; }
        public static string GameExecutableName { get; private set; }
        public static bool HideWhileGameRuns { get; private set; }
        public static bool PauseOnError { get; private set; }
        public static string WorkingDirectory { get; private set; }

        private static string ParseValue(string value)
        {
            value = value.Trim();
            value = patternVariable.Replace(value, match =>
            {
                string var = match.Groups["variable"].Value.Trim();
                Section defines;
                if (!configuration.TryGetValue("DEFINE", out defines)) return Environment.GetEnvironmentVariable(var) ?? string.Empty;
                Key result;
                return defines.Keys.TryGetValue(var, out result) ? result.Value : string.Empty;
            });

            value = patternRegSearch.Replace(value, match =>
            {
                string regKey = match.Groups["regPath"].Value.Trim();
                int index = regKey.LastIndexOf('\\');
                string keyName = index != -1 ? regKey.Substring(0, index) : regKey;
                string valueName = index != -1 ? regKey.Remove(0, index + 1) : string.Empty;

                return (string) Registry.GetValue(keyName, valueName, string.Empty);
            });

            return value;
        }

        public static string GetValue(string section, string key)
        {
            Section sec;
            Key valueKey;
            return configuration.TryGetValue(section, out sec) && sec.Keys.TryGetValue(key, out valueKey)
                       ? valueKey.Value : string.Empty;
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
                foreach (KeyValuePair<string, Section> section in configuration)
                {
                    sw.WriteLine("[{0}]", section.Key);
                    foreach (KeyValuePair<string, Key> key in section.Value.Keys)
                    {
                        sw.WriteLine("{0}={1}", key.Key, key.Value.Value);
                    }
                    sw.WriteLine();
                }
            }
        }

        public static void ParseConfig(string path)
        {
            ConfigFilePath = path;
            string[] lines = File.ReadAllLines(path);

            Section currentSection = new Section("DEFINE");
            configuration.Add("DEFINE", currentSection);

            foreach (string configLine in lines)
            {
                string line = configLine.Trim();

                if (line.StartsWith(";")) continue;

                Match sectionMatch = patternSection.Match(line);

                if (sectionMatch.Success)
                {
                    string sec = sectionMatch.Groups["section"].Value.Trim();

                    if (!configuration.ContainsKey(sec)) configuration.Add(sec, new Section(sec));
                    currentSection = configuration[sec];
                    continue;
                }

                Match keyValMatch = patternKeyVal.Match(line);

                if (!keyValMatch.Success) continue;
                string key = keyValMatch.Groups["key"].Value.Trim();
                string value = keyValMatch.Groups["value"].Value.Trim();

                if (!currentSection.Keys.ContainsKey(key)) currentSection.Keys.Add(key, new Key(key, value));
                else
                {
                    Key k = currentSection.Keys[key];
                    k.Value = value;
                    currentSection.Keys[key] = k;
                }
            }
            LoadValues();
        }

        private static void LoadValues()
        {
            Executable = GetValue("Launch", "Executable");
            Assert(() => Executable.IsNullOrWhiteSpace() || !File.Exists(Executable),
                "Could not locate the executable to launch. Make sure the launching executable is specified!");

            ExecArgs = GetValue("Launch", "Arguments");

            WorkingDirectory = GetValue("Launch", "WorkingDirectory");

            GameExecutableName = GetValue("Launch", "GameExecutableName");

            string pauseOnError = GetValue("UULauncher", "PauseOnError");
            bool bPauseOnError = true;
            if (pauseOnError.IsNullOrWhiteSpace() || !bool.TryParse(pauseOnError, out bPauseOnError)) Logger.Log(LogLevel.Warning, $"Value PauseOnError could not be parsed! Setting to {bPauseOnError}...");
            PauseOnError = bPauseOnError;

            string continueWithErrors = GetValue("UULauncher", "ContinueWithErrors");
            bool bContinueWithErrors = false;
            if (continueWithErrors.IsNullOrWhiteSpace() || !bool.TryParse(continueWithErrors, out bContinueWithErrors))
                Logger.Log(LogLevel.Warning,
                    $"Value ContinueWithErrors could not be parsed! Setting to {bContinueWithErrors}...");
            ContinueWithErrors = bContinueWithErrors;

            string hideWhileGameRuns = GetValue("UULauncher", "HideWhileGameRuns");
            bool bHideWhileGameRuns = false;
            if (hideWhileGameRuns.IsNullOrWhiteSpace() || !bool.TryParse(hideWhileGameRuns, out bHideWhileGameRuns))
                Logger.Log(LogLevel.Warning,
                    $"Value HideWhileGameRuns could not be parsed! Setting to {bHideWhileGameRuns}...");
            HideWhileGameRuns = bHideWhileGameRuns;
        }

        public static void CreateDefaultConfiguration()
        {
            using (
                Stream s = Assembly.GetExecutingAssembly()
                                   .GetManifestResourceStream(typeof (Configuration), "UULauncher.ini"))
            {
                using (FileStream fs = File.Open(DEFAULT_CONFIG_NAME, FileMode.Create))
                {
                    byte[] buffer = new byte[1024];
                    int len;
                    while ((len = s.Read(buffer, 0, 1024)) > 0) fs.Write(buffer, 0, len);
                }
            }
        }

        public struct Section
        {
            public List<string> Comments;
            public Dictionary<string, Key> Keys;
            public string Name;

            public Section(string name)
            {
                Name = name;
                Keys = new Dictionary<string, Key>();
                Comments = new List<string>();
            }
        }

        public struct Key
        {
            private string _rawValue;
            private string _value;
            public List<string> Comments;
            public string Name;

            public Key(string name, string rawValue)
            {
                Name = name;
                Comments = new List<string>();
                _rawValue = rawValue;
                _value = ParseValue(rawValue);
            }

            public string Value
            {
                get { return _value; }
                set
                {
                    value = Value.Trim();
                    _rawValue = value;
                    _value = ParseValue(_rawValue);
                }
            }
        }
    }
}