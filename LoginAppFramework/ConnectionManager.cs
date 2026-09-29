using Microsoft.Data.SqlClient;
using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace LoginAppFramework
{
    public static class ConnectionManager
    {
        public static string DynamicConnectionString { get; private set; }
        public static bool IsConfigured { get; private set; }

        private static readonly string AppDataFolder =
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "InventoryApp");

        private static readonly string SettingsFilePath =
            Path.Combine(AppDataFolder, "settings.json");

        public static void LoadSettings()
        {
            DynamicConnectionString = null;
            IsConfigured = false;

            if (!File.Exists(SettingsFilePath))
                return;

            try
            {
                string json = File.ReadAllText(SettingsFilePath);
                var settings = JsonSerializer.Deserialize<ConnectionSettings>(json);

                if (settings == null ||
                    string.IsNullOrWhiteSpace(settings.ServerAddress) ||
                    string.IsNullOrWhiteSpace(settings.SqlUsername) ||
                    string.IsNullOrWhiteSpace(settings.EncryptedSqlPassword))
                {
                    return;
                }

                string decryptedPassword =
                    EncryptionHelper.Decrypt(settings.EncryptedSqlPassword);

                if (string.IsNullOrWhiteSpace(decryptedPassword))
                    return;

                DynamicConnectionString = BuildConnectionString(
                    settings.ServerAddress,
                    settings.SqlUsername,
                    decryptedPassword);

                IsConfigured = true;
            }
            catch
            {
                DynamicConnectionString = null;
                IsConfigured = false;
            }
        }

        public static string GetActiveConnectionString()
        {
            if (!IsConfigured || string.IsNullOrWhiteSpace(DynamicConnectionString))
                LoadSettings();

            if (!IsConfigured || string.IsNullOrWhiteSpace(DynamicConnectionString))
            {
                throw new InvalidOperationException(
                    "Verilənlər bazası bağlantısı konfiqurasiya edilməyib.");
            }

            return DynamicConnectionString;
        }

        public static void SaveSettings(ConnectionSettings settings)
        {
            if (settings == null)
                throw new ArgumentNullException(nameof(settings));

            if (string.IsNullOrWhiteSpace(settings.ServerAddress) ||
                string.IsNullOrWhiteSpace(settings.SqlUsername) ||
                string.IsNullOrWhiteSpace(settings.SqlPassword))
            {
                throw new ArgumentException(
                    "Server, SQL istifadəçi adı və şifrə boş ola bilməz.",
                    nameof(settings));
            }

            Directory.CreateDirectory(AppDataFolder);

            settings.EncryptedSqlPassword =
                EncryptionHelper.Encrypt(settings.SqlPassword);

            string json = JsonSerializer.Serialize(
                settings,
                new JsonSerializerOptions { WriteIndented = true });

            File.WriteAllText(SettingsFilePath, json);

            DynamicConnectionString = BuildConnectionString(
                settings.ServerAddress,
                settings.SqlUsername,
                settings.SqlPassword);

            IsConfigured = true;
        }

        public static async Task<bool> TestConnection(ConnectionSettings settings)
        {
            if (settings == null ||
                string.IsNullOrWhiteSpace(settings.ServerAddress) ||
                string.IsNullOrWhiteSpace(settings.SqlUsername) ||
                string.IsNullOrWhiteSpace(settings.SqlPassword))
            {
                return false;
            }

            try
            {
                string testConnectionString = BuildConnectionString(
                    settings.ServerAddress,
                    settings.SqlUsername,
                    settings.SqlPassword);

                await using var connection = new SqlConnection(testConnectionString);
                await connection.OpenAsync();
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static void BeginReconfiguration()
        {
            DynamicConnectionString = null;
            IsConfigured = false;
        }

        private static string BuildConnectionString(
            string server,
            string username,
            string password)
        {
            var builder = new SqlConnectionStringBuilder
            {
                DataSource = server,
                UserID = username,
                Password = password,
                InitialCatalog = "InventoryDB",
                IntegratedSecurity = false,
                TrustServerCertificate = true,
                ConnectTimeout = 15
            };

            return builder.ConnectionString;
        }
    }
}
