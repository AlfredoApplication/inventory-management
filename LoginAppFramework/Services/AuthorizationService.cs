using System;

namespace LoginAppFramework
{
    public interface IAuthorizationService
    {
        void RequireEdit();
        void RequireDelete();
        void RequireManageUsers();
    }

    public sealed class AuthorizationService : IAuthorizationService
    {
        public void RequireEdit()
        {
            if (!SessionManager.CanEdit())
                throw new UnauthorizedAccessException(
                    "Bu əməliyyat üçün redaktə icazəsi tələb olunur.");
        }

        public void RequireDelete()
        {
            if (!SessionManager.CanDelete())
                throw new UnauthorizedAccessException(
                    "Bu əməliyyat üçün silmə icazəsi tələb olunur.");
        }

        public void RequireManageUsers()
        {
            if (!SessionManager.CanManageUsers())
                throw new UnauthorizedAccessException(
                    "İstifadəçi idarəetməsi üçün Admin icazəsi tələb olunur.");
        }
    }
}
