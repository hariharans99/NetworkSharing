using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using NetworkSharing.Logging;

namespace NetworkSharing.Core
{
    public class SmbSessionInfo
    {
        public string ClientComputer { get; set; }
        public string UserName { get; set; }
        public int OpenFilesCount { get; set; }
        public string IdleTime { get; set; }
        public string ClientType { get; set; }
    }

    public static class SessionManager
    {
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct SESSION_INFO_10
        {
            [MarshalAs(UnmanagedType.LPWStr)]
            public string sesi10_cname;
            [MarshalAs(UnmanagedType.LPWStr)]
            public string sesi10_username;
            public uint sesi10_time;
            public uint sesi10_idle_time;
        }

        [DllImport("Netapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern uint NetSessionEnum(
            string servername,
            string UncClientName,
            string username,
            uint level,
            out IntPtr bufptr,
            int prefmaxlen,
            out int entriesread,
            out int totalentries,
            ref int resume_handle);

        [DllImport("Netapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern uint NetSessionDel(
            string servername,
            string UncClientName,
            string username);

        [DllImport("Netapi32.dll")]
        private static extern int NetApiBufferFree(IntPtr Buffer);

        private const uint NERR_Success = 0;

        /// <summary>
        /// Retrieves all currently connected remote clients and users accessing this PC.
        /// Tries native NetSessionEnum first, falling back to net session CLI parsing.
        /// </summary>
        public static List<SmbSessionInfo> GetActiveSessions()
        {
            List<SmbSessionInfo> sessions = new List<SmbSessionInfo>();
            try
            {
                IntPtr bufPtr = IntPtr.Zero;
                int entriesRead = 0;
                int totalEntries = 0;
                int resumeHandle = 0;

                uint result = NetSessionEnum(null, null, null, 10, out bufPtr, -1, out entriesRead, out totalEntries, ref resumeHandle);

                if (result == NERR_Success && bufPtr != IntPtr.Zero && entriesRead > 0)
                {
                    int structSize = Marshal.SizeOf(typeof(SESSION_INFO_10));
                    for (int i = 0; i < entriesRead; i++)
                    {
                        IntPtr currentPtr = new IntPtr(bufPtr.ToInt64() + (i * structSize));
                        SESSION_INFO_10 info = (SESSION_INFO_10)Marshal.PtrToStructure(currentPtr, typeof(SESSION_INFO_10));

                        string cname = (info.sesi10_cname ?? "").Trim();
                        if (cname.StartsWith(@"\\")) cname = cname.Substring(2);

                        TimeSpan idleSpan = TimeSpan.FromSeconds(info.sesi10_idle_time);
                        string idleStr = string.Format("{0:D2}:{1:D2}:{2:D2}", idleSpan.Hours, idleSpan.Minutes, idleSpan.Seconds);

                        sessions.Add(new SmbSessionInfo
                        {
                            ClientComputer = cname,
                            UserName = info.sesi10_username ?? "Anonymous",
                            IdleTime = idleStr,
                            OpenFilesCount = 1,
                            ClientType = "SMB Active Client"
                        });
                    }
                    NetApiBufferFree(bufPtr);
                    return sessions;
                }
            }
            catch (Exception ex)
            {
                Logger.Warning("NetSessionEnum API failed, attempting CLI fallback: " + ex.Message);
            }

            // Fallback: Parse 'net session' CLI output
            try
            {
                string output = RunCommandWithOutput("net.exe", "session");
                if (!string.IsNullOrEmpty(output))
                {
                    string[] lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                    bool readingData = false;

                    foreach (string line in lines)
                    {
                        string trimmed = line.Trim();
                        if (trimmed.StartsWith("---"))
                        {
                            readingData = true;
                            continue;
                        }
                        if (trimmed.StartsWith("The command completed successfully", StringComparison.OrdinalIgnoreCase))
                        {
                            break;
                        }

                        if (readingData && trimmed.Length > 0 && !trimmed.StartsWith("There are no entries", StringComparison.OrdinalIgnoreCase))
                        {
                            string[] parts = System.Text.RegularExpressions.Regex.Split(trimmed, @"\s{2,}");
                            if (parts.Length >= 2)
                            {
                                string client = parts[0].TrimStart('\\');
                                string user = parts[1];
                                string clientType = parts.Length > 2 ? parts[2] : "Windows";
                                string idle = parts.Length > 4 ? parts[4] : "00:00:00";

                                sessions.Add(new SmbSessionInfo
                                {
                                    ClientComputer = client,
                                    UserName = user,
                                    ClientType = clientType,
                                    IdleTime = idle,
                                    OpenFilesCount = 1
                                });
                            }
                        }
                    }
                }
            }
            catch { }

            return sessions;
        }

        /// <summary>
        /// Disconnects/kicks a connected remote client machine from this host.
        /// </summary>
        public static bool DisconnectSession(string clientComputer)
        {
            if (string.IsNullOrWhiteSpace(clientComputer)) return false;
            try
            {
                string formatted = clientComputer.Trim();
                if (!formatted.StartsWith(@"\\")) formatted = @"\\" + formatted;

                uint res = NetSessionDel(null, formatted, null);
                if (res == NERR_Success)
                {
                    Logger.Security("Successfully terminated SMB session for client: " + clientComputer);
                    return true;
                }

                // CLI fallback
                string args = string.Format("session \"{0}\" /delete", formatted);
                int exitCode = RunHiddenCommand("net.exe", args);
                if (exitCode == 0)
                {
                    Logger.Security("Terminated SMB session via CLI for: " + clientComputer);
                    return true;
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Error terminating session for " + clientComputer, ex);
            }
            return false;
        }

        /// <summary>
        /// Flushes all active remote sessions connected to this host.
        /// Useful when the host changes passwords or usernames, forcing all connected clients to re-authenticate cleanly.
        /// </summary>
        public static int FlushAllActiveSessions()
        {
            int count = 0;
            try
            {
                var sessions = GetActiveSessions();
                count = sessions.Count;

                foreach (var s in sessions)
                {
                    DisconnectSession(s.ClientComputer);
                }

                RunHiddenCommand("net.exe", "session /delete /y");
                Logger.Security(string.Format("Flushed all active SMB sessions on host ({0} session(s) disconnected).", count));
            }
            catch (Exception ex)
            {
                Logger.Warning("Error flushing active sessions: " + ex.Message);
            }
            return count;
        }

        private static int RunHiddenCommand(string exe, string args)
        {
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo(exe, args)
                {
                    CreateNoWindow = true,
                    UseShellExecute = false
                };
                using (Process p = Process.Start(psi))
                {
                    p.WaitForExit(6000);
                    return p.ExitCode;
                }
            }
            catch { return -1; }
        }

        private static string RunCommandWithOutput(string exe, string args)
        {
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo(exe, args)
                {
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };
                using (Process p = Process.Start(psi))
                {
                    string outStr = p.StandardOutput.ReadToEnd();
                    p.WaitForExit(6000);
                    return outStr;
                }
            }
            catch { return string.Empty; }
        }
    }
}
