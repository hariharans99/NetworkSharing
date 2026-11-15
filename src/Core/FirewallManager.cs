using System;
using System.Diagnostics;
using NetworkSharing.Logging;

namespace NetworkSharing.Core
{
    public static class FirewallManager
    {
        public const string RuleName = "NetworkSharing_SMB_Inbound";
        public const string SpeedRuleName = "NetworkSharing_SpeedTest_Inbound";

        public static bool IsPrivateNetwork()
        {
            try
            {
                string output = RunCommandWithOutput("powershell.exe", 
                    "-NoProfile -Command \"Get-NetConnectionProfile | Select-Object -ExpandProperty NetworkCategory\"");
                return output.IndexOf("Private", StringComparison.OrdinalIgnoreCase) >= 0 ||
                       output.IndexOf("Domain", StringComparison.OrdinalIgnoreCase) >= 0;
            }
            catch
            {
                return true; // Default fallback
            }
        }

        public static bool ConfigureInboundSmbRule(string localSubnet = "LocalSubnet")
        {
            try
            {
                Logger.Info("Configuring Windows Firewall SMB inbound protection rule...");

                // First remove old app rule if exists
                RemoveInboundSmbRule();

                // Create strictly scoped inbound rule:
                // TCP 445, Private profile only, remote IP restricted to local subnet
                string args = string.Format(
                    "advfirewall firewall add rule name=\"{0}\" dir=in action=allow protocol=TCP localport=445,139 profile=private remoteip={1} description=\"Inbound SMB access restricted to local subnet by NetworkSharing.exe\"",
                    RuleName, localSubnet);

                int exitCode = RunHiddenCommand("netsh", args);
                if (exitCode == 0)
                {
                    Logger.Security(string.Format("Firewall rule '{0}' created. Inbound SMB is restricted strictly to {1} on Private profile.", RuleName, localSubnet));
                    return true;
                }

                Logger.Error(string.Format("Failed to create firewall rule '{0}'. Netsh exit code: {1}", RuleName, exitCode));
                return false;
            }
            catch (Exception ex)
            {
                Logger.Error("Exception configuring firewall", ex);
                return false;
            }
        }

        public static bool RemoveInboundSmbRule()
        {
            try
            {
                string args = string.Format("advfirewall firewall delete rule name=\"{0}\"", RuleName);
                int exitCode = RunHiddenCommand("netsh", args);
                Logger.Info("Removed application firewall rule: " + RuleName);
                return exitCode == 0;
            }
            catch (Exception ex)
            {
                Logger.Warning("Could not delete firewall rule: " + ex.Message);
                return false;
            }
        }

        public static bool ConfigureInboundSpeedRule()
        {
            try
            {
                string args = string.Format(
                    "advfirewall firewall add rule name=\"{0}\" dir=in action=allow protocol=TCP localport=52010 profile=any description=\"Inbound TCP Speed Test daemon for NetworkSharing.exe\"",
                    SpeedRuleName);
                int exitCode = RunHiddenCommand("netsh", args);
                if (exitCode == 0)
                {
                    Logger.Info("Configured Windows Firewall rule for Speed Daemon port 52010.");
                    return true;
                }
                return false;
            }
            catch { return false; }
        }

        public static bool RemoveInboundSpeedRule()
        {
            try
            {
                string args = string.Format("advfirewall firewall delete rule name=\"{0}\"", SpeedRuleName);
                int exitCode = RunHiddenCommand("netsh", args);
                return exitCode == 0;
            }
            catch { return false; }
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
