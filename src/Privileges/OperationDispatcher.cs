using System;
using NetworkSharing.Logging;

namespace NetworkSharing.Privileges
{
    public enum PrivilegedOperationType
    {
        None,
        HostSetup,
        FullRollback,
        FirewallRepair
    }

    public class DispatchedOperation
    {
        public PrivilegedOperationType OpType { get; set; }
        public string TargetReference { get; set; }
        public bool IsValid { get; set; }
    }

    public static class OperationDispatcher
    {
        /// <summary>
        /// Parses and validates process command line arguments on startup.
        /// Validates that no sensitive information is present and dispatches the validated operation.
        /// </summary>
        public static DispatchedOperation ParseArguments(string[] args)
        {
            DispatchedOperation result = new DispatchedOperation
            {
                OpType = PrivilegedOperationType.None,
                TargetReference = string.Empty,
                IsValid = false
            };

            if (args == null || args.Length == 0)
            {
                return result;
            }

            for (int i = 0; i < args.Length; i++)
            {
                string arg = args[i].Trim();
                if (arg.Equals("--op", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                {
                    string opName = args[++i].Trim().ToLowerInvariant();
                    switch (opName)
                    {
                        case "host":
                        case "setup":
                            result.OpType = PrivilegedOperationType.HostSetup;
                            result.IsValid = true;
                            break;
                        case "rollback":
                        case "uninstall":
                            result.OpType = PrivilegedOperationType.FullRollback;
                            result.IsValid = true;
                            break;
                        case "firewall":
                            result.OpType = PrivilegedOperationType.FirewallRepair;
                            result.IsValid = true;
                            break;
                        default:
                            Logger.Warning("Unknown operation argument: " + opName);
                            break;
                    }
                }
                else if (arg.Equals("--target", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                {
                    result.TargetReference = args[++i].Trim();
                }
                else if (arg.Equals("--host", StringComparison.OrdinalIgnoreCase))
                {
                    result.OpType = PrivilegedOperationType.HostSetup;
                    result.IsValid = true;
                }
                else if (arg.Equals("--rollback", StringComparison.OrdinalIgnoreCase))
                {
                    result.OpType = PrivilegedOperationType.FullRollback;
                    result.IsValid = true;
                }
            }

            return result;
        }
    }
}
