using System;
using System.IO;
using System.Security.AccessControl;
using System.Security.Principal;
using NetworkSharing.Logging;
using NetworkSharing.Security;

namespace NetworkSharing.Core
{
    public enum SharePermissionType
    {
        ReadOnly,
        ReadWrite
    }

    public static class PermissionManager
    {
        public static bool ApplyPermissions(string path, string username, SharePermissionType permissionType)
        {
            try
            {
                if (!Directory.Exists(path))
                {
                    Logger.Error("Cannot apply permissions. Target path does not exist: " + path);
                    return false;
                }

                // 1. First preserve original ACL for rollback capability
                AclBackup.BackupAcl(path);

                DirectoryInfo dInfo = new DirectoryInfo(path);
                DirectorySecurity security = dInfo.GetAccessControl(AccessControlSections.Access);

                FileSystemRights rights = FileSystemRights.ReadAndExecute | FileSystemRights.ListDirectory | FileSystemRights.Synchronize;

                if (permissionType == SharePermissionType.ReadWrite)
                {
                    // Modify rights: Read, Write, Delete, Append, but NOT FullControl (TakeOwnership/ChangePermissions)
                    rights |= FileSystemRights.Modify | FileSystemRights.Write;
                }

                InheritanceFlags inheritFlags = InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit;
                PropagationFlags propFlags = PropagationFlags.None;
                AccessControlType accessType = AccessControlType.Allow;

                NTAccount account = null;
                try
                {
                    account = new NTAccount(Environment.MachineName, username);
                    account.Translate(typeof(SecurityIdentifier));
                }
                catch
                {
                    account = new NTAccount(username);
                }

                FileSystemAccessRule rule = new FileSystemAccessRule(account, rights, inheritFlags, propFlags, accessType);

                // Add rule without replacing existing system rules
                security.AddAccessRule(rule);
                dInfo.SetAccessControl(security);

                Logger.Info(string.Format("Applied NTFS {0} permissions for '{1}' on: {2}", 
                    permissionType == SharePermissionType.ReadWrite ? "Read/Write (Modify)" : "Read-Only", username, path));
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to apply NTFS permissions on: " + path, ex);
                return false;
            }
        }

        public static bool RemoveUserPermissions(string path, string username)
        {
            try
            {
                if (!Directory.Exists(path)) return true;

                DirectoryInfo dInfo = new DirectoryInfo(path);
                DirectorySecurity security = dInfo.GetAccessControl(AccessControlSections.Access);

                NTAccount account = new NTAccount(Environment.MachineName, username);
                security.PurgeAccessRules(account);
                dInfo.SetAccessControl(security);

                Logger.Info("Removed NTFS access rules for '" + username + "' from: " + path);
                return true;
            }
            catch (Exception ex)
            {
                Logger.Warning("Could not purge access rules for " + username + " on " + path + ": " + ex.Message);
                return false;
            }
        }
    }
}
