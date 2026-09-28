using System.Collections.Generic;
using System.Linq;

namespace LoginAppFramework
{
    public static class AppData
    {
        private static List<Asset> _assets;
        private static List<Worker> _workers;
        private static List<string> _assetStatuses;
        private static List<string> _workerDepartments;
        private static Dictionary<string, string> _assetStatusColors;
        private static Dictionary<string, int> _categoryDefaultLifecycles;
        private static Dictionary<string, List<string>> _categoryCustomFields;
        private static Dictionary<string, string> _deviceCategoryIcons;
        private static List<DeviceCategory> _hierarchicalCategories;

        public static void LoadAllData()
        {
            if (string.IsNullOrEmpty(SessionManager.CurrentUserConnectionString)) return;

            _assets = DataAccess.GetAllAssets();
            _workers = DataAccess.GetAllWorkers();
            _assetStatuses = DataAccess.GetAssetStatuses();
            var allCategories = DataAccess.GetDeviceCategories();
            _categoryCustomFields = DataAccess.GetCategoryCustomFields();
            _assetStatusColors = DataAccess.GetAssetStatusColors();
            _deviceCategoryIcons = DataAccess.GetDeviceCategoryIcons();
            _categoryDefaultLifecycles = DataAccess.GetCategoryDefaultLifecycles();

            _hierarchicalCategories = new List<DeviceCategory>();
            var categoryLookup = allCategories.ToDictionary(c => c.Id);
            foreach (var category in allCategories)
            {
                if (category.ParentId.HasValue && categoryLookup.TryGetValue(category.ParentId.Value, out var parent))
                {
                    parent.Subcategories.Add(category);
                }
                else
                {
                    _hierarchicalCategories.Add(category);
                }
            }

            if (_assets != null)
            {
                var assetsToUpdate = new List<Asset>();
                foreach (var asset in _assets)
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
                    if (needsUpdate)
                    {
                        assetsToUpdate.Add(asset);
                    }
                }
                if (assetsToUpdate.Any())
                {
                    DataAccess.BulkSaveAssets(assetsToUpdate, SessionManager.CurrentUser.FullName);
                }
            }

            if (_workers != null)
            {
                _workerDepartments = _workers
                    .Select(w => w.pdp_adi)
                    .Where(d => !string.IsNullOrWhiteSpace(d))
                    .Distinct()
                    .OrderBy(d => d)
                    .ToList();
            }
            else
            {
                _workerDepartments = new List<string>();
            }
        }

        public static void ClearData()
        {
            _assets = null; _workers = null; _assetStatuses = null;
            _workerDepartments = null; _categoryCustomFields = null;
            _deviceCategoryIcons = null; _assetStatusColors = null; _categoryDefaultLifecycles = null;
            _hierarchicalCategories = null;
        }

        public static List<Asset> GetAssets() => _assets?.ToList() ?? new List<Asset>();
        public static List<Worker> GetWorkers() => _workers?.ToList() ?? new List<Worker>();
        public static List<string> GetAssetStatuses() => _assetStatuses?.ToList() ?? new List<string>();
        public static List<string> GetWorkerDepartments() => _workerDepartments?.ToList() ?? new List<string>();

        public static List<string> GetDeviceCategories()
        {
            if (_hierarchicalCategories == null) return new List<string>();
            static IEnumerable<string> Flatten(IEnumerable<DeviceCategory> cats) =>
                cats.SelectMany(c => new[] { c.Name }.Concat(Flatten(c.Subcategories)));
            return Flatten(_hierarchicalCategories).OrderBy(name => name).ToList();
        }

        public static List<DeviceCategory> GetHierarchicalCategories() => _hierarchicalCategories ?? new List<DeviceCategory>();
        public static Dictionary<string, List<string>> GetCategoryCustomFields() => _categoryCustomFields ?? new Dictionary<string, List<string>>();
        public static Dictionary<string, string> GetDeviceCategoryIcons() => _deviceCategoryIcons ?? new Dictionary<string, string>();
        public static Dictionary<string, string> GetAssetStatusColors() => _assetStatusColors ?? new Dictionary<string, string>();
        public static Dictionary<string, int> GetCategoryDefaultLifecycles() => _categoryDefaultLifecycles ?? new Dictionary<string, int>();

        public static void SaveAndRefreshAsset(Asset asset)
        {
            DataAccess.SaveAsset(asset, SessionManager.CurrentUser.FullName);
            LoadAllData();
        }

        public static void BulkSaveAndRefreshAssets(List<Asset> assets)
        {
            DataAccess.BulkSaveAssets(assets, SessionManager.CurrentUser.FullName);
            LoadAllData();
        }

        public static void DeleteAndRefreshAsset(Asset asset)
        {
            DataAccess.DeleteAsset(asset);
            LoadAllData();
        }

        public static void SaveAndRefreshWorker(Worker worker)
        {
            DataAccess.SaveWorker(worker);
            LoadAllData();
        }

        public static void DeleteAndRefreshWorker(Worker worker)
        {
            DataAccess.DeleteWorker(worker);
            LoadAllData();
        }
    }
}