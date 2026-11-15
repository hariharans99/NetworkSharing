using System;
using System.Collections.Generic;
using NetworkSharing.Core;
using NetworkSharing.Logging;
using NetworkSharing.Security;

namespace NetworkSharing.Recovery
{
    public enum RollbackActionType
    {
        DeleteShare,
        DeleteUser,
        RemoveFirewallRule,
        RestoreAcl
    }

    public class RollbackItem
    {
        public RollbackActionType ActionType { get; set; }
        public string Target { get; set; }
        public string Extra { get; set; }
    }

    public static class RollbackManager
    {
        private static readonly Stack<RollbackItem> _actionStack = new Stack<RollbackItem>();

        public static void PushShareCreated(string shareName)
        {
            _actionStack.Push(new RollbackItem { ActionType = RollbackActionType.DeleteShare, Target = shareName });
        }

        public static void PushUserCreated(string username)
        {
            _actionStack.Push(new RollbackItem { ActionType = RollbackActionType.DeleteUser, Target = username });
        }

        public static void PushFirewallRuleCreated(string ruleName)
        {
            _actionStack.Push(new RollbackItem { ActionType = RollbackActionType.RemoveFirewallRule, Target = ruleName });
        }

        public static void PushAclModified(string path)
        {
            _actionStack.Push(new RollbackItem { ActionType = RollbackActionType.RestoreAcl, Target = path });
        }

        public static void Commit()
        {
            _actionStack.Clear();
            Logger.Info("Transaction committed successfully. Rollback stack cleared.");
        }

        public static void RollbackAll()
        {
            Logger.Warning(string.Format("Executing rollback of {0} operation(s)...", _actionStack.Count));

            while (_actionStack.Count > 0)
            {
                RollbackItem item = _actionStack.Pop();
                try
                {
                    switch (item.ActionType)
                    {
                        case RollbackActionType.DeleteShare:
                            ShareManager.DeleteShare(item.Target);
                            break;
                        case RollbackActionType.DeleteUser:
                            UserManager.DeleteUser(item.Target);
                            break;
                        case RollbackActionType.RemoveFirewallRule:
                            FirewallManager.RemoveInboundSmbRule();
                            break;
                        case RollbackActionType.RestoreAcl:
                            AclBackup.RestoreAcl(item.Target);
                            break;
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error("Error during rollback action: " + item.ActionType + " on " + item.Target, ex);
                }
            }

            Logger.Info("Rollback completed.");
        }
    }
}
