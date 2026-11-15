using System;
using System.Collections.Generic;
using NetworkSharing.Core;
using NetworkSharing.Logging;
using NetworkSharing.Security;

namespace NetworkSharing.Recovery
{
    public static class UninstallManager
    {
        public static bool PerformFullRollback(string dedicatedUser = "ShareUser")
        {
            Logger.Security("Initiating full rollback and removal of all application sharing configurations...");

            try
            {
                // 1. Remove all shares managed by this application
                HashSet<string> appShares = ShareManager.GetAppManagedShares();
                Logger.Info(string.Format("Removing {0} application-managed SMB share(s)...", appShares.Count));
                foreach (string share in appShares)
                {
                    ShareManager.DeleteShare(share);
                }

                // 2. Remove application firewall rules
                FirewallManager.RemoveInboundSmbRule();
                FirewallManager.RemoveInboundSpeedRule();

                // 3. Disconnect all mapped network drives
                DriveMapper.DisconnectAll();

                // 4. Remove saved credentials
                CredentialManager.DeleteCredential(Environment.MachineName);
                CredentialManager.DeleteCredential(NetworkManager.GetLocalIpAddress());

                // 5. Remove dedicated sharing user account
                if (!string.IsNullOrEmpty(dedicatedUser) && UserManager.UserExists(dedicatedUser))
                {
                    UserManager.DeleteUser(dedicatedUser);
                }

                Logger.Security("Full rollback and uninstall completed successfully. All original settings restored.");
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error("Exception during full rollback", ex);
                return false;
            }
        }
    }
}
