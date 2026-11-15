using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using NetworkSharing.Logging;

namespace NetworkSharing.Core
{
    public class SharedFolderInfo
    {
        public string ShareName { get; set; }
        public string Path { get; set; }
        public string Description { get; set; }
        public SharePermissionType Permission { get; set; }
        public string GrantedUser { get; set; }
        public DateTime CreatedAt { get; set; }
        public bool CreatedByApp { get; set; }

        public string UncPath
        {
            get { return string.Format("\\\\{0}\\{1}", Environment.MachineName, ShareName); }
        }
    }

    public static class ShareManager
    {
        public static string LastError { get; set; }
        private static readonly string AppSharesRecordFile;
        private static readonly HashSet<string> _appManagedShares = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        static ShareManager()
        {
            try
            {
                string appData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NetworkSharing");
                if (!Directory.Exists(appData))
                {
                    Directory.CreateDirectory(appData);
                }
                AppSharesRecordFile = Path.Combine(appData, "app_shares.txt");
                LoadAppSharesRecord();
            }
            catch (Exception ex)
            {
                Logger.Error("Error initializing ShareManager storage", ex);
            }
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct SHARE_INFO_502
        {
            [MarshalAs(UnmanagedType.LPWStr)]
            public string shi502_netname;
            public uint shi502_type;
            [MarshalAs(UnmanagedType.LPWStr)]
            public string shi502_remark;
            public uint shi502_permissions;
            public uint shi502_max_uses;
            public uint shi502_current_uses;
            [MarshalAs(UnmanagedType.LPWStr)]
            public string shi502_path;
            [MarshalAs(UnmanagedType.LPWStr)]
            public string shi502_passwd;
            public uint shi502_reserved;
            public IntPtr shi502_security_descriptor;
        }

        [DllImport("Netapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern uint NetShareAdd(
            [MarshalAs(UnmanagedType.LPWStr)] string servername,
            uint level,
            ref SHARE_INFO_502 buf,
            out uint parm_err);

        private const uint STYPE_DISKTREE = 0;

        public static void EnsureServerServiceRunning()
        {
            try
            {
                RunHiddenCommand("sc.exe", "config LanmanServer start= auto");
                RunHiddenCommand("net.exe", "start LanmanServer");
            }
            catch { }
        }

        public static bool CreateShare(string shareName, string folderPath, string username, SharePermissionType permission, string description = "Shared via NetworkSharing.exe")
        {
            try
            {
                Logger.Info(string.Format("Creating SMB share '{0}' -> '{1}' for user '{2}'...", shareName, folderPath, username));

                if (!Directory.Exists(folderPath))
                {
                    LastError = "Target folder path does not exist: " + folderPath;
                    Logger.Error(LastError);
                    return false;
                }

                EnsureServerServiceRunning();

                // 1. Apply NTFS permissions (Best effort: share permissions guarantee network protection)
                if (!PermissionManager.ApplyPermissions(folderPath, username, permission))
                {
                    Logger.Warning("NTFS permissions produced a warning on " + folderPath + ", relying on SMB share-level security.");
                }

                // If share exists, delete it first to ensure clean state
                DeleteShare(shareName);

                string cleanPath = folderPath.Trim();
                if (cleanPath.Length > 3 && cleanPath.EndsWith("\\"))
                {
                    cleanPath = cleanPath.TrimEnd('\\');
                }

                string smbPermission = (permission == SharePermissionType.ReadWrite) ? "CHANGE" : "READ";

                // Format the share mapping argument safely:
                // Never put a closing quote immediately after a backslash (e.g. "D:\" escapes the quote in Windows cmdline!)
                string shareMapping;
                if (cleanPath.Contains(" "))
                {
                    string noSlash = cleanPath.TrimEnd('\\');
                    shareMapping = string.Format("\"{0}={1}\"", shareName, noSlash);
                }
                else
                {
                    // No spaces (such as D:\): do NOT enclose in quotes so trailing backslash does not escape
                    shareMapping = string.Format("{0}={1}", shareName, cleanPath);
                }

                string args = string.Format("share {0} /GRANT:\"{1}\",{2} /GRANT:Administrators,FULL /REMARK:\"{3}\"",
                    shareMapping, username, smbPermission, description);

                string cmdOutput;
                int exitCode = RunHiddenCommandWithDetails("net", args, out cmdOutput);
                if (exitCode == 0)
                {
                    _appManagedShares.Add(shareName);
                    SaveAppSharesRecord();
                    Logger.Security(string.Format("Successfully created SMB share '{0}' with {1} permissions.", shareName, smbPermission));
                    return true;
                }

                // Try PowerShell New-SmbShare fallback (built into modern Windows)
                try
                {
                    string psAccess = (permission == SharePermissionType.ReadWrite) ? "-ChangeAccess" : "-ReadAccess";
                    string psArgs = string.Format("-NoProfile -ExecutionPolicy Bypass -Command \"New-SmbShare -Name '{0}' -Path '{1}' {2} '{3}' -FullAccess 'Administrators' -Description '{4}' -Force\"",
                        shareName, cleanPath, psAccess, username, description.Replace("'", "''"));
                    string psOutput;
                    int psExit = RunHiddenCommandWithDetails("powershell.exe", psArgs, out psOutput);
                    if (psExit == 0)
                    {
                        _appManagedShares.Add(shareName);
                        SaveAppSharesRecord();
                        Logger.Security(string.Format("Successfully created SMB share '{0}' via PowerShell fallback.", shareName));
                        return true;
                    }
                    cmdOutput = string.Format("net share: {0} | PowerShell: {1}", cmdOutput, psOutput);
                }
                catch { }

                LastError = cmdOutput;
                Logger.Error(string.Format("Failed to create SMB share '{0}'. Details: {1}", shareName, cmdOutput));
                return false;
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
                Logger.Error("Exception while creating share " + shareName, ex);
                return false;
            }
        }

        [DllImport("Netapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern uint NetShareDel(
            [MarshalAs(UnmanagedType.LPWStr)] string servername,
            [MarshalAs(UnmanagedType.LPWStr)] string netname,
            int reserved);

        private const uint NERR_Success = 0;
        private const uint NERR_NetNameNotFound = 2311;

        public static bool DeleteShare(string shareName)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(shareName)) return false;

                // 1. Try native Win32 NetShareDel API first
                uint result = NetShareDel(null, shareName, 0);
                if (result == NERR_Success || result == NERR_NetNameNotFound)
                {
                    _appManagedShares.Remove(shareName);
                    SaveAppSharesRecord();
                    Logger.Info("Deleted SMB share via NetShareDel: " + shareName);
                    return true;
                }

                // 2. Fall back to net share CLI
                string args = string.Format("share \"{0}\" /delete /yes", shareName);
                int exitCode = RunHiddenCommand("net", args);

                if (exitCode == 0)
                {
                    _appManagedShares.Remove(shareName);
                    SaveAppSharesRecord();
                    Logger.Info("Deleted SMB share via CLI fallback: " + shareName);
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                Logger.Warning("Error deleting share " + shareName + ": " + ex.Message);
                return false;
            }
        }

        public static List<SharedFolderInfo> GetActiveShares()
        {
            List<SharedFolderInfo> shares = new List<SharedFolderInfo>();

            try
            {
                string output = RunCommandWithOutput("net", "share");
                string[] lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

                bool readingShares = false;
                foreach (string line in lines)
                {
                    if (line.StartsWith("---"))
                    {
                        readingShares = true;
                        continue;
                    }
                    if (line.StartsWith("The command completed")) break;

                    if (readingShares && !string.IsNullOrWhiteSpace(line))
                    {
                        // Standard format: ShareName  Path  Remark
                        string trimmed = line.Trim();
                        // Ignore administrative hidden shares ending with $ unless requested
                        if (trimmed.Contains("$")) continue;

                        string[] parts = trimmed.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length >= 2)
                        {
                            string sName = parts[0];
                            string sPath = parts[1];

                            SharedFolderInfo info = new SharedFolderInfo
                            {
                                ShareName = sName,
                                Path = sPath,
                                CreatedByApp = _appManagedShares.Contains(sName),
                                Permission = SharePermissionType.ReadWrite,
                                GrantedUser = "Configured User"
                            };
                            shares.Add(info);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to parse active shares", ex);
            }

            return shares;
        }

        public static List<string> GetRemoteShares(string remoteHost, string username = null, string password = null)
        {
            List<string> shareNames = new List<string>();
            try
            {
                string args = "view \\\\" + remoteHost;
                string output = RunCommandWithOutput("net", args);

                string[] lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                bool reading = false;
                foreach (string line in lines)
                {
                    if (line.StartsWith("---"))
                    {
                        reading = true;
                        continue;
                    }
                    if (line.StartsWith("The command completed")) break;

                    if (reading && !string.IsNullOrWhiteSpace(line))
                    {
                        // Look for "Disk" shares
                        if (line.IndexOf("Disk", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            string[] tokens = line.Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                            if (tokens.Length > 0 && !tokens[0].EndsWith("$"))
                            {
                                shareNames.Add(tokens[0]);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Warning("Remote share enumeration error: " + ex.Message);
            }
            return shareNames;
        }

        public static bool IsAppManaged(string shareName)
        {
            return _appManagedShares.Contains(shareName);
        }

        public static HashSet<string> GetAppManagedShares()
        {
            return new HashSet<string>(_appManagedShares, StringComparer.OrdinalIgnoreCase);
        }

        private static void LoadAppSharesRecord()
        {
            if (string.IsNullOrEmpty(AppSharesRecordFile) || !File.Exists(AppSharesRecordFile)) return;
            try
            {
                string[] lines = File.ReadAllLines(AppSharesRecordFile);
                foreach (string l in lines)
                {
                    if (!string.IsNullOrWhiteSpace(l)) _appManagedShares.Add(l.Trim());
                }
            }
            catch { }
        }

        private static void SaveAppSharesRecord()
        {
            if (string.IsNullOrEmpty(AppSharesRecordFile)) return;
            try
            {
                File.WriteAllLines(AppSharesRecordFile, new List<string>(_appManagedShares).ToArray());
            }
            catch { }
        }

        private static int RunHiddenCommandWithDetails(string exe, string args, out string output)
        {
            output = string.Empty;
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo(exe, args);
                psi.CreateNoWindow = true;
                psi.UseShellExecute = false;
                psi.RedirectStandardOutput = true;
                psi.RedirectStandardError = true;
                using (Process p = Process.Start(psi))
                {
                    string stdout = p.StandardOutput.ReadToEnd();
                    string stderr = p.StandardError.ReadToEnd();
                    p.WaitForExit(8000);
                    output = (stderr + " " + stdout).Trim();
                    return p.ExitCode;
                }
            }
            catch (Exception ex)
            {
                output = ex.Message;
                return -1;
            }
        }

        private static int RunHiddenCommand(string exe, string args)
        {
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo(exe, args);
                psi.CreateNoWindow = true;
                psi.UseShellExecute = false;
                psi.RedirectStandardOutput = true;
                psi.RedirectStandardError = true;
                using (Process p = Process.Start(psi))
                {
                    p.WaitForExit(8000);
                    return p.ExitCode;
                }
            }
            catch
            {
                return -1;
            }
        }

        private static string RunCommandWithOutput(string exe, string args)
        {
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo(exe, args);
                psi.CreateNoWindow = true;
                psi.UseShellExecute = false;
                psi.RedirectStandardOutput = true;
                psi.RedirectStandardError = true;
                using (Process p = Process.Start(psi))
                {
                    string output = p.StandardOutput.ReadToEnd() + p.StandardError.ReadToEnd();
                    p.WaitForExit(8000);
                    return output;
                }
            }
            catch
            {
                return string.Empty;
            }
        }
    }
}
