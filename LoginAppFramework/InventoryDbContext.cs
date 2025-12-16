using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace LoginAppFramework
{
    public class InventoryDbContext : DbContext
    {
        private readonly string _connectionString;

        public InventoryDbContext(string connectionString)
        {
            _connectionString = connectionString;
        }

        public InventoryDbContext()
        {
            var exePath = Assembly.GetExecutingAssembly().Location;
            var directory = Path.GetDirectoryName(exePath);

            var builder = new ConfigurationBuilder()
                .SetBasePath(directory)
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);

            IConfigurationRoot configuration = builder.Build();

        }

        public DbSet<AppUser> AppUsers { get; set; }
        public DbSet<Asset> Assets { get; set; }
        public DbSet<Worker> Workers { get; set; }
        public DbSet<Supplier> Suppliers { get; set; }
        public DbSet<AssetStatus> AssetStatuses { get; set; }
        public DbSet<Department> Departments { get; set; }
        public DbSet<DeviceCategory> DeviceCategories { get; set; }
        public DbSet<CategoryCustomField> CategoryCustomFields { get; set; }
        public DbSet<AlertRule> AlertRules { get; set; }
        public DbSet<AssetLog> AssetLogs { get; set; }
        public DbSet<AssignmentHistoryEntry> AssignmentHistories { get; set; }
        public DbSet<UnifiedHistoryEntry> UnifiedHistoryEntries { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
                optionsBuilder.UseSqlServer(_connectionString, options =>
                {
                    options.CommandTimeout(120);
                });
            }
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Asset>(entity =>
            {
                entity.ToTable(tb => tb.HasTrigger("Trigger_Assets_AuditLog"));

                entity.HasMany(a => a.History)
                      .WithOne(h => h.Asset)
                      .HasForeignKey(h => h.AssetId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(a => a.Worker)
                      .WithMany()
                      .HasForeignKey(a => a.WorkerId)
                      .OnDelete(DeleteBehavior.SetNull);

                var jsonOptions = new System.Text.Json.JsonSerializerOptions();
                entity.Property(e => e.CustomFields).HasConversion(
                    v => System.Text.Json.JsonSerializer.Serialize(v, jsonOptions),
                    v => System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(v, jsonOptions) ?? new Dictionary<string, string>()
                );
                entity.Property(e => e.MaintenanceHistory).HasConversion(
                    v => System.Text.Json.JsonSerializer.Serialize(v, jsonOptions),
                    v => System.Text.Json.JsonSerializer.Deserialize<List<MaintenanceRecord>>(v, jsonOptions) ?? new List<MaintenanceRecord>()
                );
            });

            modelBuilder.Entity<AssignmentHistoryEntry>(entity =>
            {
                entity.ToTable("AssignmentHistory");
                entity.Property(e => e.Action).HasConversion<int>();
            });

            modelBuilder.Entity<AssetLog>(entity =>
            {
                entity.ToTable("Assets_upd_del");
                // The .HasNoKey() has been removed. EF will now use the 'Id' property as the primary key by convention.
            });

            modelBuilder.Entity<AssetStatus>(e => e.ToTable("L_AssetStatuses"));
            modelBuilder.Entity<Department>(e => e.ToTable("L_Departments"));
            modelBuilder.Entity<DeviceCategory>(e => e.ToTable("L_DeviceCategories"));
            modelBuilder.Entity<CategoryCustomField>(e => e.ToTable("L_CategoryCustomFields"));
            modelBuilder.Entity<AlertRule>(e => e.ToTable("L_AlertRules"));
            modelBuilder.Entity<UnifiedHistoryEntry>().HasNoKey();
        }
    }
}