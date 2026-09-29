using System;
using System.Collections.Generic;
using System.Linq;

namespace LoginAppFramework
{
    public interface IAuditService
    {
        List<AssetLog> GetAssetLogs();
        List<AssignmentHistoryEntry> GetAssignmentHistory();
        HashSet<string> GetCurrentAssetCodes();
        AuditRestoreResult RestoreDeletedAsset(AssetLog log);
    }

    public sealed class AuditService : IAuditService
    {
        private readonly IAuthorizationService _authorization;

        public AuditService(IAuthorizationService authorization = null)
        {
            _authorization = authorization ?? AppServices.Authorization;
        }
        public List<AssetLog> GetAssetLogs() => DataAccess.GetAssetLogs();

        public List<AssignmentHistoryEntry> GetAssignmentHistory()
            => DataAccess.GetAssignmentHistoryEntries();

        public HashSet<string> GetCurrentAssetCodes()
            => DataAccess.GetExistingAssetCodes();

        public AuditRestoreResult RestoreDeletedAsset(AssetLog log)
        {
            _authorization.RequireEdit();

            if (log == null)
                return AuditRestoreResult.Fail("Audit qeydi tapılmadı.");

            if (!string.Equals(log.status, "Silinən", StringComparison.OrdinalIgnoreCase))
                return AuditRestoreResult.Fail("Yalnız silinmiş vəsait audit qeydi bərpa edilə bilər.");

            var snapshot = AssetAuditSnapshot.FromDeletedLog(log);
            var asset = snapshot.Asset;

            if (string.IsNullOrWhiteSpace(asset.VesaitinKodu))
                return AuditRestoreResult.Fail("Audit snapshot-da vəsait kodu yoxdur.");

            if (DataAccess.AssetCodeExists(asset.VesaitinKodu))
                return AuditRestoreResult.Fail($"'{asset.VesaitinKodu}' kodlu vəsait artıq mövcuddur.");

            Worker worker = null;
            var workers = AppData.GetWorkers();

            if (snapshot.WorkerId.HasValue)
                worker = workers.FirstOrDefault(w => w.Id == snapshot.WorkerId.Value);

            if (worker == null && !string.IsNullOrWhiteSpace(snapshot.WorkerName))
            {
                worker = workers.FirstOrDefault(w =>
                    string.Equals(
                        w.per_adiper_soyadi,
                        snapshot.WorkerName,
                        StringComparison.OrdinalIgnoreCase));
            }

            string originalStatus = asset.Status;

            if (worker != null)
            {
                asset.AssignWorker(worker);
                if (!string.IsNullOrWhiteSpace(originalStatus))
                    asset.Status = originalStatus;

                asset.History.Add(new AssignmentHistoryEntry
                {
                    Action = AssignmentAction.Assigned,
                    FromWorkerName = "Audit bərpası",
                    ToWorkerName = worker.per_adiper_soyadi,
                    ChangedBy = SessionManager.CurrentUser?.FullName ?? "Sistem",
                    ChangeDate = DateTime.Now
                });
            }
            else
            {
                asset.ClearWorkerAssignment();
                if (!string.IsNullOrWhiteSpace(originalStatus) &&
                    !string.Equals(originalStatus, "İstifadədədir", StringComparison.OrdinalIgnoreCase))
                {
                    asset.Status = originalStatus;
                }
            }

            if (string.IsNullOrWhiteSpace(asset.Status))
                asset.Status = worker != null ? "İstifadədədir" : "Anbarda";

            int restoredId = DataAccess.RestoreDeletedAsset(
                asset,
                SessionManager.CurrentUser?.FullName ?? "Sistem");

            AppData.RefreshAssets(new[] { restoredId });

            return AuditRestoreResult.Ok(
                restoredId,
                $"{asset.VesaitinAdi ?? asset.VesaitinKodu} tam audit snapshot-dan bərpa edildi.");
        }
    }
}
