namespace VenueGo.Dtos
{
    public class SmtpSettings
    {
        public string Host { get; set; } = string.Empty;
        public int Port { get; set; }
        public string SenderName { get; set; } = string.Empty;
        public string SenderEmail { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        //SMTP 電子郵件伺服器的登入密碼
        //當會員忘記密碼時，系統未來可能會寄送密碼重設連結。
        //你的 EmailService 需要連線到郵件伺服器，伺服器可能要求帳號密碼來確認寄件者的身分。
        //目前只測試忘記密碼流程： 如果還沒串接真正的郵件服務，可以先保留這個類別，Password 暫時留空
        //未來要正式寄信： 如果 SMTP 服務要求驗證，就需要設定正確的登入憑證。有些服務使用應用程式專用密碼，而不是一般電子郵件帳號的登入密碼。
        //使用不需要帳密的 SMTP 服務： 可能不需要填寫 Username 和 Password，依服務商設定而定。
        public string Password { get; set; } = string.Empty;
        public bool EnableSsl { get; set; } = true;
    }
}