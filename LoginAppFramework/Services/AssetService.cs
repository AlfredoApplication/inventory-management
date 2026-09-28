using System;
using System.Collections.Generic;
using System.Linq;

namespace LoginAppFramework
{
    public interface IAssetService
    {
        void Save(Asset asset);
        void SaveMany(List<Asset> assets);
        void Delete(Asset asset);
        void DeleteMany(List<Asset> assets);
        void ApplyAssignment(Asset asset, Worker worker, string source);
        void ApplyUnassignment(Asset asset, string source, string nextStatus = "Anbarda");
        void Assign(Asset asset, Worker worker, string source);
        void Unassign(Asset asset, string source, string nextStatus = "Anbarda");
        void Archive(Asset asset, string source);
        void AssignMany(IEnumerable<Asset> assets, Worker worker, string source);
    }

    public sealed class AssetService : IAssetService
    {
        public void Save(Asset asset)
        {
            if (asset == null) throw new ArgumentNullException(nameof(asset));

            AddAssignmentHistoryForEdit(asset);
            AppData.SaveAndRefreshAsset(asset);
        }

        public void SaveMany(List<Asset> assets)
        {
            if (assets == null || assets.Count == 0) return;
            AppData.BulkSaveAndRefreshAssets(assets);
        }

        public void Delete(Asset asset)
        {
            if (asset == null) return;
            AppData.DeleteAndRefreshAsset(asset);
        }

        public void DeleteMany(List<Asset> assets)
        {
            if (assets == null || assets.Count == 0) return;
            AppData.BulkDeleteAndRefreshAssets(assets);
        }

        public void ApplyAssignment(Asset asset, Worker worker, string source)
        {
            if (asset == null) throw new ArgumentNullException(nameof(asset));
            if (worker == null) throw new ArgumentNullException(nameof(worker));

            string previousWorker = asset.AssignedUser;
            bool wasAssigned = asset.WorkerId.HasValue;

            asset.AssignWorker(worker);
            AddHistory(
                asset,
                wasAssigned ? AssignmentAction.Reassigned : AssignmentAction.Assigned,
                wasAssigned ? previousWorker : source,
                worker.per_adiper_soyadi,
                source);
        }

        public void ApplyUnassignment(Asset asset, string source, string nextStatus = "Anbarda")
        {
            if (asset == null) throw new ArgumentNullException(nameof(asset));

            string previousWorker = asset.AssignedUser;
            bool wasAssigned = asset.WorkerId.HasValue || !string.IsNullOrWhiteSpace(previousWorker);

            asset.ClearWorkerAssignment(nextStatus);

            if (wasAssigned)
            {
                AddHistory(
                    asset,
                    AssignmentAction.Unassigned,
                    previousWorker,
                    source,
                    source);
            }
        }

        public void Assign(Asset asset, Worker worker, string source)
        {
            ApplyAssignment(asset, worker, source);
            AppData.SaveAndRefreshAsset(asset);
        }

        public void Unassign(Asset asset, string source, string nextStatus = "Anbarda")
        {
            ApplyUnassignment(asset, source, nextStatus);
            AppData.SaveAndRefreshAsset(asset);
        }

        public void Archive(Asset asset, string source)
        {
            ApplyUnassignment(asset, source, "Arxivdə");
            asset.Status = "Arxivdə";
            AppData.SaveAndRefreshAsset(asset);
        }

        public void AssignMany(IEnumerable<Asset> assets, Worker worker, string source)
        {
            if (assets == null) return;
            if (worker == null) throw new ArgumentNullException(nameof(worker));

            var assetList = assets.ToList();
            foreach (var asset in assetList)
            {
                ApplyAssignment(asset, worker, source);
            }

            AppData.BulkSaveAndRefreshAssets(assetList);
        }

        private static void AddAssignmentHistoryForEdit(Asset asset)
        {
            if (asset.Id <= 0) return;

            var original = AppData.GetAssets().FirstOrDefault(a => a.Id == asset.Id);
            if (original == null || original.WorkerId == asset.WorkerId) return;

            bool alreadyHasPendingAssignmentHistory = asset.History?.Any(h => h.Id == 0) == true;
            if (alreadyHasPendingAssignmentHistory) return;

            if (asset.WorkerId.HasValue)
            {
                AddHistory(
                    asset,
                    original.WorkerId.HasValue ? AssignmentAction.Reassigned : AssignmentAction.Assigned,
                    original.AssignedUser ?? "Sistem",
                    asset.AssignedUser ?? "Naməlum əməkdaş",
                    "Vəsait redaktəsi");
            }
            else
            {
                AddHistory(
                    asset,
                    AssignmentAction.Unassigned,
                    original.AssignedUser ?? "Naməlum əməkdaş",
                    "Vəsait redaktəsi",
                    "Vəsait redaktəsi");
            }
        }

        private static void AddHistory(
            Asset asset,
            AssignmentAction action,
            string from,
            string to,
            string source)
        {
            asset.History ??= new List<AssignmentHistoryEntry>();
            asset.History.Add(new AssignmentHistoryEntry
            {
                Action = action,
                FromWorkerName = from,
                ToWorkerName = to,
                ChangedBy = SessionManager.CurrentUser?.FullName ?? SessionManager.CurrentUser?.Username ?? "Sistem",
                ChangeDate = DateTime.Now
            });
        }
    }
}
