using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using NetworkSharing.Logging;

namespace NetworkSharing.Core
{
    public class MappedDriveInfo
    {
        public string DriveLetter { get; set; }  // e.g. "Z:"
        public string RemotePath { get; set; }   // e.g. "\\192.168.0.128\Drive_D"
        public string Status { get; set; }       // "Connected", "Unavailable"
        public bool IsAppCreated { get; set; }
    }

    public class MappingResult
    {
        public bool Success { get; set; }
        public int ErrorCode { get; set; }
        public string Message { get; set; }
        public string RecoveryTip { get; set; }

        public static MappingResult Ok(string message)
        {
            return new MappingResult { Success = true, ErrorCode = 0, Message = message, RecoveryTip = string.Empty };
        }

        public static MappingResult Fail(int errorCode, string message, string tip)
        {
            return new MappingResult { Success = false, ErrorCode = errorCode, Message = message, RecoveryTip = tip };
        }
    }

    public static class DriveMapper
    {
        private const int RESOURCETYPE_DISK = 0x00000001;
        private const int CONNECT_UPDATE_PROFILE = 0x00000001;

        // Windows Win32 Error Codes
        public const int NO_ERROR = 0;
        public const int ERROR_ACCESS_DENIED = 5;
        public const int ERROR_BAD_NETPATH = 53;
        public const int ERROR_BAD_NET_NAME = 67;
        public const int ERROR_ALREADY_ASSIGNED = 85;
        public const int ERROR_INVALID_PASSWORD = 86;
        public const int ERROR_SESSION_CREDENTIAL_CONFLICT = 1219;
        public const int ERROR_LOGON_FAILURE = 1326;

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct NETRESOURCE
        {
            public int dwScope;
            public int dwType;
            public int dwDisplayType;
            public int dwUsage;
            public string lpLocalName;
            public string lpRemoteName;
            public string lpComment;
            public string lpProvider;
        }

        [DllImport("mpr.dll", CharSet = CharSet.Unicode)]
        private static extern int WNetAddConnection2(ref NETRESOURCE lpNetResource, string lpPassword, string lpUserName, int dwFlags);

        [DllImport("mpr.dll", CharSet = CharSet.Unicode)]
        private static extern int WNetCancelConnection2(string lpName, int dwFlags, bool fForce);

        public static List<string> GetAvailableDriveLetters()
        {
            List<string> freeLetters = new List<string>();
            HashSet<char> usedLetters = new HashSet<char>();

            DriveInfo[] drives = DriveInfo.GetDrives();
            foreach (DriveInfo d in drives)
            {
                if (d.Name.Length > 0)
                {
                    usedLetters.Add(char.ToUpperInvariant(d.Name[0]));
                }
            }

            // Start from Z: down to E:
            for (char c = 'Z'; c >= 'E'; c--)
            {
                if (!usedLetters.Contains(c))
                {
                    freeLetters.Add(c + ":");
                }
            }

            return freeLetters;
        }

        public static bool IsDriveLetterInUse(string driveLetter)
        {
            if (string.IsNullOrEmpty(driveLetter)) return false;
            char target = char.ToUpperInvariant(driveLetter.Trim()[0]);
            foreach (DriveInfo d in DriveInfo.GetDrives())
            {
                if (char.ToUpperInvariant(d.Name[0]) == target)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Maps a remote SMB share to a local drive letter using memory-secure Win32 WNetAddConnection2W.
        /// Passwords are never passed via command-line arguments.
        /// </summary>
        public static MappingResult MapDrive(string driveLetter, string remotePath, string username = null, string password = null, bool persistent = true)
        {
            try
            {
                Logger.Info(string.Format("Mapping {0} -> {1} (User: {2}, Persistent: {3})...", 
                    driveLetter, remotePath, string.IsNullOrEmpty(username) ? "Current" : username, persistent));

                driveLetter = driveLetter.Trim().ToUpperInvariant();
                if (!driveLetter.EndsWith(":")) driveLetter += ":";

                // Check collision
                if (IsDriveLetterInUse(driveLetter))
                {
                    Logger.Warning("Drive letter " + driveLetter + " is already occupied.");
                    UnmapDrive(driveLetter);
                }

                NETRESOURCE nr = new NETRESOURCE
                {
                    dwType = RESOURCETYPE_DISK,
                    lpLocalName = driveLetter,
                    lpRemoteName = remotePath
                };

                int flags = persistent ? CONNECT_UPDATE_PROFILE : 0;
                int result = WNetAddConnection2(ref nr, password, username, flags);

                if (result == NO_ERROR)
                {
                    Logger.Security(string.Format("Successfully mapped {0} to {1}", driveLetter, remotePath));
                    return MappingResult.Ok("Drive successfully mapped to " + driveLetter);
                }

                // Explicit Error 1219 Handling: Session Credential Conflict
                if (result == ERROR_SESSION_CREDENTIAL_CONFLICT)
                {
                    Logger.Warning("Windows Error 1219 (Multiple credentials for same server). Attempting session reset...");
                    string serverRoot = ExtractServerRoot(remotePath);
                    if (!string.IsNullOrEmpty(serverRoot))
                    {
                        WNetCancelConnection2(serverRoot, 0, true);
                    }

                    // Retry once after server session disconnect
                    result = WNetAddConnection2(ref nr, password, username, flags);
                    if (result == NO_ERROR)
                    {
                        Logger.Security(string.Format("Successfully mapped {0} to {1} after resolving credential conflict.", driveLetter, remotePath));
                        return MappingResult.Ok("Drive mapped successfully after resolving previous credential session.");
                    }

                    return MappingResult.Fail(1219,
                        "Windows error 1219: An existing connection to this server exists under different credentials.",
                        "Disconnect existing connections to this server in File Explorer or click 'Disconnect All' before reconnecting.");
                }

                if (result == ERROR_LOGON_FAILURE || result == ERROR_INVALID_PASSWORD)
                {
                    return MappingResult.Fail(result, "Authentication failed: Unknown username or incorrect password.", "Verify the sharing username and password configured on the host PC.");
                }

                if (result == ERROR_BAD_NETPATH || result == ERROR_BAD_NET_NAME)
                {
                    return MappingResult.Fail(result, "Network path not found or share does not exist.", "Verify the host IP, computer name, and that the share is active on the host.");
                }

                if (result == ERROR_ACCESS_DENIED)
                {
                    return MappingResult.Fail(result, "Access Denied by host.", "The host PC refused connection. Ensure permissions on the share grant access to this user.");
                }

                return MappingResult.Fail(result, "Windows network error: " + result, "Check network connection and firewall settings.");
            }
            catch (Exception ex)
            {
                Logger.Error("Exception mapping drive " + driveLetter, ex);
                return MappingResult.Fail(-1, "Internal error: " + ex.Message, "Check application logs.");
            }
        }

        public static bool UnmapDrive(string driveLetter)
        {
            try
            {
                driveLetter = driveLetter.Trim().ToUpperInvariant();
                if (!driveLetter.EndsWith(":")) driveLetter += ":";

                WNetCancelConnection2(driveLetter, CONNECT_UPDATE_PROFILE, true);
                Logger.Info("Unmapped drive: " + driveLetter);
                return true;
            }
            catch (Exception ex)
            {
                Logger.Warning("Error unmapping drive " + driveLetter + ": " + ex.Message);
                return false;
            }
        }

        public static bool DisconnectNetworkDrive(string driveLetter)
        {
            return UnmapDrive(driveLetter);
        }

        public static bool DisconnectAll()
        {
            try
            {
                Logger.Info("Disconnecting all mapped network drives...");
                List<MappedDriveInfo> list = GetMappedDrives();
                foreach (var d in list)
                {
                    UnmapDrive(d.DriveLetter);
                }
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error("Error disconnecting all drives", ex);
                return false;
            }
        }

        public static List<MappedDriveInfo> GetMappedDrives()
        {
            List<MappedDriveInfo> result = new List<MappedDriveInfo>();
            try
            {
                DriveInfo[] drives = DriveInfo.GetDrives();
                foreach (DriveInfo d in drives)
                {
                    if (d.DriveType == DriveType.Network)
                    {
                        string letter = d.Name.TrimEnd('\\');
                        string remote = GetRemotePathForDrive(letter);
                        result.Add(new MappedDriveInfo
                        {
                            DriveLetter = letter,
                            RemotePath = !string.IsNullOrEmpty(remote) ? remote : @"\\RemoteServer\Share",
                            Status = d.IsReady ? "Connected" : "Unavailable",
                            IsAppCreated = true
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to list mapped drives", ex);
            }

            return result;
        }

        public static bool CreateDesktopShortcut(string name, string targetPath)
        {
            try
            {
                string desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                string cleanName = Path.GetFileName(targetPath.Trim('\\'));
                if (string.IsNullOrEmpty(cleanName)) cleanName = name;

                string filePath = Path.Combine(desktop, string.Format("{0}.url", cleanName));

                StringBuilder sb = new StringBuilder();
                sb.AppendLine("[InternetShortcut]");
                sb.AppendLine("URL=file:////" + targetPath.TrimStart('\\'));
                sb.AppendLine("IconIndex=0");
                sb.AppendLine(@"IconFile=" + Environment.GetFolderPath(Environment.SpecialFolder.System) + @"\shell32.dll");

                File.WriteAllText(filePath, sb.ToString());
                Logger.Info("Created Desktop shortcut: " + filePath);
                return true;
            }
            catch (Exception ex)
            {
                Logger.Warning("Could not create Desktop shortcut: " + ex.Message);
                return false;
            }
        }

        public static void OpenInExplorer(string path)
        {
            try
            {
                Process.Start("explorer.exe", path);
                Logger.Info("Opened in File Explorer: " + path);
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to open Explorer for: " + path, ex);
            }
        }

        private static string ExtractServerRoot(string remotePath)
        {
            if (string.IsNullOrEmpty(remotePath) || !remotePath.StartsWith(@"\\")) return null;
            int nextSlash = remotePath.IndexOf('\\', 2);
            if (nextSlash > 2)
            {
                return remotePath.Substring(0, nextSlash);
            }
            return remotePath;
        }

        private static string GetRemotePathForDrive(string driveLetter)
        {
            try
            {
                // Query remote path via WNetGetConnection
                StringBuilder sb = new StringBuilder(512);
                int length = sb.Capacity;
                int res = WNetGetConnection(driveLetter, sb, ref length);
                if (res == 0)
                {
                    return sb.ToString();
                }
            }
            catch { }
            return string.Empty;
        }

        [DllImport("mpr.dll", CharSet = CharSet.Unicode)]
        private static extern int WNetGetConnection(string lpLocalName, StringBuilder lpRemoteName, ref int lpnLength);
    }
}
