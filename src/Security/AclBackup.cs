using System;
using System.Collections.Generic;
using System.IO;
using System.Security.AccessControl;
using NetworkSharing.Logging;

namespace NetworkSharing.Security
{
    public class AclBackupEntry
    {
        public string TargetPath { get; set; }
        public string OriginalSddl { get; set; }
        public DateTime Timestamp { get; set; }
    }

    public static class AclBackup
    {
        private static readonly string BackupFilePath;
        private static readonly Dictionary<string, string> _inMemoryBackups = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        static AclBackup()
        {
            try
            {
                string appData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NetworkSharing");
                if (!Directory.Exists(appData))
                {
                    Directory.CreateDirectory(appData);
                }
                BackupFilePath = Path.Combine(appData, "acl_backups.txt");
                LoadPersistedBackups();
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to initialize AclBackup storage", ex);
            }
        }

        public static bool BackupAcl(string path)
        {
            try
            {
                if (!Directory.Exists(path)) return false;

                string fullPath = Path.GetFullPath(path);
                if (_inMemoryBackups.ContainsKey(fullPath))
                {
                    // Already have the original backup
                    return true;
                }

                DirectoryInfo dInfo = new DirectoryInfo(fullPath);
                DirectorySecurity security = dInfo.GetAccessControl(AccessControlSections.Access);
                string sddl = security.GetSecurityDescriptorSddlForm(AccessControlSections.Access);

                _inMemoryBackups[fullPath] = sddl;
                PersistBackup(fullPath, sddl);

                Logger.Info("Preserved original NTFS security descriptor for: " + fullPath);
                return true;
            }
            catch (Exception ex)
            {
                Logger.Warning("Could not backup original ACL for " + path + ": " + ex.Message);
                return false;
            }
        }

        public static bool RestoreAcl(string path)
        {
            try
            {
                if (!Directory.Exists(path)) return false;

                string fullPath = Path.GetFullPath(path);
                if (!_inMemoryBackups.ContainsKey(fullPath))
                {
                    Logger.Warning("No ACL backup found for path: " + fullPath);
                    return false;
                }

                string sddl = _inMemoryBackups[fullPath];
                DirectorySecurity security = new DirectorySecurity();
                security.SetSecurityDescriptorSddlForm(sddl, AccessControlSections.Access);

                DirectoryInfo dInfo = new DirectoryInfo(fullPath);
                dInfo.SetAccessControl(security);

                Logger.Info("Restored original NTFS security descriptor for: " + fullPath);
                _inMemoryBackups.Remove(fullPath);
                SaveAllBackups();
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to restore ACL for " + path, ex);
                return false;
            }
        }

        private static void PersistBackup(string path, string sddl)
        {
            if (string.IsNullOrEmpty(BackupFilePath)) return;
            try
            {
                string line = path + "|||" + sddl + Environment.NewLine;
                File.AppendAllText(BackupFilePath, line);
            }
            catch { }
        }

        private static void SaveAllBackups()
        {
            if (string.IsNullOrEmpty(BackupFilePath)) return;
            try
            {
                List<string> lines = new List<string>();
                foreach (var kvp in _inMemoryBackups)
                {
                    lines.Add(kvp.Key + "|||" + kvp.Value);
                }
                File.WriteAllLines(BackupFilePath, lines.ToArray());
            }
            catch { }
        }

        private static void LoadPersistedBackups()
        {
            if (string.IsNullOrEmpty(BackupFilePath) || !File.Exists(BackupFilePath)) return;
            try
            {
                string[] lines = File.ReadAllLines(BackupFilePath);
                foreach (string line in lines)
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    string[] parts = line.Split(new string[] { "|||" }, StringSplitOptions.None);
                    if (parts.Length == 2)
                    {
                        _inMemoryBackups[parts[0]] = parts[1];
                    }
                }
            }
            catch { }
        }
    }
}
