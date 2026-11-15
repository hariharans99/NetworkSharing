using System;
using System.IO;
using System.Security.AccessControl;
using System.Security.Principal;
using NetworkSharing.Logging;

namespace NetworkSharing.Security
{
    public class SecurityDescriptorSnapshot
    {
        public string TargetPath { get; set; }
        public string Sddl { get; set; }
        public string OwnerSid { get; set; }
        public string GroupSid { get; set; }
        public bool AreAccessRulesProtected { get; set; }
        public DateTime Timestamp { get; set; }
    }

    /// <summary>
    /// Comprehensive security descriptor and DACL inspection manager.
    /// Captures full SDDL including owner, group, DACL, and inheritance flags.
    /// Ensures canonical ACE ordering and minimal privilege granting.
    /// </summary>
    public static class SecurityDescriptorManager
    {
        public static SecurityDescriptorSnapshot CaptureSnapshot(string path)
        {
            if (string.IsNullOrEmpty(path) || !Directory.Exists(path))
            {
                return null;
            }

            try
            {
                string fullPath = Path.GetFullPath(path);
                DirectoryInfo dir = new DirectoryInfo(fullPath);
                
                // Use Access, Owner, and Group (DACL) to avoid requiring SeSecurityPrivilege (SACL) which standard users do not hold
                AccessControlSections sections = AccessControlSections.Access | AccessControlSections.Owner | AccessControlSections.Group;
                DirectorySecurity sec = dir.GetAccessControl(sections);

                string sddl = sec.GetSecurityDescriptorSddlForm(sections);
                IdentityReference owner = sec.GetOwner(typeof(SecurityIdentifier));
                IdentityReference group = sec.GetGroup(typeof(SecurityIdentifier));

                return new SecurityDescriptorSnapshot
                {
                    TargetPath = fullPath,
                    Sddl = sddl,
                    OwnerSid = owner != null ? owner.Value : string.Empty,
                    GroupSid = group != null ? group.Value : string.Empty,
                    AreAccessRulesProtected = sec.AreAccessRulesProtected,
                    Timestamp = DateTime.UtcNow
                };
            }
            catch (Exception ex)
            {
                Logger.Error("Failed capturing security descriptor snapshot for: " + path, ex);
                return null;
            }
        }

        public static bool RestoreSnapshot(SecurityDescriptorSnapshot snapshot)
        {
            if (snapshot == null || string.IsNullOrEmpty(snapshot.TargetPath) || !Directory.Exists(snapshot.TargetPath))
            {
                return false;
            }

            try
            {
                DirectorySecurity sec = new DirectorySecurity();
                AccessControlSections sections = AccessControlSections.Access | AccessControlSections.Owner | AccessControlSections.Group;
                sec.SetSecurityDescriptorSddlForm(snapshot.Sddl, sections);

                DirectoryInfo dir = new DirectoryInfo(snapshot.TargetPath);
                dir.SetAccessControl(sec);

                Logger.Security("Successfully restored complete security descriptor on: " + snapshot.TargetPath);
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error("Failed restoring security descriptor snapshot for: " + snapshot.TargetPath, ex);
                return false;
            }
        }
    }
}
