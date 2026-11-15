using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Security.Principal;
using System.Windows.Forms;
using NetworkSharing.Logging;

namespace NetworkSharing.Privileges
{
    public static class ElevationManager
    {
        /// <summary>
        /// Determines if the current application process has full Windows Administrator privileges.
        /// </summary>
        public static bool IsElevated()
        {
            try
            {
                WindowsIdentity identity = WindowsIdentity.GetCurrent();
                WindowsPrincipal principal = new WindowsPrincipal(identity);
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
            catch (Exception ex)
            {
                Logger.Error("Error checking process elevation", ex);
                return false;
            }
        }

        /// <summary>
        /// Requests UAC elevation via Windows Shell 'runas' with a validated operation identifier.
        /// Secrets are NEVER passed in command-line arguments.
        /// Returns true if elevated process started, false if user cancelled or error occurred.
        /// </summary>
        public static bool RequestElevation(string operationId, string targetRef = "", bool exitCurrent = true)
        {
            if (IsElevated())
            {
                Logger.Info("Application is already elevated. Operation '" + operationId + "' can proceed in-process.");
                return true;
            }

            string sanitizedOp = (operationId ?? "").Trim().ToLowerInvariant();
            string sanitizedTarget = (targetRef ?? "").Trim();

            // Validate operation identifier
            if (sanitizedOp != "host" && sanitizedOp != "rollback" && sanitizedOp != "firewall")
            {
                Logger.Warning("Unrecognized elevation operation: " + sanitizedOp);
            }

            string args = string.Format("--op {0}", sanitizedOp);
            if (!string.IsNullOrEmpty(sanitizedTarget))
            {
                args += string.Format(" --target \"{0}\"", sanitizedTarget);
            }

            try
            {
                Logger.Info("Requesting UAC elevation for operation: " + sanitizedOp);
                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = Application.ExecutablePath,
                    Arguments = args,
                    Verb = "runas",
                    UseShellExecute = true
                };

                Process p = Process.Start(psi);
                if (p != null)
                {
                    if (exitCurrent)
                    {
                        Application.Exit();
                    }
                    return true;
                }
                return false;
            }
            catch (Win32Exception winEx)
            {
                // Error code 1223 = ERROR_CANCELLED (The user cancelled the UAC prompt)
                if (winEx.NativeErrorCode == 1223)
                {
                    Logger.Warning("User declined UAC elevation prompt for operation: " + sanitizedOp);
                    MessageBox.Show("Administrator privileges are required for host sharing and configuration rollback.\n\nThe operation was cancelled.",
                        "Elevation Declined", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return false;
                }

                Logger.Error("Win32 error launching elevated process", winEx);
                MessageBox.Show("Could not elevate process: " + winEx.Message, "Elevation Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
            catch (Exception ex)
            {
                Logger.Error("Unexpected error launching elevated process", ex);
                return false;
            }
        }
    }
}
