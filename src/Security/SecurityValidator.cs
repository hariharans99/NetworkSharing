using System;
using System.IO;
using System.Net;
using System.Text.RegularExpressions;

namespace NetworkSharing.Security
{
    public class ValidationResult
    {
        public bool IsValid { get; set; }
        public bool IsWarning { get; set; }
        public string Message { get; set; }

        public static ValidationResult Success()
        {
            return new ValidationResult { IsValid = true, IsWarning = false, Message = string.Empty };
        }

        public static ValidationResult Warning(string message)
        {
            return new ValidationResult { IsValid = true, IsWarning = true, Message = message };
        }

        public static ValidationResult Error(string message)
        {
            return new ValidationResult { IsValid = false, IsWarning = false, Message = message };
        }
    }

    public static class SecurityValidator
    {
        private static readonly string[] SystemPaths = new string[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.Windows),
            Environment.GetFolderPath(Environment.SpecialFolder.System),
            Environment.GetFolderPath(Environment.SpecialFolder.SystemX86),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            Environment.GetFolderPath(Environment.SpecialFolder.CommonProgramFiles),
            Environment.GetFolderPath(Environment.SpecialFolder.CommonProgramFilesX86)
        };

        private static readonly string[] BlockedFolderNames = new string[]
        {
            "System Volume Information",
            "$Recycle.Bin",
            "RECYCLER",
            "Windows.old",
            "Recovery",
            "Boot"
        };

        public static ValidationResult ValidateShareName(string shareName)
        {
            if (string.IsNullOrWhiteSpace(shareName))
                return ValidationResult.Error("Share name cannot be empty.");

            shareName = shareName.Trim();
            if (shareName.Length > 80)
                return ValidationResult.Error("Share name cannot exceed 80 characters.");

            if (Regex.IsMatch(shareName, @"[\\/:*?""<>|]"))
                return ValidationResult.Error("Share name contains invalid characters (\\ / : * ? \" < > |).");

            // Prevent administrative share syntax (ending with $)
            if (shareName.EndsWith("$"))
                return ValidationResult.Error("Creating hidden or administrative shares (ending with '$') is restricted for security.");

            string[] reserved = { "CON", "PRN", "AUX", "NUL", "COM1", "COM2", "COM3", "LPT1", "LPT2", "LPT3", "IPC$", "ADMIN$" };
            foreach (string r in reserved)
            {
                if (shareName.Equals(r, StringComparison.OrdinalIgnoreCase))
                    return ValidationResult.Error("Share name cannot be a reserved Windows device or system share name.");
            }

            return ValidationResult.Success();
        }

        public static ValidationResult ValidatePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return ValidationResult.Error("Path cannot be empty.");

            path = path.Trim();

            // Detect path traversal sequence
            if (path.Contains(".."))
                return ValidationResult.Error("Directory traversal sequences ('..') are strictly prohibited.");

            if (!Path.IsPathRooted(path))
                return ValidationResult.Error("Path must be an absolute path with drive letter.");

            if (!Directory.Exists(path))
                return ValidationResult.Error("Selected folder or drive path does not exist.");

            string fullPath;
            try
            {
                fullPath = Path.GetFullPath(path).TrimEnd('\\');
            }
            catch (Exception ex)
            {
                return ValidationResult.Error("Invalid path format: " + ex.Message);
            }

            DirectoryInfo dInfo = new DirectoryInfo(fullPath);

            // Check for Reparse Points (Symlinks / Directory Junctions)
            if (IsReparsePoint(fullPath))
            {
                return ValidationResult.Error("Reparse points, directory junctions, and symlinks cannot be shared directly to prevent link-traversal vulnerabilities.");
            }

            string systemDrive = Path.GetPathRoot(Environment.SystemDirectory).TrimEnd('\\');

            // C: root check
            if (fullPath.Equals(systemDrive, StringComparison.OrdinalIgnoreCase))
            {
                return ValidationResult.Warning(
                    "Security Notice: You are sharing the root of the Windows Operating System drive (" + systemDrive + @"\). " +
                    "To protect system stability, Read-Only permission is automatically recommended.");
            }

            // Blocked folder names (System Volume Information, $Recycle.Bin, etc.)
            string folderName = dInfo.Name;
            foreach (string b in BlockedFolderNames)
            {
                if (folderName.Equals(b, StringComparison.OrdinalIgnoreCase) || fullPath.IndexOf("\\" + b, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return ValidationResult.Error("Access Denied: Sharing protected operating system directory '" + b + "' is blocked.");
                }
            }

            // System directories check
            foreach (string sysPath in SystemPaths)
            {
                if (string.IsNullOrEmpty(sysPath)) continue;
                string cleanSys = Path.GetFullPath(sysPath).TrimEnd('\\');
                if (fullPath.Equals(cleanSys, StringComparison.OrdinalIgnoreCase) ||
                    fullPath.StartsWith(cleanSys + "\\", StringComparison.OrdinalIgnoreCase))
                {
                    return ValidationResult.Error(
                        "Access Denied: Sharing core Windows system folders (" + cleanSys + ") is blocked for operating system safety.");
                }
            }

            return ValidationResult.Success();
        }

        public static ValidationResult ValidateUsername(string username)
        {
            if (string.IsNullOrWhiteSpace(username))
                return ValidationResult.Error("Username cannot be empty.");

            username = username.Trim();
            if (username.Length > 20)
                return ValidationResult.Error("Windows username cannot exceed 20 characters.");

            if (Regex.IsMatch(username, @"[""/\\\[\]:|<>+=;,?*@]"))
                return ValidationResult.Error("Username contains invalid characters (no quotes, slashes, brackets, colon, @).");

            if (username.Equals("Administrator", StringComparison.OrdinalIgnoreCase) ||
                username.Equals("Guest", StringComparison.OrdinalIgnoreCase) ||
                username.Equals("SYSTEM", StringComparison.OrdinalIgnoreCase) ||
                username.Equals("DefaultAccount", StringComparison.OrdinalIgnoreCase))
            {
                return ValidationResult.Error("Cannot use built-in Windows system account name.");
            }

            return ValidationResult.Success();
        }

        public static ValidationResult ValidatePassword(string password)
        {
            if (string.IsNullOrEmpty(password))
                return ValidationResult.Error("Password cannot be empty.");

            if (password.Length < 8)
                return ValidationResult.Error("Password must be at least 8 characters long.");

            bool hasUpper = Regex.IsMatch(password, "[A-Z]");
            bool hasLower = Regex.IsMatch(password, "[a-z]");
            bool hasDigit = Regex.IsMatch(password, "[0-9]");

            if (!hasUpper || !hasLower || !hasDigit)
            {
                return ValidationResult.Warning("Password recommendation: Include a combination of uppercase letters, lowercase letters, numbers, and symbols.");
            }

            return ValidationResult.Success();
        }

        public static ValidationResult ValidateHost(string host)
        {
            if (string.IsNullOrWhiteSpace(host))
                return ValidationResult.Error("Host IP address or hostname cannot be empty.");

            host = host.Trim();
            IPAddress ip;
            if (IPAddress.TryParse(host, out ip))
            {
                return ValidationResult.Success();
            }

            // Validate hostname characters
            if (Regex.IsMatch(host, @"^[a-zA-Z0-9\-\.]+$"))
            {
                return ValidationResult.Success();
            }

            return ValidationResult.Error("Invalid hostname or IP address format.");
        }

        public static bool IsReparsePoint(string path)
        {
            try
            {
                if (string.IsNullOrEmpty(path) || (!Directory.Exists(path) && !File.Exists(path)))
                    return false;

                FileAttributes attr = File.GetAttributes(path);
                return (attr & FileAttributes.ReparsePoint) == FileAttributes.ReparsePoint;
            }
            catch
            {
                return false;
            }
        }
    }
}
