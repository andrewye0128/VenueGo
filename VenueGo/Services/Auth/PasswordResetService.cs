using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;
using VenueGo.Data;
using VenueGo.Helpers;
using VenueGo.Models.Constants;
using VenueGo.Models.Entities;

namespace VenueGo.Services.Auth
{
    public class PasswordResetService : IPasswordResetService
    {
        private readonly dbVenueContext _db;
        private readonly ITimeService _time;

        // PasswordResetTokens.IpAddress 欄位長度上限（45，剛好容納一個完整的 IPv6 文字位址）
        private const int MaxIpAddressLength = 45;

        public PasswordResetService(dbVenueContext db, ITimeService time)
        {
            _db = db;
            _time = time;
        }

        public async Task<string?> CreateResetTokenAsync(
            string email,
            string ipAddress)
        {
            var normalizedEmail = email.Trim().ToLowerInvariant();

            var user = await _db.Users
                .FirstOrDefaultAsync(u =>
                    u.Email.ToLower() == normalizedEmail &&
                    u.Status == UserStatuses.Active);

            // 帳號不存在時，不回傳 Token
            if (user == null)
            {
                return null;
            }

            // 產生原始 Token
            string rawToken = Guid.NewGuid().ToString("N");

            // SHA256 Hash 後才存入資料庫
            using var sha256 = SHA256.Create();

            byte[] hashBytes = sha256.ComputeHash(
                Encoding.UTF8.GetBytes(rawToken)
            );

            string tokenHash = Convert.ToBase64String(hashBytes);

            var resetToken = new PasswordResetToken
            {
                UserId = user.UserId,
                TokenHash = tokenHash,
                IpAddress = ipAddress.Length > MaxIpAddressLength ? ipAddress[..MaxIpAddressLength] : ipAddress,
                ExpiresAt = _time.Now.AddMinutes(30),
                UsedAt = null,
                CreatedAt = _time.Now
            };

            // [補強] 同一個帳號重複申請時，讓先前還沒用過的連結全部失效，同一時間只有「最新一封」有效。
            // 原本每申請一次就多一組 30 分鐘內都能用的連結，被盜用的風險面也跟著放大。
            // （沒有「已撤銷」欄位，所以沿用 UsedAt 標記為已失效。）
            var now = _time.Now;
            var staleTokens = await _db.PasswordResetTokens
                .Where(t => t.UserId == user.UserId && t.UsedAt == null && t.ExpiresAt > now)
                .ToListAsync();
            foreach (var stale in staleTokens) stale.UsedAt = now;

            _db.PasswordResetTokens.Add(resetToken);

            await _db.SaveChangesAsync();

            return rawToken;
        }

        public async Task<bool> ValidateResetTokenAsync(
    string email,
    string rawToken)
        {
            var normalizedEmail = email.Trim().ToLowerInvariant();

            var user = await _db.Users
                .FirstOrDefaultAsync(u =>
                    u.Email.ToLower() == normalizedEmail &&
                    u.Status == UserStatuses.Active);

            if (user == null)
            {
                return false;
            }

            using var sha256 = SHA256.Create();

            byte[] hashBytes = sha256.ComputeHash(
                Encoding.UTF8.GetBytes(rawToken)
            );

            string tokenHash = Convert.ToBase64String(hashBytes);

            var tokenRecord = await _db.PasswordResetTokens
                .FirstOrDefaultAsync(t =>
                    t.UserId == user.UserId &&
                    t.TokenHash == tokenHash);

            if (tokenRecord == null)
            {
                return false;
            }

            if (tokenRecord.UsedAt != null)
            {
                return false;
            }

            if (tokenRecord.ExpiresAt < _time.Now)
            {
                return false;
            }

            return true;
        }

        public async Task<bool> ResetPasswordAsync(
    string email,
    string rawToken,
    string newPassword)
        {
            var normalizedEmail = email.Trim().ToLowerInvariant();

            var user = await _db.Users
                .FirstOrDefaultAsync(u =>
                    u.Email.ToLower() == normalizedEmail &&
                    u.Status == UserStatuses.Active);

            if (user == null)
            {
                return false;
            }

            using var sha256 = SHA256.Create();

            byte[] hashBytes = sha256.ComputeHash(
                Encoding.UTF8.GetBytes(rawToken)
            );

            string tokenHash = Convert.ToBase64String(hashBytes);

            var tokenRecord = await _db.PasswordResetTokens
                .FirstOrDefaultAsync(t =>
                    t.UserId == user.UserId &&
                    t.TokenHash == tokenHash);

            if (tokenRecord == null ||
                tokenRecord.UsedAt != null ||
                tokenRecord.ExpiresAt < _time.Now)
            {
                return false;
            }

            // 更新密碼
            user.PasswordHash = PasswordHelper.HashPassword(newPassword);

            // [修正] 重設密碼不應該順便解除「帳號鎖定」。
            // 原本這裡無條件把 FailedLoginCount 歸零、LockedUntil 清空，結果：
            // 密碼錯 5 次被鎖 15 分鐘 → 按「忘記密碼」→ 重設密碼 → 鎖定立刻消失，
            // 鎖定機制等於形同虛設（暴力猜密碼被鎖住後，只要走一次重設流程就能繼續）。
            // 現在：鎖定中的帳號維持鎖定，直到 LockedUntil 到期（AuthenticationService 在到期後會自動歸零）；
            // 沒有被鎖定的帳號，才順便把累積的失敗次數歸零。
            bool isCurrentlyLocked = user.LockedUntil.HasValue && user.LockedUntil.Value > _time.Now;
            if (!isCurrentlyLocked)
            {
                user.FailedLoginCount = 0;
                user.LockedUntil = null;
            }
            user.UpdatedAt = _time.Now;

            // Token 標記為已使用
            tokenRecord.UsedAt = _time.Now;

            // [補強] 密碼已經重設，這個帳號其他還沒用過的重設連結一併失效，
            // 避免舊連結（例如早先申請、可能已外流的那一封）之後還能再把密碼改掉。
            var otherTokens = await _db.PasswordResetTokens
                .Where(t => t.UserId == user.UserId && t.UsedAt == null && t.TokenId != tokenRecord.TokenId)
                .ToListAsync();
            foreach (var other in otherTokens) other.UsedAt = _time.Now;

            await _db.SaveChangesAsync();

            return true;
        }
    }
}