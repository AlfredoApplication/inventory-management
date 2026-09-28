namespace LoginAppFramework
{
    public class AppUser
    {
        public int Id { get; set; }
        public string Username { get; set; }
        public string PasswordHash { get; set; }
        public string FullName { get; set; }
        public string EmployeeCode { get; set; }
        public string Role { get; set; } = "Admin"; // Default to Admin for backward compatibility
    }
}