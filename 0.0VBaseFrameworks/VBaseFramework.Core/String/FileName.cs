using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace VBaseFramework.Core.String
{
    public static class PasswordHasher
    {
        private const int SaltSize = 16; // 128 bit
        private const int HashSize = 32; // 256 bit
        private const int Iterations = 100_000;

        public static string HashPassword(string password)
        {
            // 生成随机盐
            var salt = RandomNumberGenerator.GetBytes(SaltSize);

            // PBKDF2 生成哈希
            var hash = new Rfc2898DeriveBytes(password, salt, Iterations, HashAlgorithmName.SHA256).GetBytes(HashSize);

            // 输出格式：迭代次数.盐.Base64.哈希.Base64
            return $"{Iterations}.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(hash)}";
        }

        public static bool VerifyPassword(string password, string hashedPassword)
        {
            var parts = hashedPassword.Split('.');
            if (parts.Length != 3) return false;

            var iterations = int.Parse(parts[0]);
            var salt = Convert.FromBase64String(parts[1]);
            var hash = Convert.FromBase64String(parts[2]);

            var hashToCompare = new Rfc2898DeriveBytes(password, salt, iterations, HashAlgorithmName.SHA256).GetBytes(HashSize);

            // 安全比较
            return CryptographicOperations.FixedTimeEquals(hash, hashToCompare);
        }
    }
}
