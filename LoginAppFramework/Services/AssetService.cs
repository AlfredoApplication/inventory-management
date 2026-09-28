using System;
using System.Collections.Generic;
using System.Linq;

namespace LoginAppFramework
{
    public interface IAssetService
    {
        void Save(Asset asset);
        void SaveMany(List<Asset> assets);
        void ImportNewAssets(List<Asset> assets);
        void Delete(Asset asset);
        void DeleteMany(List<Asset> assets);
        void ApplyAssignment(Asset asset, Worker worker, string source);
        void ApplyUnassignment(Asset asset, string source, string nextStatus = "Anbarda");
        void Assign(Asset asset, Worker worker, string source);
        void Unassign(Asset asset, string source, string nextStatus = "Anbarda");
        void Archive(Asset asset, string source);
        void AssignMany(IEnumerable<Asset> assets, Worker worker, string source);
        AssetBulkUpdateResult ApplyBulkChanges(
            IEnumerable<Asset> assets,
            BulkAssetChanges changes,
            string source);
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

        public void ImportNewAssets(List<Asset> assets)
        {
            if (assets == null || assets.Count == 0) return;

            foreach (var asset in assets.Where(a => a.WorkerId.HasValue && a.History?.Any(h => h.Id == 0) != true))
            {
                AddHistory(
                    asset,
                    AssignmentAction.Assigned,
                    "Excel import",
                    asset.AssignedUser ?? "Naməlum əməkdaş",
                    "Excel import");
            }

            DataAccess.InsertAssets(
                assets,
                SessionManager.CurrentUser?.FullName ?? "Sistem");

            AppData.RefreshAssets(assets.Select(a => a.Id));
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

        public AssetBulkUpdateResult ApplyBulkChanges(
            IEnumerable<Asset> assets,
            BulkAssetChanges changes,
            string source)
        {
            if (assets == null) throw new ArgumentNullException(nameof(assets));
            if (changes == null) throw new ArgumentNullException(nameof(changes));

            var changedAssets = new List<Asset>();
            var skippedStatusAssets = new List<Asset>();

            foreach (var asset in assets)
            {
                if (asset == null) continue;

                bool changed = false;

                if (changes.VesaitinKodu != null)
                {
                    asset.VesaitinKodu = changes.VesaitinKodu;
                    changed = true;
                }

                if (changes.VesaitinAdi != null)
                {
                    asset.VesaitinAdi = changes.VesaitinAdi;
                    changed = true;
                }

                if (changes.ITAvadanliqlarininSeriyaNomresi != null)
                {
                    asset.ITAvadanliqlarininSeriyaNomresi =
                        changes.ITAvadanliqlarininSeriyaNomresi;
                    changed = true;
                }

                if (changes.Kateqoriya != null)
                {
                    asset.Kateqoriya = changes.Kateqoriya;
                    changed = true;
                }

                if (changes.YerleshmeYeri != null)
                {
                    asset.YerleshmeYeri = changes.YerleshmeYeri;
                    changed = true;
                }

                if (changes.Erazi != null)
                {
                    asset.Erazi = changes.Erazi;
                    changed = true;
                }

                if (changes.PurchaseCost.HasValue)
                {
                    asset.PurchaseCost = changes.PurchaseCost.Value;
                    changed = true;
                }

                if (changes.PurchaseDate.HasValue)
                {
                    asset.PurchaseDate = changes.PurchaseDate.Value;
                    changed = true;
                }

                if (changes.UsefulLifeInYears.HasValue)
                {
                    asset.UsefulLifeInYears = changes.UsefulLifeInYears.Value;
                    changed = true;
                }

                if (changes.Supplier != null)
                {
                    asset.Supplier = changes.Supplier;
                    changed = true;
                }

                if (changes.WarrantyExpirationDate.HasValue)
                {
                    asset.WarrantyExpirationDate =
                        changes.WarrantyExpirationDate.Value;
                    changed = true;
                }

                if (changes.AssignedWorker != null)
                {
                    if (changes.AssignedWorker.Id == 0)
                        ApplyUnassignment(asset, source);
                    else
                        ApplyAssignment(asset, changes.AssignedWorker, source);

                    changed = true;
                }

                if (changes.Status != null)
                {
                    if (asset.WorkerId.HasValue)
                    {
                        skippedStatusAssets.Add(asset);
                    }
                    else
                    {
                        asset.Status = changes.Status;
                        changed = true;
                    }
                }

                if (changed)
                    changedAssets.Add(asset);
            }

            if (changedAssets.Count > 0)
                SaveMany(changedAssets);

            return new AssetBulkUpdateResult
            {
                UpdatedCount = changedAssets.Count,
                SkippedStatusAssets = skippedStatusAssets
            };
        }

        private static void AddAssignmentHistoryForEdit(Asset asset)
        {
            if (asset.Id <= 0)
            {
                if (asset.WorkerId.HasValue && asset.History?.Any(h => h.Id == 0) != true)
                {
                    AddHistory(
                        asset,
                        AssignmentAction.Assigned,
                        "Vəsait yaradılması",
                        asset.AssignedUser ?? "Naməlum əməkdaş",
                        "Vəsait yaradılması");
                }
                return;
            }

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
