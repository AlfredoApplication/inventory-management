using System.Collections.Generic;
using System.Linq;

namespace LoginAppFramework
{
    public static class AppData
    {
        private static readonly object CacheLock = new();

        private static List<Asset> _assets;
        private static List<Worker> _workers;
        private static List<string> _assetStatuses;
        private static List<string> _workerDepartments;
        private static Dictionary<string, string> _assetStatusColors;
        private static Dictionary<string, int> _categoryDefaultLifecycles;
        private static Dictionary<string, List<string>> _categoryCustomFields;
        private static Dictionary<string, string> _deviceCategoryIcons;
        private static List<DeviceCategory> _hierarchicalCategories;

        // Full reload is intentionally reserved for login/startup or explicit recovery.
        public static void LoadAllData()
        {
            if (string.IsNullOrEmpty(SessionManager.CurrentUserConnectionString)) return;

            var assets = DataAccess.GetAllAssets();
            var workers = DataAccess.GetAllWorkers();
            var assetStatuses = DataAccess.GetAssetStatuses();
            var allCategories = DataAccess.GetDeviceCategories();
            var categoryCustomFields = DataAccess.GetCategoryCustomFields();
            var assetStatusColors = DataAccess.GetAssetStatusColors();
            var deviceCategoryIcons = DataAccess.GetDeviceCategoryIcons();
            var categoryDefaultLifecycles = DataAccess.GetCategoryDefaultLifecycles();

            var hierarchicalCategories = new List<DeviceCategory>();
            var categoryLookup = allCategories.ToDictionary(c => c.Id);
            foreach (var category in allCategories)
            {
                if (category.ParentId.HasValue && categoryLookup.TryGetValue(category.ParentId.Value, out var parent))
                {
                    parent.Subcategories.Add(category);
                }
                else
                {
                    hierarchicalCategories.Add(category);
                }
            }

            var assetsToNormalize = new List<Asset>();
            foreach (var asset in assets)
            {
                bool needsUpdate = false;
                if (asset.WorkerId.HasValue && asset.Status != "İstifadədədir")
                {
                    asset.Status = "İstifadədədir";
                    needsUpdate = true;
                }
                else if (!asset.WorkerId.HasValue && asset.Status == "İstifadədədir")
                {
                    asset.Status = "Anbarda";
                    needsUpdate = true;
                }

                if (needsUpdate) assetsToNormalize.Add(asset);
            }

            if (assetsToNormalize.Any())
            {
                DataAccess.BulkSaveAssets(assetsToNormalize, SessionManager.CurrentUser.FullName);
            }

            lock (CacheLock)
            {
                _assets = assets;
                _workers = workers;
                _assetStatuses = assetStatuses;
                _categoryCustomFields = categoryCustomFields;
                _assetStatusColors = assetStatusColors;
                _deviceCategoryIcons = deviceCategoryIcons;
                _categoryDefaultLifecycles = categoryDefaultLifecycles;
                _hierarchicalCategories = hierarchicalCategories;

                RebuildWorkerDepartmentsUnsafe();
                RelinkAllAssetWorkersUnsafe();
            }
        }

        public static void RefreshWorkersFromDatabase()
        {
            var workers = DataAccess.GetAllWorkers();

            lock (CacheLock)
            {
                _workers = workers;
                RebuildWorkerDepartmentsUnsafe();
                RelinkAllAssetWorkersUnsafe();
            }
        }

        public static void RefreshAssets(IEnumerable<int> assetIds)
        {
            var ids = assetIds?
                .Where(id => id > 0)
                .Distinct()
                .ToList() ?? new List<int>();

            if (!ids.Any()) return;

            var refreshedAssets = DataAccess.GetAssetsByIds(ids);
            lock (CacheLock)
            {
                UpsertAssetsUnsafe(refreshedAssets);
            }
        }

        public static void ClearData()
        {
            lock (CacheLock)
            {
                _assets = null;
                _workers = null;
                _assetStatuses = null;
                _workerDepartments = null;
                _categoryCustomFields = null;
                _deviceCategoryIcons = null;
                _assetStatusColors = null;
                _categoryDefaultLifecycles = null;
                _hierarchicalCategories = null;
            }
        }

        public static List<Asset> GetAssets()
        {
            lock (CacheLock) return _assets?.ToList() ?? new List<Asset>();
        }

        public static List<Worker> GetWorkers()
        {
            lock (CacheLock) return _workers?.ToList() ?? new List<Worker>();
        }

        public static List<string> GetAssetStatuses()
        {
            lock (CacheLock) return _assetStatuses?.ToList() ?? new List<string>();
        }

        public static List<string> GetWorkerDepartments()
        {
            lock (CacheLock) return _workerDepartments?.ToList() ?? new List<string>();
        }

        public static List<string> GetDeviceCategories()
        {
            lock (CacheLock)
            {
                if (_hierarchicalCategories == null) return new List<string>();

                static IEnumerable<string> Flatten(IEnumerable<DeviceCategory> cats) =>
                    cats.SelectMany(c => new[] { c.Name }.Concat(Flatten(c.Subcategories)));

                return Flatten(_hierarchicalCategories).OrderBy(name => name).ToList();
            }
        }

        public static List<DeviceCategory> GetHierarchicalCategories()
        {
            lock (CacheLock) return _hierarchicalCategories?.ToList() ?? new List<DeviceCategory>();
        }

        public static Dictionary<string, List<string>> GetCategoryCustomFields()
        {
            lock (CacheLock) return _categoryCustomFields ?? new Dictionary<string, List<string>>();
        }

        public static Dictionary<string, string> GetDeviceCategoryIcons()
        {
            lock (CacheLock) return _deviceCategoryIcons ?? new Dictionary<string, string>();
        }

        public static Dictionary<string, string> GetAssetStatusColors()
        {
            lock (CacheLock) return _assetStatusColors ?? new Dictionary<string, string>();
        }

        public static Dictionary<string, int> GetCategoryDefaultLifecycles()
        {
            lock (CacheLock) return _categoryDefaultLifecycles ?? new Dictionary<string, int>();
        }

        public static void SaveAndRefreshAsset(Asset asset)
        {
            DataAccess.SaveAsset(asset, SessionManager.CurrentUser.FullName);
            RefreshAssets(new[] { asset.Id });
        }

        public static void BulkSaveAndRefreshAssets(List<Asset> assets)
        {
            if (assets == null || !assets.Any()) return;

            DataAccess.BulkSaveAssets(assets, SessionManager.CurrentUser.FullName);
            RefreshAssets(assets.Select(a => a.Id));
        }

        public static void DeleteAndRefreshAsset(Asset asset)
        {
            if (asset == null) return;

            DataAccess.DeleteAsset(asset);
            RemoveCachedAssets(new[] { asset.Id });
        }

        public static void BulkDeleteAndRefreshAssets(List<Asset> assets)
        {
            if (assets == null || !assets.Any()) return;

            var ids = assets.Select(a => a.Id).Where(id => id > 0).Distinct().ToList();
            DataAccess.BulkDeleteAssets(assets);
            RemoveCachedAssets(ids);
        }

        public static void SaveAndRefreshWorker(Worker worker)
        {
            if (worker == null) return;

            DataAccess.SaveWorker(worker);
            var refreshedWorker = DataAccess.GetWorkerById(worker.Id);
            if (refreshedWorker == null) return;

            lock (CacheLock)
            {
                _workers ??= new List<Worker>();
                int index = _workers.FindIndex(w => w.Id == refreshedWorker.Id);
                if (index >= 0) _workers[index] = refreshedWorker;
                else _workers.Add(refreshedWorker);

                RebuildWorkerDepartmentsUnsafe();
                RelinkAssetsForWorkerUnsafe(refreshedWorker.Id);
            }
        }

        public static void DeleteAndRefreshWorker(Worker worker)
        {
            if (worker == null || worker.Id <= 0) return;

            var affectedAssetIds = DataAccess.DeleteWorkerAndUnassignAssets(
                worker.Id,
                SessionManager.CurrentUser.FullName);

            var refreshedAssets = DataAccess.GetAssetsByIds(affectedAssetIds);

            lock (CacheLock)
            {
                _workers?.RemoveAll(w => w.Id == worker.Id);
                RebuildWorkerDepartmentsUnsafe();
                UpsertAssetsUnsafe(refreshedAssets);
            }
        }

        public static int BulkSetWorkersActiveState(IEnumerable<int> workerIds, bool isActive)
        {
            var ids = workerIds?
                .Where(id => id > 0)
                .Distinct()
                .ToList() ?? new List<int>();

            if (!ids.Any()) return 0;

            int affectedRows = DataAccess.BulkSetWorkersActiveState(ids, isActive);
            var idSet = ids.ToHashSet();

            lock (CacheLock)
            {
                if (_workers != null)
                {
                    foreach (var worker in _workers.Where(w => idSet.Contains(w.Id)))
                    {
                        worker.IsActive = isActive;
                    }
                }
            }

            return affectedRows;
        }

        private static void RemoveCachedAssets(IEnumerable<int> assetIds)
        {
            var idSet = assetIds.Where(id => id > 0).ToHashSet();
            if (!idSet.Any()) return;

            lock (CacheLock)
            {
                _assets?.RemoveAll(a => idSet.Contains(a.Id));
            }
        }

        private static void UpsertAssetsUnsafe(IEnumerable<Asset> assets)
        {
            _assets ??= new List<Asset>();
            var indexById = _assets
                .Select((asset, index) => new { asset.Id, index })
                .Where(x => x.Id > 0)
                .ToDictionary(x => x.Id, x => x.index);

            foreach (var asset in assets)
            {
                RelinkAssetWorkerUnsafe(asset);

                if (asset.Id > 0 && indexById.TryGetValue(asset.Id, out int index))
                {
                    _assets[index] = asset;
                }
                else
                {
                    _assets.Add(asset);
                    if (asset.Id > 0) indexById[asset.Id] = _assets.Count - 1;
                }
            }

            _assets = _assets.OrderBy(a => a.Id).ToList();
        }

        private static void RebuildWorkerDepartmentsUnsafe()
        {
            _workerDepartments = _workers?
                .Select(w => w.pdp_adi)
                .Where(d => !string.IsNullOrWhiteSpace(d))
                .Distinct()
                .OrderBy(d => d)
                .ToList() ?? new List<string>();
        }

        private static void RelinkAllAssetWorkersUnsafe()
        {
            if (_assets == null) return;
            foreach (var asset in _assets) RelinkAssetWorkerUnsafe(asset);
        }

        private static void RelinkAssetsForWorkerUnsafe(int workerId)
        {
            if (_assets == null) return;
            foreach (var asset in _assets.Where(a => a.WorkerId == workerId))
            {
                RelinkAssetWorkerUnsafe(asset);
            }
        }

        private static void RelinkAssetWorkerUnsafe(Asset asset)
        {
            if (!asset.WorkerId.HasValue)
            {
                asset.Worker = null;
                return;
            }

            asset.Worker = _workers?.FirstOrDefault(w => w.Id == asset.WorkerId.Value);
        }
    }
}
