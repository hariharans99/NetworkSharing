using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Windows.Forms;
using NetworkSharing.Logging;
using NetworkSharing.Privileges;
using NetworkSharing.Tests;
using NetworkSharing.UI;

namespace NetworkSharing
{
    static class Program
    {
        [DllImport("user32.dll")]
        private static extern bool SetProcessDPIAware();

        [DllImport("kernel32.dll")]
        private static extern bool AttachConsole(int dwProcessId);
        private const int ATTACH_PARENT_PROCESS = -1;

        [STAThread]
        static int Main(string[] args)
        {
            try
            {
                // Enable High-DPI Awareness for crisp rendering on high-res / scaled displays
                try
                {
                    SetProcessDPIAware();
                }
                catch { }

                // Check for automated test execution flag
                if (args.Length > 0 && (args[0].Equals("--test", StringComparison.OrdinalIgnoreCase) || args[0].Equals("--test-all", StringComparison.OrdinalIgnoreCase)))
                {
                    AttachConsole(ATTACH_PARENT_PROCESS);
                    Console.WriteLine("\n=======================================================");
                    Console.WriteLine(" NetworkSharing.exe - Automated Production Test Suite ");
                    Console.WriteLine("=======================================================\n");

                    var results = TestRunner.RunAllTests();
                    int passed = 0;
                    int failed = 0;

                    foreach (var r in results)
                    {
                        string status = r.Passed ? "[PASS]" : "[FAIL]";
                        Console.WriteLine(string.Format("{0,-7} | {1,-12} | {2,-45} ({3} ms)", status, r.Category, r.TestName, r.DurationMs));
                        if (!r.Passed)
                        {
                            Console.WriteLine("        Error: " + r.Details);
                            failed++;
                        }
                        else
                        {
                            passed++;
                        }
                    }

                    Console.WriteLine("\n-------------------------------------------------------");
                    Console.WriteLine(string.Format("Results: {0} Passed, {1} Failed, Total: {2}", passed, failed, results.Count));
                    Console.WriteLine("=======================================================\n");

                    return failed == 0 ? 0 : 1;
                }

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);

                // Dispatch operation from command line
                DispatchedOperation op = OperationDispatcher.ParseArguments(args);

                Logger.Info("NetworkSharing.exe starting up...");
                Application.Run(new MainDashboard(op));
                return 0;
            }
            catch (Exception ex)
            {
                Logger.Error("Fatal application error", ex);
                MessageBox.Show("Fatal Application Error: " + ex.Message, "NetworkSharing Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return -1;
            }
        }

        public static bool IsAdministrator()
        {
            return ElevationManager.IsElevated();
        }

        public static void RelaunchAsAdmin(string arguments = "")
        {
            ElevationManager.RequestElevation("host", arguments);
        }
    }
}
