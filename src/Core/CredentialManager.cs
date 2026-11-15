using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using NetworkSharing.Logging;

namespace NetworkSharing.Core
{
    public class StoredCredential
    {
        public string Target { get; set; }
        public string UserName { get; set; }
        public string Password { get; set; }
    }

    /// <summary>
    /// Dual-layer enterprise credential vault.
    /// Layer 1: Native Windows Credential Manager (Advapi32 CredWriteW/CredReadW/CredDeleteW).
    /// Layer 2: Windows DPAPI encrypted vault (ProtectedData with CurrentUser hardware key) for non-interactive or restricted sessions.
    /// </summary>
    public static class CredentialManager
    {
        private const int CRED_TYPE_GENERIC = 1;
        private const int CRED_PERSIST_SESSION = 1;
        private const int CRED_PERSIST_LOCAL_MACHINE = 2;
        private const int CRED_PERSIST_ENTERPRISE = 3;

        private static readonly string VaultFilePath;
        private static readonly object _vaultLock = new object();

        static CredentialManager()
        {
            try
            {
                string appData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NetworkSharing");
                if (!Directory.Exists(appData))
                {
                    Directory.CreateDirectory(appData);
                }
                VaultFilePath = Path.Combine(appData, "credentials.vault");
            }
            catch (Exception ex)
            {
                Logger.Error("Failed initializing CredentialManager storage", ex);
            }
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct CREDENTIAL
        {
            public int Flags;
            public int Type;
            public string TargetName;
            public string Comment;
            public System.Runtime.InteropServices.ComTypes.FILETIME LastWritten;
            public int CredentialBlobSize;
            public IntPtr CredentialBlob;
            public int Persist;
            public int AttributeCount;
            public IntPtr Attributes;
            public string TargetAlias;
            public string UserName;
        }

        [DllImport("Advapi32.dll", SetLastError = true, EntryPoint = "CredWriteW", CharSet = CharSet.Unicode)]
        private static extern bool CredWrite([In] ref CREDENTIAL userCredential, [In] uint flags);

        [DllImport("Advapi32.dll", SetLastError = true, EntryPoint = "CredReadW", CharSet = CharSet.Unicode)]
        private static extern bool CredRead(string target, int type, int reservedFlag, out IntPtr credentialPtr);

        [DllImport("Advapi32.dll", SetLastError = true, EntryPoint = "CredDeleteW", CharSet = CharSet.Unicode)]
        private static extern bool CredDelete(string target, int type, int flags);

        [DllImport("Advapi32.dll", SetLastError = true, EntryPoint = "CredFree")]
        private static extern void CredFree([In] IntPtr cred);

        public static bool SaveCredential(string target, string userName, string password)
        {
            if (string.IsNullOrEmpty(target) || string.IsNullOrEmpty(userName)) return false;

            // 1. Try Windows Credential Manager (Native Advapi32)
            try
            {
                byte[] passBytes = Encoding.Unicode.GetBytes(password ?? string.Empty);
                IntPtr passPtr = Marshal.AllocHGlobal(passBytes.Length);
                Marshal.Copy(passBytes, 0, passPtr, passBytes.Length);

                CREDENTIAL cred = new CREDENTIAL
                {
                    Type = CRED_TYPE_GENERIC,
                    TargetName = target,
                    UserName = userName,
                    CredentialBlob = passPtr,
                    CredentialBlobSize = passBytes.Length,
                    Persist = CRED_PERSIST_ENTERPRISE,
                    Comment = "Stored securely by NetworkSharing.exe"
                };

                bool success = CredWrite(ref cred, 0);
                if (!success)
                {
                    // Retry with session persist if enterprise persist restricted
                    cred.Persist = CRED_PERSIST_SESSION;
                    success = CredWrite(ref cred, 0);
                }

                Marshal.FreeHGlobal(passPtr);

                if (success)
                {
                    Logger.Security("Saved network credentials in Windows Credential Manager for target: " + target);
                    return true;
                }
            }
            catch (Exception ex)
            {
                Logger.Warning("Native CredWrite note: " + ex.Message + ". Using DPAPI encrypted vault...");
            }

            // 2. Fallback to Windows DPAPI Encrypted Vault (ProtectedData with CurrentUser scope)
            return SaveToDpapiVault(target, userName, password);
        }

        public static StoredCredential GetCredential(string target)
        {
            if (string.IsNullOrEmpty(target)) return null;

            // 1. Try Windows Credential Manager
            try
            {
                IntPtr credPtr;
                if (CredRead(target, CRED_TYPE_GENERIC, 0, out credPtr))
                {
                    try
                    {
                        CREDENTIAL cred = (CREDENTIAL)Marshal.PtrToStructure(credPtr, typeof(CREDENTIAL));
                        string pass = string.Empty;
                        if (cred.CredentialBlobSize > 0 && cred.CredentialBlob != IntPtr.Zero)
                        {
                            byte[] passBytes = new byte[cred.CredentialBlobSize];
                            Marshal.Copy(cred.CredentialBlob, passBytes, 0, cred.CredentialBlobSize);
                            pass = Encoding.Unicode.GetString(passBytes);
                        }

                        return new StoredCredential
                        {
                            Target = cred.TargetName,
                            UserName = cred.UserName,
                            Password = pass
                        };
                    }
                    finally
                    {
                        CredFree(credPtr);
                    }
                }
            }
            catch { }

            // 2. Try DPAPI Vault
            return ReadFromDpapiVault(target);
        }

        public static bool DeleteCredential(string target)
        {
            if (string.IsNullOrEmpty(target)) return false;

            bool winCredDeleted = false;
            try
            {
                winCredDeleted = CredDelete(target, CRED_TYPE_GENERIC, 0);
            }
            catch { }

            bool dpapiDeleted = DeleteFromDpapiVault(target);
            Logger.Info("Deleted stored credentials for target: " + target);
            return winCredDeleted || dpapiDeleted;
        }

        /// <summary>
        /// Searches and purges all stored credentials matching a target IP, hostname,
        /// or UNC network share from both Windows Credential Manager and the DPAPI vault.
        /// Resolves issues when credentials change on the host PC.
        /// </summary>
        public static int PurgeTargetCredentials(string targetHostOrIp)
        {
            if (string.IsNullOrWhiteSpace(targetHostOrIp)) return 0;

            int deletedCount = 0;
            string clean = targetHostOrIp.Trim().TrimStart('\\');
            int slash = clean.IndexOf('\\');
            if (slash > 0) clean = clean.Substring(0, slash);

            HashSet<string> targetsToTry = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                targetHostOrIp.Trim(),
                clean,
                @"\\" + clean,
                clean + @"\"
            };

            try
            {
                string resolved = NetworkManager.ResolveHostname(clean);
                if (!string.IsNullOrEmpty(resolved) && !targetsToTry.Contains(resolved))
                {
                    targetsToTry.Add(resolved);
                    targetsToTry.Add(@"\\" + resolved);
                }
            }
            catch { }

            foreach (string t in targetsToTry)
            {
                try
                {
                    // Generic credential (Type 1)
                    if (CredDelete(t, CRED_TYPE_GENERIC, 0))
                    {
                        deletedCount++;
                        Logger.Security("Purged generic credential for: " + t);
                    }
                }
                catch { }

                try
                {
                    // Domain password (Type 2)
                    if (CredDelete(t, 2, 0))
                    {
                        deletedCount++;
                        Logger.Security("Purged domain credential for: " + t);
                    }
                }
                catch { }

                // Also purge from DPAPI vault
                if (DeleteFromDpapiVault(t))
                {
                    deletedCount++;
                }
            }

            Logger.Info(string.Format("Purged {0} cached credential(s) for host {1}.", deletedCount, clean));
            return deletedCount;
        }

        /// <summary>
        /// Clears all credentials stored by NetworkSharing in the DPAPI vault and Windows Credential Manager.
        /// </summary>
        public static int PurgeAllNetworkCredentials()
        {
            int count = 0;
            lock (_vaultLock)
            {
                try
                {
                    var vault = LoadVaultData();
                    count = vault.Count;
                    foreach (var key in vault.Keys)
                    {
                        try { CredDelete(key, CRED_TYPE_GENERIC, 0); } catch { }
                        try { CredDelete(key, 2, 0); } catch { }
                    }
                    if (File.Exists(VaultFilePath))
                    {
                        File.Delete(VaultFilePath);
                    }
                    Logger.Security(string.Format("Purged all {0} network credentials from vault.", count));
                }
                catch { }
            }
            return count;
        }

        private static bool SaveToDpapiVault(string target, string userName, string password)
        {
            lock (_vaultLock)
            {
                try
                {
                    Dictionary<string, string> vault = LoadVaultData();
                    string payload = string.Format("{0}|||{1}", userName, password);
                    vault[target] = payload;
                    SaveVaultData(vault);
                    Logger.Security("Stored credentials in DPAPI hardware-encrypted vault for target: " + target);
                    return true;
                }
                catch (Exception ex)
                {
                    Logger.Error("Failed saving to DPAPI vault for " + target, ex);
                    return false;
                }
            }
        }

        private static StoredCredential ReadFromDpapiVault(string target)
        {
            lock (_vaultLock)
            {
                try
                {
                    Dictionary<string, string> vault = LoadVaultData();
                    if (vault.ContainsKey(target))
                    {
                        string[] parts = vault[target].Split(new[] { "|||" }, StringSplitOptions.None);
                        if (parts.Length >= 2)
                        {
                            return new StoredCredential
                            {
                                Target = target,
                                UserName = parts[0],
                                Password = parts[1]
                            };
                        }
                    }
                }
                catch { }
                return null;
            }
        }

        private static bool DeleteFromDpapiVault(string target)
        {
            lock (_vaultLock)
            {
                try
                {
                    Dictionary<string, string> vault = LoadVaultData();
                    if (vault.Remove(target))
                    {
                        SaveVaultData(vault);
                        return true;
                    }
                }
                catch { }
                return false;
            }
        }

        private static Dictionary<string, string> LoadVaultData()
        {
            Dictionary<string, string> dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrEmpty(VaultFilePath) || !File.Exists(VaultFilePath)) return dict;

            try
            {
                byte[] encryptedBytes = File.ReadAllBytes(VaultFilePath);
                byte[] rawBytes = ProtectedData.Unprotect(encryptedBytes, null, DataProtectionScope.CurrentUser);
                string text = Encoding.UTF8.GetString(rawBytes);

                using (StringReader sr = new StringReader(text))
                {
                    string line;
                    while ((line = sr.ReadLine()) != null)
                    {
                        int idx = line.IndexOf("=>");
                        if (idx > 0)
                        {
                            string key = line.Substring(0, idx).Trim();
                            string val = line.Substring(idx + 2).Trim();
                            dict[key] = val;
                        }
                    }
                }
            }
            catch { }

            return dict;
        }

        private static void SaveVaultData(Dictionary<string, string> dict)
        {
            if (string.IsNullOrEmpty(VaultFilePath)) return;

            StringBuilder sb = new StringBuilder();
            foreach (var kvp in dict)
            {
                sb.AppendLine(string.Format("{0}=>{1}", kvp.Key, kvp.Value));
            }

            byte[] rawBytes = Encoding.UTF8.GetBytes(sb.ToString());
            byte[] encryptedBytes = ProtectedData.Protect(rawBytes, null, DataProtectionScope.CurrentUser);
            File.WriteAllBytes(VaultFilePath, encryptedBytes);
        }
    }
}
