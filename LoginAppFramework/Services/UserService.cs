using System;
using System.Collections.Generic;
using System.Linq;

namespace LoginAppFramework
{
    public interface IUserService
    {
        List<AppUser> GetAll();
        void Save(AppUser user, string newPassword);
        void Delete(AppUser user);
    }

    public sealed class UserService : IUserService
    {
        private readonly IAuthorizationService _authorization;

        public UserService(IAuthorizationService authorization = null)
        {
            _authorization = authorization ?? AppServices.Authorization;
        }
        public List<AppUser> GetAll()
        {
            _authorization.RequireManageUsers();
            return DataAccess.GetAllAppUsers()
                .OrderBy(u => u.Username)
                .ToList();
        }

        public void Save(AppUser user, string newPassword)
        {
            _authorization.RequireManageUsers();

            if (user == null)
                throw new ArgumentNullException(nameof(user));

            user.Username = user.Username?.Trim();

            if (string.IsNullOrWhiteSpace(user.Username))
                throw new InvalidOperationException("İstifadəçi adı tələb olunur.");

            if (user.Role is not ("Admin" or "ReadOnly"))
                throw new InvalidOperationException("Etibarlı rol seçilməlidir.");

            var allUsers = DataAccess.GetAllAppUsers();

            bool duplicateUsername = allUsers.Any(existing =>
                existing.Id != user.Id &&
                string.Equals(
                    existing.Username,
                    user.Username,
                    StringComparison.OrdinalIgnoreCase));

            if (duplicateUsername)
                throw new InvalidOperationException(
                    $"'{user.Username}' adlı istifadəçi artıq mövcuddur.");

            if (user.Role != "Admin")
            {
                bool anotherAdminExists = allUsers.Any(existing =>
                    existing.Id != user.Id &&
                    string.Equals(existing.Role, "Admin", StringComparison.OrdinalIgnoreCase));

                if (!anotherAdminExists)
                {
                    throw new InvalidOperationException(
                        "Sistem ən azı bir Admin istifadəçisi tələb edir. Əvvəlcə başqa Admin yaradın.");
                }
            }

            if (user.Id == 0 && string.IsNullOrWhiteSpace(newPassword))
                throw new InvalidOperationException("Yeni istifadəçi üçün şifrə tələb olunur.");

            if (!string.IsNullOrWhiteSpace(newPassword))
                user.PasswordHash = PasswordHasher.HashPassword(newPassword);

            DataAccess.SaveAppUser(user);
        }

        public void Delete(AppUser user)
        {
            _authorization.RequireManageUsers();

            if (user == null)
                throw new ArgumentNullException(nameof(user));

            var allUsers = DataAccess.GetAllAppUsers();

            if (string.Equals(user.Role, "Admin", StringComparison.OrdinalIgnoreCase))
            {
                int otherAdminCount = allUsers.Count(existing =>
                    existing.Id != user.Id &&
                    string.Equals(existing.Role, "Admin", StringComparison.OrdinalIgnoreCase));

                if (otherAdminCount == 0)
                {
                    throw new InvalidOperationException(
                        "Son Admin istifadəçisini silmək olmaz.");
                }
            }

            DataAccess.DeleteAppUser(user);
        }

    }
}
