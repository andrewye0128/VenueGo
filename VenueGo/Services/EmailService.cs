using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Options;
using VenueGo.Models;
using VenueGo.Services;
namespace VenueGo.Services
{
    public class EmailService : IEmailService
    {
        private readonly SmtpSettings _smtpSettings;

        public EmailService(IOptions<SmtpSettings> smtpSettings)
        {
            _smtpSettings = smtpSettings.Value;
        }

        public async Task SendEmailAsync(string toEmail, string subject, string bodyHtml)
        {
            // 建立信件內容
            using var message = new MailMessage
            {
                From = new MailAddress(_smtpSettings.SenderEmail, _smtpSettings.SenderName),
                Subject = subject,
                Body = bodyHtml,
                IsBodyHtml = true // 指定使用 HTML 格式
            };
            message.To.Add(toEmail);

            // 設定 SMTP 客戶端
            using var smtpClient = new SmtpClient(_smtpSettings.Host, _smtpSettings.Port)
            {
                EnableSsl = _smtpSettings.EnableSsl,
                Credentials = new NetworkCredential(_smtpSettings.Username, _smtpSettings.Password)
            };

            // 異步發送信件
            await smtpClient.SendMailAsync(message);
        }
    }
}