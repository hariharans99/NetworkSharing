using System;
using System.Collections.Generic;
using System.IO;
using NetworkSharing.Core;
using NetworkSharing.Logging;
using NetworkSharing.Security;

namespace NetworkSharing.Recovery
{
    public enum TransactionStepType
    {
        UserCreated,
        ShareCreated,
        FirewallRuleCreated,
        AclModified,
        DriveMapped,
        CredentialSaved
    }

    public class TransactionStep
    {
        public TransactionStepType StepType { get; set; }
        public string TargetResource { get; set; }
        public string Details { get; set; }
        public DateTime Timestamp { get; set; }
    }

    public enum TransactionStatus
    {
        Active,
        Committed,
        RolledBack,
        Failed
    }

    public class TransactionRecord
    {
        public string TransactionId { get; set; }
        public string OperationName { get; set; }
        public DateTime StartTime { get; set; }
        public TransactionStatus Status { get; set; }
        public List<TransactionStep> Steps { get; set; }

        public TransactionRecord()
        {
            TransactionId = Guid.NewGuid().ToString("N");
            StartTime = DateTime.UtcNow;
            Status = TransactionStatus.Active;
            Steps = new List<TransactionStep>();
        }
    }

    /// <summary>
    /// Transactional operation journal manager.
    /// Ensures every configuration change is recorded before execution.
    /// Provides atomic rollback during setup and automatic crash recovery on startup.
    /// </summary>
    public static class TransactionManager
    {
        private static readonly object _syncLock = new object();
        private static TransactionRecord _activeTransaction;
        private static readonly string JournalFilePath;

        static TransactionManager()
        {
            try
            {
                string appData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NetworkSharing");
                if (!Directory.Exists(appData))
                {
                    Directory.CreateDirectory(appData);
                }
                JournalFilePath = Path.Combine(appData, "journal.txt");
            }
            catch (Exception ex)
            {
                Logger.Error("Failed initializing TransactionManager journal", ex);
            }
        }

        public static TransactionRecord BeginTransaction(string operationName)
        {
            lock (_syncLock)
            {
                if (_activeTransaction != null && _activeTransaction.Status == TransactionStatus.Active)
                {
                    Logger.Warning("Previous transaction " + _activeTransaction.TransactionId + " was still active. Rolling it back first.");
                    RollbackActiveTransaction();
                }

                _activeTransaction = new TransactionRecord
                {
                    OperationName = operationName
                };

                Logger.Info(string.Format("Transaction [{0}] started for operation: {1}", _activeTransaction.TransactionId, operationName));
                AppendJournal("BEGIN|" + _activeTransaction.TransactionId + "|" + operationName);
                return _activeTransaction;
            }
        }

        public static void RecordStep(TransactionStepType type, string resource, string details = "")
        {
            lock (_syncLock)
            {
                if (_activeTransaction == null || _activeTransaction.Status != TransactionStatus.Active)
                {
                    return;
                }

                TransactionStep step = new TransactionStep
                {
                    StepType = type,
                    TargetResource = resource,
                    Details = details,
                    Timestamp = DateTime.UtcNow
                };

                _activeTransaction.Steps.Add(step);
                AppendJournal(string.Format("STEP|{0}|{1}|{2}", _activeTransaction.TransactionId, type, resource));
            }
        }

        public static bool CommitActiveTransaction()
        {
            lock (_syncLock)
            {
                if (_activeTransaction == null || _activeTransaction.Status != TransactionStatus.Active)
                {
                    return false;
                }

                _activeTransaction.Status = TransactionStatus.Committed;
                AppendJournal("COMMIT|" + _activeTransaction.TransactionId);
                Logger.Info(string.Format("Transaction [{0}] committed successfully with {1} steps.", _activeTransaction.TransactionId, _activeTransaction.Steps.Count));
                _activeTransaction = null;
                return true;
            }
        }

        public static bool RollbackActiveTransaction()
        {
            lock (_syncLock)
            {
                if (_activeTransaction == null)
                {
                    return false;
                }

                Logger.Warning(string.Format("Rolling back transaction [{0}] ({1} steps to unwind)...", _activeTransaction.TransactionId, _activeTransaction.Steps.Count));
                
                // Unwind in reverse chronological order
                for (int i = _activeTransaction.Steps.Count - 1; i >= 0; i--)
                {
                    TransactionStep step = _activeTransaction.Steps[i];
                    try
                    {
                        switch (step.StepType)
                        {
                            case TransactionStepType.ShareCreated:
                                ShareManager.DeleteShare(step.TargetResource);
                                break;
                            case TransactionStepType.FirewallRuleCreated:
                                FirewallManager.RemoveInboundSmbRule();
                                break;
                            case TransactionStepType.UserCreated:
                                UserManager.DeleteUser(step.TargetResource);
                                break;
                            case TransactionStepType.AclModified:
                                AclBackup.RestoreAcl(step.TargetResource);
                                break;
                            case TransactionStepType.DriveMapped:
                                DriveMapper.DisconnectNetworkDrive(step.TargetResource);
                                break;
                            case TransactionStepType.CredentialSaved:
                                CredentialManager.DeleteCredential(step.TargetResource);
                                break;
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Error("Error unwinding transaction step " + step.StepType, ex);
                    }
                }

                _activeTransaction.Status = TransactionStatus.RolledBack;
                AppendJournal("ROLLBACK|" + _activeTransaction.TransactionId);
                _activeTransaction = null;
                return true;
            }
        }

        private static void AppendJournal(string entry)
        {
            if (string.IsNullOrEmpty(JournalFilePath)) return;
            try
            {
                string line = string.Format("{0:O}|{1}", DateTime.UtcNow, entry);
                File.AppendAllText(JournalFilePath, line + Environment.NewLine);
            }
            catch { }
        }
    }
}
