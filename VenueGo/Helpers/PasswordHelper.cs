using System.Security.Cryptography;
using System.Text;

namespace VenueGo.Helpers
{
    /// <summary>
    /// 密碼雜湊與驗證工具類別
    /// 使用安全的 PBKDF2 (Password-Based Key Derivation Function 2) 演算法處理密碼
    /// </summary>
    public static class PasswordHelper
    {
        // 定義鹽值 (Salt) 長度為 16 位元組 (128 bits)，大幅提高碰撞與彩虹表破解難度
        private const int SaltSize = 16;

        // 定義最終產生的雜湊值 (Hash) 長度為 32 位元組 (256 bits)
        private const int KeySize = 32;

        // 疊代次數 (Iterations)：決定計算耗時。350,000 次符合美國 NIST 針對 PBKDF2-HMAC-SHA256 的安全建議標準
        private const int Iterations = 350000;

        // 指定底層產生的雜湊演算法為 SHA-256
        private static readonly HashAlgorithmName HashAlgorithm = HashAlgorithmName.SHA256;

        /// <summary>
        /// 對明文密碼進行「加鹽」與「拉長計算時間」的雜湊處理
        /// </summary>
        /// <param name="password">使用者輸入的明文密碼</param>
        /// <returns>格式化後的雜湊結果字串 (格式: 疊代次數.Base64鹽值.Base64雜湊值)</returns>
        public static string HashPassword(string password)
        {
            // 1. 使用密碼學安全的隨機數產生器 (CSPRNG) 生成 16 位元組的獨一無二隨機鹽值 (Salt)
            byte[] salt = RandomNumberGenerator.GetBytes(SaltSize);

            // 2. 使用 PBKDF2 演算法加上 Salt 反覆疊代計算出最終密碼雜湊值
            byte[] hash = Rfc2898DeriveBytes.Pbkdf2(
                Encoding.UTF8.GetBytes(password),
                salt,
                Iterations,
                HashAlgorithm,
                KeySize
            );

            // 3. 將計算參數 (Iterations)、鹽值 (Salt) 與雜湊值 (Hash) 以點號相連，打包儲存於資料庫中
            return $"{Iterations}.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(hash)}";
        }

        /// <summary>
        /// 驗證使用者輸入的明文密碼與資料庫中的雜湊字串是否相符
        /// </summary>
        /// <param name="password">登入時使用者輸入的明文密碼</param>
        /// <param name="hashedPassword">資料庫中儲存的雜湊字串 (格式: 疊代次數.Salt.Hash)</param>
        /// <returns>若密碼一致傳回 true，否則傳回 false</returns>
        public static bool VerifyPassword(string password, string hashedPassword)
        {
            // 1. 將儲存的字串依據 '.' 拆解出三個部分：疊代次數、鹽值與原始雜湊值
            var parts = hashedPassword.Split('.');
            if (parts.Length != 3)
            {
                return false; // 格式不符合即驗證失敗
            }

            // 2. 解析還原出原本儲存的疊代次數、Salt 位元組陣列與 Hash 位元組陣列
            int iterations = int.Parse(parts[0]);
            byte[] salt = Convert.FromBase64String(parts[1]);
            byte[] hash = Convert.FromBase64String(parts[2]);

            // 3. 拿使用者剛輸入的明文密碼，配合原本提取出來的 Salt 與 Iterations 重新計算一次 Hash
            byte[] inputHash = Rfc2898DeriveBytes.Pbkdf2(
                Encoding.UTF8.GetBytes(password),
                salt,
                iterations,
                HashAlgorithm,
                hash.Length
            );

            // 4. 使用 CryptographicOperations.FixedTimeEquals 進行固定時間比較
            // 確保比較時間不因前半段字串吻合狀況不同而改變，有效防止時脈攻擊 (Timing Attack)
            return CryptographicOperations.FixedTimeEquals(hash, inputHash);
        }
    }
}