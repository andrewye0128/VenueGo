using System.Security.Cryptography;
using System.Text;

namespace VenueGo.Helpers
{
    public static class PasswordHelper
    {
        private const int SaltSize = 16; // 128 bits
        private const int KeySize = 32;  // 256 bits
        private const int Iterations = 350000; // NIST 建議的最小疊代次數
        private static readonly HashAlgorithmName HashAlgorithm = HashAlgorithmName.SHA256;

        /// <summary>
        /// 密碼雜湊加密
        /// </summary>
        public static string HashPassword(string password)
        {
            // 1. 產生 16 bytes 的隨機 Salt
            byte[] salt = RandomNumberGenerator.GetBytes(SaltSize);

            // 2. 使用 PBKDF2 進行雜湊
            byte[] hash = Rfc2898DeriveBytes.Pbkdf2(
                Encoding.UTF8.GetBytes(password),
                salt,
                Iterations,
                HashAlgorithm,
                KeySize
            );

            // 3. 將 Salt 與 Hash 拼成字串儲存 (格式: Iterations.Salt.Hash)
            return $"{Iterations}.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(hash)}";
        }

        /// <summary>
        /// 驗證密碼是否正確 (未來登入時使用)
        /// </summary>
        public static bool VerifyPassword(string password, string hashedPassword)
        {
            var parts = hashedPassword.Split('.');
            if (parts.Length != 3)
            {
                return false; // 格式不符合
            }

            int iterations = int.Parse(parts[0]);
            byte[] salt = Convert.FromBase64String(parts[1]);
            byte[] hash = Convert.FromBase64String(parts[2]);

            // 拿使用者輸入的密碼與原本的 Salt 重新計算一次 Hash
            byte[] inputHash = Rfc2898DeriveBytes.Pbkdf2(
                Encoding.UTF8.GetBytes(password),
                salt,
                iterations,
                HashAlgorithm,
                hash.Length
            );

            // 比較兩者的 Hash 是否一致 (固定時間比較，防範 Timing Attack)
            return CryptographicOperations.FixedTimeEquals(hash, inputHash);
        }
    }
}