using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

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
            if (user.Id > 0 && context.AppUsers.Any(u => u.Id != user.Id && u.Username.ToLower() == user.Username.ToLower()))
            {
                throw new Exception($"An app user with the username '{user.Username}' already exists.");
            }
            if (user.Id > 0)
            {
                var existingUser = context.AppUsers.Find(user.Id);
                if (existingUser != null)
                {
                    existingUser.Username = user.Username;
                    existingUser.FullName = user.FullName;
                    existingUser.EmployeeCode = user.EmployeeCode;
                    if (!string.IsNullOrWhiteSpace(user.PasswordHash))
                    {
                        existingUser.PasswordHash = user.PasswordHash;
                    }
                }
            }
            else { context.AppUsers.Add(user); }
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
                context.Assets.Add(asset);
            }
            context.SaveChanges();
        }

        public static void SaveAssetInTransaction(InventoryDbContext context, Asset asset)
        {
            if (asset.Id > 0) { context.Assets.Update(asset); }
            else { context.Assets.Add(asset); }
        }

        public static void DeleteAsset(Asset asset)
        {
            using var context = new InventoryDbContext(SessionManager.CurrentUserConnectionString);
            context.Database.ExecuteSqlRaw("EXEC sp_set_session_context @key=N'CurrentUser', @value=@user", new SqlParameter("@user", SessionManager.CurrentUser.FullName));
            context.Assets.Remove(asset);
            context.SaveChanges();
        }

        public static void BulkDeleteAssets(List<Asset> assetsToDelete)
        {
            if (assetsToDelete == null || !assetsToDelete.Any()) return;
            using var context = new InventoryDbContext(SessionManager.CurrentUserConnectionString);
            context.Database.ExecuteSqlRaw("EXEC sp_set_session_context @key=N'CurrentUser', @value=@user", new SqlParameter("@user", SessionManager.CurrentUser.FullName));
            context.Assets.RemoveRange(assetsToDelete);
            context.SaveChanges();
        }

        public static void BulkSaveAssets(List<Asset> assetsToSave, string changedByUser)
        {
            if (assetsToSave == null || !assetsToSave.Any()) return;
            using var context = new InventoryDbContext(SessionManager.CurrentUserConnectionString);
            context.Database.ExecuteSqlRaw("EXEC sp_set_session_context @key=N'CurrentUser', @value=@user", new SqlParameter("@user", changedByUser));

            foreach (var asset in assetsToSave)
            {
                var originalAsset = context.Assets
                                        .Include(a => a.History)
                                        .FirstOrDefault(a => a.Id == asset.Id);

                if (originalAsset != null)
                {
                    // Update all properties except navigation properties
                    context.Entry(originalAsset).CurrentValues.SetValues(asset);
                    originalAsset.WorkerId = asset.WorkerId;

                    // Handle history entries if they exist
                    if (asset.History != null && asset.History.Any())
                    {
                        var entriesToAdd = asset.History.Where(h_ui => h_ui.Id == 0).ToList();
                        foreach (var entry in entriesToAdd)
                        {
                            originalAsset.History.Add(entry);
                        }
                    }
                }
            }
            context.SaveChanges();
        }

        public static List<AssetLog> GetAssetLogs()
        {
            using var context = new InventoryDbContext(SessionManager.CurrentUserConnectionString);
            return context.AssetLogs.OrderByDescending(log => log.ChangeDate).ToList();
        }

        public static void DeleteAssetLog(AssetLog logToDelete)
        {
            if (logToDelete == null) return;
            using var context = new InventoryDbContext(SessionManager.CurrentUserConnectionString);
            var logInDb = context.AssetLogs.Find(logToDelete.Id);
            if (logInDb != null)
            {
                context.AssetLogs.Remove(logInDb);
                context.SaveChanges();
            }
        }

        public static List<UnifiedHistoryEntry> GetUnifiedHistory()
        {
            using var context = new InventoryDbContext(SessionManager.CurrentUserConnectionString);
            return context.Set<UnifiedHistoryEntry>().FromSqlRaw("EXEC dbo.GetUnifiedHistory").ToList();
        }

        public static bool RestoreAssetFromLog(AssetLog logEntry)
        {
            using var context = new InventoryDbContext(SessionManager.CurrentUserConnectionString);
            using var transaction = context.Database.BeginTransaction();
            try
            {
                var existingAsset = context.Assets.FirstOrDefault(a => a.VesaitinKodu == logEntry.VesaitinKodu);
                if (existingAsset != null)
                {
                    MessageBox.Show($"'{logEntry.VesaitinKodu}' kodlu vəsait artıq əsas cədvəldə mövcuddur. Silinmiş elementi eyni kodla bərpa etmək olmaz.", "Bərpa edilmədi", MessageBoxButton.OK, MessageBoxImage.Warning);
                    transaction.Rollback();
                    return false;
                }
                var assetToRestore = new Asset
                {
                    VesaitinKodu = logEntry.VesaitinKodu,
                    VesaitinAdi = logEntry.VesaitinAdi,
                    ITAvadanliqlarininSeriyaNomresi = logEntry.ITAvadanliqlarininSeriyaNomresi,
                    Kateqoriya = logEntry.Kateqoriya,
                    TehkimOlunanEmekdas = logEntry.TehkimOlunanEmekdas,
                    Vezifesi = logEntry.Vezifesi,
                    BolmeShobeDepartment = logEntry.BolmeShobeDepartment,
                    YerleshmeYeri = logEntry.YerleshmeYeri,
                    Erazi = logEntry.Erazi,
                    Status = "Anbarda"
                };
                context.Assets.Add(assetToRestore);
                var logToDelete = context.AssetLogs.FirstOrDefault(log =>
                    log.VesaitinKodu == logEntry.VesaitinKodu &&
                    log.ChangeDate == logEntry.ChangeDate
                );
                if (logToDelete != null)
                {
                    context.AssetLogs.Remove(logToDelete);
                }
                context.SaveChanges();
                transaction.Commit();
                return true;
            }
            catch (Exception ex)
            {
                transaction.Rollback();
                MessageBox.Show($"Məlumat bazası xətası baş verdi:\n\n{ex.Message}", "Məlumat Bazası Xətası", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }
        #endregion
    }
}