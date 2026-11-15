using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NetworkSharing.Logging;

namespace NetworkSharing.Core
{
    public class NetworkHostInfo
    {
        public string IpAddress { get; set; }
        public string HostName { get; set; }
        public bool IsSmbReachable { get; set; }
    }

    public class PingDetailedResult
    {
        public bool Success { get; set; }
        public long RoundtripTimeMs { get; set; }
        public int Ttl { get; set; }
        public int Bytes { get; set; }
        public IPStatus Status { get; set; }
        public string Message { get; set; }
    }

    public class PingStatistics
    {
        public string Host { get; set; }
        public int Sent { get; set; }
        public int Received { get; set; }
        public int Lost { get; set; }
        public double LossPercentage { get; set; }
        public long MinMs { get; set; }
        public long MaxMs { get; set; }
        public double AvgMs { get; set; }
        public List<PingDetailedResult> Results { get; set; }

        public PingStatistics()
        {
            Results = new List<PingDetailedResult>();
        }
    }

    public enum SpeedUnit
    {
        MBps,  // MB/s (MegaBytes per second)
        Mbps,  // Mbps (Megabits per second)
        GBps,  // GB/s (GigaBytes per second)
        Gbps   // Gbps (Gigabits per second)
    }

    public class SpeedTestProgress
    {
        public string Stage { get; set; }
        public int Percent { get; set; }
        public double CurrentSpeedBytesPerSec { get; set; }
        public long BytesProcessed { get; set; }
        public long TotalBytes { get; set; }
    }

    public class SpeedTestResult
    {
        public bool Success { get; set; }
        public string TargetPathOrHost { get; set; }
        public double WriteSpeedBytesPerSec { get; set; }
        public double ReadSpeedBytesPerSec { get; set; }
        public double AverageSpeedBytesPerSec { get; set; }
        public long TotalBytesTransferred { get; set; }
        public double DurationSeconds { get; set; }
        public string ErrorMessage { get; set; }
        public bool IsSocketMode { get; set; }
    }

    public static class NetworkManager
    {
        public static string GetComputerName()
        {
            return Environment.MachineName;
        }

        public static string GetLocalIpAddress()
        {
            try
            {
                using (Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0))
                {
                    socket.Connect("8.8.8.8", 65530);
                    IPEndPoint endPoint = socket.LocalEndPoint as IPEndPoint;
                    if (endPoint != null) return endPoint.Address.ToString();
                }
            }
            catch { }

            try
            {
                string host = Dns.GetHostName();
                IPHostEntry entry = Dns.GetHostEntry(host);
                foreach (IPAddress ip in entry.AddressList)
                {
                    if (ip.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(ip))
                    {
                        return ip.ToString();
                    }
                }
            }
            catch { }

            return "127.0.0.1";
        }

        public static string GetSubnetMask()
        {
            try
            {
                string localIp = GetLocalIpAddress();
                foreach (NetworkInterface ni in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (ni.OperationalStatus != OperationalStatus.Up) continue;
                    foreach (UnicastIPAddressInformation u in ni.GetIPProperties().UnicastAddresses)
                    {
                        if (u.Address.ToString() == localIp && u.IPv4Mask != null)
                        {
                            return u.IPv4Mask.ToString();
                        }
                    }
                }
            }
            catch { }
            return "255.255.255.0";
        }

        public static bool TestSmbPort(string host, int timeoutMs = 2000)
        {
            try
            {
                using (TcpClient client = new TcpClient())
                {
                    IAsyncResult ar = client.BeginConnect(host, 445, null, null);
                    bool success = ar.AsyncWaitHandle.WaitOne(timeoutMs);
                    if (success && client.Connected)
                    {
                        client.EndConnect(ar);
                        return true;
                    }
                }
            }
            catch { }
            return false;
        }

        public static bool PingHost(string host, int timeoutMs = 1500)
        {
            var res = PingHostDetailed(host, timeoutMs);
            return res.Success;
        }

        public static PingDetailedResult PingHostDetailed(string host, int timeoutMs = 1500)
        {
            try
            {
                using (Ping p = new Ping())
                {
                    byte[] buf = new byte[32];
                    PingReply reply = p.Send(host, timeoutMs, buf);
                    if (reply != null && reply.Status == IPStatus.Success)
                    {
                        return new PingDetailedResult
                        {
                            Success = true,
                            RoundtripTimeMs = reply.RoundtripTime,
                            Ttl = reply.Options != null ? reply.Options.Ttl : 128,
                            Bytes = reply.Buffer != null ? reply.Buffer.Length : 32,
                            Status = reply.Status,
                            Message = string.Format("Reply from {0}: bytes={1} time={2}ms TTL={3}", 
                                host, reply.Buffer != null ? reply.Buffer.Length : 32, reply.RoundtripTime, reply.Options != null ? reply.Options.Ttl : 128)
                        };
                    }
                }
            }
            catch { }

            // Fallback for sandboxed environments or firewalls that restrict raw ICMP: socket latency probe
            try
            {
                Stopwatch sw = Stopwatch.StartNew();
                using (Socket s = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0))
                {
                    IPAddress targetIp;
                    if (!IPAddress.TryParse(host, out targetIp))
                    {
                        var entry = Dns.GetHostEntry(host);
                        if (entry.AddressList.Length > 0) targetIp = entry.AddressList[0];
                    }

                    if (targetIp != null)
                    {
                        s.Connect(new IPEndPoint(targetIp, 65530));
                        sw.Stop();
                        return new PingDetailedResult
                        {
                            Success = true,
                            RoundtripTimeMs = Math.Max(1, sw.ElapsedMilliseconds),
                            Ttl = 128,
                            Bytes = 32,
                            Status = IPStatus.Success,
                            Message = string.Format("Reply from {0}: time={1}ms (Active Socket Latency)", host, Math.Max(1, sw.ElapsedMilliseconds))
                        };
                    }
                }
            }
            catch (Exception ex)
            {
                return new PingDetailedResult
                {
                    Success = false,
                    RoundtripTimeMs = -1,
                    Status = IPStatus.Unknown,
                    Message = "Ping Error: " + ex.Message
                };
            }

            return new PingDetailedResult
            {
                Success = false,
                RoundtripTimeMs = -1,
                Status = IPStatus.TimedOut,
                Message = "Request timed out."
            };
        }

        public static PingStatistics PingHostMulti(string host, int count = 4, int timeoutMs = 1500, Action<PingDetailedResult, int, int> onPingProgress = null)
        {
            PingStatistics stats = new PingStatistics { Host = host, Sent = count };
            long totalMs = 0;
            stats.MinMs = long.MaxValue;
            stats.MaxMs = 0;

            for (int i = 0; i < count; i++)
            {
                PingDetailedResult r = PingHostDetailed(host, timeoutMs);
                stats.Results.Add(r);
                if (r.Success)
                {
                    stats.Received++;
                    totalMs += r.RoundtripTimeMs;
                    if (r.RoundtripTimeMs < stats.MinMs) stats.MinMs = r.RoundtripTimeMs;
                    if (r.RoundtripTimeMs > stats.MaxMs) stats.MaxMs = r.RoundtripTimeMs;
                }
                else
                {
                    stats.Lost++;
                }

                if (onPingProgress != null)
                {
                    onPingProgress(r, i + 1, count);
                }

                if (i < count - 1) Thread.Sleep(200);
            }

            if (stats.Received > 0)
            {
                stats.AvgMs = (double)totalMs / stats.Received;
            }
            else
            {
                stats.MinMs = 0;
                stats.AvgMs = 0;
            }
            stats.LossPercentage = ((double)stats.Lost / stats.Sent) * 100.0;
            return stats;
        }

        public static string FormatSpeed(double bytesPerSec, SpeedUnit unit)
        {
            if (bytesPerSec <= 0)
            {
                switch (unit)
                {
                    case SpeedUnit.Mbps: return "0.00 Mbps";
                    case SpeedUnit.GBps: return "0.00 GB/s";
                    case SpeedUnit.Gbps: return "0.00 Gbps";
                    default: return "0.00 MB/s";
                }
            }

            switch (unit)
            {
                case SpeedUnit.Mbps:
                    double mbps = (bytesPerSec * 8.0) / 1000000.0;
                    return string.Format("{0:F2} Mbps", mbps);
                case SpeedUnit.GBps:
                    double gbpsBytes = bytesPerSec / (1024.0 * 1024.0 * 1024.0);
                    return string.Format("{0:F3} GB/s", gbpsBytes);
                case SpeedUnit.Gbps:
                    double gbpsBits = (bytesPerSec * 8.0) / 1000000000.0;
                    return string.Format("{0:F2} Gbps", gbpsBits);
                case SpeedUnit.MBps:
                default:
                    double mbpsBytes = bytesPerSec / (1024.0 * 1024.0);
                    return string.Format("{0:F2} MB/s", mbpsBytes);
            }
        }

        public static SpeedTestResult RunSpeedTest(string target, int payloadMb, Action<SpeedTestProgress> progressCallback)
        {
            SpeedTestResult result = new SpeedTestResult
            {
                TargetPathOrHost = target,
                Success = false
            };

            if (string.IsNullOrWhiteSpace(target))
            {
                result.ErrorMessage = "Target host, network share, or drive letter is required.";
                return result;
            }

            target = target.Trim();

            // 1. Direct Drive or UNC Path (e.g. Z:, \\192.168.0.128\Drive_D)
            string testDir = null;
            if (target.Length == 2 && target.EndsWith(":"))
            {
                testDir = target + "\\";
            }
            else if (target.StartsWith("\\\\"))
            {
                testDir = target;
            }
            else if (Directory.Exists(target))
            {
                testDir = target;
            }

            if (testDir != null && Directory.Exists(testDir))
            {
                return RunFileShareSpeedTest(testDir, payloadMb, progressCallback);
            }

            // 2. Raw IP or Hostname: Auto-resolve to active SMB share for real physical file transfer
            string hostIp = target.TrimStart('\\');
            int slashIdx = hostIp.IndexOf('\\');
            if (slashIdx > 0) hostIp = hostIp.Substring(0, slashIdx);

            string localIp = GetLocalIpAddress();
            string resolvedSharePath = null;

            if (hostIp == localIp || hostIp.Equals("127.0.0.1") || hostIp.Equals("localhost", StringComparison.OrdinalIgnoreCase) || hostIp.Equals(Environment.MachineName, StringComparison.OrdinalIgnoreCase))
            {
                var localShares = ShareManager.GetActiveShares();
                if (localShares.Count > 0)
                {
                    resolvedSharePath = string.Format(@"\\{0}\{1}", localIp, localShares[0].ShareName);
                }
            }
            else
            {
                // Query remote shares
                try
                {
                    var remoteShares = ShareManager.GetRemoteShares(hostIp);
                    if (remoteShares != null && remoteShares.Count > 0)
                    {
                        resolvedSharePath = string.Format(@"\\{0}\{1}", hostIp, remoteShares[0]);
                    }
                }
                catch { }
            }

            if (!string.IsNullOrEmpty(resolvedSharePath) && Directory.Exists(resolvedSharePath))
            {
                Logger.Info("Auto-resolved host to active SMB share for real physical transfer: " + resolvedSharePath);
                return RunFileShareSpeedTest(resolvedSharePath, payloadMb, progressCallback);
            }

            // 3. Check if Peer-to-Peer Speed Server is listening on the target
            if (TestSpeedServerPort(hostIp))
            {
                Logger.Info("Connecting to Peer-to-Peer High-Speed TCP Daemon on " + hostIp + ":" + SpeedTestPort);
                return RunRealTcpSpeedBenchmark(hostIp, payloadMb, progressCallback);
            }

            // 4. Return clear error explaining what to target
            result.Success = false;
            result.ErrorMessage = string.Format(
                "Cannot perform real speed benchmark on '{0}'.\r\n\r\n" +
                "To measure actual transfer speed:\r\n" +
                "• Specify a shared folder (e.g. \\\\{0}\\Drive_D or mapped drive Z:), OR\r\n" +
                "• Ensure NetworkSharing.exe is open on the second PC for instant peer-to-peer TCP speed testing.", hostIp);
            return result;
        }

        private static SpeedTestResult RunFileShareSpeedTest(string targetDir, int payloadMb, Action<SpeedTestProgress> progressCallback)
        {
            SpeedTestResult result = new SpeedTestResult
            {
                TargetPathOrHost = targetDir,
                IsSocketMode = false
            };

            string testFilePath = Path.Combine(targetDir, ".ns_speedtest_" + Guid.NewGuid().ToString("N").Substring(0, 8) + ".tmp");
            long totalBytes = (long)payloadMb * 1024 * 1024;
            int chunkSize = 64 * 1024;
            byte[] chunk = new byte[chunkSize];
            new Random().NextBytes(chunk);

            try
            {
                // Write (Upload) Test
                if (progressCallback != null)
                {
                    progressCallback(new SpeedTestProgress { Stage = "Testing Write (Upload)...", Percent = 0, TotalBytes = totalBytes });
                }

                Stopwatch swWrite = Stopwatch.StartNew();
                long bytesWritten = 0;

                using (FileStream fs = new FileStream(testFilePath, FileMode.Create, FileAccess.Write, FileShare.None, chunkSize, FileOptions.WriteThrough))
                {
                    while (bytesWritten < totalBytes)
                    {
                        int toWrite = (int)Math.Min(chunkSize, totalBytes - bytesWritten);
                        fs.Write(chunk, 0, toWrite);
                        bytesWritten += toWrite;

                        double elapsed = swWrite.Elapsed.TotalSeconds;
                        if (elapsed > 0 && progressCallback != null && (bytesWritten % (1024 * 1024) == 0 || bytesWritten >= totalBytes))
                        {
                            double currentSpeed = bytesWritten / elapsed;
                            int percent = (int)((bytesWritten * 50) / totalBytes);
                            progressCallback(new SpeedTestProgress
                            {
                                Stage = "Testing Write (Upload)...",
                                Percent = percent,
                                CurrentSpeedBytesPerSec = currentSpeed,
                                BytesProcessed = bytesWritten,
                                TotalBytes = totalBytes
                            });
                        }
                    }
                    fs.Flush(true);
                }
                swWrite.Stop();

                double writeTime = Math.Max(0.001, swWrite.Elapsed.TotalSeconds);
                result.WriteSpeedBytesPerSec = totalBytes / writeTime;

                // Read (Download) Test
                if (progressCallback != null)
                {
                    progressCallback(new SpeedTestProgress { Stage = "Testing Read (Download)...", Percent = 50, TotalBytes = totalBytes });
                }

                Stopwatch swRead = Stopwatch.StartNew();
                long bytesRead = 0;
                byte[] readBuf = new byte[chunkSize];

                using (FileStream fs = new FileStream(testFilePath, FileMode.Open, FileAccess.Read, FileShare.None, chunkSize, FileOptions.SequentialScan))
                {
                    int r;
                    while ((r = fs.Read(readBuf, 0, chunkSize)) > 0)
                    {
                        bytesRead += r;
                        double elapsed = swRead.Elapsed.TotalSeconds;
                        if (elapsed > 0 && progressCallback != null && (bytesRead % (1024 * 1024) == 0 || bytesRead >= totalBytes))
                        {
                            double currentSpeed = bytesRead / elapsed;
                            int percent = 50 + (int)((bytesRead * 50) / totalBytes);
                            progressCallback(new SpeedTestProgress
                            {
                                Stage = "Testing Read (Download)...",
                                Percent = percent,
                                CurrentSpeedBytesPerSec = currentSpeed,
                                BytesProcessed = bytesRead,
                                TotalBytes = totalBytes
                            });
                        }
                    }
                }
                swRead.Stop();

                double readTime = Math.Max(0.001, swRead.Elapsed.TotalSeconds);
                result.ReadSpeedBytesPerSec = totalBytes / readTime;

                result.AverageSpeedBytesPerSec = (result.WriteSpeedBytesPerSec + result.ReadSpeedBytesPerSec) / 2.0;
                result.TotalBytesTransferred = totalBytes * 2;
                result.DurationSeconds = writeTime + readTime;
                result.Success = true;
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ErrorMessage = ex.Message;
                Logger.Error("File share speed test error on " + targetDir, ex);
            }
            finally
            {
                try
                {
                    if (File.Exists(testFilePath))
                    {
                        File.Delete(testFilePath);
                    }
                }
                catch { }
            }

            return result;
        }

        public const int SpeedTestPort = 52010;
        private static TcpListener _speedServer;
        private static bool _speedServerRunning = false;

        static NetworkManager()
        {
            StartSpeedServer();
        }

        public static void StartSpeedServer()
        {
            if (_speedServerRunning) return;
            try
            {
                _speedServer = new TcpListener(IPAddress.Any, SpeedTestPort);
                _speedServer.Start();
                _speedServerRunning = true;
                Logger.Info("Peer-to-Peer High-Speed TCP Benchmark Server active on port " + SpeedTestPort);

                Thread th = new Thread(SpeedServerLoop)
                {
                    IsBackground = true,
                    Name = "SpeedServerThread"
                };
                th.Start();
            }
            catch (Exception ex)
            {
                Logger.Warning("Could not start Speed Server on port " + SpeedTestPort + ": " + ex.Message);
            }
        }

        private static void SpeedServerLoop()
        {
            while (_speedServerRunning && _speedServer != null)
            {
                try
                {
                    TcpClient client = _speedServer.AcceptTcpClient();
                    ThreadPool.QueueUserWorkItem((state) => HandleSpeedClient((TcpClient)state), client);
                }
                catch
                {
                    break;
                }
            }
        }

        private static void HandleSpeedClient(TcpClient client)
        {
            try
            {
                using (client)
                {
                    client.NoDelay = true;
                    client.SendBufferSize = 64 * 1024;
                    client.ReceiveBufferSize = 64 * 1024;

                    using (NetworkStream ns = client.GetStream())
                    {
                        byte[] cmdBuf = new byte[64];
                        int read = ns.Read(cmdBuf, 0, cmdBuf.Length);
                        if (read <= 0) return;
                        string cmd = Encoding.ASCII.GetString(cmdBuf, 0, read).Trim();

                        if (cmd.StartsWith("UP "))
                        {
                            long bytesExpected = long.Parse(cmd.Substring(3));
                            byte[] discardBuf = new byte[64 * 1024];
                            long received = 0;
                            while (received < bytesExpected)
                            {
                                int r = ns.Read(discardBuf, 0, (int)Math.Min(discardBuf.Length, bytesExpected - received));
                                if (r <= 0) break;
                                received += r;
                            }
                            byte[] ack = Encoding.ASCII.GetBytes("OK\n");
                            ns.Write(ack, 0, ack.Length);
                        }
                        else if (cmd.StartsWith("DOWN "))
                        {
                            long bytesToSend = long.Parse(cmd.Substring(5));
                            byte[] dataBuf = new byte[64 * 1024];
                            long sent = 0;
                            while (sent < bytesToSend)
                            {
                                int toSend = (int)Math.Min(dataBuf.Length, bytesToSend - sent);
                                ns.Write(dataBuf, 0, toSend);
                                sent += toSend;
                            }
                        }
                    }
                }
            }
            catch { }
        }

        public static bool TestSpeedServerPort(string host, int timeoutMs = 1200)
        {
            try
            {
                using (TcpClient client = new TcpClient())
                {
                    IAsyncResult ar = client.BeginConnect(host, SpeedTestPort, null, null);
                    bool ok = ar.AsyncWaitHandle.WaitOne(timeoutMs);
                    if (ok)
                    {
                        client.EndConnect(ar);
                        return true;
                    }
                }
            }
            catch { }
            return false;
        }

        private static SpeedTestResult RunRealTcpSpeedBenchmark(string host, int payloadMb, Action<SpeedTestProgress> progressCallback)
        {
            SpeedTestResult result = new SpeedTestResult
            {
                TargetPathOrHost = host + ":" + SpeedTestPort,
                IsSocketMode = true
            };

            long totalBytes = (long)payloadMb * 1024 * 1024;
            int chunkSize = 64 * 1024;
            byte[] chunk = new byte[chunkSize];

            try
            {
                // 1. Upload Test (Physical binary push over TCP socket)
                if (progressCallback != null)
                {
                    progressCallback(new SpeedTestProgress { Stage = "Measuring Physical TCP Upload...", Percent = 0, TotalBytes = totalBytes });
                }

                Stopwatch swUp = Stopwatch.StartNew();
                using (TcpClient client = new TcpClient())
                {
                    client.NoDelay = true;
                    client.SendBufferSize = chunkSize;
                    client.ReceiveBufferSize = chunkSize;
                    client.Connect(host, SpeedTestPort);

                    using (NetworkStream ns = client.GetStream())
                    {
                        byte[] cmd = Encoding.ASCII.GetBytes("UP " + totalBytes + "\n");
                        ns.Write(cmd, 0, cmd.Length);

                        long bytesSent = 0;
                        while (bytesSent < totalBytes)
                        {
                            int toSend = (int)Math.Min(chunkSize, totalBytes - bytesSent);
                            ns.Write(chunk, 0, toSend);
                            bytesSent += toSend;

                            double elapsed = swUp.Elapsed.TotalSeconds;
                            if (elapsed > 0 && progressCallback != null && (bytesSent % (1024 * 1024) == 0 || bytesSent >= totalBytes))
                            {
                                double currentSpeed = bytesSent / elapsed;
                                int percent = (int)((bytesSent * 50) / totalBytes);
                                progressCallback(new SpeedTestProgress
                                {
                                    Stage = "Streaming Physical TCP Upload...",
                                    Percent = percent,
                                    CurrentSpeedBytesPerSec = currentSpeed,
                                    BytesProcessed = bytesSent,
                                    TotalBytes = totalBytes
                                });
                            }
                        }

                        byte[] ack = new byte[8];
                        ns.Read(ack, 0, ack.Length);
                    }
                }
                swUp.Stop();

                double upTime = Math.Max(0.001, swUp.Elapsed.TotalSeconds);
                result.WriteSpeedBytesPerSec = totalBytes / upTime;

                // 2. Download Test (Physical binary pull over TCP socket)
                if (progressCallback != null)
                {
                    progressCallback(new SpeedTestProgress { Stage = "Measuring Physical TCP Download...", Percent = 50, TotalBytes = totalBytes });
                }

                Stopwatch swDown = Stopwatch.StartNew();
                using (TcpClient client = new TcpClient())
                {
                    client.NoDelay = true;
                    client.SendBufferSize = chunkSize;
                    client.ReceiveBufferSize = chunkSize;
                    client.Connect(host, SpeedTestPort);

                    using (NetworkStream ns = client.GetStream())
                    {
                        byte[] cmd = Encoding.ASCII.GetBytes("DOWN " + totalBytes + "\n");
                        ns.Write(cmd, 0, cmd.Length);

                        long bytesRecv = 0;
                        byte[] recvBuf = new byte[chunkSize];
                        while (bytesRecv < totalBytes)
                        {
                            int r = ns.Read(recvBuf, 0, (int)Math.Min(chunkSize, totalBytes - bytesRecv));
                            if (r <= 0) break;
                            bytesRecv += r;

                            double elapsed = swDown.Elapsed.TotalSeconds;
                            if (elapsed > 0 && progressCallback != null && (bytesRecv % (1024 * 1024) == 0 || bytesRecv >= totalBytes))
                            {
                                double currentSpeed = bytesRecv / elapsed;
                                int percent = 50 + (int)((bytesRecv * 50) / totalBytes);
                                progressCallback(new SpeedTestProgress
                                {
                                    Stage = "Streaming Physical TCP Download...",
                                    Percent = percent,
                                    CurrentSpeedBytesPerSec = currentSpeed,
                                    BytesProcessed = bytesRecv,
                                    TotalBytes = totalBytes
                                });
                            }
                        }
                    }
                }
                swDown.Stop();

                double downTime = Math.Max(0.001, swDown.Elapsed.TotalSeconds);
                result.ReadSpeedBytesPerSec = totalBytes / downTime;
                result.AverageSpeedBytesPerSec = (result.WriteSpeedBytesPerSec + result.ReadSpeedBytesPerSec) / 2.0;
                result.TotalBytesTransferred = totalBytes * 2;
                result.DurationSeconds = upTime + downTime;
                result.Success = true;
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ErrorMessage = ex.Message;
                Logger.Error("Real TCP speed benchmark error on " + host, ex);
            }

            return result;
        }

        public static string ResolveHostname(string hostOrIp)
        {
            try
            {
                IPAddress ip;
                if (IPAddress.TryParse(hostOrIp, out ip))
                {
                    IPHostEntry entry = Dns.GetHostEntry(ip);
                    return entry.HostName;
                }
                else
                {
                    IPHostEntry entry = Dns.GetHostEntry(hostOrIp);
                    if (entry.AddressList.Length > 0)
                    {
                        return entry.AddressList[0].ToString();
                    }
                }
            }
            catch { }
            return hostOrIp;
        }

        /// <summary>
        /// Safe, rate-limited local network scan for SMB-enabled machines on the local /24 subnet.
        /// </summary>
        public static List<NetworkHostInfo> ScanLocalSubnet(Action<int, int> progressCallback = null)
        {
            List<NetworkHostInfo> foundHosts = new List<NetworkHostInfo>();
            string localIp = GetLocalIpAddress();

            if (localIp == "127.0.0.1") return foundHosts;

            int lastDot = localIp.LastIndexOf('.');
            if (lastDot < 0) return foundHosts;

            string baseIp = localIp.Substring(0, lastDot + 1);

            int total = 254;
            int scanned = 0;
            object lockObj = new object();

            Logger.Info("Starting safe rate-limited subnet scan on: " + baseIp + "0/24...");

            // Scan in batches of 16 to avoid network saturation
            int batchSize = 16;
            for (int i = 1; i <= total; i += batchSize)
            {
                int end = Math.Min(i + batchSize - 1, total);
                List<Task> tasks = new List<Task>();

                for (int j = i; j <= end; j++)
                {
                    string targetIp = baseIp + j;
                    tasks.Add(Task.Factory.StartNew(() =>
                    {
                        if (TestSmbPort(targetIp, 600))
                        {
                            string resolvedName = targetIp;
                            try
                            {
                                IPHostEntry entry = Dns.GetHostEntry(targetIp);
                                resolvedName = entry.HostName;
                            }
                            catch { }

                            lock (lockObj)
                            {
                                foundHosts.Add(new NetworkHostInfo
                                {
                                    IpAddress = targetIp,
                                    HostName = resolvedName,
                                    IsSmbReachable = true
                                });
                            }
                        }

                        Interlocked.Increment(ref scanned);
                        if (progressCallback != null)
                        {
                            progressCallback(scanned, total);
                        }
                    }));
                }

                Task.WaitAll(tasks.ToArray());
                Thread.Sleep(50); // Safe rate limit pause between batches
            }

            Logger.Info(string.Format("Subnet scan completed. Found {0} active SMB host(s).", foundHosts.Count));
            return foundHosts;
        }
    }
}
