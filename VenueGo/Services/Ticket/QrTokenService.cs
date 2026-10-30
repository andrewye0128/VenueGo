using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using VenueGo.Enums;
using VenueGo.Models.CheckinModels;
using VenueGo.Services.Time;

namespace VenueGo.Services.Ticket
{
    public class QrTokenService
    {
        //  QR 內容格式：v1.{ticketId}.{時間窗}.{簽章}
        // 驗證順序：格式 → 撈票 → 簽章 → 時間窗。
        private const string Version = "v1";
        private const int WindowSeconds = 300;   // 時間窗：30 秒一格
        private const int SignatureBytes = 16; // 簽章只取 16 bytes(128位元)

        private static readonly DateTime Epoch = new(1970, 1, 1);
        private readonly byte[] _key;
        private readonly ITimeService _time;
        private readonly CEntryTicketFactory _ticketFactory;

        public QrTokenService(IConfiguration config, ITimeService time, CEntryTicketFactory ticketFactory)
        {
            var key = config["QrSigning:Key"] ?? throw new InvalidOperationException("缺少 QrSigning:Key 設定");
            _key = Encoding.UTF8.GetBytes(key);
            _time = time;
            _ticketFactory = ticketFactory;
        }

        public (string Payload, DateTime ExpriesAt) Generate(int ticketId, string qrtoken)
        {
            long window = GetWindow(_time.Now);
            string signature = Sign(ticketId, qrtoken, window);
            // 不傳QRToken以存到簽章
            string payload = $"{Version}.{ticketId}.{window}.{signature}";
            // 到期時間為下一格
            DateTime expireAt = Epoch.AddSeconds((window + 1) * WindowSeconds);
            return (payload, expireAt);
        }

        public async Task<QrVerifyResult> VerifyAsync(string? payload)
        {
            // 以下照順序1.拆字串,檢查格式 -> 2.用拆出的 ticketId 去 DB 撈 Qrtoken, 3.用 Qrtoken 重算簽章比對 + 檢查時間窗
            if (!TryParse(payload, out int ticketId, out long window, out string signature))
            {
                return QrVerifyResult.Fail(QrVerifyFailReason.InvalidFormat);
            }
            string? qrtoken = await _ticketFactory.GetQrtokenAsync(ticketId);
            if(qrtoken == null)
            {
                return QrVerifyResult.Fail(QrVerifyFailReason.TicketNotFound);
            }
            return Verify(ticketId, window, signature, qrtoken);
        }

        //  拆解 QR 字串(公開先單元測試) => 這個方法回傳bool是否成功並帶出三個資料
        public bool TryParse(string? payload, out int ticketId, out long window, out string signature)
        {
            ticketId = 0;
            window = 0;
            signature = "";
            if (string.IsNullOrEmpty(payload))
            {
                return false;
            }
            var parts = payload.Split('.');
            if (parts.Length != 4) return false;
            if (parts[0] != Version) return false;
            if (!int.TryParse(parts[1], out ticketId) || ticketId <= 0) return false;
            if (!long.TryParse(parts[2], out window)) return false;
            if (string.IsNullOrEmpty(parts[3])) return false;

            signature = parts[3];
            return true;
        }

        //拿到 DB 的 Qrtoken 之後的驗證（簽章 → 時間窗），不碰資料庫(公開仙單元測試)
        public QrVerifyResult Verify(int ticketId, long window, string signature, string qrtoken)
        {
            // 用「固定時間比對」，不會因為比對快慢洩漏簽章資訊
            byte[] expected = Encoding.UTF8.GetBytes(Sign(ticketId, qrtoken, window));
            byte[] actual = Encoding.UTF8.GetBytes(signature);
            if (!CryptographicOperations.FixedTimeEquals(expected, actual))
                return QrVerifyResult.Fail(QrVerifyFailReason.InvalidSignature);

            // 只接受現在這一格和前一格
            long current = GetWindow(_time.Now);
            if (window > current || window < current - 1)
                return QrVerifyResult.Fail(QrVerifyFailReason.Expired);

            return QrVerifyResult.Ok(ticketId);
        }

        private static long GetWindow(DateTime now)
        {
            // 把現在時間 - 時間起點(要算第幾格) -> 轉為總秒數 / 設定秒數 -> 得到第幾格
            return (long)(now - Epoch).TotalSeconds / WindowSeconds;
        }

        private string Sign(int ticketId, string qrtoken, long window)
        {
            byte[] data = Encoding.UTF8.GetBytes($"{ticketId}.{qrtoken}.{window}");
            byte[] hash = HMACSHA256.HashData(_key, data);
            return Base64Url.EncodeToString(hash.AsSpan(0, SignatureBytes));
        }
    }
}
