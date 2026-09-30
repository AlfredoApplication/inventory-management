using Microsoft.Data.SqlClient;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace LoginAppFramework
{
    public enum ConnectionHealthState
    {
        Unknown,
        Checking,
        Online,
        Offline
    }

    public static class ConnectionHealthService
    {
        private static readonly SemaphoreSlim CheckLock = new(1, 1);
        private static Timer _timer;
        private static bool _started;

        public static event EventHandler Changed;

        public static ConnectionHealthState DatabaseState { get; private set; }
            = ConnectionHealthState.Unknown;

        public static ConnectionHealthState HrState { get; private set; }
            = ConnectionHealthState.Unknown;

        public static DateTime? DatabaseCheckedAt { get; private set; }
        public static DateTime? HrCheckedAt { get; private set; }
        public static string DatabaseError { get; private set; }
        public static string HrError { get; private set; }

        public static void StartMonitoring()
        {
            if (_started)
                return;

            _started = true;

            _timer = new Timer(
                async _ => await CheckDatabaseAsync(),
                null,
                TimeSpan.Zero,
                TimeSpan.FromSeconds(45));
        }

        public static void StopMonitoring()
        {
            _timer?.Dispose();
            _timer = null;
            _started = false;
        }

        public static async Task<bool> CheckDatabaseAsync()
        {
            if (!await CheckLock.WaitAsync(0))
                return DatabaseState == ConnectionHealthState.Online;

            try
            {
                SetDatabaseState(
                    ConnectionHealthState.Checking,
                    null,
                    checkedAt: null);

                try
                {
                    string connectionString =
                        ConnectionManager.GetActiveConnectionString();

                    var builder =
                        new SqlConnectionStringBuilder(connectionString)
                        {
                            ConnectTimeout = 5
                        };

                    await using var connection =
                        new SqlConnection(builder.ConnectionString);

                    await connection.OpenAsync();

                    await using var command =
                        new SqlCommand("SELECT 1", connection)
                        {
                            CommandTimeout = 5
                        };

                    await command.ExecuteScalarAsync();

                    SetDatabaseState(
                        ConnectionHealthState.Online,
                        null,
                        DateTime.Now);

                    return true;
                }
                catch (Exception ex)
                {
                    SetDatabaseState(
                        ConnectionHealthState.Offline,
                        ex.Message,
                        DateTime.Now);

                    DiagnosticService.Log(
                        "Connection",
                        "Primary database health check failed.",
                        ex);

                    return false;
                }
            }
            finally
            {
                CheckLock.Release();
            }
        }

        public static void ReportHrSuccess()
        {
            HrState = ConnectionHealthState.Online;
            HrCheckedAt = DateTime.Now;
            HrError = null;
            RaiseChanged();
        }

        public static void ReportHrFailure(Exception exception)
        {
            HrState = ConnectionHealthState.Offline;
            HrCheckedAt = DateTime.Now;
            HrError = exception?.Message;

            DiagnosticService.Log(
                "Connection",
                "HR synchronization failed.",
                exception);

            RaiseChanged();
        }

        public static void ResetHrState()
        {
            HrState = ConnectionHealthState.Unknown;
            HrCheckedAt = null;
            HrError = null;
            RaiseChanged();
        }

        private static void SetDatabaseState(
            ConnectionHealthState state,
            string error,
            DateTime? checkedAt)
        {
            DatabaseState = state;
            DatabaseError = error;

            if (checkedAt.HasValue)
                DatabaseCheckedAt = checkedAt;

            RaiseChanged();
        }

        private static void RaiseChanged()
            => Changed?.Invoke(null, EventArgs.Empty);
    }
}
