using Microsoft.Data.SqlClient;
using System;

namespace LoginAppFramework
{
    public enum LoginFailureReason
    {
        None,
        InvalidCredentials,
        ConfigurationMissing,
        DatabaseUnavailable,
        UnexpectedError
    }

    public sealed class LoginResult
    {
        public bool Success { get; init; }
        public LoginFailureReason FailureReason { get; init; }
        public string Message { get; init; }

        public static LoginResult Ok()
            => new()
            {
                Success = true,
                FailureReason = LoginFailureReason.None
            };

        public static LoginResult Fail(
            LoginFailureReason reason,
            string message)
            => new()
            {
                Success = false,
                FailureReason = reason,
                Message = message
            };
    }

    public class SessionUser
    {
        public string Username { get; set; }
        public string FullName { get; set; }
        public string ProfilePicture { get; set; }
        public string Role { get; set; }
    }

    public static class SessionManager
    {
        public static SessionUser CurrentUser { get; private set; }
        public static string CurrentUserConnectionString { get; private set; }

        public static LoginResult Login(string username, string password)
        {
            if (string.IsNullOrWhiteSpace(username) ||
                string.IsNullOrEmpty(password))
            {
                return LoginResult.Fail(
                    LoginFailureReason.InvalidCredentials,
                    "İstifadəçi adı və şifrəni daxil edin.");
            }

            try
            {
                string connectionString =
                    ConnectionManager.GetActiveConnectionString();

                var appUser =
                    DataAccess.GetAppUserByUsername(username, connectionString);

                if (appUser == null ||
                    !PasswordHasher.VerifyPassword(appUser.PasswordHash, password))
                {
                    return LoginResult.Fail(
                        LoginFailureReason.InvalidCredentials,
                        "İstifadəçi adı və ya şifrə yanlışdır.");
                }

                CurrentUserConnectionString = connectionString;
                CurrentUser = new SessionUser
                {
                    Username = appUser.Username,
                    FullName = appUser.FullName,
                    Role = appUser.Role ?? "Admin"
                };

                return LoginResult.Ok();
            }
            catch (InvalidOperationException ex)
            {
                return LoginResult.Fail(
                    LoginFailureReason.ConfigurationMissing,
                    ex.Message);
            }
            catch (SqlException)
            {
                return LoginResult.Fail(
                    LoginFailureReason.DatabaseUnavailable,
                    "Verilənlər bazasına qoşulmaq mümkün olmadı. Server bağlantısını yoxlayın.");
            }
            catch
            {
                return LoginResult.Fail(
                    LoginFailureReason.UnexpectedError,
                    "Daxil olma zamanı gözlənilməz xəta baş verdi.");
            }
        }

        public static bool IsAdmin()
            => CurrentUser?.Role == "Admin";

        public static bool IsReadOnly()
            => CurrentUser?.Role == "ReadOnly";

        public static bool CanEdit()
            => IsAdmin();

        public static bool CanDelete()
            => IsAdmin();

        public static bool CanManageUsers()
            => IsAdmin();

        public static void Logout()
        {
            CurrentUser = null;
            CurrentUserConnectionString = null;
            AppData.ClearData();
        }
    }
}
