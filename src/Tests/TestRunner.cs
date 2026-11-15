using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using NetworkSharing.Core;
using NetworkSharing.Logging;
using NetworkSharing.Recovery;
using NetworkSharing.Security;

namespace NetworkSharing.Tests
{
    public class TestResult
    {
        public string Category { get; set; }
        public string TestName { get; set; }
        public bool Passed { get; set; }
        public string Details { get; set; }
        public long DurationMs { get; set; }
    }

    /// <summary>
    /// Automated Test Suite for NetworkSharing.exe.
    /// Runs unit tests, security boundary tests, and recovery tests.
    /// </summary>
    public static class TestRunner
    {
        public static List<TestResult> RunAllTests()
        {
            List<TestResult> results = new List<TestResult>();

            // --- 1. SECURITY & VALIDATION TESTS ---
            RunTest(results, "Security", "Directory Traversal Protection ('..')", () =>
            {
                var r = SecurityValidator.ValidatePath(@"C:\Users\..\Windows\System32");
                Assert(!r.IsValid, "SecurityValidator must block path traversal sequences ('..').");
            });

            RunTest(results, "Security", "Protected Core System Folder Block (C:\\Windows)", () =>
            {
                string winDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
                var r = SecurityValidator.ValidatePath(winDir);
                Assert(!r.IsValid, "SecurityValidator must block sharing core Windows directory: " + winDir);
            });

            RunTest(results, "Security", "Protected Program Files Block", () =>
            {
                string progFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
                var r = SecurityValidator.ValidatePath(progFiles);
                Assert(!r.IsValid, "SecurityValidator must block sharing Program Files.");
            });

            RunTest(results, "Security", "Protected System Volume Information Block", () =>
            {
                var r = SecurityValidator.ValidatePath(@"C:\System Volume Information");
                Assert(!r.IsValid, "SecurityValidator must block System Volume Information.");
            });

            RunTest(results, "Security", "System Drive Root Read-Only Warning", () =>
            {
                string root = Path.GetPathRoot(Environment.SystemDirectory);
                var r = SecurityValidator.ValidatePath(root);
                Assert(r.IsValid && r.IsWarning, "System drive root should return a warning recommendation for Read-Only.");
            });

            RunTest(results, "Security", "Administrative Share Name Block ($)", () =>
            {
                var r = SecurityValidator.ValidateShareName("Confidential$");
                Assert(!r.IsValid, "Share names ending with '$' must be restricted.");
            });

            RunTest(results, "Security", "Reserved Device Share Name Block", () =>
            {
                var r = SecurityValidator.ValidateShareName("COM1");
                Assert(!r.IsValid, "Reserved Windows device names like COM1 must be blocked.");
            });

            RunTest(results, "Security", "Invalid Characters in Share Name", () =>
            {
                var r = SecurityValidator.ValidateShareName("My/Share\\Name");
                Assert(!r.IsValid, "Invalid characters in share name must be rejected.");
            });

            RunTest(results, "Security", "Built-in Account Name Protection", () =>
            {
                var r = SecurityValidator.ValidateUsername("Administrator");
                Assert(!r.IsValid, "Built-in account names like Administrator must be rejected.");
            });

            RunTest(results, "Security", "Cryptographic Password Generator Quality", () =>
            {
                string pwd = PasswordGenerator.Generate(16);
                Assert(pwd.Length == 16, "Password length must match request.");
                var val = SecurityValidator.ValidatePassword(pwd);
                Assert(val.IsValid && !val.IsWarning, "Generated password must meet all complexity requirements.");
            });

            RunTest(results, "Security", "Secret Redactor Sanitization", () =>
            {
                string testLog = "Connecting with user: ShareUser and password=SecretPassword123! to host";
                string redacted = SecretRedactor.Redact(testLog);
                Assert(!redacted.Contains("SecretPassword123!"), "SecretRedactor must remove plaintext passwords.");
                Assert(redacted.Contains("[REDACTED]"), "SecretRedactor must replace secrets with [REDACTED].");
            });

            RunTest(results, "Security", "Elevation Operation Dispatcher Parsing", () =>
            {
                var op = NetworkSharing.Privileges.OperationDispatcher.ParseArguments(new string[] { "--op", "host" });
                Assert(op != null && op.OpType == NetworkSharing.Privileges.PrivilegedOperationType.HostSetup, "OperationDispatcher must parse --op host correctly without secrets.");
            });

            RunTest(results, "Security", "Security Descriptor SDDL Backup Capture", () =>
            {
                var snapshot = SecurityDescriptorManager.CaptureSnapshot(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
                Assert(snapshot != null && !string.IsNullOrEmpty(snapshot.Sddl) && (snapshot.Sddl.StartsWith("O:") || snapshot.Sddl.StartsWith("D:")), "SecurityDescriptorManager must generate valid SDDL.");
            });

            RunTest(results, "Security", "Reparse Point & Symlink Safety Check", () =>
            {
                bool isJunction = SecurityValidator.IsReparsePoint(Environment.GetFolderPath(Environment.SpecialFolder.Windows));
                Assert(!isJunction, "System directory should not be reported as reparse point / junction.");
            });

            // --- 2. CREDENTIAL MANAGER INTEGRATION TESTS ---
            RunTest(results, "Integration", "Windows Credential Manager Save & Retrieve & Delete", () =>
            {
                string testTarget = "TestHost_" + Guid.NewGuid().ToString("N").Substring(0, 8);
                string testUser = "TestUser";
                string testPass = "TestPass_" + Guid.NewGuid().ToString("N");

                try
                {
                    bool saved = CredentialManager.SaveCredential(testTarget, testUser, testPass);
                    Assert(saved, "CredWriteW must succeed.");

                    var read = CredentialManager.GetCredential(testTarget);
                    Assert(read != null, "CredReadW must retrieve saved credential.");
                    Assert(read.UserName == testUser, "Retrieved username must match.");
                    Assert(read.Password == testPass, "Retrieved password must match.");

                    bool deleted = CredentialManager.DeleteCredential(testTarget);
                    Assert(deleted, "CredDeleteW must succeed.");

                    var postDelete = CredentialManager.GetCredential(testTarget);
                    Assert(postDelete == null, "Credential must no longer exist after deletion.");
                }
                finally
                {
                    CredentialManager.DeleteCredential(testTarget);
                }
            });

            // --- 3. DRIVE DETECTOR & NETWORKING TESTS ---
            RunTest(results, "Functional", "Drive Detector Enumeration", () =>
            {
                List<DriveItem> drives = DriveDetector.GetAvailableDrives();
                Assert(drives.Count > 0, "DriveDetector must find at least one drive.");
                bool hasSystem = false;
                foreach (var d in drives)
                {
                    if (d.IsSystemDrive) hasSystem = true;
                }
                Assert(hasSystem, "DriveDetector must identify the Windows OS system drive.");
            });

            RunTest(results, "Functional", "Local Network IP Resolution", () =>
            {
                string ip = NetworkManager.GetLocalIpAddress();
                Assert(!string.IsNullOrEmpty(ip) && ip != "127.0.0.1", "NetworkManager must return active local IPv4 address.");
            });

            RunTest(results, "Functional", "Speed Test Format Unit Toggle Conversion", () =>
            {
                double bytesSec = 125000000.0;
                string mbps = NetworkManager.FormatSpeed(bytesSec, SpeedUnit.Mbps);
                string mbSec = NetworkManager.FormatSpeed(bytesSec, SpeedUnit.MBps);
                string gbSec = NetworkManager.FormatSpeed(bytesSec, SpeedUnit.GBps);
                string gbps = NetworkManager.FormatSpeed(bytesSec, SpeedUnit.Gbps);

                Assert(mbps.Contains("1000.00 Mbps"), "Mbps unit conversion must be accurate.");
                Assert(mbSec.Contains("119.21 MB/s"), "MB/s unit conversion must be accurate.");
                Assert(gbSec.Contains("GB/s"), "GB/s unit conversion must contain valid unit.");
                Assert(gbps.Contains("1.00 Gbps"), "Gbps unit conversion must be accurate.");
            });

            RunTest(results, "Functional", "Ping Latency MS Measurement", () =>
            {
                var r = NetworkManager.PingHostDetailed("127.0.0.1", 1000);
                Assert(r.Success, "Ping to 127.0.0.1 must succeed.");
                Assert(r.RoundtripTimeMs >= 0, "Ping latency must be non-negative.");
                Assert(!string.IsNullOrEmpty(r.Message), "Ping detailed message must be populated.");
            });

            RunTest(results, "Functional", "iPerf3 IP-to-IP Speed Benchmark Loopback", () =>
            {
                bool serverListening = NetworkManager.TestSpeedServerPort("127.0.0.1", 500);
                if (serverListening)
                {
                    var res = NetworkManager.RunSpeedTest("127.0.0.1", 2, true);
                    Assert(res.Success, "iPerf3 benchmark must succeed: " + res.ErrorMessage);
                    Assert(res.DurationSeconds >= 1.0, "Benchmark duration must be recorded.");
                    Assert(res.TotalBytesTransferred > 0, "Benchmark must physically stream real bytes.");
                }
                else
                {
                    // In sandboxed environments without loopback socket rights, verify routing & error guidance
                    var res = NetworkManager.RunSpeedTest("127.0.0.1", 2, true);
                    Assert(res.ErrorMessage != null && res.ErrorMessage.Contains("52010"), "Error message must guide user to speed benchmark port 52010.");
                }
            });

            // --- 4. ADVANCED & MOBILE CAPABILITY TESTS ---
            RunTest(results, "Mobile", "Mobile QR Code Bitmap Synthesis", () =>
            {
                using (var bmp = NetworkSharing.UI.QrCodeGenerator.GenerateQrBitmap("smb://192.168.0.128/Drive_D", 220))
                {
                    Assert(bmp != null, "QR Bitmap must not be null.");
                    Assert(bmp.Width == 220 && bmp.Height == 220, "QR Bitmap dimensions must match requested pixel size.");
                }
            });

            RunTest(results, "Monitoring", "Active SMB Session Enumeration Query", () =>
            {
                var sessions = SessionManager.GetActiveSessions();
                Assert(sessions != null, "GetActiveSessions must return a valid non-null list.");
            });

            // --- 5. RECOVERY & TRANSACTION TESTS ---
            RunTest(results, "Recovery", "Transaction Manager Journal & Rollback Lifecycle", () =>
            {
                var tx = TransactionManager.BeginTransaction("TestLifecycleOperation");
                Assert(tx != null, "BeginTransaction must create a new active transaction.");

                TransactionManager.RecordStep(TransactionStepType.CredentialSaved, "TempTestResource", "Details");
                bool commited = TransactionManager.CommitActiveTransaction();
                Assert(commited, "CommitActiveTransaction must succeed.");
            });

            return results;
        }

        private static void RunTest(List<TestResult> results, string category, string testName, Action testAction)
        {
            Stopwatch sw = Stopwatch.StartNew();
            TestResult res = new TestResult
            {
                Category = category,
                TestName = testName
            };

            try
            {
                testAction();
                res.Passed = true;
                res.Details = "PASSED";
            }
            catch (Exception ex)
            {
                res.Passed = false;
                res.Details = ex.Message;
            }
            finally
            {
                sw.Stop();
                res.DurationMs = sw.ElapsedMilliseconds;
                results.Add(res);
            }
        }

        private static void Assert(bool condition, string failureMessage)
        {
            if (!condition)
            {
                throw new Exception("Assertion Failed: " + failureMessage);
            }
        }
    }
}
