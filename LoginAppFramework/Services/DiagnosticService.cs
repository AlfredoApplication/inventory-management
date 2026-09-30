using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;

namespace LoginAppFramework
{
    public static class DiagnosticService
    {
        private static readonly object Sync = new();

        private static readonly string FolderPath =
            Path.Combine(
                Environment.GetFolderPath(
                    Environment.SpecialFolder.LocalApplicationData),
                "InventoryManagement",
                "Diagnostics");

        private static readonly string LogFilePath =
            Path.Combine(FolderPath, "application.log");

        public static string LogPath => LogFilePath;

        public static void Initialize()
        {
            Directory.CreateDirectory(FolderPath);

            Log(
                "Application",
                $"Application started. Version={GetVersion()}");
        }

        public static void Log(
            string category,
            string message,
            Exception exception = null)
        {
            try
            {
                Directory.CreateDirectory(FolderPath);

                var builder = new StringBuilder();
                builder.Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"));
                builder.Append(" [");
                builder.Append(category ?? "General");
                builder.Append("] ");
                builder.AppendLine(message ?? string.Empty);

                if (exception != null)
                {
                    builder.AppendLine(
                        SanitizeException(exception));
                }

                lock (Sync)
                {
                    File.AppendAllText(
                        LogFilePath,
                        builder.ToString(),
                        Encoding.UTF8);
                }
            }
            catch
            {
                // Diagnostics must never crash the application.
            }
        }

        public static string ExportPackage(
            string destinationZipPath,
            Exception exception = null,
            string context = null)
        {
            if (string.IsNullOrWhiteSpace(destinationZipPath))
            {
                throw new ArgumentException(
                    "Diaqnostika faylının yolu boş ola bilməz.",
                    nameof(destinationZipPath));
            }

            string tempFolder = Path.Combine(
                Path.GetTempPath(),
                "InventoryManagementDiagnostics",
                Guid.NewGuid().ToString("N"));

            Directory.CreateDirectory(tempFolder);

            try
            {
                CopyLog(tempFolder);
                WriteEnvironmentInfo(tempFolder);
                WriteConnectionInfo(tempFolder);
                WriteExceptionInfo(
                    tempFolder,
                    exception,
                    context);

                string directory =
                    Path.GetDirectoryName(destinationZipPath);

                if (!string.IsNullOrWhiteSpace(directory))
                    Directory.CreateDirectory(directory);

                if (File.Exists(destinationZipPath))
                    File.Delete(destinationZipPath);

                ZipFile.CreateFromDirectory(
                    tempFolder,
                    destinationZipPath,
                    CompressionLevel.Optimal,
                    includeBaseDirectory: false);

                return destinationZipPath;
            }
            finally
            {
                try
                {
                    Directory.Delete(
                        tempFolder,
                        recursive: true);
                }
                catch
                {
                    // Best effort cleanup.
                }
            }
        }

        private static void CopyLog(string tempFolder)
        {
            if (!File.Exists(LogFilePath))
                return;

            lock (Sync)
            {
                File.Copy(
                    LogFilePath,
                    Path.Combine(tempFolder, "application.log"),
                    overwrite: true);
            }
        }

        private static void WriteEnvironmentInfo(
            string tempFolder)
        {
            var process = Process.GetCurrentProcess();

            var info = new Dictionary<string, object>
            {
                ["GeneratedAt"] = DateTime.Now,
                ["ApplicationVersion"] = GetVersion(),
                ["OperatingSystem"] = RuntimeInformation.OSDescription,
                ["OSArchitecture"] = RuntimeInformation.OSArchitecture.ToString(),
                ["ProcessArchitecture"] = RuntimeInformation.ProcessArchitecture.ToString(),
                ["DotNetVersion"] = Environment.Version.ToString(),
                ["MachineName"] = Environment.MachineName,
                ["ProcessId"] = Environment.ProcessId,
                ["WorkingSetMB"] = Math.Round(
                    process.WorkingSet64 / 1024d / 1024d,
                    2),
                ["CurrentUser"] =
                    SessionManager.CurrentUser?.Username ?? "anonymous",
                ["CurrentRole"] =
                    SessionManager.CurrentUser?.Role ?? "none",
                ["DatabaseState"] =
                    ConnectionHealthService.DatabaseState.ToString(),
                ["DatabaseCheckedAt"] =
                    ConnectionHealthService.DatabaseCheckedAt,
                ["HrState"] =
                    ConnectionHealthService.HrState.ToString(),
                ["HrCheckedAt"] =
                    ConnectionHealthService.HrCheckedAt
            };

            File.WriteAllText(
                Path.Combine(
                    tempFolder,
                    "environment.json"),
                JsonSerializer.Serialize(
                    info,
                    new JsonSerializerOptions
                    {
                        WriteIndented = true
                    }),
                Encoding.UTF8);
        }

        private static void WriteConnectionInfo(
            string tempFolder)
        {
            try
            {
                string server = "not-configured";
                string database = "InventoryDB";

                if (ConnectionManager.IsConfigured &&
                    !string.IsNullOrWhiteSpace(
                        ConnectionManager.DynamicConnectionString))
                {
                    var builder =
                        new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(
                            ConnectionManager.DynamicConnectionString);

                    server = builder.DataSource;
                    database = builder.InitialCatalog;
                }

                var info = new
                {
                    Server = server,
                    Database = database,
                    DatabaseState =
                        ConnectionHealthService.DatabaseState.ToString(),
                    DatabaseError =
                        ConnectionHealthService.DatabaseError,
                    HrState =
                        ConnectionHealthService.HrState.ToString(),
                    HrError =
                        ConnectionHealthService.HrError
                };

                File.WriteAllText(
                    Path.Combine(
                        tempFolder,
                        "connection.json"),
                    JsonSerializer.Serialize(
                        info,
                        new JsonSerializerOptions
                        {
                            WriteIndented = true
                        }),
                    Encoding.UTF8);
            }
            catch (Exception ex)
            {
                File.WriteAllText(
                    Path.Combine(
                        tempFolder,
                        "connection.txt"),
                    $"Connection metadata unavailable: {ex.Message}",
                    Encoding.UTF8);
            }
        }

        private static void WriteExceptionInfo(
            string tempFolder,
            Exception exception,
            string context)
        {
            if (exception == null &&
                string.IsNullOrWhiteSpace(context))
            {
                return;
            }

            var builder = new StringBuilder();

            if (!string.IsNullOrWhiteSpace(context))
            {
                builder.AppendLine($"Context: {context}");
                builder.AppendLine();
            }

            if (exception != null)
            {
                builder.AppendLine(
                    SanitizeException(exception));
            }

            File.WriteAllText(
                Path.Combine(
                    tempFolder,
                    "exception.txt"),
                builder.ToString(),
                Encoding.UTF8);
        }

        private static string SanitizeException(
            Exception exception)
        {
            if (exception == null)
                return string.Empty;

            string text = exception.ToString();

            string connectionString =
                ConnectionManager.DynamicConnectionString;

            if (!string.IsNullOrWhiteSpace(connectionString))
            {
                text = text.Replace(
                    connectionString,
                    "[CONNECTION_STRING_REDACTED]",
                    StringComparison.OrdinalIgnoreCase);
            }

            return text;
        }

        private static string GetVersion()
            => Assembly
                .GetEntryAssembly()?
                .GetName()
                .Version?
                .ToString()
                ?? "unknown";
    }
}
