// In SessionManager.cs

using Microsoft.Extensions.Configuration;
using System;
using System.IO;
using System.Linq;

namespace LoginAppFramework
{

    public class SessionUser
    {
        public string Username { get; set; }
        public string FullName { get; set; }
        public string ProfilePicture { get; set; }
    }

    public static class SessionManager
    {
        public static SessionUser CurrentUser { get; private set; }

        // This will now hold the single, consistent connection string for the entire session.
        public static string CurrentUserConnectionString { get; private set; }

        // THE LOGIN LOGIC IS NOW COMPLETELY DIFFERENT AND CORRECT FOR YOUR GOAL
        // In SessionManager.cs

        public static bool Login(string username, string password)
        {
            try
            {
                string serviceAccountConnectionString = ConnectionManager.GetDefaultConnectionString();
                var appUser = DataAccess.GetAppUserByUsername(username, serviceAccountConnectionString);
                if (appUser == null)
                {
                    return false;
                }
                if (PasswordHasher.VerifyPassword(appUser.PasswordHash, password))
                {
                    CurrentUserConnectionString = serviceAccountConnectionString;
                    CurrentUser = new SessionUser
                    {
                        Username = appUser.Username,
                        FullName = appUser.FullName
                        // The line for ProfilePicture has been removed.
                    };
                    return true;
                }
                return false;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public static void Logout()
        {
            CurrentUser = null;
            CurrentUserConnectionString = null;
            AppData.ClearData();
        }
    }
}