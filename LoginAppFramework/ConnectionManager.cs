using Microsoft.Data.SqlClient;
using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration; // Add this if missing

namespace LoginAppFramework
{
    public static class ConnectionManager
    {
        public static string DynamicConnectionString { get; private set; }
        public static bool IsConfigured { get; set; }

        private static readonly string AppDataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "InventoryApp");
        private static readonly string SettingsFilePath = Path.Combine(AppDataFolder, "settings.json");

        // This new helper method is for the local AppUsers table, ensuring it always has credentials
        public static string GetDefaultConnectionString()
        {
            var builder = new ConfigurationBuilder().SetBasePath(Directory.GetCurrentDirectory()).AddJsonFile("appsettings.json");
            IConfigurationRoot config = builder.Build();
            return config.GetConnectionString("DefaultConnection");
        }

        public static void LoadSettings()
        {
            if (File.Exists(SettingsFilePath))
            {
                try
                {
                    string json = File.ReadAllText(SettingsFilePath);
                    var settings = JsonSerializer.Deserialize<ConnectionSettings>(json);
                    if (settings != null && !string.IsNullOrEmpty(settings.ServerAddress) && !string.IsNullOrEmpty(settings.EncryptedSqlPassword))
                    {
                        // DECRYPT the password from the file for use in the app
                        string decryptedPassword = EncryptionHelper.Decrypt(settings.EncryptedSqlPassword);
                        DynamicConnectionString = BuildConnectionString(settings.ServerAddress, settings.SqlUsername, decryptedPassword);

                        IsConfigured = true;
                        return;
                    }
                }
                catch { /* Fail silently */ }
            }
            IsConfigured = false;
        }

        public static void SaveSettings(ConnectionSettings settings)
        {
            Directory.CreateDirectory(AppDataFolder);

            // Before saving, ENCRYPT the plaintext password that the user typed in the form.
            settings.EncryptedSqlPassword = EncryptionHelper.Encrypt(settings.SqlPassword);

            string json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(SettingsFilePath, json);

            // Build the connection string with the unencrypted password for the current session to use immediately
            DynamicConnectionString = BuildConnectionString(settings.ServerAddress, settings.SqlUsername, settings.SqlPassword);
            IsConfigured = true;
        }

        public static async Task<bool> TestConnection(ConnectionSettings settings)
        {
            try
            {
                // Test connection with the plaintext password from the form
                string testConnectionString = BuildConnectionString(settings.ServerAddress, settings.SqlUsername, settings.SqlPassword);
                await using var connection = new SqlConnection(testConnectionString);
                await connection.OpenAsync();
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static string BuildConnectionString(string server, string username, string password)
        {
            var builder = new SqlConnectionStringBuilder
            {
                DataSource = server,
                UserID = username,
                Password = password,
                InitialCatalog = "InventoryDB",
                IntegratedSecurity = false,
                TrustServerCertificate = true,
                ConnectTimeout = 15 // Increased timeout for potentially slower initial connections
            };
            return builder.ConnectionString;
        }
    }
}