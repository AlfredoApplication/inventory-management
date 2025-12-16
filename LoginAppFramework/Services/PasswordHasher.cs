using System;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Cryptography.KeyDerivation;

namespace LoginAppFramework
{
    public static class PasswordHasher
    {
        public static string HashPassword(string password)
        {
            byte[] salt = RandomNumberGenerator.GetBytes(128 / 8); // 16 bytes
            string hashed = Convert.ToBase64String(KeyDerivation.Pbkdf2(
                password: password,
                salt: salt,
                prf: KeyDerivationPrf.HMACSHA256,
                iterationCount: 100000,
                numBytesRequested: 256 / 8));
            return $"{Convert.ToBase64String(salt)}:{hashed}";
        }

        public static bool VerifyPassword(string hashedPasswordWithSalt, string password)
        {
            if (string.IsNullOrEmpty(hashedPasswordWithSalt) || string.IsNullOrEmpty(password))
            {
                return false;
            }
            try
            {
                var parts = hashedPasswordWithSalt.Split(':', 2);
                if (parts.Length != 2) return false;
                byte[] salt = Convert.FromBase64String(parts[0]);
                string expectedHash = parts[1];
                string actualHashed = Convert.ToBase64String(KeyDerivation.Pbkdf2(
                    password: password,
                    salt: salt,
                    prf: KeyDerivationPrf.HMACSHA256,
                    iterationCount: 100000,
                    numBytesRequested: 256 / 8));
                return CryptographicOperations.FixedTimeEquals(
                    Convert.FromBase64String(expectedHash),
                    Convert.FromBase64String(actualHashed));
            }
            catch { return false; }
        }
    }
}