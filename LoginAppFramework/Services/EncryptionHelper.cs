using System;
using System.Security.Cryptography;
using System.Text;

namespace LoginAppFramework
{
    public static class EncryptionHelper
    {
        // This entropy is an extra piece of "salt" to make the encryption more secure.
        // It's not a secret, but should be unique to your application.
        private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("InventoryApp-Unique-Entropy-String");

        public static string Encrypt(string plainText)
        {
            if (string.IsNullOrEmpty(plainText))
                return string.Empty;

            // Convert the plaintext password to a byte array.
            byte[] dataToProtect = Encoding.UTF8.GetBytes(plainText);

            // Use the built-in Windows Data Protection API (DPAPI).
            // This is the most secure and simplest way for a desktop application.
            // DataProtectionScope.CurrentUser means only the current Windows user on this machine can decrypt it.
            byte[] protectedData = ProtectedData.Protect(dataToProtect, Entropy, DataProtectionScope.CurrentUser);

            // Convert the encrypted byte array to a Base64 string for easy storage in your JSON file.
            return Convert.ToBase64String(protectedData);
        }

        public static string Decrypt(string cipherText)
        {
            if (string.IsNullOrEmpty(cipherText))
                return string.Empty;

            try
            {
                // Convert the Base64 string back to a byte array.
                byte[] protectedData = Convert.FromBase64String(cipherText);

                // Use DPAPI to decrypt the data. This will only work if the
                // same Windows user who encrypted it is running the application on the same machine.
                byte[] data = ProtectedData.Unprotect(protectedData, Entropy, DataProtectionScope.CurrentUser);

                // Convert the decrypted byte array back to a plaintext string.
                return Encoding.UTF8.GetString(data);
            }
            catch
            {
                // If anything fails (e.g., trying to open on a different PC or with a different user),
                // it will fail safely and return an empty string.
                return string.Empty;
            }
        }
    }
}