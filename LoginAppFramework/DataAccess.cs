using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace LoginAppFramework
{
    public static class DataAccess
    {
        #region App User Management
        public static AppUser GetAppUserByUsername(string username, string userConnectionString)
        {
            using var context = new InventoryDbContext(userConnectionString);
            return context.AppUsers.FirstOrDefault(u => u.Username.ToLower() == username.ToLower());
        }

        public static List<AppUser> GetAllAppUsers()
        {
            using var context = new InventoryDbContext(SessionManager.CurrentUserConnectionString);
            return context.AppUsers.ToList();
        }

        public static void SaveAppUser(AppUser user)
        {
            using var context = new InventoryDbContext(SessionManager.CurrentUserConnectionString);

            string normalizedUsername = user.Username?.Trim().ToLower();
            bool duplicateUsername = context.AppUsers.Any(u =>
                u.Id != user.Id &&
                u.Username != null &&
                u.Username.ToLower() == normalizedUsername);

            if (duplicateUsername)
            {
                throw new InvalidOperationException(
                    $"An app user with the username '{user.Username}' already exists.");
            }

            if (user.Id > 0)
            {
                var existingUser = context.AppUsers.Find(user.Id);
                if (existingUser == null)
                    throw new InvalidOperationException("The app user no longer exists.");

                existingUser.Username = user.Username?.Trim();
                existingUser.FullName = user.FullName;
                existingUser.EmployeeCode = user.EmployeeCode;
                existingUser.Role = user.Role;

                if (!string.IsNullOrWhiteSpace(user.PasswordHash))
                {
                    existingUser.PasswordHash = user.PasswordHash;
                }
            }
            else
            {
                user.Username = user.Username?.Trim();
                context.AppUsers.Add(user);
            }

            context.SaveChanges();
        }

        public static void DeleteAppUser(AppUser user)
        {
            using var context = new InventoryDbContext(SessionManager.CurrentUserConnectionString);
            context.AppUsers.Remove(user);
            context.SaveChanges();
        }
        #endregion

        #region Worker Management
        public static List<Worker> GetAllWorkers()
        {
            using var context = new InventoryDbContext(SessionManager.CurrentUserConnectionString);
            return context.Workers.OrderBy(w => w.per_adiper_soyadi).ToList();
        }

        public static Worker GetWorkerById(int workerId)
        {
            if (workerId <= 0) return null;
            using var context = new InventoryDbContext(SessionManager.CurrentUserConnectionString);
            return context.Workers.AsNoTracking().FirstOrDefault(w => w.Id == workerId);
        }

        public static void SaveWorker(Worker worker)
        {
            using var context = new InventoryDbContext(SessionManager.CurrentUserConnectionString);
            if (worker.Id > 0) { context.Workers.Update(worker); }
            else { context.Workers.Add(worker); }
            context.SaveChanges();
        }

        public static void DeleteWorker(Worker worker)
        {
            using var context = new InventoryDbContext(SessionManager.CurrentUserConnectionString);
            context.Workers.Remove(worker);
            context.SaveChanges();
        }

        public static List<int> DeleteWorkerAndUnassignAssets(int workerId, string changedByUser)
        {
            if (workerId <= 0) return new List<int>();

            using var context = new InventoryDbContext(SessionManager.CurrentUserConnectionString);
            using var transaction = context.Database.BeginTransaction();

            try
            {
                context.Database.ExecuteSqlRaw(
                    "EXEC sp_set_session_context @key=N'CurrentUser', @value=@user",
                    new SqlParameter("@user", changedByUser));

                var affectedAssets = context.Assets
                    .Where(a => a.WorkerId == workerId)
                    .ToList();

                foreach (var asset in affectedAssets)
                {
                    asset.ClearWorkerAssignment();
                }

                var worker = context.Workers.Find(workerId);
                if (worker != null)
                {
                    context.Workers.Remove(worker);
                }

                context.SaveChanges();
                transaction.Commit();
                return affectedAssets.Select(a => a.Id).ToList();
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }

        public static int BulkSetWorkersActiveState(IEnumerable<int> workerIds, bool isActive)
        {
            var ids = workerIds?
                .Where(id => id > 0)
                .Distinct()
                .ToList() ?? new List<int>();

            if (!ids.Any()) return 0;

            using var context = new InventoryDbContext(SessionManager.CurrentUserConnectionString);
            int affectedRows = 0;

            // Keep each IN clause comfortably below SQL Server's parameter limit.
            foreach (var batch in ids.Chunk(1000))
            {
                var batchIds = batch.ToArray();
                affectedRows += context.Workers
                    .Where(w => batchIds.Contains(w.Id) && w.IsActive != isActive)
                    .ExecuteUpdate(setters => setters
                        .SetProperty(w => w.IsActive, isActive));
            }

            return affectedRows;
        }

        public static async Task<int> SynchronizeWorkersFromRemoteAsync()
        {
            using var context = new InventoryDbContext(SessionManager.CurrentUserConnectionString);
            string mergeQuery = @"
                MERGE INTO dbo.Workers AS T
                USING (
                    SELECT 
                        p.per_kod, 
                        LTRIM(RTRIM(p.per_adi + ' ' + p.per_soyadi)) AS per_adiper_soyadi,
                        g.pgk_gorev_adi, 
                        d.pdp_adi,
                        -- 1899 means no exit date (active), any other year means inactive
                        CASE WHEN YEAR(p.per_cikis_tar) = 1899 THEN 1 ELSE 0 END AS is_active_flag
                    FROM 
                        [SRV50_29].[MikroDB_V16_01].[dbo].PERSONELLER AS p
                        LEFT JOIN [SRV50_29].[MikroDB_V16_01].[dbo].PERSONEL_GOREV_TANIMLARI AS g ON p.per_kim_gorev = g.pgk_gorev_kodu
                        LEFT JOIN [SRV50_29].[MikroDB_V16_01].[dbo].DEPARTMANLAR AS d ON p.per_dept_kod = d.pdp_kodu
                ) AS S
                ON T.per_kod = S.per_kod COLLATE DATABASE_DEFAULT

                WHEN MATCHED AND (
                    T.per_adiper_soyadi <> S.per_adiper_soyadi COLLATE DATABASE_DEFAULT OR
                    T.pgk_gorev_adi    <> S.pgk_gorev_adi    COLLATE DATABASE_DEFAULT OR
                    T.pdp_adi          <> S.pdp_adi          COLLATE DATABASE_DEFAULT OR
                    T.IsActive         <> S.is_active_flag
                ) THEN
                    UPDATE SET 
                        T.per_adiper_soyadi = S.per_adiper_soyadi,
                        T.pgk_gorev_adi     = S.pgk_gorev_adi,
                        T.pdp_adi           = S.pdp_adi,
                        T.IsActive          = S.is_active_flag

                WHEN NOT MATCHED BY TARGET THEN
                    INSERT (per_kod, per_adiper_soyadi, pgk_gorev_adi, pdp_adi, IsActive)
                    VALUES (S.per_kod, S.per_adiper_soyadi, S.pgk_gorev_adi, S.pdp_adi, S.is_active_flag)

                WHEN NOT MATCHED BY SOURCE AND T.IsActive = 1 THEN 
                    UPDATE SET T.IsActive = 0;
            ";
            int affectedRows = await context.Database.ExecuteSqlRawAsync(mergeQuery);
            return affectedRows;
        }
        #endregion

        #region Inventory Management
        public static List<Asset> GetAllAssets()
        {
            using var context = new InventoryDbContext(SessionManager.CurrentUserConnectionString);
            var assets = context.Assets
                .Include(a => a.Worker)
                .Include(a => a.History)
                .OrderBy(a => a.Id)
                .ToList();

            // --- AUTO-MIGRATION: Merge old statuses to "Anbarda" ---
            bool needsDBSave = false;
            
            // 1. Update Asset records
            var oldWarehouseAssets = assets.Where(a => a.Status == "Anbarda və İşlək" || a.Status == "Anbarda və Xarab").ToList();
            if (oldWarehouseAssets.Any())
            {
                foreach (var a in oldWarehouseAssets) { a.Status = "Anbarda"; }
                needsDBSave = true;
            }

            // 2. Update AssetStatus definitions
            var statusTable = context.AssetStatuses.ToList();
            var oldStatusRecords = statusTable.Where(s => s.Name == "Anbarda və İşlək" || s.Name == "Anbarda və Xarab").ToList();
            if (oldStatusRecords.Any())
            {
                // If "Anbarda" doesn't exist yet, rename one of them
                if (!statusTable.Any(s => s.Name == "Anbarda"))
                {
                    var first = oldStatusRecords.First();
                    first.Name = "Anbarda";
                    first.ColorHexCode = "#1E90FF"; // DodgerBlue
                    context.AssetStatuses.Update(first);
                    oldStatusRecords.Remove(first);
                }
                
                // Remove the remaining old ones
                if (oldStatusRecords.Any())
                {
                    context.AssetStatuses.RemoveRange(oldStatusRecords);
                }
                needsDBSave = true;
            }

            if (needsDBSave)
            {
                context.SaveChanges();
            }

            return assets;
        }

        public static Asset GetAssetById(int assetId)
        {
            if (assetId <= 0) return null;

            using var context = new InventoryDbContext(SessionManager.CurrentUserConnectionString);
            return context.Assets
                .AsNoTracking()
                .Include(a => a.Worker)
                .Include(a => a.History)
                .FirstOrDefault(a => a.Id == assetId);
        }

        public static List<Asset> GetAssetsByIds(IEnumerable<int> assetIds)
        {
            var ids = assetIds?
                .Where(id => id > 0)
                .Distinct()
                .ToList() ?? new List<int>();

            if (!ids.Any()) return new List<Asset>();

            using var context = new InventoryDbContext(SessionManager.CurrentUserConnectionString);
            var assets = new List<Asset>();

            foreach (var batch in ids.Chunk(1000))
            {
                var batchIds = batch.ToArray();
                assets.AddRange(
                    context.Assets
                        .AsNoTracking()
                        .Include(a => a.Worker)
                        .Include(a => a.History)
                        .Where(a => batchIds.Contains(a.Id))
                        .ToList());
            }

            return assets.OrderBy(a => a.Id).ToList();
        }

        public static List<AssignmentHistoryEntry> GetAssignmentHistoryEntries()
        {
            using var context = new InventoryDbContext(SessionManager.CurrentUserConnectionString);
            return context.AssignmentHistories
                .Include(h => h.Asset)
                .ToList();
        }

        public static List<string> GetAssetStatuses() { using var context = new InventoryDbContext(SessionManager.CurrentUserConnectionString); return context.AssetStatuses.Select(s => s.Name).OrderBy(name => name).ToList(); }
        public static Dictionary<string, string> GetAssetStatusColors() { using var context = new InventoryDbContext(SessionManager.CurrentUserConnectionString); return context.AssetStatuses.Where(s => s.ColorHexCode != null).AsEnumerable().ToDictionary(s => s.Name, s => s.ColorHexCode); }

        public static List<DeviceCategory> GetDeviceCategories()
        {
            using var context = new InventoryDbContext(SessionManager.CurrentUserConnectionString);
            return context.DeviceCategories
                .OrderBy(c => c.SortOrder)
                .ThenBy(c => c.Name)
                .ToList();
        }

        public static Dictionary<string, string> GetDeviceCategoryIcons() { using var context = new InventoryDbContext(SessionManager.CurrentUserConnectionString); return context.DeviceCategories.Where(c => c.Icon != null).AsEnumerable().ToDictionary(c => c.Name, c => c.Icon); }
        public static Dictionary<string, int> GetCategoryDefaultLifecycles() { using var context = new InventoryDbContext(SessionManager.CurrentUserConnectionString); return context.DeviceCategories.Where(c => c.DefaultUsefulLifeYears.HasValue).AsEnumerable().ToDictionary(c => c.Name, c => c.DefaultUsefulLifeYears.Value); }
        public static Dictionary<string, List<string>> GetCategoryCustomFields() { using var context = new InventoryDbContext(SessionManager.CurrentUserConnectionString); return context.CategoryCustomFields.GroupBy(cf => cf.CategoryName).ToDictionary(g => g.Key, g => g.Select(cf => cf.FieldName).ToList()); }
        public static List<AlertRule> GetAlertRules() { using var context = new InventoryDbContext(SessionManager.CurrentUserConnectionString); return context.AlertRules.Where(r => r.IsEnabled).ToList(); }

        public static void SaveAsset(Asset asset, string changedByUser)
        {
            using var context = new InventoryDbContext(SessionManager.CurrentUserConnectionString);
            context.Database.ExecuteSqlRaw("EXEC sp_set_session_context @key=N'CurrentUser', @value=@user", new SqlParameter("@user", changedByUser));

            if (asset.Id > 0)
            {
                var originalAsset = context.Assets
                                        .Include(a => a.History)
                                        .FirstOrDefault(a => a.Id == asset.Id);

                if (originalAsset != null)
                {
                    context.Entry(originalAsset).CurrentValues.SetValues(asset);
                    originalAsset.WorkerId = asset.WorkerId;

                    var entriesToDelete = originalAsset.History.Where(h_db => !asset.History.Any(h_ui => h_ui.Id == h_db.Id)).ToList();
                    foreach (var entry in entriesToDelete)
                    {
                        context.AssignmentHistories.Remove(entry);
                    }

                    var entriesToAdd = asset.History.Where(h_ui => h_ui.Id == 0).ToList();
                    foreach (var entry in entriesToAdd)
                    {
                        originalAsset.History.Add(entry);
                    }
                }
            }
            else
            {
                asset.Worker = null;
                context.Assets.Add(asset);
            }
            context.SaveChanges();
        }

        public static void InsertAssets(List<Asset> assets, string changedByUser)
        {
            if (assets == null || assets.Count == 0) return;

            using var context = new InventoryDbContext(SessionManager.CurrentUserConnectionString);
            using var transaction = context.Database.BeginTransaction();

            try
            {
                context.Database.ExecuteSqlRaw(
                    "EXEC sp_set_session_context @key=N'CurrentUser', @value=@user",
                    new SqlParameter("@user", changedByUser));

                foreach (var asset in assets)
                {
                    // WorkerId is the persisted source of truth. A Worker navigation object
                    // from AppData belongs to a different DbContext and must not be inserted.
                    asset.Worker = null;
                }

                context.Assets.AddRange(assets);
                context.SaveChanges();
                transaction.Commit();
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }

        public static void DeleteAsset(Asset asset)
        {
            if (asset == null || asset.Id <= 0)
                return;

            using var context =
                new InventoryDbContext(
                    SessionManager.CurrentUserConnectionString);

            context.Database.ExecuteSqlRaw(
                "EXEC sp_set_session_context @key=N'CurrentUser', @value=@user",
                new SqlParameter(
                    "@user",
                    SessionManager.CurrentUser?.FullName ??
                    SessionManager.CurrentUser?.Username ??
                    "Sistem"));

            var existing = context.Assets
                .Include(current => current.History)
                .FirstOrDefault(current =>
                    current.Id == asset.Id);

            if (existing == null)
                return;

            context.Assets.Remove(existing);
            context.SaveChanges();
        }

        public static void BulkDeleteAssets(List<Asset> assetsToDelete)
        {
            var ids = assetsToDelete?
                .Where(asset => asset?.Id > 0)
                .Select(asset => asset.Id)
                .Distinct()
                .ToList()
                ?? new List<int>();

            if (ids.Count == 0)
                return;

            using var context =
                new InventoryDbContext(
                    SessionManager.CurrentUserConnectionString);

            context.Database.ExecuteSqlRaw(
                "EXEC sp_set_session_context @key=N'CurrentUser', @value=@user",
                new SqlParameter(
                    "@user",
                    SessionManager.CurrentUser?.FullName ??
                    SessionManager.CurrentUser?.Username ??
                    "Sistem"));

            foreach (var batch in ids.Chunk(1000))
            {
                var batchIds = batch.ToArray();

                var existing = context.Assets
                    .Include(asset => asset.History)
                    .Where(asset => batchIds.Contains(asset.Id))
                    .ToList();

                context.Assets.RemoveRange(existing);
            }

            context.SaveChanges();
        }

        public static void BulkSaveAssets(List<Asset> assetsToSave, string changedByUser)
        {
            if (assetsToSave == null || !assetsToSave.Any()) return;

            using var context = new InventoryDbContext(SessionManager.CurrentUserConnectionString);
            using var transaction = context.Database.BeginTransaction();

            try
            {
                context.Database.ExecuteSqlRaw(
                    "EXEC sp_set_session_context @key=N'CurrentUser', @value=@user",
                    new SqlParameter("@user", changedByUser));

                var assetIds = assetsToSave
                    .Where(a => a.Id > 0)
                    .Select(a => a.Id)
                    .Distinct()
                    .ToList();

                // One preload query replaces the previous per-asset SELECT (N+1 pattern).
                var existingAssets = assetIds.Any()
                    ? context.Assets
                        .Include(a => a.History)
                        .Where(a => assetIds.Contains(a.Id))
                        .ToDictionary(a => a.Id)
                    : new Dictionary<int, Asset>();

                foreach (var asset in assetsToSave)
                {
                    if (asset.Id <= 0)
                    {
                        asset.Worker = null;
                        context.Assets.Add(asset);
                        continue;
                    }

                    if (!existingAssets.TryGetValue(asset.Id, out var originalAsset))
                    {
                        continue;
                    }

                    context.Entry(originalAsset).CurrentValues.SetValues(asset);
                    originalAsset.WorkerId = asset.WorkerId;

                    if (asset.History != null)
                    {
                        foreach (var entry in asset.History.Where(h => h.Id == 0))
                        {
                            entry.AssetId = originalAsset.Id;
                            entry.Asset = originalAsset;
                            originalAsset.History.Add(entry);
                        }
                    }
                }

                context.SaveChanges();
                transaction.Commit();
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }

        public static List<AssetLog> GetAssetLogs()
        {
            using var context = new InventoryDbContext(SessionManager.CurrentUserConnectionString);
            return context.AssetLogs.OrderByDescending(log => log.ChangeDate).ToList();
        }

        public static List<UnifiedHistoryEntry> GetUnifiedHistory()
        {
            using var context = new InventoryDbContext(SessionManager.CurrentUserConnectionString);
            return context.Set<UnifiedHistoryEntry>().FromSqlRaw("EXEC dbo.GetUnifiedHistory").ToList();
        }

        public static HashSet<string> GetExistingAssetCodes()
        {
            using var context = new InventoryDbContext(SessionManager.CurrentUserConnectionString);
            return context.Assets
                .AsNoTracking()
                .Where(a => a.VesaitinKodu != null && a.VesaitinKodu != "")
                .Select(a => a.VesaitinKodu)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
        }

        public static bool AssetCodeExists(string assetCode)
        {
            if (string.IsNullOrWhiteSpace(assetCode)) return false;

            using var context = new InventoryDbContext(SessionManager.CurrentUserConnectionString);
            return context.Assets.AsNoTracking().Any(a => a.VesaitinKodu == assetCode);
        }

        public static int RestoreDeletedAsset(Asset asset, string changedByUser)
        {
            if (asset == null) throw new ArgumentNullException(nameof(asset));

            using var context = new InventoryDbContext(SessionManager.CurrentUserConnectionString);
            using var transaction = context.Database.BeginTransaction();

            try
            {
                context.Database.ExecuteSqlRaw(
                    "EXEC sp_set_session_context @key=N'CurrentUser', @value=@user",
                    new SqlParameter("@user", changedByUser));

                if (!string.IsNullOrWhiteSpace(asset.VesaitinKodu) &&
                    context.Assets.Any(a => a.VesaitinKodu == asset.VesaitinKodu))
                {
                    throw new InvalidOperationException(
                        $"'{asset.VesaitinKodu}' kodlu vəsait artıq mövcuddur.");
                }

                asset.Worker = null;
                context.Assets.Add(asset);
                context.SaveChanges();
                transaction.Commit();
                return asset.Id;
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }

        #endregion
    }
}