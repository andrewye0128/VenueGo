using Shouldly;
using VenueGo.Services.Payments;

namespace VenueGo.Tests.Services.Payments
{
    /// <summary>
    /// EcpayCheckMac（綠界的防偽章 CheckMacValue）的單元測試。
    /// <para>
    /// 【為什麼要測】防偽章算錯一個字，綠界就會拒絕付款、我們也會把綠界的通知當成假的。
    /// 規則很多（排序不分大小寫、特殊的網址編碼、轉小寫、SHA256 轉大寫），改程式時很容易改壞。
    /// </para>
    /// <para>
    /// 【預期值從哪裡來】不能拿 EcpayCheckMac 自己算出來的值當答案（那樣永遠會通過）。
    /// 下面的 Expected 值，是用另一個獨立寫的程式（Node.js）照綠界規則算出來的，
    /// 那個程式曾經用來模擬綠界通知，後端有接受它算的防偽章。
    /// </para>
    /// <para>
    /// 【金鑰】HashKey、HashIV 是隨便編的測試值，不是綠界或我們的設定，放進 git 也沒關係。
    /// </para>
    /// </summary>
    public class EcpayCheckMacTests
    {
        // ════════════════════════════════════════════════════════
        //  共用的測試資料
        // ════════════════════════════════════════════════════════

        private const string HashKey = "TestHashKey123";
        private const string HashIV = "TestHashIV456";

        /// <summary>
        /// 一張典型的綠界付款單。刻意包含：空白、中文、/ : 這些需要編碼的字。
        /// 每次呼叫都回傳新的 Dictionary，測試改了內容不會影響其他測試。
        /// </summary>
        private static Dictionary<string, string> CreateOrderFields() => new()
        {
            ["MerchantID"] = "3002607",
            ["MerchantTradeNo"] = "VG261112090503ABC123",
            ["MerchantTradeDate"] = "2026/11/12 09:05:03",
            ["PaymentType"] = "aio",
            ["TotalAmount"] = "900",
            ["TradeDesc"] = "VenueGo 場地預約",
            ["ItemName"] = "羽球場-A 2026/11/15 16:00-18:00",
            ["ReturnURL"] = "https://example.com/api/payments/ecpay/return",
            ["ChoosePayment"] = "Credit",
            ["EncryptType"] = "1",
        };

        /// <summary>CreateOrderFields() 的正確防偽章（由獨立程式算出）</summary>
        private const string ExpectedOrderMac = "C4D66FCDF597E2BBB0A1E39C61C7E59AF834C272DF68A8FC5EE62CB3BC9E42EE";

        /// <summary>付款單加上正確的防偽章，模擬「綠界送來的通知」</summary>
        private static Dictionary<string, string> CreateSignedFields()
        {
            var fields = CreateOrderFields();
            fields[EcpayCheckMac.FieldName] = ExpectedOrderMac;
            return fields;
        }

        // ════════════════════════════════════════════════════════
        //  Create：產生防偽章
        // ════════════════════════════════════════════════════════

        [Fact]
        public void Create_典型的付款單_算出跟獨立程式一樣的防偽章()
        {
            var mac = EcpayCheckMac.Create(CreateOrderFields(), HashKey, HashIV);

            mac.ShouldBe(ExpectedOrderMac);
        }

        [Fact]
        public void Create_欄位順序打亂_結果一樣()
        {
            // 反過來排：程式應該自己依欄位名稱排序，跟傳進來的順序無關
            var reversed = CreateOrderFields().Reverse();

            var mac = EcpayCheckMac.Create(reversed, HashKey, HashIV);

            mac.ShouldBe(ExpectedOrderMac);
        }

        [Fact]
        public void Create_欄位名稱大小寫混雜_排序不分大小寫()
        {
            // 綠界規定排序不分大小寫：a、b、C。
            // 如果寫錯成分大小寫的排序，大寫字母會全部排在小寫前面，變成 C、a、b，防偽章就不一樣
            // （刻意讓大寫的 C 排最後：如果大寫排第一，兩種排序結果一樣，就測不出差別）
            var fields = new Dictionary<string, string>
            {
                ["b"] = "2",
                ["C"] = "3",
                ["a"] = "1",
            };

            var mac = EcpayCheckMac.Create(fields, HashKey, HashIV);

            mac.ShouldBe("7BB54F658EE3EA3C838C96E1AE3682B5DFA051F294B36EBAFBA11189CB5E7E20");
        }

        [Fact]
        public void Create_特殊符號_照綠界的編碼規則()
        {
            // 綠界的編碼規則：空白變成 +；只有英數字和 - _ . ! * ( ) 不編碼；
            // ' ~ @ # % & = + / ? 都要編碼。寫錯其中一個，防偽章就會不一樣
            var fields = new Dictionary<string, string>
            {
                ["Note"] = "a-b_c.d!e*f(g)h i'j~k@#%&=+/?",
            };

            var mac = EcpayCheckMac.Create(fields, HashKey, HashIV);

            mac.ShouldBe("CDDD81AC280FBC4F430E8436FB3CA0CE14064B601C767095B18CC4F38FD3D2CB");
        }

        [Fact]
        public void Create_輸入裡已經有CheckMacValue_不會算進去()
        {
            // 驗證綠界的通知時，收到的欄位裡本來就有 CheckMacValue，計算時一定要排除它
            var fields = CreateOrderFields();
            fields[EcpayCheckMac.FieldName] = "ANYTHING";

            var mac = EcpayCheckMac.Create(fields, HashKey, HashIV);

            mac.ShouldBe(ExpectedOrderMac);
        }

        // ════════════════════════════════════════════════════════
        //  Verify：檢查收到的防偽章對不對
        // ════════════════════════════════════════════════════════

        [Fact]
        public void Verify_防偽章正確_回傳True()
        {
            EcpayCheckMac.Verify(CreateSignedFields(), HashKey, HashIV).ShouldBeTrue();
        }

        [Fact]
        public void Verify_防偽章是小寫_也回傳True()
        {
            // 比對前會轉成大寫，所以綠界送小寫也接受
            var fields = CreateSignedFields();
            fields[EcpayCheckMac.FieldName] = ExpectedOrderMac.ToLowerInvariant();

            EcpayCheckMac.Verify(fields, HashKey, HashIV).ShouldBeTrue();
        }

        [Fact]
        public void Verify_金額被竄改_回傳False()
        {
            // 有人攔截通知，把 900 元改成 1 元：防偽章對不上，要被擋下來
            var fields = CreateSignedFields();
            fields["TotalAmount"] = "1";

            EcpayCheckMac.Verify(fields, HashKey, HashIV).ShouldBeFalse();
        }

        [Fact]
        public void Verify_HashKey不同_回傳False()
        {
            // 用別人的金鑰簽的通知（例如別家商店的），不能通過
            EcpayCheckMac.Verify(CreateSignedFields(), "OtherHashKey", HashIV).ShouldBeFalse();
        }

        [Theory]
        [InlineData(null)]   // 根本沒有 CheckMacValue 欄位
        [InlineData("")]     // 有欄位但是空的
        public void Verify_沒有防偽章_回傳False(string? receivedMac)
        {
            var fields = CreateOrderFields();
            if (receivedMac is not null)
            {
                fields[EcpayCheckMac.FieldName] = receivedMac;
            }

            EcpayCheckMac.Verify(fields, HashKey, HashIV).ShouldBeFalse();
        }
    }
}