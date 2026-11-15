using System;
using System.Collections.Generic;
using System.IO;
using NetworkSharing.Logging;

namespace NetworkSharing
{
    public class AppSettings
    {
        public bool IsDarkMode { get; set; }
        public bool AutoReconnectOnStartup { get; set; }
        public List<string> SavedHosts { get; set; }
        public string LastConfiguredUser { get; set; }

        public AppSettings()
        {
            IsDarkMode = false; // Default clean modern Windows 11 light mode
            AutoReconnectOnStartup = true;
            SavedHosts = new List<string>();
            LastConfiguredUser = "ShareUser";
        }
    }

    public static class AppConfig
    {
        private static readonly string ConfigPath;
        public static AppSettings Settings { get; private set; }

        static AppConfig()
        {
            try
            {
                string appData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NetworkSharing");
                if (!Directory.Exists(appData))
                {
                    Directory.CreateDirectory(appData);
                }
                ConfigPath = Path.Combine(appData, "config.ini");
                Settings = new AppSettings();
                Load();
            }
            catch (Exception ex)
            {
                Logger.Error("Error initializing AppConfig", ex);
                Settings = new AppSettings();
            }
        }

        public static void Save()
        {
            try
            {
                if (string.IsNullOrEmpty(ConfigPath)) return;

                List<string> lines = new List<string>();
                lines.Add("IsDarkMode=" + Settings.IsDarkMode);
                lines.Add("AutoReconnectOnStartup=" + Settings.AutoReconnectOnStartup);
                lines.Add("LastConfiguredUser=" + Settings.LastConfiguredUser);
                lines.Add("SavedHosts=" + string.Join(",", Settings.SavedHosts.ToArray()));

                File.WriteAllLines(ConfigPath, lines.ToArray());
            }
            catch (Exception ex)
            {
                Logger.Warning("Failed to save config: " + ex.Message);
            }
        }

        private static void Load()
        {
            if (string.IsNullOrEmpty(ConfigPath) || !File.Exists(ConfigPath)) return;
            try
            {
                string[] lines = File.ReadAllLines(ConfigPath);
                foreach (string line in lines)
                {
                    if (string.IsNullOrWhiteSpace(line) || !line.Contains("=")) continue;
                    string[] parts = line.Split(new[] { '=' }, 2);
                    string key = parts[0].Trim();
                    string val = parts[1].Trim();

                    if (key.Equals("IsDarkMode", StringComparison.OrdinalIgnoreCase))
                    {
                        bool b;
                        if (bool.TryParse(val, out b)) Settings.IsDarkMode = b;
                    }
                    else if (key.Equals("AutoReconnectOnStartup", StringComparison.OrdinalIgnoreCase))
                    {
                        bool b;
                        if (bool.TryParse(val, out b)) Settings.AutoReconnectOnStartup = b;
                    }
                    else if (key.Equals("LastConfiguredUser", StringComparison.OrdinalIgnoreCase))
                    {
                        Settings.LastConfiguredUser = val;
                    }
                    else if (key.Equals("SavedHosts", StringComparison.OrdinalIgnoreCase))
                    {
                        if (!string.IsNullOrEmpty(val))
                        {
                            Settings.SavedHosts = new List<string>(val.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries));
                        }
                    }
                }
            }
            catch { }
        }
    }
}
