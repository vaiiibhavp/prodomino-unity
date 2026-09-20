using System;
using System.Security.Cryptography;
using System.Text;

namespace HelperSharedLibrary
{
    public class SecurityHelper
    {
        /// <summary>
        /// Derives a 256-bit key using SHA256 from two parts.
        /// </summary>
        public static string DeriveKey(string partA, string partB)
        {
            using (SHA256 sha256 = SHA256.Create())
            {
                string combinedString = partA + partB;
                byte[] hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(combinedString));
                return Convert.ToBase64String(hash);
            }
        }

        /// <summary>
        /// Derives a 128-bit Initialization Vector (IV) using SHA256 from two parts.
        /// </summary>
        public static string DeriveIV(string partA, string partB)
        {
            using (SHA256 sha256 = SHA256.Create())
            {
                string combinedString = "IV-" + partA + partB;
                byte[] hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(combinedString));

                // AES IV must be 16 bytes
                Array.Resize(ref hash, 16);

                return Convert.ToBase64String(hash);
            }
        }

        /// <summary>
        /// Encrypts the given string using AES encryption and returns the cipher + IV in Base64.
        /// </summary>
        public static SecurityData EncryptData(string data, string derivedKey, string? derivedIV = null)
        {
            using (Aes aes = Aes.Create())
            {
                aes.Key = Convert.FromBase64String(derivedKey);

                // Use derived IV if provided, otherwise generate a random one
                if (!string.IsNullOrEmpty(derivedIV))
                    aes.IV = Convert.FromBase64String(derivedIV);
                else
                    aes.GenerateIV();

                using (ICryptoTransform encryptor = aes.CreateEncryptor(aes.Key, aes.IV))
                {
                    // Convert string to UTF-8 bytes
                    byte[] utf8Bytes = Encoding.UTF8.GetBytes(data);

                    // IMPORTANT: use utf8Bytes.Length, not data.Length (chars vs bytes!)
                    byte[] encryptedData = encryptor.TransformFinalBlock(utf8Bytes, 0, utf8Bytes.Length);

                    return new SecurityData(
                        Convert.ToBase64String(encryptedData),
                        Convert.ToBase64String(aes.IV)
                    );
                }
            }
        }

        /// <summary>
        /// Decrypts the given SecurityData using AES and returns the original string.
        /// </summary>
        public static string DecryptData(SecurityData securityData, string derivedKey)
        {
            using (Aes aes = Aes.Create())
            {
                aes.Key = Convert.FromBase64String(derivedKey);
                aes.IV = Convert.FromBase64String(securityData.ivBase64);

                using (ICryptoTransform decryptor = aes.CreateDecryptor(aes.Key, aes.IV))
                {
                    // Convert cipher text from Base64 to raw bytes
                    byte[] encryptedBytes = Convert.FromBase64String(securityData.encryptedData);

                    // Decrypt all bytes
                    byte[] decryptedData = decryptor.TransformFinalBlock(encryptedBytes, 0, encryptedBytes.Length);

                    // Convert back to UTF-8 string
                    return Encoding.UTF8.GetString(decryptedData);
                }
            }
        }
    }
}
