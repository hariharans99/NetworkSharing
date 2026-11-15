using System;
using System.Diagnostics;
using System.DirectoryServices;
using System.Runtime.InteropServices;
using System.Security.Principal;
using NetworkSharing.Logging;

namespace NetworkSharing.Core
{
    public static class UserManager
    {
        private const uint USER_PRIV_USER = 1;
        private const uint UF_SCRIPT = 0x0001;
        private const uint UF_NORMAL_ACCOUNT = 0x0200;
        private const uint UF_DONT_EXPIRE_PASSWD = 0x10000;
        private const uint NERR_Success = 0;
        private const uint NERR_UserExists = 2224;

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct USER_INFO_1
        {
            public string usri1_name;
            public string usri1_password;
            public uint usri1_password_age;
            public uint usri1_priv;
            public string usri1_home_dir;
            public string usri1_comment;
            public uint usri1_flags;
            public string usri1_script_path;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct USER_INFO_1003
        {
            public string usri1003_password;
        }

        [DllImport("Netapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern uint NetUserAdd(string servername, uint level, ref USER_INFO_1 buf, out uint parm_err);

        [DllImport("Netapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern uint NetUserSetInfo(string servername, string username, uint level, ref USER_INFO_1003 buf, out uint parm_err);

        [DllImport("Netapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern uint NetUserDel(string servername, string username);

        public static bool UserExists(string username)
        {
            if (string.IsNullOrWhiteSpace(username)) return false;

            try
            {
                using (DirectoryEntry machine = new DirectoryEntry("WinNT://" + Environment.MachineName))
                {
                    try
                    {
                        DirectoryEntry user = machine.Children.Find(username.Trim(), "User");
                        return user != null;
                    }
                    catch
                    {
                        return false;
                    }
                }
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Creates a dedicated standard, non-administrator local Windows account.
        /// Uses native NetAPI32 in-memory APIs; passwords are NEVER exposed in process command line arguments.
        /// </summary>
        public static bool CreateDedicatedUser(string username, string password, string description = "Dedicated Local Network File Sharing Account")
        {
            try
            {
                username = (username ?? "").Trim();
                if (UserExists(username))
                {
                    Logger.Info("Sharing user '" + username + "' already exists. Updating password in memory...");
                    return SetPassword(username, password);
                }

                Logger.Info("Creating dedicated standard Windows user via native NetAPI32: " + username);

                // 1. First attempt: Native NetAPI32 NetUserAdd (No process argument exposure)
                USER_INFO_1 userInfo = new USER_INFO_1
                {
                    usri1_name = username,
                    usri1_password = password,
                    usri1_priv = USER_PRIV_USER, // Standard user only, never admin!
                    usri1_home_dir = null,
                    usri1_comment = description,
                    usri1_flags = UF_SCRIPT | UF_NORMAL_ACCOUNT | UF_DONT_EXPIRE_PASSWD,
                    usri1_script_path = null
                };

                uint parmErr;
                uint result = NetUserAdd(null, 1, ref userInfo, out parmErr);
                if (result == NERR_Success)
                {
                    Logger.Security("Successfully created dedicated standard user via Netapi32: " + username);
                    return true;
                }
                else if (result == NERR_UserExists)
                {
                    return SetPassword(username, password);
                }

                Logger.Warning(string.Format("NetUserAdd returned code {0} (parmErr: {1}). Attempting DirectoryEntry fallback...", result, parmErr));

                // 2. Second attempt: DirectoryEntry (WinNT://) in-process COM
                using (DirectoryEntry machine = new DirectoryEntry("WinNT://" + Environment.MachineName))
                {
                    using (DirectoryEntry newUser = machine.Children.Add(username, "User"))
                    {
                        newUser.Invoke("SetPassword", new object[] { password });
                        newUser.Invoke("Put", new object[] { "Description", description });
                        int flags = 0x0040 | 0x10000; // UserCannotChangePassword | PasswordNeverExpires
                        newUser.Invoke("Put", new object[] { "UserFlags", flags });
                        newUser.CommitChanges();
                    }

                    try
                    {
                        DirectoryEntry usersGroup = machine.Children.Find("Users", "Group");
                        usersGroup.Invoke("Add", new object[] { "WinNT://" + Environment.MachineName + "/" + username });
                        usersGroup.CommitChanges();
                    }
                    catch { }
                }

                Logger.Security("Successfully created dedicated standard user via DirectoryServices: " + username);
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error("Exception while creating user '" + username + "'", ex);
                return false;
            }
        }

        public static bool SetPassword(string username, string newPassword)
        {
            try
            {
                username = (username ?? "").Trim();

                // 1. Native NetAPI32 NetUserSetInfo
                USER_INFO_1003 info = new USER_INFO_1003 { usri1003_password = newPassword };
                uint parmErr;
                uint res = NetUserSetInfo(null, username, 1003, ref info, out parmErr);
                if (res == NERR_Success)
                {
                    Logger.Security("Updated password for sharing user via Netapi32: " + username);
                    return true;
                }

                // 2. DirectoryEntry fallback
                using (DirectoryEntry machine = new DirectoryEntry("WinNT://" + Environment.MachineName))
                {
                    using (DirectoryEntry user = machine.Children.Find(username, "User"))
                    {
                        user.Invoke("SetPassword", new object[] { newPassword });
                        user.CommitChanges();
                        Logger.Security("Updated password for sharing user via DirectoryServices: " + username);
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to update password for user " + username, ex);
                return false;
            }
        }

        public static bool DeleteUser(string username)
        {
            try
            {
                username = (username ?? "").Trim();
                if (!UserExists(username)) return true;

                Logger.Security("Deleting sharing user: " + username);

                // 1. Native NetAPI32 NetUserDel
                uint res = NetUserDel(null, username);
                if (res == NERR_Success)
                {
                    Logger.Info("Deleted user via Netapi32: " + username);
                    return true;
                }

                // 2. DirectoryEntry fallback
                using (DirectoryEntry machine = new DirectoryEntry("WinNT://" + Environment.MachineName))
                {
                    using (DirectoryEntry user = machine.Children.Find(username, "User"))
                    {
                        machine.Children.Remove(user);
                        machine.CommitChanges();
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to delete user '" + username + "'", ex);
                return false;
            }
        }
    }
}
